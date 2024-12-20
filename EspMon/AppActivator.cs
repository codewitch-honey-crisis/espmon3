using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace EspMon
{
	class AppActivator : IDisposable
	{
		private const int WM_CUSTOM_ACTIVATE = 0x801C;
		public static AppActivator Instance { get; private set; }
		delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern int GetWindowTextLength(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

		// Delegate to filter which windows to include 
		public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
		[System.Runtime.InteropServices.StructLayout(
			System.Runtime.InteropServices.LayoutKind.Sequential,
		   CharSet = System.Runtime.InteropServices.CharSet.Unicode
		)]
		struct WNDCLASS
		{
			public uint style;
			public IntPtr lpfnWndProc;
			public int cbClsExtra;
			public int cbWndExtra;
			public IntPtr hInstance;
			public IntPtr hIcon;
			public IntPtr hCursor;
			public IntPtr hbrBackground;
			[System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
			public string lpszMenuName;
			[System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
			public string lpszClassName;
		}

		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		static extern System.UInt16 RegisterClassW(
			[System.Runtime.InteropServices.In] ref WNDCLASS lpWndClass
		);

		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		static extern IntPtr CreateWindowExW(
		   UInt32 dwExStyle,
		   [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
	   string lpClassName,
		   [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)]
	   string lpWindowName,
		   UInt32 dwStyle,
		   Int32 x,
		   Int32 y,
		   Int32 nWidth,
		   Int32 nHeight,
		   IntPtr hWndParent,
		   IntPtr hMenu,
		   IntPtr hInstance,
		   IntPtr lpParam
		);

		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		static extern System.IntPtr DefWindowProcW(
			IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam
		);

		[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
		static extern bool DestroyWindow(
			IntPtr hWnd
		);

		private const int ERROR_CLASS_ALREADY_EXISTS = 1410;

		private bool m_disposed;
		private IntPtr m_hwnd;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if (!m_disposed)
			{
				// Dispose unmanaged resources
				if (m_hwnd != IntPtr.Zero)
				{
					DestroyWindow(m_hwnd);
					m_hwnd = IntPtr.Zero;
				}
				m_disposed = true;
			}
		}
		~AppActivator()
		{
			Dispose(false);
		}
		public event EventHandler AppActivated;
		public AppActivator()
		{
			if (Instance != null)
			{
				throw new InvalidOperationException("A message window already exists");
			}
			m_wnd_proc_delegate = ForwardWndProc;

			// Create WNDCLASS
			WNDCLASS wind_class = new WNDCLASS();
			wind_class.lpszClassName = Assembly.GetEntryAssembly().GetName().Name+"ActCls";
			wind_class.lpfnWndProc = System.Runtime.InteropServices.Marshal.GetFunctionPointerForDelegate(m_wnd_proc_delegate);

			UInt16 class_atom = RegisterClassW(ref wind_class);

			int last_error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();

			if (class_atom == 0 && last_error != ERROR_CLASS_ALREADY_EXISTS)
			{
				throw new System.Exception("Could not register window class");
			}
			Instance = this;
			// Create window
			m_hwnd = CreateWindowExW(
				0,
				wind_class.lpszClassName,
				Assembly.GetEntryAssembly().GetName().Name+" Activator",
				0,
				0,
				0,
				0,
				0,
				IntPtr.Zero,
				IntPtr.Zero,
				IntPtr.Zero,
				IntPtr.Zero
			);
			
		}

		private static IntPtr ForwardWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
		{
			if(msg==WM_CUSTOM_ACTIVATE && Instance!=null)
			{
				Instance.AppActivated?.Invoke(Instance,EventArgs.Empty);
			}
			return DefWindowProcW(hWnd, msg, wParam, lParam);
		}

		private WndProc m_wnd_proc_delegate;

		public static bool ActivateExisting()
		{
			IntPtr hwnd = FindWindowsWithText(Assembly.GetEntryAssembly().GetName().Name+" Activator").FirstOrDefault();
			if (hwnd != IntPtr.Zero)
			{
				PostMessage(hwnd, AppActivator.WM_CUSTOM_ACTIVATE, IntPtr.Zero, IntPtr.Zero);
				return true;
			}
			return false;
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
