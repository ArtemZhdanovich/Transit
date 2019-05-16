﻿// Copyright 2007-2018 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.WebJobs.ServiceBusIntegration
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.ServiceBus.Core.Transport;
    using Contexts;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.ServiceBus;
    using Microsoft.Extensions.Logging;
    using Transports;


    public class ServiceBusAttributePublishTransportProvider :
        IPublishTransportProvider
    {
        readonly IBinder _binder;
        readonly ILogger _logger;
        readonly CancellationToken _cancellationToken;

        public ServiceBusAttributePublishTransportProvider(IBinder binder, ILogger logger, CancellationToken cancellationToken)
        {
            _binder = binder;
            _logger = logger;
            _cancellationToken = cancellationToken;
        }

        Task<ISendTransport> IPublishTransportProvider.GetPublishTransport<T>(Uri publishAddress)
        {
            return GetSendTransport(publishAddress);
        }

        async Task<ISendTransport> GetSendTransport(Uri address)
        {
            var queueOrTopicName = address.AbsolutePath.Trim('/');

            var serviceBusTopic = new ServiceBusAttribute(queueOrTopicName, EntityType.Topic);

            IAsyncCollector<Message> collector = await _binder.BindAsync<IAsyncCollector<Message>>(serviceBusTopic, _cancellationToken).ConfigureAwait(false);

            _logger.LogDebug("Creating Publish Transport: {0}", queueOrTopicName);

            var client = new CollectorMessageSendEndpointContext(queueOrTopicName, _logger, collector, _cancellationToken);

            var source = new CollectorSendEndpointContextSource(client);

            var transport = new ServiceBusSendTransport(source, address);

            return transport;
        }
    }
}
