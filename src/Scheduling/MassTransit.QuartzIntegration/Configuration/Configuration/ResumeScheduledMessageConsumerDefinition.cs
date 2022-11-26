﻿namespace MassTransit.Configuration
{
    using MassTransit.Scheduling;
    using MassTransit.QuartzIntegration;


    public class ResumeScheduledMessageConsumerDefinition :
        ConsumerDefinition<ResumeScheduledMessageConsumer>
    {
        readonly QuartzEndpointDefinition _endpointDefinition;

        public ResumeScheduledMessageConsumerDefinition(QuartzEndpointDefinition endpointDefinition)
        {
            _endpointDefinition = endpointDefinition;

            EndpointDefinition = endpointDefinition;
        }

        protected override void ConfigureConsumer(IReceiveEndpointConfigurator endpointConfigurator,
            IConsumerConfigurator<ResumeScheduledMessageConsumer> consumerConfigurator)
        {
            endpointConfigurator.UseMessageRetry(r => r.Interval(5, 250));

            consumerConfigurator.Message<ResumeScheduledRecurringMessage>(m => m.UsePartitioner(_endpointDefinition.Partition, p => p.Message.CorrelationId));
        }
    }
}
