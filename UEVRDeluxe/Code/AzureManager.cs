#region Usings
using Microsoft.Extensions.Http;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using UEVRDeluxe.Common;
#endregion

namespace UEVRDeluxe.Code;

public static class AzureManager {
	static HttpClient httpFunctionClient;

	// Since the profiles are small, not eviction necessary
	static Dictionary<string, object> memCache = new();

	/// <summary>Admin have this filled in environment variables. Set as passkey in Azure functions</summary>
	public static string GetCloudAdminPasskey() => Environment.GetEnvironmentVariable("UEVRDeluxeCloudAdminPasskey");

	static HttpClient GetHttpClient() {
		if (httpFunctionClient == null) {
			httpFunctionClient = new();

			// Add Polly policy. Especially for shut down Azure Functions
			var retryPolicy = HttpPolicyExtensions
				.HandleTransientHttpError()
				.OrResult(msg => msg.StatusCode == HttpStatusCode.BadGateway || msg.StatusCode == HttpStatusCode.ServiceUnavailable)
				.WaitAndRetryAsync(1, retryAttempt => TimeSpan.FromSeconds(0.4));

			httpFunctionClient = new HttpClient(new PolicyHttpMessageHandler(retryPolicy) { InnerHandler = new HttpClientHandler() });

			httpFunctionClient.BaseAddress = new Uri(CompiledSecret.FUNCTION_BASE_ADDRESS);

			// Add the headers
			httpFunctionClient.DefaultRequestHeaders.Add("Accept", "application/json");
			httpFunctionClient.DefaultRequestHeaders.Add("User-Agent", "UEVRDeluxe");
		}

		return httpFunctionClient;
	}

	public static async Task<List<ProfileMeta>> UploadProfileAsync(byte[] zipData) {
		var client = GetHttpClient();
		var content = new ByteArrayContent(zipData);
		content.Headers.ContentType = MediaTypeHeaderValue.Parse(MimeMapping.KnownMimeTypes.Zip);

		var resp = await client.PostAsync($"profiles?code={GetCloudAdminPasskey()}", content);
		if (!resp.IsSuccessStatusCode) throw new Exception($"Failed to upload profile ({(int)resp.StatusCode})");

		return await resp.Content.ReadFromJsonAsync<List<ProfileMeta>>();
	}

	public static async Task<IEnumerable<ProfileMeta>> SearchProfilesAsync(string exeName, bool includeEnvironments, bool nocache = false) {
		string cacheKey = $"Search_{exeName}";

		if (!nocache && memCache.TryGetValue(cacheKey, out object cached)) return (List<ProfileMeta>)cached;

		var client = GetHttpClient();

		var resp = await client.GetAsync($"profiles/{exeName}?code={GetCloudAdminPasskey()}"
			+ (includeEnvironments ? $"&{AzConstants.QUERYSTRING_INCLUDEENVIRONMENTS}={includeEnvironments}" : "")
			+ (nocache ? $"&{AzConstants.QUERYSTRING_NOCACHE}=1" : ""));
		if (!resp.IsSuccessStatusCode) throw new Exception($"Failed to search profile ({(int)resp.StatusCode})");

		var result = await resp.Content.ReadFromJsonAsync<List<ProfileMeta>>();
		memCache[cacheKey] = result;

		return result.OrderBy(p => p.GameName).ThenByDescending(p => p.ModifiedDate);
	}

	public static async Task<string> GetAllProfileNamesAsync(bool nocache = false) {
		string cacheKey = "GetAllProfileNames";

		if (!nocache && memCache.TryGetValue(cacheKey, out object cached)) return (string)cached;

		var client = GetHttpClient();

		var resp = await client.GetAsync("allprofilenames");
		if (!resp.IsSuccessStatusCode) throw new Exception($"Failed to search all profile names ({(int)resp.StatusCode})");

		var result = await resp.Content.ReadAsStringAsync();
		memCache[cacheKey] = result;

		return result;
	}

	public static async Task<byte[]> DownloadProfileZipAsync(string exeName, Guid profileID) {
		string cacheKey = $"Download_{exeName}_{profileID:n}";
		if (memCache.TryGetValue(cacheKey, out object cached)) return (byte[])cached;

		var client = GetHttpClient();

		// profiles are immuatable. NOCACHE makes no sense here
		var resp = await client.GetAsync($"profiles/{exeName}/{profileID:n}");
		if (!resp.IsSuccessStatusCode) throw new Exception($"Failed to download profile ({(int)resp.StatusCode})");

		var result = await resp.Content.ReadAsByteArrayAsync();
		memCache[cacheKey] = result;

		return result;
	}

	public static async Task<string> DownloadProfileDescriptionAsync(string exeName, Guid profileID) {
		string cacheKey = $"DownloadDescription_{exeName}_{profileID:n}";
		if (memCache.TryGetValue(cacheKey, out object cached)) return (string)cached;

		var client = GetHttpClient();

		// profiles are immuatable
		var resp = await client.GetAsync($"profiles/{exeName}/{profileID:n}/description");
		if (!resp.IsSuccessStatusCode) throw new Exception($"Failed to download profile description ({(int)resp.StatusCode})");

		string result;
		using (var archive = new ZipArchive(resp.Content.ReadAsStream(), ZipArchiveMode.Read)) {
			var descrEntry = archive.GetEntry(ProfileMeta.DESCRIPTION_FILENAME);
			using (var rdr = new StreamReader(descrEntry.Open())) result = await rdr.ReadToEndAsync();
		}

		memCache[cacheKey] = result;

		return result;
	}

	public static async Task DeleteProfileAsync(string exeName, Guid profileID) {
		var client = GetHttpClient();

		var resp = await client.DeleteAsync($"profiles/{exeName}/{profileID:n}?code={GetCloudAdminPasskey()}");
		if (!resp.IsSuccessStatusCode) throw new Exception($"Failed to delete profile ({(int)resp.StatusCode})");
	}
}
