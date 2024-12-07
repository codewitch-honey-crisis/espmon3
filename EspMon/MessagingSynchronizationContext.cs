using System;
using System.Threading;

namespace EspMon
{
	/// <summary>
	/// Provides a <see cref="SynchronizationContext"/> using <see cref="MessageQueue{T}"/>
	/// </summary>
#if TASKSLIB
	public
#endif
	class MessagingSynchronizationContext : SynchronizationContext
	{
		private struct Message
		{
			public readonly SendOrPostCallback Callback;
			public readonly object State;
			public readonly ManualResetEventSlim FinishedEvent;
			public Message(SendOrPostCallback callback, object state, ManualResetEventSlim finishedEvent)
			{
				Callback = callback;
				State = state;
				FinishedEvent = finishedEvent;
			}
			public Message(SendOrPostCallback callback, object state) : this(callback, state, null)
			{
			}
		}
		MessageQueue<Message> _messageQueue = new MessageQueue<Message>();
		/// <summary>
		/// Sends a message and does not wait
		/// </summary>
		/// <param name="callback">The delegate to execute</param>
		/// <param name="state">The state associated with the message</param>
		public override void Post(SendOrPostCallback callback, object state)
		{
			_messageQueue.Post(new Message(callback, state));
		}
		/// <summary>
		/// Sends a message and waits for completion
		/// </summary>
		/// <param name="callback">The delegate to execute</param>
		/// <param name="state">The state associated with the message</param>
		public override void Send(SendOrPostCallback callback, object state)
		{
			var ev = new ManualResetEventSlim(false);
			try
			{
				_messageQueue.Post(new Message(callback, state, ev));
				ev.Wait();
			}
			finally
			{
				ev.Dispose();
			}
		}
		/// <summary>
		/// Starts the message loop
		/// </summary>
		/// <param name="cancellationToken">The cancellation token</param>
		/// <exception cref="OperationCanceledException">The operation was canceled</exception>
		public void Start(CancellationToken cancellationToken)
		{
			Message msg;
			do
			{
				// blocks until a message comes in:
				msg = _messageQueue.Receive(cancellationToken);
				// execute the code on this thread
				msg.Callback?.Invoke(msg.State);
				// let Send() know we're done:
				if (null != msg.FinishedEvent)
					msg.FinishedEvent.Set();
				// exit on the quit message
			} while (null != msg.Callback && !cancellationToken.IsCancellationRequested);
			cancellationToken.ThrowIfCancellationRequested();
		}
		/// <summary>
		/// Starts the message loop
		/// </summary>
		public void Start()
		{
			Message msg;
			do
			{
				// blocks until a message comes in:
				msg = _messageQueue.Receive();
				// execute the code on this thread
				msg.Callback?.Invoke(msg.State);
				// let Send() know we're done:
				if (null != msg.FinishedEvent)
					msg.FinishedEvent.Set();
				// exit on the quit message
			} while (null != msg.Callback);
		}
		public bool Poll()
		{
			Message msg;
			if(!_messageQueue.IsEmpty)
			{
				// blocks until a message comes in:
				msg = _messageQueue.Receive();
				// execute the code on this thread
				msg.Callback?.Invoke(msg.State);
				// let Send() know we're done:
				if (null != msg.FinishedEvent)
					msg.FinishedEvent.Set();
				// exit on the quit message
				return msg.Callback != null;
			}
			return false;
		}
		/// <summary>
		/// Stops the message loop
		/// </summary>
		public void Stop()
		{
			var ev = new ManualResetEventSlim(false);
			try
			{
				// post the quit message
				_messageQueue.Post(new Message(null, null, ev));
				ev.Wait();
			}
			finally {
				ev.Dispose();
			}
		}
	}
}
