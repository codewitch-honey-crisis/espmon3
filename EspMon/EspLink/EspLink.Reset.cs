using System.IO;
using System.IO.Ports;
using System.Threading;

namespace EL
{
	partial class EspLink
	{
	
		public delegate bool ResetStrategy(SerialPort port);
		public static readonly ResetStrategy NoResetStrategy = new ResetStrategy(NoResetImpl);
		public static readonly ResetStrategy HardResetStrategy = new ResetStrategy(HardResetImpl);
		public static readonly ResetStrategy HardResetUsbStrategy = new ResetStrategy(HardResetUsbImpl);
		public static readonly ResetStrategy ClassicResetStrategy = new ResetStrategy(ClassicResetImpl);
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
