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
namespace MassTransit.RabbitMqTransport.Tests
{
    using System;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using TestFramework;


    [TestFixture, Explicit]
    public class Failing_to_connect_to_rabbitmq :
        AsyncTestFixture
    {
        [Test]
        public void Should_fault_nicely()
        {
            var busControl = Bus.Factory.CreateUsingRabbitMq(x =>
            {
                x.Host(new Uri("rabbitmq://unknownhost:32787"), h =>
                {
                    h.Username("whocares");
                    h.Password("Ohcrud");
                });
            });

            Assert.That(async () =>
            {
                var handle = await busControl.StartAsync();
                try
                {
                    Console.WriteLine("Waiting for connection...");

                    await handle.Ready;
                }
                finally
                {
                    await handle.StopAsync();
                }
            }, Throws.TypeOf<RabbitMqConnectionException>());
        }

        [Test]
        public void Should_fault_when_credentials_are_bad()
        {
            var busControl = Bus.Factory.CreateUsingRabbitMq(x =>
            {
                x.Host(new Uri("rabbitmq://localhost/"), h =>
                {
                    h.Username("guest");
                    h.Password("guessed");
                });
            });

            Assert.That(async () =>
            {
                var handle = await busControl.StartAsync();
                try
                {
                    Console.WriteLine("Waiting for connection...");

                    await handle.Ready;
                }
                finally
                {
                    await handle.StopAsync();
                }
            }, Throws.TypeOf<RabbitMqConnectionException>());
        }

        [Test, Explicit]
        public async Task Should_recover_from_a_crashed_server()
        {
            var busControl = Bus.Factory.CreateUsingRabbitMq(x =>
            {
                x.Host(new Uri("rabbitmq://localhost/"), h =>
                {
                });
            });

            var handle = await busControl.StartAsync();
            try
            {
                Console.WriteLine("Waiting for connection...");

                await handle.Ready;

                for (var i = 0; i < 10; i++)
                {
                    try
                    {
                        await Task.Delay(1000);

                        await busControl.Publish(new TestMessage());

                        Console.WriteLine("Published: {0}", i);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Publish {0} faulted: {1}", i, ex.Message);
                    }
                }
            }
            finally
            {
                await handle.StopAsync();
            }
        }

        [Test, Explicit]
        public async Task Should_startup_and_shut_down_cleanly()
        {
            var busControl = Bus.Factory.CreateUsingRabbitMq(x =>
            {
                x.Host(new Uri("rabbitmq://localhost/"), h =>
                {
                });
            });

            var handle = await busControl.StartAsync();
            try
            {
                Console.WriteLine("Waiting for connection...");

                await handle.Ready;
            }
            finally
            {
                await handle.StopAsync();
            }
        }

        [Test, Explicit]
        public async Task Should_startup_and_shut_down_cleanly_with_an_endpoint()
        {
            var busControl = Bus.Factory.CreateUsingRabbitMq(x =>
            {
                var host = x.Host(new Uri("rabbitmq://localhost/"), h =>
                {
                });

                x.ReceiveEndpoint(host, "input_queue", e =>
                {
                    e.Handler<Test>(async context =>
                    {
                    });
                });
            });

            var handle = await busControl.StartAsync();
            try
            {
                Console.WriteLine("Waiting for connection...");

                await handle.Ready;
            }
            finally
            {
                await handle.StopAsync();
            }
        }

        [Test, Explicit]
        public async Task Should_startup_and_shut_down_cleanly_with_publish()
        {
            var busControl = Bus.Factory.CreateUsingRabbitMq(x =>
            {
                x.Host(new Uri("rabbitmq://localhost/"), h =>
                {
                });
            });

            await busControl.StartAsync();
            try
            {
                await busControl.Publish(new TestMessage());
            }
            finally
            {
                await busControl.StopAsync();
            }
        }


        public interface Test
        {
        }


        public class TestMessage : Test
        {
        }
    }
}