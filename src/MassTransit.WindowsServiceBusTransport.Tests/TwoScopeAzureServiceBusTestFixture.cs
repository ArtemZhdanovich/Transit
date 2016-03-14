﻿// Copyright 2007-2015 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.WindowsServiceBusTransport.Tests
{
    using System;
    using System.Threading;
    using WindowsServiceBusTransport;
    using WindowsServiceBusTransport.Configuration;
    using Logging;
    using Microsoft.ServiceBus;
    using NUnit.Framework;


    [TestFixture]
    public class TwoScopeAzureServiceBusTestFixture :
        ServiceBusTestFixture
    {
        static readonly ILog _log = Logger.Get<TwoScopeAzureServiceBusTestFixture>();

        public TwoScopeAzureServiceBusTestFixture()
        {
            _secondServiceUri = ServiceBusEnvironment.CreateServiceUri("sb", "masstransit-build", "MassTransit.Tests.SecondService");
        }

        Uri _secondInputQueueAddress;
        readonly Uri _secondServiceUri;
        IBusControl _secondBus;
        BusHandle _secondBusHandle;
        ISendEndpoint _secondBusSendEndpoint;
        ISendEndpoint _secondInputQueueSendEndpoint;

        /// <summary>
        /// The sending endpoint for the InputQueue
        /// </summary>
        protected ISendEndpoint SecondInputQueueSendEndpoint => _secondInputQueueSendEndpoint;

        /// <summary>
        /// The sending endpoint for the Bus 
        /// </summary>
        protected ISendEndpoint SecondBusSendEndpoint => _secondBusSendEndpoint;

        protected Uri SecondBusAddress => _secondBus.Address;

        protected Uri SecondInputQueueAddress
        {
            get { return _secondInputQueueAddress; }
            set
            {
                if (SecondBus != null)
                    throw new InvalidOperationException("The LocalBus has already been created, too late to change the URI");

                _secondInputQueueAddress = value;
            }
        }

        protected virtual IBus SecondBus => _secondBus;

        [TestFixtureSetUp]
        public void SetupSecondAzureServiceBusTestFixture()
        {
            _secondBus = CreateSecondBus();

            _secondBusHandle = _secondBus.Start();
            try
            {
                _secondBusSendEndpoint = Await(() => _secondBus.GetSendEndpoint(_secondBus.Address));

                _secondInputQueueSendEndpoint = Await(() => _secondBus.GetSendEndpoint(_secondInputQueueAddress));
            }
            catch (Exception)
            {
                try
                {
                    using (var tokenSource = new CancellationTokenSource(TestTimeout))
                    {
                        _secondBusHandle.Stop(tokenSource.Token);
                    }
                }
                finally
                {
                    _secondBusHandle = null;
                    _secondBus = null;
                }

                throw;
            }
        }

        [TestFixtureTearDown]
        public void TearDownTwoScopeTestFixture()
        {
            try
            {
                using (var tokenSource = new CancellationTokenSource(TestTimeout))
                {
                    _secondBusHandle?.Stop(tokenSource.Token);
                }
            }
            catch (Exception ex)
            {
                _log.Error("SecondBus Stop Failed", ex);
            }
            finally
            {
                _secondBusHandle = null;
                _secondBus = null;
            }
        }

        protected virtual void ConfigureSecondBus(IServiceBusBusFactoryConfigurator configurator)
        {
        }

        protected virtual void ConfigureSecondBusHost(IServiceBusBusFactoryConfigurator configurator, IServiceBusHost host)
        {
        }

        protected virtual void ConfigureSecondInputQueueEndpoint(IServiceBusReceiveEndpointConfigurator configurator)
        {
        }

        IBusControl CreateSecondBus()
        {
            return MassTransit.Bus.Factory.CreateUsingAzureServiceBus(x =>
            {
                ConfigureSecondBus(x);

                var connectionString = new TestServiceBusAccountSettings().ConnectionString;

                IServiceBusHost host = x.Host(connectionString, h =>
                {
                });

                ConfigureSecondBusHost(x, host);

                x.ReceiveEndpoint(host, "input_queue", e =>
                {
                    _secondInputQueueAddress = e.InputAddress;

                    ConfigureSecondInputQueueEndpoint(e);
                });
            });
        }
    }
}