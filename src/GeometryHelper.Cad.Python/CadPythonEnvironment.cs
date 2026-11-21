using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GeometryHelper.Cad.Python
{
    /// <summary>
    /// The Python the app will run scripts with, found before the engine starts, because a start that fails cannot be tried again
    /// in the same process. It is the one named in Global settings; failing that the one <c>PYTHONNET_PYDLL</c> names; failing that
    /// the newest 64-bit CPython 3.11 to 3.15 registered under PEP 514 (HKCU before HKLM at the same version). A setting that does
    /// not work is a problem to show, never silently replaced by another Python.
    /// </summary>
    public sealed class CadPythonEnvironment
    {
        private const int OldestMinor = 11;
        private const int NewestMinor = 15;
        private static readonly Regex DllName = new Regex(
            @"^python3(\d+)\.dll$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        private readonly List<string> _passedOver = new List<string>();

        private CadPythonEnvironment() { }

        /// <summary>Gets the <c>python.exe</c> given to the engine as its program, so that a virtual environment's <c>pyvenv.cfg</c> sets the paths; null where none is named.</summary>
        public string Executable { get; private set; }

        /// <summary>Gets the full path of <c>python3XY.dll</c>; null where no Python was found.</summary>
        public string Library { get; private set; }

        /// <summary>Gets the version, <c>3.14.4</c> from the environment's <c>pyvenv.cfg</c> or the registry, else <c>3.14</c> from the DLL's name; null where no Python was found.</summary>
        public string Version { get; private set; }

        /// <summary>Gets where it came from: <c>Global settings</c>, <c>PYTHONNET_PYDLL</c> or <c>Registry (PEP 514)</c>; null where no Python was found.</summary>
        public string Source { get; private set; }

        /// <summary>Gets what the app shows when no Python can be used, or null when one can.</summary>
        public string Problem { get; private set; }

        /// <summary>Gets each candidate that was left out and why, in the order they were looked at: <c>3.13: D:\Python313\python.exe is missing</c>.</summary>
        public IReadOnlyList<string> PassedOver => _passedOver;

        /// <summary>Gets whether a Python can be used: there is no <see cref="Problem"/>.</summary>
        public bool IsUsable => Problem == null;

        /// <summary>Finds the Python to use, as the class describes.</summary>
        /// <param name="configured">The <c>python.exe</c> (or its folder) set in Global settings; empty or null for the automatic order.</param>
        /// <returns>The environment, with a <see cref="Problem"/> where none can be used.</returns>
        public static CadPythonEnvironment Find(string configured) =>
            Find(configured, ReadRegistry(), Environment.GetEnvironmentVariable("PYTHONNET_PYDLL"));

        /// <summary>Finds the Python from the given setting, <c>PYTHONNET_PYDLL</c> and registry entries, which a test feeds.</summary>
        internal static CadPythonEnvironment Find(
            string configured,
            IReadOnlyList<CadPythonRegistration> registered,
            string environmentDll
        )
        {
            var found = new CadPythonEnvironment();
            string setting = (configured ?? string.Empty).Trim().Trim('"').Trim();

            if (setting.Length > 0)
            {
                found.Source = "Global settings";
                string exe = Directory.Exists(setting) ? Path.Combine(setting, "python.exe") : setting;

                if (!File.Exists(exe))
                {
                    return found.Fail("The Python set in Global settings (" + setting + ") was not found.");
                }

                return found.Check(exe, null);
            }

            string dll = (environmentDll ?? string.Empty).Trim().Trim('"');

            if (dll.Length > 0)
            {
                found.Source = "PYTHONNET_PYDLL";

                if (!File.Exists(dll))
                {
                    return found.Fail("The Python set in PYTHONNET_PYDLL (" + dll + ") was not found.");
                }

                string beside = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dll)), "python.exe");
                return found.Check(File.Exists(beside) ? beside : null, dll);
            }

            return found.FromRegistry(registered ?? new CadPythonRegistration[0]);
        }

        /// <inheritdoc/>
        public override string ToString() =>
            IsUsable
                ? "Python " + Version + (Executable == null ? " (" + Library + ")" : " (" + Executable + ")")
                : Problem;

        private CadPythonEnvironment Fail(string problem)
        {
            Problem = problem;
            Executable = null;
            Library = null;
            Version = null;
            return this;
        }

        /// <summary>Checks a Python named by its program or its DLL and keeps it when it is usable; the reason is <see cref="Problem"/> where it is not.</summary>
        private CadPythonEnvironment Check(string exe, string dll)
        {
            string shown = exe ?? dll;
            string reason = Examine(exe, dll, out string library, out string version);

            if (reason != null)
            {
                return Fail(reason.Replace("{path}", shown));
            }

            Executable = exe;
            Library = library;
            Version = version;
            Problem = null;
            return this;
        }

        /// <summary>The reason a Python cannot be used (with {path} for the name to show), or null with its DLL and version.</summary>
        private static string Examine(string exe, string dll, out string library, out string version)
        {
            library = dll;
            version = null;
            string baseFolder = dll != null ? Path.GetDirectoryName(Path.GetFullPath(dll)) : null;

            if (dll == null)
            {
                string home = null;
                string folder = Path.GetDirectoryName(Path.GetFullPath(exe));
                string cfg = Path.Combine(folder, "pyvenv.cfg");

                if (!File.Exists(cfg))
                {
                    cfg = Path.Combine(Path.GetDirectoryName(folder) ?? folder, "pyvenv.cfg");
                }

                if (File.Exists(cfg))
                {
                    ReadVenv(cfg, out home, out string cfgVersion);
                    version = cfgVersion;
                }

                baseFolder = !string.IsNullOrEmpty(home) ? home : folder;
                library = PickDll(baseFolder, version, out int minor);

                if (library == null)
                {
                    return "{path} has no python3XY.dll in "
                        + baseFolder
                        + ": GeometryHelper CAD needs a Python installed with its DLL.";
                }
            }

            Match name = DllName.Match(Path.GetFileName(library));
            int dllMinor = name.Success ? int.Parse(name.Groups[1].Value, CultureInfo.InvariantCulture) : 0;

            if (!name.Success)
            {
                return "{path} is not a python3XY.dll: GeometryHelper CAD needs the versioned DLL, never python3.dll.";
            }

            if (dllMinor < OldestMinor || dllMinor > NewestMinor)
            {
                return "{path} is Python 3."
                    + dllMinor.ToString(CultureInfo.InvariantCulture)
                    + ": GeometryHelper CAD needs 3.11 to 3.15.";
            }

            switch (ReadMachine(library))
            {
                case 0x8664:
                    break;
                case 0x14C:
                    return "{path} is a 32-bit Python: GeometryHelper CAD needs a 64-bit one.";
                default:
                    return "{path} is not an x64 Python: GeometryHelper CAD needs a 64-bit x64 one.";
            }

            version = version ?? "3." + dllMinor.ToString(CultureInfo.InvariantCulture);
            return null;
        }

        /// <summary>The python3XY.dll in a folder: the one of the given version where there is one, else the highest.</summary>
        private static string PickDll(string folder, string version, out int minor)
        {
            minor = 0;
            string best = null;

            if (!Directory.Exists(folder))
            {
                return null;
            }

            string[] files = Directory.GetFiles(folder, "python3*.dll");
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                Match name = DllName.Match(Path.GetFileName(file));

                if (!name.Success)
                {
                    continue;
                }

                int found = int.Parse(name.Groups[1].Value, CultureInfo.InvariantCulture);

                if (
                    version != null
                    && version.StartsWith("3." + found.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                )
                {
                    minor = found;
                    return file;
                }

                if (found > minor)
                {
                    minor = found;
                    best = file;
                }
            }

            return best;
        }

        /// <summary>Reads <c>home</c> and <c>version</c> (or the first three parts of <c>version_info</c>) of a <c>pyvenv.cfg</c>.</summary>
        private static void ReadVenv(string path, out string home, out string version)
        {
            home = null;
            version = null;
            string info = null;

            foreach (string line in File.ReadAllLines(path))
            {
                int equals = line.IndexOf('=');

                if (equals < 0)
                {
                    continue;
                }

                string key = line.Substring(0, equals).Trim();
                string value = line.Substring(equals + 1).Trim();

                if (key.Equals("home", StringComparison.OrdinalIgnoreCase))
                {
                    home = value;
                }
                else if (key.Equals("version", StringComparison.OrdinalIgnoreCase))
                {
                    version = value;
                }
                else if (key.Equals("version_info", StringComparison.OrdinalIgnoreCase))
                {
                    info = value;
                }
            }

            if (version == null && info != null)
            {
                string[] parts = info.Split('.');
                version = parts.Length >= 3 ? parts[0] + "." + parts[1] + "." + parts[2] : info;
            }
        }

        /// <summary>The machine field of the PE header of a file, 0x8664 for x64 and 0x14C for x86; 0 where it is not a PE file.</summary>
        private static int ReadMachine(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    if (stream.Length < 0x40 || reader.ReadUInt16() != 0x5A4D)
                    {
                        return 0;
                    }

                    stream.Position = 0x3C;
                    int header = reader.ReadInt32();

                    if (header < 0 || header + 6 > stream.Length)
                    {
                        return 0;
                    }

                    stream.Position = header;
                    return reader.ReadUInt32() == 0x4550 ? reader.ReadUInt16() : 0;
                }
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }

        private CadPythonEnvironment FromRegistry(IReadOnlyList<CadPythonRegistration> registered)
        {
            Source = "Registry (PEP 514)";
            var usable = new List<Candidate>();

            foreach (CadPythonRegistration entry in registered)
            {
                string reason = Candidate.Judge(entry, out Candidate candidate);

                if (reason != null)
                {
                    _passedOver.Add(entry.Tag + ": " + reason);
                }
                else
                {
                    usable.Add(candidate);
                }
            }

            Candidate chosen = null;

            foreach (Candidate candidate in usable)
            {
                if (
                    chosen == null
                    || candidate.Minor > chosen.Minor
                    || (
                        candidate.Minor == chosen.Minor && candidate.Entry.Hive == "HKCU" && chosen.Entry.Hive != "HKCU"
                    )
                )
                {
                    chosen = candidate;
                }
            }

            foreach (Candidate candidate in usable)
            {
                if (!ReferenceEquals(candidate, chosen))
                {
                    _passedOver.Add(candidate.Entry.Tag + ": " + chosen.Entry.Tag + " was chosen instead");
                }
            }

            if (chosen == null)
            {
                Source = null;
                return Fail(
                    "No Python 3.11 to 3.15 (64-bit) was found. Install one from python.org, or set its python.exe in Global settings > Python."
                );
            }

            Executable = chosen.Executable;
            Library = chosen.Library;
            Version = chosen.Entry.Version ?? "3." + chosen.Minor.ToString(CultureInfo.InvariantCulture);
            Problem = null;
            return this;
        }

        /// <summary>A registered Python that passed every check, with the numbers the choice needs.</summary>
        private sealed class Candidate
        {
            internal CadPythonRegistration Entry;
            internal string Executable;
            internal string Library;
            internal int Minor;

            /// <summary>The reason an entry cannot be used, or null with the candidate it makes.</summary>
            internal static string Judge(CadPythonRegistration entry, out Candidate candidate)
            {
                candidate = null;

                if (string.IsNullOrEmpty(entry.InstallPath) && string.IsNullOrEmpty(entry.ExecutablePath))
                {
                    return "no InstallPath";
                }

                string exe = !string.IsNullOrEmpty(entry.ExecutablePath)
                    ? entry.ExecutablePath
                    : Path.Combine(entry.InstallPath, "python.exe");

                if (!File.Exists(exe))
                {
                    return exe + " is missing";
                }

                bool is32 =
                    string.Equals(entry.SysArchitecture, "32bit", StringComparison.OrdinalIgnoreCase)
                    || (entry.SysArchitecture == null && entry.Tag.EndsWith("-32", StringComparison.Ordinal));

                if (is32)
                {
                    return exe + " is a 32-bit Python";
                }

                string folder = !string.IsNullOrEmpty(entry.InstallPath)
                    ? entry.InstallPath
                    : Path.GetDirectoryName(exe);
                string library = PickDll(folder, entry.Version ?? entry.Tag, out int minor);

                if (library == null)
                {
                    return "python3XY.dll is missing in " + folder;
                }

                if (minor < OldestMinor || minor > NewestMinor)
                {
                    return "Python 3." + minor.ToString(CultureInfo.InvariantCulture) + " is outside 3.11 to 3.15";
                }

                if (ReadMachine(library) != 0x8664)
                {
                    return library + " is not an x64 DLL";
                }

                candidate = new Candidate
                {
                    Entry = entry,
                    Executable = exe,
                    Library = library,
                    Minor = minor,
                };
                return null;
            }
        }

        /// <summary>Reads <c>Software\Python\PythonCore\&lt;tag&gt;</c> of HKCU, then HKLM, the tags of each in ordinal order.</summary>
        internal static IReadOnlyList<CadPythonRegistration> ReadRegistry()
        {
            var entries = new List<CadPythonRegistration>();

            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            {
                try
                {
                    using (RegistryKey root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                    using (RegistryKey core = root.OpenSubKey(@"Software\Python\PythonCore"))
                    {
                        if (core == null)
                        {
                            continue;
                        }

                        string[] tags = core.GetSubKeyNames();
                        Array.Sort(tags, StringComparer.Ordinal);

                        foreach (string tag in tags)
                        {
                            using (RegistryKey key = core.OpenSubKey(tag))
                            using (RegistryKey install = key?.OpenSubKey("InstallPath"))
                            {
                                entries.Add(
                                    new CadPythonRegistration
                                    {
                                        Hive = hive == RegistryHive.CurrentUser ? "HKCU" : "HKLM",
                                        Tag = tag,
                                        InstallPath = install?.GetValue(null) as string,
                                        ExecutablePath = install?.GetValue("ExecutablePath") as string,
                                        SysArchitecture = key?.GetValue("SysArchitecture") as string,
                                        Version = key?.GetValue("Version") as string,
                                    }
                                );
                            }
                        }
                    }
                }
                catch (Exception exception)
                    when (exception is System.Security.SecurityException
                        || exception is UnauthorizedAccessException
                        || exception is IOException
                    )
                {
                    GeometryHelperLog.Debug("CadPythonEnvironment: the registry could not be read.", exception);
                }
            }

            return entries;
        }
    }

    /// <summary>One <c>Software\Python\PythonCore\&lt;tag&gt;</c> key of the registry (PEP 514), as the finder reads it; a test makes them.</summary>
    internal sealed class CadPythonRegistration
    {
        /// <summary>HKCU or HKLM.</summary>
        internal string Hive { get; set; }

        /// <summary>The key's name: <c>3.14</c>.</summary>
        internal string Tag { get; set; }

        /// <summary>The default value of <c>InstallPath</c>; null where there is none.</summary>
        internal string InstallPath { get; set; }

        /// <summary>The <c>ExecutablePath</c> value of <c>InstallPath</c>; null where there is none.</summary>
        internal string ExecutablePath { get; set; }

        /// <summary>The <c>SysArchitecture</c> value: <c>64bit</c>; null where there is none.</summary>
        internal string SysArchitecture { get; set; }

        /// <summary>The <c>Version</c> value: <c>3.14.4</c>; null where there is none.</summary>
        internal string Version { get; set; }
    }
}
