using System.IO;
using System.IO.Ports;
using System.Threading;

namespace EL
{
	partial class EspLink
	{
		/// <summary>
		/// A reset strategy
		/// </summary>
		/// <param name="port">The target serial port</param>
		/// <returns>True if the reset was successful, otherwise false</returns>
		public delegate bool ResetStrategy(SerialPort port);
		/// <summary>
		/// Do not reset
		/// </summary>
		public static readonly ResetStrategy NoResetStrategy = new ResetStrategy(NoResetImpl);
		/// <summary>
		/// Hard reset the device (doesn't enter bootloader/will exit bootloader)
		/// </summary>
		public static readonly ResetStrategy HardResetStrategy = new ResetStrategy(HardResetImpl);
		/// <summary>
		/// Hard reset the device (USB)
		/// </summary>
		public static readonly ResetStrategy HardResetUsbStrategy = new ResetStrategy(HardResetUsbImpl);
		/// <summary>
		/// Reset the device using Dtr/Rts to force the MCU into bootloader mode
		/// </summary>
		public static readonly ResetStrategy ClassicResetStrategy = new ResetStrategy(ClassicResetImpl);
		/// <summary>
		/// Reset the device using Dtr/Rts to force the MCU into bootloader mode (USB Serial JTAG)
		/// </summary>
		public static readonly ResetStrategy SerialJtagResetStrategy = new ResetStrategy(SerialJtagResetImpl);
		static bool SerialJtagResetImpl(SerialPort port)
		{
			if (port == null || !port.IsOpen) { return false; }
			port.RtsEnable = false;
			port.DtrEnable = port.DtrEnable;
			port.DtrEnable = false;
			Thread.Sleep(100);
			port.DtrEnable = true;
			port.RtsEnable = false;
			port.DtrEnable = port.DtrEnable;
			Thread.Sleep(100);
			port.RtsEnable = true;
			port.DtrEnable = port.DtrEnable;
			port.DtrEnable = false;
			port.RtsEnable = true;
			port.DtrEnable = port.DtrEnable;
			Thread.Sleep(100);
			port.DtrEnable = false;
			port.RtsEnable = false;
			port.DtrEnable = port.DtrEnable;

			return true;
		}
		static bool HardResetImplInt(SerialPort port, bool isUsb)
		{
			if (port == null || !port.IsOpen) { return false; }
			port.RtsEnable = true;
			port.DtrEnable = port.DtrEnable;
			if (isUsb)
			{
				Thread.Sleep(200);
				port.RtsEnable = false;
				port.DtrEnable = port.DtrEnable;
				Thread.Sleep(200);
			}
			else
			{
				Thread.Sleep(100);
				port.RtsEnable = false;
				port.DtrEnable = port.DtrEnable;

			}

			return true;
		}
		static bool NoResetImpl(SerialPort port)
		{
			return true;
		}
		static bool HardResetImpl(SerialPort port)
		{
			return HardResetImplInt(port, false);
		}
		static bool HardResetUsbImpl(SerialPort port)
		{
			return HardResetImplInt(port, true);
		}
		static bool ClassicResetImpl(SerialPort port)
		{
			if (port == null || !port.IsOpen) { return false; }
			port.DtrEnable = false;
			port.RtsEnable = true;
			port.DtrEnable = port.DtrEnable;
			Thread.Sleep(50);
			port.DtrEnable = true;
			port.RtsEnable = false;
			port.DtrEnable = port.DtrEnable;
			Thread.Sleep(550);
			port.DtrEnable = false;
			return true;
		}
		/// <summary>
		/// Terminates any connection and reset the device.
		/// </summary>
		/// <param name="strategy">The reset strategy to use, or null to hard reset</param>
		/// <exception cref="IOException">Unable to communicate with the device</exception>
		public void Reset(ResetStrategy strategy = null)
		{
			Close();
			try
			{
				if (strategy == null)
				{
					strategy = HardResetStrategy;
				}
				SerialPort port = GetOrOpenPort();
				if (port != null && port.IsOpen)
				{
					port.Handshake = Handshake.None;
					port.DiscardInBuffer();

					// On targets with USB modes, the reset process can cause the port to
					// disconnect / reconnect during reset.
					// This will retry reconnections on ports that
					// drop out during the reset sequence.
					for (var i = 2; i >= 0; --i)
					{
						{
							var b = strategy?.Invoke(port);
							if (b.HasValue && b.Value)
							{
								return;
							}
						}
					}
					throw new IOException("Unable to reset device");
				}
			}
			finally
			{
				Close();
			}
		}



	}
}
