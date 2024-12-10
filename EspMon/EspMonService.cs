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
		PortDispatcher _dispatcher;
		public EspMonService()
		{
			InitializeComponent();
			_dispatcher = new PortDispatcher();
			_dispatcher.RefreshPortsRequested += _dispatcher_RefreshPortsRequested;
		}
		protected static void GetRegPortNames(List<string> ports)
		{
			var path = Assembly.GetExecutingAssembly().Location;
			path = Path.Combine(Path.GetDirectoryName(path), "EspMon.cfg");
			if (File.Exists(path))
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
								ports.Add(line);
							}
						}
					}
				}
			}
		}

		private void _dispatcher_RefreshPortsRequested(object sender, RefreshPortsEventArgs e)
		{
			GetRegPortNames(e.PortNames);
		}
		
		protected override void OnStart(string[] args)
		{
			_dispatcher.Start();
		}	
	
		protected override void OnStop()
		{
			_dispatcher.Stop();
			
		}
	}
}
