using System.Threading;

namespace EspMon
{
	internal class HostedController : Controller
	{
		PortDispatcher _dispatcher;
		private SynchronizationContext _sync;
		public HostedController()
		{
			_dispatcher = new PortDispatcher();
			_dispatcher.RefreshPortsRequested += _dispatcher_RefreshPortsRequested;
			_sync = SynchronizationContext.Current;
		}

		private void _dispatcher_RefreshPortsRequested(object sender, RefreshPortsEventArgs e)
		{
			_sync.Send((object state) => {
				foreach (var item in PortItems)
				{
					if(item.IsChecked)
					{
						e.PortNames.Add(item.Name);
					}
				}
			}, null);
			
		}

		protected override int GetCpuTMax()
		{
			return _dispatcher.CpuTMax;
		}

		protected override int GetGpuTMax()
		{
			return _dispatcher.GpuTMax;
		}

		protected override bool GetIsStarted()
		{
			return _dispatcher.IsStarted;
		}

		protected override void SetCpuTMax(int value)
		{
			_dispatcher.CpuTMax = value;
		}

		protected override void SetGpuTMax(int value)
		{
			_dispatcher.GpuTMax = value;
		}

		protected override void Start()
		{
			_dispatcher.Start();
		}

		protected override void StopAll()
		{
			_dispatcher.Stop();
		}

		protected override void OnClose()
		{
			base.OnClose();
			_dispatcher.Dispose();
		}
	}
}
