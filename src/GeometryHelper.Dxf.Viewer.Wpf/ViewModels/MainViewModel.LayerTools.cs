using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// LAYWALK's LayerWalk and LAYTRANS's Layer Translator, each a dialog shown through a function the workspace sets, and the
    /// DesignCenter palette (ADCENTER, ADCCLOSE, and the key given ADCENTER), shown beside the window and left open. With no
    /// function set, the commands say they have no window. A block inserted, an external reference attached, or content copied
    /// from DesignCenter goes through the commands and calls that the palette's menu, double click and drag share.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private const int DesignCenterHistoryLimit = 24;

        private readonly List<string> _designCenterHistory = new List<string>();

        // What INSERT and XATTACH are given while DesignCenter starts them: the file, the block, where it is dropped, and whether
        // the Blocks palette is shown first (Insert Block…).
        private string _designCenterFile;
        private string _designCenterBlock;
        private GeoPoint2? _designCenterAt;
        private bool _designCenterPalette;
        private string _designCenterAttach;
        private bool _isDesignCenterShown;

        /// <summary>Gets or sets how the workspace shows the LayerWalk dialog of LAYWALK; true once it is answered.</summary>
        public Func<LayerWalkViewModel, bool> ShowLayerWalk { get; set; }

        /// <summary>Gets or sets how the workspace shows the Layer Translator of LAYTRANS; true once it is answered.</summary>
        public Func<LayerTranslatorViewModel, bool> ShowLayerTranslator { get; set; }

        /// <summary>Gets or sets how the workspace shows the DesignCenter palette, beside the window and left open (the one open is brought forward).</summary>
        public Action<DesignCenterViewModel> ShowDesignCenter { get; set; }

        /// <summary>Gets or sets how the workspace closes the DesignCenter palette (ADCCLOSE).</summary>
        public Action CloseDesignCenter { get; set; }

        /// <summary>Gets whether the DesignCenter palette is open, as its window says.</summary>
        public bool IsDesignCenterShown
        {
            get => _isDesignCenterShown;
            private set => Set(ref _isDesignCenterShown, value);
        }

        /// <summary>Gets the drawings DesignCenter showed, the latest first, as its History tab lists them.</summary>
        public IReadOnlyList<string> DesignCenterHistory => _designCenterHistory;

        /// <summary>Gets the drawings open, in ordinal order of their titles then their paths, for DesignCenter's Open Drawings tab.</summary>
        internal IReadOnlyList<CadDocument> OpenDocuments =>
            _files
                .Values.Select(f => f.Document)
                .Where(d => d != null)
                .OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.Title, StringComparer.Ordinal)
                .ThenBy(d => d.FileName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ToList();

        /// <summary>Tells the model the DesignCenter palette is open or closed; the window calls it as it is shown and closed.</summary>
        /// <param name="shown">Whether it is open.</param>
        public void NoteDesignCenterShown(bool shown) => IsDesignCenterShown = shown;

        /// <summary>Notes a drawing DesignCenter showed, put first in its History, a drawing already in it moved up, the list kept to 24.</summary>
        /// <param name="path">The path of the drawing.</param>
        internal void NoteDesignCenterVisit(string path)
        {
            _designCenterHistory.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            _designCenterHistory.Insert(0, path);

            if (_designCenterHistory.Count > DesignCenterHistoryLimit)
            {
                _designCenterHistory.RemoveRange(
                    DesignCenterHistoryLimit,
                    _designCenterHistory.Count - DesignCenterHistoryLimit
                );
            }
        }

        /// <summary>Writes a line DesignCenter says to the command line's history, as a command's own words are.</summary>
        internal void SayFromDesignCenter(string line)
        {
            if (_current == null || string.IsNullOrEmpty(line))
            {
                return;
            }

            _current.Session.Write(line);
            SyncHistory();
        }

        /// <summary>
        /// Starts INSERT with a block of a drawing's file given, or the whole file as one block where the block is null: the block
        /// is placed at once, the insertion point asked on the command line, as the Blocks palette's tile does; with a point given
        /// (the block dropped on the canvas) it is placed there, scale 1, no rotation. With <paramref name="palette"/> the
        /// Blocks palette opens on the file first, for the insertion options.
        /// </summary>
        /// <param name="file">The DWG or DXF file.</param>
        /// <param name="block">The block's name; null for the file itself.</param>
        /// <param name="at">Where it is dropped; null to ask.</param>
        /// <param name="palette">Whether the Blocks palette is shown first.</param>
        internal void InsertFromDesignCenter(string file, string block, GeoPoint2? at, bool palette)
        {
            if (_current == null || string.IsNullOrEmpty(file))
            {
                return;
            }

            _designCenterFile = file;
            _designCenterBlock = block ?? string.Empty;
            _designCenterAt = at;
            _designCenterPalette = palette;

            try
            {
                StartCommand("INSERT");
            }
            finally
            {
                _designCenterFile = null;
                _designCenterBlock = null;
                _designCenterAt = null;
                _designCenterPalette = false;
            }
        }

        /// <summary>Starts XATTACH with a drawing's file chosen already: the Attach External Reference dialog opens on it.</summary>
        /// <param name="file">The file; null does nothing.</param>
        internal void AttachFromDesignCenter(string file)
        {
            if (_current == null || string.IsNullOrEmpty(file))
            {
                return;
            }

            _designCenterAttach = file;

            try
            {
                StartCommand("XATTACH");
            }
            finally
            {
                _designCenterAttach = null;
            }
        }

        /// <summary>
        /// What a drag out of DesignCenter does where it is dropped on the canvas: a block is inserted there, a drawing file
        /// inserted there as a block, and layers, linetypes, styles and layouts copied into the drawing, as the palette's menu does.
        /// </summary>
        /// <param name="text">What <see cref="DesignCenterDrag.Write"/> wrote.</param>
        /// <param name="at">Where it is dropped, in the drawing; null where it is not known.</param>
        /// <returns>Whether the text was one a drag wrote and was acted on.</returns>
        public bool DropFromDesignCenter(string text, GeoPoint2? at)
        {
            if (
                _current == null
                || !DesignCenterDrag.TryRead(
                    text,
                    out string path,
                    out CadDesignCenterKind? kind,
                    out IReadOnlyList<string> names
                )
            )
            {
                return false;
            }

            if (kind == null)
            {
                InsertFromDesignCenter(path, null, at, false);
                return true;
            }

            if (kind == CadDesignCenterKind.Blocks && names.Count > 0)
            {
                InsertFromDesignCenter(path, names[0], at, false);
                return true;
            }

            if (kind == CadDesignCenterKind.Xrefs)
            {
                SayFromDesignCenter("An external reference is attached from its file: use Attach as Xref.");
                return false;
            }

            CadDesignCenterDrawing from;

            try
            {
                from = CadDesignCenterDrawing.Open(path);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is FormatException
                )
            {
                GeometryHelperLog.Warn(
                    "MainViewModel: the drawing " + path + " dropped from DesignCenter was not read.",
                    exception
                );
                SayFromDesignCenter("Cannot read " + path + ": " + exception.Message);
                return false;
            }

            CadDesignCenterResult result = CadDesignCenter.Add(_current.Document, from, kind.Value, names);
            SayFromDesignCenter(
                result.Added.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " added, "
                    + result.Kept.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " kept."
                    + (result.Failed.Count > 0 ? " " + string.Join(" ", result.Failed) : string.Empty)
            );
            return true;
        }

        /// <summary>Shows or hides the DesignCenter palette, as the key given ADCENTER does (AutoCAD's Ctrl+2).</summary>
        /// <returns>Whether it did something: there is a window to show it in.</returns>
        public bool ToggleDesignCenter()
        {
            if (_isDesignCenterShown && CloseDesignCenter != null)
            {
                CloseDesignCenter();
                return true;
            }

            if (ShowDesignCenter == null || _current == null)
            {
                return false;
            }

            StartCommand("ADCENTER");
            return true;
        }

        /// <summary>A request of LAYWALK, LAYTRANS, ADCENTER or ADCCLOSE: the dialog or palette shown where the workspace shows it.</summary>
        private void OnLayerToolsRequested(CadUiRequestEventArgs e)
        {
            switch (e.Payload)
            {
                case CadLayerWalkRequest walk when ShowLayerWalk != null:
                    e.Handled = ShowLayerWalk(new LayerWalkViewModel(walk));
                    return;
                case CadLayerTranslatorRequest translate when ShowLayerTranslator != null:
                    e.Handled = ShowLayerTranslator(new LayerTranslatorViewModel(translate));
                    return;
            }

            if (e.Name == "ADCENTER" && ShowDesignCenter != null && _current != null)
            {
                ShowDesignCenter(new DesignCenterViewModel(this));
                e.Handled = true;
            }
            else if (e.Name == "ADCCLOSE" && CloseDesignCenter != null)
            {
                CloseDesignCenter();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Answers the INSERT a DesignCenter action started: the file and block given to its request, and where the block is
        /// dropped; the Blocks palette is left to show where it was asked for.
        /// </summary>
        /// <returns>True where the request is answered and needs no palette.</returns>
        private bool AnswerDesignCenterInsert(CadInsertRequest insert)
        {
            if (_designCenterFile == null)
            {
                return false;
            }

            insert.FileName = _designCenterFile;

            if (_designCenterPalette)
            {
                return false;
            }

            insert.Name = _designCenterBlock;
            insert.Action = CadDialogAction.Ok;

            if (_designCenterAt != null)
            {
                insert.SpecifyPointOnScreen = false;
                insert.Point = new GeoPoint3(_designCenterAt.Value.X, _designCenterAt.Value.Y, 0.0);
            }

            return true;
        }
    }
}
