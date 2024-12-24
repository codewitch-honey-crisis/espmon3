using System;
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
		//ConcurrentQueue<byte> _serialIncoming = new ConcurrentQueue<byte>();
		readonly object _lock = new object();
		Queue<byte> _serialIncoming = new Queue<byte>();	
		Handshake _serialHandshake;
		/// <summary>
		/// The serial handshake protocol(s) to use
		/// </summary>
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
				_port.ReceivedBytesThreshold = 1;
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

		void _port_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine("Serial error: "+e.EventType.ToString());
		}

		int ReadByteNoBlock()
		{
			lock(_lock)
			{
				if(_serialIncoming.Count>0)
				{
					return _serialIncoming.Dequeue();
				}
			}
			return -1;
		}
		void _port_DataReceived(object sender, SerialDataReceivedEventArgs e)
		{
			if(e.EventType==SerialData.Chars)
			{
				int len = _port.BytesToRead;
				int i = -1;
				lock (_lock)
				{
					try
					{
						while (len-- > 0)
						{
							i = _port.ReadByte();
							if (i < 0) break;
							_serialIncoming.Enqueue((byte)i);
						}
					}
					catch
					{

					}
				}
			}
		}
		/// <summary>
		/// Asynchronously changes the baud rate
		/// </summary>
		/// <param name="newBaud">The new baud rate</param>
		/// <param name="cancellationToken">A <see cref="CancellationToken"/> that can be used to cancel the operation</param>
		/// <param name="timeout">The timeout in milliseconds</param>
		/// <returns>A waitable <see cref="Task"/></returns>
		public async Task SetBaudRateAsync(int newBaud, CancellationToken cancellationToken,int timeout = -1)
		{
			int oldBaud = _baudRate;
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
		/// <summary>
		/// Gets or sets the baud rate
		/// </summary>
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
					SetBaudRateAsync(value, CancellationToken.None,DefaultTimeout).Wait();			
				}
			}
		}
		/// <summary>
		/// Closes the link
		/// </summary>
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
		/// <summary>
		/// Finds a COM port with a particular name
		/// </summary>
		/// <param name="name">The port name</param>
		/// <returns>A tuple indicating the name, id, long name, VID, PID, and description of the port</returns>
		/// <exception cref="ArgumentException">The port was not found</exception>
		public static (string Name, string Id, string LongName, string Vid, string Pid, string Description) FindComPort(string name)
		{
			foreach (var port in GetComPorts())
			{
				if (port.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					return port;
				}
			}
			throw new ArgumentException("The COM port was not found", nameof(name));
		}
		private static int GetComPortNum(string portName)
		{
			if (!string.IsNullOrEmpty(portName))
			{
				if (portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
				{
					int result;
					if (int.TryParse(portName.Substring(3), System.Globalization.NumberStyles.Number, CultureInfo.InvariantCulture.NumberFormat, out result))
					{
						return result;
					}
				}
			}
			return 0;
		}
		/// <summary>
		/// Retrieves a list of the COM ports
		/// </summary>
		/// <returns>A read-only list of tuples indicating the name, id, long name, VID, PID, and description of the port</returns>
		public static IReadOnlyList<(string Name, string Id, string LongName, string Vid, string Pid, string Description)> GetComPorts()
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
