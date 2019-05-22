﻿namespace MassTransit.AspNetCoreIntegration
{
    using System;
    using ExtensionsDependencyInjectionIntegration;
    using HealthChecks;
    using Logging;
    using Logging.Tracing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;


    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Register and hosts the resolved bus with all required interfaces.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="createBus">Bus factory that loads consumers and sagas from IServiceProvider</param>
        /// <param name="configureHealthChecks">Optional, allows you to specify custom health check names</param>
        /// <returns></returns>
        public static IServiceCollection AddMassTransit(this IServiceCollection services, Func<IServiceProvider, IBusControl> createBus,
            Action<HealthCheckOptions> configureHealthChecks = null)
        {
            services.AddMassTransit(x =>
            {
                x.AddBus(provider =>
                {
                    var loggerFactory = provider.GetService<ILoggerFactory>();

                    if (loggerFactory != null && Logger.Current.GetType() == typeof(TraceLogger))
                        ExtensionsLoggingIntegration.ExtensionsLogger.Use(loggerFactory);

                    return createBus(provider);
                });
            });

            services.AddSimplifiedHostedService(configureHealthChecks);

            return services;
        }

        /// <summary>
        /// Register and hosts the resolved bus with all required interfaces.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="createBus">Bus factory that loads consumers and sagas from IServiceProvider</param>
        /// <param name="configure">Use MassTransit DI extensions for IServiceCollection to register consumers and sagas</param>
        /// <param name="configureHealthChecks">Optional, allows you to specify custom health check names</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static IServiceCollection AddMassTransit(this IServiceCollection services, Func<IServiceProvider, IBusControl> createBus,
            Action<IServiceCollectionConfigurator> configure, Action<HealthCheckOptions> configureHealthChecks = null)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            services.AddMassTransit(x =>
            {
                configure(x);

                x.AddBus(provider =>
                {
                    var loggerFactory = provider.GetService<ILoggerFactory>();

                    if (loggerFactory != null && Logger.Current.GetType() == typeof(TraceLogger))
                        ExtensionsLoggingIntegration.ExtensionsLogger.Use(loggerFactory);

                    return createBus(provider);
                });
            });

            services.AddSimplifiedHostedService(configureHealthChecks);

            return services;
        }

        /// <summary>
        /// Register and hosts a given bus instance with all required interfaces.
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="bus">The bus instance</param>
        /// <param name="loggerFactory">Optional: ASP.NET Core logger factory instance</param>
        /// <param name="configureHealthChecks">Optional, allows you to specify custom health check names</param>
        /// <returns></returns>
        public static IServiceCollection AddMassTransit(this IServiceCollection services, IBusControl bus, ILoggerFactory loggerFactory = null,
            Action<HealthCheckOptions> configureHealthChecks = null)
        {
            services.AddMassTransit(x =>
            {
                x.AddBus(provider =>
                {
                    if (loggerFactory == null)
                        loggerFactory = provider.GetService<ILoggerFactory>();

                    if (loggerFactory != null && Logger.Current.GetType() == typeof(TraceLogger))
                        ExtensionsLoggingIntegration.ExtensionsLogger.Use(loggerFactory);

                    return bus;
                });
            });

            services.AddSimplifiedHostedService(configureHealthChecks);

            return services;
        }

        static void AddSimplifiedHostedService(this IServiceCollection services, Action<HealthCheckOptions> configureHealthChecks)
        {
            var busCheck = new SimplifiedBusHealthCheck();
            var receiveEndpointCheck = new ReceiveEndpointHealthCheck();

            var healthCheckOptions = HealthCheckOptions.Default;
            configureHealthChecks?.Invoke(healthCheckOptions);

            services.AddHealthChecks()
                .AddBusHealthCheck(healthCheckOptions.BusHealthCheckName, busCheck)
                .AddBusHealthCheck(healthCheckOptions.ReceiveEndpointHealthCheckName, receiveEndpointCheck);

            services.AddSingleton<IHostedService>(p =>
            {
                var bus = p.GetRequiredService<IBusControl>();
                var loggerFactory = p.GetService<ILoggerFactory>();

                return new MassTransitHostedService(bus, loggerFactory, busCheck, receiveEndpointCheck);
            });
        }

        static IHealthChecksBuilder AddBusHealthCheck(this IHealthChecksBuilder builder, string healthCheckName, IHealthCheck healthCheck)
        {
            return builder.AddCheck(healthCheckName, healthCheck, HealthStatus.Unhealthy, new[] {"ready"});
        }
    }
}
