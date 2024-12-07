using Microsoft.Win32;

using OpenHardwareMonitor.Hardware;

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


namespace EspMon
{
	

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
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
					_notifyIcon.BalloonTipText = "The Esp Mon has been minimised. Click the tray icon to show.";
					_notifyIcon.BalloonTipTitle = "Esp Mon";
					_notifyIcon.Text = "Esp Mon";
					_notifyIcon.Click += new EventHandler(_notifyIcon_Click);
					_notifyIcon.Icon = new System.Drawing.Icon(stm);
					_notifyIcon.Visible = true;	
				}
			}	
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
			if (!_ViewModel.IsInstalled)
			{
				_ViewModel.IsStarted = false;
			}
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
			Show();
			WindowState = _storedWindowState;
		}

		private void installService_Click(object sender, RoutedEventArgs e)
		{
			
			
		}
    }
}
