using System;
using System.Collections.Generic;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>A layer of the drawing as the canvas and the layer box show it.</summary>
    public sealed class CadLayerInfo
    {
        internal CadLayerInfo(DxfLayerTableRecord record)
        {
            Name = record.Name;
            Rgb = record.Color == null ? 0xFFFFFF : record.Color.ColorValue & 0xFFFFFF;
            IsOn = !record.IsOff;
            IsFrozen = record.IsFrozen;
            IsLocked = record.IsLocked;
            Linetype = record.LinetypeName ?? "Continuous";
            int weight = (int)record.LineWeight;
            LineWeight = weight >= 0 ? weight / 100.0 : 0.25;
        }

        /// <summary>Gets the name of the layer.</summary>
        public string Name { get; }

        /// <summary>Gets the colour of the layer as 0xRRGGBB.</summary>
        public int Rgb { get; }

        /// <summary>Gets whether the layer is on.</summary>
        public bool IsOn { get; }

        /// <summary>Gets whether the layer is frozen.</summary>
        public bool IsFrozen { get; }

        /// <summary>Gets whether the layer is locked.</summary>
        public bool IsLocked { get; }

        /// <summary>Gets the name of the layer's linetype.</summary>
        public string Linetype { get; }

        /// <summary>Gets the line weight of the layer in millimetres.</summary>
        public double LineWeight { get; }
    }

    /// <summary>One space of a drawing: model space, or a layout (a sheet of paper with its viewports).</summary>
    public sealed class CadSpace
    {
        private readonly CadDocument _document;
        private DisplayBox? _paper;

        internal CadSpace(CadDocument document, string name, bool isModel, DxfBlockTableRecord block, DxfLayout layout)
        {
            _document = document;
            Name = name;
            IsModel = isModel;
            Block = block;
            Layout = layout;
        }

        /// <summary>
        /// Gets or sets the part of the space a named view restored in it shows when the space is next shown, as VIEW Restore
        /// asks of a space other than the one on screen; null where none is waiting. The window zooms to it and sets it null.
        /// </summary>
        public DisplayBox? PendingView { get; set; }

        /// <summary>
        /// Gets the sheet of paper a layout is drawn on, in the units of paper space, as AutoCAD shows it behind the layout:
        /// the paper size of its plot settings about its plotted area, the printable area's lower left corner at the origin,
        /// so that a layout of A3 on its side with margins of 7.5 at the sides and 20 above and below lies from (-7.5, -20) to
        /// (412.5, 277); its limits where it has no paper size; and where it has neither, what it draws with a margin of 2 per
        /// cent of its larger side. Empty for model space, which has no paper.
        /// </summary>
        public DisplayBox PaperBounds
        {
            get
            {
                if (IsModel)
                {
                    return DisplayBox.Empty;
                }

                if (_paper == null)
                {
                    _paper = Paper();
                }

                return _paper.Value;
            }
        }

        private DisplayBox Paper()
        {
            try
            {
                GeoPoint2 size = Layout.PlotPaperSize;

                if (size.X > 0.0 && size.Y > 0.0)
                {
                    // The plot settings are in millimetres; a layout of inches is drawn in inches.
                    double unit = Layout.PlotPaperUnits == DxfPlotPaperUnit.Inches ? 1.0 / 25.4 : 1.0;
                    DxfExtents2d margins = Layout.PlotPaperMargins;
                    GeoPoint2 origin = Layout.PlotOrigin;
                    bool turned = Layout.PlotRotation == DxfPlotRotation.Degrees090 || Layout.PlotRotation == DxfPlotRotation.Degrees270;
                    double width = (turned ? size.Y : size.X) * unit;
                    double height = (turned ? size.X : size.Y) * unit;
                    double left = ((turned ? margins.MinPoint.Y : margins.MinPoint.X) + origin.X) * unit;
                    double bottom = ((turned ? margins.MinPoint.X : margins.MinPoint.Y) + origin.Y) * unit;
                    return new DisplayBox(-left, -bottom, width - left, height - bottom);
                }

                DxfExtents2d limits = Layout.Limits;

                if (limits.MaxPoint.X > limits.MinPoint.X && limits.MaxPoint.Y > limits.MinPoint.Y)
                {
                    return new DisplayBox(limits.MinPoint.X, limits.MinPoint.Y, limits.MaxPoint.X, limits.MaxPoint.Y);
                }
            }
            catch (Exception)
            {
                // A layout whose plot settings cannot be read is given the paper of what it draws.
            }

            DisplayBox drawn = _document.GetDisplay(this).Bounds;
            return drawn.Inflate(Math.Max(drawn.Width, drawn.Height) * 0.02);
        }

        /// <summary>Gets the name shown on the tab: Model, or the layout's name.</summary>
        public string Name { get; }

        /// <summary>Gets whether this is model space.</summary>
        public bool IsModel { get; }

        /// <summary>Gets the block whose entities this space holds.</summary>
        public DxfBlockTableRecord Block { get; private set; }

        /// <summary>Gets the layout of a paper space, null for model space.</summary>
        public DxfLayout Layout { get; private set; }

        /// <summary>
        /// Gets whether the Block Editor shows this space now (BEDIT): the block opened, which is drawn as model space is but
        /// on the Block Editor's own background, or model space itself where &lt;Current Drawing&gt; was opened.
        /// </summary>
        public bool IsBlockEditor { get; internal set; }

        /// <summary>Points the space at its block and layout in a drawing read again, as undo reads it.</summary>
        internal void Rebind(DxfBlockTableRecord block, DxfLayout layout)
        {
            Block = block;
            Layout = layout;
            _paper = null;
        }

        /// <summary>Drops the sheet worked out, so that the next <see cref="PaperBounds"/> reads the plot settings a page setup wrote.</summary>
        internal void ForgetPaper() => _paper = null;

        /// <summary>Gets or sets the handle of the viewport of this layout whose model space is worked in (MSPACE); null on the paper.</summary>
        internal string Through { get; set; }

        /// <summary>Gets or sets the handle of the viewport last worked through, which MSPACE and the MODEL button enter again.</summary>
        internal string LastThrough { get; set; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// A drawing open in the presenter: a <see cref="DxfDatabase"/> with its spaces, the display list of each, the index
    /// that picks in it and the selection. Opening and showing run with no window, so a test can drive them.
    /// </summary>
    public sealed partial class CadDocument
    {
        private readonly Dictionary<CadSpace, DisplayList2> _displays = new Dictionary<CadSpace, DisplayList2>();
        private readonly Dictionary<CadSpace, PickIndex> _indexes = new Dictionary<CadSpace, PickIndex>();

        // The display lists edits have changed, kept with what they changed until the space is next drawn; looked up only, never enumerated.
        private readonly Dictionary<CadSpace, StaleDisplay> _stale = new Dictionary<CadSpace, StaleDisplay>();
        private readonly List<CadSpace> _spaces = new List<CadSpace>();
        private DisplayBuilder _builder;
        private Dictionary<string, DxfEntity> _byHandle;
        private Dictionary<string, CadSpace> _spaceByHandle;

        /// <summary>The width the display gives the words of a text, as MIRROR measures one it keeps readable.</summary>
        internal double TextWidth(DxfDBText text) => _builder.WidthOf(text);

        private CadDocument(DxfDatabase database, string fileName)
        {
            Database = database;
            FileName = fileName;
            Selection = new CadSelection();
            _builder = new DisplayBuilder(database, this);
            var model = new CadSpace(this, "Model", true, database.ModelSpace, null);
            _spaces.Add(model);

            foreach (DxfLayout layout in LayoutsOf(database))
            {
                _spaces.Add(new CadSpace(this, layout.LayoutName, false, layout.BlockTableRecord, layout));
            }

            ModelSpace = model;
            CurrentSpace = model;
        }

        /// <summary>The layouts of a drawing other than model space, in the order of their tabs, a tie by name.</summary>
        private static List<DxfLayout> LayoutsOf(DxfDatabase database)
        {
            var layouts = new List<DxfLayout>();

            foreach (DxfLayout layout in database.Layouts)
            {
                if (!string.Equals(layout.LayoutName, "Model", StringComparison.OrdinalIgnoreCase) && layout.BlockTableRecord != null)
                {
                    layouts.Add(layout);
                }
            }

            layouts.Sort((a, b) =>
            {
                int byTab = a.TabOrder.CompareTo(b.TabOrder);
                return byTab != 0 ? byTab : string.CompareOrdinal(a.LayoutName, b.LayoutName);
            });
            return layouts;
        }

        /// <summary>Opens a DXF file.</summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>The document.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public static CadDocument Open(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return new CadDocument(DxfDatabase.Open(path), Path.GetFullPath(path)) { _hasPath = true };
        }

        /// <summary>Opens a DXF from a stream.</summary>
        /// <param name="stream">The stream of the bytes of the file.</param>
        /// <param name="name">A name to show for it; null for none.</param>
        /// <returns>The document.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        public static CadDocument Open(Stream stream, string name = null)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            return new CadDocument(DxfDatabase.Open(stream), name);
        }

        /// <summary>Shows a database already in memory.</summary>
        /// <param name="database">The database.</param>
        /// <param name="name">A name to show for it; null for none.</param>
        /// <returns>The document.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the database is null.</exception>
        public static CadDocument From(DxfDatabase database, string name = null)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            return new CadDocument(database, name);
        }

        /// <summary>Gets the drawing.</summary>
        public DxfDatabase Database { get; private set; }

        /// <summary>Gets the full path of the file it was opened from, or the name it was given; null for neither.</summary>
        public string FileName { get; private set; }

        /// <summary>Gets the name of the file without its folder, or "Drawing" where it has none.</summary>
        public string Title => string.IsNullOrEmpty(FileName) ? "Drawing" : Path.GetFileName(FileName);

        /// <summary>Gets the spaces in the order of their tabs: model space first, then the layouts.</summary>
        public IReadOnlyList<CadSpace> Spaces => _spaces;

        /// <summary>Gets model space.</summary>
        public CadSpace ModelSpace { get; }

        /// <summary>Gets or sets the space the canvas shows; see <see cref="WorkingSpace"/> for the one edits go into.</summary>
        public CadSpace CurrentSpace { get; set; }

        /// <summary>Gets the selection.</summary>
        public CadSelection Selection { get; }

        /// <summary>Gets the layers of the drawing.</summary>
        public IReadOnlyList<CadLayerInfo> Layers
        {
            get
            {
                var layers = new List<CadLayerInfo>();

                foreach (DxfLayerTableRecord record in Database.LayerTable)
                {
                    layers.Add(new CadLayerInfo(record));
                }

                return layers;
            }
        }

        /// <summary>
        /// Gets or sets the height of the view, in drawing units, that sizes the points of a relative size (PDSIZE nought or below)
        /// when a space is next drawn, as AutoCAD sizes them by the viewport at a regeneration: with PDSIZE -10 and a view 300
        /// high, each point's symbol is 30 across. A display list already made keeps its sizes until <see cref="Regen"/>; REGEN
        /// sets it from the view of its command line first. Nought, the default, takes the height of the drawing's extents,
        /// else of its limits.
        /// </summary>
        public double PointViewHeight { get; set; }

        /// <summary>
        /// Gets the display list of a space, made on the first call and kept. After an edit, an undo or a redo the list is made
        /// again from the one before: the entities the edit's records name, and those that read a layer it switched or
        /// recoloured, are drawn anew and every other entity keeps its items, so that moving one line of a drawing of a million
        /// entities draws one line, not a million. The list is the one a REGEN would make, item for item; an edit of a table,
        /// a block or a setting that redraws everything (LTSCALE, PDMODE) still draws every entity of the spaces it touched.
        /// </summary>
        /// <param name="space">The space.</param>
        /// <returns>The items to draw.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the space is null.</exception>
        public DisplayList2 GetDisplay(CadSpace space)
        {
            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            if (!_displays.TryGetValue(space, out DisplayList2 list))
            {
                if (!space.IsModel)
                {
                    // A viewport draws the display list of model space, which may have been kept from before the builder was made.
                    _builder.UseModel(GetDisplay(ModelSpace));
                }

                if (_stale.TryGetValue(space, out StaleDisplay stale))
                {
                    // Only what the edits since the list was made changed is drawn again.
                    _stale.Remove(space);
                    list = _builder.Patch(space.Block, ReferenceEquals(space, ModelSpace), stale.List, stale.Handles, stale.List.LayerMask(stale.Layers));
                }
                else
                {
                    // Only model space's own list is the one viewports draw; the Block Editor's block is drawn as model space is, but is not it.
                    list = _builder.Build(space.Block, ReferenceEquals(space, ModelSpace));
                }

                _displays[space] = list;
            }

            return list;
        }

        /// <summary>
        /// Gets the index that picks in a space, made on the first call and kept. Its picks put the entity drawn on top first
        /// among those at about the same distance while the drawing's SORTENTS holds its bit 1, read at each call
        /// (<see cref="PickIndex.Pick"/>).
        /// </summary>
        /// <param name="space">The space.</param>
        /// <returns>The index.</returns>
        public PickIndex GetPickIndex(CadSpace space)
        {
            if (!_indexes.TryGetValue(space, out PickIndex index))
            {
                index = new PickIndex(GetDisplay(space));
                _indexes[space] = index;
            }

            index.ByDrawOrder = (Database.SortEnts & 1) != 0;
            return index;
        }

        /// <summary>Gets the entity with a handle, or null.</summary>
        /// <param name="handle">The handle.</param>
        /// <returns>The entity.</returns>
        public DxfEntity FindEntity(string handle)
        {
            if (string.IsNullOrEmpty(handle))
            {
                return null;
            }

            IndexHandles();
            return _byHandle.TryGetValue(handle, out DxfEntity found) && !found.IsErased ? found : null;
        }

        /// <summary>
        /// The entity of a handle as the index of handles holds it, erased or not: an entity an edit just erased is read still,
        /// its reactors among it, until the index is next made again.
        /// </summary>
        internal DxfEntity FindKept(string handle)
        {
            if (string.IsNullOrEmpty(handle))
            {
                return null;
            }

            IndexHandles();
            return _byHandle.TryGetValue(handle, out DxfEntity found) ? found : null;
        }

        /// <summary>Gets the space an entity of a handle is in, or null where no space holds it.</summary>
        /// <param name="handle">The handle.</param>
        /// <returns>The space.</returns>
        public CadSpace FindSpace(string handle)
        {
            if (FindEntity(handle) == null)
            {
                return null;
            }

            return _spaceByHandle.TryGetValue(handle, out CadSpace space) ? space : null;
        }

        private void IndexHandles()
        {
            if (_byHandle != null)
            {
                return;
            }

            // The entities of every space by handle, made once; the first of a handle met in tab order wins.
            _byHandle = new Dictionary<string, DxfEntity>(StringComparer.OrdinalIgnoreCase);
            _spaceByHandle = new Dictionary<string, CadSpace>(StringComparer.OrdinalIgnoreCase);

            foreach (CadSpace space in AllSpaces)
            {
                foreach (DxfEntity entity in space.Block)
                {
                    if (!string.IsNullOrEmpty(entity.Handle) && !_byHandle.ContainsKey(entity.Handle))
                    {
                        _byHandle[entity.Handle] = entity;
                        _spaceByHandle[entity.Handle] = space;
                    }
                }
            }
        }
    }
}
