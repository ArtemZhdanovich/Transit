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
namespace MassTransit.HttpTransport.Configuration
{
    using System;
    using System.Threading.Tasks;
    using Clients;
    using Context;
    using Hosting;
    using MassTransit.Configuration;
    using MassTransit.Topology;
    using Transport;
    using Transports;


    public class HttpHostConfiguration :
        IHttpHostConfiguration
    {
        readonly IHttpBusConfiguration _busConfiguration;
        readonly HttpHostSettings _settings;
        readonly IHostTopology _hostTopology;
        readonly HttpHost _host;

        public HttpHostConfiguration(IHttpBusConfiguration busConfiguration, HttpHostSettings settings, IHostTopology hostTopology)
        {
            _busConfiguration = busConfiguration;
            _settings = settings;
            _hostTopology = hostTopology;

            HostAddress = settings.GetInputAddress();

            _host = new HttpHost(this);
        }

        public Uri HostAddress { get; }

        IBusHostControl IHostConfiguration.Host => _host;
        IHostTopology IHostConfiguration.Topology => _hostTopology;

        IHttpHost IHttpHostConfiguration.Host => _host;
        IHttpBusConfiguration IHttpHostConfiguration.BusConfiguration => _busConfiguration;
        HttpHostSettings IHttpHostConfiguration.Settings => _settings;

        public bool Matches(Uri address)
        {
            var settings = address.GetHostSettings();

            return HttpHostEqualityComparer.Default.Equals(_settings, settings);
        }

        public Task<ISendTransport> CreateSendTransport(Uri address)
        {
            throw new NotImplementedException();
        }

        public IHttpReceiveEndpointConfiguration CreateReceiveEndpointConfiguration(string pathMatch)
        {
            return new HttpReceiveEndpointConfiguration(this, pathMatch, _busConfiguration.CreateEndpointConfiguration());
        }

        public Task<ISendTransport> CreateSendTransport(Uri address, ReceiveEndpointContext receiveEndpointContext)
        {
            var clientContextSupervisor = new HttpClientContextSupervisor(receiveEndpointContext.ReceivePipe);

            var sendSettings = address.GetSendSettings();

            var transport = new HttpSendTransport(clientContextSupervisor, sendSettings, receiveEndpointContext);

            _host.Add(transport);

            return Task.FromResult<ISendTransport>(transport);
        }
    }
}