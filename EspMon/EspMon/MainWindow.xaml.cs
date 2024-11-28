using OpenHardwareMonitor.Hardware;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;


namespace EspMon
{
	// traverses OHWM data
	public class OhwmUpdateVisitor : IVisitor
	{
		public void VisitComputer(IComputer computer)
		{
			computer.Traverse(this);
		}
		public void VisitHardware(IHardware hardware)
		{
			hardware.Update();
			foreach (IHardware subHardware in hardware.SubHardware)
				subHardware.Accept(this);
		}
		public void VisitSensor(ISensor sensor) { }
		public void VisitParameter(IParameter parameter) { }
	}
	public class PortItem : INotifyPropertyChanged
	{
		private SynchronizationContext _syncContext;
		public const int BaudRate = 115200;
		SerialPort _port;
		WeakReference<MainWindowViewModel> _parent;
		bool _isChecked;
		string _name;
		public PortItem(SynchronizationContext syncContext, string name,MainWindowViewModel parent)
		{
			_syncContext = syncContext;
			_name = name;
			_parent = new WeakReference<MainWindowViewModel>(parent);
			_port = new SerialPort(_name, BaudRate);
			_port.DataReceived += DataReceived;
		}
		private MainWindowViewModel _Parent
		{
			get
			{
				MainWindowViewModel result;
				if(!_parent.TryGetTarget(out result))
				{
					throw new ObjectDisposedException(nameof(MainWindowViewModel));
				}
				return result;
			}
		}
		public bool IsChecked
		{
			get => _isChecked;
			set
			{
				if (_isChecked != value)
				{
					if(_Parent.IsStarted)
					{
						if(value)
						{
							if(Port!=null && !Port.IsOpen)
							{
								Port.Open();
							}
						} else
						{
							ClosePort();
						}
					}
					_isChecked = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
				}
			}
		}
		
