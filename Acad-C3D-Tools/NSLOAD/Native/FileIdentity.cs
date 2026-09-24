using System;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace NSLOAD.Native
{
    /// <summary>
    /// Whether two paths name the same file on disk. The loader may report a
    /// mapped module under another spelling than the one it was loaded with (a
    /// resolved drive letter, a short name, a <c>\\?\</c> prefix), so comparing
    /// path strings can call one file two.
    /// </summary>
    internal static class FileIdentity
    {
        private const uint FileReadAttributes = 0x80;
        private const uint ShareAll = 0x1 | 0x2 | 0x4; // read | write | delete
        private const uint OpenExisting = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
            uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle hFile, out ByHandleFileInformation lpFileInformation);

        /// <summary>
        /// True when both paths name one file. Equal spellings short-circuit;
        /// otherwise the volume serial and file index decide. A path that cannot
        /// be opened (gone, or no access) is not the same file.
        /// </summary>
        public static bool Same(string a, string b)
        {
            if (string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase))
                return true;

            var idA = Identify(a);
            var idB = Identify(b);
            return idA != null && idA == idB;
        }

        // Opened for attributes only and sharing everything, so a mapped image
        // or a file OneDrive is writing can still be identified.
        private static (uint Volume, ulong Index)? Identify(string path)
        {
            using var handle = CreateFileW(path, FileReadAttributes, ShareAll, IntPtr.Zero,
                                           OpenExisting, 0, IntPtr.Zero);
            if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info))
                return null;
            return (info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);
        }
    }
}
