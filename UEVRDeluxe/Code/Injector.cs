#region Usings
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
	#region * Injection
	public static Process FindInjectableProcess(string exeName) {
		var allProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exeName).ToLowerInvariant());

		foreach (var process in allProcesses) {
			try {
				if (process.MainWindowTitle.Length == 0) continue;

				if (Environment.Is64BitOperatingSystem
					&& Win32.IsWow64Process(process.Handle, out bool isWow64) && isWow64)
					continue;

				foreach (ProcessModule module in process.Modules) {
					string moduleLow = module.ModuleName?.ToLower();
					if (moduleLow == "d3d11.dll" || moduleLow == "d3d12.dll") return process;
				}
			} catch { }
		}

		return null;
	}

	/// <summary>Inject the DLL into the target process</summary>
	/// <param name="dllName">local filename</param>
	public static void InjectDll(int processID, string dllName) {
		string fullPath = GetFullDLLPath(dllName);

		// Open the target process with the necessary access
		nint processHandle = Win32.OpenProcess(0x1F0FFF, false, processID);
		if (processHandle == nint.Zero) throw new Exception("Access error. You must start UEVR Easy Injector as administrator.");

		// Get the address of the LoadLibrary function
		nint loadLibraryAddress = Win32.GetProcAddress(Win32.GetModuleHandle("kernel32.dll"), "LoadLibraryW");
		if (loadLibraryAddress == nint.Zero) throw new Exception("Could not obtain LoadLibraryW address in the target process");

		// Allocate memory in the target process for the DLL path
		// Flags: MEM_COMMIT, PAGE_EXECUTE_READWRITE
		nint dllPathAddress = Win32.VirtualAllocEx(processHandle, nint.Zero, (uint)(fullPath.Length * 2), 0x1000, 0x40);
		if (dllPathAddress == nint.Zero) throw new Exception("Failed to allocate memory in the target process");

		// Write the DLL path in UTF-16
		byte[] bytes = Encoding.Unicode.GetBytes(fullPath);
		if (!Win32.WriteProcessMemory(processHandle, dllPathAddress, bytes, (uint)bytes.Length, out int bytesWritten)
			|| bytesWritten != bytes.Length)
			throw new Exception("Failed to write DLL path to the target process memory");

		// Create a remote thread in the target process that calls LoadLibrary with the DLL path
		nint threadHandle = Win32.CreateRemoteThread(processHandle, nint.Zero, 0, loadLibraryAddress, dllPathAddress, 0, nint.Zero);
		if (threadHandle == nint.Zero) throw new Exception("Failed to create remote thread in the target processs");

		uint waitResult = Win32.WaitForSingleObject(threadHandle, 3000);
		if (waitResult != 0) throw new Exception("Failed to wait for remote thread in the target process injecting");

		// Flag: MEM_RELEASE
		if (Win32.VirtualFreeEx(processHandle, dllPathAddress, 0, 0x8000) == 0)
			throw new Exception("Failed to free memory in the target process");
	}

	public static nint InjectDllFindBase(int processID, string dllName) {
		InjectDll(processID, dllName);

		string fullPath = GetFullDLLPath(dllName);
		var process = Process.GetProcessById(processID);
		if (process == null) throw new Exception("Process killed");

		foreach (ProcessModule module in process.Modules) {
			if (module.FileName == fullPath) return module.BaseAddress;
		}

		throw new Exception("Cannot find DLL path");
	}

	public static void CallFunctionNoArgs(int processId, string dllName, nint dllBase, string functionName) {
		nint processHandle = Win32.OpenProcess(0x1F0FFF, false, processId);
		if (processHandle == nint.Zero)
			throw new Exception("Could not open a handle to the target process.\nYou may need to start this program as an administrator, or the process may be protected.");

		// We need to load the DLL into our own process temporarily as a workaround for GetProcAddress not working with remote DLLs
		nint localDllHandle = Win32.LoadLibrary(Path.Combine(UEVRBaseDir, dllName));
		if (localDllHandle == nint.Zero) throw new Exception("Could not load the target DLL into our own process");

		nint localVa = Win32.GetProcAddress(localDllHandle, functionName);

		if (localVa == nint.Zero) throw new Exception($"Could not obtain {functionName} address in our own process");

		nint rva = (nint)(localVa.ToInt64() - localDllHandle.ToInt64());
		nint functionAddress = (nint)(dllBase.ToInt64() + rva.ToInt64());

		// Create a remote thread in the target process that calls the function
		nint threadHandle = Win32.CreateRemoteThread(processHandle, nint.Zero, 0, functionAddress, nint.Zero, 0, nint.Zero);
		if (threadHandle == nint.Zero) throw new Exception("Failed to create remote thread in the target processs");

		uint waitResult = Win32.WaitForSingleObject(threadHandle, 3000);
		if (waitResult != 0) throw new Exception("Failed to wait for remote thread in the target process calling function");
	}
	#endregion

	#region * UpdateBackend
	const string UEVR_NIGHTLY_URL = "https://github.com/praydog/UEVR-nightly/releases/latest";

	/// <summary>Contains the URL of the version successfully downloaded</summary>
	const string UEVR_VERSION_FILENAME = "UEVRLink.txt";


	/// <summary>Download latest UEVR nightly and install locally</summary>
	/// <returns>True if update was required, false if not.</returns>
	public static async Task<bool> UpdateBackendAsync() {
		if (!Win32.IsUserAnAdmin()) throw new Exception("Please run UEVR Easy Injector as an administrator for downloads");

		string zipUrl;

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

			if (File.Exists(VersionFilePath) && string.Equals(File.ReadAllText(VersionFilePath).Trim(), zipUrl)) {
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

		File.WriteAllText(VersionFilePath, zipUrl);
		return true;
	}

	/// <summary>Currently installed nightly UEVR version</summary>
	/// <returns>Returns NULL if not downloaded yet</returns>
	public static string GetUEVRVersion() {
		if (!File.Exists(VersionFilePath)) return null;
		string version = File.ReadAllText(VersionFilePath).Trim();

		// Parse the nighly number from a URL like https://github.com/praydog/UEVR-nightly/releases/download/nightly-{nightlyNumber}-{commitHash}/uevr.zip
		var match = Regex.Match(version, @"nightly-(?<NightlyNumber>[^-]+)-");
		if (!match.Success) return null;

		return match.Groups["NightlyNumber"].Value;
	}
	#endregion

	#region * Directory paths
	static string UEVRBaseDir => Path.Combine(AppContext.BaseDirectory, "UEVR");
	static string VersionFilePath => Path.Combine(UEVRBaseDir, UEVR_VERSION_FILENAME);

	static string GetFullDLLPath(string dllName) {
		var fullPath = Path.Combine(UEVRBaseDir, dllName);

		if (!File.Exists(fullPath))
			throw new Exception($"{dllName} does not appear to exist! Check if any anti-virus software has deleted the file. Reinstall UEVR if necessary.\n\nBaseDirectory: {AppContext.BaseDirectory}");

		fullPath = Path.GetFullPath(fullPath);
		return fullPath;
	}
	#endregion
}
