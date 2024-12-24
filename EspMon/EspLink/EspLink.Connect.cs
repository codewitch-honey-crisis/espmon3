using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EL
{
	/// <summary>
	/// The connection mode
	/// </summary>
	public enum EspConnectMode
	{
		/// <summary>
		/// Standard attempt to reset into the bootloader and sync
		/// </summary>
		Default = 0,
		/// <summary>
		/// Do not reset first (assumes already in bootloader)
		/// </summary>
		NoReset = 1,
		/// <summary>
		/// Do not sync
		/// </summary>
		NoSync = 2,
		/// <summary>
		/// Do not reset or sync
		/// </summary>
		NoResetNoSync = 3,
		/// <summary>
		/// Use USB reset technique
		/// </summary>
		UsbReset=4
	}
	partial class EspLink
	{
		static readonly byte[] _syncPacket = new byte[] { 0x07, 0x07, 0x12, 0x20, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55, 0x55 };
		static readonly Regex _bootloaderRegex = new Regex(@"boot:0x([0-9a-fA-F]+)(.*waiting for download)?", RegexOptions.CultureInvariant | RegexOptions.Compiled);
		bool _inBootloader = false;
		void CheckReady(bool checkConnected = true)
		{
			if (checkConnected)
			{
				if (Device == null)
				{
					throw new InvalidOperationException("The device is not connected");
				}
			}
			if (!_inBootloader)
			{
				throw new InvalidOperationException("The bootloader is not entered");
			}
		}

		async Task SyncAsync(int timeout, CancellationToken cancellationToken, IProgress<int> progress, int prog)
		{
			var cmdRet = await CommandAsync(cancellationToken, 0x08, _syncPacket, 0, timeout);
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
			if (lastEx != null)
			{
				throw lastEx;
			}
		}
		struct StrategyEntry
		{
			public readonly ResetStrategy ResetStrategy;
			public readonly int Delay;
			public StrategyEntry(ResetStrategy resetStrategy, int delay = 0)
			{
				ResetStrategy = resetStrategy;
				Delay = delay;
			}
		}
		/// <summary>
		/// True if the port is a USB serial JTAG connection, otherwise false
		/// </summary>
		public bool IsUsbSerialJtag
		{
			get
			{
				return FindComPort(_portName).Pid.Equals("PID_1001",StringComparison.OrdinalIgnoreCase);
			}
		}
		StrategyEntry[] BuildConnectStrategy(EspConnectMode connectMode,int defaultResetDelay=50,int extraDelay=550)
		{
			// Serial JTAG USB
			if(connectMode==EspConnectMode.UsbReset || IsUsbSerialJtag)
			{
				return new StrategyEntry[] { new StrategyEntry(SerialJtagResetStrategy) };
			}
			var iswin = (Environment.OSVersion.Platform == PlatformID.Win32NT || Environment.OSVersion.Platform == PlatformID.Win32Windows || Environment.OSVersion.Platform == PlatformID.Win32S);
			// USB-to-Serial bridge
			if (!iswin && !_portName.StartsWith("rfc2217:",StringComparison.Ordinal))
			{
				/*return (
					UnixTightReset(self._port, delay),
					UnixTightReset(self._port, extra_delay),
					ClassicReset(self._port, delay),
					ClassicReset(self._port, extra_delay),

				)*/
				throw new NotImplementedException("Unix reset is not implemented");
			}
			return new StrategyEntry[] { new StrategyEntry(ClassicResetStrategy, defaultResetDelay), new StrategyEntry(ClassicResetStrategy, extraDelay) };

		}
		async Task ConnectAttemptAsync(StrategyEntry strategy, EspConnectMode mode, CancellationToken cancellationToken, IProgress<int> progress, int prog, int timeout=-1)
		{
			if (mode == EspConnectMode.NoResetNoSync)
			{
				return;
			}

			var bootLogDetected = false;
			var downloadMode = false;
			ushort bootMode = 0;
			var port = GetOrOpenPort();
			progress?.Report(prog++);

			if (mode != EspConnectMode.NoReset)
			{
				port.DiscardInBuffer();
				strategy.ResetStrategy?.Invoke(port);
				progress?.Report(prog++);

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
			}
			Exception ex = null;
			for (var i = 0; i < 5; ++i)
			{
				progress?.Report(prog++);
				try
				{
					port.DiscardInBuffer();
					await port.BaseStream.FlushAsync();
					await SyncAsync(timeout, cancellationToken, progress, prog);
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
		/// <summary>
		/// Connects to an Espressif MCU device
		/// </summary>
		/// <param name="mode">The <see cref="EspConnectMode"/> to use</param>
		/// <param name="attempts">The number of attempts to make</param>
		/// <param name="detecting">True if only detecting, and no actual connection should be made</param>
		/// <param name="timeout">The timeout for each suboperation (not the total)</param>
		/// <param name="progress">A <see cref="IProgress{Int32}"/> implementation to report progress back</param>
		public void Connect(EspConnectMode mode=EspConnectMode.Default, int attempts=3, bool detecting = false, int timeout = -1, IProgress<int> progress = null)
		{
			ConnectAsync(mode, attempts, detecting ,CancellationToken.None, timeout, progress).Wait();
		}
		/// <summary>
		/// Asynchronously connects to an Espressif MCU device
		/// </summary>
		/// <param name="mode">The <see cref="EspConnectMode"/> to use</param>
		/// <param name="attempts">The number of attempts to make</param>
		/// <param name="detecting">True if only detecting, and no actual connection should be made</param>
		/// <param name="cancellationToken">The <see cref="CancellationToken"/> to use which allows for cancelling the operation</param>
		/// <param name="timeout">The timeout for each suboperation (not the total)</param>
		/// <param name="progress">A <see cref="IProgress{Int32}"/> implementation to report progress back</param>
		public async Task ConnectAsync(EspConnectMode mode, int attempts, bool detecting, CancellationToken cancellationToken, int timeout = -1, IProgress<int> progress = null)
		{
			var strategy = BuildConnectStrategy(mode);
			int strategyIndex = 0;
			if(attempts<strategy.Length)
			{
				attempts = strategy.Length;
			}
			int prog = int.MinValue;
			progress?.Report(prog++);
			Exception lastErr = null;
			var connected = false;
			for (var i = 0; i < attempts; ++i)
			{
				try
				{
					progress?.Report(prog++);
					await ConnectAttemptAsync(strategy[strategyIndex], mode, cancellationToken, progress, prog, timeout);
					++strategyIndex;
					if(strategyIndex == strategy.Length)
					{
						strategyIndex = 0;
					}
					connected = true;
					break;
				}
				catch (Exception ex)
				{
					lastErr = ex;
				}
			}
			if (!connected)
			{
				throw lastErr;
			}
			GetOrOpenPort().DiscardInBuffer();
			if (!detecting)
			{
				var magic = await ReadRegAsync(0x40001000, cancellationToken, timeout);
				CreateDevice(magic);
				_inBootloader = true;
				await Device.ConnectAsync(cancellationToken, DefaultTimeout);
				if (_baudRate != 115200)
				{
					await SetBaudRateAsync(_baudRate, cancellationToken, timeout);
				}
			}
		}
	}
}
