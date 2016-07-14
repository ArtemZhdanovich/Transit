﻿// Copyright 2007-2014 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.Tests.Courier
{
    using System;
    using System.Threading.Tasks;
    using MassTransit.Courier;
    using MassTransit.Courier.Contracts;
    using NUnit.Framework;
    using TestFramework;
    using TestFramework.Courier;


    [TestFixture]
    public class A_routing_slip_event_subscription :
        InMemoryTestFixture
    {
        [Test]
        public void Should_serialize_properly()
        {
            var builder = new RoutingSlipBuilder(Guid.NewGuid());
            builder.AddActivity("a", new Uri("loopback://locahost/execute_a"));

            builder.AddSubscription(new Uri("loopback://localhost/events"), RoutingSlipEvents.Completed | RoutingSlipEvents.Faulted);

            RoutingSlip routingSlip = builder.Build();

            string jsonString = routingSlip.ToJsonString();

            RoutingSlip loaded = RoutingSlipExtensions.GetRoutingSlip(jsonString);

            Assert.AreEqual(RoutingSlipEvents.Completed | RoutingSlipEvents.Faulted, loaded.Subscriptions[0].Events);
        }
    }


    [TestFixture]
    public class Subscription_without_variables :
        InMemoryActivityTestFixture
    {
        [Test]
        public async Task Should_not_receive_the_routing_slip_activity_log()
        {
            ConsumeContext<RoutingSlipActivityCompleted> context = await _activityCompleted;

            Assert.IsFalse(context.Message.Data.ContainsKey("OriginalValue"));
        }

        [Test]
        public async Task Should_not_receive_the_routing_slip_activity_variable()
        {
            ConsumeContext<RoutingSlipActivityCompleted> context = await _activityCompleted;

            Assert.IsFalse(context.Message.Variables.ContainsKey("Variable"));
        }

        [Test]
        public async Task Should_receive_the_routing_slip_activity_completed_event()
        {
            ConsumeContext<RoutingSlipActivityCompleted> context = await _activityCompleted;

            Assert.AreEqual(_trackingNumber, context.Message.TrackingNumber);
        }

        Task<ConsumeContext<RoutingSlipCompleted>> _completed;
        Task<ConsumeContext<RoutingSlipActivityCompleted>> _activityCompleted;
        Guid _trackingNumber;

        [OneTimeSetUp]
        public void Should_publish_the_completed_event()
        {
            _completed = SubscribeHandler<RoutingSlipCompleted>();
            _activityCompleted = SubscribeHandler<RoutingSlipActivityCompleted>();

            _trackingNumber = NewId.NextGuid();
            var builder = new RoutingSlipBuilder(_trackingNumber);
            builder.AddSubscription(Bus.Address, RoutingSlipEvents.ActivityCompleted, RoutingSlipEventContents.None);

            ActivityTestContext testActivity = GetActivityContext<TestActivity>();
            builder.AddActivity(testActivity.Name, testActivity.ExecuteUri, new
            {
                Value = "Hello",
            });

            builder.AddVariable("Variable", "Knife");

            Await(() =>  Bus.Execute(builder.Build()));
        }

        protected override void SetupActivities()
        {
            AddActivityContext<TestActivity, TestArguments, TestLog>(() => new TestActivity());
        }
    }


//    [TestFixture]
//    public class Adding_a_subscription_to_the_routing_slip
//    {
//        [Test]
//        public void Should_send_the_completed_event()
//        {
//            Assert.IsTrue(_test.Sent.Select<RoutingSlipCompleted>());
//        }
//
//        [Test]
//        public void Should_not_publish_the_completed_event()
//        {
//            Assert.IsFalse(_test.Published.Select<RoutingSlipCompleted>());
//        }
//
//        IConsumerTest<IBusTestScenario, ExecuteActivityHost<TestActivity, TestArguments>> _test;
//
//        [OneTimeSetUp]
//        public void Setup()
//        {
//            _test = TestFactory.ForConsumer<ExecuteActivityHost<TestActivity, TestArguments>>()
//                .InSingleBusScenario()
//                .New(x =>
//                {
//                    x.UseConsumerFactory(() =>
//                    {
//                        var compensateAddress = new Uri("loopback://localhost/mt_server");
//
//                        return new ExecuteActivityHost<TestActivity, TestArguments>(compensateAddress,
//                            new FactoryMethodExecuteActivityFactory<TestActivity, TestArguments>(_ => new TestActivity()));
//                    });
//
//                    var builder = new RoutingSlipBuilder(Guid.NewGuid());
//                    builder.AddActivity("test", new Uri("loopback://localhost/mt_client"), new
//                    {
//                        Value = "Hello",
//                    });
//                    builder.AddSubscription(new Uri("loopback://localhost/mt_client"), RoutingSlipEvents.Completed | RoutingSlipEvents.Faulted);
//
//                    var routingSlip = builder.Build();
//
//                    string jsonString = routingSlip.ToJsonString();
//
//                    Console.WriteLine(jsonString);
//
//                    x.Send(routingSlip);
//                });
//
//            _test.Execute();
//        }
//
//        [OneTimeTearDown]
//        public void Teardown()
//        {
//            _test.Dispose();
//        }
//
//
//        [Test]
//        public void Should_serialize_and_deserialize_properly()
//        {
//            var builder = new RoutingSlipBuilder(Guid.NewGuid());
//            builder.AddActivity("a", new Uri("loopback://locahost/execute_a"));
//
//            builder.AddSubscription(new Uri("loopback://localhost/events"), RoutingSlipEvents.Completed | RoutingSlipEvents.Faulted);
//
//            RoutingSlip routingSlip = builder.Build();
//
//            string jsonString = routingSlip.ToJsonString();
//
//            RoutingSlip loaded = RoutingSlipExtensions.GetRoutingSlip(jsonString);
//
//            Assert.AreEqual(RoutingSlipEvents.Completed | RoutingSlipEvents.Faulted, loaded.Subscriptions[0].Events);
//        }
//    }
}