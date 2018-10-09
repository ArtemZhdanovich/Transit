// Copyright 2007-2017 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.AutofacIntegration
{
    using System;
    using Autofac;
    using Saga;
    using Scoping;


    public class AutofacSagaRepositoryFactory :
        ISagaRepositoryFactory
    {
        readonly string _name;
        readonly Action<ContainerBuilder, ConsumeContext> _configureScope;
        readonly ILifetimeScopeProvider _scopeProvider;

        public AutofacSagaRepositoryFactory(ILifetimeScopeProvider scopeProvider, string name, Action<ContainerBuilder, ConsumeContext> configureScope)
        {
            _scopeProvider = scopeProvider;
            _name = name;
            _configureScope = configureScope;
        }

        ISagaRepository<T> ISagaRepositoryFactory.CreateSagaRepository<T>()
        {
            var repository = _scopeProvider.LifetimeScope.Resolve<ISagaRepository<T>>();

            var scopeProvider = new AutofacSagaScopeProvider<T>(_scopeProvider, _name, _configureScope);

            return new ScopeSagaRepository<T>(repository, scopeProvider);
        }
    }
}