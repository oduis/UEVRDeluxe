#region Usings
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using MimeMapping;
using System.Net;
using System.Security;
using System.Text.Json;
#endregion

/// <summary>Helper functions to generate HttpResponses that are easily cacheable by Azure Front Door.</summary>
public static class HttpDataHelpers {
	public static HttpResponseData CreateOKResultReponse(HttpRequestData req, int? cacheMinutes = null, string contentType = null) {
		HttpResponseData resp = req.CreateResponse(HttpStatusCode.OK);

		if (contentType != null) resp.Headers.Add("Content-Type", contentType);

		if (cacheMinutes.HasValue) {
			if (cacheMinutes.Value > 0) {
				int seconds = cacheMinutes.Value * 60;
				resp.Headers.Add("cache-control", $"public, max-age={seconds}, s-maxage={seconds}, stale-while-revalidate=30");
			}
		} else if (string.Equals(req.Method, HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase)) {
			resp.Headers.Add("cache-control", "no-cache");
		}

		return resp;
	}

	public static async Task<HttpResponseData> CreateOKJsonResponseAsync<T>(HttpRequestData req, T obj, int? cacheMinutes = null) {
		var resp = CreateOKResultReponse(req, cacheMinutes, KnownMimeTypes.Json);

		// To allow Azure Front Door to compress, we need a Content-Length header.
		// Therefore, DO NOT use the built-in functions
		using (var mem = new MemoryStream()) {
			JsonSerializer.Serialize(mem, obj);

			resp.Headers.Add("Content-length", mem.Length.ToString());

			mem.Seek(0, SeekOrigin.Begin);
			await mem.CopyToAsync(resp.Body);
		}

		return resp;
	}

	/// <summary>Bei Fehlern passende HttpResponseData generieren und wegloggen</summary>
	public static async Task<HttpResponseData> CreateLogExceptionResponseAsync(ILogger logger, HttpRequestData req, Exception ex) {
		logger.LogError(ex, ex.Message);

		string resultText = null;
		var statusCode = HttpStatusCode.InternalServerError;
		if (ex is SecurityException) statusCode = HttpStatusCode.Forbidden;
		if (ex is ApplicationException) {
			statusCode = HttpStatusCode.BadRequest;
			resultText = ex.Message;
		}

		var resp = req.CreateResponse(statusCode);
		if (resultText != null) await resp.WriteStringAsync(resultText);

		return resp;
	}
}
