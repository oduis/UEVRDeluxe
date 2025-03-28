using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace UEVRDeluxe.Pages;

static class PageHelpers {
	public static async Task RefreshDescriptionAsync(WebView2 webViewDescription, string descriptionMD, bool darkMode) {
		if (webViewDescription.CoreWebView2 == null) await webViewDescription.EnsureCoreWebView2Async(MainWindow.WebViewEnv);

		string html;
		if (!string.IsNullOrWhiteSpace(descriptionMD))
			html = Markdig.Markdown.ToHtml(descriptionMD);
		else
			html = "<p>( no profile description found )</p>";

		string bodyStyle = darkMode ? "background-color: black; color: white;" : "background-color: white; color: black;";
		string linkColor = darkMode ? "lightblue" : "blue";

		html = """
			<html><head><style>
			h1 { margin-bottom: 4px; font-size: 22px; font-weight: bold; }
			h2 { margin-bottom: 2px; font-size: 22px; font-weight: normal; }
			h3 { margin-bottom: 0px; font-size: 16px; font-weight: normal; }
			ul, ol { margin-block-start: 0.5rem; margin-block-end: 0.5rem; padding-inline-start: 24px; }
			p { margin: 0 0 2px 0; }
			a { color: 
			""" + linkColor
			+ "; }</style></head><body style=\"font-family: 'Segoe UI'; font-size: 12pt; margin:0; padding:0;"
			+ bodyStyle + "\">" + html + "</body></html>";

		webViewDescription.NavigateToString(html);
	}
}
