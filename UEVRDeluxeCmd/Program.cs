#region Usings
using System.IO.Compression;
using System.Text.RegularExpressions;
#endregion

namespace UEVRDeluxeCmd;

/// <summary>Helper Exe that is called as Admin by the UEVRDeluxe application</summary>
class Program {
	#region Main
	static async Task<int> Main(string[] args) {
		int resultCode = 0;
		string resultFilePathPath = null;

		try {
			if (args.Length < 2) throw new Exception("Too few arguments");
			resultFilePathPath = args[0];

			switch (args[1].ToUpperInvariant()) {
				case "UPDATEBACKEND":
					await UpdateBackendAsync(int.Parse(args[2]));
					break;

				default:
					throw new Exception($"Unknown command: {args[1]}");
			}

			Console.WriteLine("Operation completed successfully");
		} catch (Exception ex) {
			Console.Error.WriteLine(ex.Message);

			try {
				File.WriteAllText(@"C:\temp\err.txt", ex.Message);
				if (resultFilePathPath != null) File.WriteAllText(resultFilePathPath, ex.Message);
			} catch { }

			resultCode = 0xff;
		}

		return resultCode;
	} 
	#endregion


	#region UpdateBackend
	const string UEVR_SEARCH_NIGHTLY_URL = "https://github.com/praydog/UEVR-nightly/releases?q=Nightly+{0}&expanded=true";
	const string UEVR_VERSION_FILENAME = "UEVRLink.txt";

	/// <summary>Update UEVR backend from GitHub</summary>
	public static async Task UpdateBackendAsync(int nightlyNumber) {
		string zipUrl, sNightlyNumber, commitHash;

		string UEVRBaseDir = Path.Combine(AppContext.BaseDirectory, "..\\UEVR");
		string VersionFilePath = Path.Combine(UEVRBaseDir, UEVR_VERSION_FILENAME);

		byte[] zipData;
		using (var client = new HttpClient()) {
			sNightlyNumber = nightlyNumber.ToString("D5");
			Console.WriteLine($"Checking for UEVR nightly {sNightlyNumber}");

			string searchUrl = string.Format(UEVR_SEARCH_NIGHTLY_URL, sNightlyNumber);

			string html = await client.GetStringAsync(searchUrl);

			// e.g. <a href="/praydog/UEVR-nightly/releases/tag/nightly-01095-69fd6801eec8f9ede3c6667302b1740268b89c50" data-view-component="true" class="Link--primary Link" ...
			var match = Regex.Match(html, $"releases/tag/nightly-{sNightlyNumber}-([0-9a-f]+)");
			if (!match.Success) throw new Exception($"Could not find nightly version {sNightlyNumber} on GitHub");
			commitHash = match.Groups[1].Value;

			zipUrl = $"https://github.com/praydog/UEVR-nightly/releases/download/nightly-{sNightlyNumber}-{commitHash}/uevr.zip";

			if (File.Exists(VersionFilePath) && string.Equals(File.ReadAllText(VersionFilePath).Trim(), zipUrl)) {
				Console.WriteLine("UEVR backend is already up to date");
				return;
			}

			zipData = await client.GetByteArrayAsync(zipUrl);
		}

		using (var zipStream = new MemoryStream(zipData))
		using (var archive = new ZipArchive(zipStream)) {
			Directory.CreateDirectory(UEVRBaseDir);
			foreach (var entry in archive.Entries) {
				if (entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
					string destinationPath = Path.Combine(UEVRBaseDir, entry.Name);
					entry.ExtractToFile(destinationPath, true);

					File.SetLastAccessTimeUtc(destinationPath, entry.LastWriteTime.UtcDateTime);
				}
			}
		}

		File.WriteAllText(VersionFilePath, zipUrl);
	} 
	#endregion
}
