using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace DS3AppearanceTool.Game;

/// <summary>
/// Thin wrapper over ReadProcessMemory / WriteProcessMemory plus the two things
/// a Cheat Engine table does for us: an AOB scan over the main module and a
/// remote thread that runs a few bytes of our own code inside the game.
/// </summary>
public sealed class ProcessMemory : IDisposable
{
    private IntPtr _handle;

    private ProcessMemory(Process process, IntPtr handle)
    {
        Process = process;
        _handle = handle;
        var module = process.MainModule
            ?? throw new InvalidOperationException("Main module is not accessible.");
        ModuleBase = module.BaseAddress;
        ModuleSize = module.ModuleMemorySize;
    }

    public Process Process { get; }
    public IntPtr ModuleBase { get; }
    public int ModuleSize { get; }

    public bool IsRunning
    {
        get
        {
            try { return _handle != IntPtr.Zero && !Process.HasExited; }
            catch { return false; }
        }
    }

    /// <summary>Attaches to the first running process with the given name, or null.</summary>
    public static ProcessMemory? Open(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_ACCESS, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                process.Dispose();
                throw new Win32Exception(error,
                    $"Cannot open {processName} (error {error}). Try running this tool as administrator.");
            }

            try
            {
                return new ProcessMemory(process, handle);
            }
            catch
            {
                NativeMethods.CloseHandle(handle);
                process.Dispose();
                throw;
            }
        }

        return null;
    }

    public byte[] Read(IntPtr address, int size)
    {
        var buffer = new byte[size];
        if (!NativeMethods.ReadProcessMemory(_handle, address, buffer, size, out var read)
            || (int)read != size)
        {
            throw new IOException(
                $"Failed to read {size} bytes at 0x{address.ToInt64():X} " +
                $"(error {Marshal.GetLastWin32Error()}).");
        }

        return buffer;
    }

    /// <summary>Best-effort read: returns false instead of throwing on unreadable pages.</summary>
    public bool TryRead(IntPtr address, byte[] buffer, int size)
    {
        return NativeMethods.ReadProcessMemory(_handle, address, buffer, size, out var read)
               && (int)read == size;
    }

    public void Write(IntPtr address, byte[] data)
    {
        if (!NativeMethods.WriteProcessMemory(_handle, address, data, data.Length, out var written)
            || (int)written != data.Length)
        {
            throw new IOException(
                $"Failed to write {data.Length} bytes at 0x{address.ToInt64():X} " +
                $"(error {Marshal.GetLastWin32Error()}).");
        }
    }

    public long ReadInt64(IntPtr address) => BitConverter.ToInt64(Read(address, 8), 0);

    public int ReadInt32(IntPtr address) => BitConverter.ToInt32(Read(address, 4), 0);

    public byte ReadByte(IntPtr address) => Read(address, 1)[0];

    public void WriteByte(IntPtr address, byte value) => Write(address, new[] { value });

    /// <summary>
    /// Scans the main module for a byte pattern and returns the address of the first
    /// match, or IntPtr.Zero. The module is read in chunks that overlap by the pattern
    /// length, so a match never falls between two chunks; unreadable chunks are skipped.
    /// </summary>
    public IntPtr Scan(byte?[] pattern)
    {
        if (pattern.Length == 0) throw new ArgumentException("Empty pattern.", nameof(pattern));

        const int chunkSize = 4 * 1024 * 1024;
        var buffer = new byte[chunkSize];
        var overlap = pattern.Length - 1;
        var offset = 0;

        while (offset < ModuleSize)
        {
            var size = Math.Min(chunkSize, ModuleSize - offset);
            if (size < pattern.Length) break;

            if (TryRead(ModuleBase + offset, buffer, size))
            {
                var index = Find(buffer, size, pattern);
                if (index >= 0) return ModuleBase + offset + index;
            }

            offset += size - overlap;
        }

        return IntPtr.Zero;
    }

    private static int Find(byte[] haystack, int length, byte?[] pattern)
    {
        var first = pattern[0];
        var last = length - pattern.Length;

        for (var i = 0; i <= last; i++)
        {
            if (first.HasValue && haystack[i] != first.Value) continue;

            var matched = true;
            for (var j = 1; j < pattern.Length; j++)
            {
                var expected = pattern[j];
                if (expected.HasValue && haystack[i + j] != expected.Value)
                {
                    matched = false;
                    break;
                }
            }

            if (matched) return i;
        }

        return -1;
    }

    /// <summary>'48 8B 05 ?? ?? ?? ?? C3' -> bytes with nulls for the wildcards.</summary>
    public static byte?[] ParsePattern(string aob)
    {
        var tokens = aob.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var pattern = new byte?[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            pattern[i] = token is "??" or "?" or "xx"
                ? null
                : Convert.ToByte(token, 16);
        }

        return pattern;
    }

    /// <summary>
    /// Runs a small piece of code inside the game and waits for it to finish, the way
    /// a Cheat Engine <c>createthread</c> script does. The allocation is leaked rather
    /// than freed if the thread is still running when the wait times out — freeing code
    /// a thread is executing would crash the game.
    /// </summary>
    public void RunShellcode(byte[] code, uint timeoutMs = 5000)
    {
        var remote = NativeMethods.VirtualAllocEx(
            _handle, IntPtr.Zero, code.Length,
            NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
            NativeMethods.PAGE_EXECUTE_READWRITE);
        if (remote == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualAllocEx failed.");

        var finished = false;
        try
        {
            Write(remote, code);

            var thread = NativeMethods.CreateRemoteThread(
                _handle, IntPtr.Zero, IntPtr.Zero, remote, IntPtr.Zero, 0, out _);
            if (thread == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateRemoteThread failed.");

            try
            {
                finished = NativeMethods.WaitForSingleObject(thread, timeoutMs)
                           == NativeMethods.WAIT_OBJECT_0;
            }
            finally
            {
                NativeMethods.CloseHandle(thread);
            }
        }
        finally
        {
            if (finished)
                NativeMethods.VirtualFreeEx(_handle, remote, IntPtr.Zero, NativeMethods.MEM_RELEASE);
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }

        Process.Dispose();
    }
}
