﻿namespace MassTransit.KafkaIntegration.Transport
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Confluent.Kafka;
    using Context;
    using Contexts;
    using GreenPipes;
    using Metadata;
    using Pipeline;
    using Pipeline.Observables;
    using Serializers;
    using Transports;
    using Transports.Metrics;
    using Util;


    public class KafkaReceiver<TKey, TValue> :
        IKafkaReceiver<TKey, TValue>
        where TValue : class
    {
        readonly ReceiveEndpointContext _context;
        readonly IReceivePipeDispatcher _dispatcher;
        readonly IHeadersDeserializer _headersDeserializer;
        readonly ReceiveTransportObservable _observers;

        public KafkaReceiver(ReceiveEndpointContext context, IHeadersDeserializer headersDeserializer)
        {
            _context = context;
            _headersDeserializer = headersDeserializer;
            _observers = new ReceiveTransportObservable();
            _dispatcher = context.CreateReceivePipeDispatcher();
        }

        void IProbeSite.Probe(ProbeContext context)
        {
            var scope = context.CreateScope("receiver");
            scope.Add("key-type", TypeMetadataCache<TKey>.ShortName);
            scope.Add("value-type", TypeMetadataCache<TValue>.ShortName);
        }

        public ReceiveTransportHandle Start()
        {
            return new ReceiverHandle();
        }

        ConnectHandle IReceiveObserverConnector.ConnectReceiveObserver(IReceiveObserver observer)
        {
            return _context.ConnectReceiveObserver(observer);
        }

        ConnectHandle IPublishObserverConnector.ConnectPublishObserver(IPublishObserver observer)
        {
            return _context.ConnectPublishObserver(observer);
        }

        ConnectHandle ISendObserverConnector.ConnectSendObserver(ISendObserver observer)
        {
            return _context.ConnectSendObserver(observer);
        }

        public async Task Handle(ConsumeResult<TKey, TValue> message, CancellationToken cancellationToken, Action<ReceiveContext> contextCallback)
        {
            var context = new KafkaReadReceiveContext<TKey, TValue>(message, _context, _headersDeserializer);
            contextCallback?.Invoke(context);

            CancellationTokenRegistration registration;
            if (cancellationToken.CanBeCanceled)
                registration = cancellationToken.Register(context.Cancel);

            ConsumeResultLockContext<TKey, TValue> receiveLock = context.TryGetPayload(out IConsumerLockContext<TKey, TValue> lockContext)
                ? new ConsumeResultLockContext<TKey, TValue>(lockContext, message)
                : default;

            try
            {
                await _dispatcher.Dispatch(context, receiveLock).ConfigureAwait(false);
            }
            finally
            {
                registration.Dispose();
                context.Dispose();
            }
        }

        ConnectHandle IConsumeMessageObserverConnector.ConnectConsumeMessageObserver<T>(IConsumeMessageObserver<T> observer)
        {
            return _context.ReceivePipe.ConnectConsumeMessageObserver(observer);
        }

        ConnectHandle IConsumeObserverConnector.ConnectConsumeObserver(IConsumeObserver observer)
        {
            return _context.ReceivePipe.ConnectConsumeObserver(observer);
        }

        public ConnectHandle ConnectReceiveTransportObserver(IReceiveTransportObserver observer)
        {
            return _observers.Connect(observer);
        }

        public async Task Ready(ReceiveTransportReady ready)
        {
            await _context.TransportObservers.Ready(ready).ConfigureAwait(false);
            await _observers.Ready(ready).ConfigureAwait(false);
        }

        public async Task Completed(ReceiveTransportCompleted completed)
        {
            await _context.TransportObservers.Completed(completed).ConfigureAwait(false);
            await _observers.Completed(completed).ConfigureAwait(false);
        }

        public async Task Faulted(ReceiveTransportFaulted faulted)
        {
            await _context.TransportObservers.Faulted(faulted).ConfigureAwait(false);
            await _observers.Faulted(faulted).ConfigureAwait(false);
        }

        public int ActiveDispatchCount => _dispatcher.ActiveDispatchCount;
        public long DispatchCount => _dispatcher.DispatchCount;
        public int MaxConcurrentDispatchCount => _dispatcher.MaxConcurrentDispatchCount;

        public event ZeroActiveDispatchHandler ZeroActivity
        {
            add => _dispatcher.ZeroActivity += value;
            remove => _dispatcher.ZeroActivity -= value;
        }

        public DeliveryMetrics GetMetrics()
        {
            return _dispatcher.GetMetrics();
        }


        class ReceiverHandle :
            ReceiveTransportHandle
        {
            public Task Stop(CancellationToken cancellationToken = default)
            {
                return TaskUtil.Completed;
            }
        }
    }
}
