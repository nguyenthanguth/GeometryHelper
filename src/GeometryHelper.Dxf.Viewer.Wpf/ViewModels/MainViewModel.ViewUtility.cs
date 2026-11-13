using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// Batch 4 of acad.pgp: the Viewports dialog of VPORTS and the tiles of model space it makes, the Object Selection Filters
    /// dialog of FILTER, the QuickCalc palette of QUICKCALC with the values CAL gives it, and the Quick Properties panel of
    /// QUICKPROPERTIES and QPMODE. Each dialog is shown through a function the workspace sets, so that a test drives it with
    /// none.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private static readonly string[] QuickLabels = { "Color", "Layer", "Linetype" };

        // QPMODE as AutoCAD 2021 starts: -1, the panel off, 1 coming back with Ctrl+Shift+P.
        private int _quickPropertiesMode = -1;
        private bool _quickPropertiesAsked;
        private bool _isQuickPropertiesShown;
        private IReadOnlyList<PropertyRowViewModel> _quickRows = Array.Empty<PropertyRowViewModel>();
        private Action<CadCalcValue> _calcWaiting;
        private QuickCalcViewModel _quickCalc;

        /// <summary>Gets or sets how the workspace shows Viewports: true when OK was pressed, the dialog then applied.</summary>
        public Func<ViewportsViewModel, bool> ShowViewports { get; set; }

        /// <summary>Gets or sets how the workspace shows Object Selection Filters: true when Apply was pressed.</summary>
        public Func<SelectionFiltersViewModel, bool> ShowSelectionFilters { get; set; }

        /// <summary>
        /// Gets or sets the file the named lists of FILTER are kept in, AutoCAD's <c>filter.nfl</c> form, shared by every drawing
        /// opened afterwards; null keeps them for the session of each drawing.
        /// </summary>
        public string FilterListsPath { get; set; }

        /// <summary>Gets or sets how the workspace shows the QuickCalc palette, which stays open.</summary>
        public Action<QuickCalcViewModel> ShowQuickCalc { get; set; }

        /// <summary>Raised when the tiles of model space change, or another tile becomes current, so that the canvas lays them out again.</summary>
        public event EventHandler TilesChanged;

        /// <summary>
        /// Gets the tiles model space is shown through while it is the space shown, as VPORTS made them; null in a layout and
        /// with no drawing. The current tile is the canvas; the others are drawn beside it with their own views.
        /// </summary>
        public CadTiledViewports ModelTiles
        {
            get
            {
                if (_current == null || !IsModelSpace || Document.BlockEditor != null)
                {
                    return null;
                }

                AttachTiles(_current);
                return _current.Document.TiledViewports;
            }
        }

        /// <summary>Gets or sets QPMODE: above nought the Quick Properties panel shows the objects picked with no command running; nought or below it does not.</summary>
        public int QuickPropertiesMode
        {
            get => _quickPropertiesMode;
            set
            {
                if (Set(ref _quickPropertiesMode, Math.Max(-2, Math.Min(2, value))))
                {
                    UpdateQuickProperties();
                }
            }
        }

        /// <summary>Gets whether the Quick Properties panel is shown.</summary>
        public bool IsQuickPropertiesShown
        {
            get => _isQuickPropertiesShown;
            private set => Set(ref _isQuickPropertiesShown, value);
        }

        /// <summary>Gets the rows the Quick Properties panel shows: Color, Layer and Linetype of the selection, edited as in Properties.</summary>
        public IReadOnlyList<PropertyRowViewModel> QuickPropertyRows
        {
            get => _quickRows;
            private set => Set(ref _quickRows, value);
        }

        /// <summary>Hides the Quick Properties panel, as its close button and Esc do; the selection stays.</summary>
        public void CloseQuickProperties()
        {
            _quickPropertiesAsked = false;
            IsQuickPropertiesShown = false;
        }

        /// <summary>
        /// Makes another tile of model space the current one, as a click in it does: the view the canvas showed kept in the
        /// tile it leaves, and the canvas showing the view of the tile it comes to.
        /// </summary>
        /// <param name="id">The number of the tile.</param>
        /// <returns>Whether there is such a tile.</returns>
        public bool ActivateTile(int id)
        {
            CadTiledViewports tiles = ModelTiles;
            return tiles != null && tiles.MakeCurrent(id);
        }

        /// <summary>Ctrl+Shift+P: QPMODE's sign switched, as AutoCAD's key does, saying "&lt;Quick Properties panel on&gt;" or "&lt;Quick Properties panel off&gt;".</summary>
        private void ToggleQuickPropertiesMode()
        {
            int mode = _quickPropertiesMode;
            QuickPropertiesMode =
                mode > 0 ? -mode
                : mode == 0 ? 1
                : -mode;
            Say(QuickPropertiesMode > 0 ? "<Quick Properties panel on>" : "<Quick Properties panel off>");
        }

        /// <summary>QPMODE among the variables of the window.</summary>
        private CadSystemVariable QuickPropertiesVariable() =>
            CadSystemVariable.Integer("QPMODE", -2, 2, () => QuickPropertiesMode, v => QuickPropertiesMode = v);

        /// <summary>The panel shown for the selection where QPMODE is on and no command runs, or QUICKPROPERTIES asked for it; hidden with nothing selected.</summary>
        private void UpdateQuickProperties()
        {
            List<string> selected = _current?.Document.Selection.Handles.ToList() ?? new List<string>();

            if (selected.Count == 0)
            {
                _quickPropertiesAsked = false;
            }

            bool shown =
                selected.Count > 0
                && (_quickPropertiesAsked || (_quickPropertiesMode > 0 && Session?.IsRunning != true));
            QuickPropertyRows = shown
                ? PropertyGroups.SelectMany(g => g.Rows).Where(r => QuickLabels.Contains(r.Label)).ToList()
                : (IReadOnlyList<PropertyRowViewModel>)Array.Empty<PropertyRowViewModel>();
            IsQuickPropertiesShown = shown && QuickPropertyRows.Count > 0;
        }

        /// <summary>The requests of batch 4: VPORTS, FILTER, QUICKCALC, CAL and QUICKPROPERTIES.</summary>
        private void OnViewUtilityRequested(CadUiRequestEventArgs e)
        {
            CadDocument document = e.Document ?? _current?.Document;

            switch (e.Name)
            {
                case "VPORTS":
                    if (document != null && ShowViewports != null)
                    {
                        KnownFile file = _files.Values.FirstOrDefault(f => f.Document == document);

                        if (file != null)
                        {
                            AttachTiles(file);
                        }

                        var dialog = new ViewportsViewModel((CadTiledViewports)e.Payload);

                        while (ShowViewports(dialog) && !dialog.Apply()) { }

                        e.Handled = true;
                    }

                    break;
                case "FILTER":
                    if (document != null && ShowSelectionFilters != null)
                    {
                        var dialog = new SelectionFiltersViewModel(document, (CadSelectionFilter)e.Payload);

                        if (ShowSelectionFilters(dialog))
                        {
                            dialog.Commit();
                            e.Answer = "Apply";
                        }
                        else if (dialog.IsAddingSelected)
                        {
                            e.Answer = "Add Selected Object";
                        }

                        e.Handled = true;
                    }

                    break;
                case "QUICKCALC":
                    if (ShowQuickCalc != null)
                    {
                        _quickCalc = _quickCalc ?? new QuickCalcViewModel(PickForCalc, text => CommandText = text);
                        ShowQuickCalc(_quickCalc);
                        e.Handled = true;
                    }

                    break;
                case "CAL":
                    Action<CadCalcValue> waiting = _calcWaiting;
                    _calcWaiting = null;

                    if (waiting != null && e.Payload is CadCalcValue value)
                    {
                        waiting(value);
                        e.Handled = true;
                    }

                    break;
                case "QUICKPROPERTIES":
                    _quickPropertiesAsked = true;
                    UpdateQuickProperties();
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// QuickCalc's point buttons: CAL run with the expression of <c>cur</c> points, its value given back when the points
        /// are picked; refused while a command runs, in the calculator's words.
        /// </summary>
        private void PickForCalc(string expression, Action<CadCalcValue> done)
        {
            if (_current == null)
            {
                return;
            }

            if (Session.IsRunning)
            {
                Say("Acquiring points from the drawing area is not possible during a command");
                return;
            }

            _calcWaiting = done;
            Execute("CAL");
            Execute(expression);
        }

        /// <summary>Lets the tiles of a file's drawing read the view the canvas shows, and follows their changes; once.</summary>
        private void AttachTiles(KnownFile file)
        {
            CadTiledViewports tiles = file.Document.TiledViewports;

            if (tiles.ReadView != null)
            {
                return;
            }

            tiles.ReadView = tile => ReadTileView(file, tile);
            tiles.Changed += (s, e) => OnTilesChanged(file);
        }

        /// <summary>The view the canvas shows in model space, read into the current tile.</summary>
        private void ReadTileView(KnownFile file, CadTiledViewport tile)
        {
            CadView view = file.ViewOf(file.Document.ModelSpace, _canvasWidth, _canvasHeight).View;
            tile.Center = new GeoPoint2(view.CenterX, view.CenterY);
            tile.Width = view.Width / view.PixelsPerUnit;
            tile.Height = view.Height / view.PixelsPerUnit;
        }

        /// <summary>The tiles changed: the canvas shows the view of the current one, and the workspace lays them out again.</summary>
        private void OnTilesChanged(KnownFile file)
        {
            CadTiledViewport current = file.Document.TiledViewports.Current;
            ViewState state = file.ViewOf(file.Document.ModelSpace, _canvasWidth, _canvasHeight);

            if (current.Height > 0.0)
            {
                // The height of the tile's view is kept, as AutoCAD keeps it: the width follows the shape of the canvas.
                state.View.ZoomToHeight(current.Center.X, current.Center.Y, current.Height);
                state.FitPending = false;
            }

            Revision++;
            TilesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
