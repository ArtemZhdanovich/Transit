namespace MassTransit.Context
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Converters;
    using Events;
    using GreenPipes;
    using GreenPipes.Internals.Extensions;
    using Metadata;
    using Util;


    public abstract class BaseConsumeContext :
        ConsumeContext
    {
        protected BaseConsumeContext(ReceiveContext receiveContext)
        {
            ReceiveContext = receiveContext;
        }

        public virtual CancellationToken CancellationToken => ReceiveContext.CancellationToken;

        public abstract bool HasPayloadType(Type payloadType);

        public abstract bool TryGetPayload<T>(out T payload)
            where T : class;

        public abstract T GetOrAddPayload<T>(PayloadFactory<T> payloadFactory)
            where T : class;

        public abstract T AddOrUpdatePayload<T>(PayloadFactory<T> addFactory, UpdatePayloadFactory<T> updateFactory)
            where T : class;

        public virtual ReceiveContext ReceiveContext { get; }

        public abstract Task ConsumeCompleted { get; }

        public abstract Guid? MessageId { get; }
        public abstract Guid? RequestId { get; }
        public abstract Guid? CorrelationId { get; }
        public abstract Guid? ConversationId { get; }
        public abstract Guid? InitiatorId { get; }
        public abstract DateTime? ExpirationTime { get; }
        public abstract Uri SourceAddress { get; }
        public abstract Uri DestinationAddress { get; }
        public abstract Uri ResponseAddress { get; }
        public abstract Uri FaultAddress { get; }
        public abstract DateTime? SentTime { get; }
        public abstract Headers Headers { get; }
        public abstract HostInfo Host { get; }
        public abstract IEnumerable<string> SupportedMessageTypes { get; }
        public abstract bool HasMessageType(Type messageType);

        public abstract bool TryGetMessage<T>(out ConsumeContext<T> consumeContext)
            where T : class;

        public virtual Task RespondAsync<T>(T message)
            where T : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            return ConsumeTask(RespondInternal(message));
        }

        public virtual Task RespondAsync<T>(T message, IPipe<SendContext<T>> sendPipe)
            where T : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (sendPipe == null)
                throw new ArgumentNullException(nameof(sendPipe));

            return ConsumeTask(RespondInternal(message, sendPipe));
        }

        public virtual Task RespondAsync<T>(T message, IPipe<SendContext> sendPipe)
            where T : class
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (sendPipe == null)
                throw new ArgumentNullException(nameof(sendPipe));

            return ConsumeTask(RespondInternal(message, sendPipe));
        }

        public virtual Task RespondAsync(object message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var messageType = message.GetType();

            return ResponseEndpointConverterCache.Respond(this, message, messageType);
        }

        public virtual Task RespondAsync(object message, Type messageType)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            return ResponseEndpointConverterCache.Respond(this, message, messageType);
        }

        public virtual Task RespondAsync(object message, IPipe<SendContext> sendPipe)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (sendPipe == null)
                throw new ArgumentNullException(nameof(sendPipe));

            var messageType = message.GetType();

            return ResponseEndpointConverterCache.Respond(this, message, messageType, sendPipe);
        }

        public virtual Task RespondAsync(object message, Type messageType, IPipe<SendContext> sendPipe)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));
            if (sendPipe == null)
                throw new ArgumentNullException(nameof(sendPipe));

            return ResponseEndpointConverterCache.Respond(this, message, messageType, sendPipe);
        }

        public virtual Task RespondAsync<T>(object values)
            where T : class
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            return ConsumeTask(RespondInternal<T>(values));
        }

        public virtual Task RespondAsync<T>(object values, IPipe<SendContext<T>> sendPipe)
            where T : class
        {
            return ConsumeTask(RespondInternal<T>(values, sendPipe));
        }

        public virtual Task RespondAsync<T>(object values, IPipe<SendContext> sendPipe)
            where T : class
        {
            return ConsumeTask(RespondInternal<T>(values, sendPipe));
        }

        public virtual void Respond<T>(T message)
            where T : class
        {
            AddConsumeTask(RespondInternal(message));
        }

        public virtual async Task<ISendEndpoint> GetSendEndpoint(Uri address)
        {
            var sendEndpoint = await ReceiveContext.SendEndpointProvider.GetSendEndpoint(address).ConfigureAwait(false);

            return new ConsumeSendEndpoint(sendEndpoint, this, ConsumeTask, default);
        }

        public virtual Task NotifyConsumed<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType)
            where T : class
        {
            return ReceiveContext.NotifyConsumed(context, duration, consumerType);
        }

        public virtual async Task NotifyFaulted<T>(ConsumeContext<T> context, TimeSpan duration, string consumerType, Exception exception)
            where T : class
        {
            await GenerateFault(context, exception).ConfigureAwait(false);

            await ReceiveContext.NotifyFaulted(context, duration, consumerType, exception).ConfigureAwait(false);
        }

        public virtual Task Publish<T>(T message, CancellationToken cancellationToken)
            where T : class
        {
            return ConsumeTask(PublishInternal(cancellationToken, message));
        }

        public virtual Task Publish<T>(T message, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken)
            where T : class
        {
            return ConsumeTask(PublishInternal(cancellationToken, message, publishPipe));
        }

        public virtual Task Publish<T>(T message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken)
            where T : class
        {
            return ConsumeTask(PublishInternal(cancellationToken, message, publishPipe));
        }

        public virtual Task Publish(object message, CancellationToken cancellationToken)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var messageType = message.GetType();

            return PublishEndpointConverterCache.Publish(this, message, messageType, cancellationToken);
        }

        public virtual Task Publish(object message, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            var messageType = message.GetType();

            return PublishEndpointConverterCache.Publish(this, message, messageType, publishPipe, cancellationToken);
        }

        public virtual Task Publish(object message, Type messageType, CancellationToken cancellationToken)
        {
            return PublishEndpointConverterCache.Publish(this, message, messageType, cancellationToken);
        }

        public virtual Task Publish(object message, Type messageType, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken)
        {
            return PublishEndpointConverterCache.Publish(this, message, messageType, publishPipe, cancellationToken);
        }

        public virtual Task Publish<T>(object values, CancellationToken cancellationToken)
            where T : class
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            return ConsumeTask(PublishInternal<T>(cancellationToken, values));
        }

        public virtual Task Publish<T>(object values, IPipe<PublishContext<T>> publishPipe, CancellationToken cancellationToken)
            where T : class
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            return ConsumeTask(PublishInternal<T>(cancellationToken, values, publishPipe));
        }

        public virtual Task Publish<T>(object values, IPipe<PublishContext> publishPipe, CancellationToken cancellationToken)
            where T : class
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            return ConsumeTask(PublishInternal<T>(cancellationToken, values, publishPipe));
        }

        public ConnectHandle ConnectPublishObserver(IPublishObserver observer)
        {
            return ReceiveContext.PublishEndpointProvider.ConnectPublishObserver(observer);
        }

        public ConnectHandle ConnectSendObserver(ISendObserver observer)
        {
            return ReceiveContext.SendEndpointProvider.ConnectSendObserver(observer);
        }

        public abstract void AddConsumeTask(Task task);

        protected virtual async Task<ISendEndpoint> GetPublishSendEndpoint<T>()
            where T : class
        {
            var publishSendEndpoint = await ReceiveContext.PublishEndpointProvider.GetPublishSendEndpoint<T>().ConfigureAwait(false);

            return new ConsumeSendEndpoint(publishSendEndpoint, this, ConsumeTask, default);
        }

        protected async Task PublishInternal<T>(CancellationToken cancellationToken, T message, IPipe<PublishContext<T>> pipe = default)
            where T : class
        {
            var sendEndpoint = await GetPublishSendEndpoint<T>().ConfigureAwait(false);

            if (pipe.IsNotEmpty())
                await sendEndpoint.Send(message, new PublishPipe<T>(pipe), cancellationToken).ConfigureAwait(false);
            else
                await sendEndpoint.Send(message, cancellationToken).ConfigureAwait(false);
        }

        protected async Task PublishInternal<T>(CancellationToken cancellationToken, object values, IPipe<PublishContext<T>> pipe = default)
            where T : class
        {
            var sendEndpoint = await GetPublishSendEndpoint<T>().ConfigureAwait(false);

            if (pipe.IsNotEmpty())
                await sendEndpoint.Send(values, new PublishPipe<T>(pipe), cancellationToken).ConfigureAwait(false);
            else
                await sendEndpoint.Send<T>(values, cancellationToken).ConfigureAwait(false);
        }

        protected virtual Task<ISendEndpoint> GetResponseEndpoint<T>()
            where T : class
        {
            if (ResponseAddress != null)
            {
                var sendEndpointTask = ReceiveContext.SendEndpointProvider.GetSendEndpoint(ResponseAddress);
                if (sendEndpointTask.IsCompletedSuccessfully())
                    return Task.FromResult<ISendEndpoint>(new ConsumeSendEndpoint(sendEndpointTask.Result, this, ConsumeTask, RequestId));

                async Task<ISendEndpoint> GetResponseEndpointAsync()
                {
                    var sendEndpoint = await sendEndpointTask.ConfigureAwait(false);

                    return new ConsumeSendEndpoint(sendEndpoint, this, ConsumeTask, RequestId);
                }

                return GetResponseEndpointAsync();
            }

            var publishSendEndpointTask = ReceiveContext.PublishEndpointProvider.GetPublishSendEndpoint<T>();
            if (publishSendEndpointTask.IsCompletedSuccessfully())
                return Task.FromResult<ISendEndpoint>(new ConsumeSendEndpoint(publishSendEndpointTask.Result, this, ConsumeTask, RequestId));

            async Task<ISendEndpoint> GetPublishEndpointAsync()
            {
                var sendEndpoint = await publishSendEndpointTask.ConfigureAwait(false);

                return new ConsumeSendEndpoint(sendEndpoint, this, ConsumeTask, RequestId);
            }

            return GetPublishEndpointAsync();
        }

        protected async Task RespondInternal<T>(T message, IPipe<SendContext<T>> pipe = default)
            where T : class
        {
            var responseEndpoint = await GetResponseEndpoint<T>().ConfigureAwait(false);

            if (pipe.IsNotEmpty())
                await responseEndpoint.Send(message, pipe, CancellationToken).ConfigureAwait(false);
            else
                await responseEndpoint.Send(message, CancellationToken).ConfigureAwait(false);
        }

        protected async Task RespondInternal<T>(object values, IPipe<SendContext<T>> pipe = default)
            where T : class
        {
            var responseEndpoint = await GetResponseEndpoint<T>().ConfigureAwait(false);

            if (pipe.IsNotEmpty())
                await responseEndpoint.Send(values, pipe, CancellationToken).ConfigureAwait(false);
            else
                await responseEndpoint.Send<T>(values, CancellationToken).ConfigureAwait(false);
        }

        protected virtual async Task<ISendEndpoint> GetFaultEndpoint<T>()
            where T : class
        {
            var destinationAddress = FaultAddress ?? ResponseAddress;
            if (destinationAddress != null)
            {
                var sendEndpoint = await ReceiveContext.SendEndpointProvider.GetSendEndpoint(destinationAddress).ConfigureAwait(false);

                return new ConsumeSendEndpoint(sendEndpoint, this, ConsumeTask, RequestId);
            }

            var publishSendEndpoint = await ReceiveContext.PublishEndpointProvider.GetPublishSendEndpoint<Fault<T>>().ConfigureAwait(false);

            return new ConsumeSendEndpoint(publishSendEndpoint, this, ConsumeTask, RequestId);
        }

        protected virtual async Task GenerateFault<T>(ConsumeContext<T> context, Exception exception)
            where T : class
        {
            Fault<T> fault = new FaultEvent<T>(context.Message, context.MessageId, HostMetadataCache.Host, exception, context.SupportedMessageTypes.ToArray());

            var faultPipe = new FaultPipe<T>(context);

            var faultEndpoint = await GetFaultEndpoint<T>().ConfigureAwait(false);

            await faultEndpoint.Send(fault, faultPipe, CancellationToken).ConfigureAwait(false);
        }

        Task ConsumeTask(Task task)
        {
            AddConsumeTask(task);

            return task;
        }


        readonly struct PublishPipe<T> :
            IPipe<SendContext<T>>
            where T : class
        {
            readonly IPipe<PublishContext<T>> _pipe;

            public PublishPipe(IPipe<PublishContext<T>> pipe)
            {
                _pipe = pipe;
            }

            void IProbeSite.Probe(ProbeContext context)
            {
                _pipe.Probe(context);
            }

            public Task Send(SendContext<T> context)
            {
                var publishContext = context.GetPayload<PublishContext<T>>();

                return _pipe.Send(publishContext);
            }
        }
    }


    readonly struct FaultPipe<T> :
        IPipe<SendContext<Fault<T>>>
        where T : class
    {
        readonly ConsumeContext<T> _context;

        public FaultPipe(ConsumeContext<T> context)
        {
            _context = context;
        }

        public Task Send(SendContext<Fault<T>> context)
        {
            context.TransferConsumeContextHeaders(_context);

            context.CorrelationId = _context.CorrelationId;
            context.RequestId = _context.RequestId;

            if (_context.TryGetPayload(out ConsumeRetryContext retryContext) && retryContext.RetryCount > 0)
            {
                context.Headers.Set(MessageHeaders.FaultRetryCount, retryContext.RetryCount);
            }

            return TaskUtil.Completed;
        }

        public void Probe(ProbeContext context)
        {
        }
    }
}
