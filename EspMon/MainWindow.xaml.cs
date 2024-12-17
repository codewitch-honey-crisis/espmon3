using Microsoft.Win32;

using OpenHardwareMonitor.Hardware;
using Path = System.IO.Path;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.ComTypes;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.IO.Compression;
using System.Diagnostics;
using System.Windows.Input;
using System.Text;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Controls;
using EL;

namespace EspMon
{
	

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		internal const int UploadBaud = 115200 * 4;
		AppActivator _appActivator;
		private System.Windows.Forms.ContextMenu NotifyContextMenu;
		private System.Windows.Forms.MenuItem NotifyContextMenuStarted;
		private System.Windows.Forms.MenuItem NotifyContextMenuSeparator;
		private System.Windows.Forms.MenuItem NotifyContextMenuShow;
		ViewModel _ViewModel;
		System.Windows.Forms.NotifyIcon _notifyIcon;
		public MainWindow()
		{
			InitializeComponent();
			
			this.Loaded += MainWindow_Loaded;
			_ViewModel = new ViewModel();
			DataContext = _ViewModel;
			using (Stream stm = System.Reflection.Assembly.GetEntryAssembly().GetManifestResourceStream("EspMon.espmon.ico"))
			{
				if (stm != null)
				{
					_notifyIcon = new System.Windows.Forms.NotifyIcon();
					_notifyIcon.BalloonTipText = "Esp Mon has been minimised. Click the tray icon to show.";
					_notifyIcon.BalloonTipTitle = "Esp Mon";
					_notifyIcon.Text = "Esp Mon";
					_notifyIcon.Click += new EventHandler(_notifyIcon_Click);					_notifyIcon.Icon = new System.Drawing.Icon(stm);
					_notifyIcon.Visible = true;	
				}
			}
			this.NotifyContextMenu = new System.Windows.Forms.ContextMenu();
			this.NotifyContextMenuStarted = new System.Windows.Forms.MenuItem();
			this.NotifyContextMenuSeparator = new System.Windows.Forms.MenuItem();
			this.NotifyContextMenuShow = new System.Windows.Forms.MenuItem();
			this.NotifyContextMenu.Name = "NotifyContextMenu";
			this.NotifyContextMenuStarted.Name = "NotifyContextMenuStarted";
			this.NotifyContextMenuStarted.Text = "Started";
			this.isStartedCheckbox.Checked += IsStartedCheckbox_Checked;
			this.NotifyContextMenuStarted.Click += NotifyContextMenuStarted_Click;
			this.NotifyContextMenuSeparator.Name = "NotifyContextMenuSeparator";
			this.NotifyContextMenuSeparator.Text = "-";
			this.NotifyContextMenuShow.Name = "ShowToolStripMenuItem";
			this.NotifyContextMenuShow.Text = "Show...";
			this.NotifyContextMenuShow.Click += NotifyContextMenuShow_Click;
			this.NotifyContextMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] {
			this.NotifyContextMenuShow,
			this.NotifyContextMenuSeparator,
			this.NotifyContextMenuStarted});
			_notifyIcon.ContextMenu = this.NotifyContextMenu;
			_appActivator = new AppActivator();
			_appActivator.AppActivated += _appActivator_AppActivated;
			_ViewModel.PropertyChanging += _ViewModel_PropertyChanging;
			_ViewModel.InstallComplete += _ViewModel_InstallComplete;
			_ViewModel.UninstallComplete += _ViewModel_UninstallComplete;
		}

		private void _appActivator_AppActivated(object sender, EventArgs e)
		{
			ActivateApp();
		}

		private void _ViewModel_UninstallComplete(object sender, EventArgs e)
		{
			const bool enabled = true;
			serviceInstalledButton.IsEnabled= enabled;
			isStartedCheckbox.IsEnabled = enabled;
			flashDevice.IsEnabled = enabled;
			cpuTmax.IsEnabled = enabled;
			gpuTmax.IsEnabled = enabled;
			comPortsList.IsEnabled = enabled;
			refreshComPortCombo.IsEnabled = enabled;
			

		}

		private void _ViewModel_InstallComplete(object sender, EventArgs e)
		{
			const bool enabled = true;
			serviceInstalledButton.IsEnabled = enabled;
			isStartedCheckbox.IsEnabled = enabled;
			flashDevice.IsEnabled = enabled;
			cpuTmax.IsEnabled = enabled;
			gpuTmax.IsEnabled = enabled;
			comPortsList.IsEnabled = enabled;
			refreshComPortCombo.IsEnabled = enabled;

		}

		private void _ViewModel_PropertyChanging(object sender, PropertyChangingEventArgs e)
		{
			if(e.PropertyName=="IsPersistent")
			{
				bool enabled = false;
				serviceInstalledButton.IsEnabled = enabled;
				isStartedCheckbox.IsEnabled = enabled;
				flashDevice.IsEnabled = enabled;
				cpuTmax.IsEnabled = enabled;
				gpuTmax.IsEnabled = enabled;
				comPortsList.IsEnabled = enabled;
				refreshComPortCombo.IsEnabled = enabled;

			}
		}


		public void ActivateApp()
		{
			Show();
			WindowState = _storedWindowState;
		}
		private void NotifyContextMenuStarted_Click(object sender, EventArgs e)
		{
			_ViewModel.IsStarted = !_ViewModel.IsStarted;
			NotifyContextMenuStarted.Checked = _ViewModel.IsStarted;
		}

		private void IsStartedCheckbox_Checked(object sender, RoutedEventArgs e)
		{
			this.NotifyContextMenuStarted.Checked = isStartedCheckbox.IsChecked.Value;
		}

		
		private void NotifyContextMenuShow_Click(object sender, EventArgs e)
		{
			ActivateApp();
		}

		private void MainWindow_Loaded(object sender, RoutedEventArgs e)
		{
			_ViewModel.Refresh();
		}

		protected override void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);
		}
		protected override void OnClosed(EventArgs e)
		{
			_appActivator.Dispose();
			if (!_ViewModel.IsPersistent)
			{
				_ViewModel.IsStarted = false;
			}
			_ViewModel.Dispose();
			_notifyIcon.Dispose();
			_notifyIcon = null;
			base.OnClosed(e);
		}
		private void comPortsRefresh_Click(object sender, RoutedEventArgs e)
		{
			_ViewModel.Refresh();
		}


		private WindowState _storedWindowState = WindowState.Normal;
		protected override void OnStateChanged(EventArgs e)
		{
			if (WindowState == WindowState.Minimized)
			{
				Hide();
				if (_notifyIcon != null)
					_notifyIcon.ShowBalloonTip(2000);
			}
			else
				_storedWindowState = WindowState;
		}
		

		void _notifyIcon_Click(object sender, EventArgs e)
		{
			ActivateApp();
		}

		private void flashDevice_Click(object sender, RoutedEventArgs e)
		{
			
			RefreshFlashingComPorts();
			RefreshFlashingDevices();
			_ViewModel.MainVisibility = Visibility.Hidden;
			_ViewModel.FlashingVisibility = Visibility.Visible;
		}

		private void back_Click(object sender, RoutedEventArgs e)
		{
			_ViewModel.MainVisibility = Visibility.Visible;
			_ViewModel.FlashingVisibility = Visibility.Hidden;
		}
		void RefreshFlashingComPorts()
		{
			int si = comPortCombo.SelectedIndex;
			if (si == -1) { si = 0; }
			comPortCombo.Items.Clear();
			foreach (var port in SerialPort.GetPortNames())
			{
				comPortCombo.Items.Add(port);
			}
			if (comPortCombo.Items.Count > si)
			{
				comPortCombo.SelectedIndex = si;
			}
			else if (comPortCombo.Items.Count > 0)
			{
				comPortCombo.SelectedIndex = 0;
			}
		}
		void RefreshFlashingDevices()
		{
			var path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "firmware.zip");
			int si = deviceCombo.SelectedIndex;
			if (si == -1) { si = 0; }
			deviceCombo.Items.Clear();
			try
			{
				var items = new List<string>();
				using (var file = ZipFile.OpenRead(path))
				{
					foreach (var entry in file.Entries)
					{
						items.Add(Path.GetFileNameWithoutExtension(entry.FullName));
					}
				}
				items.Sort();
				foreach(var item in items)
				{
					deviceCombo.Items.Add(item);
				}
			}
			catch
			{

			}
			if (deviceCombo.Items.Count > si)
			{
				deviceCombo.SelectedIndex = si;
			}
			else if (deviceCombo.Items.Count > 0)
			{
				deviceCombo.SelectedIndex = 0;
			}
		}
		private void refreshComPortCombo_Click(object sender, RoutedEventArgs e)
		{
			RefreshFlashingComPorts();
			
		}
		private static void DoEvents()
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
												  new Action(delegate { }));
		}
