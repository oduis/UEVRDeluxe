#region Usings
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using UEVRDeluxe.Common;
#endregion

namespace UEVRDeluxeFunc;

public abstract class FunctionsBase {
	protected readonly ILogger logger;
	protected readonly IConfiguration config;

	protected FunctionsBase(ILoggerFactory loggerFactory, IConfiguration config) {
		this.config = config;
		logger = loggerFactory.CreateLogger<FunctionsBase>();
	}

	#region * Blob Handling
	/// <summary>Subdirectory name that contains the profile</summary>
	string BLOB_PROFILE_CONTAINER_NAME = "uevrprofiles";

	protected async Task<BlobContainerClient> CreateOpenBlobsContainerAsync() {
		var container = new BlobContainerClient(config["StorageConnectString"], BLOB_PROFILE_CONTAINER_NAME);
#if DEBUG
		// Kostet Zugriffe, nur für Developer zum Emulator
		await container.CreateIfNotExistsAsync();
#else
		await Task.CompletedTask;  // keine Warnings
#endif
		return container;
	}
	#endregion

	#region * Table Handling
	const string TABLE_NAME = "uevrprofiles";

	protected async Task<TableClient> CreateOpenTableAsync() {
		var table = new TableClient(config["StorageConnectString"], TABLE_NAME);
#if DEBUG
		// Kostet Zugriffe, nur für Developer zum Emulator
		await table.CreateIfNotExistsAsync();
#else
		await Task.CompletedTask;  // keine Warnings
#endif
		return table;
	}

	protected async Task<List<ProfileMeta>> ReadProfilesAsync(TableClient table, string exeName) {
		if (string.IsNullOrWhiteSpace(exeName)) throw new ArgumentNullException(nameof(exeName));
		var query = table.QueryAsync<TableEntity>(q => q.PartitionKey == exeName);

		var profiles = new List<ProfileMeta>();
		await foreach (TableEntity item in query) {
			profiles.Add(new() {
				ID = Guid.Parse(item.RowKey),
				EXEName = item.PartitionKey,
				AuthorName = item[nameof(ProfileMeta.AuthorName)] as string,
				GameName = item[nameof(ProfileMeta.GameName)] as string,
				GameVersion = item[nameof(ProfileMeta.GameVersion)] as string,
				ModifiedDate = DateTime.ParseExact(item[nameof(ProfileMeta.ModifiedDate)] as string, "yyyyMMdd", CultureInfo.InvariantCulture),
				Remarks = item[nameof(ProfileMeta.Remarks)] as string,
				MinEVRVersionDate = DateTime.ParseExact(item[nameof(ProfileMeta.MinEVRVersionDate)] as string, "yyyyMMdd", CultureInfo.InvariantCulture)
			});
		}

		logger.LogInformation($"Found {profiles.Count} profiles for {exeName}");

		return profiles;
	}
	#endregion

	#region * Helpers
	protected void CheckHttpRequest(HttpRequestData req) {
		// Make sure that no other client misuses it
		if (!req.Headers.Any(h => h.Key == "User-Agent" && h.Value.FirstOrDefault() == AzConstants.AGENT_NAME))
			throw new UnauthorizedAccessException();
	}
	#endregion
}
