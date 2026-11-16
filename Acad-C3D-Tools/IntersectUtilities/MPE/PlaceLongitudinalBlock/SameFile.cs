using System.IO;
using System.Runtime.InteropServices;

using CSharpFunctionalExtensions;

using Microsoft.Win32.SafeHandles;

using FileTime = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace IntersectUtilities.MPE.PlaceLongitudinalBlock;

/// <summary>
/// Whether two paths name the same file. A drive letter that is a subst of a folder, a UNC path, "/" or ".." all
/// name the same file by a different path, so the file's identity decides: its volume serial number and file
/// index, read without opening it for reading, so a drawing AutoCAD holds open is readable too. When either
/// identity cannot be read, the full paths are compared, ignoring case.
/// </summary>
internal static class SameFile
{
    private const uint ShareAll = 0x1 | 0x2 | 0x4;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;

    private readonly record struct FileIdentity(uint VolumeSerialNumber, uint IndexHigh, uint IndexLow);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public FileTime CreationTime;
        public FileTime LastAccessTime;
        public FileTime LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    internal static bool Matches(string first, string second) =>
        IdentityOf(first)
            .Bind(firstIdentity => IdentityOf(second).Map(secondIdentity => firstIdentity == secondIdentity))
            .Match(
                same => same,
                () => string.Equals(FullPathOf(first), FullPathOf(second), StringComparison.OrdinalIgnoreCase));

    /// <summary>The file's identity; None when the file cannot be reached (no exception: the API returns a status).</summary>
    private static Maybe<FileIdentity> IdentityOf(string path)
    {
        // Access 0 asks only for metadata, which no sharing mode of another open handle refuses.
        using SafeFileHandle handle =
            CreateFileW(path, 0, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero);

        return !handle.IsInvalid && GetFileInformationByHandle(handle, out ByHandleFileInformation information)
            ? Maybe.From(new FileIdentity(
                information.VolumeSerialNumber, information.FileIndexHigh, information.FileIndexLow))
            : Maybe<FileIdentity>.None;
    }

    private static string FullPathOf(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            // Not a valid path: compared as written.
            return path;
        }
        catch (PathTooLongException)
        {
            return path;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);
}
