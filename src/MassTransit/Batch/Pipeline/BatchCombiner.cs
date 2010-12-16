// Copyright 2007-2010 The Apache Software Foundation.
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
namespace MassTransit.Batch.Pipeline
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Threading;
	using Common.Logging;
	using Magnum.Extensions;
	using Magnum.Threading;
	using MassTransit.Pipeline;

	public class BatchCombiner<TMessage, TBatchId> :
		IPipelineSink<TMessage>,
		Consumes<TMessage>.All,
		IEnumerable<TMessage>
		where TMessage : class, BatchedBy<TBatchId>
	{
		private static readonly ILog _log = LogManager.GetLogger(typeof (BatchCombiner<TMessage, TBatchId>));

		private readonly TBatchId _batchId;
		private readonly int _batchLength;
		private readonly Batch<TMessage, TBatchId> _batchMessage;
		private readonly Action<Batch<TMessage, TBatchId>> _consumer;
		private readonly TimeSpan _timeout = 30.Seconds();
		private ManualResetEvent _complete = new ManualResetEvent(false);
		private volatile bool _disposed;
		private int _messageCount;
		private Semaphore _messageRequested = new Semaphore(0, 1);
		private ReaderWriterLockedObject<Queue<TMessage>> _messages = new ReaderWriterLockedObject<Queue<TMessage>>(new Queue<TMessage>());
		private Semaphore _messageWaiting = new Semaphore(0, 1);

		public BatchCombiner(TBatchId batchId, int batchLength, Action<Batch<TMessage, TBatchId>> consumer)
		{
			_batchId = batchId;
			_batchLength = batchLength;
			_consumer = consumer;

			_batchMessage = new Batch<TMessage, TBatchId>(batchId, batchLength, this);
			if (batchLength <= 0)
				_complete.Set();

			ThreadPool.QueueUserWorkItem(BatchConsumerWorker);
		}

		public object BatchId
		{
			get { return _batchId; }
		}

		public void Consume(TMessage message)
		{
			_messages.WriteLock(x => x.Enqueue(message));
			_messageWaiting.Release();
		}

		public IEnumerator<TMessage> GetEnumerator()
		{
			_messageRequested.Release();

			var handles = new WaitHandle[] {_complete, _messageWaiting};

			// TODO This can hang on shutdown if we're waiting for a batch to finish, so we need to have a kill/cancel to shut it down
			int waitResult;
			while ((waitResult = WaitHandle.WaitAny(handles, _timeout, true)) == 1)
			{
				yield return _messages.WriteLock(x => x.Dequeue());

				if (Interlocked.Increment(ref _messageCount) == _batchLength)
				{
					_complete.Set();
					break;
				}

				_messageRequested.Release();
			}

			if (waitResult == WaitHandle.WaitTimeout)
			{
				// TODO _bus.Publish(new BatchTimeout<TMessage, TBatchId>(_batchId));
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public IEnumerable<Action<TMessage>> Enumerate(TMessage message)
		{
			if (IsCompleted())
				yield break;

			if (_batchId.Equals(message.BatchId) && _messageRequested.WaitOne(_timeout, false))
				yield return Consume;
		}

		public bool Inspect(IPipelineInspector inspector)
		{
			inspector.Inspect(this);

			return true;
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected void Dispose(bool disposing)
		{
			if (_disposed) return;
			if (disposing)
			{
				_messageRequested.Close();
				_messageRequested = null;

				_messageWaiting.Close();
				_messageWaiting = null;

				_complete.Close();
				_complete = null;

				_messages.Dispose();
				_messages = null;
			}

			_disposed = true;
		}

		private bool IsCompleted()
		{
			return _complete.WaitOne(0, false);
		}

		private void BatchConsumerWorker(object obj)
		{
			try
			{
				_consumer(_batchMessage);
			}
			catch (Exception ex)
			{
				_log.Error("Exception in Batch " + typeof (Batch<TMessage, TBatchId>).FullName + ":" + _batchId, ex);
			}
		}

		~BatchCombiner()
		{
			Dispose(false);
		}
	}
}