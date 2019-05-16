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
namespace MassTransit.Azure.ServiceBus.Core.Transport
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Contexts;
    using GreenPipes;
    using GreenPipes.Agents;
    using GreenPipes.Internals.Extensions;
    using Logging;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Core;
    using Microsoft.Extensions.Logging;
    using Transports.Metrics;
    using Util;


    public class Receiver :
        Supervisor,
        IReceiver
    {
        static readonly ILogger _logger = Logger.Get<Receiver>();

        readonly ClientContext _context;
        readonly TaskCompletionSource<bool> _deliveryComplete;
        readonly IBrokeredMessageReceiver _messageReceiver;
        protected readonly IDeliveryTracker Tracker;

        public Receiver(ClientContext context, IBrokeredMessageReceiver messageReceiver)
        {
            _context = context;
            _messageReceiver = messageReceiver;

            Tracker = new DeliveryTracker(HandleDeliveryComplete);
            _deliveryComplete = new TaskCompletionSource<bool>();
        }

        public Task DeliveryCompleted => _deliveryComplete.Task;
        bool IReceiver.IsStopping => IsStopping;

        public DeliveryMetrics GetDeliveryMetrics()
        {
            return Tracker.GetDeliveryMetrics();
        }

        public virtual Task Start()
        {
            _context.OnMessageAsync(OnMessage, ExceptionHandler);

            SetReady();

            return TaskUtil.Completed;
        }

        protected Task ExceptionHandler(ExceptionReceivedEventArgs args)
        {
            if (!(args.Exception is OperationCanceledException))
                _logger.LogError($"Exception received on receiver: {_context.InputAddress} during {args.ExceptionReceivedContext.Action}", args.Exception);

            if (Tracker.ActiveDeliveryCount == 0)
            {
                _logger.LogDebug("Receiver shutdown completed: {0}", _context.InputAddress);

                _deliveryComplete.TrySetResult(true);

                SetCompleted(TaskUtil.Faulted<bool>(args.Exception));
            }

            return Task.CompletedTask;
        }

        void HandleDeliveryComplete()
        {
            if (IsStopping)
            {
                _logger.LogDebug("Receiver shutdown completed: {0}", _context.InputAddress);

                _deliveryComplete.TrySetResult(true);
            }
        }

        protected override async Task StopSupervisor(StopSupervisorContext context)
        {
            _logger.LogDebug("Stopping receiver: {0}", _context.InputAddress);

            SetCompleted(ActiveAndActualAgentsCompleted(context));

            await Completed.ConfigureAwait(false);

            await _context.CloseAsync(context.CancellationToken).ConfigureAwait(false);
        }

        async Task ActiveAndActualAgentsCompleted(StopSupervisorContext context)
        {
            await Task.WhenAll(context.Agents.Select(x => Completed)).UntilCompletedOrCanceled(context.CancellationToken).ConfigureAwait(false);

            if (Tracker.ActiveDeliveryCount > 0)
                try
                {
                    await _deliveryComplete.Task.UntilCompletedOrCanceled(context.CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Stop canceled waiting for message consumers to complete: {0}", _context.InputAddress);
                }
        }

        async Task OnMessage(IReceiverClient messageReceiver, Message message, CancellationToken cancellationToken)
        {
            if (IsStopping)
            {
                await WaitAndAbandonMessage(messageReceiver, message).ConfigureAwait(false);
                return;
            }

            using (var delivery = Tracker.BeginDelivery())
            {
                _logger.LogDebug("Receiving {0}:{1}({2})", delivery.Id, message.MessageId, _context.EntityPath);

                await _messageReceiver.Handle(message, context => AddReceiveContextPayloads(context, messageReceiver, message)).ConfigureAwait(false);
            }
        }

        void AddReceiveContextPayloads(ReceiveContext receiveContext, IReceiverClient receiverClient, Message message)
        {
            MessageLockContext lockContext = new ReceiverClientMessageLockContext(receiverClient, message);

            receiveContext.GetOrAddPayload(() => lockContext);
            receiveContext.GetOrAddPayload(() => _context.GetPayload<NamespaceContext>());
        }

        protected async Task WaitAndAbandonMessage(IReceiverClient receiverClient, Message message)
        {
            try
            {
                await _deliveryComplete.Task.ConfigureAwait(false);

                await receiverClient.AbandonAsync(message.SystemProperties.LockToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogDebug($"Stopping receiver, abandoned message faulted: {_context.InputAddress}", exception);
            }
        }
    }
}
