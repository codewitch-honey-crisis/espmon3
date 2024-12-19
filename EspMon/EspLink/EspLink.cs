using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	public partial class EspLink
	{
		static readonly Regex _bootloaderRegex = new Regex(@"boot:0x([0-9a-fA-F]+)(.*waiting for download)?", RegexOptions.CultureInvariant | RegexOptions.Compiled);
		bool _inBootloader = false;
		public EspLink(string portName)
		{
			_portName = portName;
		}
		void CheckReady(bool checkConnected =true)
		{
			if(checkConnected)
			{
				if(Device==null)
				{
					throw new InvalidOperationException("The device is not connected");
				}
			}
			if (!_inBootloader)
			{
				throw new InvalidOperationException("The bootloader is not entered");
			}
		}
		public int DefaultTimeout { get; set; } = 3000;
		async Task SyncAsync(int timeout, CancellationToken cancellationToken,IProgress<int> progress , int prog)
		{
			var data = new byte[] { 0x07, 0x07, 0x12, 0x20, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55 };
			var cmdRet = await CommandAsync(cancellationToken,0x08,  data, 0, timeout);
			progress?.Report(prog++);
			int stubDetected = cmdRet.Value == 0 ? 1 : 0;
			Exception lastEx = null;
			for (var i = 0; i < 7; ++i)
			{
				try
				{
					cmdRet = await CommandAsync(cancellationToken, -1, null, 0, timeout);
					progress?.Report(prog++);
					stubDetected &= cmdRet.Value == 0 ? 1 : 0;
				}
				catch (TimeoutException ex)
				{
					lastEx = ex;
				}
			}
			if(lastEx != null)
			{
				throw lastEx;
			}

		}
		async Task ConnectAttemptAsync(ResetStrategy resetStrategy, int extraDelay , CancellationToken cancellationToken, IProgress<int> progress, int prog)
		{
			var bootLogDetected = false;
			var downloadMode = false;
			ushort bootMode = 0;
			var port = GetOrOpenPort();
			if (resetStrategy != null)
			{
				port.DiscardInBuffer();
				progress?.Report(prog++);
				if (resetStrategy != null)
				{
					resetStrategy(port);
					progress?.Report(prog++);
				}
				if (extraDelay > 0)
				{
					Thread.Sleep(extraDelay);
					progress?.Report(prog++);
				}
				var str = port.ReadExisting();
				
				var match = _bootloaderRegex.Match(str);
				if (match.Success && ushort.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture.NumberFormat, out bootMode))
				{
					bootLogDetected = true;
					
					if (match.Groups.Count > 2)
					{
						downloadMode = true;
					}
				}
				Exception ex = null;
				for (var i = 0; i < 5; ++i)
				{
					progress?.Report(prog++);
					try
					{
						port.DiscardInBuffer();
						port.BaseStream.Flush();
						await SyncAsync(1000,cancellationToken, progress,prog);
						return;
					}
					catch (Exception e)
					{
						ex = e;
					}
				}
				if (ex != null) { throw ex; }
				if (bootLogDetected)
				{
					if (downloadMode)
					{
						throw new IOException("Download mode detected, but getting no sync reply");
					}
					throw new IOException("Wrong boot mode detected. MCU must be in download mode");
				}

			}
		}
		public static (string Name, string Id, string LongName, string Vid, string Pid, string Description) FindComPort(string name)
		{
			foreach(var port in GetComPorts())
			{
				if(port.Name.Equals(name,StringComparison.OrdinalIgnoreCase))
				{
					return port;
				}
			}
			throw new ArgumentException("The COM port was not found", nameof(name));
		}

		public void Connect(bool reset=true, int attempts=3, bool sync = true,int timeout=-1, IProgress<int> progress = null)
		{
			ConnectAsync(reset, attempts, sync,CancellationToken.None,timeout, progress).Wait();
		}
		public async Task ConnectAsync(bool reset, int attempts , bool sync , CancellationToken cancellationToken, int timeout = -1,IProgress<int> progress=null )
		{ 
			int prog = int.MinValue;
			progress?.Report(prog++);
			Exception lastErr = null;
			var connected = false;
			for (var i = 0; i < attempts; ++i)
			{
				try
				{
					progress?.Report(prog++);
					if (0 == (i & 1))
					{
						await ConnectAttemptAsync(reset?ClassicResetStrategy:null, 500,cancellationToken, progress,prog);
						connected = true;
						progress?.Report(prog++);
						break;
					}
					else
					{
						await ConnectAttemptAsync(reset?ClassicResetStrategy:null, 7000,cancellationToken, progress,prog);
						progress?.Report(prog++);
						connected = true;
						break;
					}


				}
				catch (TimeoutException ex)
				{
					lastErr = ex;
				}
			}
			if (!connected)
			{
				throw lastErr;
			}
	
			var magic = await ReadRegAsync( 0x40001000, cancellationToken, 3000);
			CreateDevice(magic);
			_inBootloader = true;
			if (_baudRate!=115200)
			{
				await SetBaudRateAsync( 115200, _baudRate, cancellationToken,timeout);
			}
		}

	}
}
