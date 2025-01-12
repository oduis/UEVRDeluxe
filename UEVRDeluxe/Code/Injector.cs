#region Usings
using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.IO;
using System.Text; 
#endregion

namespace UEVRDeluxe.Code;

class Injector {
	// Inject the DLL into the target process
	// dllPath is local filename, relative to EXE.
	public static bool InjectDll(int processID, string dllPath, out nint dllBase) {
		string originalPath = dllPath;

		try {
			var exeDirectory = AppContext.BaseDirectory;

			if (exeDirectory != null) {
				var newPath = Path.Combine(exeDirectory, dllPath);

				if (File.Exists(newPath)) {
					dllPath = Path.Combine(exeDirectory, dllPath);
				}
			}
		} catch (Exception) {
		}

		if (!File.Exists(dllPath))
			throw new Exception($"{originalPath} does not appear to exist! Check if any anti-virus software has deleted the file. Reinstall UEVR if necessary.\n\nBaseDirectory: {AppContext.BaseDirectory}");

		dllBase = nint.Zero;

		string fullPath = Path.GetFullPath(dllPath);

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

		if (threadHandle == nint.Zero)throw new Exception("Failed to create remote thread in the target processs.");

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

	public static bool InjectDll(int processId, string dllPath) {
		nint dummy;
		return InjectDll(processId, dllPath, out dummy);
	}

	public static bool CallFunctionNoArgs(int processId, string dllPath, nint dllBase, string functionName, bool wait = false) {
		nint processHandle = Win32.OpenProcess(0x1F0FFF, false, processId);

		if (processHandle == nint.Zero)
			throw new Exception("Could not open a handle to the target process.\nYou may need to start this program as an administrator, or the process may be protected.");

		// We need to load the DLL into our own process temporarily as a workaround for GetProcAddress not working with remote DLLs
		nint localDllHandle = Win32.LoadLibrary(dllPath);

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
}
