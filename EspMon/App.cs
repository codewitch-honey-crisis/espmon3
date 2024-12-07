﻿using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace EspMon
{
	public class App : System.Windows.Application

	{

		[STAThread]

		public static void Main(string[] args)

		{
			if (Environment.UserInteractive)
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