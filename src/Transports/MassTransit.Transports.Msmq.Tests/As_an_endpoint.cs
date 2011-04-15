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
namespace MassTransit.Transports.Msmq.Tests
{
	using System;
	using Configuration;
	using Exceptions;
	using MassTransit.Serialization;
	using NUnit.Framework;
	using Rhino.Mocks;

	[TestFixture, Category("Integration")]
	public class Creating_an_endpoint_that_does_not_exist
	{
		private IMessageSerializer _serializer;
		private Uri _uri;

		[SetUp]
		public void Setup()
		{
			EndpointConfigurator.Defaults(x =>
				{
					x.CreateMissingQueues = false;
					x.PurgeOnStartup = false;
				});

			_serializer = MockRepository.GenerateStub<IMessageSerializer>();
			_uri = new Uri("msmq://localhost/idontexist_tx");
		}

		[Test]
		[ExpectedException(typeof (EndpointException))]
		public void Should_throw_an_endpoint_exception_from_the_msmq_endpoint_factory()
		{
		    var tfs = new[] {new MsmqTransportFactory()};
		    var epf = new EndpointFactory(tfs);
            
		    epf.BuildEndpoint(_uri, cfg =>
		    {
		        cfg.SetSerializer(_serializer);
		    });
		}

		[Test]
		[ExpectedException(typeof (EndpointException))]
		public void Should_throw_an_endpoint_exception_from_the_endpoint_factory()
		{
			IEndpointResolver ef = EndpointResolverConfigurator.New(x =>
			    {
			        x.AddTransportFactory<MsmqTransportFactory>();
			    });

			ef.GetEndpoint(_uri);
		}
	}
}