using OpenHardwareMonitor.Hardware;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace EspMon
{
	internal class RefreshPortsEventArgs
	{
		public RefreshPortsEventArgs() { }
		public List<string> PortNames { get; } = new List<string>();
	}
	internal delegate void RefreshPortsEventHandler(object sender, RefreshPortsEventArgs e);

	internal class PortDispatcher : IDisposable
	{
		public const int BaudRate = 115200;
		internal class OhwmUpdateVisitor : IVisitor
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
		public static PortDispatcher Instance { get; private set; }
		public event RefreshPortsEventHandler RefreshPortsRequested;
		Thread _ohwmThread = null;
		MessagingSynchronizationContext _ohwmSyncContext = null;
		CancellationTokenSource _ohwmCancelSource;
		CancellationToken _ohwmCancel;
		Computer _computer = null;
		Timer _refreshTimer = null;
		OhwmUpdateVisitor _updateVisitor = null;
		volatile bool _started = false;
		static volatile int cpuTMax = 90;
		static volatile int gpuTMax = 80;
		static volatile int cpuUsage = 0;
		static volatile int cpuTemp = 0;
		static volatile int gpuUsage = 0;
		static volatile int gpuTemp = 0;
		ConcurrentDictionary<string, SerialPort> _regPorts = null;
		private bool _disposed;

		private static void OhwmThreadProc(object state)
		{
			PortDispatcher _this = (PortDispatcher)state;
			_this._started = true;
			try
			{
				_this._ohwmSyncContext.Start(_this._ohwmCancel);
			}
			catch (OperationCanceledException)
			{

			}
			_this._started = false;
		}
		public PortDispatcher()
		{
			if(Instance!=null)
			{
				throw new InvalidOperationException("A port dispatcher already exists");
			}
			Instance = this;
			_ohwmCancelSource = new CancellationTokenSource();
			_ohwmCancel = _ohwmCancelSource.Token;
			_ohwmThread = new Thread(new ParameterizedThreadStart(OhwmThreadProc));
			_ohwmSyncContext = new MessagingSynchronizationContext();
			_computer = new Computer();
			_computer.CPUEnabled = true;
			_computer.GPUEnabled = true;
			_updateVisitor = new OhwmUpdateVisitor();
			_started = false;
			_regPorts = new ConcurrentDictionary<string, SerialPort>(StringComparer.InvariantCultureIgnoreCase);
		}
		public int CpuTMax
		{
			get { return cpuTMax; }
			set
			{
				if (value < 0 || value > 255) { value = 90; }
				cpuTMax = value;
			}
		}
		public int GpuTMax {
			get { return gpuTMax; }
			set
			{
				if(value<0||value>255) { value = 80; }
				gpuTMax = value;
			}
		}
		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				_disposed = true;
			}
			Instance = null;
		}
		private static void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			try
			{
				int cpuTMax = PortDispatcher.cpuTMax;
				int gpuTMax = PortDispatcher.gpuTMax;
				int cpuUsage = PortDispatcher.cpuUsage;
				int cpuTemp = PortDispatcher.cpuTemp;
				int gpuUsage = PortDispatcher.gpuUsage;
				int gpuTemp = PortDispatcher.gpuTemp;

				SerialPort _port = (SerialPort)sender;
				int i = _port.ReadByte();
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
				}
				else
				{
					_port.ReadExisting();
				}
			}
			catch
			{

			}
		}
		private void FetchHardwareInfo()
		{
			if (_computer == null)
			{
				// TODO: Event log
				return;
			}
			// use OpenHardwareMonitorLib to collect the system info
			_computer.Accept(_updateVisitor);
			for (int i = 0; i < _computer.Hardware.Length; i++)
			{
				if (_computer.Hardware[i].HardwareType == HardwareType.CPU)
				{
					for (int j = 0; j < _computer.Hardware[i].Sensors.Length; j++)
					{
						var sensor = _computer.Hardware[i].Sensors[j];
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
				if (_computer.Hardware[i].HardwareType == HardwareType.GpuAti ||
					_computer.Hardware[i].HardwareType == HardwareType.GpuNvidia)
				{
					for (int j = 0; j < _computer.Hardware[i].Sensors.Length; j++)
					{
						var sensor = _computer.Hardware[i].Sensors[j];
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
				if (gpuTMax < 1 || gpuTMax > 255)
				{
					gpuTMax = 80;
				}
			}
		}
		~PortDispatcher()
		{
		     Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public void Start()
		{
			if(_started) return;
			_ohwmThread.Start(this);
			// wait for the message pump to start
			do
			{
				Thread.Sleep(50);
			} while (!_started);
			_ohwmSyncContext.Send(new SendOrPostCallback((object state) => {
				_computer.Open();
			}), null);
			_refreshTimer = new Timer(new TimerCallback(UpdateTimerProc), this, 0, 100);
		}
		private static void UpdateTimerProc(object state)
		{
			PortDispatcher _this = (PortDispatcher)state;
			_this._ohwmSyncContext.Post(new SendOrPostCallback((object st) => {
				((PortDispatcher)st).FetchHardwareInfo();
			}), _this);

			var args = new RefreshPortsEventArgs();
			_this.RefreshPortsRequested?.Invoke(_this, args);
			var portNames = args.PortNames.ToArray();

			for (int i = 0; i < portNames.Length; i++)
			{
				var portName = portNames[i];
				SerialPort p;
				if (!_this._regPorts.TryGetValue(portName, out p))
				{
					var port = new SerialPort(portName, BaudRate);
					try
					{
						port.Open();
						port.DataReceived += Port_DataReceived;
					}
					catch
					{
						// Don't log here. We'll keep retrying later
					}
					// shouldn't fail
					_this._regPorts.TryAdd(portName, port);
				}
				else
				{
					if (!p.IsOpen)
					{
						try
						{
							p.Open();
							p.DataReceived += Port_DataReceived;
						}
						catch
						{
						}
					}
				}
			}
			var toRemove = new List<string>(_this._regPorts.Count);
			// now clean out the ports that are no longer in the registry
			foreach (var kvp in _this._regPorts)
			{
				if (!portNames.Contains(kvp.Key, StringComparer.InvariantCultureIgnoreCase))
				{
					if (kvp.Value.IsOpen)
					{
						try
						{
							kvp.Value.Close();
						}
						catch { }
						kvp.Value.DataReceived -= Port_DataReceived;
						toRemove.Add(kvp.Key);
					}
				}
			}
			foreach (var key in toRemove)
			{
				SerialPort p;
				_this._regPorts.TryRemove(key, out p);
			}
			toRemove.Clear();
			toRemove = null;
			portNames = null;
		}
		public void Stop()
		{
			if(!_started)
			{
				return;
			}
			_refreshTimer?.Dispose();
			_refreshTimer = null;
			foreach (var kvp in _regPorts)
			{
				try
				{
					kvp.Value.Close();
				}
				catch
				{

				}
				kvp.Value.DataReceived -= Port_DataReceived;
			}
			_regPorts.Clear();
			_ohwmCancelSource.Cancel();
			_ohwmThread.Join();
			_ohwmCancelSource.Dispose();
			_ohwmCancelSource = new CancellationTokenSource();
			_ohwmCancel = _ohwmCancelSource.Token;
			_ohwmThread = new Thread(new ParameterizedThreadStart(OhwmThreadProc));

		}
		public bool IsStarted
		{
			get
			{
				return _started;
			}
		}
	}

	
}
