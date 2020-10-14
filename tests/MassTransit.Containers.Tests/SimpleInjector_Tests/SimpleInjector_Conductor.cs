namespace MassTransit.Containers.Tests.SimpleInjector_Tests
{
    using System.Threading.Tasks;
    using Common_Tests;
    using Microsoft.Extensions.DependencyInjection;
    using NUnit.Framework;
    using SimpleInjector;


    public class SimpleInjector_Conductor :
        Common_Conductor
    {
        readonly Container _container;

        public SimpleInjector_Conductor(bool instanceEndpoint)
            : base(instanceEndpoint)
        {
            _container = new Container();
            _container.SetRequiredOptions();
            _container.AddMassTransit(ConfigureRegistration);
        }

        [OneTimeTearDown]
        public async Task Close_container()
        {
            await _container.DisposeAsync();
        }

        protected override void ConfigureServiceEndpoints(IBusFactoryConfigurator<IInMemoryReceiveEndpointConfigurator> configurator)
        {
            configurator.ConfigureServiceEndpoints(_container.GetRequiredService<IBusRegistrationContext>(), Options);
        }

        protected override IClientFactory GetClientFactory()
        {
            return _container.GetInstance<IClientFactory>();
        }
    }
}
