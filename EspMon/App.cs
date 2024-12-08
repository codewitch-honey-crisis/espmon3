using System;
using System.Collections.Generic;
using System.Configuration.Install;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EspMon
{
	public class App : System.Windows.Application

	{
		[DllImport("user32.dll",CharSet = CharSet.Unicode)]
		static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

		// Delegate to filter which windows to include 
		public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

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
					} else
					{
						IntPtr hwnd = FindWindowsWithText("Esp Mon Activator").FirstOrDefault();
						if(hwnd!=IntPtr.Zero)
						{
							PostMessage(hwnd, MessageWindow.WM_CUSTOM_ACTIVATE, IntPtr.Zero, IntPtr.Zero);
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
		
		static string GetWindowText(IntPtr hWnd)
		{
			int size = GetWindowTextLength(hWnd);
			if (size > 0)
			{
				var builder = new StringBuilder(size + 1);
				GetWindowText(hWnd, builder, builder.Capacity);
				return builder.ToString();
			}

			return String.Empty;
		}

		static IEnumerable<IntPtr> FindWindows(EnumWindowsProc filter)
		{
			IntPtr found = IntPtr.Zero;
			List<IntPtr> windows = new List<IntPtr>();

			EnumWindows(delegate (IntPtr wnd, IntPtr param)
			{
				if (filter(wnd, param))
				{
					// only add the windows that pass the filter
					windows.Add(wnd);
				}

				// but return true here so that we iterate all windows
				return true;
			}, IntPtr.Zero);

			return windows;
		}

		/// <summary> Find all windows that contain the given title text </summary>
		/// <param name="titleText"> The text that the window title must contain. </param>
		public static IEnumerable<IntPtr> FindWindowsWithText(string titleText)
		{
			return FindWindows(delegate (IntPtr wnd, IntPtr param)
			{
				return GetWindowText(wnd).Contains(titleText);
			});
		}
	}
}
