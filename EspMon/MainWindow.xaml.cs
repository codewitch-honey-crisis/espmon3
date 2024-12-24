using Path = System.IO.Path;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Ports;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.IO.Compression;
using System.Windows.Threading;
using System.Windows.Controls;
using EL;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Linq;
using System.Globalization;

namespace EspMon
{


	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		internal const int UploadBaud = 115200 * 4;
		AppActivator _appActivator;
		private string _updateArgs;
		private System.Windows.Forms.ContextMenu NotifyContextMenu;
		private System.Windows.Forms.MenuItem NotifyContextMenuStarted;
		private System.Windows.Forms.MenuItem NotifyContextMenuSeparator;
		private System.Windows.Forms.MenuItem NotifyContextMenuShow;
		ViewModel _ViewModel;
		System.Windows.Forms.NotifyIcon _notifyIcon;
		private void Init()
		{
			InitializeComponent();

			_ViewModel = new ViewModel(SynchronizationContext.Current);
			DataContext = _ViewModel;
			using (Stream stm = System.Reflection.Assembly.GetEntryAssembly().GetManifestResourceStream("EspMon.espmon.ico"))
			{
				if (stm != null)
				{
					_notifyIcon = new System.Windows.Forms.NotifyIcon();
					_notifyIcon.BalloonTipText = "Esp Mon has been minimised. Click the tray icon to show.";
					_notifyIcon.BalloonTipTitle = "Esp Mon";
					_notifyIcon.Text = "Esp Mon";
					_notifyIcon.Click += new EventHandler(_notifyIcon_Click); _notifyIcon.Icon = new System.Drawing.Icon(stm);
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
		public MainWindow()
		{
			Init();
		}
		PortItem FindPortItem(string name)
		{
			foreach(var item in _ViewModel.PortItems)
			{
				if(item.Name.Equals(name,StringComparison.OrdinalIgnoreCase))
				{
					return item;
				}
			}
			return null;
		}
		

		private void _appActivator_AppActivated(object sender, EventArgs e)
		{
			ActivateApp();
		}

		private void _ViewModel_UninstallComplete(object sender, EventArgs e)
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
			if (e.PropertyName == "IsPersistent")
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
			var args = App.Instance.UpdateArgs;
			if (args!=null && args.Length>0)
			{
				if (args.Contains("--finish_updater"))
				{
					try
					{
						File.Delete(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "EspMonUpdater.exe"));
					}
					catch { }
					var exe = args.Contains("--exe");
					var firmware = args.Contains("--firmware");
					var persistent = args.Contains("--persistent");
					var started = args.Contains("--started");
					if (firmware)
					{
						MessageBox.Show("The firmware has been updated. You should reflash your device(s).", "Update occurred", MessageBoxButton.OK);
					}
					if (!persistent)
					{
						var cputmax = Array.IndexOf(args, "--cputmax");
						var gputmax = Array.IndexOf(args, "--gputmax");
						if (cputmax > -1 && cputmax < args.Length - 1)
						{
							int tmax;
							if (int.TryParse(args[cputmax+1], NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out tmax))
							{
								_ViewModel.CpuTMax = tmax;
							}
						}
						if (gputmax > -1 && gputmax < args.Length - 1)
						{
							int tmax;
							if (int.TryParse(args[gputmax+1], NumberStyles.Integer, CultureInfo.InvariantCulture.NumberFormat, out tmax))
							{
								_ViewModel.GpuTMax = tmax;
							}
						}
						_ViewModel.IsStarted = started;
		
						for (int i = 0; i < args.Length; i++)
						{
							var a = args[i].Substring(2);
							var port = FindPortItem(a);
							if (port != null)
							{
								port.IsChecked = true;
							}
						}
						
					} else
					{
						_ViewModel.IsPersistent = true;
						_ViewModel.IsStarted = started;
					}
					
				}
			}
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
			if (_updateArgs != null)
			{
				// we're doing an update
				var psi = new ProcessStartInfo()
				{
					FileName = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "EspMonUpdater.exe"),
					UseShellExecute = true,
					Verb = "runas",
					Arguments = _updateArgs
				};
				var proc = Process.Start(psi);
			}
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
				foreach (var item in items)
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
		private void output_TextChanged(object sender, TextChangedEventArgs e)
		{
			output.ScrollToEnd();
		}

#if !USE_ESPLINK_OOP && !USE_ESPTOOL
		// doesn't friggin flash for some reason
		// losing serial events inproc
		internal class EspProgress : IProgress<int>
		{
			ViewModel _model;
			int _old = -1;
			public EspProgress(ViewModel viewModel)
			{
				_model = viewModel;
			}
			public int Value { get; private set; }
			public bool IsBounded
			{
				get
				{
					return Value >= 0;
				}
			}

			public void Report(int value)
			{
				Value = value;
				if (value != _old)
				{
					if (IsBounded)
					{
						_model.AppendOutput($"{value}%", true);
						_model.FlashProgress = value;
					}
					else
					{
						_model.AppendOutput(".", false);
						_model.FlashProgress = 1;
					}
					_old = value;
				}
			}
		}
		private async void flashDeviceButton_Click(object sender, RoutedEventArgs e)
		{
			var startPending = false;
			if (_ViewModel.IsStarted)
			{
				startPending = true;
				_ViewModel.IsStarted = false;
				DoEvents();
			}
			var path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "firmware.zip");
			MemoryStream stm = null;
			ZipArchive file = null;
			_ViewModel.FlashProgress = 1;
			_ViewModel.IsIdle = false;
			DoEvents();
			try
			{
				file = ZipFile.OpenRead(path);

				foreach (var entry in file.Entries)
				{
					if (entry.Name == deviceCombo.Text + ".bin")
					{
						stm = new MemoryStream();
						using (var stm2 = entry.Open())
						{
							await stm2.CopyToAsync(stm);
						}
						break;
					}
				}
				if (stm == null)
				{
					throw new Exception("Unable to find archive entry");
				}
				stm.Position = 0;
				var portName = comPortCombo.Text;
				try
				{
					using (var link = new EspLink(portName))
					{
						link.SerialHandshake = Handshake.RequestToSend;
						_ViewModel.AppendOutput("Connecting...", false);
						await link.ConnectAsync(EspConnectMode.Default, 3, false, CancellationToken.None, link.DefaultTimeout, new EspProgress(_ViewModel));
						_ViewModel.AppendOutput("done!", true);
						_ViewModel.AppendOutput("Running Stub...", true);
						await link.RunStubAsync(CancellationToken.None, link.DefaultTimeout, new EspProgress(_ViewModel));
						_ViewModel.AppendOutput("", true);
						await link.SetBaudRateAsync(115200 * 8, CancellationToken.None, link.DefaultTimeout);
						_ViewModel.AppendOutput($"Changed baud rate to {link.BaudRate}", true);
						_ViewModel.AppendOutput($"Flashing to offset 0x10000... ", true);
						await link.FlashAsync(CancellationToken.None, stm, 16 * 1024, 0x10000, 3, false, link.DefaultTimeout, new EspProgress(_ViewModel));
						_ViewModel.AppendOutput("", true);
						_ViewModel.AppendOutput("Hard resetting", true);
						link.Reset();
						_ViewModel.AppendOutput($"Finished flashing {stm.Length / 1024}KB to {portName}", true);
					}
				}
				catch (Exception ex)
				{
					_ViewModel.AppendOutput($"Error flashing firmware: {ex.Message}", true);
					_ViewModel.FlashProgress = 0;
					_ViewModel.IsIdle = true;
					DoEvents();	
				}
				finally
				{
					if (stm != null)
					{
						stm.Close();
						stm = null;
					}

				}
			}
			finally
			{
				if (stm != null)
				{
					stm.Close();
				}
				if (file != null)
				{
					file.Dispose();
				}
				_ViewModel.FlashProgress = 0;
				_ViewModel.IsIdle = true;
				DoEvents();
				Thread.Sleep(50);
				
				if (startPending)
				{
					_ViewModel.IsStarted = true;
				}
				DoEvents();


			}
		}

		private async void updateButton_Click(object sender, RoutedEventArgs e)
		{
			var updated = await _ViewModel.PrepareUpdateAsync();
			if (updated)
			{
				var extra = "";
				_updateArgs = "--finish_updater";
				if (_ViewModel.IsPersistent)
				{
					_updateArgs += " --persistent";
					_ViewModel.IsPersistent = false;
				}
				else
				{
					extra += " --cputmax " + _ViewModel.CpuTMax.ToString();
					extra += " --gputmax " + _ViewModel.GpuTMax.ToString();
					foreach (var port in _ViewModel.PortItems)
					{
						if (port.IsChecked)
						{
							extra += (" --" + port.Name);

						}
					}
				}
				if (_ViewModel.IsStarted)
				{
					_ViewModel.IsStarted = false;
					_updateArgs += " --started";
				}
				_updateArgs += extra;
				Close();
			}
			
		}
#endif
#if USE_ESPLINK_OOP
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
						break;
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
#endif
#if USE_ESPTOOL
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
						break;
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
