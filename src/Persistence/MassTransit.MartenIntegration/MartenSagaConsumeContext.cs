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

namespace MassTransit.MartenIntegration
{
    using System;
    using System.Threading.Tasks;
    using Context;
    using Logging;
    using Marten;
    using Microsoft.Extensions.Logging;
    using Saga;
    using Util;


    public class MartenSagaConsumeContext<TSaga, TMessage> :
        ConsumeContextProxyScope<TMessage>,
        SagaConsumeContext<TSaga, TMessage>
        where TMessage : class
        where TSaga : class, ISaga
    {
        static readonly ILogger Log = Logger.Get<MartenSagaRepository<TSaga>>();
        readonly IDocumentSession _session;

        public MartenSagaConsumeContext(IDocumentSession session,
            ConsumeContext<TMessage> context, TSaga instance)
            : base(context)
        {
            _session = session;
            Saga = instance;
        }

        Guid? MessageContext.CorrelationId => Saga.CorrelationId;

        Task SagaConsumeContext<TSaga>.SetCompleted()
        {
            _session.Delete(Saga);
            _session.SaveChanges();
            IsCompleted = true;
            Log.LogDebug("SAGA:{0}:{1} Removed {2}", TypeMetadataCache<TSaga>.ShortName, TypeMetadataCache<TMessage>.ShortName,
                Saga.CorrelationId);

            return TaskUtil.Completed;
        }

        public TSaga Saga { get; }
        public bool IsCompleted { get; private set; }
    }
}
