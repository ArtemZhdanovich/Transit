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
namespace Starbucks.Customer
{
	using MassTransit;
	using MassTransit.StructureMapIntegration;
	using StructureMap;

	public class CustomerRegistry :
		MassTransitRegistryBase
	{
		private readonly IContainer _container;

		public CustomerRegistry(IContainer container)
		{
			_container = container;

			RegisterControlBus("msmq://localhost/starbucks_customer_control", x => { x.SetConcurrentConsumerLimit(1); });

			RegisterServiceBus("msmq://localhost/starbucks_customer", x =>
				{
					x.UseControlBus(_container.GetInstance<IControlBus>());

					ConfigureSubscriptionClient("msmq://localhost/mt_subscriptions", x);
				});
		}
	}
}