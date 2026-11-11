using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>One line of the details pane of the External References palette: its label and its value.</summary>
    public sealed class ReferenceDetailViewModel
    {
        internal ReferenceDetailViewModel(string key, string value, bool isPath)
        {
            Key = key;
            Value = value ?? string.Empty;
            IsPath = isPath;
        }

        /// <summary>Gets the label: Reference Name, Status, Size, Type, Date, Saved Path, Found At, Path type.</summary>
        public string Key { get; }

        /// <summary>Gets the value.</summary>
        public string Value { get; }

        /// <summary>Gets whether the value is a path, set in the monospaced font.</summary>
        public bool IsPath { get; }
    }

    /// <summary>
    /// One row of the External References palette: the drawing itself (Opened, Current) first, then each external reference
    /// and image with its status, size and type, as AutoCAD's palette lists them.
    /// </summary>
    public sealed class ReferenceRowViewModel
    {
        internal ReferenceRowViewModel(CadExternalReference reference, bool tree = false)
        {
            Reference = reference;
            Name = reference.Name;
            StatusText = reference.StatusText;
            TypeName = reference.IsNested && !tree ? "Nested" : reference.TypeName;
            SizeText = Size(reference.Size);
            Indent = tree ? 16 * (reference.Depth + 1) : 0;
        }

        internal ReferenceRowViewModel(CadDocument document)
        {
            Name = Path.GetFileNameWithoutExtension(document.Title);
            StatusText = "Opened";
            TypeName = "Current";
            IsCurrent = true;
            long size = -1;

            try
            {
                size = document.HasFile && File.Exists(document.FileName) ? new FileInfo(document.FileName).Length : -1;
            }
            catch (IOException)
            {
                size = -1;
            }

            SizeText = Size(size);
        }

        /// <summary>Gets the reference of the row; null for the drawing itself.</summary>
        public CadExternalReference Reference { get; }

        /// <summary>Gets the name shown.</summary>
        public string Name { get; }

        /// <summary>Gets the status in the palette's words.</summary>
        public string StatusText { get; }

        /// <summary>Gets the type: Current, Attach, Overlay, or the kind of picture; Nested for a nested reference in the list view.</summary>
        public string TypeName { get; }

        /// <summary>Gets the indent of the name in the tree view: 16 under the drawing, 16 more for each level a reference is nested; 0 in the list view.</summary>
        public double Indent { get; }

        /// <summary>Gets whether the row is a nested reference, which the drawing of another reference places.</summary>
        public bool IsNested => Reference != null && Reference.IsNested;

        /// <summary>Gets the size of its file, as 48 KB or 1.2 MB; empty where no file was found.</summary>
        public string SizeText { get; }

        /// <summary>Gets whether the row is the drawing itself.</summary>
        public bool IsCurrent { get; }

        /// <summary>Gets whether the row is an image.</summary>
        public bool IsImage => Reference != null && Reference.IsImage;

        /// <summary>Gets the colour of the status: green Loaded, red Not Found, amber Unloaded, Needs Reloading and Orphaned, the text's otherwise.</summary>
        public Brush StatusBrush
        {
            get
            {
                switch (Reference?.Status)
                {
                    case CadReferenceStatus.Loaded:
                        return Frozen(0x3F, 0xB9, 0x50);
                    case CadReferenceStatus.NotFound:
                    case CadReferenceStatus.Unresolved:
                        return Frozen(0xF8, 0x51, 0x49);
                    case CadReferenceStatus.Unloaded:
                    case CadReferenceStatus.NeedsReloading:
                    case CadReferenceStatus.Orphaned:
                        return Frozen(0xD2, 0x99, 0x22);
                    default:
                        return Frozen(0xE6, 0xE9, 0xED);
                }
            }
        }

        /// <summary>Gets the colour of the icon: red for a reference not found.</summary>
        public Brush IconBrush =>
            Reference?.Status == CadReferenceStatus.NotFound ? Frozen(0xF8, 0x51, 0x49) : Frozen(0xC9, 0xD1, 0xDB);

        /// <summary>Gets the outline of the icon: a sheet for a drawing, a sheet ticked for the drawing itself, a picture for an image.</summary>
        public System.Windows.Media.Geometry Icon =>
            System.Windows.Media.Geometry.Parse(
                IsCurrent ? "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8zM14 3v5h5M9 14l2 2 4-4"
                : IsImage ? "M3 5h18v14H3zM7 15l3-4 3 3 2-2 3 3M8 9h.01"
                : "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8zM14 3v5h5"
            );

        /// <summary>A size as the palette shows it: kilobytes rounded up below a megabyte, megabytes to one place above.</summary>
        internal static string Size(long bytes)
        {
            if (bytes < 0)
            {
                return string.Empty;
            }

            return bytes < 1024 * 1024
                ? ((bytes + 1023) / 1024).ToString(CultureInfo.InvariantCulture) + " KB"
                : (bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }

        private static Brush Frozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>
    /// AutoCAD's External References palette, which XREF (XR), EXTERNALREFERENCES (ER) and IMAGE (IM) show: the drawing and
    /// its references with their status, the details of the one selected, and what can be done with it, each through the
    /// command AutoCAD runs (-XREF, -IMAGE, XATTACH, IMAGEATTACH), so that each change is an undo step of the drawing.
    /// </summary>
    public sealed class ExternalReferencesViewModel : ObservableObject
    {
        private readonly MainViewModel _model;
        private ReferenceRowViewModel _selected;
        private IReadOnlyList<ReferenceRowViewModel> _selectedRows = new ReferenceRowViewModel[0];
        private bool _isTreeView;
        private bool _isPreview;
        private ImageSource _preview;

        /// <summary>Makes the palette over the window's view model, listing the drawing shown.</summary>
        /// <param name="model">The window's view model.</param>
        /// <exception cref="ArgumentNullException">Thrown when the model is null.</exception>
        public ExternalReferencesViewModel(MainViewModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            AttachDxfCommand = new DelegateCommand(_ => _model.StartCommand("XATTACH"), _ => Document != null);
            AttachImageCommand = new DelegateCommand(_ => _model.StartCommand("IMAGEATTACH"), _ => Document != null);
            OpenCommand = new DelegateCommand(_ => Open(), _ => IsReference && Selected.Reference.FoundAt != null);
            UnloadCommand = new DelegateCommand(_ => Run("Unload"), _ => IsOwn);
            ReloadCommand = new DelegateCommand(_ => Run("Reload"), _ => IsOwn);
            DetachCommand = new DelegateCommand(_ => Run("Detach"), _ => IsOwn);
            RefreshCommand = new DelegateCommand(_ => Refresh());
            ReloadAllCommand = new DelegateCommand(_ => ReloadAll(), _ => Document != null);
            MakeAbsoluteCommand = new DelegateCommand(
                _ => SetPathType(CadPathType.Full),
                _ => IsOwn && Selected.Reference.FoundAt != null
            );
            MakeRelativeCommand = new DelegateCommand(
                _ => SetPathType(CadPathType.Relative),
                _ => IsOwn && Selected.Reference.FoundAt != null && Document?.HasFile == true
            );
            RemovePathCommand = new DelegateCommand(
                _ => SetPathType(CadPathType.None),
                _ => IsOwn && Selected.Reference.FoundAt != null
            );
            BindCommand = new DelegateCommand(_ => Bind(), _ => SelectedRows.Any(IsBindable));
            Refresh();
        }

        /// <summary>Gets the document listed; null where none is shown.</summary>
        public CadDocument Document => _model.Document;

        /// <summary>Gets the rows: the drawing itself, then its external references, then its images.</summary>
        public ObservableCollection<ReferenceRowViewModel> Rows { get; } =
            new ObservableCollection<ReferenceRowViewModel>();

        /// <summary>Gets or sets the row selected, whose details are shown.</summary>
        public ReferenceRowViewModel Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    _preview = null;
                    Raise(nameof(Details));
                    Raise(nameof(Preview));
                    Raise(nameof(IsReference));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        /// <summary>
        /// Gets or sets the rows selected, Ctrl and Shift adding to them as in AutoCAD's palette, which Bind… binds together;
        /// the row <see cref="Selected"/> alone where they do not hold it.
        /// </summary>
        public IReadOnlyList<ReferenceRowViewModel> SelectedRows
        {
            get =>
                _selectedRows.Contains(_selected) ? _selectedRows
                : _selected == null ? new ReferenceRowViewModel[0]
                : new[] { _selected };
            set
            {
                _selectedRows = value ?? new ReferenceRowViewModel[0];
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>Gets whether the row selected is a reference or an image rather than the drawing itself.</summary>
        public bool IsReference => _selected?.Reference != null;

        /// <summary>
        /// Gets or sets whether the rows are shown as a tree under the drawing rather than as a list: each nested reference
        /// under the one whose drawing places it, of its own type, where the list shows it once, of the type Nested.
        /// </summary>
        public bool IsTreeView
        {
            get => _isTreeView;
            set
            {
                if (Set(ref _isTreeView, value))
                {
                    Refresh();
                }
            }
        }

        /// <summary>Whether the row selected is a reference or image of the drawing's own, which Unload, Reload, Detach and Change Path act on; a nested one is not.</summary>
        private bool IsOwn => IsReference && !_selected.IsNested;

        /// <summary>Gets or sets whether the lower pane shows a preview of the row selected rather than its details.</summary>
        public bool IsPreview
        {
            get => _isPreview;
            set
            {
                if (Set(ref _isPreview, value))
                {
                    Raise(nameof(Preview));
                }
            }
        }

        /// <summary>Gets the details of the row selected: name, status, size, type, date, saved path, where it was found and its path type.</summary>
        public IReadOnlyList<ReferenceDetailViewModel> Details
        {
            get
            {
                var details = new List<ReferenceDetailViewModel>();
                ReferenceRowViewModel row = _selected;

                if (row == null)
                {
                    return details;
                }

                CadExternalReference reference = row.Reference;
                string found = reference == null ? Document?.FileName : reference.FoundAt;
                DateTime? date = reference?.Date;

                if (reference == null && found != null && File.Exists(found))
                {
                    date = File.GetLastWriteTime(found);
                }

                details.Add(new ReferenceDetailViewModel("Reference Name", row.Name, false));
                details.Add(new ReferenceDetailViewModel("Status", row.StatusText, false));
                details.Add(new ReferenceDetailViewModel("Size", row.SizeText, false));
                details.Add(new ReferenceDetailViewModel("Type", row.TypeName, false));
                details.Add(
                    new ReferenceDetailViewModel(
                        "Date",
                        date?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                        false
                    )
                );
                details.Add(new ReferenceDetailViewModel("Saved Path", reference?.SavedPath ?? string.Empty, true));
                details.Add(new ReferenceDetailViewModel("Found At", found ?? string.Empty, true));

                if (reference != null)
                {
                    details.Add(new ReferenceDetailViewModel("Path type", PathTypeName(reference.PathType), false));
                }

                return details;
            }
        }

        /// <summary>Gets the preview of the row selected: the drawing referenced drawn, or the picture; null where there is none.</summary>
        public ImageSource Preview
        {
            get
            {
                if (!_isPreview || _selected?.Reference?.FoundAt == null)
                {
                    return null;
                }

                return _preview
                    ?? (
                        _preview = PreviewOf(_selected.Reference.FoundAt, _selected.IsImage, Document?.Fonts, 420, 240)
                    );
            }
        }

        /// <summary>Gets the command of Attach DXF… (XATTACH).</summary>
        public ICommand AttachDxfCommand { get; }

        /// <summary>Gets the command of Attach Image… (IMAGEATTACH).</summary>
        public ICommand AttachImageCommand { get; }

        /// <summary>Gets the command of Open: the drawing referenced opened as another drawing of the window, the picture in its own program.</summary>
        public ICommand OpenCommand { get; }

        /// <summary>Gets the command of Unload (-XREF Unload, -IMAGE Unload).</summary>
        public ICommand UnloadCommand { get; }

        /// <summary>Gets the command of Reload (-XREF Reload, -IMAGE Reload).</summary>
        public ICommand ReloadCommand { get; }

        /// <summary>Gets the command of Detach (-XREF Detach, -IMAGE Detach).</summary>
        public ICommand DetachCommand { get; }

        /// <summary>Gets the command of Refresh: the rows read again, each file looked for anew.</summary>
        public ICommand RefreshCommand { get; }

        /// <summary>Gets the command of Reload All References.</summary>
        public ICommand ReloadAllCommand { get; }

        /// <summary>Gets the command of Change Path › Make Absolute (-XREF pathType Full).</summary>
        public ICommand MakeAbsoluteCommand { get; }

        /// <summary>Gets the command of Change Path › Make Relative (-XREF pathType Relative), for a drawing saved.</summary>
        public ICommand MakeRelativeCommand { get; }

        /// <summary>Gets the command of Change Path › Remove Path (-XREF pathType None).</summary>
        public ICommand RemovePathCommand { get; }

        /// <summary>
        /// Gets the command of Bind…: the Bind Xrefs/DGN Underlays dialog for the drawings selected, then -XREF Bind with them.
        /// Offered where the rows selected hold a drawing loaded (Needs Reloading counts as loaded), a nested one too, which
        /// -XREF Bind then refuses; greyed for a selection of only drawings unloaded or not found, or of images.
        /// </summary>
        public ICommand BindCommand { get; }

        /// <summary>Reads the rows again, the row selected kept by its name where it is still there.</summary>
        public void Refresh()
        {
            string selected = _selected?.Name;
            bool wasCurrent = _selected?.IsCurrent == true;
            Rows.Clear();
            CadDocument document = Document;

            if (document != null)
            {
                Rows.Add(new ReferenceRowViewModel(document));
                var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (CadExternalReference reference in document.Xrefs.GetReferences())
                {
                    // The list shows a reference nested in two places once; the tree under each.
                    if (_isTreeView || !reference.IsNested || listed.Add(reference.Name))
                    {
                        Rows.Add(new ReferenceRowViewModel(reference, _isTreeView));
                    }
                }
            }

            ReferenceRowViewModel keep = wasCurrent
                ? Rows.FirstOrDefault()
                : Rows.FirstOrDefault(r =>
                    !r.IsCurrent && string.Equals(r.Name, selected, StringComparison.OrdinalIgnoreCase)
                );
            _selected = null;
            Selected = keep ?? Rows.FirstOrDefault(r => !r.IsCurrent) ?? Rows.FirstOrDefault();
            Raise(nameof(Details));
        }

        /// <summary>Gives a new path to the reference selected (Change Path › Select New Path): -XREF Path or -IMAGE Path with it.</summary>
        /// <param name="path">The new path.</param>
        public void SetPath(string path)
        {
            if (!IsOwn || string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string name = _selected.Name;
            _model.RunWithAnswers(_selected.IsImage ? "-IMAGE" : "-XREF", "P", name, path, string.Empty);
            Refresh();
        }

        /// <summary>The words of the palette for a path type: Relative path, Full path, No path.</summary>
        internal static string PathTypeName(CadPathType type) =>
            type == CadPathType.Relative ? "Relative path"
            : type == CadPathType.Full ? "Full path"
            : "No path";

        /// <summary>The picture of a file: a drawing's model space drawn, or the picture read at the width asked; null where it cannot be read.</summary>
        internal static ImageSource PreviewOf(string path, bool image, CadFonts fonts, int width, int height)
        {
            try
            {
                if (image)
                {
                    var picture = new BitmapImage();
                    picture.BeginInit();
                    picture.CacheOption = BitmapCacheOption.OnLoad;
                    picture.DecodePixelWidth = width;
                    picture.UriSource = new Uri(path);
                    picture.EndInit();
                    picture.Freeze();
                    return picture;
                }

                return CadStylePreview.Drawing(CadDrawingFile.Open(path), fonts, width, height);
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Debug(
                    "ExternalReferencesViewModel: no preview of " + path + ": " + exception.Message
                );
                return null;
            }
        }

        private void Open()
        {
            CadExternalReference reference = _selected?.Reference;

            if (reference?.FoundAt == null)
            {
                return;
            }

            if (reference.IsImage)
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(reference.FoundAt) { UseShellExecute = true }
                );
                return;
            }

            _model.Open(reference.FoundAt);
        }

        private void Run(string option)
        {
            if (!IsOwn)
            {
                return;
            }

            string answer =
                option == "Unload" ? "U"
                : option == "Reload" ? "R"
                : "D";
            _model.RunWithAnswers(_selected.IsImage ? "-IMAGE" : "-XREF", answer, _selected.Name);
            Refresh();
        }

        /// <summary>Whether a row is a drawing Bind… binds: loaded, or changed on disk since it was read.</summary>
        private static bool IsBindable(ReferenceRowViewModel row) =>
            row.Reference != null
            && !row.IsImage
            && (
                row.Reference.Status == CadReferenceStatus.Loaded
                || row.Reference.Status == CadReferenceStatus.NeedsReloading
            );

        /// <summary>
        /// Bind…: the drawings selected, in the palette's order, each once, those unloaded or not found among them, so that
        /// -XREF Bind refuses them all as it refuses them typed.
        /// </summary>
        private void Bind()
        {
            List<string> names = SelectedRows
                .Where(r => r.Reference != null && !r.IsImage)
                .OrderBy(r => Rows.IndexOf(r))
                .Select(r => r.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count == 0)
            {
                return;
            }

            _model.BindXrefs(names);
            Refresh();
        }

        private void ReloadAll()
        {
            if (Rows.Any(r => r.Reference != null && !r.IsImage))
            {
                _model.RunWithAnswers("-XREF", "R", "*");
            }

            if (Rows.Any(r => r.IsImage))
            {
                _model.RunWithAnswers("-IMAGE", "R", "*");
            }

            Refresh();
        }

        private void SetPathType(CadPathType type)
        {
            if (!IsReference || _selected.Reference.FoundAt == null)
            {
                return;
            }

            if (!_selected.IsImage)
            {
                _model.RunWithAnswers("-XREF", "T", _selected.Name, type.ToString());
                Refresh();
                return;
            }

            // -IMAGE has no path type of its own: Path is given the path the type writes.
            SetPath(
                CadXrefManager.PathFor(
                    _selected.Reference.FoundAt,
                    type,
                    Document.HasFile ? Path.GetDirectoryName(Document.FileName) : null
                )
            );
        }
    }

    /// <summary>
    /// AutoCAD's Attach External Reference and Attach Image dialogs, which XATTACH (XA) and IMAGEATTACH (IAT) show, over the
    /// request the command hands the window: the file and its name, the reference type of a drawing, the path type, and the
    /// insertion point, scale and rotation, each given or asked on the command line after OK.
    /// </summary>
    public sealed class AttachReferenceViewModel : ObservableObject
    {
        private readonly string[] _pathTypes = { "Relative path", "Full path", "No path" };
        private string _x;
        private string _y;
        private string _z;
        private string _scaleX;
        private string _scaleY;
        private string _scaleZ;
        private string _angle;
        private ImageSource _preview;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of XATTACH or IMAGEATTACH.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public AttachReferenceViewModel(CadAttachRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            _x = BlockNumbers.Text(request.Point.X);
            _y = BlockNumbers.Text(request.Point.Y);
            _z = BlockNumbers.Text(request.Point.Z);
            _scaleX = BlockNumbers.Text(request.Scale.X);
            _scaleY = BlockNumbers.Text(request.Scale.Y);
            _scaleZ = BlockNumbers.Text(request.Scale.Z);
            _angle = BlockNumbers.Plain(request.Rotation * 180.0 / Math.PI);
        }

        /// <summary>Gets the request.</summary>
        public CadAttachRequest Request { get; }

        /// <summary>Gets the title: Attach External Reference, or Attach Image.</summary>
        public string Title => IsImage ? "Attach Image" : "Attach External Reference";

        /// <summary>Gets whether a picture is attached.</summary>
        public bool IsImage => Request.Kind == CadAttachKind.Image;

        /// <summary>Gets whether a drawing is attached.</summary>
        public bool IsXref => !IsImage;

        /// <summary>Gets the kinds of file offered, as the dialog's note says them.</summary>
        public string FilesNote =>
            IsImage
                ? "Files of type: PNG (*.png), JPEG (*.jpg, *.jpeg), Bitmap (*.bmp). TIFF, GIF, PDF and the other raster types of AutoCAD are not offered."
                : "Files of type: Drawing (*.dwg), of AutoCAD 2000 to 2018, and Drawing Exchange Format (*.dxf).";

        /// <summary>Gets the filter of the Browse dialog: for a drawing, DWG first as AutoCAD's, then DXF.</summary>
        public string BrowseFilter =>
            IsImage
                ? "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|PNG (*.png)|*.png|JPEG (*.jpg;*.jpeg)|*.jpg;*.jpeg|Bitmap (*.bmp)|*.bmp"
                : CadDrawingFile.OpenFilter;

        /// <summary>Gets or sets the full path of the file chosen; set, the dialog reads it and shows what it found.</summary>
        public string FileName
        {
            get => Request.FileName;
            set
            {
                Request.FileName = value;
                _preview = null;

                foreach (
                    string name in new[]
                    {
                        nameof(FileName),
                        nameof(Name),
                        nameof(Names),
                        nameof(SavedPath),
                        nameof(FoundIn),
                        nameof(UnitText),
                        nameof(UnitSecond),
                        nameof(Error),
                        nameof(CanOk),
                        nameof(Preview),
                    }
                )
                {
                    Raise(name);
                }
            }
        }

        /// <summary>Gets or sets the name the reference or image is given; the file's name until set.</summary>
        public string Name
        {
            get => Request.Name;
            set
            {
                Request.Name = value ?? string.Empty;
                Raise(nameof(Name));
                Raise(nameof(CanOk));
            }
        }

        /// <summary>Gets the names the Name box offers: the one chosen, then those of the drawing's references or images of the same kind.</summary>
        public IReadOnlyList<string> Names
        {
            get
            {
                var names = new List<string>();

                if (!string.IsNullOrEmpty(Request.Name))
                {
                    names.Add(Request.Name);
                }

                foreach (CadExternalReference reference in Request.Document.Xrefs.GetReferences())
                {
                    if (
                        reference.IsImage == IsImage
                        && !names.Contains(reference.Name, StringComparer.OrdinalIgnoreCase)
                    )
                    {
                        names.Add(reference.Name);
                    }
                }

                return names;
            }
        }

        /// <summary>Gets or sets whether the drawing is attached (Attachment) rather than overlaid.</summary>
        public bool IsAttachment
        {
            get => !Request.IsOverlay;
            set
            {
                Request.IsOverlay = !value;
                Raise(nameof(IsAttachment));
                Raise(nameof(IsOverlay));
            }
        }

        /// <summary>Gets or sets whether the drawing is overlaid.</summary>
        public bool IsOverlay
        {
            get => Request.IsOverlay;
            set => IsAttachment = !value;
        }

        /// <summary>Gets the path types the Path type box offers.</summary>
        public IReadOnlyList<string> PathTypes => _pathTypes;

        /// <summary>Gets or sets the path type chosen, in the dialog's words.</summary>
        public string PathType
        {
            get => ExternalReferencesViewModel.PathTypeName(Request.PathType);
            set
            {
                Request.PathType =
                    value == "Full path" ? CadPathType.Full
                    : value == "No path" ? CadPathType.None
                    : CadPathType.Relative;
                Raise(nameof(PathType));
                Raise(nameof(SavedPath));
            }
        }

        /// <summary>Gets the note under the path type.</summary>
        public string PathNote => "Relative needs this drawing saved first; until then Full path is used.";

        /// <summary>Gets or sets whether the insertion point is asked on the command line after OK.</summary>
        public bool SpecifyPointOnScreen
        {
            get => Request.SpecifyPointOnScreen;
            set
            {
                Request.SpecifyPointOnScreen = value;
                Raise(nameof(SpecifyPointOnScreen));
                Raise(nameof(IsPointTyped));
            }
        }

        /// <summary>Gets whether the insertion point is typed in the dialog.</summary>
        public bool IsPointTyped => !Request.SpecifyPointOnScreen;

        /// <summary>Gets or sets the X of the insertion point, as typed.</summary>
        public string X
        {
            get => _x;
            set => Set(ref _x, value);
        }

        /// <summary>Gets or sets the Y of the insertion point, as typed.</summary>
        public string Y
        {
            get => _y;
            set => Set(ref _y, value);
        }

        /// <summary>Gets or sets the Z of the insertion point, as typed.</summary>
        public string Z
        {
            get => _z;
            set => Set(ref _z, value);
        }

        /// <summary>Gets or sets whether the scale is asked on the command line after OK.</summary>
        public bool SpecifyScaleOnScreen
        {
            get => Request.SpecifyScaleOnScreen;
            set
            {
                Request.SpecifyScaleOnScreen = value;
                Raise(nameof(SpecifyScaleOnScreen));
                Raise(nameof(IsScaleTyped));
                Raise(nameof(IsScaleYZTyped));
            }
        }

        /// <summary>Gets whether the scale is typed in the dialog.</summary>
        public bool IsScaleTyped => !Request.SpecifyScaleOnScreen;

        /// <summary>Gets whether the scales along Y and Z are typed: the scale typed, and not alike along every axis.</summary>
        public bool IsScaleYZTyped => !Request.SpecifyScaleOnScreen && !Request.UniformScale;

        /// <summary>Gets or sets the label of the first scale box: X: for a drawing, Scale: for a picture.</summary>
        public string ScaleLabel => IsImage ? "Scale:" : "X:";

        /// <summary>Gets or sets the scale along X, as typed; the scale of a picture.</summary>
        public string ScaleX
        {
            get => _scaleX;
            set
            {
                if (Set(ref _scaleX, value) && Request.UniformScale)
                {
                    Set(ref _scaleY, value, nameof(ScaleY));
                    Set(ref _scaleZ, value, nameof(ScaleZ));
                }
            }
        }

        /// <summary>Gets or sets the scale along Y, as typed.</summary>
        public string ScaleY
        {
            get => _scaleY;
            set => Set(ref _scaleY, value);
        }

        /// <summary>Gets or sets the scale along Z, as typed.</summary>
        public string ScaleZ
        {
            get => _scaleZ;
            set => Set(ref _scaleZ, value);
        }

        /// <summary>Gets or sets whether the drawing is scaled alike along every axis (Uniform Scale).</summary>
        public bool UniformScale
        {
            get => Request.UniformScale;
            set
            {
                Request.UniformScale = value;
                Raise(nameof(UniformScale));
                Raise(nameof(IsScaleYZTyped));

                if (value)
                {
                    ScaleX = _scaleX;
                }
            }
        }

        /// <summary>Gets or sets whether the rotation is asked on the command line after OK.</summary>
        public bool SpecifyRotationOnScreen
        {
            get => Request.SpecifyRotationOnScreen;
            set
            {
                Request.SpecifyRotationOnScreen = value;
                Raise(nameof(SpecifyRotationOnScreen));
                Raise(nameof(IsRotationTyped));
            }
        }

        /// <summary>Gets whether the rotation is typed in the dialog.</summary>
        public bool IsRotationTyped => !Request.SpecifyRotationOnScreen;

        /// <summary>Gets or sets the rotation in degrees, as typed.</summary>
        public string Angle
        {
            get => _angle;
            set => Set(ref _angle, value);
        }

        /// <summary>Gets the title of the unit group: Block Unit for a drawing, Resolution for a picture.</summary>
        public string UnitTitle => IsImage ? "Resolution" : "Block Unit";

        /// <summary>Gets the unit: of the drawing chosen for a reference, of this drawing for a picture.</summary>
        public string UnitText => Request.Units == DxfUnits.Undefined ? "Unitless" : Request.Units.ToString();

        /// <summary>Gets the label of the second line of the unit group: Factor: for a drawing, Size: for a picture.</summary>
        public string UnitSecondLabel => IsImage ? "Size:" : "Factor:";

        /// <summary>
        /// Gets the second line of the unit group: the factor of a drawing's unit, 1.0000; of a picture its pixels and what a
        /// pixel is at scale 1, "40 × 30 px · 1 px = 0.2646 mm", or "width 1 unit" for one of no resolution.
        /// </summary>
        public string UnitSecond
        {
            get
            {
                if (!IsImage)
                {
                    return Request.UnitFactor.ToString("0.0000", CultureInfo.InvariantCulture);
                }

                if (Request.PixelWidth <= 0)
                {
                    return string.Empty;
                }

                string pixels =
                    Request.PixelWidth.ToString(CultureInfo.InvariantCulture)
                    + " × "
                    + Request.PixelHeight.ToString(CultureInfo.InvariantCulture)
                    + " px";
                return Request.HasResolution
                    ? pixels
                        + " · 1 px = "
                        + (Request.BaseWidth / Request.PixelWidth).ToString("0.####", CultureInfo.InvariantCulture)
                        + " "
                        + UnitShort(Request.Document.Database.Insunits)
                    : pixels + " · width 1 unit";
            }
        }

        /// <summary>Gets where the file chosen was found, its full path.</summary>
        public string FoundIn => Request.FileName ?? string.Empty;

        /// <summary>Gets the path that is written for it.</summary>
        public string SavedPath => Request.SavedPath;

        /// <summary>Gets why the file chosen cannot be attached; empty where it can.</summary>
        public string Error => Request.Error ?? string.Empty;

        /// <summary>Gets whether OK can be pressed: a file chosen that can be attached, and a name.</summary>
        public bool CanOk =>
            Request.FileName != null && Request.Error == null && !string.IsNullOrWhiteSpace(Request.Name);

        /// <summary>Gets the preview of the file chosen: the drawing drawn, or the picture; null before one is chosen.</summary>
        public ImageSource Preview
        {
            get
            {
                if (_preview != null || Request.FileName == null || Request.Error != null)
                {
                    return _preview;
                }

                _preview =
                    IsImage ? ExternalReferencesViewModel.PreviewOf(Request.FileName, true, null, 200, 150)
                    : Request.Drawing == null ? null
                    : CadStylePreview.Drawing(Request.Drawing, Request.Document.Fonts, 200, 150);
                return _preview;
            }
        }

        /// <summary>Writes what the dialog holds into the request and says what it was closed with.</summary>
        /// <param name="action">OK or Cancel.</param>
        public void Close(CadDialogAction action)
        {
            if (action == CadDialogAction.Ok)
            {
                Request.Point = new GeoPoint3(Read(_x, 0.0), Read(_y, 0.0), Read(_z, 0.0));
                double x = Read(_scaleX, 1.0);
                Request.Scale =
                    Request.UniformScale || IsImage
                        ? new GeoVector3(x, x, x)
                        : new GeoVector3(x, Read(_scaleY, x), Read(_scaleZ, x));
                Request.Rotation = Read(_angle, 0.0) * Math.PI / 180.0;
            }

            Request.Action = action;
        }

        private static double Read(string text, double fallback) =>
            BlockNumbers.TryRead(text, out double value) && value != 0.0 ? value : fallback;

        private static string UnitShort(DxfUnits units)
        {
            switch (units)
            {
                case DxfUnits.Millimeters:
                    return "mm";
                case DxfUnits.Centimeters:
                    return "cm";
                case DxfUnits.Meters:
                    return "m";
                case DxfUnits.Inches:
                    return "in";
                case DxfUnits.Feet:
                    return "ft";
                default:
                    return "units";
            }
        }
    }

    /// <summary>
    /// AutoCAD's Bind Xrefs/DGN Underlays dialog, which the palette's Bind… shows: the bind type, Bind or Insert, started from
    /// BINDTYPE, and the drawings to bind. OK writes the type back to BINDTYPE and binds them as -XREF Bind binds them.
    /// </summary>
    public sealed class XrefBindViewModel : ObservableObject
    {
        private bool _isInsert;

        /// <summary>Makes the dialog for the drawings selected.</summary>
        /// <param name="names">The names of the drawings to bind, in the palette's order.</param>
        /// <param name="bindType">BINDTYPE: 0 Bind, 1 Insert.</param>
        /// <exception cref="ArgumentNullException">Thrown when the names are null.</exception>
        public XrefBindViewModel(IReadOnlyList<string> names, int bindType)
        {
            Names = names ?? throw new ArgumentNullException(nameof(names));
            _isInsert = bindType == 1;
        }

        /// <summary>Gets the names of the drawings to bind.</summary>
        public IReadOnlyList<string> Names { get; }

        /// <summary>Gets the line under the bind type: "Binding: grid-base", the names parted by commas.</summary>
        public string NamesText => "Binding: " + string.Join(", ", Names);

        /// <summary>
        /// Gets or sets whether the records of each drawing are renamed after it, as BINDTYPE 0 binds: layer WALL of grid-base
        /// becomes grid-base$0$WALL.
        /// </summary>
        public bool IsBind
        {
            get => !_isInsert;
            set => IsInsert = !value;
        }

        /// <summary>
        /// Gets or sets whether the records of each drawing are merged by name, as if it were detached and inserted, as
        /// BINDTYPE 1 binds: WALL stays WALL, the drawing's own kept.
        /// </summary>
        public bool IsInsert
        {
            get => _isInsert;
            set
            {
                if (Set(ref _isInsert, value))
                {
                    Raise(nameof(IsBind));
                }
            }
        }

        /// <summary>Gets the bind type chosen, as BINDTYPE holds it: 0 Bind, 1 Insert.</summary>
        public int BindType => _isInsert ? 1 : 0;

        /// <summary>Gets what the dialog was closed with: Cancel until OK is pressed.</summary>
        public CadDialogAction Action { get; private set; }

        /// <summary>Says what the dialog was closed with.</summary>
        /// <param name="action">OK or Cancel; the close button and Esc are Cancel.</param>
        public void Close(CadDialogAction action) => Action = action;
    }

    /// <summary>
    /// The box Bind… shows where -XREF Bind bound nothing: the lines it wrote on the command line, in AutoCAD 2021's words, and
    /// what to do next.
    /// </summary>
    public sealed class XrefBindRefusedViewModel
    {
        /// <summary>Makes the box.</summary>
        /// <param name="lines">The lines -XREF Bind wrote.</param>
        /// <param name="hint">What to do next.</param>
        /// <exception cref="ArgumentNullException">Thrown when the lines are null.</exception>
        public XrefBindRefusedViewModel(IReadOnlyList<string> lines, string hint)
        {
            Lines = lines ?? throw new ArgumentNullException(nameof(lines));
            Hint = hint ?? string.Empty;
        }

        /// <summary>
        /// Gets the lines -XREF Bind wrote: "Xref "column" is nested and cannot be bound.", or "Cannot bind unloaded xref(s).
        /// Reload them first:" with a line for each unloaded, or "xref "detail-B" is currently not found. Reload or detach
        /// before binding."
        /// </summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>Gets what to do next, ending "The same lines are on the command line."</summary>
        public string Hint { get; }
    }
}
