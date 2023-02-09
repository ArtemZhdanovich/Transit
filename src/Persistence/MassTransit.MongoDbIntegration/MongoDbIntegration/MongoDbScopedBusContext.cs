#nullable enable
namespace MassTransit.MongoDbIntegration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Clients;
    using DependencyInjection;
    using Middleware;
    using Middleware.Outbox;
    using Outbox;
    using Serialization;
    using Transports;


    public class MongoDbScopedBusContext<TBus> :
        ScopedBusContext,
        OutboxSendContext,
        IDisposable
        where TBus : class, IBus
    {
        readonly TBus _bus;
        readonly IClientFactory _clientFactory;
        readonly MongoDbContext _dbContext;
        readonly IBusOutboxNotification _notification;
        readonly Guid _outboxId;
        readonly MongoDbCollectionContext<OutboxMessage> _outboxMessages;
        readonly MongoDbCollectionContext<OutboxState> _outboxStates;
        readonly IServiceProvider _provider;

        IPublishEndpoint? _publishEndpoint;
        IScopedClientFactory? _scopedClientFactory;
        ISendEndpointProvider? _sendEndpointProvider;
        Task? _startTransaction;

        public MongoDbScopedBusContext(TBus bus, MongoDbContext dbContext, IBusOutboxNotification notification, IClientFactory clientFactory,
            IServiceProvider provider)
        {
            _bus = bus;
            _dbContext = dbContext;
            _notification = notification;
            _clientFactory = clientFactory;
            _provider = provider;

            _outboxMessages = _dbContext.GetCollection<OutboxMessage>();
            _outboxStates = _dbContext.GetCollection<OutboxState>();

            _outboxId = NewId.NextGuid();
        }

        public void Dispose()
        {
            lock (_dbContext)
            {
                if (_startTransaction != null)
                    _notification.Delivered();
            }
        }

        public async Task AddSend<T>(SendContext<T> context)
            where T : class
        {
            lock (_dbContext)
                _startTransaction ??= StartOutboxTransaction(context.CancellationToken);

            await _startTransaction.ConfigureAwait(false);

            await _outboxMessages.AddSend(context, SystemTextJsonMessageSerializer.Instance, outboxId: _outboxId).ConfigureAwait(false);
        }

        public object? GetService(Type serviceType)
        {
            return _provider.GetService(serviceType);
        }

        public ISendEndpointProvider SendEndpointProvider
        {
            get { return _sendEndpointProvider ??= new OutboxSendEndpointProvider(this, _bus); }
        }

        public IPublishEndpoint PublishEndpoint
        {
            get { return _publishEndpoint ??= new PublishEndpoint(new OutboxPublishEndpointProvider(this, _bus)); }
        }

        public IScopedClientFactory ClientFactory
        {
            get
            {
                return _scopedClientFactory ??=
                    new ScopedClientFactory(new ClientFactory(new ScopedClientFactoryContext(_clientFactory, _provider)), null);
            }
        }

        async Task StartOutboxTransaction(CancellationToken cancellationToken)
        {
            await _dbContext.BeginTransaction(cancellationToken).ConfigureAwait(false);

            var outboxState = new OutboxState
            {
                OutboxId = _outboxId,
                Created = DateTime.UtcNow
            };

            await _outboxStates.InsertOne(outboxState, cancellationToken).ConfigureAwait(false);
        }
    }
}
