using OpenHardwareMonitor.Hardware;

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EspMon
{
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
	internal class HostedController : Controller
	{
		volatile int _cpuTMax = 90;
		volatile int _gpuTMax = 80;
		volatile int _cpuUsage = 0;
		volatile int _cpuTemp = 0;
		volatile int _gpuUsage = 0;
		volatile int _gpuTemp = 0;
		volatile bool _isStarted = false;
		Thread _updateThread = null;
		protected override int GetCpuTMax()
		{
			return _cpuTMax;
		}
		protected override void SetCpuTMax(int value)
		{
			if(value<0||value>255)
			{
				value = 90;
			}
			_cpuTMax = value;
		}
		protected override int GetGpuTMax()
		{
			return _gpuTMax;
		}
		protected override void SetGpuTMax(int value)
		{
			if (value < 0 || value > 255)
			{
				value = 80;
			}
			_gpuTMax = value;
		}
		protected override void StopAll()
		{
			foreach (var item in PortItems)
			{
				if (item.Port != null && item.Port.IsOpen)
				{
					try
					{
						item.Port.Close();
					}
					catch { }
				}
			}
			_isStarted = false;
			if (_updateThread != null)
			{
				_updateThread.Join();
				_updateThread = null;
			}
		}
		void _CollectSystemInfoThread(object state)
		{
			var _this = (HostedController)state;
			int cpuTMax = 90;
			int gpuTMax = 80;
			int cpuUsage = 0;
			int cpuTemp = 0;
			int gpuUsage = 0;
			int gpuTemp = 0;

			cpuTMax = _this._cpuTMax;
			gpuTMax = _this._gpuTMax;
			cpuUsage = _this._cpuUsage;
			cpuTemp = _this._cpuTemp;
			gpuUsage = _this._gpuUsage;
			gpuTemp = _this._gpuTemp;
			Computer computer = new Computer() { CPUEnabled = true, GPUEnabled = true };
			computer.Open();
			// use OpenHardwareMonitorLib to collect the system info
			var updateVisitor = new OhwmUpdateVisitor();
			_isStarted = true;
			while (_isStarted)
			{
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
				_this._cpuTMax = cpuTMax;
				_this._gpuTMax = gpuTMax;
				_this._cpuUsage = cpuUsage;
				_this._cpuTemp = cpuTemp;
				_this._gpuUsage = gpuUsage;
				_this._gpuTemp = gpuTemp;
				Thread.Sleep(100);
			}
			computer.Close();
			computer = null;

		}
		protected override void Start()
		{
			if (_isStarted)
			{
				return;
			}
			_updateThread = new Thread(new ParameterizedThreadStart(_CollectSystemInfoThread));
			_updateThread.Start(this);
			do
			{
				Thread.Sleep(50);
			} while (!_isStarted);
			foreach (var item in PortItems)
			{
				if(item.Port==null)
				{
					item.Port = new SerialPort(item.Name, Baud);
					item.Port.DataReceived += Port_DataReceived;
				}
				if (item.IsChecked)
				{
					if (!item.Port.IsOpen)
					{
						try
						{
							item.Port.Open();
						}
						catch
						{
						}
					}
				} else
				{
					if (item.Port.IsOpen)
					{
						try
						{
							item.Port.Close();
						}
						catch { }
					}
				}
			}
		}
		protected override bool GetIsStarted()
		{
			return _isStarted;
		}
		protected override void OnRefresh()
		{
			foreach (var item in PortItems)
			{
				item.PropertyChanged += Item_PropertyChanged;
				if (item.Port == null)
				{
					item.Port = new SerialPort(item.Name, Baud);
					item.Port.DataReceived += Port_DataReceived;
				}
				if (_isStarted)
				{
					if (item.IsChecked)
					{
						if (!item.Port.IsOpen)
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
					else
					{
						if (item.Port.IsOpen)
						{
							try
							{
								item.Port.Close();
							}
							catch { }
						}
					}
				}
				else
				{
					if (item.Port!=null && item.Port.IsOpen)
					{
						try
						{
							item.Port.Close();
						}
						catch { }
					}
				}
			}
		}

		private void Item_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "IsChecked")
			{
				PortItem portItem = (PortItem)sender;
				if (portItem.Port == null)
				{
					portItem.Port = new SerialPort(portItem.Name, Baud);
				}
				if (portItem.IsChecked && _isStarted)
				{
					if (!portItem.Port.IsOpen)
					{
						try { portItem.Port.Open(); } catch { }
					}
				}
				else
				{
					if (portItem.Port.IsOpen)
					{
						try { portItem.Port.Close(); } catch { }
					}
				}
			}
		}

		private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			
			var cpuTMax = _cpuTMax;
			var gpuTMax = _gpuTMax;
			var cpuUsage = _cpuUsage;
			var cpuTemp = _cpuTemp;
			var gpuUsage = _gpuUsage;
			var gpuTemp = _gpuTemp;
			var port = (SerialPort)sender;
			var i = port.ReadByte();
			if (i == 1)
			{
				var ba = new byte[7];
				ba[0] = 1;
				var v = ((double)cpuUsage / (double)100); ;
				ba[1] = (byte)cpuTMax;
				ba[2] = (byte)gpuTMax;
				ba[3] = (byte)(v * 255);
				v = ((double)cpuTemp / (double)cpuTMax); ;
				ba[4] = (byte)(v * 255);
				v = ((double)gpuUsage / (double)100); ;
				ba[5] = (byte)(v * 255);
				v = ((double)gpuTemp / (double)gpuTMax); ;
				ba[6] = (byte)(v * 255);
				port.Write(ba, 0, ba.Length);
				port.BaseStream.Flush();
			}
			else
			{
				port.ReadExisting();
			}
		}
	}
}