#region Usings
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using NReco.Logging.File;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UEVRDeluxe.Code;
using UEVRDeluxe.Pages;
#endregion

namespace UEVRDeluxe;

/// <summary>Main application Window</summary>
public sealed partial class MainWindow : Window {
	public static CoreWebView2Environment WebViewEnv;

	/// <summary>Difficult to find in WinUI3</summary>
	public static nint hWnd { get; private set; }

	public MainWindow() {
		InitializeComponent();
		this.Closed += MainWindow_Closed;

		Logger.Startup();

		_ = InitializeBrowser();

		var version = Assembly.GetExecutingAssembly().GetName().Version;
		tbCaption.Text = $"Unreal VR Easy Injector {version}";

		hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
		var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
		var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

		if (appWindow != null) {
			appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
			appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
			appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
		}

		InitGlobalShortcut();

		mainFrame.Navigate(typeof(MainPage));
	}

	void MainWindow_Closed(object sender, WindowEventArgs args) => Logger.Shutdown();

	/// <summary>Trick to make it work if installed in Program Files folder, where user has no access rights</summary>
	async Task InitializeBrowser() {
		try {
			string userDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\UEVRDeluxe\\BrowserCache";
			WebViewEnv = await CoreWebView2Environment.CreateWithOptionsAsync(null, userDataFolder, new CoreWebView2EnvironmentOptions());
		} catch (Exception ex) {
			Logger.Log.LogCritical(ex, "InitBrowser failed");
		}
	}

	#region * Hotkey
	/// <summary>If this is set, a Hotkey was pressed. Can be waited on in pages.</summary>
	public static ManualResetEventSlim HotkeyEvent = new(false);

	/// <summary>See https://learn.microsoft.com/de-de/windows/win32/inputdev/virtual-key-codes</summary>
	const int VK_U = 0x55;

	const int MOD_ALT = 0x0001;
	const int MOD_CONTROL = 0x0002;

	const uint WM_HOTKEY = 0x0312;
	static Win32.WNDPROC newWndProc;
	static IntPtr oldWndProc;

	unsafe void InitGlobalShortcut() {
		bool success = Win32.RegisterHotKey(hWnd, 0, MOD_ALT | MOD_CONTROL, VK_U);
		if (!success) {
			Logger.Log.LogError("Failed to register hotkey");
			return;
		}

		newWndProc = WndProc;
		var hotKeyPrcPointer = Marshal.GetFunctionPointerForDelegate(newWndProc);
		oldWndProc = Win32.SetWindowLongPtr(hWnd, WindowLongIndexFlags.GWL_WNDPROC, hotKeyPrcPointer);
	}

	IntPtr WndProc(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam) {
		if (uMsg == WM_HOTKEY) {
			HotkeyEvent.Set();
			return IntPtr.Zero;
		}

		return Win32.CallWindowProc(oldWndProc, hwnd, uMsg, wParam, lParam);
	}
	#endregion
}
