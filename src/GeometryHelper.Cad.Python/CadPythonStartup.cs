using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Cad.Python
{
    /// <summary>
    /// The Python files that load when the app starts, as the Lisps folder and the Startup Suite do for AutoLISP, but once for
    /// the process: Python commands are added to every drawing, so loading them again at each drawing opened would load each
    /// file again and replace its commands. <see cref="FilesToLoad"/> lists the files and touches no Python; <see cref="Run"/>
    /// loads them, and starts the engine only if there is a file to load.
    /// </summary>
    public sealed class CadPythonStartup
    {
        private readonly CadPythonHost _host;
        private bool _ran;
        private bool _started;

        /// <summary>Makes the startup over a host; nothing is loaded, and Python is not started, until <see cref="Run"/>.</summary>
        /// <param name="host">The Python host.</param>
        /// <exception cref="ArgumentNullException">Thrown when the host is null.</exception>
        public CadPythonStartup(CadPythonHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// <summary>
        /// Lists the files to load at start: the <c>.py</c> files directly in each Pythons folder, the folders in the order
        /// given and the files of each by name ignoring case, then by the name as written (a subfolder is a package the files
        /// import, not a startup file), then those of the startup suite in its order. A folder that does not exist is passed
        /// over; a file reached twice, by its full path ignoring case, is listed once, where it first came.
        /// </summary>
        /// <param name="pythonsFolders">The Pythons folders.</param>
        /// <param name="startupSuite">The files of the Python Startup Suite, as full paths.</param>
        /// <returns>The full paths, in the order they load.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public static IReadOnlyList<string> FilesToLoad(
            IEnumerable<string> pythonsFolders,
            IEnumerable<string> startupSuite
        )
        {
            if (pythonsFolders == null)
            {
                throw new ArgumentNullException(nameof(pythonsFolders));
            }

            if (startupSuite == null)
            {
                throw new ArgumentNullException(nameof(startupSuite));
            }

            var files = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string folder in pythonsFolders)
            {
                if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                {
                    continue;
                }

                var here = new List<string>();

                foreach (string file in Directory.GetFiles(folder))
                {
                    if (string.Equals(Path.GetExtension(file), ".py", StringComparison.OrdinalIgnoreCase))
                    {
                        here.Add(file);
                    }
                }

                here.Sort(CompareNames);

                foreach (string file in here)
                {
                    Add(files, seen, file);
                }
            }

            foreach (string file in startupSuite)
            {
                if (!string.IsNullOrWhiteSpace(file))
                {
                    Add(files, seen, file.Trim());
                }
            }

            return files;
        }

        /// <summary>
        /// Loads the files the first time it is called, each as <see cref="CadPythonHost.LoadFile"/> does, a file that fails
        /// writing its line and the next one loading, and does nothing after. With no file it does nothing and Python is not
        /// started; it may be called again. Where the engine cannot start, one line,
        /// <c>Python files were not loaded: </c> and the environment's problem, is written to the command line, and the files
        /// are not tried.
        /// </summary>
        /// <param name="session">The command session of the first drawing, which the files load in.</param>
        /// <param name="files">The files, as <see cref="FilesToLoad"/> lists them.</param>
        /// <returns>False when Python could not start; true otherwise, also when a file failed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public bool Run(CadCommandSession session, IReadOnlyList<string> files)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            if (_ran)
            {
                return _started;
            }

            if (files.Count == 0)
            {
                return true;
            }

            _ran = true;
            _started = _host.StartForFiles(session);

            if (!_started)
            {
                return false;
            }

            foreach (string file in files)
            {
                _host.LoadFile(session, file);
            }

            return true;
        }

        private static void Add(List<string> files, HashSet<string> seen, string file)
        {
            string full;

            try
            {
                full = Path.GetFullPath(file);
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                )
            {
                full = file;
            }

            if (seen.Add(full))
            {
                files.Add(full);
            }
        }

        private static int CompareNames(string a, string b)
        {
            int order = string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase);

            if (order == 0)
            {
                order = string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b));
            }

            return order != 0 ? order : string.CompareOrdinal(a, b);
        }
    }
}
