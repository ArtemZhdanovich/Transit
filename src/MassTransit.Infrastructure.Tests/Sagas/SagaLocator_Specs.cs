// Copyright 2007-2011 The Apache Software Foundation.
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
namespace MassTransit.Infrastructure.Tests.Sagas
{
	using System;
	using System.Collections.Generic;
	using System.Data;
	using System.Linq;
	using Magnum;
	using Magnum.Data;
	using MassTransit.Saga;
	using MassTransit.Tests.Messages;
	using MassTransit.Tests.Saga.Locator;
	using MassTransit.Tests.Saga.StateMachine;
	using NHibernate;
	using NHibernate.Cfg;
	using NHibernate.Tool.hbm2ddl;
	using NUnit.Framework;
	using Saga;

	[TestFixture, Category("Integration")]
	public class When_using_the_saga_locator_with_NHibernate
	{
		[SetUp]
		public void Setup()
		{
			_cfg = new Configuration();

			_cfg.SetProperty("connection.provider", "NHibernate.Connection.DriverConnectionProvider");
			_cfg.SetProperty("connection.driver_class", "NHibernate.Driver.SqlClientDriver");
			_cfg.SetProperty("connection.connection_string", _connectionString);
			_cfg.SetProperty("dialect", "NHibernate.Dialect.MsSql2005Dialect");
			_cfg.SetProperty("default_schema", "bus");
			_cfg.SetProperty("show_sql", "true");
			_cfg.SetProperty("proxyfactory.factory_class", "NHibernate.ByteCode.Castle.ProxyFactoryFactory, NHibernate.ByteCode.Castle");

			_cfg.AddAssembly(typeof (NHibernateSagaRepositoryForContainers<>).Assembly);
			_cfg.AddAssembly(typeof (RegisterUserStateMachine).Assembly);
			_cfg.AddAssembly(typeof (When_using_the_saga_locator_with_NHibernate).Assembly);

			_sessionFactory = _cfg.BuildSessionFactory();

			LocalContext.Current.Store(_sessionFactory);

			_sagaId = CombGuid.Generate();
		}

		private Guid _sagaId;

		private const string _connectionString = "Server=localhost;initial catalog=test;Trusted_Connection=yes";
		private Configuration _cfg;
		private ISessionFactory _sessionFactory;


		[Test]
		public void A_correlated_message_should_find_the_correct_saga()
		{
			using (var repository = new NHibernateSagaRepositoryForContainers<TestSaga>(_sessionFactory))
			{
				var ping = new PingMessage(_sagaId);

				var initiatePolicy = new InitiatingSagaPolicy<TestSaga, PingMessage>(x => false);

				var message = new PingMessage(_sagaId);
				repository.Send(x => x.CorrelationId == message.CorrelationId, initiatePolicy, message, saga => saga.Name = "Joe");


				List<TestSaga> sagas = repository.Where(x => x.CorrelationId == _sagaId).ToList();

				Assert.AreEqual(1, sagas.Count);
				Assert.IsNotNull(sagas[0]);
				Assert.AreEqual(_sagaId, sagas[0].CorrelationId);
			}
		}

		[Test]
		public void A_nice_interface_should_be_available_for_defining_saga_locators()
		{
		}

		[Test]
		public void A_plain_message_should_find_the_correct_saga_using_a_property()
		{
			var name = new NameMessage {Name = "Joe"};

			//new PropertySagaMessageSink<TestSaga,NameMessage>(context, bus, _repository, new ExistingSagaPolicy<TestSaga, NameMessage>());

			//	bool found = locator.TryGetSagaForMessage(name, out saga);

			//Assert.IsTrue(found);
			//	using (saga)
			{
				//		Assert.IsNotNull(saga);
				//		Assert.IsNotNull(saga.Instance);
				//		Assert.AreEqual(_sagaId, saga.Instance.CorrelationId);
			}
		}

		[Test]
		public void A_plain_message_with_an_unknown_relationship_should_not_find_it()
		{
			var name = new NameMessage {Name = "Tom"};

			//	ISagaLocator<TestSaga, NameMessage> locator =
			////		new PropertySagaLocator<TestSaga, NameMessage>(_repository, new ExistingSagaPolicy<TestSaga, NameMessage>(),
			//			(s, m) => s.Name == m.Name);

			//	InstanceScope<TestSaga> saga;
			//	bool found = locator.TryGetSagaForMessage(name, out saga);

			//	Assert.IsFalse(found);
			//	Assert.IsNull(saga);
		}

		[Test, Explicit]
		public void First_we_need_a_schema_to_test()
		{
			var schemaExport = new SchemaExport(_cfg);
			schemaExport.Drop(true, true);
			schemaExport.Create(true, true);
		}
	}

	public class NameMessage
	{
		public string Name { get; set; }
	}
}