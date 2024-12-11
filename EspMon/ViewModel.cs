using Microsoft.Win32;

using System;
using Path = System.IO.Path;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace EspMon
{
	internal class ViewModel : INotifyPropertyChanging, INotifyPropertyChanged, IDisposable
	{
		int _flashProgress = 0;
		StringBuilder outputBuffer = new StringBuilder();
		private Controller _controller;
		private bool _disposed;
		private bool _isFlashing;
		public event EventHandler InstallComplete;
		public event EventHandler UninstallComplete;
		public event PropertyChangedEventHandler PropertyChanged;
		public event PropertyChangingEventHandler PropertyChanging;
		public ObservableCollection<PortItem> PortItems { get; private set; } = new ObservableCollection<PortItem>();
		public ViewModel()
		{
			ServiceController ctl = ServiceController.GetServices()
				.FirstOrDefault(s => s.ServiceName == "EspMon Service");
			if (ctl != null)
			{
				_controller = new SvcController();
				_controller.PortItems= PortItems;
			} else
			{
				_controller = new HostedController();
				_controller.PortItems = PortItems; ;
			}
		}
		public System.Windows.Visibility FlashingVisibility { 
			get { return _isFlashing?System.Windows.Visibility.Visible:System.Windows.Visibility.Hidden; } 
			set {
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(FlashingVisibility)));
				_isFlashing= value==System.Windows.Visibility.Visible;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlashingVisibility)));
			}
		}
		public System.Windows.Visibility MainVisibility
		{
			get { return !_isFlashing ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden; }
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(MainVisibility)));
				_isFlashing = value != System.Windows.Visibility.Visible;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MainVisibility)));
			}
		}
		public int FlashProgress
		{
			get { return _flashProgress; }
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(FlashProgress)));
				_flashProgress= value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlashProgress)));
			}
		}
		public System.Windows.Visibility FlashButtonVisibility
		{
			get {
				var path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "firmware.zip");
				return !_isFlashing && File.Exists(path) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden; 
			}
		}
		public bool HasFirmware
		{
			get
			{
				return File.Exists(Path.Combine(Assembly.GetEntryAssembly().Location,"firmware.zip"));
			}
		}
		public bool IsPersistent
		{
			get
			{
				ServiceController ctl = ServiceController.GetServices()
					.FirstOrDefault(s => s.ServiceName == "EspMon Service");
				return ctl != null;
			}
			set
			{
				bool inst = IsPersistent;
				if (inst != value)
				{
					PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(IsPersistent)));
					if (!inst)
					{
						var task = Task.Run(() => {
							ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
						});
						var sync = SynchronizationContext.Current;
						task.GetAwaiter().OnCompleted(() => {
							sync.Post((object state) => {
								InstallComplete?.Invoke(state, EventArgs.Empty);
							}, this);
						});
						var path = Assembly.GetExecutingAssembly().Location;
						path = Path.Combine(Path.GetDirectoryName(path), "EspMon.cfg");
						if(!File.Exists(path))
						{
							using (var file = new StreamWriter(path))
							{
								file.WriteLine(CpuTMax.ToString());
								file.WriteLine(GpuTMax.ToString());
							}
						}
						if (_controller != null)
						{
							_controller.IsStarted = false;
							_controller.Dispose();
						}
						var newCtl = new SvcController();
						newCtl.PropertyChanging += NewCtl_PropertyChanging;
						newCtl.PropertyChanged += NewCtl_PropertyChanged;
						newCtl.PortItems= PortItems;
						_controller = newCtl;
					}
					else
					{
						if (_controller is SvcController)
						{
							IsStarted = false;
							_controller?.Dispose();
						}
						var task = Task.Run(() =>
						{
							ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
						});
						var sync = SynchronizationContext.Current;
						task.GetAwaiter().OnCompleted(() => {
							sync.Post((object state) => { 
								UninstallComplete?.Invoke(state,EventArgs.Empty);
							}, this);
						});
						
						var newCtl = new HostedController();
						newCtl.PropertyChanging += NewCtl_PropertyChanging;
						newCtl.PropertyChanged += NewCtl_PropertyChanged;
						newCtl.PortItems = PortItems;
						_controller = newCtl;
					}
					Refresh();
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPersistent)));
				}
			}
		}
		public string OutputText
		{
			get
			{
				return outputBuffer.ToString();
			}
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(OutputText)));
				outputBuffer.Clear();
				outputBuffer.Append(value);
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputText)));
			}
		}
		public void ClearOutput()
		{
			PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(OutputText)));
			outputBuffer.Clear();
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputText)));
		}
		public void AppendOutputLine(string line)
		{
			PropertyChanging?.Invoke(this,new PropertyChangingEventArgs(nameof(OutputText)));
			outputBuffer.AppendLine(line.TrimEnd());
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputText)));
		}
		private void NewCtl_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(this, e);
		}

		private void NewCtl_PropertyChanging(object sender, PropertyChangingEventArgs e)
		{
			PropertyChanging?.Invoke(this, e);
		}

		public bool IsStarted
		{
			get
			{
				return _controller.IsStarted;
			}
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(IsStarted)));
				_controller.IsStarted = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsStarted)));
			}
		}
		public int CpuTMax
		{
			get { return _controller.CpuTMax; }
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(CpuTMax)));
				_controller.CpuTMax = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CpuTMax)));
			}
		}
		public int GpuTMax
		{
			get { return _controller.GpuTMax; }
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(GpuTMax)));
				_controller.GpuTMax = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpuTMax)));
			}
		}

		public void Refresh()
		{
			ClearPortItems();
			_controller.Refresh();
			foreach (PortItem item in _controller.PortItems)
			{
				PortItems.Add(item);
			}
		}
		protected void ClearPortItems()
		{
			foreach(var item in PortItems)
			{
				if(item.Port!=null && item.Port.IsOpen)
				{
					try { item.Port.Close(); } catch { }
				}
			}
			PortItems.Clear();
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if(_controller!=null && _controller is IDisposable disp) {  disp.Dispose(); }
				_disposed = true;
			}
		}

		~ViewModel()
		{
		     Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
	internal class PortItem : INotifyPropertyChanged
	{
		bool _isChecked;

		public event PropertyChangedEventHandler PropertyChanged;

		public string Name { get; private set; }
		public bool IsChecked
		{
			get
			{
				return _isChecked;
			}
			set
			{
				if (_isChecked != value)
				{
					_isChecked = value;
					if (!value)
					{
						if (Port != null && Port.IsOpen)
						{
							try { Port.Close(); } catch { }
						}
					}
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
				}
			}
		}
		public SerialPort Port { get; set; }
		public PortItem(string name, bool isChecked = false)
		{
			Name = name;
			IsChecked = isChecked;
			Port = null;
		}
	}
}
