using System;
using System.IO;
using System.Runtime.InteropServices;

namespace BoogieBoardSync
{
    internal static class StorageUtil
    {
        public static string FindBoogieDrive(bool verbose)
        {
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (drive.DriveType != DriveType.Removable || !drive.IsReady)
                        {
                            continue;
                        }

                        string root = drive.RootDirectory.FullName;
                        bool hasSyncFolder = Directory.Exists(Path.Combine(root, "SYNC"));
                        long size = drive.TotalSize;
                        string format = drive.DriveFormat;
                        string label = "";
                        try
                        {
                            label = drive.VolumeLabel;
                        }
                        catch (Exception)
                        {
                        }

                        bool sizeMatch = format.Equals("FAT32", StringComparison.OrdinalIgnoreCase)
                            && size > 3500000000L
                            && size < 4500000000L;
                        bool labelMatch = label.IndexOf("BOOGIE", StringComparison.OrdinalIgnoreCase) >= 0
                            || label.IndexOf("SYNC", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (sizeMatch && !hasSyncFolder && !labelMatch && verbose)
                        {
                            Console.WriteLine("Skipping {0}: FAT32 3.5-4.5GB but no SYNC folder or Boogie label. Use --eject={1} if this is the board.",
                                root, char.ToUpperInvariant(drive.Name[0]));
                        }

                        if (hasSyncFolder || labelMatch)
                        {
                            if (verbose)
                            {
                                Console.WriteLine("Detected Boogie Board storage: {0} label='{1}' format={2} size={3:N0}",
                                    root, label, format, size);
                            }
                            return char.ToUpperInvariant(drive.Name[0]).ToString();
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
            return "";
        }

        public static bool ReleaseBoogieStorage(bool verbose)
        {
            string letter = FindBoogieDrive(verbose);
            if (letter.Length == 0)
            {
                if (verbose)
                {
                    Console.WriteLine("No removable Boogie Board volume found; skipping storage release.");
                }
                return false;
            }
            return Release(letter, verbose);
        }

        public static bool Eject(string driveLetter)
        {
            return Release(driveLetter, true);
        }

        public static bool Release(string driveLetter, bool verbose)
        {
            if (string.IsNullOrEmpty(driveLetter))
            {
                return false;
            }

            char letter = char.ToUpperInvariant(driveLetter[0]);
            string path = @"\\.\" + letter + ":";
            IntPtr handle = NativeMethods.CreateFile(
                path,
                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                if (verbose)
                {
                    Console.WriteLine("Release {0}: open failed (error {1}).", path, Marshal.GetLastWin32Error());
                }
                return false;
            }

            try
            {
                uint returned;
                bool locked = NativeMethods.DeviceIoControl(
                    handle,
                    NativeMethods.FSCTL_LOCK_VOLUME,
                    IntPtr.Zero,
                    0,
                    null,
                    0,
                    out returned,
                    IntPtr.Zero);
                if (verbose)
                {
                    Console.WriteLine("Release {0}: lock={1} err={2}", letter, locked, locked ? 0 : Marshal.GetLastWin32Error());
                }

                bool dismounted = NativeMethods.DeviceIoControl(
                    handle,
                    NativeMethods.FSCTL_DISMOUNT_VOLUME,
                    IntPtr.Zero,
                    0,
                    null,
                    0,
                    out returned,
                    IntPtr.Zero);
                if (verbose)
                {
                    Console.WriteLine("Release {0}: dismount={1} err={2}", letter, dismounted, dismounted ? 0 : Marshal.GetLastWin32Error());
                }

                bool ejected = NativeMethods.DeviceIoControl(
                    handle,
                    NativeMethods.IOCTL_STORAGE_EJECT_MEDIA,
                    IntPtr.Zero,
                    0,
                    null,
                    0,
                    out returned,
                    IntPtr.Zero);
                if (verbose)
                {
                    Console.WriteLine("Release {0}: eject={1} err={2}", letter, ejected, ejected ? 0 : Marshal.GetLastWin32Error());
                }
                return ejected;
            }
            finally
            {
                NativeMethods.CloseHandle(handle);
            }
        }
    }
}
