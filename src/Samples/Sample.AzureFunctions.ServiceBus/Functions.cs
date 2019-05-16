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
namespace Sample.AzureFunctions.ServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using GreenPipes;
    using MassTransit;
    using MassTransit.Logging;
    using MassTransit.WebJobs.ServiceBusIntegration;
    using Microsoft.Azure.EventHubs;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Extensions.Logging;


    public static class Functions
    {
        [FunctionName("SubmitOrder")]
        public static Task SubmitOrderAsync([ServiceBusTrigger("input-queue")] Message message, IBinder binder, Microsoft.Extensions.Logging.ILogger logger,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Creating brokered message receiver");

            var handler = Bus.Factory.CreateBrokeredMessageReceiver(binder, cfg =>
            {
                cfg.CancellationToken = cancellationToken;
                cfg.SetLog(logger);
                cfg.InputAddress = new Uri("sb://masstransit-build.servicebus.windows.net/input-queue");

                cfg.UseRetry(x => x.Intervals(10, 100, 500, 1000));
                cfg.Consumer(() => new SubmitOrderConsumer(cfg.Logger));
            });

            return handler.Handle(message);
        }

        [FunctionName("AuditOrder")]
        public static Task AuditOrderAsync([EventHubTrigger("input-hub")] EventData message, IBinder binder, Microsoft.Extensions.Logging.ILogger logger,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("Creating EventHub receiver");

            var handler = Bus.Factory.CreateEventDataReceiver(binder, cfg =>
            {
                cfg.CancellationToken = cancellationToken;
                cfg.SetLog(logger);
                cfg.InputAddress = new Uri("sb://masstransit-eventhub.servicebus.windows.net/input-hub");

                cfg.UseRetry(x => x.Intervals(10, 100, 500, 1000));
                cfg.Consumer(() => new AuditOrderConsumer(cfg.Logger));
            });

            return handler.Handle(message);
        }
    }


    public class SubmitOrderConsumer :
        IConsumer<SubmitOrder>
    {
        readonly ILogger _logger;

        public SubmitOrderConsumer(ILogger logger)
        {
            _logger = logger;
        }

        public Task Consume(ConsumeContext<SubmitOrder> context)
        {
            _logger.LogDebug("Processing Order: {0}", context.Message.OrderNumber);

            context.Publish<OrderReceived>(new
            {
                context.Message.OrderNumber,
                Timestamp = DateTime.UtcNow
            });

            return context.RespondAsync<OrderAccepted>(new {context.Message.OrderNumber});
        }
    }


    public class AuditOrderConsumer :
        IConsumer<OrderReceived>
    {
        readonly ILogger _logger;

        public AuditOrderConsumer(ILogger logger)
        {
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<OrderReceived> context)
        {
            _logger.LogDebug("Received Order: {0}", context.Message.OrderNumber);
        }
    }


    public interface SubmitOrder
    {
        string OrderNumber { get; }
    }


    public interface OrderAccepted
    {
        string OrderNumber { get; }
    }


    public interface OrderReceived
    {
        DateTime Timestamp { get; }

        string OrderNumber { get; }
    }
}
