// Copyright 2007-2016 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
    using System.Threading;
    using System.Threading.Tasks;


    /// <summary>
    /// Limits the concurrency of the next section of the pipeline based on the concurrency limit
    /// specified.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class RateLimitFilter<T> :
        IFilter<T>,
        IDisposable
        where T : class, PipeContext
    {
        readonly TimeSpan _interval;
        readonly SemaphoreSlim _limit;
        readonly int _rateLimit;
        readonly Timer _timer;
        int _count;

        public RateLimitFilter(int rateLimit, TimeSpan interval)
        {
            _rateLimit = rateLimit;
            _interval = interval;
            _limit = new SemaphoreSlim(rateLimit);
            _timer = new Timer(Reset, null, interval, interval);
        }

        public void Dispose()
        {
            _limit?.Dispose();
            _timer?.Dispose();
        }

        void IProbeSite.Probe(ProbeContext context)
        {
            ProbeContext scope = context.CreateFilterScope("rateLimit");
            scope.Add("limit", _rateLimit);
            scope.Add("available", _limit.CurrentCount);
            scope.Add("interval", _interval);
        }

        [DebuggerNonUserCode]
        public async Task Send(T context, IPipe<T> next)
        {
            await _limit.WaitAsync(context.CancellationToken).ConfigureAwait(false);

            Interlocked.Increment(ref _count);

            await next.Send(context).ConfigureAwait(false);
        }

        void Reset(object state)
        {
            int processed = Interlocked.Exchange(ref _count, 0);
            if (processed > 0)
                _limit.Release(processed);
        }
    }
}