#region Usings
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
#endregion

namespace UEVRDeluxe.Code;

static class CmdManager {
	/// <summary>Run the helper command with elevated permissions to e.g. update the backend.</summary>
	public static async Task RunAsync(string arguments) {
		string helperPath = Path.Combine(AppContext.BaseDirectory, "Cmd\\UEVRDeluxeCmd.exe");
		if (!File.Exists(helperPath)) throw new Exception("Helper executable not found");

		string tempFilePath = Path.GetTempFileName();

		var psi = new ProcessStartInfo {
			FileName = helperPath,
			Arguments = $"{tempFilePath} {arguments}",
			Verb = "runas",
			UseShellExecute = false,
			WorkingDirectory = Path.GetDirectoryName(helperPath)
		};

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
	}
}
