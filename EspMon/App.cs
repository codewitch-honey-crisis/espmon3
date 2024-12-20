using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EspMon
{
	public class App : System.Windows.Application
	{
		public static App Instance { get; private set; }
		public string[] UpdateArgs { get; private set; }
		[STAThread]
		static void Main(string[] args)
		{
			if (Environment.UserInteractive)
			{
				
				var appName = Assembly.GetEntryAssembly().GetName().Name;
				var notAlreadyRunning = true;
				using (var mutex = new Mutex(true, appName + "Singleton", out notAlreadyRunning))
				{
					if (notAlreadyRunning)
					{
						App app = new App();
						Instance = app;
						app.StartupUri = new System.Uri("MainWindow.xaml", System.UriKind.Relative);
						if(args.Length!=0)
						{
							app.UpdateArgs = args;
						}
						app.Run();
						return;
					} else
					{
						AppActivator.ActivateExisting();
					}
				}
			}
			else
			{
				ServiceBase[] ServicesToRun;
				ServicesToRun = new ServiceBase[]
				{
				new EspMonService()
				};
				ServiceBase.Run(ServicesToRun);
			}

		}
	}
}
