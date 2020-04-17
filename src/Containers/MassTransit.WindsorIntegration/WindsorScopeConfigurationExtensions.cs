namespace MassTransit.WindsorIntegration
{
    using Castle.MicroKernel;
    using Scoping;


    public static class WindsorScopeConfigurationExtensions
    {
        /// <summary>
        /// Use scope for Send
        /// </summary>
        /// <param name="configurator">The send pipe configurator</param>
        /// /// <param name="kernel">IKernel</param>
        public static void UseSendScope(this IBusFactoryConfigurator configurator, IKernel kernel)
        {
            configurator.UseSendScope(kernel.Resolve<ISendScopeProvider>());
            configurator.UsePublishScope(kernel.Resolve<IPublishScopeProvider>());
        }
    }
}
