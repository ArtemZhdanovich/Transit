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
namespace MassTransit
{
    using System;
    using GreenPipes;
    using PipeConfigurators;
    using Pipeline;


    public static class FilterConfiguratorExtensions
    {
        /// <summary>
        /// Adds a filter to the pipe
        /// </summary>
        /// <typeparam name="T">The context type</typeparam>
        /// <param name="configurator">The pipe configurator</param>
        /// <param name="filter">The filter to add</param>
        public static void UseFilter<T>(this IPipeConfigurator<T> configurator, IFilter<T> filter)
            where T : class, PipeContext
        {
            if (configurator == null)
                throw new ArgumentNullException(nameof(configurator));

            var pipeBuilderConfigurator = new FilterPipeSpecification<T>(filter);

            configurator.AddPipeSpecification(pipeBuilderConfigurator);
        }

        /// <summary>
        /// Adds a filter to the send pipeline
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="configurator"></param>
        /// <param name="filter"></param>
        public static void UseSendFilter<T>(this ISendPipeConfigurator configurator, IFilter<SendContext<T>> filter)
            where T : class
        {
            var specification = new FilterPipeSpecification<SendContext<T>>(filter);

            configurator.AddPipeSpecification(specification);
        }

        /// <summary>
        /// Adds a filter to the send pipeline
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="configurator"></param>
        /// <param name="filter"></param>
        public static void UseSendFilter(this ISendPipeConfigurator configurator, IFilter<SendContext> filter)
        {
            var specification = new FilterPipeSpecification<SendContext>(filter);

            configurator.AddPipeSpecification(specification);
        }


    }
}