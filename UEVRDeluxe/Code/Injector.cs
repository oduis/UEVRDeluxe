#region Usings
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text; 
#endregion

namespace UEVRDeluxe.Code;

class Injector {
	[DllImport("kernel32.dll")]
	public static extern nint OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

	[DllImport("kernel32.dll")]
	public static extern bool CloseHandle(nint hObject);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
	public static extern nint GetModuleHandle(string lpModuleName);

	[DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
	public static extern nint GetProcAddress(nint hModule, string procName);

	[DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
	public static extern nint VirtualAllocEx(nint hProcess, nint lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

	[DllImport("kernel32.dll", SetLastError = true)]
	public static extern bool WriteProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

	[DllImport("kernel32.dll")]
	public static extern nint CreateRemoteThread(nint hProcess, nint lpThreadAttributes, uint dwStackSize, nint lpStartAddress, nint lpParameter, uint dwCreationFlags, nint lpThreadId);

	[DllImport("kernel32.dll", SetLastError = true)]
	static extern bool GetExitCodeThread(nint hThread, out uint lpExitCode);

	[DllImport("kernel32.dll", SetLastError = true)]
	public static extern uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);

	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
	public static extern nint LoadLibrary(string lpFileName);

	// FreeLibrary
	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
	public static extern bool FreeLibrary(nint hModule);

	[DllImport("user32.dll")]
	public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

	// Inject the DLL into the target process
	// dllPath is local filename, relative to EXE.
	public static bool InjectDll(int processId, string dllPath, out nint dllBase) {
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

		if (!File.Exists(dllPath)) {
			throw new Exception($"{originalPath} does not appear to exist! Check if any anti-virus software has deleted the file. Reinstall UEVR if necessary.\n\nBaseDirectory: {AppContext.BaseDirectory}");
		}

		dllBase = nint.Zero;

		string fullPath = Path.GetFullPath(dllPath);

		// Open the target process with the necessary access
		nint processHandle = OpenProcess(0x1F0FFF, false, processId);

		if (processHandle == nint.Zero) {
			throw new Exception("Could not open a handle to the target process.\nYou may need to start this program as an administrator, or the process may be protected.");
		}

		// Get the address of the LoadLibrary function
		nint loadLibraryAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryW");

		if (loadLibraryAddress == nint.Zero) {
			throw new Exception("Could not obtain LoadLibraryW address in the target process.");
		}

		// Allocate memory in the target process for the DLL path
		nint dllPathAddress = VirtualAllocEx(processHandle, nint.Zero, (uint)fullPath.Length, 0x1000, 0x40);

		if (dllPathAddress == nint.Zero) {
			throw new Exception("Failed to allocate memory in the target process.");
		}

		// Write the DLL path in UTF-16
		int bytesWritten = 0;
		var bytes = Encoding.Unicode.GetBytes(fullPath);
		WriteProcessMemory(processHandle, dllPathAddress, bytes, (uint)(fullPath.Length * 2), out bytesWritten);

		// Create a remote thread in the target process that calls LoadLibrary with the DLL path
		nint threadHandle = CreateRemoteThread(processHandle, nint.Zero, 0, loadLibraryAddress, dllPathAddress, 0, nint.Zero);

		if (threadHandle == nint.Zero)throw new Exception("Failed to create remote thread in the target processs.");

		WaitForSingleObject(threadHandle, 1000);

		var p = Process.GetProcessById(processId);

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
		nint processHandle = OpenProcess(0x1F0FFF, false, processId);

		if (processHandle == nint.Zero)
			throw new Exception("Could not open a handle to the target process.\nYou may need to start this program as an administrator, or the process may be protected.");

		// We need to load the DLL into our own process temporarily as a workaround for GetProcAddress not working with remote DLLs
		nint localDllHandle = LoadLibrary(dllPath);

		if (localDllHandle == nint.Zero) throw new Exception("Could not load the target DLL into our own process.");

		nint localVa = GetProcAddress(localDllHandle, functionName);

		if (localVa == nint.Zero) throw new Exception("Could not obtain " + functionName + " address in our own process.");

		nint rva = (nint)(localVa.ToInt64() - localDllHandle.ToInt64());
		nint functionAddress = (nint)(dllBase.ToInt64() + rva.ToInt64());

		// Create a remote thread in the target process that calls the function
		nint threadHandle = CreateRemoteThread(processHandle, nint.Zero, 0, functionAddress, nint.Zero, 0, nint.Zero);

		if (threadHandle == nint.Zero) throw new Exception("Failed to create remote thread in the target processs.");

		if (wait) {
			WaitForSingleObject(threadHandle, 2000);
		}

		return true;
	}
}
