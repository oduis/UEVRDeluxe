using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace UEVRDeluxe.Code;

public static partial class Win32 {
	[LibraryImport("kernel32.dll", SetLastError = true)]
	internal static partial nint OpenProcess(int access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool CloseHandle(IntPtr handle);

	[LibraryImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	internal static partial IntPtr GetModuleHandle(string moduleName);

	[LibraryImport("kernel32.dll")]
	internal static partial IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	internal static partial nint VirtualAllocEx(nint hProcess, nint lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool WriteProcessMemory(nint hProcess, nint lpBaseAddress, byte[] lpBuffer, uint nSize, out int lpNumberOfBytesWritten);

	[LibraryImport("kernel32.dll")]
	internal static partial nint CreateRemoteThread(nint hProcess, nint lpThreadAttributes, uint dwStackSize, nint lpStartAddress, nint lpParameter, uint dwCreationFlags, nint lpThreadId);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool GetExitCodeThread(nint hThread, out uint lpExitCode);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	internal static partial uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);

	[LibraryImport("kernel32.dll", EntryPoint = "LoadLibraryW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	internal static partial nint LoadLibrary(string lpFileName);

	[LibraryImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool FreeLibrary(nint hModule);

	[LibraryImport("user32.dll")]
	internal static partial uint GetDpiForWindow(IntPtr hwnd);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
	public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, WindowLongIndexFlags nIndex, IntPtr dwNewLong);

	public delegate IntPtr WNDPROC(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

	[LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
	internal static partial IntPtr CallWindowProc(IntPtr prevWndFunc, IntPtr hWnd, uint msg, IntPtr wparam, IntPtr lparam);

	[LibraryImport("user32.dll")]
	internal static partial void SwitchToThisWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAltTab);

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

	[LibraryImport("user32.dll")]
	internal static partial IntPtr GetForegroundWindow();

	[LibraryImport("user32.dll")]
	internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

	[LibraryImport("kernel32.dll")]
	internal static partial IntPtr OpenProcess(ProcessAccessFlags processAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint processId);

	[LibraryImport("psapi.dll", EntryPoint = "GetModuleBaseNameW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	internal static partial uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] char[] lpBaseName, int nSize);

	[LibraryImport("shell32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	internal static partial bool IsUserAnAdmin();

	[LibraryImport("user32.dll")]
	internal static partial uint SendInput(uint nInputs, [MarshalAs(UnmanagedType.LPArray), In] INPUT[] pInputs, int cbSize);

	[LibraryImport("user32.dll", EntryPoint = "GetKeyboardLayout", SetLastError = true)]
	internal static partial IntPtr GetKeyboardLayout(uint idThread);

	[LibraryImport("user32.dll", EntryPoint = "VkKeyScanExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	internal static partial ushort VkKeyScanExW(char ch, IntPtr dwhkl);

	internal const uint INPUT_KEYBOARD = 1;
	internal const ushort VK_RETURN = 0x0D;
	internal const uint KEYEVENTF_KEYUP = 0x0002;
}

[Flags]
public enum ProcessAccessFlags : uint {
	QUERY_INFORMATION = 0x0400,
	VM_READ = 0x0010
}

[Flags]
public enum WindowLongIndexFlags : int {
	GWL_EXSTYLE = -20,
	GWLP_HINSTANCE = -6,
	GWLP_HWNDPARENT = -8,
	GWL_ID = -12,
	GWLP_ID = GWL_ID,
	GWL_STYLE = -16,
	GWL_USERDATA = -21,
	GWLP_USERDATA = GWL_USERDATA,
	GWL_WNDPROC = -4,
	GWLP_WNDPROC = GWL_WNDPROC,
	DWLP_USER = 0x8,
	DWLP_MSGRESULT = 0x0,
	DWLP_DLGPROC = 0x4,
}

[Flags]
public enum SetWindowLongFlags : uint {
	WS_OVERLAPPED = 0,
	WS_POPUP = 0x80000000,
	WS_CHILD = 0x40000000,
	WS_MINIMIZE = 0x20000000,
	WS_VISIBLE = 0x10000000,
	WS_DISABLED = 0x8000000,
	WS_CLIPSIBLINGS = 0x4000000,
	WS_CLIPCHILDREN = 0x2000000,
	WS_MAXIMIZE = 0x1000000,
	WS_CAPTION = 0xC00000,
	WS_BORDER = 0x800000,
	WS_DLGFRAME = 0x400000,
	WS_VSCROLL = 0x200000,
	WS_HSCROLL = 0x100000,
	WS_SYSMENU = 0x80000,
	WS_THICKFRAME = 0x40000,
	WS_GROUP = 0x20000,
	WS_TABSTOP = 0x10000,
	WS_MINIMIZEBOX = 0x20000,
	WS_MAXIMIZEBOX = 0x10000,
	WS_TILED = WS_OVERLAPPED,
	WS_ICONIC = WS_MINIMIZE,
	WS_SIZEBOX = WS_THICKFRAME,

	WS_EX_DLGMODALFRAME = 0x0001,
	WS_EX_NOPARENTNOTIFY = 0x0004,
	WS_EX_TOPMOST = 0x0008,
	WS_EX_ACCEPTFILES = 0x0010,
	WS_EX_TRANSPARENT = 0x0020,
	WS_EX_MDICHILD = 0x0040,
	WS_EX_TOOLWINDOW = 0x0080,
	WS_EX_WINDOWEDGE = 0x0100,
	WS_EX_CLIENTEDGE = 0x0200,
	WS_EX_CONTEXTHELP = 0x0400,
	WS_EX_RIGHT = 0x1000,
	WS_EX_LEFT = 0x0000,
	WS_EX_RTLREADING = 0x2000,
	WS_EX_LTRREADING = 0x0000,
	WS_EX_LEFTSCROLLBAR = 0x4000,
	WS_EX_RIGHTSCROLLBAR = 0x0000,
	WS_EX_CONTROLPARENT = 0x10000,
	WS_EX_STATICEDGE = 0x20000,
	WS_EX_APPWINDOW = 0x40000,
	WS_EX_OVERLAPPEDWINDOW = WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE,
	WS_EX_PALETTEWINDOW = WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST,
	WS_EX_LAYERED = 0x00080000,
	WS_EX_NOINHERITLAYOUT = 0x00100000,
	WS_EX_LAYOUTRTL = 0x00400000,
	WS_EX_COMPOSITED = 0x02000000,
	WS_EX_NOACTIVATE = 0x08000000,
}

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT {
	public uint type;
	public InputUnion u;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion {
	[FieldOffset(0)] public MOUSEINPUT mi;
	[FieldOffset(0)] public KEYBDINPUT ki;
	[FieldOffset(0)] public HARDWAREINPUT hi;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT {
	public int dx;
	public int dy;
	public uint mouseData;
	public uint dwFlags;
	public uint time;
	public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT {
	public ushort wVk;
	public ushort wScan;
	public uint dwFlags;
	public uint time;
	public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HARDWAREINPUT {
	public uint uMsg;
	public ushort wParamL;
	public ushort wParamH;
}