#if !USE_ESPTOOL
		private void flashDeviceButton_Click(object sender, RoutedEventArgs e)
		{
			var startPending = false;
			if (_ViewModel.IsStarted)
			{
				startPending = true;
				_ViewModel.IsStarted = false;
			}
			var path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "firmware.zip");
			var path2 = Path.Combine(Path.GetDirectoryName(path), "firmware.bin");
			_ViewModel.FlashProgress = 1;
			_ViewModel.IsIdle = false;
			DoEvents();
			using (var file = ZipFile.OpenRead(path))
			{
				foreach (var entry in file.Entries)
				{
					if (entry.Name == deviceCombo.Text + ".bin")
					{

						try
						{
							File.Delete(path2);
						}
						catch { }
						entry.ExtractToFile(path2);
					}
				}
			}
			path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "esplink.exe");
			var sb = new StringBuilder();
			sb.Append(comPortCombo.Text);
			sb.Append(" firmware.bin");
			var psi = new ProcessStartInfo(path, sb.ToString())
			{
				CreateNoWindow = true,
				WorkingDirectory = Path.GetDirectoryName(path),
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				RedirectStandardInput = false
			};
			using (var proc = Process.Start(psi))
			{
				if (proc == null)
				{
					throw new IOException(
						"Error burning firmware");
				}
				var isprog = false;
				while (!proc.StandardOutput.EndOfStream)
				{
					var line = proc.StandardOutput.ReadLine();
					if (line != null)
					{
						_ViewModel.AppendOutput(line,true);
						if (line.EndsWith("%"))
						{
							var num = line.Substring(0, line.Length-1);
							int i;
							if (int.TryParse(num, out i))
							{
								isprog = true;
								_ViewModel.FlashProgress = i;
							}
						}
						else if (isprog)
						{
							_ViewModel.FlashProgress = 100;
						}
						output.ScrollToEnd();
						DoEvents();
					}
				}
				proc.WaitForExit();

				try
				{
					File.Delete(path2);
				}
				catch { }
				_ViewModel.FlashProgress = 0;
				_ViewModel.IsIdle = true;
				DoEvents();
			}
			if (startPending)
			{
				_ViewModel.IsStarted = true;
			}

		}
