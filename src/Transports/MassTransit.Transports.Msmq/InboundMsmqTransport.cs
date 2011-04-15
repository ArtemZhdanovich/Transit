﻿// Copyright 2007-2011 The Apache Software Foundation.
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
namespace MassTransit.Transports.Msmq
{
	using System;
	using System.Messaging;
	using System.Threading;
	using Exceptions;
	using Internal;
	using log4net;

	public abstract class InboundMsmqTransport :
		IInboundTransport
	{
		private static readonly ILog _log = LogManager.GetLogger(typeof (InboundMsmqTransport));
		private readonly IMsmqEndpointAddress _address;
		private bool _disposed;
		private MessageQueue _queue;

		protected InboundMsmqTransport(IMsmqEndpointAddress address)
		{
			_address = address;
		}

		public IEndpointAddress Address
		{
			get { return _address; }
		}

		public virtual void Receive(Func<IReceiveContext, Action<IReceiveContext>> callback, TimeSpan timeout)
		{
			try
			{
				Connect();

				EnumerateQueue(callback, timeout);
			}
			catch (MessageQueueException ex)
			{
				HandleInboundMessageQueueException(ex, timeout);
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected bool EnumerateQueue(Func<IReceiveContext, Action<IReceiveContext>> receiver,
		                              TimeSpan timeout)
		{
			if (_disposed) 
				throw new ObjectDisposedException("The transport has been disposed: '{0}'".FormatWith(Address));

			bool received = false;

			using (MessageEnumerator enumerator = _queue.GetMessageEnumerator2())
			{
				if (_log.IsDebugEnabled)
					_log.DebugFormat("Enumerating endpoint: {0} ({1}ms)", Address, timeout);

				while (enumerator.MoveNext(timeout))
				{
					if (enumerator.Current == null)
					{
						if (_log.IsDebugEnabled)
							_log.DebugFormat("Current message was null while enumerating endpoint");

						continue;
					}

					string acceptedMessageId;
					Action<IReceiveContext> receive;
					using (var context = new MsmqReceiveContext(enumerator.Current))
					{
						receive = receiver(context);
						if (receive == null)
						{
							if (_log.IsDebugEnabled)
								_log.DebugFormat("SKIP:{0}:{1}", Address, context.MessageId);

							if (SpecialLoggers.Messages.IsInfoEnabled)
								SpecialLoggers.Messages.InfoFormat("SKIP:{0}:{1}", Address, context.MessageId);

							continue;
						}

						acceptedMessageId = context.MessageId;
					}

					ReceiveMessage(enumerator, timeout, receiveCurrent =>
						{
							using (var context = new MsmqReceiveContext(receiveCurrent()))
							{
								if (context.Message == null)
									throw new TransportException(Address.Uri,
										"Unable to remove message from queue: " + acceptedMessageId);

								if (context.MessageId != acceptedMessageId)
									throw new TransportException(Address.Uri,
										string.Format(
											"Received message does not match current message: ({0} != {1})",
											context.MessageId, acceptedMessageId));
								receive(context);

								received = true;
							}
						});
				}
			}

			return received;
		}

		protected virtual void ReceiveMessage(MessageEnumerator enumerator, TimeSpan timeout,
		                                      Action<Func<Message>> receiveAction)
		{
			receiveAction(() => enumerator.RemoveCurrent(timeout, MessageQueueTransactionType.None));
		}


		protected void Connect()
		{
			if (_queue != null)
				return;

			_queue = new MessageQueue(_address.FormatName, QueueAccessMode.Receive);
		}

		private void Disconnect()
		{
			if (_queue == null) 
				return;

			_queue.Dispose();
			_queue = null;
		}

		private void Reconnect()
		{
			Disconnect();
			Connect();
		}

		protected void HandleInboundMessageQueueException(MessageQueueException ex, TimeSpan timeout)
		{
			switch (ex.MessageQueueErrorCode)
			{
				case MessageQueueErrorCode.IOTimeout:
					break;

				case MessageQueueErrorCode.ServiceNotAvailable:
					if (_log.IsErrorEnabled)
						_log.Error("The message queuing service is not available, pausing for timeout period", ex);

					Thread.Sleep(timeout);
					Reconnect();
					break;

				case MessageQueueErrorCode.QueueNotAvailable:
				case MessageQueueErrorCode.AccessDenied:
				case MessageQueueErrorCode.QueueDeleted:
					if (_log.IsErrorEnabled)
						_log.Error("The message queue was not available: " + _address.FormatName, ex);

					Thread.Sleep(timeout);
					Reconnect();
					break;

				case MessageQueueErrorCode.QueueNotFound:
				case MessageQueueErrorCode.IllegalFormatName:
				case MessageQueueErrorCode.MachineNotFound:
					if (_log.IsErrorEnabled)
						_log.Error("The message queue was not found or is improperly named: " + _address.FormatName, ex);

					Thread.Sleep(timeout);
					Reconnect();
					break;

				case MessageQueueErrorCode.MessageAlreadyReceived:
					// we are competing with another consumer, no reason to report an error since
					// the message has already been handled.
					if (_log.IsDebugEnabled)
						_log.Debug(
							"The message was removed from the queue before it could be received. This could be the result of another service reading from the same queue.");
					break;

				case MessageQueueErrorCode.InvalidHandle:
				case MessageQueueErrorCode.StaleHandle:
					if (_log.IsErrorEnabled)
						_log.Error(
							"The message queue handle is stale or no longer valid due to a restart of the message queuing service: " +
							_address.FormatName, ex);


					Thread.Sleep(timeout);
					Reconnect();
					break;

				default:
					if (_log.IsErrorEnabled)
						_log.Error("There was a problem communicating with the message queue: " + _address.FormatName, ex);
					break;
			}
		}

		private void Dispose(bool disposing)
		{
			if (_disposed) return;
			if (disposing)
			{
				Disconnect();
			}

			_disposed = true;
		}

		~InboundMsmqTransport()
		{
			Dispose(false);
		}
	}
}