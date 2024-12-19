using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	partial class EspLink
	{
		string _portName;
		SerialPort _port;
		int _baudRate = 115200;
		ConcurrentQueue<byte> _serialIncoming = new ConcurrentQueue<byte>();
		Handshake _serialHandshake;
		public Handshake SerialHandshake
		{
			get => _serialHandshake;
			set { _serialHandshake = value;
				if (_port != null)
				{
					_port.Handshake = _serialHandshake;
				}
			
			}
		}
		SerialPort GetOrOpenPort()
		{
			if (_port == null)
			{
				_port = new SerialPort(_portName, 115200, Parity.None, 8, StopBits.One);
				_port.DataReceived += _port_DataReceived;
				_port.ErrorReceived += _port_ErrorReceived;
				
				
			}
			if (!_port.IsOpen)
			{
				try
				{
					_port.Open();
				}

				catch { return null; }
			}
			return _port;
		}

		private void _port_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine("Serial error: "+e.EventType.ToString());
		}

		int ReadByteNoBlock()
		{
			byte result;
			if(_serialIncoming.TryDequeue(out result))
			{
				return result;
			}
			return -1;
		}
		private async void _port_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			if(e.EventType==SerialData.Chars)
			{
				var port =(SerialPort)sender;
				if (port.BytesToRead > 0) {
					var ba = new byte[port.BytesToRead];
					var len = await port.BaseStream.ReadAsync(ba, 0, ba.Length);
					for (int i = 0; i < len; i++)
					{
						_serialIncoming.Enqueue(ba[i]);
					}
				}
			}
		}
		public async Task SetBaudRateAsync(int oldBaud, int newBaud, CancellationToken cancellationToken,int timeout = -1)
		{
			_baudRate = newBaud;
			if (Device == null || _inBootloader == false)
			{
				return;
			}
			// stub takes the new baud rate and the old one
			var secondArg = IsStub ? (uint)oldBaud : 0;
			var data = new byte[8];
			PackUInts(data, 0, new uint[] {(uint)newBaud,secondArg });
			await CommandAsync(cancellationToken, Device.ESP_CHANGE_BAUDRATE, data, 0, timeout);
			if(_port!=null&&_port.IsOpen)
			{
				_port.BaudRate = newBaud;
				Thread.Sleep(50); // ignore crap.
				_port.DiscardInBuffer();
			}
		}
		
		
		public int BaudRate
		{
			get
			{
				return _baudRate;
			}
			set
			{
				if(value!=_baudRate)
				{
					SetBaudRateAsync(_baudRate, value, CancellationToken.None,DefaultTimeout).Wait();			
				}
			}
		}
		public void Close()
		{
			if (_port != null)
			{
				if (_port.IsOpen)
				{
					try
					{
						_port.Close();
					}
					catch { }
				}
				_port = null;
			}
			Cleanup();
		}
		private static int GetComPortNum(string portName)
		{
			if (!string.IsNullOrEmpty(portName))
			{
				if (portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
				{
					int result;
					if (int.TryParse(portName.Substring(4), System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture.NumberFormat, out result))
					{
						return result;
					}
				}
			}
			return 0;
		}
		public static List<(string Name, string Id, string LongName, string Vid, string Pid, string Description)> GetComPorts()
		{
			var result = new List<(string Name, string Id, string LongName, string Vid, string Pid, string Description)>();
			ManagementClass pnpCls = new ManagementClass("Win32_PnPEntity");
			ManagementObjectCollection pnpCol = pnpCls.GetInstances();

			foreach (var pnpObj in pnpCol)
			{
				var clsid = pnpObj["classGuid"];

				if (clsid != null && ((string)clsid).Equals("{4d36e978-e325-11ce-bfc1-08002be10318}", StringComparison.OrdinalIgnoreCase))
				{
					string deviceId = pnpObj["deviceid"].ToString();

					int vidIndex = deviceId.IndexOf("VID_");
					string vid = null;
					if (vidIndex > -1)
					{
						string startingAtVid = deviceId.Substring(vidIndex);
						vid = startingAtVid.Substring(0, 8); // vid is four characters long

					}
					string pid = null;
					int pidIndex = deviceId.IndexOf("PID_");
					if (pidIndex > -1)
					{
						string startingAtPid = deviceId.Substring(pidIndex);
						pid = startingAtPid.Substring(0, 8); // pid is four characters long
					}

					var idProp = pnpObj["deviceId"];
					var nameProp = pnpObj["name"];
					var descProp = pnpObj["description"];
					var name = nameProp.ToString();
					var idx = name.IndexOf('(');
					if (idx > -1)
					{
						var lidx = name.IndexOf(')', idx + 2);
						if (lidx > -1)
						{
							name = name.Substring(idx + 1, lidx - idx - 1);
						}
					}
					result.Add((Name: name, Id: idProp.ToString(), LongName: nameProp?.ToString(), Vid: vid, Pid: pid, Description: descProp?.ToString()));

				}
			}
			result.Sort((x, y) => {
				var xn = GetComPortNum(x.Name);
				var yn = GetComPortNum(y.Name);
				var cmp = xn.CompareTo(yn);
				if(cmp==0)
				{
					cmp = String.Compare(x.Name, y.Name, StringComparison.Ordinal);
				}
				return cmp;
			});
			return result;
		}
	}
}
