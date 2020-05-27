namespace MassTransit.KafkaIntegration
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Attachments;
    using Configuration;
    using Configuration.Configurators;
    using Confluent.Kafka;
    using GreenPipes;
    using Registration;
    using Serializers;
    using Subscriptions;
    using Util;


    public class KafkaFactoryConfigurator :
        IKafkaFactoryConfigurator
    {
        readonly ClientConfig _clientConfig;
        readonly BusAttachmentObservable _observers;
        readonly List<IKafkaTopic> _topics;
        IHeadersDeserializer _headersDeserializer;

        public KafkaFactoryConfigurator(ClientConfig clientConfig)
        {
            _clientConfig = clientConfig;
            _topics = new List<IKafkaTopic>();
            _observers = new BusAttachmentObservable();

            SetHeadersDeserializer(DictionaryHeadersSerialize.Deserializer);
        }

        public void Host(IEnumerable<string> servers, Action<IKafkaHostConfigurator> configure)
        {
            if (servers == null || !servers.Any())
                throw new ArgumentException(nameof(servers));

            const string serverSeparator = ",";

            _clientConfig.BootstrapServers = string.Join(serverSeparator, servers);
            var configurator = new KafkaHostConfigurator(_clientConfig);
            configure?.Invoke(configurator);
        }

        public void ConfigureApi(Action<IKafkaApiConfigurator> configure)
        {
            var configurator = new KafkaApiConfigurator(_clientConfig);
            configure?.Invoke(configurator);
        }

        public void ConfigureSocket(Action<IKafkaSocketConfigurator> configure)
        {
            var configurator = new KafkaSocketConfigurator(_clientConfig);
            configure?.Invoke(configurator);
        }

        public void Topic<TKey, TValue>(ITopicNameFormatter topicNameFormatter, string groupId,
            Action<IKafkaTopicConfigurator<TKey, TValue>> configure)
            where TValue : class
        {
            if (topicNameFormatter == null)
                throw new ArgumentNullException(nameof(topicNameFormatter));

            if (string.IsNullOrEmpty(groupId))
                throw new ArgumentException(groupId);

            Topic(topicNameFormatter, new ConsumerConfig(_clientConfig) {GroupId = groupId}, configure);
        }

        public void Topic<TKey, TValue>(ITopicNameFormatter topicNameFormatter, ConsumerConfig consumerConfig,
            Action<IKafkaTopicConfigurator<TKey, TValue>> configure)
            where TValue : class
        {
            if (topicNameFormatter == null)
                throw new ArgumentNullException(nameof(topicNameFormatter));

            if (consumerConfig == null)
                throw new ArgumentNullException(nameof(consumerConfig));

            var topic = new KafkaTopic<TKey, TValue>(consumerConfig, topicNameFormatter, _headersDeserializer, configure);
            _topics.Add(topic);
        }

        public void SetHeadersDeserializer(IHeadersDeserializer deserializer)
        {
            _headersDeserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        public Acks? Acks
        {
            set => _clientConfig.Acks = value;
        }

        public string ClientId
        {
            set => _clientConfig.ClientId = value;
        }

        public int? MessageMaxBytes
        {
            set => _clientConfig.MessageMaxBytes = value;
        }

        public int? MessageCopyMaxBytes
        {
            set => _clientConfig.MessageCopyMaxBytes = value;
        }

        public int? ReceiveMessageMaxBytes
        {
            set => _clientConfig.ReceiveMessageMaxBytes = value;
        }

        public int? MaxInFlight
        {
            set => _clientConfig.MaxInFlight = value;
        }

        public TimeSpan? MetadataRequestTimeout
        {
            set => _clientConfig.MetadataRequestTimeoutMs = value?.Milliseconds;
        }

        public TimeSpan? TopicMetadataRefreshInterval
        {
            set => _clientConfig.TopicMetadataRefreshIntervalMs = value?.Milliseconds;
        }

        public TimeSpan? MetadataMaxAge
        {
            set => _clientConfig.MetadataMaxAgeMs = value?.Milliseconds;
        }

        public TimeSpan? TopicMetadataRefreshFastInterval
        {
            set => _clientConfig.TopicMetadataRefreshFastIntervalMs = value?.Milliseconds;
        }

        public bool? TopicMetadataRefreshSparse
        {
            set => _clientConfig.TopicMetadataRefreshSparse = value;
        }

        public string TopicBlacklist
        {
            set => _clientConfig.TopicBlacklist = value;
        }

        public string Debug
        {
            set => _clientConfig.Debug = value;
        }

        public int? BrokerAddressTtl
        {
            set => _clientConfig.BrokerAddressTtl = value;
        }

        public BrokerAddressFamily? BrokerAddressFamily
        {
            set => _clientConfig.BrokerAddressFamily = value;
        }

        public TimeSpan? ReconnectBackoff
        {
            set => _clientConfig.ReconnectBackoffMs = value?.Milliseconds;
        }

        public TimeSpan? ReconnectBackoffMax
        {
            set => _clientConfig.ReconnectBackoffMaxMs = value?.Milliseconds;
        }

        public TimeSpan? StatisticsInterval
        {
            set => _clientConfig.StatisticsIntervalMs = value?.Milliseconds;
        }

        public bool? LogQueue
        {
            set => _clientConfig.LogQueue = value;
        }

        public bool? LogThreadName
        {
            set => _clientConfig.LogThreadName = value;
        }

        public bool? LogConnectionClose
        {
            set => _clientConfig.LogConnectionClose = value;
        }

        public int? InternalTerminationSignal
        {
            set => _clientConfig.InternalTerminationSignal = value;
        }

        public SecurityProtocol? SecurityProtocol
        {
            set => _clientConfig.SecurityProtocol = value;
        }

        public string PluginLibraryPaths
        {
            set => _clientConfig.PluginLibraryPaths = value;
        }

        public string ClientRack
        {
            set => _clientConfig.ClientRack = value;
        }

        public ConnectHandle ConnectBusAttachmentObserver(IBusAttachmentObserver observer)
        {
            return _observers.Connect(observer);
        }

        public string Name => KafkaBaseBusAttachment.InstanceName;

        public ConnectHandle ConnectReceiveEndpointObserver(IReceiveEndpointObserver observer)
        {
            return new MultipleConnectHandle(_topics.Select(x => x.ConnectReceiveEndpointObserver(observer)));
        }

        public IBusInstanceConfigurator Build()
        {
            var configurator = new KafkaBusInstanceConfigurator(_topics, _observers);
            return configurator;
        }
    }
}
