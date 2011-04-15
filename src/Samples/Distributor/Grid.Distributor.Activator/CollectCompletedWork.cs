// Copyright 2007-2008 The Apache Software Foundation.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use 
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed 
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace Grid.Distributor.Activator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Shared;
    using Shared.Messages;
    using log4net;
    using MassTransit;
    using MassTransit.Distributor;
    using System.Configuration;
    using MassTransit.Configuration;
    using MassTransit.Services.Subscriptions.Configuration;
    using MassTransit.Serialization;
    using MassTransit.Transports.Msmq;

    public class CollectCompletedWork :
        Consumes<CompletedSimpleWorkItem>.All,
        IServiceInterface
    {
        private UnsubscribeAction _unsubscribeAction;
        private readonly ILog _log = LogManager.GetLogger(typeof(CollectCompletedWork));
        private int _received;
        private int _sent;
        private readonly List<int> _values = new List<int>();

        public IObjectBuilder ObjectBuilder { get; set; }
        public IControlBus ControlBus { get; set; }
        public IServiceBus DataBus { get; set; }

        public CollectCompletedWork(IObjectBuilder objectBuilder)
        {
            ObjectBuilder = objectBuilder;

            var endpointFactory = EndpointResolverConfigurator.New(x =>
            {
                x.AddTransportFactory<MsmqTransportFactory>();
                x.SetObjectBuilder(objectBuilder);
                x.SetDefaultSerializer<XmlMessageSerializer>();
            });

            ControlBus = ControlBusConfigurator.New(x =>
            {
                x.SetObjectBuilder(ObjectBuilder);
            	x.SetEndpointFactory(endpointFactory);

                x.ReceiveFrom(new Uri(ConfigurationManager.AppSettings["SourceQueue"]).AppendToPath("_control"));

                x.PurgeBeforeStarting();
            });

            DataBus = ServiceBusConfigurator.New(x =>
            {
                x.SetObjectBuilder(ObjectBuilder);
                x.ConfigureService<SubscriptionClientConfigurator>(y =>
                {
                    y.SetSubscriptionServiceEndpoint(ConfigurationManager.AppSettings["SubscriptionQueue"]);
                });
                x.ReceiveFrom(ConfigurationManager.AppSettings["SourceQueue"]);
                x.UseControlBus(ControlBus);
                x.SetConcurrentConsumerLimit(4);
            	x.SetEndpointFactory(endpointFactory);
                x.UseDistributorFor<DoSimpleWorkItem>(endpointFactory);
            });
        }

        public void Consume(CompletedSimpleWorkItem message)
        {
            Interlocked.Increment(ref _received);

            int messageMs = DateTime.UtcNow.Subtract(message.RequestCreatedAt).Milliseconds;

            lock (_values)
            {
                _values.Add(messageMs);
            }

            _log.InfoFormat("Received: {0} - {1} [{2}ms]", _received, message.CorrelationId, messageMs);
            _log.InfoFormat("Stats\n\tMin: {0:0000.0}ms\n\tMax: {1:0000.0}ms\n\tAvg: {2:0000.0}ms", _values.Min(), _values.Max(), _values.Average());
        }

        public void Start()
        {
            _unsubscribeAction = DataBus.Subscribe(this);

            Thread.Sleep(1000);

            for (int i = 0; i < 100; i++)
            {
                var g = Guid.NewGuid();
                _log.InfoFormat("Publishing: {0}", g);
                DataBus.Publish(new DoSimpleWorkItem(g));

                Interlocked.Increment(ref _sent);
            }
        }

        public void Stop()
        {
            var action = _unsubscribeAction;
            if (action != null)
            {
                action();
            }
        }
    }
}