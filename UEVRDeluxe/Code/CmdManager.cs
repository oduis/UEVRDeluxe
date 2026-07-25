#region Usings
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UEVRDeluxe.Common;
#endregion

namespace UEVRDeluxe.Code;

static class CmdManager {
	#region Public commands
	/// <summary>Wrapper to update the backend using the elevated helper command.</summary>
	/// <param name="nightlyNumber">Nightly number to update to. Use a positive integer.</param>
	public static async Task UpdatePraydogBackendAsync(int nightlyNumber) {
		if (nightlyNumber <= 0) throw new ArgumentException("nightlyNumber must be a positive integer", nameof(nightlyNumber));
		await RunAsync($"{UEVRCmdArgs.UPDATE_PRAYDOG_BACKEND} {nightlyNumber}");
	}

	/// <summary>Wrapper to update the backend for Joey Hodge using the elevated helper command.</summary>
	/// <param name="name">Tag name.</param>
	public static async Task UpdateJoeyHodgeBackendAsync(string name)
		=> await RunAsync($"{UEVRCmdArgs.UPDATE_JOEYHODGE_BACKEND} {name}");

	/// <summary>Wrapper to update the backend for PureDark using the elevated helper command.</summary>
	/// <param name="name">Tag name.</param>
	public static async Task UpdatePureDarkBackendAsync(string name)
		=> await RunAsync($"{UEVRCmdArgs.UPDATE_PUREDARK_BACKENDS} {name}");

	/// <summary>Wrapper to update the backend for Dortamur using the elevated helper command.</summary>
	/// <param name="name">Tag name.</param>
	public static async Task UpdateDortamurBackendAsync(string name)
		=> await RunAsync($"{UEVRCmdArgs.UPDATE_DORTAMUR_BACKEND} {name}");

	/// <summary>Install a profile by calling the elevated helper command.</summary>
	public static Task InstallAsync(string profileRootFolder, string gameExeFolder) {
		if (string.IsNullOrWhiteSpace(profileRootFolder)) throw new ArgumentException("profileRootFolder is required", nameof(profileRootFolder));
		if (string.IsNullOrWhiteSpace(gameExeFolder)) throw new ArgumentException("gameExeFolder is required", nameof(gameExeFolder));

		// Quote paths to preserve spaces when passed to the helper
		string args = $"{UEVRCmdArgs.INSTALL_PROFILE} \"{profileRootFolder}\" \"{gameExeFolder}\"";
		return RunAsync(args);
	}

	/// <summary>Uninstall a profile by calling the elevated helper command.</summary>
	public static Task UninstallAsync(string profileRootFolder, string gameExeFolder) {
		if (string.IsNullOrWhiteSpace(profileRootFolder)) throw new ArgumentException("profileRootFolder is required", nameof(profileRootFolder));
		if (string.IsNullOrWhiteSpace(gameExeFolder)) throw new ArgumentException("gameExeFolder is required", nameof(gameExeFolder));

		string args = $"{UEVRCmdArgs.UNINSTALL_PROFILE} \"{profileRootFolder}\" \"{gameExeFolder}\"";
		return RunAsync(args);
	}
	#endregion

	#region RunAsync
	/// <summary>Run the helper command with elevated permissions to e.g. update the backend.</summary>
	static async Task RunAsync(string arguments) {
		string helperPath = Path.Combine(AppContext.BaseDirectory, "Cmd\\UEVRDeluxeCmd.exe");
		if (!File.Exists(helperPath)) throw new Exception("Helper executable not found");

		string tempFilePath = Path.GetTempFileName();

		var psi = new ProcessStartInfo {
			FileName = helperPath,
			Arguments = $"\"{tempFilePath}\" {arguments}",
			Verb = "runas",
			UseShellExecute = true,
			WorkingDirectory = Path.GetDirectoryName(helperPath)
		};

		Logger.Log.LogInformation($"Running {helperPath} with arguments: {psi.Arguments}");

		try {
			using var proc = Process.Start(psi);
			if (proc != null) {
				await proc.WaitForExitAsync();

				if (proc.ExitCode != 0) {
					string errMsg = null;

					if (File.Exists(tempFilePath)) {
						errMsg = File.ReadAllText(tempFilePath);
						File.Delete(tempFilePath);
					}

					if (string.IsNullOrEmpty(errMsg)) errMsg = "Unknown error in helper EXE";

					throw new Exception(errMsg);
				}
			} else throw new Exception("Failed to start helper EXE");
		} catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) {
			throw new OperationCanceledException("User cancelled elevation request", ex);
		}
	}
	#endregion
}
