// Copyright 2007-2010 The Apache Software Foundation.
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
namespace MassTransit.Tests.TextFixtures
{
	using System;
	using Configuration;
	using Magnum.Extensions;
	using MassTransit.Saga;
	using MassTransit.Serialization;
	using MassTransit.Transports;
	using NUnit.Framework;
	using Rhino.Mocks;

	[TestFixture]
	public abstract class EndpointTestFixture<TTransportFactory>
		where TTransportFactory : ITransportFactory
	{
		[SetUp]
		public void Setup()
		{
			ObjectBuilder = MockRepository.GenerateMock<IObjectBuilder>();

			XmlMessageSerializer serializer = new XmlMessageSerializer();
			ObjectBuilder.Stub(x => x.GetInstance<XmlMessageSerializer>()).Return(serializer);

			EndpointResolver = EndpointResolverConfigurator.New(x =>
				{
					x.SetObjectBuilder(ObjectBuilder);
					x.AddTransportFactory<TTransportFactory>();
					x.SetDefaultSerializer<XmlMessageSerializer>();

					AdditionalEndpointFactoryConfiguration(x);
				});
			ObjectBuilder.Stub(x => x.GetInstance<IEndpointResolver>()).Return(EndpointResolver);

			ServiceBusConfigurator.Defaults(x =>
				{
                    x.SetEndpointFactory(EndpointResolver);
					x.SetObjectBuilder(ObjectBuilder);
					x.SetReceiveTimeout(50.Milliseconds());
					x.SetConcurrentConsumerLimit(Environment.ProcessorCount*2);
				});

			EstablishContext();
		}

		[TearDown]
		public void Teardown()
		{
			TeardownContext();

			EndpointResolver.Dispose();
			EndpointResolver = null;
		}

		protected virtual void AdditionalEndpointFactoryConfiguration(IEndpointResolverConfigurator x)
		{
		}

		protected IEndpointResolver EndpointResolver { get; set; }

		protected IObjectBuilder ObjectBuilder { get; private set; }

		protected virtual void EstablishContext()
		{
		}

		protected virtual void TeardownContext()
		{
		}

		public static InMemorySagaRepository<TSaga> SetupSagaRepository<TSaga>(IObjectBuilder builder)
			where TSaga : class, ISaga
		{
			var sagaRepository = new InMemorySagaRepository<TSaga>();

			builder.Stub(x => x.GetInstance<ISagaRepository<TSaga>>())
				.Return(sagaRepository);

			return sagaRepository;
		}
	}
}