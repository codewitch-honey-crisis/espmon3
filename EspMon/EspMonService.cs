using OpenHardwareMonitor.Hardware;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;

namespace EspMon
{

	public partial class EspMonService : ServiceBase
	{
		// traverses OHWM data
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
		const int BaudRate = 115200;
		Thread _ohwmThread = null;
		MessagingSynchronizationContext _ohwmSyncContext = null;
		CancellationTokenSource _ohwmCancelSource;
		CancellationToken _ohwmCancel;
		Computer _computer = null;
		Timer _refreshTimer = null;
		volatile bool _started = false;
		OhwmUpdateVisitor _updateVisitor = null;
		static volatile int cpuTMax = 90;
		static volatile int gpuTMax = 80;
		static volatile int cpuUsage = 0;
		static volatile int cpuTemp = 0;
		static volatile int gpuUsage = 0;
		static volatile int gpuTemp = 0;
		ConcurrentDictionary<string,SerialPort> _regPorts = null;
		public EspMonService()
		{
			InitializeComponent();
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
		private static void OhwmThreadProc(object state)
		{
			EspMonService _this = (EspMonService)state;
			_this._started = true;
			try
			{
				_this._ohwmSyncContext.Start(_this._ohwmCancel);
			}
			catch(OperationCanceledException)
			{

			}
			_this._started = false;
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
		private static string[] GetRegPortNames()
		{
			var path = Assembly.GetExecutingAssembly().Location;
			path = Path.Combine(Path.GetDirectoryName(path), "EspMon.cfg");
			var regPortList = new List<string>();
			if (File.Exists(path))
			{
				try
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
				catch { return null; }
			}
			
			return regPortList.ToArray();
		}
		static bool ReadTMax(out int cpuTMax, out int gpuTMax)
		{
			cpuTMax = 90;
			gpuTMax = 80;
			var path = Assembly.GetExecutingAssembly().Location;
			path = Path.Combine(Path.GetDirectoryName(path), "EspMon.cfg");
			if (File.Exists(path))
			{
				try
				{
					using (var reader = new StreamReader(path))
					{
						string line = reader.ReadLine()?.Trim();
						if (line != null)
						{
							if (!int.TryParse(line, out cpuTMax))
							{
								cpuTMax = 90;
							}
						}
						line = reader.ReadLine()?.Trim();
						if (line != null)
						{
							if (!int.TryParse(line, out gpuTMax))
							{
								gpuTMax = 80;
							}
						}
					}
				}
				catch { return false; }
			}
			return true;
		}
		private static void UpdateTimerProc(object state)
		{
			EspMonService _this = (EspMonService)state;
			_this._ohwmSyncContext.Post(new SendOrPostCallback((object st) => {
				((EspMonService)st).FetchHardwareInfo();
			}), _this);
			int cpuTM, gpuTM;
			while(!ReadTMax(out cpuTM, out gpuTM))
			{
				Thread.Sleep(5);
			}
			EspMonService.cpuTMax = cpuTM;
			EspMonService.gpuTMax = gpuTM;
			var regNames = GetRegPortNames();
			while(regNames==null)
			{
				Thread.Sleep(10);
				regNames = GetRegPortNames();
			}
			
			for (int i = 0; i < regNames.Length; i++)
			{
				var regName = regNames[i];
				SerialPort p;
				if (!_this._regPorts.TryGetValue(regName, out p))
				{
					var port = new SerialPort(regName,BaudRate);
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
					_this._regPorts.TryAdd(regName, port);
				} else
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
				if(!regNames.Contains(kvp.Key,StringComparer.InvariantCultureIgnoreCase))
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
			regNames = null;
			
		}

		private static void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			try
			{
				int cpuTMax = EspMonService.cpuTMax;
				int gpuTMax = EspMonService.gpuTMax;
				int cpuUsage = EspMonService.cpuUsage;
				int cpuTemp = EspMonService.cpuTemp;
				int gpuUsage = EspMonService.gpuUsage;
				int gpuTemp = EspMonService.gpuTemp;

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

		protected override void OnStart(string[] args)
		{
			_ohwmThread.Start(this);
			// wait for the message pump to start
			do
			{
				Thread.Sleep(50);
			} while (!_started);
			_ohwmSyncContext.Send(new SendOrPostCallback((object state) => {
				_computer.Open();
			}),null);
			_refreshTimer = new Timer(new TimerCallback(UpdateTimerProc), this, 0, 100);
		}	

		protected override void OnStop()
		{
			_refreshTimer.Dispose();
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
			
			
		}
	}
}
