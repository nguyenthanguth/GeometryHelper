using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD 2021's Insert Object dialog over INSERTOBJ's file job (design InsertObject and InsertObjectNew): Create New,
    /// whose Object Type list is empty since the presenter makes no OLE object, and Create from File with its File box, its
    /// Browse... and Link and Display As Icon greyed, the object being embedded as a static picture. OK gives the file to
    /// INSERTOBJ, which takes a bitmap or a PNG.
    /// </summary>
    public sealed class InsertObjectViewModel : ObservableObject
    {
        private readonly CadExportJob _job;
        private bool _fromFile = true;
        private string _file;

        /// <summary>Makes the dialog over INSERTOBJ's job.</summary>
        /// <param name="job">The job; its file name is set by <see cref="Accept"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when the job is null.</exception>
        public InsertObjectViewModel(CadExportJob job)
        {
            _job = job ?? throw new ArgumentNullException(nameof(job));
            _file = job.FileName ?? string.Empty;
        }

        /// <summary>Gets or sets whether Create from File is picked; it is when the dialog opens.</summary>
        public bool IsFromFile
        {
            get => _fromFile;
            set
            {
                if (Set(ref _fromFile, value))
                {
                    Raise(nameof(IsCreateNew));
                    Raise(nameof(CanAccept));
                    Raise(nameof(Result));
                }
            }
        }

        /// <summary>Gets or sets whether Create New is picked.</summary>
        public bool IsCreateNew
        {
            get => !_fromFile;
            set => IsFromFile = !value;
        }

        /// <summary>Gets the Object Type list of Create New: none, the presenter making no OLE object.</summary>
        public IReadOnlyList<string> ObjectTypes { get; } = Array.Empty<string>();

        /// <summary>Gets whether Link may be ticked: never, a picture inserted is embedded.</summary>
        public bool CanLink => false;

        /// <summary>Gets or sets the file of Create from File, typed or chosen by Browse...</summary>
        public string File
        {
            get => _file;
            set
            {
                if (Set(ref _file, value ?? string.Empty))
                {
                    Raise(nameof(CanAccept));
                }
            }
        }

        /// <summary>Gets whether OK is on: Create from File with a file named.</summary>
        public bool CanAccept => _fromFile && _file.Trim().Trim('"').Length > 0;

        /// <summary>Gets the words of Result.</summary>
        public string Result =>
            _fromFile
                ? "Inserts the contents of the file into your drawing as a picture, at its own size at the upper left of the view."
                : "No object type can be made here. Pick Create from File to insert a bitmap or a PNG.";

        /// <summary>Gets or sets how the window shows the Browse dialog for the job, which sets the job's file name; null for none.</summary>
        public Action<CadExportJob> BrowseFile { get; set; }

        /// <summary>Browse...: the window's Open dialog for a bitmap or a PNG, titled Insert Object; the file chosen is put in File.</summary>
        public void Browse()
        {
            if (BrowseFile == null)
            {
                return;
            }

            string before = _job.FileName;
            _job.FileName = _file.Length > 0 ? _file : null;
            BrowseFile(_job);
            string chosen = _job.FileName;
            _job.FileName = before;

            if (!string.IsNullOrEmpty(chosen))
            {
                File = chosen;
            }
        }

        /// <summary>OK: the file named is the one INSERTOBJ inserts; false where OK is off.</summary>
        /// <returns>Whether the dialog may close.</returns>
        public bool Accept()
        {
            if (!CanAccept)
            {
                return false;
            }

            _job.FileName = _file.Trim().Trim('"');
            return true;
        }

        /// <summary>Cancel: INSERTOBJ inserts nothing.</summary>
        public void Cancel() => _job.FileName = null;
    }

    /// <summary>A copy autosave kept, as a row of the Drawing Recovery Manager.</summary>
    public sealed class BackupFileRow
    {
        internal BackupFileRow(string path, string original)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
            Original = original;
            Folder = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path)) ?? string.Empty;

            try
            {
                var info = new FileInfo(path);
                Saved = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                Size = Math.Max(1L, (info.Length + 1023L) / 1024L).ToString(CultureInfo.InvariantCulture) + " KB";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Saved = string.Empty;
                Size = string.Empty;
            }
        }

        /// <summary>Gets the full name of the copy.</summary>
        public string Path { get; }

        /// <summary>Gets the name of the copy: "plan.autosave1.dxf".</summary>
        public string Name { get; }

        /// <summary>Gets the name of the drawing it is a copy of: "plan.dxf".</summary>
        public string Original { get; }

        /// <summary>Gets the folder it lies in.</summary>
        public string Folder { get; }

        /// <summary>Gets when it was saved: "2026-11-13 08:12".</summary>
        public string Saved { get; }

        /// <summary>Gets its size: "124 KB".</summary>
        public string Size { get; }
    }

    /// <summary>
    /// AutoCAD 2021's Drawing Recovery Manager palette over the copies autosave keeps beside the drawings listed (design
    /// DrawingRecovery): each copy with the drawing it is of, when it was saved and its size; Open opens the one picked.
    /// </summary>
    public sealed class DrawingRecoveryViewModel : ObservableObject
    {
        private readonly Action<string> _open;
        private BackupFileRow _selected;

        /// <summary>Makes the palette over the copies.</summary>
        /// <param name="copies">The copies, in the order listed.</param>
        /// <param name="open">What opens a copy by its full name.</param>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public DrawingRecoveryViewModel(IEnumerable<BackupFileRow> copies, Action<string> open)
        {
            Copies = (copies ?? throw new ArgumentNullException(nameof(copies))).ToList();
            _open = open ?? throw new ArgumentNullException(nameof(open));
            _selected = Copies.FirstOrDefault();
        }

        /// <summary>Gets the copies, in the order DRAWINGRECOVERY lists them on the command line.</summary>
        public IReadOnlyList<BackupFileRow> Copies { get; }

        /// <summary>Gets or sets the copy picked; the first when the palette opens.</summary>
        public BackupFileRow Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(CanOpen));
                }
            }
        }

        /// <summary>Gets whether Open is on: a copy is picked.</summary>
        public bool CanOpen => _selected != null;

        /// <summary>Gets the line under the list: that there is no copy; empty where there are some.</summary>
        public string Message =>
            Copies.Count == 0 ? "No backup files were found beside the drawings listed." : string.Empty;

        /// <summary>Open: the copy picked is opened; the palette stays.</summary>
        /// <returns>Whether one was picked.</returns>
        public bool Open()
        {
            if (_selected == null)
            {
                return false;
            }

            _open(_selected.Path);
            return true;
        }
    }

    /// <summary>
    /// AutoCAD 2021's Check Spelling Settings dialog, from Settings... of Check Spelling (design CheckSpellingSettings): Include
    /// (Dimension Text and External References greyed, the presenter checking neither) and the words passed over. It edits a
    /// copy, taken on OK.
    /// </summary>
    public sealed class CheckSpellingSettingsViewModel : ObservableObject
    {
        /// <summary>Makes the dialog over a copy of the settings.</summary>
        /// <param name="settings">The settings; null for AutoCAD's defaults.</param>
        public CheckSpellingSettingsViewModel(CadSpellSettings settings)
        {
            Settings = settings?.Clone() ?? new CadSpellSettings();
        }

        /// <summary>Gets the copy the dialog edits.</summary>
        public CadSpellSettings Settings { get; }

        /// <summary>Gets or sets Dimension Text.</summary>
        public bool DimensionText
        {
            get => Settings.DimensionText;
            set => Settings.DimensionText = value;
        }

        /// <summary>Gets or sets Block Attributes.</summary>
        public bool BlockAttributes
        {
            get => Settings.BlockAttributes;
            set => Settings.BlockAttributes = value;
        }

        /// <summary>Gets or sets External References.</summary>
        public bool ExternalReferences
        {
            get => Settings.ExternalReferences;
            set => Settings.ExternalReferences = value;
        }

        /// <summary>Gets whether Dimension Text may be changed: no.</summary>
        public bool CanCheckDimensionText => Settings.CanCheckDimensionText;

        /// <summary>Gets whether External References may be changed: no.</summary>
        public bool CanCheckExternalReferences => Settings.CanCheckExternalReferences;

        /// <summary>Gets or sets Ignore capitalized words.</summary>
        public bool IgnoreCapitalized
        {
            get => Settings.IgnoreCapitalized;
            set => Settings.IgnoreCapitalized = value;
        }

        /// <summary>Gets or sets Ignore words with mixed cases.</summary>
        public bool IgnoreMixedCase
        {
            get => Settings.IgnoreMixedCase;
            set => Settings.IgnoreMixedCase = value;
        }

        /// <summary>Gets or sets Ignore words in uppercase.</summary>
        public bool IgnoreUppercase
        {
            get => Settings.IgnoreUppercase;
            set => Settings.IgnoreUppercase = value;
        }

        /// <summary>Gets or sets Ignore words with numbers.</summary>
        public bool IgnoreWithNumbers
        {
            get => Settings.IgnoreWithNumbers;
            set => Settings.IgnoreWithNumbers = value;
        }

        /// <summary>Gets or sets Ignore words containing punctuation.</summary>
        public bool IgnorePunctuation
        {
            get => Settings.IgnorePunctuation;
            set => Settings.IgnorePunctuation = value;
        }
    }
}
