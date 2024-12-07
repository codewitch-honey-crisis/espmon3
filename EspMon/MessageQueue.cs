using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EspMon
{
	/// <summary>
	/// Represents a thread safe asynchronous message queue
	/// </summary>
	/// <typeparam name="T">The type of the messages</typeparam>
#if TASKSLIB
	public
#endif       
	sealed class MessageQueue<T>
	{
		ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
		SemaphoreSlim _sync = new SemaphoreSlim(0);
		/// <summary>
		/// Indicates whether or not the queue is empty.
		/// </summary>
		public bool IsEmpty 
			=> _queue.IsEmpty;
		/// <summary>
		/// Indicates the count of messages in the queue
		/// </summary>
		public int Count
			=> _queue.Count;
		/// <summary>
		/// Sends a message
		/// </summary>
		/// <param name="message">The message to send</param>
		public void Post(T message)
		{
			_queue.Enqueue(message);
			_sync.Release(1);
		}
		/// <summary>
		/// Receives the next message from the queue
		/// </summary>
		/// <returns>The received message</returns>
		public T Receive()
		{
			T result;
			_sync.Wait();
			if (!_queue.TryDequeue(out result))
				throw new InvalidOperationException("The queue is empty");
			return result;
		}
		/// <summary>
		/// Receives the next message from the queue
		/// </summary>
		/// <param name="cancellationToken">A cancellation token</param>
		/// <returns>The received message</returns>
		/// <exception cref="OperationCanceledException">The operation was cancelled</exception>
		public T Receive(CancellationToken cancellationToken)
		{
			T result=default(T);
			_sync.Wait(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			if (!_queue.TryDequeue(out result))
				throw new InvalidOperationException("The queue is empty");
			cancellationToken.ThrowIfCancellationRequested();
			return result;
		}


		/// <summary>
		/// Asynchronously receives a message
		/// </summary>
		/// <returns>A <see cref="Task{T}"/> containing the message</returns>
		public async Task<T> ReceiveAsync()
		{
			// this method would require a 
			// state machine because loops
			// are not async by nature.
			// however, we make the C#
			// compiler generate it for us
			// using the "async/await" 
			// keywords.
			// because an await occurs
			// inside a loop, the C#
			// compiler will unroll the
			// loop and turn it into 
			// a goto based state machine
			// that way we don't have to
			// code it ourselves
			T result;
			await _sync.WaitAsync();
			if(!_queue.TryDequeue(out result))
			{
				throw new InvalidOperationException("The queue is empty");
			}
			return result;
		}
		/// <summary>
		/// Asynchronously receives a message
		/// </summary>
		/// <param name="cancellationToken">A cancelation token</param>
		/// <returns>A <see cref="Task{T}"/> containing the message</returns>
		/// <exception cref="OperationCanceledException">The operation was canceled</exception>
		public async Task<T> ReceiveAsync(CancellationToken cancellationToken)
		{
			// this method would require a 
			// state machine because loops
			// are not async by nature.
			// however, we make the C#
			// compiler generate it for us
			// using the "async/await" 
			// keywords.
			// because an await occurs
			// inside a loop, the C#
			// compiler will unroll the
			// loop and turn it into 
			// a goto based state machine
			// that way we don't have to
			// code it ourselves
			T result=default(T);
			await _sync.WaitAsync(cancellationToken);
			cancellationToken.ThrowIfCancellationRequested();
			if (!_queue.TryDequeue(out result))
				throw new InvalidOperationException("The queue is empty");
			cancellationToken.ThrowIfCancellationRequested();
			return result;
		}
		/// <summary>
		/// Polls for a message
		/// </summary>
		/// <param name="result">The message to receive</param>
		/// <returns>True if a message is available, otherwise false. If the result is false <paramref name="result"/> is undefined.</returns>
		/// <remarks>This function never waits</remarks>
		public bool Poll(out T result)
			=> _queue.TryDequeue(out result);
	}
}
