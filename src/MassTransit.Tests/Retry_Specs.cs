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
namespace MassTransit.Tests
{
    using System;
    using Magnum.Extensions;
    using Magnum.TestFramework;
    using Messages;
	using NUnit.Framework;
	using Rhino.Mocks;
	using TextFixtures;
    using TestFramework;

	[TestFixture]
	public class When_a_message_consumer_specifies_that_it_should_retry_a_message :
		LoopbackTestFixture
	{
		[Test]
		public void The_retry_count_should_be_set_on_the_message()
		{
			FutureMessage<PingMessage> future = new FutureMessage<PingMessage>();

			bool first = true;

			LocalBus.Subscribe<PingMessage>(message =>
				{
					if(first)
					{
						Assert.AreEqual(0, CurrentMessage.Headers.RetryCount);

						CurrentMessage.RetryLater();

						first = false;
					}
					else
					{
						Assert.AreEqual(1, CurrentMessage.Headers.RetryCount);

						future.Set(message);
					}
				});

			LocalBus.ShouldHaveSubscriptionFor<PingMessage>();

			LocalBus.Publish(new PingMessage());

			Assert.IsTrue(future.IsAvailable(15.Seconds()));
		}

	    [Test]
	    public void Should_do_something_nicely()
	    {
	        var ding = new PingMessage();

            var future = new FutureMessage<PingMessage>();

	        var bus = MockRepository.GenerateMock<IServiceBus>();
	        bus.Stub(x => x.Publish<PingMessage>(null)).Callback<PingMessage>(message =>
	            {
                    if(message != ding )
                        Assert.Fail("Bugger me");

	                future.Set(message);

	                return true;
	            });

	        bus.Publish(ding);

	        future.IsAvailable(TimeSpan.Zero).ShouldBeTrue();
	    }
	}
}