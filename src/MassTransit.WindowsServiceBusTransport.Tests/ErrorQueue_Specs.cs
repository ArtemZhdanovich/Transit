﻿namespace MassTransit.WindowsServiceBusTransport.Tests
{
    using System;
    using System.Runtime.Serialization;
    using System.Threading.Tasks;
    using WindowsServiceBusTransport.Configuration;
    using NUnit.Framework;
    using Shouldly;
    using TestFramework.Messages;


    [TestFixture]
    public class A_serialization_exception :
        ServiceBusTestFixture
    {
        [Test]
        public async void Should_have_the_correlation_id()
        {
            ConsumeContext<PingMessage> context = await _errorHandler;

            context.CorrelationId.ShouldBe(_correlationId);
        }

        [Test]
        public async void Should_have_the_original_destination_address()
        {
            ConsumeContext<PingMessage> context = await _errorHandler;

            context.DestinationAddress.ShouldBe(InputQueueAddress);
        }

        [Test]
        public async void Should_have_the_original_fault_address()
        {
            ConsumeContext<PingMessage> context = await _errorHandler;

            context.FaultAddress.ShouldBe(BusAddress);
        }

        [Test]
        public async void Should_have_the_original_response_address()
        {
            ConsumeContext<PingMessage> context = await _errorHandler;

            context.ResponseAddress.ShouldBe(BusAddress);
        }

        [Test]
        public async void Should_have_the_original_source_address()
        {
            ConsumeContext<PingMessage> context = await _errorHandler;

            context.SourceAddress.ShouldBe(BusAddress);
        }

        [Test]
        public async void Should_move_the_message_to_the_error_queue()
        {
            await _errorHandler;
        }

        Task<ConsumeContext<PingMessage>> _errorHandler;
        readonly Guid? _correlationId = NewId.NextGuid();

        [TestFixtureSetUp]
        public void Setup()
        {
            Await(() => InputQueueSendEndpoint.Send(new PingMessage(), Pipe.Execute<SendContext<PingMessage>>(context =>
            {
                context.CorrelationId = _correlationId;
                context.ResponseAddress = context.SourceAddress;
                context.FaultAddress = context.SourceAddress;
            })));
        }

        protected override void ConfigureBusHost(IServiceBusBusFactoryConfigurator configurator, IServiceBusHost host)
        {
            configurator.ReceiveEndpoint(host, "input_queue_error", x =>
            {
                _errorHandler = Handled<PingMessage>(x);
            });
        }

        protected override void ConfigureInputQueueEndpoint(IServiceBusReceiveEndpointConfigurator configurator)
        {
            Handler<PingMessage>(configurator, async context =>
            {
                throw new SerializationException("This is fine, forcing death");
            });
        }
    }
}
