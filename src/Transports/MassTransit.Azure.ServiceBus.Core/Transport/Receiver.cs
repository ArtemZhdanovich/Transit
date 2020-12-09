﻿namespace MassTransit.Azure.ServiceBus.Core.Transport
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Context;
    using Contexts;
    using GreenPipes.Agents;
    using GreenPipes.Internals.Extensions;
    using Logging;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Core;
    using Transports.Metrics;
    using Util;


    public class Receiver :
        Agent,
        IReceiver
    {
        readonly ClientContext _context;
        readonly TaskCompletionSource<bool> _deliveryComplete;
        readonly IBrokeredMessageReceiver _messageReceiver;

        public Receiver(ClientContext context, IBrokeredMessageReceiver messageReceiver)
        {
            _context = context;
            _messageReceiver = messageReceiver;

            messageReceiver.ZeroActivity += HandleDeliveryComplete;

            _deliveryComplete = TaskUtil.GetTask<bool>();
        }

        public DeliveryMetrics GetDeliveryMetrics()
        {
            return _messageReceiver.GetMetrics();
        }

        public virtual Task Start()
        {
            _context.OnMessageAsync(OnMessage, ExceptionHandler);

            SetReady();

            return TaskUtil.Completed;
        }

        protected Task ExceptionHandler(ExceptionReceivedEventArgs args)
        {
            var activeDispatchCount = _messageReceiver.ActiveDispatchCount;
            var requiresRecycle = true;

            if (args.Exception is MessageLockLostException)
                requiresRecycle = false;
            else if (args.Exception is ServiceBusException sbException)
                requiresRecycle = !sbException.IsTransient;

            if (args.Exception is ServiceBusCommunicationException communicationException && communicationException.IsTransient)
            {
                LogContext.Debug?.Log(args.Exception,
                    "Exception on Receiver {InputAddress} during {Action} ActiveDispatchCount({activeDispatch}) ErrorRequiresRecycle({requiresRecycle})",
                    _context.InputAddress, args.ExceptionReceivedContext.Action, activeDispatchCount, requiresRecycle);
            }
            else if (!(args.Exception is OperationCanceledException))
            {
                EnabledLogger? logger = requiresRecycle ? LogContext.Error : LogContext.Warning;

                logger?.Log(args.Exception,
                    "Exception on Receiver {InputAddress} during {Action} ActiveDispatchCount({activeDispatch}) ErrorRequiresRecycle({requiresRecycle})",
                    _context.InputAddress, args.ExceptionReceivedContext.Action, activeDispatchCount, requiresRecycle);
            }

            if (activeDispatchCount == 0 && requiresRecycle)
            {
                LogContext.Debug?.Log("Receiver shutdown completed: {InputAddress}", _context.InputAddress);

                _deliveryComplete.TrySetResult(true);

                SetCompleted(TaskUtil.Faulted<bool>(args.Exception));
            }

            return Task.CompletedTask;
        }

        async Task HandleDeliveryComplete()
        {
            if (IsStopping)
                _deliveryComplete.TrySetResult(true);
        }

        protected override async Task StopAgent(StopContext context)
        {
            await _context.ShutdownAsync().ConfigureAwait(false);

            SetCompleted(ActiveAndActualAgentsCompleted(context));

            await Completed.ConfigureAwait(false);

            await _context.CloseAsync().ConfigureAwait(false);
        }

        async Task ActiveAndActualAgentsCompleted(StopContext context)
        {
            if (_messageReceiver.ActiveDispatchCount > 0)
            {
                try
                {
                    await _deliveryComplete.Task.OrCanceled(context.CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    LogContext.Warning?.Log("Stop canceled waiting for message consumers to complete: {InputAddress}", _context.InputAddress);
                }
            }
        }

        Task OnMessage(IReceiverClient messageReceiver, Message message, CancellationToken cancellationToken)
        {
            LogContext.Warning?.Log("Received at {received} {expire}", DateTime.UtcNow, message.ExpiresAtUtc);
            return _messageReceiver.Handle(message, cancellationToken, context => AddReceiveContextPayloads(context, messageReceiver, message));
        }

        void AddReceiveContextPayloads(ReceiveContext receiveContext, IReceiverClient receiverClient, Message message)
        {
            MessageLockContext lockContext = new ReceiverClientMessageLockContext(receiverClient, message);

            receiveContext.GetOrAddPayload(() => lockContext);
            receiveContext.GetOrAddPayload(() => _context);
        }
    }
}
