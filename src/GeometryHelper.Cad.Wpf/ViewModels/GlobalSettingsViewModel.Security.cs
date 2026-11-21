using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// A folder of the Security page's list of trusted folders: the program's own, which is always trusted and cannot be
    /// changed, or an entry of TRUSTEDPATHS shown as it is kept (<c>D:\CAD\Company\...</c>, the dots trusting the folders under
    /// it too), marked "(not found)" where its folder is not there.
    /// </summary>
    public sealed class TrustedFolderRow : ObservableObject
    {
        private const string Subfolders = "\\...";

        private string _entry;

        internal TrustedFolderRow(string entry, bool isProgram)
        {
            _entry = entry ?? string.Empty;
            IsProgramFolder = isProgram;
        }

        /// <summary>Gets whether this is the folder of the program, always trusted with every folder under it.</summary>
        public bool IsProgramFolder { get; }

        /// <summary>Gets the entry as it is kept: a folder, or a folder and <c>\...</c> for its subfolders too.</summary>
        public string Entry
        {
            get => _entry;
            internal set
            {
                if (Set(ref _entry, value ?? string.Empty))
                {
                    Raise(nameof(IncludesSubfolders));
                    Raise(nameof(MissingMark));
                }
            }
        }

        /// <summary>Gets whether the entry ends in <c>\...</c> and so trusts the folders under its folder too.</summary>
        public bool IncludesSubfolders => IsProgramFolder || Entry.EndsWith(Subfolders, StringComparison.Ordinal);

        /// <summary>Gets the words after the program's folder: (program folder, always trusted); empty for any other.</summary>
        public string ProgramMark => IsProgramFolder ? " (program folder, always trusted)" : string.Empty;

        /// <summary>Gets " (not found)" for an entry whose folder is missing or does not start at a drive or a share, which trusts nothing; empty otherwise.</summary>
        public string MissingMark => IsProgramFolder || FolderExists(Entry) ? string.Empty : " (not found)";

        /// <inheritdoc/>
        public override string ToString() => Entry + ProgramMark + MissingMark;

        internal static bool FolderExists(string entry)
        {
            string folder = entry.EndsWith(Subfolders, StringComparison.Ordinal)
                ? entry.Substring(0, entry.Length - Subfolders.Length)
                : entry;

            try
            {
                return Path.IsPathRooted(folder) && Directory.Exists(folder);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    // The Security page: SECURELOAD, the trusted folders (TRUSTEDPATHS), LEGACYCODESEARCH and the files answered Always Load.
    // Every change is made on the settings being edited and reaches the app with Apply or OK, as the other pages' do.
    public sealed partial class GlobalSettingsViewModel
    {
        private const string Subfolders = "\\...";

        private TrustedFolderRow _trustedSelected;

        /// <summary>Gets whether the Security page is shown.</summary>
        public bool IsSecurityPage => _category.Name == "Security";

        /// <summary>Gets or sets SECURELOAD: 0 loads from anywhere, 1 warns outside the trusted folders, 2 loads from them only.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not 0, 1 or 2.</exception>
        public int SecureLoad
        {
            get => _working.SecureLoad;
            set
            {
                _working.SecureLoad = value;
                Raise(nameof(SecureLoad));
                Raise(nameof(IsSecureLoadLeast));
                Raise(nameof(IsSecureLoadWarn));
                Raise(nameof(IsSecureLoadMost));
            }
        }

        /// <summary>Gets or sets the radio "Least secure setting": SECURELOAD 0. Setting it false does nothing; another radio takes the choice.</summary>
        public bool IsSecureLoadLeast
        {
            get => SecureLoad == 0;
            set
            {
                if (value)
                {
                    SecureLoad = 0;
                }
            }
        }

        /// <summary>Gets or sets the radio "Load executable files from Trusted locations, and display a warning": SECURELOAD 1.</summary>
        public bool IsSecureLoadWarn
        {
            get => SecureLoad == 1;
            set
            {
                if (value)
                {
                    SecureLoad = 1;
                }
            }
        }

        /// <summary>Gets or sets the radio "Most secure setting": SECURELOAD 2.</summary>
        public bool IsSecureLoadMost
        {
            get => SecureLoad == 2;
            set
            {
                if (value)
                {
                    SecureLoad = 2;
                }
            }
        }

        /// <summary>Gets or sets whether a search for a file of code looks in the Start In and drawing folders: LEGACYCODESEARCH 1.</summary>
        public bool LegacyCodeSearch
        {
            get => _working.LegacyCodeSearch;
            set
            {
                _working.LegacyCodeSearch = value;
                Raise(nameof(LegacyCodeSearch));
                Raise(nameof(LegacyCodeSearchExcluded));
            }
        }

        /// <summary>Gets or sets the radio "Exclude the Start In or drawing folders": LEGACYCODESEARCH 0. Setting it false does nothing.</summary>
        public bool LegacyCodeSearchExcluded
        {
            get => !LegacyCodeSearch;
            set
            {
                if (value)
                {
                    LegacyCodeSearch = false;
                }
            }
        }

        /// <summary>Gets the trusted folders as the list shows them: the program's folder first, then the entries of TRUSTEDPATHS in order.</summary>
        public ObservableCollection<TrustedFolderRow> TrustedFolders { get; } =
            new ObservableCollection<TrustedFolderRow>();

        /// <summary>Gets or sets the row selected in the list; null for none.</summary>
        public TrustedFolderRow SelectedTrustedFolder
        {
            get => _trustedSelected;
            set
            {
                if (Set(ref _trustedSelected, value))
                {
                    RaiseTrustedSelection();
                }
            }
        }

        /// <summary>Gets whether the selected row is an entry that can be removed, moved or given its subfolders: not the program's row, and not none.</summary>
        public bool CanChangeTrustedFolder => _trustedSelected != null && !_trustedSelected.IsProgramFolder;

        /// <summary>
        /// Gets or sets whether the selected entry trusts the folders under its folder too: ticking adds <c>\...</c> to it and
        /// unticking takes it off. Ignored where no entry is selected or the program's row is.
        /// </summary>
        public bool IncludeSubfolders
        {
            get => _trustedSelected != null && _trustedSelected.IncludesSubfolders;
            set
            {
                TrustedFolderRow row = _trustedSelected;

                if (row == null || row.IsProgramFolder || row.IncludesSubfolders == value)
                {
                    return;
                }

                if (value)
                {
                    row.Entry = row.Entry.TrimEnd('\\') + Subfolders;
                }
                else
                {
                    string folder = row.Entry.Substring(0, row.Entry.Length - Subfolders.Length);
                    // "D:\..." without its dots would be D:, the current folder of the drive: keep the root.
                    row.Entry = folder.EndsWith(":", StringComparison.Ordinal) ? folder + "\\" : folder;
                }

                WriteTrustedFolders();
                Raise(nameof(IncludeSubfolders));
            }
        }

        /// <summary>Gets the command of Add folder…: a folder asked for through <see cref="BrowseTrustedFolder"/>, then the warning of <see cref="ConfirmWritableTrustedFolder"/> where the app can write in it.</summary>
        public ICommand AddTrustedFolderCommand { get; private set; }

        /// <summary>Gets the command of Remove: the selected entry leaves the list.</summary>
        public ICommand RemoveTrustedFolderCommand { get; private set; }

        /// <summary>Gets the command of Move up: the selected entry goes one place earlier; off for the first.</summary>
        public ICommand MoveTrustedFolderUpCommand { get; private set; }

        /// <summary>Gets the command of Move down: the selected entry goes one place later; off for the last.</summary>
        public ICommand MoveTrustedFolderDownCommand { get; private set; }

        /// <summary>Gets or sets how the window asks for a folder: its path, or null where none was chosen.</summary>
        public Func<string> BrowseTrustedFolder { get; set; }

        /// <summary>
        /// Gets or sets how the window asks whether to trust a folder the app can write in, given its full path: true to
        /// continue. Null, as in a test that sets nothing, refuses a writable folder.
        /// </summary>
        public Func<string, bool> ConfirmWritableTrustedFolder { get; set; }

        /// <summary>Gets how many files were answered Always Load.</summary>
        public int AlwaysLoadedCount => _working.AlwaysLoadedFiles.Count;

        /// <summary>Gets the line "Files always loaded: 3".</summary>
        public string AlwaysLoadedText =>
            "Files always loaded: " + AlwaysLoadedCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Gets the command of Forget: every file answered Always Load is asked about again once Apply or OK takes it; off at none.</summary>
        public ICommand ForgetAlwaysLoadedCommand { get; private set; }

        /// <summary>
        /// Says whether the app can make a file in a folder, which is how AutoCAD tells a folder that is not read-only: a file
        /// is created there and deleted as it is closed.
        /// </summary>
        /// <param name="folder">The folder's full path.</param>
        /// <returns>True when a file could be made.</returns>
        internal static bool IsWritable(string folder)
        {
            try
            {
                using (
                    File.Create(
                        Path.Combine(folder, Guid.NewGuid().ToString("N") + ".tmp"),
                        1,
                        FileOptions.DeleteOnClose
                    )
                )
                {
                    return true;
                }
            }
            catch (Exception e)
                when (e is IOException
                    || e is UnauthorizedAccessException
                    || e is ArgumentException
                    || e is NotSupportedException
                )
            {
                return false;
            }
        }

        private void MakeSecurityCommands()
        {
            AddTrustedFolderCommand = new DelegateCommand(_ => AddTrustedFolder());
            RemoveTrustedFolderCommand = new DelegateCommand(_ => RemoveTrustedFolder(), _ => CanChangeTrustedFolder);
            MoveTrustedFolderUpCommand = new DelegateCommand(_ => MoveTrustedFolder(-1), _ => CanMoveTrustedFolder(-1));
            MoveTrustedFolderDownCommand = new DelegateCommand(_ => MoveTrustedFolder(1), _ => CanMoveTrustedFolder(1));
            ForgetAlwaysLoadedCommand = new DelegateCommand(
                _ =>
                {
                    _working.AlwaysLoadedFiles = Array.Empty<string>();
                    Raise(nameof(AlwaysLoadedCount));
                    Raise(nameof(AlwaysLoadedText));
                    ((DelegateCommand)ForgetAlwaysLoadedCommand).RaiseCanExecuteChanged();
                },
                _ => AlwaysLoadedCount > 0
            );
        }

        /// <summary>Fills the list from the settings edited: the program's folder, then each entry of TRUSTEDPATHS, as it is kept.</summary>
        private void LoadTrustedFolders()
        {
            TrustedFolders.Clear();
            TrustedFolders.Add(new TrustedFolderRow(CadLoadSecurity.ProgramFolder.TrimEnd('\\') + Subfolders, true));

            foreach (
                string entry in (_working.TrustedPaths ?? string.Empty)
                    .Split(';')
                    .Select(e => e.Trim())
                    .Where(e => e.Length > 0)
            )
            {
                TrustedFolders.Add(new TrustedFolderRow(entry, false));
            }

            SelectedTrustedFolder = null;
        }

        private void RaiseSecurity()
        {
            Raise(nameof(IsSecurityPage));
            Raise(nameof(SecureLoad));
            Raise(nameof(IsSecureLoadLeast));
            Raise(nameof(IsSecureLoadWarn));
            Raise(nameof(IsSecureLoadMost));
            Raise(nameof(LegacyCodeSearch));
            Raise(nameof(LegacyCodeSearchExcluded));
            Raise(nameof(AlwaysLoadedCount));
            Raise(nameof(AlwaysLoadedText));
            RaiseTrustedSelection();
        }

        private void RaiseTrustedSelection()
        {
            Raise(nameof(CanChangeTrustedFolder));
            Raise(nameof(IncludeSubfolders));

            if (RemoveTrustedFolderCommand != null)
            {
                ((DelegateCommand)RemoveTrustedFolderCommand).RaiseCanExecuteChanged();
                ((DelegateCommand)MoveTrustedFolderUpCommand).RaiseCanExecuteChanged();
                ((DelegateCommand)MoveTrustedFolderDownCommand).RaiseCanExecuteChanged();
                ((DelegateCommand)ForgetAlwaysLoadedCommand).RaiseCanExecuteChanged();
            }
        }

        /// <summary>Writes the entries of the list, in order, into TRUSTEDPATHS of the settings edited.</summary>
        private void WriteTrustedFolders()
        {
            _working.TrustedPaths = string.Join(
                ";",
                TrustedFolders.Where(r => !r.IsProgramFolder).Select(r => r.Entry)
            );
            RaiseTrustedSelection();
        }

        private void AddTrustedFolder()
        {
            string picked = BrowseTrustedFolder?.Invoke();

            if (string.IsNullOrWhiteSpace(picked))
            {
                return;
            }

            string folder;

            try
            {
                folder = Path.GetFullPath(picked);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
            {
                Notice = "\"" + picked + "\" is not a folder that can be trusted.";
                return;
            }

            // "C:\" keeps its backslash: without it the entry would be the drive's current folder.
            folder = folder.Length > 3 ? folder.TrimEnd('\\') : folder;

            if (folder.IndexOf(';') >= 0)
            {
                Notice = "A folder with ; in its name cannot be listed: TRUSTEDPATHS parts its entries with it.";
                return;
            }

            TrustedFolderRow existing = TrustedFolders.FirstOrDefault(r =>
                !r.IsProgramFolder && string.Equals(r.Entry, folder, StringComparison.OrdinalIgnoreCase)
            );

            if (existing != null)
            {
                SelectedTrustedFolder = existing;
                return;
            }

            if (IsWritable(folder) && !(ConfirmWritableTrustedFolder?.Invoke(folder) ?? false))
            {
                return;
            }

            var row = new TrustedFolderRow(folder, false);
            TrustedFolders.Add(row);
            WriteTrustedFolders();
            SelectedTrustedFolder = row;
        }

        private void RemoveTrustedFolder()
        {
            int index = TrustedFolders.IndexOf(_trustedSelected);
            TrustedFolders.RemoveAt(index);
            WriteTrustedFolders();
            SelectedTrustedFolder = TrustedFolders.Count > index ? TrustedFolders[index] : TrustedFolders[index - 1];
        }

        // The program's row is first and stays there, so the first entry has index 1.
        private bool CanMoveTrustedFolder(int step)
        {
            int index = TrustedFolders.IndexOf(_trustedSelected);
            return CanChangeTrustedFolder && index + step >= 1 && index + step < TrustedFolders.Count;
        }

        private void MoveTrustedFolder(int step)
        {
            int index = TrustedFolders.IndexOf(_trustedSelected);
            TrustedFolders.Move(index, index + step);
            WriteTrustedFolders();
        }
    }
}
