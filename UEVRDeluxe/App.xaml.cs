#region Usings
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using UEVRDeluxe.Code;
#endregion

namespace UEVRDeluxe;

/// <summary>Provides application-specific behavior to supplement the default Application class.</summary>
public partial class App : Application {
	public App() { this.InitializeComponent(); }

	/// <summary>Invoked when the application is launched.</summary>
	/// <param name="args">Details about the launch request and process.</param>
	protected override void OnLaunched(LaunchActivatedEventArgs args) {
		var m_window = new MainWindow();

		var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);

		uint dpi = Win32.GetDpiForWindow(hWnd);
		float scalingFactor = (float)dpi / 96;

		// Desired logical size (at 100% scaling)
		int desiredLogicalWidth = 1342;
		int desiredLogicalHeight = 860;

		int desiredWidthPx = (int)(desiredLogicalWidth * scalingFactor);
		int desiredHeightPx = (int)(desiredLogicalHeight * scalingFactor);

		// Query monitor work area (excludes taskbar) and cap size so window always fits
		var hMonitor = Win32.MonitorFromWindow(hWnd, MonitorFromWindowFlags.MONITOR_DEFAULTTONEAREST);
		var mi = new MONITORINFO() { cbSize = Marshal.SizeOf<MONITORINFO>() };

		if (Win32.GetMonitorInfo(hMonitor, ref mi)) {
			int workWidth = (int)((mi.rcWork.right - mi.rcWork.left) * 0.95);
			int workHeight = (int)((mi.rcWork.bottom - mi.rcWork.top) * 0.95);

			int width = Math.Min(desiredWidthPx, workWidth);
			int height = Math.Min(desiredHeightPx, workHeight);

			m_window.AppWindow.Resize(new() { Width = width, Height = height });
		} else {
			// fallback to desired size if monitor info unavailable
			m_window.AppWindow.Resize(new() { Width = desiredWidthPx, Height = desiredHeightPx });
		}

		m_window.Activate();
	}
}
