using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EspMonUpdater
{
	internal class Program
	{
		static int Main(string[] args)
		{
			var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var exeFile = Path.Combine(path, "EspMon.exe");
			var ohwmFile = Path.Combine(path, "OpenHardwareMonitorLib.dll");
			var targetFile = exeFile;
			var firmwareFile = Path.Combine(path, "firmware.zip");
			var exeUpdated = false;
			var firmwareUpdated = false;
			var downloadFile = targetFile + ".download";
			if (File.Exists(downloadFile))
			{
				try
				{
					File.Delete(targetFile);
				}
				catch { };

				try
				{
					File.Move(downloadFile, targetFile);
					exeUpdated = true;
				}
				catch
				{
					return 1;
				}
			}
			targetFile = firmwareFile;
			downloadFile = targetFile + ".download";
			if (File.Exists(downloadFile))
			{
				try
				{
					File.Delete(targetFile);
				}
				catch { };

				try
				{
					File.Move(downloadFile, targetFile);
					firmwareUpdated= true;
				}
				catch
				{
					return 1;
				}
			}
			targetFile = ohwmFile; ;
			downloadFile = targetFile + ".download";
			if (File.Exists(downloadFile))
			{
				try
				{
					File.Delete(targetFile);
				}
				catch { };

				try
				{
					File.Move(downloadFile, targetFile);
					exeUpdated = true;
				}
				catch
				{
					return 1;
				}
			}
			var psi = new ProcessStartInfo()
			{
				FileName = exeFile,
				Verb = "runas",
				UseShellExecute = true,
				Arguments = ""
			};
			if (exeUpdated)
			{
				psi.Arguments += " --exe";
			}
			if (firmwareUpdated)
			{
				psi.Arguments += " --firmware";
			}
			psi.Arguments = psi.Arguments.TrimStart();
			for(int i = 0;i<args.Length;++i)
			{
				psi.Arguments += (" \"" + args[i].Replace("\"","\"")+"\"");
			}
			var proc = Process.Start(psi);
			return 0;
		}
	}
}
