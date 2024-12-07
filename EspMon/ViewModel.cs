using Microsoft.Win32;

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;

namespace EspMon
{
	internal class ViewModel : INotifyPropertyChanging, INotifyPropertyChanged
	{
		private Controller _controller;
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
		public bool IsInstalled
		{
			get
			{
				ServiceController ctl = ServiceController.GetServices()
					.FirstOrDefault(s => s.ServiceName == "EspMon Service");
				return ctl != null;
			}
			set
			{
				bool inst = IsInstalled;
				if (inst != value)
				{
					PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(IsInstalled)));
					if (!inst)
					{
						ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
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
						}
						ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location});
						var newCtl = new HostedController();
						newCtl.PropertyChanging += NewCtl_PropertyChanging;
						newCtl.PropertyChanged += NewCtl_PropertyChanged;
						newCtl.PortItems = PortItems;
						_controller = newCtl;
					}
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstalled)));
				}
			}
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
