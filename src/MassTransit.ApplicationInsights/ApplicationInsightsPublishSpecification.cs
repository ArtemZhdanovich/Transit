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
namespace MassTransit.ApplicationInsights
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GreenPipes;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;

    public class ApplicationInsightsPublishSpecification<T> : IPipeSpecification<T> where T : class, PublishContext
    {
        readonly TelemetryClient _telemetryClient;
        readonly Action<IOperationHolder<DependencyTelemetry>, T> _configureOperation;

        public ApplicationInsightsPublishSpecification(TelemetryClient telemetryClient, Action<IOperationHolder<DependencyTelemetry>, T> configureOperation)
        {
            _telemetryClient = telemetryClient;
            _configureOperation = configureOperation;
        }

        public void Apply(IPipeBuilder<T> builder)
        {
            builder.AddFilter(new ApplicationInsightsPublishFilter<T>(_telemetryClient, _configureOperation));
        }

        public IEnumerable<ValidationResult> Validate()
        {
            return Enumerable.Empty<ValidationResult>();
        }
    }
}