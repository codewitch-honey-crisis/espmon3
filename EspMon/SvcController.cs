using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.Threading;

namespace EspMon
{
	internal class SvcController : Controller
	{
	
		public SvcController()
		{
			
		}

		

		protected override int GetCpuTMax()
		{
			int cpuTMax;
			ReadTMax(out cpuTMax, out _);
			return cpuTMax;
		}
		protected override void SetCpuTMax(int value)
		{
			if (value < 0 || value > 255)
			{
				value = 90;
			}
			var path = GetEnsurePath();
			int cpuTMax,gpuTMax;
			ReadTMax(out cpuTMax, out gpuTMax);
			var ports = GetRegPortNames();
			cpuTMax = value;
			TextWriter writer;
			while(!TryOpenWrite(path,out writer))
			{
				Thread.Sleep(10);
			}
			try
			{
				writer.WriteLine(cpuTMax.ToString());
				writer.WriteLine(gpuTMax.ToString());
				foreach (var port in ports)
				{
					writer.WriteLine(port);
				}
			}
			finally
			{
				writer.Close();
			}
		}
		protected override int GetGpuTMax()
		{
			int gpuTMax;
			ReadTMax(out _, out gpuTMax);
			return gpuTMax;
		}
		protected override void SetGpuTMax(int value)
		{
			if (value < 0 || value > 255)
			{
				value = 80;
			}
			var path = GetEnsurePath();
			int cpuTMax, gpuTMax;
			ReadTMax(out cpuTMax, out gpuTMax);
			var ports = GetRegPortNames();
			cpuTMax = value;
			TextWriter writer;
			while (!TryOpenWrite(path, out writer))
			{
				Thread.Sleep(10);
			}
			try
			{
				writer.WriteLine(cpuTMax.ToString());
				writer.WriteLine(gpuTMax.ToString());
				foreach (var port in ports)
				{
					writer.WriteLine(port);
				}
			}
			finally
			{
				writer.Close();
			}
		}
		protected override void StopAll()
		{
			ServiceController ctl = ServiceController.GetServices()
					.FirstOrDefault(s => s.ServiceName == "EspMon Service");
			if (ctl != null)
			{
				ctl.Stop();
			}
		}
		protected override void OnRefresh()
		{
			base.OnRefresh();
			foreach(var portItem in PortItems)
			{
				portItem.PropertyChanged += PortItem_PropertyChanged;
			}
		}
		void ReadTMax(out int cpuTMax,out int gpuTMax)
		{
			cpuTMax = 90;
			gpuTMax = 80;
			var path = GetEnsurePath();
			using (var reader = new StreamReader(path))
			{
				string line = reader.ReadLine()?.Trim();
				if (line != null)
				{
					if (!int.TryParse(line, out cpuTMax))
					{
						cpuTMax = 90;
					}
				}
				line = reader.ReadLine()?.Trim();
				if (line != null)
				{
					if (!int.TryParse(line, out gpuTMax))
					{
						gpuTMax = 80;
					}
				}
			}
		}
		static bool TryOpenAppend(string path, out TextWriter writer)
		{
			writer = null;
			try
			{
				writer = new StreamWriter(path, true);
				return true;
			}
			catch
			{

			}
			return false;
		}
		static bool TryOpenWrite(string path, out TextWriter writer)
		{
			writer = null;
			try
			{
				var w= new StreamWriter(path, false);
				w.BaseStream.SetLength(0);
				writer = w;
				return true;
			}
			catch
			{

			}
			return false;
		}
		static string GetEnsurePath()
		{
			var path = Assembly.GetExecutingAssembly().Location;
			path = Path.Combine(Path.GetDirectoryName(path), "EspMon.cfg");
			if (File.Exists(path))
			{
				return path;
			}
			using (var writer = new StreamWriter(path, false))
			{
				writer.WriteLine(90); // CPU TMax
				writer.WriteLine(80); // CPU TMax
			}
			return path;
		}
		void AddPorts(string[] names)
		{
			var portNames = GetRegPortNames();
			var path = GetEnsurePath();
			TextWriter writer;
			while(!TryOpenAppend(path,out writer))
			{
				Thread.Sleep(10);
			}
			foreach (var name in names)
			{
				if(!portNames.Contains(name,StringComparer.InvariantCultureIgnoreCase))
				{
					writer.WriteLine(name);
				}
			}
			writer.Close();
		}
		private static int IndexOfPort(IEnumerable<string> portNames,string name)
		{
			int i = 0;
			foreach (var n in portNames)
			{
				if (n.Equals(name, StringComparison.InvariantCultureIgnoreCase))
				{
					return i;
				}
				++i;
			}
			return -1;
		}
		void RemovePorts(string[] names)
		{
			var portNames = new List<string>(GetRegPortNames());
			for (int i = 0; i < names.Length; ++i)
			{
				var idx = IndexOfPort(portNames, names[i]);
				if (i > -1)
				{
					portNames.RemoveAt(idx);
				}
			}
			int cpuTMax, gpuTMax;
			ReadTMax(out cpuTMax, out gpuTMax);
			var path = GetEnsurePath();
			TextWriter writer;
			while(!TryOpenWrite(path,out writer))
			{
				Thread.Sleep(10);
			}
			try
			{
				writer.WriteLine(cpuTMax.ToString());
				writer.WriteLine(gpuTMax.ToString());
				foreach(var name in portNames)
				{
					writer.WriteLine(name);
				}
			}
			finally
			{
				writer.Close();
			}
		}
		private void PortItem_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			var portItem = (PortItem)sender;
			if (e.PropertyName=="IsChecked")
			{
				if(portItem.IsChecked)
				{
					AddPorts(new string[] { portItem.Name });
				} else
				{
					RemovePorts(new string[] { portItem.Name });
				}
			}
		}

		protected override void Start()
		{
			ServiceController ctl = ServiceController.GetServices()
					.FirstOrDefault(s => s.ServiceName == "EspMon Service");
			if (ctl != null)
			{
				ctl.Start();
			}
		}
		protected override bool GetIsStarted()
		{
			ServiceController ctl = ServiceController.GetServices()
					.FirstOrDefault(s => s.ServiceName == "EspMon Service");
			if (ctl != null)
			{
				return ctl.Status == ServiceControllerStatus.StartPending || ctl.Status == ServiceControllerStatus.Running;
			}
			return false;
		}
		protected override void OnClose()
		{
			// don't stop the service
		}
	}
}