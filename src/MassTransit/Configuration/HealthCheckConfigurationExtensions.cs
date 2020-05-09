namespace MassTransit
{
    public static class HealthCheckConfigurationExtensions
    {
        public static void UseHealthCheck<TBus, TContainerContext>(this IBusFactoryConfigurator configurator,
            IRegistrationContext<TBus, TContainerContext> context)
            where TBus : IBus
            where TContainerContext : class
        {
            context.UseHealthCheck(configurator);
        }

        public static void UseHealthCheck<TContainerContext>(this IBusFactoryConfigurator configurator,
            IRegistrationContext<TContainerContext> context)
            where TContainerContext : class
        {
            context.UseHealthCheck(configurator);
        }
    }
}
