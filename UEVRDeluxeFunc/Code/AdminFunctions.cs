#region Usings
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UEVRDeluxe.Common;
using Azure.Data.Tables;
using Azure;
using Microsoft.Azure.Functions.Worker.Http;
#endregion

namespace UEVRDeluxeFunc;

public class AdminFunctions : FunctionsBase {
	public AdminFunctions(ILoggerFactory log, IConfiguration config) : base(log, config) { }

	#region Upload
	/// <summary>Upload a new profile zip file</summary>
	/// <returns>List of ALL profiles including the new one (saving another call)</returns>
	[Function("UploadProfile")]
	public async Task<HttpResponseData> RunUploadProfileAsync(
		[HttpTrigger(AuthorizationLevel.Function, "post", Route = "profiles")] HttpRequestData req) {
		HttpResponseData resp;

		try {
			logger.LogInformation($"UploadProfile()");

			CheckHttpRequest(req);

			// Read the zip file from the request
			using var stream = new MemoryStream();
			await req.Body.CopyToAsync(stream);
			if (stream.Length > AzConstants.MAX_PROFILE_ZIP_SIZE)
				throw new ApplicationException("File size exceeds the size. Contains logs?");

			stream.Position = 0;

			// Unpack the zip file and get Profile.json
			ProfileMeta profileMeta;
			using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true)) {
				if (archive.GetEntry("config.txt") == null) throw new ApplicationException("config.txt not found in the zip file.");
				if (archive.GetEntry("log.txt") != null) throw new ApplicationException("Zip file may not contain logs.");

				var profileEntry = archive.GetEntry(ProfileMeta.FILENAME);
				if (profileEntry == null) throw new ApplicationException($"{ProfileMeta.FILENAME} not found in the zip file.");

				using var profileStream = profileEntry.Open();
				profileMeta = JsonSerializer.Deserialize<ProfileMeta>(profileStream);
				profileMeta.ID = Guid.NewGuid();
			}

			string errMsg = profileMeta.Check();
			if (errMsg != null) throw new ApplicationException(errMsg);

			// Open first. Is something fails, than at the beginning
			var blobContainerClient = await CreateOpenBlobsContainerAsync();
			var tableClient = await CreateOpenTableAsync();

			// Write the whole posted zip with a new name to Azure Blob storage
			var blobClient = blobContainerClient.GetBlobClient(AzConstants.GetProfileFileName(profileMeta.ID, profileMeta.EXEName));
			stream.Position = 0;
			await blobClient.UploadAsync(stream);

			// Write the whole posted zip with a new name to Azure Table store
			var entity = new TableEntity(profileMeta.EXEName, profileMeta.ID.ToString("n")) {
				{ nameof(ProfileMeta.GameName), profileMeta.GameName },
				{ nameof(ProfileMeta.GameVersion), profileMeta.GameVersion },
				{ nameof(ProfileMeta.ModifiedDate), profileMeta.ModifiedDate.ToString("yyyyMMdd") },
				{nameof(ProfileMeta.AuthorName), profileMeta.AuthorName },
				{nameof(ProfileMeta.MinEVRVersionDate), profileMeta.MinEVRVersionDate.ToString("yyyyMMdd") },
				{nameof(ProfileMeta.Remarks), profileMeta.Remarks }
			};

			await tableClient.AddEntityAsync(entity);
			logger.LogInformation($"Uploaded profile {profileMeta.EXEName} {profileMeta.ID}");

			var result = await ReadProfilesAsync(tableClient, profileMeta.EXEName);
			resp = await HttpDataHelpers.CreateOKJsonResponseAsync(req, result);

		} catch (Exception ex) {
			resp = await HttpDataHelpers.CreateLogExceptionResponseAsync(logger, req, ex);
		}

		return resp;
	}
	#endregion

	#region Delete
	/// <summary>Delete a profile by ID</summary>
	[Function("DeleteProfile")]
	public async Task<HttpResponseData> RunDeleteProfileAsync(
		[HttpTrigger(AuthorizationLevel.Function, "delete", Route = "profiles/{exeName}/{id}")] HttpRequestData req, string id, string exeName) {

		HttpResponseData resp;

		try {
			logger.LogInformation($"DeleteProfile({exeName},{id:n})");

			CheckHttpRequest(req);

			if (!Guid.TryParse(id, out Guid profileId))
				throw new ApplicationException("Invalid profile ID format.");

			var blobContainerClient = await CreateOpenBlobsContainerAsync();
			var tableClient = await CreateOpenTableAsync();

			// Delete the table entity
			try {
				await tableClient.DeleteEntityAsync(exeName, profileId.ToString("n"));
			} catch (RequestFailedException ex) when (ex.Status == 404) {
				logger.LogWarning($"Table entity for profile {id} not found.");
				// No error, so we can clean half deleted profiles
			} catch (Exception ex) {
				logger.LogError($"Failed to delete table entity for profile {id}: {ex.Message}");
			}

			// Delete the blob
			var blobClient = blobContainerClient.GetBlobClient(AzConstants.GetProfileFileName(profileId, exeName));

			bool deleted = await blobClient.DeleteIfExistsAsync();
			if (!deleted) throw new ApplicationException("Profile not found");

			logger.LogInformation($"Deleted profile {id}");

			resp = HttpDataHelpers.CreateOKResultReponse(req);

		} catch (Exception ex) {
			resp = await HttpDataHelpers.CreateLogExceptionResponseAsync(logger, req, ex);
		}

		return resp;
	}
	#endregion
}
