Azure Service Bus configuration options
"""""""""""""""""""""""""""""""""""""""

.. sourcecode:: csharp

    Bus.Factory.CreateUsingAzureServiceBus(cfg =>
    {
        cfg.Host(new Uri("sb://localhost"), host =>
        {
            host.OperationTimeout = TimeSpan.FromSeconds(5);
            host.TokenProvider = new ????();
        });
    });


About Azure Service Bus
