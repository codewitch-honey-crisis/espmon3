using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;

namespace EspMon
{
	internal abstract class Controller : INotifyPropertyChanged, INotifyPropertyChanging, IDisposable
	{
		private SynchronizationContext _syncContext;
		private ObservableCollection<PortItem> _ports;
		private bool _disposed;

		public event PropertyChangingEventHandler PropertyChanging;
		public event PropertyChangedEventHandler PropertyChanged;
		public const int Baud = 115200;
		protected Controller()
		{
			_ports = null;
			_syncContext = SynchronizationContext.Current;
		}
		protected abstract int GetCpuTMax();
		protected abstract int GetGpuTMax();
		protected abstract void SetCpuTMax(int value);
		protected abstract void SetGpuTMax(int value);

		public int CpuTMax {
			get { return GetCpuTMax(); }
			set {
				if (value != GetCpuTMax())
				{
					PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(CpuTMax)));
					SetCpuTMax(value);
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CpuTMax)));
				}
			}
		}
		public int GpuTMax
		{
			get { return GetGpuTMax(); }
			set
			{
				if (value != GetGpuTMax())
				{
					PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(GpuTMax)));
					SetGpuTMax(value);
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpuTMax)));
				}
			}
		}
		protected int PortIndex(string name)
		{
			int i = 0;
			foreach (var portItem in _ports)
			{
				if (name.Equals(portItem.Name, StringComparison.InvariantCultureIgnoreCase))
				{
					return i;
				}
				++i;
			}
			return -1;
		}
		protected static string[] GetRegPortNames()
		{
			var path = Assembly.GetExecutingAssembly().Location;
			path = Path.Combine(Path.GetDirectoryName(path), "EspMon.cfg");
			var regPortList = new List<string>();
			if (File.Exists(path))
			{
				using (var file = new StreamReader(path))
				{
					var line = file.ReadLine();
					line = file.ReadLine();
					while (null != (line = file.ReadLine()))
					{
						line = line.Trim();
						if (line.StartsWith("COM", StringComparison.InvariantCultureIgnoreCase))
						{
							var s = line.Substring(3);
							if (int.TryParse(s, out _))
							{
								regPortList.Add(line);
							}
						}
					}
				}
			}

			return regPortList.ToArray();
		}

		protected virtual void OnRefresh()
		{

		}
		public void Refresh()
		{
			ServiceController ctl = ServiceController.GetServices()
					.FirstOrDefault(s => s.ServiceName == "EspMon Service");
			var isInstalled = ctl != null;
			
			var portNames = SerialPort.GetPortNames();
			var portSet = new HashSet<string>(portNames, StringComparer.InvariantCultureIgnoreCase);
			if (isInstalled)
			{
				var regPortNames = GetRegPortNames();
				foreach (var name in regPortNames)
				{
					portSet.Add(name);
					_syncContext.Send(new SendOrPostCallback((object st) => { 
						if(0>PortIndex(name))
						{
							_ports.Add(new PortItem(name,true));
						}
					}), null);
				}
			}

			_syncContext.Send(new SendOrPostCallback((object st) => {
				for(int i = 0;i< _ports.Count;++i)
				{
					var port = _ports[i];
					if(!portSet.Contains(port.Name,StringComparer.InvariantCultureIgnoreCase))
					{
						if (port.Port != null && port.Port.IsOpen)
						{
							try
							{
								port.Port.Close();
							}
							catch { }
						}
						_ports.RemoveAt(i);
						--i;
					}
				}
			}), null);
			
			foreach (var port in portSet)
			{
				_syncContext.Send(new SendOrPostCallback((object st) => {
					if (0>PortIndex(port))
					{
						_ports.Add(new PortItem(port, false));
					}
				}), null);
			}
			_syncContext.Send(new SendOrPostCallback((object st) => {
				PropertyChanging?.Invoke(st, new PropertyChangingEventArgs(nameof(CpuTMax)));
				PropertyChanged?.Invoke(st, new PropertyChangedEventArgs(nameof(CpuTMax)));
				PropertyChanging?.Invoke(st, new PropertyChangingEventArgs(nameof(GpuTMax)));
				PropertyChanged?.Invoke(st, new PropertyChangedEventArgs(nameof(GpuTMax)));
				OnRefresh();
			}), this);

		}
		
		public ObservableCollection<PortItem> PortItems
		{
			get { return _ports; }
			set { _ports = value; }
		}
		protected abstract void Start();
		protected abstract void StopAll();
		protected abstract bool GetIsStarted();
		public bool IsStarted
		{
			get
			{
				return GetIsStarted();
			}
			set
			{
				bool started = GetIsStarted();
				if (started != value)
				{
					PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(IsStarted)));
					if (!started)
					{
						Start();
					}
					else
					{
						StopAll();
					}
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsStarted)));

				}
			}
		}
		protected virtual void OnClose()
		{
			StopAll();
		}
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				OnClose();
				_disposed = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~Controller()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}
	
}
