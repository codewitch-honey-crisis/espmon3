using System;
using System.Configuration.Install;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;

namespace EspMon
{
	public class App : System.Windows.Application
	{
		[STAThread]
		public static void Main(string[] args)
		{
			if (Environment.UserInteractive)
			{
				var appName = Assembly.GetEntryAssembly().GetName().Name;
				var notAlreadyRunning = true;
				using (var mutex = new Mutex(true, appName + "Singleton", out notAlreadyRunning))
				{
					if (notAlreadyRunning)
					{
						if (args.Length == 0)
						{
							App app = new App();

							app.StartupUri = new System.Uri("MainWindow.xaml", System.UriKind.Relative);

							app.Run();
							return;
						}
						string parameter = string.Concat(args);
						switch (parameter)
						{
							case "--install":
								ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
								break;
							case "--uninstall":
								ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
								break;
						}
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
