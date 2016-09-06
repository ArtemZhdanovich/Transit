// Copyright 2007-2015 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.Pipeline.Filters
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using GreenPipes;
    using Util;


    /// <summary>
    /// Connects multiple output pipes to a single input pipe
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TeeConsumeFilter<T> :
        IFilter<ConsumeContext<T>>,
        IConsumePipeConnector<T>,
        IRequestPipeConnector<T>
        where T : class
    {
        readonly Connectable<IPipe<ConsumeContext<T>>> _connections;
        readonly Lazy<IConnectPipeById<ConsumeContext<T>, Guid>> _requestConnections;

        public TeeConsumeFilter()
        {
            _connections = new Connectable<IPipe<ConsumeContext<T>>>();
            _requestConnections = new Lazy<IConnectPipeById<ConsumeContext<T>, Guid>>(ConnectRequestFilter);
        }

        public int Count => _connections.Count;

        public ConnectHandle ConnectConsumePipe(IPipe<ConsumeContext<T>> pipe)
        {
            return _connections.Connect(pipe);
        }

        void IProbeSite.Probe(ProbeContext context)
        {
            _connections.All(pipe =>
            {
                pipe.Probe(context);
                return true;
            });
        }

        [DebuggerNonUserCode]
        public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
        {
            await _connections.ForEachAsync(async pipe => await pipe.Send(context).ConfigureAwait(false)).ConfigureAwait(false);

            await next.Send(context).ConfigureAwait(false);
        }

        public ConnectHandle ConnectRequestPipe(Guid requestId, IPipe<ConsumeContext<T>> pipe)
        {
            return _requestConnections.Value.ConnectById(requestId, pipe);
        }

        IConnectPipeById<ConsumeContext<T>, Guid> ConnectRequestFilter()
        {
            var filter = new RequestConsumeFilter<T, Guid>(GetRequestId);

            IPipe<ConsumeContext<T>> pipe = Pipe.New<ConsumeContext<T>>(x => x.UseFilter(filter));

            _connections.Connect(pipe);

            return filter;
        }

        static Guid GetRequestId(ConsumeContext<T> context)
        {
            return context.RequestId ?? Guid.Empty;
        }
    }
}