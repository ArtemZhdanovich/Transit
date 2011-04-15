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
namespace Starbucks.Cashier
{
	using MassTransit;
	using MassTransit.Services.HealthMonitoring.Configuration;
	using MassTransit.StructureMapIntegration;
	using StructureMap;

	public class CashierRegistry :
		MassTransitRegistryBase
	{
		private readonly IContainer _container;

		public CashierRegistry(IContainer container)
		{
			_container = container;

			RegisterInMemorySagaRepository();

			RegisterControlBus("msmq://localhost/starbucks_cashier_control", x => { x.SetConcurrentConsumerLimit(1); });

			RegisterServiceBus("msmq://localhost/starbucks_cashier", x =>
				{
					x.UseControlBus(_container.GetInstance<IControlBus>());
					x.SetConcurrentConsumerLimit(1); // a cashier cannot multi-task

					ConfigureSubscriptionClient("msmq://localhost/mt_subscriptions", x);

					x.ConfigureService<HealthClientConfigurator>(health => health.SetHeartbeatInterval(10));
				});
		}
	}
}