﻿// Copyright 2007-2018 Chris Patterson, Dru Sellers, Travis Smith, et. al.
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
namespace MassTransit.ActiveMqTransport.Transport
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Apache.NMS;
    using Configuration;
    using Contexts;
    using GreenPipes;
    using GreenPipes.Agents;
    using Logging;
    using Microsoft.Extensions.Logging;


    public class ConnectionContextFactory :
        IPipeContextFactory<ConnectionContext>
    {
        readonly IActiveMqHostConfiguration _configuration;
        static readonly ILogger _logger = Logger.Get<ConnectionContextFactory>();

        public ConnectionContextFactory(IActiveMqHostConfiguration configuration)
        {
            _configuration = configuration;
        }

        IPipeContextAgent<ConnectionContext> IPipeContextFactory<ConnectionContext>.CreateContext(ISupervisor supervisor)
        {
            IAsyncPipeContextAgent<ConnectionContext> asyncContext = supervisor.AddAsyncContext<ConnectionContext>();

            var context = Task.Factory.StartNew(() => CreateConnection(asyncContext, supervisor), supervisor.Stopping, TaskCreationOptions.None,
                TaskScheduler.Default).Unwrap();

            void HandleConnectionException(Exception exception)
            {
                asyncContext.Stop($"Connection Exception: {exception}");
            }

            context.ContinueWith(task =>
            {
                task.Result.Connection.ExceptionListener += HandleConnectionException;

                asyncContext.Completed.ContinueWith(_ => task.Result.Connection.ExceptionListener -= HandleConnectionException);
            }, TaskContinuationOptions.OnlyOnRanToCompletion);

            return asyncContext;
        }

        IActivePipeContextAgent<ConnectionContext> IPipeContextFactory<ConnectionContext>.CreateActiveContext(ISupervisor supervisor,
            PipeContextHandle<ConnectionContext> context, CancellationToken cancellationToken)
        {
            return supervisor.AddActiveContext(context, CreateSharedConnection(context.Context, cancellationToken));
        }

        async Task<ConnectionContext> CreateSharedConnection(Task<ConnectionContext> context, CancellationToken cancellationToken)
        {
            var connectionContext = await context.ConfigureAwait(false);

            var sharedConnection = new SharedConnectionContext(connectionContext, cancellationToken);

            return sharedConnection;
        }

        async Task<ConnectionContext> CreateConnection(IAsyncPipeContextAgent<ConnectionContext> asyncContext, IAgent supervisor)
        {
            IConnection connection = null;
            try
            {
                if (supervisor.Stopping.IsCancellationRequested)
                    throw new OperationCanceledException($"The connection is stopping and cannot be used: {_configuration.Description}");

                    _logger.LogDebug("Connecting: {0}", _configuration.Description);

                connection = _configuration.Settings.CreateConnection();

                connection.Start();

                    _logger.LogDebug("Connected: {0} (client-id: {1}, version: {2})", _configuration.Description, connection.ClientId, connection.MetaData.NMSVersion);

                var connectionContext = new ActiveMqConnectionContext(connection, _configuration, supervisor.Stopped);

                await asyncContext.Created(connectionContext).ConfigureAwait(false);

                return connectionContext;
            }
            catch (OperationCanceledException)
            {
                await asyncContext.CreateCanceled().ConfigureAwait(false);
                throw;
            }
            catch (NMSConnectionException ex)
            {
                    _logger.LogDebug("ActiveMQ Connect failed:", ex);

                await asyncContext.CreateFaulted(ex).ConfigureAwait(false);

                connection?.Dispose();

                throw new ActiveMqConnectException("Connect failed: " + _configuration.Description, ex);
            }
        }
    }
}
