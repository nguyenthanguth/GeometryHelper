using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Startup Suite of Load/Unload Applications: the AutoLISP files loaded, in this order, into every drawing opened,
    /// after <c>acad.lsp</c> and <c>acaddoc.lsp</c> and before <c>S::STARTUP</c>. The list is
    /// <see cref="GlobalSettings.LispStartupSuite"/>, saved the moment it changes; a change applies to the drawings opened
    /// afterwards, not to the ones open now. The suite holds .lsp files only: a plugin loads at start from the Plugin
    /// manager (At start).
    /// </summary>
    public sealed class StartupSuiteViewModel : ObservableObject
    {
        private readonly MainViewModel _model;
        private string _selected;
        private string _message = string.Empty;

        internal StartupSuiteViewModel(MainViewModel model)
        {
            _model = model;
        }

        /// <summary>Gets the files of the suite, as full paths in the order they are loaded.</summary>
        public IReadOnlyList<string> Files => _model.Settings.LispStartupSuite;

        /// <summary>Gets whether the suite holds no file.</summary>
        public bool IsEmpty => Files.Count == 0;

        /// <summary>Gets or sets the file chosen in the list; null for none.</summary>
        public string Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(CanRemove));
                }
            }
        }

        /// <summary>Gets whether Remove can take a file out: one is chosen.</summary>
        public bool CanRemove => _selected != null && Files.Contains(_selected, StringComparer.OrdinalIgnoreCase);

        /// <summary>Gets why the last file offered was refused, or an empty text where none was.</summary>
        public string Message => _message;

        /// <summary>Gets the address of the guide where Help opens, at the Startup Suite.</summary>
        public string HelpUrl => LispHelp.StartupSuite;

        /// <summary>
        /// Gets or sets how Add… asks for files: it shows an Open dialog for AutoLISP files (*.lsp), several allowed, and gives
        /// the paths chosen; null where none was. Not set, Add… does nothing.
        /// </summary>
        public Func<IReadOnlyList<string>> AskFiles { get; set; }

        /// <summary>Add…: asks for AutoLISP files with <see cref="AskFiles"/> and adds them as <see cref="Add(IEnumerable{string})"/> does.</summary>
        public void Add()
        {
            IReadOnlyList<string> paths = AskFiles?.Invoke();

            if (paths != null && paths.Count > 0)
            {
                Add(paths);
            }
        }

        /// <summary>
        /// Adds files to the end of the suite, saved at once; a file already in it, either case, is not added twice. A .dll is
        /// refused with the words that point to the Plugin manager, and any file that is not .lsp with the words that say the
        /// suite holds AutoLISP files only; <see cref="Message"/> says the last refusal.
        /// </summary>
        /// <param name="paths">The files.</param>
        /// <returns>How many files were added.</returns>
        public int Add(IEnumerable<string> paths)
        {
            string refused = string.Empty;
            var files = _model.Settings.LispStartupSuite.ToList();
            int before = files.Count;

            foreach (string path in paths ?? Enumerable.Empty<string>())
            {
                string full;

                try
                {
                    full = Path.GetFullPath(path ?? string.Empty);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is IOException)
                {
                    refused = path + " is not a valid file name.";
                    continue;
                }

                string name = Path.GetFileName(full);
                string extension = Path.GetExtension(name);

                if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    refused = "Plugins load at start from the Plugin manager (At start).";
                }
                else if (!string.Equals(extension, ".lsp", StringComparison.OrdinalIgnoreCase))
                {
                    refused = name + " is not an AutoLISP file: the Startup Suite holds .lsp files only.";
                }
                else if (!files.Contains(full, StringComparer.OrdinalIgnoreCase))
                {
                    files.Add(full);
                }
            }

            if (files.Count > before)
            {
                _model.ChangeSettings(s => s.LispStartupSuite = files, "the Startup Suite was changed");
                Raise(nameof(Files));
                Raise(nameof(IsEmpty));
            }

            _message = refused;
            Raise(nameof(Message));
            return files.Count - before;
        }

        /// <summary>Remove: takes the file chosen out of the suite, saved at once; the file stays on the disk. Nothing where none is chosen.</summary>
        public void Remove()
        {
            if (!CanRemove)
            {
                return;
            }

            string gone = _selected;
            _model.ChangeSettings(
                s =>
                    s.LispStartupSuite = s
                        .LispStartupSuite.Where(f => !string.Equals(f, gone, StringComparison.OrdinalIgnoreCase))
                        .ToList(),
                "the Startup Suite was changed"
            );
            _selected = null;
            _message = string.Empty;
            Raise(nameof(Files));
            Raise(nameof(IsEmpty));
            Raise(nameof(Selected));
            Raise(nameof(CanRemove));
            Raise(nameof(Message));
        }
    }
}
