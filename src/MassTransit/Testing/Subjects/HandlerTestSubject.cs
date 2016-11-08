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
namespace MassTransit.Testing.Subjects
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Configurators;
    using GreenPipes.Util;
    using ScenarioBuilders;
    using ScenarioConfigurators;


    public class HandlerTestSubject<TScenario, TSubject> :
        IHandlerTestSubject<TSubject>,
        IScenarioSpecification<TScenario>
        where TSubject : class
        where TScenario : IBusTestScenario
    {
        readonly MessageHandler<TSubject> _handler;
        ReceivedMessageList<TSubject> _received;

        public HandlerTestSubject(MessageHandler<TSubject> handler)
        {
            _handler = handler;
        }

        public IReceivedMessageList<TSubject> Received => _received;

        public ITestScenarioBuilder<TScenario> Configure(ITestScenarioBuilder<TScenario> builder)
        {
            _received = new ReceivedMessageList<TSubject>(builder.Timeout);

            var scenarioBuilder = builder as IBusTestScenarioBuilder;
            scenarioBuilder?.ConfigureReceiveEndpoint(x => x.Handler<TSubject>(HandleMessage));

            return builder;
        }

        public IEnumerable<TestConfiguratorResult> Validate()
        {
            if (_handler == null)
                yield return this.Failure("Handler", "must not be null");
        }

        async Task HandleMessage(ConsumeContext<TSubject> context)
        {
            try
            {
                await _handler(context).ConfigureAwait(false);

                _received.Add(context);
            }
            catch (Exception ex)
            {
                _received.Add(context, ex);
            }
        }

        public Task DisposeAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return TaskUtil.Completed;
        }
    }
}