using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace EspMon
{
	[RunInstaller(true)]
	public partial class EspMonInstaller : Installer
	{
		private ServiceInstaller _espMonInstaller;
		private ServiceProcessInstaller _processInstaller;
		public EspMonInstaller()
		{
			InitializeComponent();
			_processInstaller = new ServiceProcessInstaller();
			_espMonInstaller = new ServiceInstaller();
			_processInstaller.Account = ServiceAccount.LocalSystem;
			_espMonInstaller.StartType = ServiceStartMode.Automatic;
			_espMonInstaller.DisplayName = "EspMon Service";
			_espMonInstaller.ServiceName = "EspMon Service";
			_espMonInstaller.Description = "Provides CPU/GPU monitoring over serial";
			Installers.Add(_espMonInstaller);
			Installers.Add(_processInstaller);
		}
	}
}
