﻿// Copyright 2007-2015 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.Azure.ServiceBus.Core.Saga
{
    using System;
    using System.Threading.Tasks;
    using Context;
    using Logging;
    using MassTransit.Saga;
    using Microsoft.Extensions.Logging;
    using Util;


    public class MessageSessionSagaConsumeContext<TSaga, TMessage> :
        ConsumeContextProxyScope<TMessage>,
        SagaConsumeContext<TSaga, TMessage>
        where TMessage : class
        where TSaga : class, ISaga
    {
        static readonly ILogger _logger = Logger.Get<InMemorySagaRepository<TSaga>>();
        readonly MessageSessionContext _sessionContext;

        public MessageSessionSagaConsumeContext(ConsumeContext<TMessage> context, MessageSessionContext sessionContext, TSaga instance)
            : base(context)
        {
            _sessionContext = sessionContext;

            Saga = instance;
        }

        public override Guid? CorrelationId => Saga.CorrelationId;

        public async Task SetCompleted()
        {
            await RemoveState().ConfigureAwait(false);

            IsCompleted = true;

            _logger.LogDebug("SAGA:{0}:{1} Removed {2}", TypeMetadataCache<TSaga>.ShortName, Saga.CorrelationId,
                TypeMetadataCache<TMessage>.ShortName);
        }

        public bool IsCompleted { get; private set; }
        public TSaga Saga { get; }

        async Task RemoveState()
        {
            await _sessionContext.SetStateAsync(new byte[0]).ConfigureAwait(false);
        }
    }
}
