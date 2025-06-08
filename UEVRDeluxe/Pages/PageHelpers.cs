#region Usings
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks; 
#endregion

namespace UEVRDeluxe.Pages;

static class PageHelpers {
	public static async Task RefreshDescriptionAsync(WebView2 webViewDescription, string descriptionMD, bool darkMode) {
		if (webViewDescription.CoreWebView2 == null) await webViewDescription.EnsureCoreWebView2Async(MainWindow.WebViewEnv);

		string html;
		if (!string.IsNullOrWhiteSpace(descriptionMD)) {
			var pipeline = new MarkdownPipelineBuilder()
				.UseAdvancedExtensions()
				.Use(new LinkTargetExtension())
				.Build();

			html = Markdown.ToHtml(descriptionMD, pipeline);
		} else
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

	/// <summary>Custom extension to add target="_blank" to all links (except inline images)</summary>
	class LinkTargetExtension : IMarkdownExtension {
		public void Setup(MarkdownPipelineBuilder pipeline) { }

		public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer) {
			if (renderer is HtmlRenderer htmlRenderer) {
				var linkRenderer = htmlRenderer.ObjectRenderers.FindExact<LinkInlineRenderer>();
				if (linkRenderer != null) linkRenderer.TryWriters.Add(TryWriteLinkWithTargetBlank);
			}
		}

		bool TryWriteLinkWithTargetBlank(HtmlRenderer renderer, LinkInline link) {
			if (!link.IsImage) {
				renderer.Write("<a href=\"").Write(link.GetDynamicUrl != null ? link.GetDynamicUrl() ?? string.Empty : link.Url ?? string.Empty)
					.Write("\" target=\"_blank\"");
				if (!string.IsNullOrEmpty(link.Title))
					renderer.Write(" title=\"").Write(link.Title).Write("\"");
				renderer.Write(">");
				renderer.WriteChildren(link);
				renderer.Write("</a>");
				return true;
			}
			return false;
		}
	}
}
