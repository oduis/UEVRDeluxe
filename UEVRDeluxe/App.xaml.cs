#region Usings
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices; 
#endregion

namespace UEVRDeluxe;

/// <summary>Provides application-specific behavior to supplement the default Application class.</summary>
public partial class App : Application {
	[DllImport("User32.dll")]
	static extern uint GetDpiForWindow(IntPtr hwnd);

	public App() { this.InitializeComponent(); }

	/// <summary>Invoked when the application is launched.</summary>
	/// <param name="args">Details about the launch request and process.</param>
	protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
		var m_window = new MainWindow();

		var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);

		uint dpi = GetDpiForWindow(hWnd);
		float scalingFactor = (float)dpi / 96;
        int width = (int)(1024 * scalingFactor);
        int height = (int)(768 * scalingFactor);

        m_window.AppWindow.Resize(new() { Width = width, Height = height });

		m_window.Activate();
	}
}
