using System;
using Path = System.IO.Path;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration.Install;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

namespace EspMon
{
	internal class ViewModel : INotifyPropertyChanging, INotifyPropertyChanged, IDisposable
	{
		bool _isIdle = true;
		int _flashProgress = 0;
		StringBuilder outputBuffer = new StringBuilder();
		private Controller _controller;
		private bool _disposed;
		private bool _isFlashing;
		public event EventHandler InstallComplete;
		public event EventHandler UninstallComplete;
		public event PropertyChangedEventHandler PropertyChanged;
		public event PropertyChangingEventHandler PropertyChanging;
		SynchronizationContext _sync;
		public ObservableCollection<PortItem> PortItems { get; private set; } = new ObservableCollection<PortItem>();
		public ViewModel(SynchronizationContext sync)
		{
			_sync = sync;
			ServiceController ctl = ServiceController.GetServices()
				.FirstOrDefault(s => s.ServiceName == "EspMon Service");
			if (ctl != null)
			{
				_controller = new SvcController();
				_controller.PortItems= PortItems;
			} else
			{
				_controller = new HostedController();
				_controller.PortItems = PortItems;
			}
			_updateCheckTimer = new Timer(async (st) => {
				_latestVersion = await TryGetLaterVersionAsync();
				if (_latestVersion > Assembly.GetExecutingAssembly().GetName().Version)
				{
					((ViewModel)st)._sync.Post((st2) => { ((ViewModel)st2).UpdateVisibility = Visibility.Visible; }, st);
				}
			}, this, 0, 1000 * 60 * 10);

		}
		public System.Windows.Visibility FlashingVisibility { 
			get { return _isFlashing?System.Windows.Visibility.Visible:System.Windows.Visibility.Hidden; } 
			set {
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(FlashingVisibility)));
				_isFlashing= value==System.Windows.Visibility.Visible;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlashingVisibility)));
			}
		}
		public System.Windows.Visibility MainVisibility
		{
			get { return !_isFlashing ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden; }
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(MainVisibility)));
				_isFlashing = value != System.Windows.Visibility.Visible;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MainVisibility)));
			}
		}
		public int FlashProgress
		{
			get { return _flashProgress; }
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(FlashProgress)));
				_flashProgress= value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlashProgress)));
			}
		}
		public bool IsIdle
		{
			get { return _isIdle; }
			set
			{
				if (_isIdle != value)
				{
					PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(IsIdle)));
					_isIdle = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsIdle)));
				}
			}
		}

		public System.Windows.Visibility FlashButtonVisibility
		{
			get {
				var path = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "firmware.zip");
				return !_isFlashing && File.Exists(path) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Hidden; 
			}
		}
		public bool HasFirmware
		{
			get
			{
				return File.Exists(Path.Combine(Assembly.GetEntryAssembly().Location,"firmware.zip"));
			}
		}
		private static void DoEvents()
		{
			Application.Current.Dispatcher.Invoke(DispatcherPriority.Background,
												  new Action(delegate { }));
		}
		public bool IsPersistent
		{
			get
			{
				ServiceController ctl = ServiceController.GetServices()
					.FirstOrDefault(s => s.ServiceName == "EspMon Service");
				return ctl != null;
			}
			set
			{
				bool inst = IsPersistent;
				if (inst != value)
				{
					PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(IsPersistent)));
					DoEvents();
					if (!inst)
					{
						var task = Task.Run(() => {
							ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
						});
						task.Wait();
						InstallComplete?.Invoke(this, EventArgs.Empty);
						var path = Assembly.GetExecutingAssembly().Location;
						path = Path.Combine(Path.GetDirectoryName(path), "EspMon.cfg");
						if(!File.Exists(path))
						{
							using (var file = new StreamWriter(path))
							{
								file.WriteLine(CpuTMax.ToString());
								file.WriteLine(GpuTMax.ToString());
							}
						}
						if (_controller != null)
						{
							_controller.IsStarted = false;
							_controller.Dispose();
						}
						var newCtl = new SvcController();
						newCtl.PropertyChanging += NewCtl_PropertyChanging;
						newCtl.PropertyChanged += NewCtl_PropertyChanged;
						newCtl.PortItems= PortItems;
						_controller = newCtl;
					}
					else
					{
						if (_controller is SvcController)
						{
							IsStarted = false;
							_controller?.Dispose();
						}
						var task = Task.Run(() =>
						{
							ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
						});
						task.Wait();
						UninstallComplete?.Invoke(this, EventArgs.Empty);
						var newCtl = new HostedController();
						newCtl.PropertyChanging += NewCtl_PropertyChanging;
						newCtl.PropertyChanged += NewCtl_PropertyChanged;
						newCtl.PortItems = PortItems;
						_controller = newCtl;
					}
					Refresh();
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPersistent)));
					DoEvents();
				}
			}
		}
		public string OutputText
		{
			get
			{
				return outputBuffer.ToString();
			}
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(OutputText)));
				outputBuffer.Clear();
				outputBuffer.Append(value);
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputText)));
			}
		}
		public void ClearOutput()
		{
			PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(OutputText)));
			outputBuffer.Clear();
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputText)));
		}
		public void AppendOutput(string text, bool line = true)
		{
			PropertyChanging?.Invoke(this,new PropertyChangingEventArgs(nameof(OutputText)));
			if (line)
			{
				outputBuffer.AppendLine(text.TrimEnd());
			} else
			{
				outputBuffer.Append(text);
			}
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OutputText)));
		}
		private void NewCtl_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			PropertyChanged?.Invoke(this, e);
		}

		private void NewCtl_PropertyChanging(object sender, PropertyChangingEventArgs e)
		{
			PropertyChanging?.Invoke(this, e);
		}

		public bool IsStarted
		{
			get
			{
				return _controller.IsStarted;
			}
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(IsStarted)));
				_controller.IsStarted = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsStarted)));
			}
		}
		public int CpuTMax
		{
			get { return _controller.CpuTMax; }
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(CpuTMax)));
				_controller.CpuTMax = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CpuTMax)));
			}
		}
		public int GpuTMax
		{
			get { return _controller.GpuTMax; }
			set
			{
				PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(GpuTMax)));
				_controller.GpuTMax = value;
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GpuTMax)));
			}
		}

		public void Refresh()
		{
			_controller.Refresh();
			var items = _controller.PortItems.ToArray();
			ClearPortItems();
			foreach (PortItem item in items)
			{
				PortItems.Add(item);
			}
		}
		protected void ClearPortItems()
		{
			foreach(var item in PortItems)
			{
				if(item.Port!=null && item.Port.IsOpen)
				{
					try { item.Port.Close(); } catch { }
				}
			}
			PortItems.Clear();
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if(_controller!=null && _controller is IDisposable disp) {  disp.Dispose(); }
				_disposed = true;
			}
		}

		~ViewModel()
		{
		     Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		
		#region AutoUpdate stuff
		static readonly Regex _scrapeTags = new Regex(@"([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)<\/h2>", RegexOptions.IgnoreCase);
		const string tagUrl = "https://github.com/codewitch-honey-crisis/espmon3/releases";
		const string exeUpdateUrlFormat = "https://github.com/codewitch-honey-crisis/espmon3/releases/download/{0}/EspMon.exe";
		const string firmwareUpdateUrlFormat = "https://github.com/codewitch-honey-crisis/espmon3/releases/download/{0}/firmware.zip";
		const string ohwmUpdateUrlFormat = "https://github.com/codewitch-honey-crisis/espmon3/releases/download/{0}/OpenHardwareMonitorLib.dll";
		Timer _updateCheckTimer = null;
		Version _latestVersion = new Version();
		static async Task DownloadVersionAsync(Version version)
		{
			var localpath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			using (var http = new HttpClient())
			{
				var url = string.Format(exeUpdateUrlFormat, version.ToString());
				using (var input = await http.GetStreamAsync(url))
				{
					var filepath = Path.Combine(localpath, "EspMon.exe.download");
					try
					{
						System.IO.File.Delete(filepath);
					}
					catch { }
					using (var output = System.IO.File.OpenWrite(filepath))
					{
						await input.CopyToAsync(output);
					}
				}

				url = string.Format(firmwareUpdateUrlFormat, version.ToString());
				using (var input = await http.GetStreamAsync(url))
				{
					var filepath = Path.Combine(localpath, "firmware.zip.download");
					try
					{
						System.IO.File.Delete(filepath);
					}
					catch { }
					using (var output = System.IO.File.OpenWrite(filepath))
					{
						await input.CopyToAsync(output);
					}
				}
				url = string.Format(ohwmUpdateUrlFormat, version.ToString());
				using (var input = await http.GetStreamAsync(url))
				{
					var filepath = Path.Combine(localpath, "OpenHardwareMonitorLib.dll.download");
					try
					{
						System.IO.File.Delete(filepath);
					}
					catch { }
					using (var output = System.IO.File.OpenWrite(filepath))
					{
						await input.CopyToAsync(output);
					}
				}
			}
		}
		static async Task<Version> TryGetLaterVersionAsync()
		{
			try
			{
				var ver = Assembly.GetExecutingAssembly().GetName().Version;
				using (var http = new HttpClient())
				{
					var versions = new List<Version>();
					using (var reader = new StreamReader(await http.GetStreamAsync(tagUrl)))
					{
						var match = _scrapeTags.Match(reader.ReadToEnd());
						while (match.Success)
						{
							Version v;
							if (Version.TryParse(match.Groups[1].Value, out v))
							{
								versions.Add(v);
							}
							match = match.NextMatch();
						}
					}
					versions.Sort();
					var result = versions[versions.Count - 1];
					if (result > ver)
					{
						return result;
					}
				}
			}
			catch { }
			return new Version();
		}
		static async Task ExtractUpdaterAsync()
		{
			var localpath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			using (Stream input = Assembly.GetExecutingAssembly().GetManifestResourceStream("EspMon.EspMonUpdater.exe"))
			{
				if (input == null)
				{
					throw new Exception("Could not extract updater");
				}
				using (var output = System.IO.File.OpenWrite(Path.Combine(localpath, "EspMonUpdater.exe")))
				{
					await input.CopyToAsync(output);
				}
			}
		}
		public async Task<bool> PrepareUpdateAsync()
		{
			try
			{
				var ver = await TryGetLaterVersionAsync();
				if (Assembly.GetExecutingAssembly().GetName().Version < ver)
				{
					await DownloadVersionAsync(ver);
					await ExtractUpdaterAsync();
					return true;
				}
			}
			catch { }
			return false;
		}
		private Visibility _updateVisibility=Visibility.Collapsed;
		public Visibility UpdateVisibility
		{
			get
			{
				return _updateVisibility;
			}
			set
			{
				if (_updateVisibility != value)
				{
					PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(nameof(UpdateVisibility)));
					_updateVisibility = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateVisibility)));
				}
			}
		}
		#endregion

	}

	internal class PortItem : INotifyPropertyChanged
	{
		bool _isChecked;

		public event PropertyChangedEventHandler PropertyChanged;

		public string Name { get; private set; }
		public bool IsChecked
		{
			get
			{
				return _isChecked;
			}
			set
			{
				if (_isChecked != value)
				{
					_isChecked = value;
					if (!value)
					{
						if (Port != null && Port.IsOpen)
						{
							try { Port.Close(); } catch { }
						}
					}
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
				}
			}
		}
		public SerialPort Port { get; set; }
		public PortItem(string name, bool isChecked = false)
		{
			Name = name;
			IsChecked = isChecked;
			Port = null;
		}
		public override string ToString()
		{
			string suffix = IsChecked ? "Open" : "Closed";
			return $"{Name} - {suffix}";
		}
	}
}
