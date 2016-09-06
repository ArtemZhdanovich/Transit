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
namespace MassTransit.Pipeline.Filters
{
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using GreenPipes;
    using Policies;


    /// <summary>
    /// Uses a retry policy to handle exceptions, retrying the operation in according
    /// with the policy
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RepeatFilter<T> :
        IFilter<T>
        where T : class, PipeContext
    {
        readonly IRepeatPolicy _repeatPolicy;

        public RepeatFilter(IRepeatPolicy repeatPolicy)
        {
            _repeatPolicy = repeatPolicy;
        }

        void IProbeSite.Probe(ProbeContext context)
        {
            _repeatPolicy.Probe(context.CreateFilterScope("repeat"));
        }

        [DebuggerNonUserCode]
        public async Task Send(T context, IPipe<T> next)
        {
            using (IRepeatContext repeatContext = _repeatPolicy.GetRepeatContext())
            {
                await Attempt(repeatContext, context, next).ConfigureAwait(false);
            }
        }

        [DebuggerNonUserCode]
        static async Task Attempt(IRepeatContext repeatContext, T context, IPipe<T> next)
        {
            TimeSpan delay = TimeSpan.Zero;
            do
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, repeatContext.CancellationToken).ConfigureAwait(false);

                await next.Send(context).ConfigureAwait(false);
            }
            while (repeatContext.CanRepeat(out delay));
        }
    }
}