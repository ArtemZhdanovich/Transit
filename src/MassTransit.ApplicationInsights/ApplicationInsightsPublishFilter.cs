﻿// Copyright 2007-2019 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.ApplicationInsights
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using GreenPipes;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;

    public class ApplicationInsightsPublishFilter<T> :
        IFilter<T>
        where T : class, PublishContext
    {
        const string MessageId = nameof(MessageId);
        const string ConversationId = nameof(ConversationId);
        const string CorrelationId = nameof(CorrelationId);
        const string DestinationAddress = nameof(DestinationAddress);
        const string RequestId = nameof(RequestId);
        const string MessageType = nameof(MessageType);

        const string StepName = "MassTransit:Publish";
        const string DependencyType = "Queue";
        readonly Action<IOperationHolder<DependencyTelemetry>, T> _configureOperation;

        readonly TelemetryClient _telemetryClient;
        readonly string _telemetryHeaderRootKey;
        readonly string _telemetryHeaderParentKey;

        public ApplicationInsightsPublishFilter(TelemetryClient telemetryClient,
            Action<IOperationHolder<DependencyTelemetry>, T> configureOperation, string telemetryHeaderRootKey,
            string telemetryHeaderParentKey)
        {
            _telemetryClient = telemetryClient;
            _telemetryHeaderRootKey = telemetryHeaderRootKey;
            _telemetryHeaderParentKey = telemetryHeaderParentKey;
            _configureOperation = configureOperation;
        }

        public void Probe(ProbeContext context)
        {
            context.CreateFilterScope("TelemetryPublishFilter");
        }

        public async Task Send(T context, IPipe<T> next)
        {
            var contextType = context.GetType();
            var messageType = contextType.GetGenericArguments().FirstOrDefault()?.FullName ?? "Unknown";

            var telemetry = new DependencyTelemetry()
            {
                Name = $"{StepName} {messageType}",
                Type = DependencyType,
                Data = $"{StepName} {context.DestinationAddress}"
            };

            using (IOperationHolder<DependencyTelemetry> operation = _telemetryClient.StartOperation(telemetry))
            {
                _configureOperation?.Invoke(operation, context);

                context.Headers.Set(_telemetryHeaderRootKey, operation.Telemetry.Context.Operation.Id);
                context.Headers.Set(_telemetryHeaderParentKey, operation.Telemetry.Id);

                operation.Telemetry.Properties.Add(MessageType, messageType);

                if (context.MessageId.HasValue)
                    operation.Telemetry.Properties.Add(MessageId, context.MessageId.Value.ToString());

                if (context.ConversationId.HasValue)
                    operation.Telemetry.Properties.Add(ConversationId, context.ConversationId.Value.ToString());

                if (context.CorrelationId.HasValue)
                    operation.Telemetry.Properties.Add(CorrelationId, context.CorrelationId.Value.ToString());

                if (context.DestinationAddress != null)
                    operation.Telemetry.Properties.Add(DestinationAddress, context.DestinationAddress.ToString());

                if (context.RequestId.HasValue)
                    operation.Telemetry.Properties.Add(RequestId, context.RequestId.Value.ToString());

                try
                {
                    await next.Send(context).ConfigureAwait(false);

                    operation.Telemetry.Success = true;
                }
                catch (Exception e)
                {
                    _telemetryClient.TrackException(e, operation.Telemetry.Properties);

                    operation.Telemetry.Success = false;
                    throw;
                }
                finally
                {
                    _telemetryClient.StopOperation(operation);
                }
            }
        }
    }
}