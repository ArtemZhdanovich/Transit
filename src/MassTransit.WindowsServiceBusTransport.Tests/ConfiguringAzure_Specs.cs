﻿// Copyright 2007-2016 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
    namespace ConfiguringAzure_Specs
    {
        using System;
        using System.Threading.Tasks;
        using Microsoft.ServiceBus.Messaging;
        using NUnit.Framework;
        using TestFramework;


        [TestFixture]
        public class Configuring_a_bus_instance :
            AsyncTestFixture
        {
            [Test]
            public async void Should_Support_NetMessaging_Protocol()
            {
                var settings = new TestServiceBusAccountSettings();

                var completed = new TaskCompletionSource<A>();

                var bus = Bus.Factory.CreateUsingWindowsServiceBus(x =>
                {
                    var host = x.Host(settings.ConnectionString, h =>
                    {
                        h.TransportType = TransportType.NetMessaging;
                        h.OperationTimeout = TimeSpan.FromSeconds(30);
                        h.BatchFlushInterval = TimeSpan.FromMilliseconds(50);
                    });

                    x.ReceiveEndpoint(host, "input_queue", e =>
                    {
                        e.PrefetchCount = 16;

                        e.UseLog(Console.Out, async (c, l) => string.Format("Logging: {0}", c.MessageId.Value));

                        e.Handler<A>(async context => completed.TrySetResult(context.Message));

                        // Add a message handler and configure the pipeline to retry the handler
                        // if an exception is thrown
                        e.Handler<A>(Handle, h =>
                        {
                            h.UseRetry(Retry.Interval(5, 100));
                        });
                    });
                });

                // TODO: Assert something here, need to get a hook to the underlying MessageReceiver
                using (bus.Start())
                {
                }

                //                }))
                //                {
                //                    var queueAddress = new Uri(hostAddress, "input_queue");
                //                    ISendEndpoint endpoint = bus.GetSendEndpoint(queueAddress);
                //
                //                    await endpoint.Send(new A());
                //                }
            }

            [Test]
            public async void Should_support_the_new_syntax()
            {
                var settings = new TestServiceBusAccountSettings();

                var completed = new TaskCompletionSource<A>();

                var bus = Bus.Factory.CreateUsingWindowsServiceBus(x =>
                {
                    var host = x.Host(settings.ConnectionString, h =>
                    {
                    });

                    x.ReceiveEndpoint(host, "input_queue", e =>
                    {
                        e.PrefetchCount = 16;

                        e.UseLog(Console.Out, async (c, l) => string.Format("Logging: {0}", c.MessageId.Value));

                        e.Handler<A>(async context => completed.TrySetResult(context.Message));

                        // Add a message handler and configure the pipeline to retry the handler
                        // if an exception is thrown
                        e.Handler<A>(Handle, h =>
                        {
                            h.UseRetry(Retry.Interval(5, 100));
                        });
                    });
                });

                using (bus.Start())
                {
                }

//                }))
//                {
//                    var queueAddress = new Uri(hostAddress, "input_queue");
//                    ISendEndpoint endpoint = bus.GetSendEndpoint(queueAddress);
//
//                    await endpoint.Send(new A());
//                }
            }

            async Task Handle(ConsumeContext<A> context)
            {
            }


            class A
            {
            }
        }
    }
}