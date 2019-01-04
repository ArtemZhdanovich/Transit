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
namespace MassTransit.ApplicationInsights.Tests
{
    using System;
    using GreenPipes;
    using Microsoft.ApplicationInsights;
    using Moq;
    using NUnit.Framework;
    using System.Threading.Tasks;
    using Context;
    using GreenPipes.Payloads;
    using Microsoft.ApplicationInsights.DataContracts;


    [TestFixture]
    public class ApplicationInsightsConsumeFilter_Specs
    {
        Mock<SendHeaders> _mockHeaders;
        Mock<ReceiveContext> _mockReceiveContext;

        [SetUp]
        public void SetUp()
        {
            _mockHeaders = new Mock<SendHeaders>();

            _mockReceiveContext = new Mock<ReceiveContext>();
            _mockReceiveContext.Setup(c => c.InputAddress).Returns(new Uri("http://masstransit-project.com/"));
        }

        [Test]
        public async Task Should_send_context_to_next_pipe()
        {
            // Arrange.
            var mockConsumeContext = new Mock<ConsumeContext>();
            mockConsumeContext.Setup(c => c.Headers).Returns(_mockHeaders.Object);
            mockConsumeContext.Setup(c => c.ReceiveContext).Returns(_mockReceiveContext.Object);
            var filter = new ApplicationInsightsConsumeFilter<ConsumeContext>(new TelemetryClient(), null, "", "");

            var mockPipe = new Mock<IPipe<ConsumeContext>>();

            // Act.
            await filter.Send(mockConsumeContext.Object, mockPipe.Object);

            // Assert.
            mockPipe.Verify(action => action.Send(mockConsumeContext.Object), Times.Once);
        }

        [Test]
        public async Task Should_invoke_the_configure_operation()
        {
            // Arrange.

            var mockConsumeContext = new Mock<ConsumeContext>();
            mockConsumeContext.Setup(c => c.Headers).Returns(_mockHeaders.Object);
            mockConsumeContext.Setup(c => c.ReceiveContext).Returns(_mockReceiveContext.Object);
            bool configureOperationHasBeenCalled = false;

            var filter = new ApplicationInsightsConsumeFilter<ConsumeContext>(new TelemetryClient(), (holder, context) => configureOperationHasBeenCalled = true, "", "");

            // Act.
            await filter.Send(mockConsumeContext.Object, new Mock<IPipe<ConsumeContext>>().Object);

            // Assert.
            Assert.IsTrue(configureOperationHasBeenCalled);
        }

        [Test]
        public async Task Should_add_the_expected_properties_to_the_dependency_telemetry()
        {
            // Arrange.
            var messageId = Guid.NewGuid();
            var conversationId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var requestId = Guid.NewGuid();
            var inputAddress = new Uri("http://masstransit-project.com/");
            var destinationAddress = new Uri("sb://my-organization.servicebus.windows.net/MyNamespace/MyMessage");

            var mockReceiveContext = new Mock<ReceiveContext>();
            mockReceiveContext.Setup(c => c.InputAddress).Returns(inputAddress);

            var mockConsumeContext = new Mock<ConsumeContext<object>>();
            mockConsumeContext.Setup(c => c.ReceiveContext).Returns(mockReceiveContext.Object);
            mockConsumeContext.Setup(c => c.Headers).Returns(_mockHeaders.Object);
            mockConsumeContext.Setup(c => c.MessageId).Returns(messageId);
            mockConsumeContext.Setup(c => c.ConversationId).Returns(conversationId);
            mockConsumeContext.Setup(c => c.CorrelationId).Returns(correlationId);
            mockConsumeContext.Setup(c => c.RequestId).Returns(requestId);
            mockConsumeContext.Setup(c => c.DestinationAddress).Returns(destinationAddress);

            var consumeContextProxy = new ConsumeContextProxy<object>(mockConsumeContext.Object, new Mock<IPayloadCache>().Object);

            var capturedRequestTelemetry = default(RequestTelemetry);

            var filter = new ApplicationInsightsConsumeFilter<ConsumeContext>(new TelemetryClient(), (holder, context) => capturedRequestTelemetry = holder.Telemetry, "", "");

            // Act.
            await filter.Send(consumeContextProxy, new Mock<IPipe<ConsumeContext>>().Object);

            // Assert.
            Assert.IsNotNull(capturedRequestTelemetry);
            Assert.AreEqual(capturedRequestTelemetry.Properties["MessageId"], messageId.ToString());
            Assert.AreEqual(capturedRequestTelemetry.Properties["MessageType"], typeof(object).FullName);
            Assert.AreEqual(capturedRequestTelemetry.Properties["ConversationId"], conversationId.ToString());
            Assert.AreEqual(capturedRequestTelemetry.Properties["CorrelationId"], correlationId.ToString());
            Assert.AreEqual(capturedRequestTelemetry.Properties["DestinationAddress"], destinationAddress.ToString());
            Assert.AreEqual(capturedRequestTelemetry.Properties["InputAddress"], inputAddress.ToString());
            Assert.AreEqual(capturedRequestTelemetry.Properties["RequestId"], requestId.ToString());
        }
    }
}
