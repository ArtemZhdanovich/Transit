﻿// Copyright 2007-2008 The Apache Software Foundation.
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
namespace Grid.Distributor.Shared
{
    using System;
    using MassTransit.Transports;
    using MassTransit.Transports.Msmq;
    using StructureMap;
    using Topshelf;
    using Topshelf.Configuration.Dsl;

    public interface IServiceInterface
    {
        void Start();
        void Stop();
    }

    public abstract class ServiceSetup
    {
        abstract public string ServiceName { get; set; }
        abstract public string DisplayName { get; set; }
        abstract public string Description { get; set; }
        abstract public string SourceQueue { get; set; }
        abstract public string SubscriptionQueue { get; set; }
        abstract public Action<ConfigurationExpression> ContainerSetup { get; set; }

        public void ConfigureService<T>(string[] args) where T : class, IServiceInterface
        {
            var cfg = RunnerConfigurator.New(c =>
            {
                c.SetServiceName(ServiceName);
                c.SetDisplayName(DisplayName);
                c.SetDescription(Description);

                c.RunAsLocalSystem();
                c.DependencyOnMsmq();

                c.ConfigureService<T>( s =>
                {
                    EndpointConfigurator.Defaults(def => def.CreateMissingQueues = true);
                    s.HowToBuildService(name =>
                    {
                        Container container = new Container(x =>
                        {
                            x.AddRegistry(new IocRegistry(SourceQueue, SubscriptionQueue));

                            ContainerSetup(x);
                        });

                        return container.GetInstance<T>();
                    });

                    s.WhenStarted(start => start.Start());
                    s.WhenStopped(stop => stop.Stop());
                });
            });

            ObjectFactory.AssertConfigurationIsValid();

            Runner.Host(cfg, args);
        }

    }
}