using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// What AutoCAD loads when a drawing is opened, in its order: <c>acad.lsp</c> from the support folders once for the
    /// session of the program (for every drawing with <see cref="AcadLspAsDoc"/>), <c>acaddoc.lsp</c> for every drawing,
    /// then, which AutoCAD has no step for, each <c>.lsp</c> file of the <c>AutoLisps</c> folder beside the program
    /// (<see cref="CadSupportKind.AutoLisps"/>), each file of the Startup Suite, and last the function <c>S::STARTUP</c> where a
    /// file defined one. An error in a step is written and the next step runs.
    /// </summary>
    /// <remarks>
    /// One instance belongs to one run of the program: it remembers that <c>acad.lsp</c> was loaded, so that the second
    /// drawing it is given to does not load it again. The window gives each drawing it opens to <see cref="OnDocumentOpened"/>.
    /// The AutoLisps folder takes the place AutoCAD gives the files that come with a program, a menu's <c>.mnl</c>, between
    /// acaddoc.lsp and the Startup Suite. Its files load in name order, ignoring case; a file in a subfolder is not loaded
    /// (<c>(load "Sub/x")</c> reaches it), and <c>acad.lsp</c> and <c>acaddoc.lsp</c> put there are the two files of those
    /// names, found along the search path after the user's folders, and not loaded a second time here.
    /// </remarks>
    public sealed class LispStartup
    {
        private bool _acadLoaded;

        /// <summary>Gets or sets whether <c>acad.lsp</c> is loaded into every drawing opened, as AutoCAD's ACADLSPASDOC 1, and not once for the session. False by default.</summary>
        public bool AcadLspAsDoc { get; set; }

        /// <summary>Gets the files of the Startup Suite, each a full path, in the order they are loaded.</summary>
        public IList<string> StartupSuite { get; } = new List<string>();

        /// <summary>
        /// Runs the startup of a drawing in its session: <c>acad.lsp</c> found along the document's search path (the first
        /// time this is called, or every time with <see cref="AcadLspAsDoc"/>), <c>acaddoc.lsp</c>, every <c>.lsp</c> file
        /// directly in the AutoLisps folder, the Startup Suite and <c>S::STARTUP</c>. What the files write goes to the
        /// session's history; a file not found is a <c>; error: LOAD failed:</c> line for a file of the Startup Suite and no
        /// line for the two named files. A file of the AutoLisps folder that errors is written and the next one loads; a
        /// compiled <c>.fas</c> or <c>.vlx</c> there is the compiled-AutoLISP refusal, once for the drawing.
        /// </summary>
        /// <param name="session">The session of the drawing just opened.</param>
        /// <exception cref="ArgumentNullException">Thrown when the session is null.</exception>
        public void OnDocumentOpened(CadCommandSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (!_acadLoaded || AcadLspAsDoc)
            {
                _acadLoaded = true;
                LoadFound(session, "acad.lsp");
            }

            LoadFound(session, "acaddoc.lsp");
            LoadAutoLisps(session);

            foreach (string file in StartupSuite)
            {
                if (!string.IsNullOrWhiteSpace(file))
                {
                    session.RunHosted(() => session.Lisp.Load(file.Trim()));
                }
            }

            // A defun-q makes S::STARTUP a list, which AutoCAD's documents recommend so that routines can add to it.
            // The (princ) after the call keeps its value off the command line, as AutoCAD does not write it either.
            if (IsCallable(session.Lisp, session.Lisp.GetValue("S::STARTUP")))
            {
                session.RunHosted(() => session.Lisp.Run("(progn (S::STARTUP) (princ))"));
            }
        }

        /// <summary>Tells whether a value can be called: a function, or a list made like one, as <c>defun-q</c> makes.</summary>
        private static bool IsCallable(LispInterpreter lisp, object value)
        {
            try
            {
                return lisp.AsFunction(value) != null;
            }
            catch (LispException)
            {
                return false;
            }
        }

        /// <summary>
        /// Loads each file of the AutoLisps folder, in the order of its name ignoring case: the <c>.lsp</c> files, and the compiled
        /// ones too, which <c>load</c> refuses with its own line. <c>acad.lsp</c> and <c>acaddoc.lsp</c> are left to their own
        /// steps. A folder that is missing or cannot be read has no files.
        /// </summary>
        private static void LoadAutoLisps(CadCommandSession session)
        {
            foreach (string folder in session.Document.SupportRoots.ExistingOf(CadSupportKind.AutoLisps))
            {
                string[] paths;

                try
                {
                    paths = Directory.GetFiles(folder);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    continue;
                }

                var files = new List<string>();

                foreach (string path in paths)
                {
                    string name = Path.GetFileName(path);
                    string extension = Path.GetExtension(name);

                    if (
                        (
                            string.Equals(extension, ".lsp", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(extension, ".fas", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(extension, ".vlx", StringComparison.OrdinalIgnoreCase)
                        )
                        && !string.Equals(name, "acad.lsp", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(name, "acaddoc.lsp", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        files.Add(path);
                    }
                }

                files.Sort(
                    (a, b) =>
                    {
                        string x = Path.GetFileName(a);
                        string y = Path.GetFileName(b);
                        int order = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
                        return order != 0 ? order : string.CompareOrdinal(x, y);
                    }
                );

                foreach (string path in files)
                {
                    string file = path;
                    session.RunHosted(() => session.Lisp.Load(file));
                }
            }
        }

        /// <summary>Loads a file of this name from the first folder of the search path that has one; does nothing where none does.</summary>
        private static void LoadFound(CadCommandSession session, string name)
        {
            foreach (string folder in session.Lisp.SupportFolders)
            {
                string path;

                try
                {
                    path = Path.Combine(folder, name);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (File.Exists(path))
                {
                    session.RunHosted(() => session.Lisp.Load(path));
                    return;
                }
            }
        }
    }
}
