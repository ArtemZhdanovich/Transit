namespace MassTransit.AmazonSqsTransport.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using Amazon.SQS.Model;
    using Context;
    using Contexts;
    using GreenPipes;
    using GreenPipes.Agents;
    using GreenPipes.Internals.Extensions;
    using Topology;
    using Transports;
    using Transports.Metrics;
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

            var task = Task.Run(Consume);
            SetCompleted(task);
        }

        long DeliveryMetrics.DeliveryCount => _dispatcher.DispatchCount;
        int DeliveryMetrics.ConcurrentDeliveryCount => _dispatcher.MaxConcurrentDispatchCount;

        async Task Consume()
        {
            var executor = new ChannelExecutor(_receiveSettings.PrefetchCount, _receiveSettings.ConcurrentMessageLimit);

            SetReady();

            try
            {
                await PollMessages(executor).ConfigureAwait(false);
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

        async Task HandleMessageGroup(IEnumerable<Message> messages, ChannelExecutor executor)
        {
            foreach (var message in messages.OrderBy(x => x.Attributes["SequenceNumber"], SequenceNumberComparer.Instance))
            {
                await executor.Run(() => HandleMessage(message), Stopping).ConfigureAwait(false);
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

        async Task PollMessages(ChannelExecutor executor)
        {
            var maxReceiveCount = (_receiveSettings.PrefetchCount + 9) / 10;
            var receiveCount = 1;
            var messageLimit = Math.Min(_receiveSettings.PrefetchCount, 10);

            while (!IsStopping)
            {
                var received = 0;

                if (_receiveSettings.OrderedMessageHandlingEnabled)
                {
                    Task<IList<Message>>[] receiveTasks = Enumerable.Repeat(0, receiveCount).Select(_ => ReceiveMessages(messageLimit)).ToArray();
                    IList<Message>[] messages = await Task.WhenAll(receiveTasks).ConfigureAwait(false);

                    received = await OrderedHandleMessages(executor, messages);
                }
                else
                {
                    Task<int>[] receiveTasks = Enumerable.Repeat(0, receiveCount).Select(_ => ReceiveAndHandleMessages(messageLimit, executor)).ToArray();
                    var counts = await Task.WhenAll(receiveTasks).ConfigureAwait(false);

                    received = counts.Sum();
                }

                if (received == receiveCount * 10) // ramp up receivers when busy
                    receiveCount = Math.Min(maxReceiveCount, receiveCount + (maxReceiveCount - receiveCount) / 2);
                else if (received / 10 < receiveCount - 1) // dial it back when not so busy
                    receiveCount = Math.Max(1, (received + 9) / 10);
            }
        }

        async Task<int> OrderedHandleMessages(ChannelExecutor executor, IList<Message>[] messages)
        {
            var messagesByGroupId = new Dictionary<string, List<Message>>();
            var messagesWithoutGroupId = new List<Message>();
            var messagesCount = 0;

            foreach (var message in messages.SelectMany(x => x))
            {
                messagesCount++;

                if (!message.Attributes.TryGetValue("MessageGroupId", out var groupId))
                {
                    messagesWithoutGroupId.Add(message);

                    continue;
                }

                if (!messagesByGroupId.TryGetValue(groupId, out List<Message> groupedMessages))
                {
                    groupedMessages = new List<Message>();
                    messagesByGroupId[groupId] = groupedMessages;
                }

                groupedMessages.Add(message);
            }

            List<Task> handleMessagesTasks = messagesWithoutGroupId.Select(message => executor.Run(() => HandleMessage(message), Stopping)).ToList();
            IEnumerable<Task> groupedHandleMessagesTasks = messagesByGroupId.Select(x => HandleMessageGroup(x.Value, executor));

            handleMessagesTasks.AddRange(groupedHandleMessagesTasks);

            await Task.WhenAll(handleMessagesTasks).ConfigureAwait(false);

            return messagesCount;
        }

        async Task<int> ReceiveAndHandleMessages(int messageLimit, ChannelExecutor executor)
        {
            try
            {
                IList<Message> messages = await _client.ReceiveMessages(_receiveSettings.EntityName, messageLimit, _receiveSettings.WaitTimeSeconds, Stopping)
                    .ConfigureAwait(false);

                await Task.WhenAll(messages.Select(message => executor.Run(() => HandleMessage(message), Stopping))).ConfigureAwait(false);

                return messages.Count;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
        }

        async Task<IList<Message>> ReceiveMessages(int messageLimit)
        {
            try
            {
                IList<Message> messages = await _client.ReceiveMessages(_receiveSettings.EntityName, messageLimit, _receiveSettings.WaitTimeSeconds, Stopping)
                                                       .ConfigureAwait(false);

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
            {
                _deliveryComplete.TrySetResult(true);
            }

            return TaskUtil.Completed;
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

        class SequenceNumberComparer : IComparer<string>
        {
            public static readonly SequenceNumberComparer Instance = new SequenceNumberComparer();

            public int Compare(string x, string y)
            {
                if (string.IsNullOrEmpty(x))
                {
                    throw new ArgumentNullException(nameof(x));
                }

                if (string.IsNullOrEmpty(y))
                {
                    throw new ArgumentNullException(nameof(y));
                }

                if (x.Length != y.Length)
                {
                    return x.Length > y.Length ? 1 : -1;
                }

                if (string.Equals(x, y, StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }

                //SequenceNumber has 128 bits
                var xBigInt = BigInteger.Parse(x);
                var yBigInt = BigInteger.Parse(y);

                return xBigInt > yBigInt ? 1 : -1;
            }
        }
    }
}
