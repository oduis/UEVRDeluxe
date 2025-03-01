#region Usings
using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Net.Http;
using System.IO.Compression;
using HtmlAgilityPack;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
#endregion

namespace UEVRDeluxe.Code;

class Injector {
	const string UEVR_NIGHTLY_URL = "https://github.com/praydog/UEVR-nightly/releases/latest";

	/// <summary>Contains the URL of the version successfully downloaded</summary>
	const string UEVR_VERSION_FILENAME = "UEVRLink.txt";

	/// <summary>Inject the DLL into the target process</summary>
	/// <param name="dllName">local filename</param>
	public static bool InjectDll(int processID, string dllName, out nint dllBase) {
		var fullPath = Path.Combine(UEVRBaseDir, dllName);

		if (!File.Exists(fullPath))
			throw new Exception($"{dllName} does not appear to exist! Check if any anti-virus software has deleted the file. Reinstall UEVR if necessary.\n\nBaseDirectory: {AppContext.BaseDirectory}");

		dllBase = nint.Zero;

		fullPath = Path.GetFullPath(fullPath);

		// Open the target process with the necessary access
		nint processHandle = Win32.OpenProcess(0x1F0FFF, false, processID);

		if (processHandle == nint.Zero) throw new Exception("Access error. You must start UEVR Deluxe as administrator.");

		// Get the address of the LoadLibrary function
		nint loadLibraryAddress = Win32.GetProcAddress(Win32.GetModuleHandle("kernel32.dll"), "LoadLibraryW");
		if (loadLibraryAddress == nint.Zero) throw new Exception("Could not obtain LoadLibraryW address in the target process.");

		// Allocate memory in the target process for the DLL path
		nint dllPathAddress = Win32.VirtualAllocEx(processHandle, nint.Zero, (uint)fullPath.Length, 0x1000, 0x40);
		if (dllPathAddress == nint.Zero) throw new Exception("Failed to allocate memory in the target process.");

		// Write the DLL path in UTF-16
		int bytesWritten = 0;
		var bytes = Encoding.Unicode.GetBytes(fullPath);
		Win32.WriteProcessMemory(processHandle, dllPathAddress, bytes, (uint)(fullPath.Length * 2), out bytesWritten);

		// Create a remote thread in the target process that calls LoadLibrary with the DLL path
		nint threadHandle = Win32.CreateRemoteThread(processHandle, nint.Zero, 0, loadLibraryAddress, dllPathAddress, 0, nint.Zero);

		if (threadHandle == nint.Zero) throw new Exception("Failed to create remote thread in the target processs.");

		Win32.WaitForSingleObject(threadHandle, 1000);

		var p = Process.GetProcessById(processID);

		// Get base of DLL that was just injected
		if (p != null)
			try {
				foreach (ProcessModule module in p.Modules) {
					if (module.FileName != null && module.FileName == fullPath) {
						dllBase = module.BaseAddress;
						break;
					}
				}
			} catch (Exception ex) {
				throw new Exception($"Exception while injecting: {ex.Message}");
			}

		return true;
	}

	public static bool InjectDll(int processId, string dllName) {
		nint dummy;
		return InjectDll(processId, dllName, out dummy);
	}

	public static bool CallFunctionNoArgs(int processId, string dllName, nint dllBase, string functionName, bool wait = false) {
		nint processHandle = Win32.OpenProcess(0x1F0FFF, false, processId);

		if (processHandle == nint.Zero)
			throw new Exception("Could not open a handle to the target process.\nYou may need to start this program as an administrator, or the process may be protected.");

		// We need to load the DLL into our own process temporarily as a workaround for GetProcAddress not working with remote DLLs
		nint localDllHandle = Win32.LoadLibrary(Path.Combine(UEVRBaseDir, dllName));

		if (localDllHandle == nint.Zero) throw new Exception("Could not load the target DLL into our own process.");

		nint localVa = Win32.GetProcAddress(localDllHandle, functionName);

		if (localVa == nint.Zero) throw new Exception("Could not obtain " + functionName + " address in our own process.");

		nint rva = (nint)(localVa.ToInt64() - localDllHandle.ToInt64());
		nint functionAddress = (nint)(dllBase.ToInt64() + rva.ToInt64());

		// Create a remote thread in the target process that calls the function
		nint threadHandle = Win32.CreateRemoteThread(processHandle, nint.Zero, 0, functionAddress, nint.Zero, 0, nint.Zero);

		if (threadHandle == nint.Zero) throw new Exception("Failed to create remote thread in the target processs.");

		if (wait) {
			Win32.WaitForSingleObject(threadHandle, 2000);
		}

		return true;
	}

	public string GetBackendVersion() {
		string filePath = Path.Combine(UEVRBaseDir, "UEVRBackend.dll");
		if (!File.Exists(filePath)) return "UEVR backend not found (removed by Antivirus?)";
		return File.GetLastWriteTime(filePath).ToString("yyyy-MM-dd HH:mm");
	}

	/// <summary>Download latest UEVR nightly and install locally</summary>
	/// <returns>True if update was required, false if not.</returns>
	public static async Task<bool> UpdateBackendAsync() {
		if (!Win32.IsUserAnAdmin()) throw new Exception("Please run UEVR Easy Injector as an Administrator");

		string zipUrl;
		string versionFilePath = Path.Combine(UEVRBaseDir, UEVR_VERSION_FILENAME);

		byte[] zipData;
		using (var client = new HttpClient()) {
			string html = await client.GetStringAsync(UEVR_NIGHTLY_URL);
			var doc = new HtmlDocument();
			doc.LoadHtml(html);
			var title = doc.DocumentNode.SelectSingleNode("//title");
			// title is e.g. "Release UEVR Nightly 01036 (f97cc4ad910351521e8e2031f63bebc754673e26)"
			// Parse and convert to to link: https://github.com/praydog/UEVR-nightly/releases/download/nightly-01036-f97cc4ad910351521e8e2031f63bebc754673e26/uevr.zip

			var match = Regex.Match(title.InnerText, @"Release UEVR Nightly (\d+) \(([\da-f]+)\)");
			if (!match.Success) throw new Exception("Invalid release title format: {title}");

			string nightlyNumber = match.Groups[1].Value;
			string commitHash = match.Groups[2].Value;

			zipUrl = $"https://github.com/praydog/UEVR-nightly/releases/download/nightly-{nightlyNumber}-{commitHash}/uevr.zip";

			if (File.Exists(versionFilePath) && string.Equals(File.ReadAllText(versionFilePath).Trim(), zipUrl)) {
				return false;
			}

			zipData = await client.GetByteArrayAsync(zipUrl);
		}

		using (var zipStream = new MemoryStream(zipData))
		using (var archive = new ZipArchive(zipStream)) {
			foreach (var entry in archive.Entries) {
				if (entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) {
					string destinationPath = Path.Combine(UEVRBaseDir, entry.Name);
					entry.ExtractToFile(destinationPath, true);

					File.SetLastAccessTimeUtc(destinationPath, entry.LastWriteTime.UtcDateTime);
				}
			}
		}

		File.WriteAllText(versionFilePath, zipUrl);
		return true;
	}

	static string UEVRBaseDir => Path.Combine(AppContext.BaseDirectory, "UEVR");
}