		public SerialPort Port
		{
			get { return _port; }
		}
		public void ClosePort()
		{
			if (_port != null && _port.IsOpen)
			{
				_port.Close();
			}
		}
		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				if (0 == string.Compare(value, _name, StringComparison.InvariantCultureIgnoreCase))
				{
					_name = value;
					if (_port != null)
					{
						try
						{
							_port.DataReceived -= DataReceived;
							_port.Close();
						}
						catch(ObjectDisposedException)
						{
							// model is closing
						}
					}
					_port = new SerialPort(value, BaudRate);
					_port.DataReceived += DataReceived;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
				}
			}
		}

		private void DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
		
			if(_Parent==null || _Parent.Closing)
			{
				return;
			}
			int cpuTMax = 90;
			int gpuTMax = 80;
			int cpuUsage = 0;
			int cpuTemp = 0;
			int gpuUsage = 0;
			int gpuTemp = 0;
			
			cpuTMax = _Parent.CpuTMax;
			gpuTMax = _Parent.GpuTMax;
			cpuUsage = _Parent.CpuUsage;
			cpuTemp = _Parent.CpuTemp;
			gpuUsage = _Parent.GpuUsage;
			gpuTemp = _Parent.GpuTemp;
			
			int i=_port.ReadByte();
			if (i == 1)
			{
				var ba = new byte[7];
				ba[0] = 1;
				double v = ((double)cpuUsage / (double)100); ;
				ba[1] = (byte)cpuTMax;
				ba[2] = (byte)gpuTMax;
				ba[3] = (byte)(v * 255);
				v = ((double)cpuTemp / (double)cpuTMax); ;
				ba[4] = (byte)(v * 255);
				v = ((double)gpuUsage / (double)100); ;
				ba[5] = (byte)(v * 255);
				v = ((double)gpuTemp / (double)gpuTMax); ;
				ba[6] = (byte)(v * 255);

				_port.Write(ba, 0, ba.Length);
				_port.BaseStream.Flush();
			} else
			{
				_port.ReadExisting();
			}
			
		}

		public event PropertyChangedEventHandler PropertyChanged;

	}
	public partial class MainWindowViewModel : INotifyPropertyChanged, INotifyPropertyChanging
	{
		private class _ThreadControl
		{
			WeakReference<MainWindowViewModel> _parent;
			long _cancel;
			public MainWindowViewModel Parent
			{
				get
				{
					MainWindowViewModel result;
					if (!_parent.TryGetTarget(out result))
					{
						throw new ObjectDisposedException(nameof(MainWindowViewModel));
					}
					return result;
				}
			}
			public bool Cancel
			{
				get
				{
					if(0==Interlocked.Read(ref _cancel))
					{
						return false;
					}
					return true;
				}
				set
				{
					Interlocked.Exchange(ref _cancel, value?1:0);
				}
			}
			public _ThreadControl(MainWindowViewModel parent)
			{
				_parent = new WeakReference<MainWindowViewModel>(parent);
				_cancel = 0;
			}
		}
		SynchronizationContext _syncContext = SynchronizationContext.Current;
		private Computer computer = null;
		_ThreadControl _threadControl = null;
		Thread _updateThread=null;
		volatile int _cpuTMax =90;
		volatile int _gpuTMax=80;
		volatile int _cpuUsage = 0;
		volatile int _cpuTemp = 0;
		volatile int _gpuUsage = 0;
		volatile int _gpuTemp = 0;
		bool _isStarted = false;
		public ObservableCollection<PortItem> Items { get; } = new ObservableCollection<PortItem>();
		public void Refresh()
		{
			var ports = SerialPort.GetPortNames();
			var portsChecked = new HashSet<string> (ports.Length,StringComparer.InvariantCultureIgnoreCase);
			foreach(var item in Items)
			{
				if (item.IsChecked)
				{
					portsChecked.Add(item.Name);
				}
			}
			Items.Clear();
			foreach(var name in ports)
			{
				var item = new PortItem(_syncContext, name,this);
				item.IsChecked = portsChecked.Contains(name); 
				Items.Add(item);
			}
		}
		public int CpuUsage
		{
			get { return _cpuUsage; }
		}
		public int CpuTemp
		{
			get { return _cpuTemp; }
		}
		public int GpuUsage
		{
			get { return _gpuUsage; }
		}
		public int GpuTemp
		{
			get { return _gpuTemp; }
		}
		void _CollectSystemInfoThread(object state)
		{
			_ThreadControl tctrl = (_ThreadControl)state;
			int cpuTMax =90;
			int gpuTMax = 80;
			int cpuUsage = 0;
			int cpuTemp = 0;
			int gpuUsage = 0;
			int gpuTemp = 0;
			
			cpuTMax = tctrl.Parent.CpuTMax;
			gpuTMax = tctrl.Parent.GpuTMax;
			cpuUsage = tctrl.Parent.CpuUsage;
			cpuTemp = tctrl.Parent.CpuTemp;
			gpuUsage = tctrl.Parent.GpuUsage;
			gpuTemp = tctrl.Parent.GpuTemp;

			
			Computer computer = new Computer() { CPUEnabled = true, GPUEnabled = true };
			computer.Open();
			while (!tctrl.Cancel)
			{
				// use OpenHardwareMonitorLib to collect the system info
				var updateVisitor = new OhwmUpdateVisitor();
				computer.Accept(updateVisitor);
				for (int i = 0; i < computer.Hardware.Length; i++)
				{
					if (computer.Hardware[i].HardwareType == HardwareType.CPU)
					{
						for (int j = 0; j < computer.Hardware[i].Sensors.Length; j++)
						{
							var sensor = computer.Hardware[i].Sensors[j];
							if (sensor.SensorType == SensorType.Temperature &&
								sensor.Name.Contains("CPU Package"))
							{
								//if (getCpuTMax)
								//{
								/*for (int k = 0; k < sensor.Parameters.Length; ++k)
								{
									var p = sensor.Parameters[i];
									if (p.Name.ToLowerInvariant().Contains("tjmax"))
									{
										
									}
								}*/
								//}
								// store
								float f = sensor.Value.GetValueOrDefault();
								if (f != 0)
								{
									cpuTemp = (int)Math.Round(f);
								}
							}
							else if (sensor.SensorType == SensorType.Load &&
								sensor.Name.Contains("CPU Total"))
							{
								// store
								cpuUsage = (int)Math.Round(sensor.Value.GetValueOrDefault());
							}
						}
					}
					if (cpuTMax < 1 || cpuTMax > 255)
					{
						cpuTMax = 90;
					}
					if (computer.Hardware[i].HardwareType == HardwareType.GpuAti ||
						computer.Hardware[i].HardwareType == HardwareType.GpuNvidia)
					{
						for (int j = 0; j < computer.Hardware[i].Sensors.Length; j++)
						{
							var sensor = computer.Hardware[i].Sensors[j];
							if (sensor.SensorType == SensorType.Temperature &&
								sensor.Name.Contains("GPU Core"))
							{
								// store
								gpuTemp = (int)Math.Round(sensor.Value.GetValueOrDefault());
							}
							else if (sensor.SensorType == SensorType.Load &&
								sensor.Name.Contains("GPU Core"))
							{
								// store
								gpuUsage = (int)Math.Round(sensor.Value.GetValueOrDefault());
							}
						}
					}
					if (_gpuTMax < 1 || _gpuTMax > 255)
					{
						_gpuTMax = 80;
					}

				}
				tctrl.Parent._cpuTMax = cpuTMax;
				tctrl.Parent._gpuTMax = gpuTMax;
				tctrl.Parent._cpuUsage = cpuUsage;
				tctrl.Parent._cpuTemp = cpuTemp;
				tctrl.Parent._gpuUsage= gpuUsage;
				tctrl.Parent._gpuTemp = gpuTemp;
				Thread.Sleep(100);
			}
			computer.Close();
			computer = null;
		
		}
		
		public MainWindowViewModel()
		{
			Refresh();
		}
		public bool Closing
		{
			get
			{
				return _updateThread != null && _threadControl != null && _threadControl.Cancel;
			}
		}
		public void Close()
		{
			_StopAll();
		}
		private void _StopAll()
		{
			foreach (var item in Items)
			{
				item.ClosePort();
			}
			if (computer != null)
			{
				computer.Close();
			}
			computer = null;
			_isStarted = false;
			if(_updateThread != null)
			{
				_threadControl.Cancel = true;
				_updateThread.Join();
				_updateThread = null;
			}
		}
		public bool IsStarted { 
			get {
				return _isStarted;
			}
			set
			{
				if (value == _isStarted)
				{
					return;
				}
				PropertyChangingEventArgs e = new PropertyChangingEventArgs(nameof(IsStarted));
				PropertyChanging?.Invoke(this, e);
				_isStarted = value;
				if (_isStarted) {
					foreach (var item in Items)
					{
						if (item.IsChecked && item.Port != null && !item.Port.IsOpen)
						{
							try
							{
								item.Port.Open();
							}
							catch
							{

							}
						}
					}
					if(_threadControl==null)
					{
						_threadControl = new _ThreadControl(this);
					}
					_threadControl.Cancel = false;
					_updateThread = new Thread(new ParameterizedThreadStart(_CollectSystemInfoThread));
					_updateThread.Start(_threadControl);
				} else
				{
					_StopAll();
					
				}
				PropertyChangedEventArgs e2 = new PropertyChangedEventArgs(nameof(IsStarted));
				PropertyChanged?.Invoke(this, e2);
			}
		}
		public int CpuTMax
		{
			get
			{
				return _cpuTMax;
			}
			set
			{
				PropertyChangingEventArgs e = new PropertyChangingEventArgs(nameof(CpuTMax));
				PropertyChanging?.Invoke(this, e);
				if(value<1 || value>255)
				{
					value = 90;
				}
				Interlocked.Exchange(ref _cpuTMax, value);
				PropertyChangedEventArgs e2 = new PropertyChangedEventArgs(nameof(CpuTMax));
				PropertyChanged?.Invoke(this, e2);
			}
		}
		public int GpuTMax
		{
			get
			{
				return _gpuTMax;
			}
			set
			{

				PropertyChangingEventArgs e = new PropertyChangingEventArgs(nameof(GpuTMax));
				PropertyChanging?.Invoke(this, e);
				if (value < 1 || value > 255)
				{
					value = 80;
				}
				Interlocked.Exchange(ref _gpuTMax, value);
				PropertyChangedEventArgs e2 = new PropertyChangedEventArgs(nameof(GpuTMax));
				PropertyChanged?.Invoke(this, e2);
			}
		}
		public event PropertyChangedEventHandler PropertyChanged;
		public event PropertyChangingEventHandler PropertyChanging;
	}
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		MainWindowViewModel _ViewModel;
		System.Windows.Forms.NotifyIcon _notifyIcon;
		public MainWindow()
		{
			InitializeComponent();
			_ViewModel = new MainWindowViewModel();
			DataContext = _ViewModel;
			using (Stream stm = System.Reflection.Assembly.GetEntryAssembly().GetManifestResourceStream("EspMon.espmon.ico"))
			{
				if (stm != null)
				{
					_notifyIcon = new System.Windows.Forms.NotifyIcon();
					_notifyIcon.BalloonTipText = "The Esp Mon has been minimised. Click the tray icon to show.";
					_notifyIcon.BalloonTipTitle = "Esp Mon";
					_notifyIcon.Text = "Esp Mon";
					_notifyIcon.Click += new EventHandler(_notifyIcon_Click);
					_notifyIcon.Icon = new System.Drawing.Icon(stm);
					_notifyIcon.Visible = true;	
				}
			}
			

		}
		protected override void OnClosed(EventArgs e)
		{
			_ViewModel.Close();
			_notifyIcon.Dispose();
			_notifyIcon = null;
			base.OnClosed(e);
		}
		private void comPortsRefresh_Click(object sender, RoutedEventArgs e)
		{
			_ViewModel.Refresh();
		}


		private WindowState _storedWindowState = WindowState.Normal;
		protected override void OnStateChanged(EventArgs e)
		{
			if (WindowState == WindowState.Minimized)
			{
				Hide();
				if (_notifyIcon != null)
					_notifyIcon.ShowBalloonTip(2000);
			}
			else
				_storedWindowState = WindowState;
		}
		

		void _notifyIcon_Click(object sender, EventArgs e)
		{
			Show();
			WindowState = _storedWindowState;
		}
		
	}
}
