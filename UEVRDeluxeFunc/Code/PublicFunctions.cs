#region Usings
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeMapping;
using System.IO.Compression;
using UEVRDeluxe.Common;
#endregion

namespace UEVRDeluxeFunc;

public class PublicFunctions : FunctionsBase {
	public PublicFunctions(ILoggerFactory log, IConfiguration config) : base(log, config) { }

	[Function("SearchProfile")]
	public async Task<HttpResponseData> RunSearchProfileAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles/{exeName}")] HttpRequestData req, string exeName) {
		HttpResponseData resp;

		try {
			logger.LogInformation($"SearchProfile({exeName})");

			CheckHttpRequest(req);

			if (string.IsNullOrWhiteSpace(exeName)) throw new ApplicationException("exeName parameter is required.");

			var tableClient = await CreateOpenTableAsync();
			var result = await ReadProfilesAsync(tableClient, exeName);

			resp = await HttpDataHelpers.CreateOKJsonResponseAsync(req, result, req.Query.AllKeys.Contains(AzConstants.QUERYSTRING_NOCACHE) ? 0 : 15);
		} catch (Exception ex) {
			resp = await HttpDataHelpers.CreateLogExceptionResponseAsync(logger, req, ex);
		}

		return resp;
	}

	[Function("DownloadProfile")]
	public async Task<HttpResponseData> RunDownloadProfileAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles/{exeName}/{id}")] HttpRequestData req, string exeName, Guid id) {
		HttpResponseData resp;

		try {
			logger.LogInformation($"DownloadProfile({exeName},{id:n})");

			CheckHttpRequest(req);

			if (string.IsNullOrWhiteSpace(exeName)) throw new ApplicationException("exeName parameter is required.");
			if (id == Guid.Empty) throw new ApplicationException("ID parameter is required.");

			var blobContainerClient = await CreateOpenBlobsContainerAsync();
			var blobClient = blobContainerClient.GetBlobClient(AzConstants.GetProfileFileName(id, exeName));

			var blobDownloadInfo = await blobClient.DownloadAsync();
			resp = HttpDataHelpers.CreateOKResultReponse(req, 24 * 60, KnownMimeTypes.Zip);
			await blobDownloadInfo.Value.Content.CopyToAsync(resp.Body);
		} catch (Exception ex) {
			resp = await HttpDataHelpers.CreateLogExceptionResponseAsync(logger, req, ex);
		}

		return resp;
	}

	[Function("DownloadProfileDescription")]
	public async Task<HttpResponseData> RunDownloadProfileDescriptionAsync([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "profiles/{exeName}/{id}/description")] HttpRequestData req, string exeName, Guid id) {
		HttpResponseData resp;

		try {
			logger.LogInformation($"DownloadProfileDescription({exeName},{id:n})");

			CheckHttpRequest(req);

			if (string.IsNullOrWhiteSpace(exeName)) throw new ApplicationException("exeName parameter is required.");
			if (id == Guid.Empty) throw new ApplicationException("ID parameter is required.");

			var blobContainerClient = await CreateOpenBlobsContainerAsync();
			var blobClient = blobContainerClient.GetBlobClient(AzConstants.GetProfileFileName(id, exeName));

			// Minimal download to get the description file
			var blobStream = await blobClient.DownloadStreamingAsync();

			using (var archive = new ZipArchive(blobStream.Value.Content, ZipArchiveMode.Read)) {
				var descrEntry = archive.GetEntry(ProfileMeta.DESCRIPTION_FILENAME);
				if (descrEntry == null) throw new ApplicationException($"{ProfileMeta.DESCRIPTION_FILENAME} not found in the zip file.");

				byte[] result;
				using (var profileStream = descrEntry.Open()) {
					using var mem = new MemoryStream();
					using (var descrOnlyArchive = new ZipArchive(mem, ZipArchiveMode.Create)) {
						var entry = descrOnlyArchive.CreateEntry(ProfileMeta.DESCRIPTION_FILENAME, CompressionLevel.SmallestSize);
						using var entryStream = entry.Open();
						await profileStream.CopyToAsync(entryStream);
					}
					result = mem.ToArray();
				}

				resp = HttpDataHelpers.CreateOKResultReponse(req, 24 * 60, KnownMimeTypes.Zip);
				await resp.Body.WriteAsync(result);
			}
		} catch (Exception ex) {
			resp = await HttpDataHelpers.CreateLogExceptionResponseAsync(logger, req, ex);
		}

		return resp;
	}
}