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
		MessageWindow _msgWindow;
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
					_notifyIcon.BalloonTipText = "The Esp Mon has been minimised. Click the tray icon to show.";
					_notifyIcon.BalloonTipTitle = "Esp Mon";
					_notifyIcon.Text = "Esp Mon";
					_notifyIcon.Click += new EventHandler(_notifyIcon_Click);
					//_notifyIcon.ContextMenu = new System.Windows.Forms.ContextMenu();
					_notifyIcon.Icon = new System.Drawing.Icon(stm);
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
			_msgWindow = new MessageWindow(this);
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
			_msgWindow.Dispose();
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
			ActivateApp();
		}

		private void installService_Click(object sender, RoutedEventArgs e)
		{
			
			
		}
    }
}
