using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// A drive of the machine as Look in lists it: its root, its name as Windows writes it (<c>Data (D:)</c>, or <c>Local
    /// Disk (C:)</c>, <c>DVD Drive (E:)</c>, <c>Network Drive (S:)</c> where it has no label) and the words of its kind for the
    /// Type column.
    /// </summary>
    internal sealed class AppLoadDrive
    {
        internal AppLoadDrive(string root, string label, DriveType type)
        {
            Root = root;
            DriveType = type;
            string letter = root.Length >= 2 && root[1] == ':' ? " (" + root.Substring(0, 2) + ")" : string.Empty;
            string words = string.IsNullOrWhiteSpace(label) ? NameWords(type) : label.Trim();
            Name = letter.Length > 0 ? words + letter : root.TrimEnd('\\', '/');
        }

        /// <summary>Gets the root, <c>D:\</c>.</summary>
        internal string Root { get; }

        /// <summary>Gets the name Look in and This PC show, <c>Data (D:)</c>.</summary>
        internal string Name { get; }

        /// <summary>Gets the kind of drive.</summary>
        internal DriveType DriveType { get; }

        /// <summary>Gets the words of the Type column: Local Disk, CD Drive, Network Drive or Removable Disk.</summary>
        internal string TypeName
        {
            get
            {
                switch (DriveType)
                {
                    case DriveType.CDRom:
                        return "CD Drive";
                    case DriveType.Network:
                        return "Network Drive";
                    case DriveType.Removable:
                        return "Removable Disk";
                    default:
                        return "Local Disk";
                }
            }
        }

        private static string NameWords(DriveType type)
        {
            switch (type)
            {
                case DriveType.CDRom:
                    return "DVD Drive";
                case DriveType.Network:
                    return "Network Drive";
                case DriveType.Removable:
                    return "Removable Disk";
                default:
                    return "Local Disk";
            }
        }
    }

    /// <summary>Where Look in finds its places: the folders of the user and the drives, which a test replaces with its own.</summary>
    internal interface IAppLoadPlaceSource
    {
        /// <summary>Gets the Desktop folder; null where there is none.</summary>
        string Desktop { get; }

        /// <summary>Gets the user's own folder, <c>C:\Users\Ann</c>; null where there is none.</summary>
        string UserFolder { get; }

        /// <summary>Gets Documents; null where there is none.</summary>
        string Documents { get; }

        /// <summary>Gets Downloads; null where there is none.</summary>
        string Downloads { get; }

        /// <summary>Gets the drives that are ready, never waiting on one that is not.</summary>
        IReadOnlyList<AppLoadDrive> Drives();
    }

    /// <summary>The places of this machine: the known folders of Windows and its ready drives.</summary>
    internal sealed class AppLoadSystemPlaces : IAppLoadPlaceSource
    {
        // How long a drive may take to say whether it is ready: a disconnected network drive can take far longer.
        private const int DriveWaitMilliseconds = 1500;
        private static readonly Guid DownloadsId = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        public string Desktop => Existing(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));

        public string UserFolder => Existing(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        public string Documents => Existing(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        public string Downloads
        {
            get
            {
                string known = KnownFolder(DownloadsId);

                if (known != null)
                {
                    return Existing(known);
                }

                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return string.IsNullOrEmpty(profile) ? null : Existing(Path.Combine(profile, "Downloads"));
            }
        }

        public IReadOnlyList<AppLoadDrive> Drives()
        {
            DriveInfo[] all;

            try
            {
                all = DriveInfo.GetDrives();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Array.Empty<AppLoadDrive>();
            }

            Task<AppLoadDrive>[] probes = all.Select(d => Task.Run(() => Probe(d))).ToArray();

            try
            {
                Task.WaitAll(probes, DriveWaitMilliseconds);
            }
            catch (AggregateException)
            {
                // A probe that threw is left out below, as one that did not finish.
            }

            return probes
                .Where(p => p.Status == TaskStatus.RanToCompletion && p.Result != null)
                .Select(p => p.Result)
                .OrderBy(d => d.Root, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static AppLoadDrive Probe(DriveInfo drive)
        {
            try
            {
                return drive.IsReady ? new AppLoadDrive(drive.Name, drive.VolumeLabel, drive.DriveType) : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string Existing(string path) =>
            !string.IsNullOrEmpty(path) && Directory.Exists(path) ? path : null;

        /// <summary>The folder of a known-folder id, which <see cref="Environment.SpecialFolder"/> has no member for; null where the shell does not give one.</summary>
        private static string KnownFolder(Guid id)
        {
            try
            {
                if (SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out IntPtr text) != 0)
                {
                    return null;
                }

                try
                {
                    return Marshal.PtrToStringUni(text);
                }
                finally
                {
                    Marshal.FreeCoTaskMem(text);
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                return null;
            }
        }

        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath(ref Guid id, uint flags, IntPtr token, out IntPtr path);
    }
}
