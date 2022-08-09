namespace MassTransit.AmazonSqsTransport.Middleware
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Internals;
    using MassTransit.Middleware;
    using Transports;
    using Util;


    /// <summary>
    /// Receives messages from AmazonSQS, pushing them to the InboundPipe of the service endpoint.
    /// </summary>
    public sealed class AmazonSqsMessageReceiver :
        Agent,
        DeliveryMetrics
    {
        readonly ClientContext _client;
        readonly SqsReceiveEndpointContext _context;
        readonly TaskCompletionSource<bool> _deliveryComplete;
        readonly IReceivePipeDispatcher _dispatcher;
        readonly ReceiveSettings _receiveSettings;
        readonly MessagesHandler _messagesHandler;

        /// <summary>
        /// The basic consumer receives messages pushed from the broker.
        /// </summary>
        /// <param name="client">The model context for the consumer</param>
        /// <param name="context">The topology</param>
        public AmazonSqsMessageReceiver(ClientContext client, SqsReceiveEndpointContext context)
        {
            _client = client;
            _context = context;

            _receiveSettings = client.GetPayload<ReceiveSettings>();

            _deliveryComplete = TaskUtil.GetTask<bool>();

            _dispatcher = context.CreateReceivePipeDispatcher();
            _dispatcher.ZeroActivity += HandleDeliveryComplete;

            _messagesHandler = new MessagesHandler(client, context, _dispatcher);

            var task = Task.Run(Consume);
            SetCompleted(task);
        }

        long DeliveryMetrics.DeliveryCount => _dispatcher.DispatchCount;
        int DeliveryMetrics.ConcurrentDeliveryCount => _dispatcher.MaxConcurrentDispatchCount;

        async Task Consume()
        {
            var executor = new ChannelExecutor(_receiveSettings.PrefetchCount, _receiveSettings.ConcurrentMessageLimit);

            await GetQueueAttributes().ConfigureAwait(false);

            using var algorithm = new RequestRateAlgorithm(new RequestRateAlgorithmOptions()
            {
                PrefetchCount = _receiveSettings.PrefetchCount,
                RequestResultLimit = 10
            });

            var window = new Window(_receiveSettings.PrefetchCount);

            SetReady();

            try
            {
                while (!IsStopping)
                {
                    await window.WaitForOpen();

                    var messages = await ReceiveMessages(window.RequestsToReceive, new CancellationToken()).ConfigureAwait(false);

                    window.Close(messages.Count());

                    if (_receiveSettings.IsOrdered)
                    {
                        await algorithm.Run(ReceiveMessages, (m, t) => executor.Push(() => HandleMessage(m), t), GroupMessages, OrderMessages, Stopping)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        foreach(var message in messages)
                        {
                            _messagesHandler.Run(message, () => window.Open());
                        }

                        //await algorithm.Run(ReceiveMessages, (m, t) => executor.Push(() => HandleMessage(m), t), Stopping).ConfigureAwait(false);
                        //await algorithm.Run(ReceiveMessages, (m, t) => HandleMessage(m), Stopping).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException exception) when (exception.CancellationToken == Stopping)
            {
            }
            catch (Exception exception)
            {
                LogContext.Error?.Log(exception, "Consume Loop faulted");
                throw;
            }
            finally
            {
                await executor.DisposeAsync().ConfigureAwait(false);
            }
        }

        protected override async Task StopAgent(StopContext context)
        {
            LogContext.Debug?.Log("Stopping consumer: {InputAddress}", _context.InputAddress);

            SetCompleted(ActiveAndActualAgentsCompleted(context));

            await Completed.ConfigureAwait(false);
        }

        async Task GetQueueAttributes()
        {
            var queueInfo = await _client.GetQueueInfo(_receiveSettings.EntityName).ConfigureAwait(false);

            _receiveSettings.QueueUrl = queueInfo.Url;

            if (queueInfo.Attributes.TryGetValue(QueueAttributeName.VisibilityTimeout, out var value)
                && int.TryParse(value, out var visibilityTimeout)
                && visibilityTimeout != _receiveSettings.VisibilityTimeout)
            {
                LogContext.Debug?.Log("Using queue visibility timeout of {VisibilityTimeout}", TimeSpan.FromSeconds(visibilityTimeout).ToFriendlyString());

                _receiveSettings.VisibilityTimeout = visibilityTimeout;
            }
        }

        async Task HandleMessage(Message message)
        {
            if (IsStopping)
                return;

            var redelivered = message.Attributes.TryGetInt("ApproximateReceiveCount", out var receiveCount) && receiveCount > 1;

            var context = new AmazonSqsReceiveContext(message, redelivered, _context, _client, _receiveSettings, _client.ConnectionContext);
            try
            {
                await _dispatcher.Dispatch(context, context).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                context.LogTransportFaulted(exception);
            }
            finally
            {
                context.Dispose();
            }
        }

        static IEnumerable<IGrouping<string, Message>> GroupMessages(IEnumerable<Message> messages)
        {
            return messages.GroupBy(x => x.Attributes.TryGetValue(MessageSystemAttributeName.MessageGroupId, out var groupId) ? groupId : "");
        }

        static IEnumerable<Message> OrderMessages(IEnumerable<Message> messages)
        {
            return messages.OrderBy(x => x.Attributes.TryGetValue("SequenceNumber", out var sequenceNumber) ? sequenceNumber : "",
                SequenceNumberComparer.Instance);
        }

        async Task<IEnumerable<Message>> ReceiveMessages(int messageLimit, CancellationToken cancellationToken)
        {
            try
            {
                var messages = await _client.ReceiveMessages(_receiveSettings.EntityName, messageLimit, _receiveSettings.WaitTimeSeconds, cancellationToken)
                    .ConfigureAwait(false);

                if(messages.Count > 0)
                    LogContext.Warning?.Log("Message received from queue");

                return messages;
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<Message>();
            }
        }

        Task HandleDeliveryComplete()
        {
            if (IsStopping)
                _deliveryComplete.TrySetResult(true);

            return Task.CompletedTask;
        }

        async Task ActiveAndActualAgentsCompleted(StopContext context)
        {
            if (_dispatcher.ActiveDispatchCount > 0)
            {
                try
                {
                    await _deliveryComplete.Task.OrCanceled(context.CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    LogContext.Warning?.Log("Stop canceled waiting for message consumers to complete: {InputAddress}", _context.InputAddress);
                }
            }
        }


        class SequenceNumberComparer :
            IComparer<string>
        {
            public static readonly SequenceNumberComparer Instance = new SequenceNumberComparer();

            public int Compare(string x, string y)
            {
                if (string.IsNullOrWhiteSpace(x))
                    throw new ArgumentNullException(nameof(x));

                if (string.IsNullOrWhiteSpace(y))
                    throw new ArgumentNullException(nameof(y));

                if (x.Length != y.Length)
                    return x.Length > y.Length ? 1 : -1;

                return string.Compare(x, y, StringComparison.Ordinal);
            }
        }
    }
}
