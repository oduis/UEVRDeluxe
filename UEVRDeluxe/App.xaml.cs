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
	protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) {
		var m_window = new MainWindow();

		var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);

		uint dpi = Win32.GetDpiForWindow(hWnd);
		float scalingFactor = (float)dpi / 96;
        int width = (int)(1080 * scalingFactor);
        int height = (int)(780 * scalingFactor);

        m_window.AppWindow.Resize(new() { Width = width, Height = height });

		m_window.Activate();
	}
}
