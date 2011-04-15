// Copyright 2007-2008 The Apache Software Foundation.
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
namespace MassTransit.Distributor.Configuration
{
	using System;
	using Magnum.Reflection;
	using MassTransit.Configuration;

	public class SagaDistributorConfigurator
	{
		private readonly IServiceBusConfigurator _configurator;
		private readonly IEndpointResolver _endpointResolver;

		public SagaDistributorConfigurator(IServiceBusConfigurator configurator, IEndpointResolver endpointResolver)
		{
			_configurator = configurator;
			_endpointResolver = endpointResolver;
		}

		public void AddService(Type type)
		{
			this.FastInvoke(new[] {type}, "AddServiceForDataEvent");
		}

// ReSharper disable UnusedMember.Local
		private void AddServiceForDataEvent<TMessage>()
// ReSharper restore UnusedMember.Local
			where TMessage : class
		{
			_configurator.AddService(() => new Distributor<TMessage>(_endpointResolver));
		}
	}
}