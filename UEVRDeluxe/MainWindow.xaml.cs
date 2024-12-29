#region Usings
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using System;
using System.Reflection;
using System.Threading.Tasks;
using UEVRDeluxe.Pages;
#endregion

namespace UEVRDeluxe;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window {
	public static CoreWebView2Environment webViewEnv;

	public MainWindow() {
		this.InitializeComponent();
		InitializeBrowser();

		var version = Assembly.GetExecutingAssembly().GetName().Version;
		tbCaption.Text = $"Unreal VR Deluxe {version}";

		var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
		var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
		var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

		if (appWindow != null) {
			appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
			appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
			appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
		}

		mainFrame.Navigate(typeof(MainPage));
	}

	/// <summary>Trick to make it work if installed in Program Files folder, where user has no access rights</summary>
	async Task InitializeBrowser() {
		var userDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\UEVRDeluxe\\BrowserCache";
		webViewEnv = await CoreWebView2Environment.CreateWithOptionsAsync(null, userDataFolder, new CoreWebView2EnvironmentOptions());
	}
}