#else
		private void flashDeviceButton_Click(object sender, RoutedEventArgs e)
		{
			var startPending = false;
			if(_ViewModel.IsStarted)
			{
				startPending = true;
				_ViewModel.IsStarted = false;
			}
			var path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "firmware.zip");
			var path2 = Path.Combine(Path.GetDirectoryName(path), "firmware.bin");
			_ViewModel.FlashProgress = 1;
			_ViewModel.IsIdle = false;
			DoEvents();
			using (var file = ZipFile.OpenRead(path))
			{
				foreach (var entry in file.Entries)
				{
					if (entry.Name == deviceCombo.Text + ".bin")
					{
						
						try
						{
							File.Delete(path2);
						}
						catch { }
						entry.ExtractToFile(path2);
					}
				}
			}
			path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "esptool.exe");
			var sb = new StringBuilder();
			sb.Append("--baud " + UploadBaud.ToString());
			sb.Append(" --port " + comPortCombo.Text);
			sb.Append(" write_flash 0x10000 firmware.bin");
			var psi = new ProcessStartInfo(path, sb.ToString())
			{
				CreateNoWindow = true,
				WorkingDirectory = Path.GetDirectoryName(path),
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				RedirectStandardInput = false
			};
			using (var proc = Process.Start(psi))
			{
				if (proc == null)
				{
					throw new IOException(
						"Error burning firmware");
				}
				var isprog = false;
				while (!proc.StandardOutput.EndOfStream)
				{
					var line = proc.StandardOutput.ReadLine();
					if (line != null)
					{
						_ViewModel.AppendOutputLine(line);
						if(line.EndsWith(" %)"))
						{
							int idx = line.IndexOf("... ");
							if(idx>-1)
							{
								var num = line.Substring(idx + 5, line.Length - idx - 8);
								int i;
								if(int.TryParse(num,out i))
								{
									isprog = true;
									_ViewModel.FlashProgress = i;
								}
							}
						} else if(isprog)
						{
							_ViewModel.FlashProgress = 100;
						}
						output.ScrollToEnd();
						DoEvents();
					}
				}
				proc.WaitForExit();
				
				try
				{
					File.Delete(path2);
				}
				catch { }
				_ViewModel.FlashProgress = 0; 
				_ViewModel.IsIdle = true;
				DoEvents();
			}
			if(startPending)
			{
				_ViewModel.IsStarted = true;
			}

		}
#endif
	}
}
