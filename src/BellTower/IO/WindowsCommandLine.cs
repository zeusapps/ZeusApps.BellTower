using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BellTower.IO;

/// <summary>Parses a Windows process command line with the operating system's argv rules.</summary>
public static class WindowsCommandLine
{
    public static string[] Parse(string commandLine)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandLine);
        var pointer = CommandLineToArgvW(commandLine, out var count);
        if (pointer == IntPtr.Zero)
        { throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not parse a process command line."); }

        try
        {
            var arguments = new string[count];
            for (var index = 0; index < count; index++)
            {
                arguments[index] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, index * IntPtr.Size)) ??
                    throw new InvalidDataException("Windows returned an incomplete process argument list.");
            }

            return arguments;
        }
        finally
        {
            _ = LocalFree(pointer);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int argumentCount);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);
}
