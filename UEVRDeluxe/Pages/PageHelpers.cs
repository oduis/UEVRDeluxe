using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace UEVRDeluxe.Pages;

static class PageHelpers {
	public static async Task RefreshDescriptionAsync(WebView2 webViewDescription, string descriptionMD) {
		if (webViewDescription.CoreWebView2 == null) await webViewDescription.EnsureCoreWebView2Async(MainWindow.webViewEnv);

		string html;
		if (!string.IsNullOrWhiteSpace(descriptionMD))
			html = Markdig.Markdown.ToHtml(descriptionMD);
		else
			html = "<p>( no profile description found )</p>";

		html = """
			<html><head><style>
			h1 { margin-bottom: 4px; font-size: 22px; font-weight: bold; }
			h2 { margin-bottom: 2px; font-size: 22px; font-weight: normal; }
			h3 { margin-bottom: 0px; font-size: 14px; font-weight: normal; }
			ul, ol { margin-block-start: 0.5rem; margin-block-end: 0.5rem; padding-inline-start: 24px; }
			p { margin: 0 0 2px 0; }
			</style></head><body style="font-family: 'Segoe UI'; font-size: 12pt; margin:0; padding:0;">
			""" + html + "</body></html>";

		webViewDescription.NavigateToString(html);
	}
}
