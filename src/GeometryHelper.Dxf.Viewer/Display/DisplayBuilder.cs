using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Turns the entities of one space into a <see cref="DisplayList2"/>: each entity into the lines, arcs, fills and text
    /// the canvas draws, with its colour, line weight and dashes resolved from the entity, its layer and the blocks it sits
    /// in. Blocks are followed through their INSERTs under one matrix, a dimension through its block, a viewport through
    /// model space.
    /// </summary>
    internal sealed partial class DisplayBuilder
    {
        private const int MaxDepth = 12;
        private const int MaxHatchLines = 20000;
        private const double SplineSegments = 128.0;

        /// <summary>The fonts of a builder made with no document: off, every style drawn as the canvas draws it.</summary>
        private static readonly CadFonts FontsOff = new CadFonts(null, null, null);

        private readonly DxfDatabase _database;
        private readonly CadDocument _document;
        private readonly Dictionary<string, LayerState> _layers = new Dictionary<string, LayerState>(
            StringComparer.OrdinalIgnoreCase
        );

        // A layer of a drawing referenced as drawn: what the drawing showing it changed, the rest its own; made once, looked up only.
        private readonly Dictionary<string, LayerState> _merged = new Dictionary<string, LayerState>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Dictionary<string, double[]> _dashes = new Dictionary<string, double[]>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly Dictionary<string, TextFont> _fonts = new Dictionary<string, TextFont>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly double _ltScale;
        private readonly int _pdmode;
        private readonly double _pdsize;
        private double _pointViewHeight = 1.0;
        private readonly DisplayStyle _defaultStyle = new DisplayStyle(0xFFFFFF, 0.0, null);

        /// <summary>The patterns of the hatches met in the <see cref="Build"/> running, null for one drawn by its boundary; looked up only, never listed.</summary>
        private readonly Dictionary<DxfHatch, HatchPattern> _patterns = new Dictionary<DxfHatch, HatchPattern>();
        private DisplayList2 _modelCache;

        /// <summary>The entities of each block met in the <see cref="Build"/> running in the order they are drawn, by handle; looked up only, never listed.</summary>
        private readonly Dictionary<string, BlockContent> _orders = new Dictionary<string, BlockContent>(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>The styles of no dashes made, one for each colour and weight; looked up only, never listed.</summary>
        private readonly Dictionary<PlainStyle, DisplayStyle> _plainStyles = new Dictionary<PlainStyle, DisplayStyle>();

        /// <summary>The list the <see cref="Patch"/> running fills, and the bits of the layers the entity being drawn has read.</summary>
        private DisplayList2 _building;
        private ulong _runLayers;

        /// <summary>The handles of the anonymous blocks and other entities the entity being drawn has read; null for none yet.</summary>
        private List<string> _runReads;

        /// <summary>One less than how many entities of a space are drawn between two reports of <see cref="Progress"/>, a power of two.</summary>
        private const int ReportEvery = 4095;

        /// <summary>Gets or sets what is told the share of a space's entities drawn, every 4 096; null for nothing.</summary>
        internal IProgress<double> Progress { get; set; }

        /// <summary>Gets or sets what stops a space being drawn, looked at every 4 096 entities.</summary>
        internal System.Threading.CancellationToken Cancellation { get; set; }

        /// <summary>The builders of the drawings external references show, by block name and file: looked up only, never listed.</summary>
        private readonly Dictionary<string, DisplayBuilder> _referenced = new Dictionary<string, DisplayBuilder>(
            StringComparer.OrdinalIgnoreCase
        );

        /// <summary>The builder of the drawing that references this one; null for the document's own drawing.</summary>
        private readonly DisplayBuilder _host;

        /// <summary>What the layers of a drawing referenced are named after in the drawing that shows it, GRID| for GRID's; null for the document's own.</summary>
        private readonly string _prefix;

        /// <summary>The folder of the drawing a referenced one is in, which its own references are found from; null for the document's own, whose folder is read as it stands (<see cref="Folder"/>).</summary>
        private readonly string _folder;

        /// <summary>The folder references are found from: of the drawing referenced, or of the document's own as it stands now (saved since this builder was made, or not).</summary>
        private string Folder => _host == null ? _document?.DrawingFolder : _folder;

        /// <summary>Whether the frames of images are drawn (IMAGEFRAME 1 or 2), as AutoCAD 2021 draws them until set off.</summary>
        private readonly bool _imageFrames = true;

        /// <summary>The document's VISRETAIN: whether the records of the layers of its references give every property (1), or only those changed since they were last read (0).</summary>
        private readonly bool _visretain;

        public DisplayBuilder(DxfDatabase database, CadDocument document = null)
            : this(database, document, null, null, null) { }

        /// <summary>Makes the builder of a drawing an external reference shows, its layers named after the reference.</summary>
        private DisplayBuilder(
            DxfDatabase database,
            CadDocument document,
            DisplayBuilder host,
            string prefix,
            string folder
        )
        {
            _database = database;
            _document = document;
            _host = host;
            _prefix = prefix;
            _folder = folder;
            double scale = 1.0;

            // VISRETAIN of the document's drawing, which every drawing it references is shown under.
            try
            {
                _visretain = host == null ? database.Visretain : host._visretain;
            }
            catch (Exception)
            {
                _visretain = true;
            }

            try
            {
                scale = database.Ltscale;
            }
            catch (Exception)
            {
                scale = 1.0;
            }

            _ltScale = scale > 0.0 ? scale : 1.0;

            // How points are drawn, read once as LTSCALE is: a change of PDMODE or PDSIZE makes a new builder.
            try
            {
                _pdmode = database.Pdmode;
                _pdsize = database.Pdsize;
            }
            catch (Exception)
            {
                _pdmode = 0;
                _pdsize = 0.0;
            }

            foreach (DxfLayerTableRecord layer in database.LayerTable)
            {
                _layers[Prefixed(layer.Name)] = new LayerState(layer);
            }

            try
            {
                if (
                    database.NamedObjectsDictionary?.Contains("ACAD_IMAGE_VARS") == true
                    && database.NamedObjectsDictionary.GetAt("ACAD_IMAGE_VARS") is DxfRasterVariables variables
                )
                {
                    _imageFrames = variables.ImageFrame != DxfFrameSetting.ImageFrameOff;
                }
            }
            catch (Exception)
            {
                _imageFrames = true;
            }
        }

        /// <summary>Gives the builder the display list of model space its viewports draw, made before it or kept from another builder.</summary>
        internal void UseModel(DisplayList2 model) => _modelCache = model;

        internal bool IsLayerVisible(string name)
        {
            LayerState state = LayerNamed(name ?? "0");
            return state == null || state.Visible;
        }

        /// <summary>
        /// The layer of a name as it is drawn: the drawing's own; in a drawing referenced, a record of the drawing that shows
        /// it first (GRID|WALLS as AutoCAD keeps it there, or the layer of the INSERT a layer 0 takes), then its own. Under
        /// VISRETAIN 1 the record gives every property, as AutoCAD 2021 keeps it with VISRETAINMODE 0, its default, which syncs
        /// none on reload: GRID|WALLS green in the record stays green when the file makes WALLS blue. Under VISRETAIN 0 it gives
        /// only those the drawing changed since the file was last read, marked in its <see cref="CadXrefLayers"/> bits, the
        /// rest read from the file: GRID|WALLS made red in the drawing is drawn red, and left as it was, as the file has it.
        /// </summary>
        private LayerState LayerNamed(string name)
        {
            if (_host != null)
            {
                LayerState shown = _host.LayerNamed(name);

                if (shown != null)
                {
                    if (_visretain || !shown.Record.IsDependent || !_layers.TryGetValue(name, out LayerState own))
                    {
                        return shown;
                    }

                    if (!_merged.TryGetValue(name, out LayerState merged))
                    {
                        merged = new LayerState(shown, own, shown.Overrides ?? 0);
                        _merged[name] = merged;
                    }

                    return merged;
                }
            }

            return _layers.TryGetValue(name, out LayerState state) ? state : null;
        }

        /// <summary>A layer's name as the drawing showing it names it: GRID|WALLS for WALLS of the reference GRID; 0 and every layer of the document's own drawing as they are.</summary>
        private string Prefixed(string layer) => _prefix == null || layer == "0" ? layer : _prefix + layer;

        /// <summary>
        /// The layers an entity is drawn differently on by this builder than by an older one of the same drawing: switched on
        /// or off, frozen or thawed, given another colour, lineweight or linetype, added or taken away; in ordinal order.
        /// </summary>
        internal List<string> LayersChangedSince(DisplayBuilder older)
        {
            var changed = new List<string>();

            foreach (KeyValuePair<string, LayerState> pair in _layers)
            {
                if (!older._layers.TryGetValue(pair.Key, out LayerState was) || !pair.Value.DrawsAs(was))
                {
                    changed.Add(pair.Key);
                }
            }

            foreach (KeyValuePair<string, LayerState> pair in older._layers)
            {
                if (!_layers.ContainsKey(pair.Key))
                {
                    changed.Add(pair.Key);
                }
            }

            changed.Sort(StringComparer.Ordinal);
            return changed;
        }

        public DisplayList2 Build(DxfBlockTableRecord space, bool isModel) => Patch(space, isModel, null, null, 0);

        /// <summary>
        /// Draws entities that are in no space, as a command's preview makes them before it adds one: each as it would be drawn
        /// once appended to the space, in the order given; one that cannot be drawn gives nothing.
        /// </summary>
        internal IReadOnlyList<DisplayItem2> BuildLoose(IEnumerable<DxfEntity> entities) =>
            BuildLoose(entities, Affine2.Identity);

        /// <summary>Draws entities as <see cref="BuildLoose(IEnumerable{DxfEntity})"/> does, each moved by a transformation, as the objects of a paste are drawn where the cursor would put them.</summary>
        internal IReadOnlyList<DisplayItem2> BuildLoose(IEnumerable<DxfEntity> entities, Affine2 under)
        {
            var list = new DisplayList2 { PointViewHeight = _pointViewHeight, FontFolder = _document?.FontFolder };
            var root = new Frame(under);

            foreach (DxfEntity entity in entities)
            {
                try
                {
                    Draw(entity, root, list, null);
                }
                catch (Exception)
                {
                    // One entity that cannot be drawn never stops the others.
                }
            }

            return list.Items;
        }

        /// <summary>
        /// Draws a space again from the list made of it before an edit: the entities whose handles are in
        /// <paramref name="handles"/>, those that read a layer of <paramref name="layers"/> (bits of the older list's
        /// <see cref="DisplayList2.LayerBit"/>), those of no handle and those the older list did not draw are drawn anew, and
        /// every other entity's items are taken from the older list as they stand, so that a MOVE of one line in a drawing of
        /// a million entities draws one line. The list is the one <see cref="Build"/> would make, item for item, as long as the
        /// handles and layers name everything the edits changed; where the older list is null, drawn at another height of the
        /// view for points of a relative size or another font folder, or holds two entities of one handle out of their order,
        /// every entity is drawn anew.
        /// </summary>
        public DisplayList2 Patch(
            DxfBlockTableRecord space,
            bool isModel,
            DisplayList2 older,
            ISet<string> handles,
            ulong layers
        )
        {
            // A point of a relative size takes the view as it stands when its space is drawn, as AutoCAD's at a regeneration.
            if (_pdmode != 0 && _pdsize <= 0.0)
            {
                _pointViewHeight = ViewHeightOf(_database, _document);
            }

            string fontFolder = _document?.FontFolder;

            if (
                older != null
                && (
                    older.PointViewHeight != _pointViewHeight
                    || !string.Equals(older.FontFolder, fontFolder, StringComparison.Ordinal)
                )
            )
            {
                older = null;
            }

            var list = new DisplayList2(older) { PointViewHeight = _pointViewHeight, FontFolder = fontFolder };
            var root = new Frame(Affine2.Identity);

            // The patterns are kept for this build only: an edit between two builds may have changed a hatch.
            _patterns.Clear();
            _orders.Clear();
            _building = list;
            IReadOnlyList<DisplayRun> runs = older?.Runs;
            Dictionary<string, int> byHandle = null;
            int next = 0;
            HashSet<string> keys = runs == null ? null : KeysOf(runs, handles);

            // The entities the Block Editor's visibility state hides are not drawn, nor picked; those it shows are, though the
            // block keeps them invisible as its first state hides them.
            HashSet<string> shown = null,
                hidden = null;
            _document?.StateSetsOf(space, out shown, out hidden);

            try
            {
                IReadOnlyList<DxfEntity> content = ContentOf(space)?.Entities;
                int walked = 0;

                foreach (DxfEntity entity in (IEnumerable<DxfEntity>)content ?? space)
                {
                    if ((++walked & ReportEvery) == 0)
                    {
                        Cancellation.ThrowIfCancellationRequested();
                        Progress?.Report(content == null ? 0.0 : (double)walked / content.Count);
                    }

                    string handle = HandleOf(entity);

                    if (hidden != null && handle != null && hidden.Contains(handle))
                    {
                        continue;
                    }

                    if (runs != null && !string.IsNullOrEmpty(handle))
                    {
                        // The entities stand in the older list's order but where an edit added or erased one, so its runs
                        // are walked along with them, and looked up by handle only once the two part.
                        int found = -1;

                        if (
                            next < runs.Count
                            && string.Equals(runs[next].Handle, handle, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            found = next;
                        }
                        else
                        {
                            byHandle = byHandle ?? RunsByHandle(runs);

                            if (byHandle == null)
                            {
                                // Two entities of one handle out of their order: the runs cannot be told apart.
                                return Patch(space, isModel, null, null, 0);
                            }

                            if (!byHandle.TryGetValue(handle, out found))
                            {
                                found = -1;
                            }
                        }

                        if (found >= 0)
                        {
                            next = found + 1;
                            DisplayRun run = runs[found];

                            if (
                                (run.Layers & layers) == 0
                                && (handles == null || !handles.Contains(handle))
                                && !Reads(run, keys)
                            )
                            {
                                list.CopyRun(older, run);
                                continue;
                            }
                        }
                    }

                    int start = list.Count;
                    _runLayers = 0;
                    _runReads = null;

                    try
                    {
                        Draw(entity, root, list, null, shown != null && handle != null && shown.Contains(handle));
                    }
                    catch (Exception)
                    {
                        // One entity that cannot be drawn never stops the others.
                    }

                    list.AddRun(handle, start, _runLayers, _runReads?.ToArray());
                }
            }
            finally
            {
                _building = null;
                _patterns.Clear();
            }

            if (isModel)
            {
                _modelCache = list;
            }

            return list;
        }

        /// <summary>
        /// What an edit of the entities of some handles can change beyond them: the handles themselves, and the anonymous blocks
        /// and other entities those entities read when they were drawn, so that a copy of a dimension, which shares its
        /// original's picture, is drawn again when the original moves the picture. Null where no handle is named.
        /// </summary>
        private static HashSet<string> KeysOf(IReadOnlyList<DisplayRun> runs, ISet<string> handles)
        {
            if (handles == null || handles.Count == 0)
            {
                return null;
            }

            var keys = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < runs.Count; i++)
            {
                if (runs[i].Reads != null && !string.IsNullOrEmpty(runs[i].Handle) && handles.Contains(runs[i].Handle))
                {
                    keys.UnionWith(runs[i].Reads);
                }
            }

            return keys;
        }

        /// <summary>Whether a run read one of the keys an edit can have changed.</summary>
        private static bool Reads(DisplayRun run, HashSet<string> keys)
        {
            if (run.Reads == null || keys == null)
            {
                return false;
            }

            foreach (string read in run.Reads)
            {
                if (keys.Contains(read))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Notes that the entity being drawn read an anonymous block or another entity, by its handle.</summary>
        private void Read(string handle)
        {
            if (_building == null || string.IsNullOrEmpty(handle))
            {
                return;
            }

            _runReads = _runReads ?? new List<string>();

            if (!_runReads.Contains(handle))
            {
                _runReads.Add(handle);
            }
        }

        /// <summary>The handle of an entity of the space; null for none, or for one whose record cannot be read, which is drawn anew.</summary>
        private static string HandleOf(DxfEntity entity)
        {
            try
            {
                return entity?.Handle;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The place of each run by its handle; null where two runs share a handle.</summary>
        private static Dictionary<string, int> RunsByHandle(IReadOnlyList<DisplayRun> runs)
        {
            var byHandle = new Dictionary<string, int>(runs.Count, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < runs.Count; i++)
            {
                string handle = runs[i].Handle;

                if (string.IsNullOrEmpty(handle))
                {
                    continue;
                }

                if (byHandle.ContainsKey(handle))
                {
                    return null;
                }

                byHandle[handle] = i;
            }

            return byHandle;
        }

        /// <summary>
        /// The entities of a block in the order they are drawn: that of its draw order (SORTENTSTABLE, as DRAWORDER writes it),
        /// the back first, or the order of the block where it has none. Read once a build for each block, so that a block
        /// inserted 8 000 times is listed once, not 8 000 times; null where the block cannot be listed whole, to be walked as
        /// it stands each time.
        /// </summary>
        private BlockContent ContentOf(DxfBlockTableRecord block)
        {
            string handle = block.Handle;

            if (handle != null && _orders.TryGetValue(handle, out BlockContent known))
            {
                return known;
            }

            IReadOnlyList<DxfEntity> ordered = null;

            try
            {
                ordered = block.DrawOrderTable?.GetFullDrawOrder();
            }
            catch (Exception)
            {
                // A draw order that cannot be read leaves the block in its own order.
            }

            if (ordered == null)
            {
                try
                {
                    ordered = new List<DxfEntity>(block);
                }
                catch (Exception)
                {
                    ordered = null;
                }
            }

            BlockContent content = ordered == null ? null : new BlockContent(ordered, IsAnonymous(block));

            if (handle != null)
            {
                _orders[handle] = content;
            }

            return content;
        }

        /// <summary>Whether a block is anonymous (*D1, *T2, *U3): flagged so, or named with a star.</summary>
        private static bool IsAnonymous(DxfBlockTableRecord block)
        {
            try
            {
                return block.IsAnonymous
                    || (block.Name != null && block.Name.StartsWith("*", StringComparison.Ordinal));
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>The entities of a block in the order they are drawn, and whether each is shown, worked out the first time an INSERT meets it.</summary>
        private sealed class BlockContent
        {
            public BlockContent(IReadOnlyList<DxfEntity> entities, bool isAnonymous)
            {
                Entities = entities;
                Shown = new sbyte[entities.Count];
                IsAnonymous = isAnonymous;
            }

            public IReadOnlyList<DxfEntity> Entities { get; }

            public bool IsAnonymous { get; }

            /// <summary>For each entity 1 where it is shown, -1 where it is hidden (group 60), 0 until known.</summary>
            public sbyte[] Shown { get; }
        }

        private sealed class LayerState
        {
            public LayerState(DxfLayerTableRecord record)
            {
                Visible = !record.IsOff && !record.IsFrozen;
                Rgb = SafeRgb(record.Color);
                int weight = (int)record.LineWeight;
                LineWeight = weight >= 0 ? weight / 100.0 : 0.25;
                Linetype = record.LinetypeName ?? "Continuous";
                Record = record;
                Overrides = record.IsDependent ? CadXrefLayers.Overrides(record) : null;
            }

            /// <summary>
            /// A layer of a drawing referenced as the drawing showing it keeps it: what that drawing changed (the bits of
            /// <see cref="CadXrefLayers"/>) from its record, the rest from the drawing referenced.
            /// </summary>
            public LayerState(LayerState shown, LayerState own, int overrides)
            {
                bool off = (overrides & CadXrefLayers.Off) != 0 ? shown.Record.IsOff : own.Record.IsOff;
                bool frozen = (overrides & CadXrefLayers.Frozen) != 0 ? shown.Record.IsFrozen : own.Record.IsFrozen;
                Visible = !off && !frozen;
                Rgb = (overrides & CadXrefLayers.Color) != 0 ? shown.Rgb : own.Rgb;
                LineWeight = (overrides & CadXrefLayers.LineWeight) != 0 ? shown.LineWeight : own.LineWeight;
                Linetype = (overrides & CadXrefLayers.Linetype) != 0 ? shown.Linetype : own.Linetype;
                Record = shown.Record;
            }

            /// <summary>A layer as one viewport shows it: its colour, lineweight and linetype there.</summary>
            public LayerState(DxfLayerTableRecord record, DxfLayerViewportProperties look)
                : this(record)
            {
                Rgb = SafeRgb(look.Color);
                int weight = (int)look.LineWeight;
                LineWeight = weight >= 0 ? weight / 100.0 : 0.25;
                Linetype = look.Linetype ?? "Continuous";
            }

            public bool Visible { get; }

            public int Rgb { get; }

            public double LineWeight { get; }

            public string Linetype { get; }

            public DxfLayerTableRecord Record { get; }

            /// <summary>For a layer of an external reference, the properties the drawing changed of it; null where it keeps none, or is the drawing's own.</summary>
            public int? Overrides { get; }

            /// <summary>Whether an entity on this layer is drawn as on another: both shown or both hidden, of one colour, lineweight and linetype.</summary>
            public bool DrawsAs(LayerState other) =>
                Visible == other.Visible
                && Rgb == other.Rgb
                && LineWeight.Equals(other.LineWeight)
                && string.Equals(Linetype, other.Linetype, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class Frame
        {
            public Frame(Affine2 matrix)
            {
                Matrix = matrix;
                BlockRgb = 0xFFFFFF;
                BlockLineWeight = 0.25;
                BlockLinetype = "Continuous";
            }

            public Affine2 Matrix { get; set; }

            public string RootHandle { get; set; }

            public string BlockLayer { get; set; }

            public int BlockRgb { get; set; }

            public double BlockLineWeight { get; set; }

            public string BlockLinetype { get; set; }

            public int Depth { get; set; }

            public Frame Child(Affine2 matrix) =>
                new Frame(matrix)
                {
                    RootHandle = RootHandle,
                    BlockLayer = BlockLayer,
                    BlockRgb = BlockRgb,
                    BlockLineWeight = BlockLineWeight,
                    BlockLinetype = BlockLinetype,
                    Depth = Depth + 1,
                };
        }

        private static int SafeRgb(DxfColor color)
        {
            if (color == null)
            {
                return 0xFFFFFF;
            }

            return color.ColorValue & 0xFFFFFF;
        }

        private string LayerOf(DxfEntity entity, Frame frame)
        {
            string layer = entity.Layer ?? "0";
            return layer == "0" && frame.BlockLayer != null ? frame.BlockLayer : Prefixed(layer);
        }

        private DisplayStyle StyleOf(DxfEntity entity, Frame frame, string layerName)
        {
            LayerState layer = LayerNamed(layerName);
            DxfColor color = entity.Color;
            int rgb;

            if (color == null || color.IsByLayer)
            {
                rgb = layer != null ? layer.Rgb : 0xFFFFFF;
            }
            else if (color.IsByBlock)
            {
                rgb = frame.BlockRgb;
            }
            else
            {
                rgb = SafeRgb(color);
            }

            double weight;
            int raw = (int)entity.LineWeight;

            if (raw >= 0)
            {
                weight = raw / 100.0;
            }
            else if (raw == (int)DxfLineWeight.ByBlock)
            {
                weight = frame.BlockLineWeight;
            }
            else if (raw == (int)DxfLineWeight.ByLineWeightDefault)
            {
                weight = 0.25;
            }
            else
            {
                weight = layer != null ? layer.LineWeight : 0.25;
            }

            string linetype = entity.Linetype;

            if (
                string.IsNullOrEmpty(linetype) || string.Equals(linetype, "ByLayer", StringComparison.OrdinalIgnoreCase)
            )
            {
                linetype = layer != null ? layer.Linetype : "Continuous";
            }
            else if (string.Equals(linetype, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                linetype = frame.BlockLinetype;
            }

            // The entity's own linetype scale is read only for a linetype that has dashes to scale.
            double[] dashes = IsContinuous(linetype)
                ? null
                : DashesOf(linetype, entity.LinetypeScale * frame.Matrix.Scale);

            if (dashes != null)
            {
                return new DisplayStyle(rgb, weight, dashes);
            }

            // A style of no dashes is one object for each colour and weight, not one for each item: a drawing of a million
            // entities holds a few hundred, not four million.
            var key = new PlainStyle(rgb, weight);

            if (!_plainStyles.TryGetValue(key, out DisplayStyle style))
            {
                style = new DisplayStyle(rgb, weight, null);
                _plainStyles[key] = style;
            }

            return style;
        }

        /// <summary>The colour and the bits of the weight of a style of no dashes.</summary>
        private readonly struct PlainStyle : IEquatable<PlainStyle>
        {
            private readonly int _rgb;
            private readonly long _weight;

            public PlainStyle(int rgb, double weight)
            {
                _rgb = rgb;
                _weight = BitConverter.DoubleToInt64Bits(weight);
            }

            public bool Equals(PlainStyle other) => _rgb == other._rgb && _weight == other._weight;

            public override bool Equals(object obj) => obj is PlainStyle other && Equals(other);

            public override int GetHashCode() => (_rgb * 397) ^ _weight.GetHashCode();
        }

        private static bool IsContinuous(string linetype) =>
            string.IsNullOrEmpty(linetype) || string.Equals(linetype, "Continuous", StringComparison.OrdinalIgnoreCase);

        private double[] DashesOf(string linetype, double scale)
        {
            if (IsContinuous(linetype))
            {
                return null;
            }

            if (!_dashes.TryGetValue(linetype, out double[] pattern))
            {
                pattern = null;

                try
                {
                    DxfLinetypeTableRecord record = _database.LinetypeTable[linetype];
                    int n = record.NumDashes;

                    if (n > 0)
                    {
                        pattern = new double[n];

                        for (int i = 0; i < n; i++)
                        {
                            pattern[i] = record.DashLengthAt(i);
                        }
                    }
                }
                catch (Exception)
                {
                    pattern = null;
                }

                _dashes[linetype] = pattern;
            }

            if (pattern == null)
            {
                return null;
            }

            double factor = _ltScale * (scale > 0.0 ? scale : 1.0);
            var result = new double[pattern.Length];

            for (int i = 0; i < pattern.Length; i++)
            {
                result[i] = Math.Abs(pattern[i]) * factor;
            }

            return result;
        }

        /// <summary>What a text style is drawn in: its SHX font found, else the name of its typeface or file the canvas takes; with the document's fonts off, the typeface it names, else the file of its font, the name null where it has neither.</summary>
        private CadFontFace FaceOf(string styleName) => Style(styleName).Face;

        /// <summary>The slant of a text style's letters in radians.</summary>
        private double ObliqueOf(string styleName) => Style(styleName).Oblique;

        /// <summary>How wide a text style's letters are for their height: 1 where the style gives none or is not found.</summary>
        private double WidthFactorOf(string styleName) => Style(styleName).WidthFactor;

        private TextFont Style(string styleName)
        {
            string key = styleName ?? string.Empty;

            if (!_fonts.TryGetValue(key, out TextFont font))
            {
                font = new TextFont(null, 0.0, 1.0);

                try
                {
                    DxfTextStyleTableRecord record = _database.TextStyleTable[
                        string.IsNullOrEmpty(styleName) ? "Standard" : styleName
                    ];

                    if (record != null)
                    {
                        font = new TextFont(
                            Resolve(record.FileName, record.BigFontFileName, record.Font.TypeFace),
                            record.ObliquingAngle,
                            record.XScale
                        );
                    }
                }
                catch (Exception)
                {
                    font = new TextFont(null, 0.0, 1.0);
                }

                _fonts[key] = font;
            }

            return font;
        }

        /// <summary>
        /// The face of an MTEXT's font code before its words (<see cref="FontCodeOf"/>), found as a style's font is; null where it
        /// has none or the document's fonts are off, its style's face then drawn.
        /// </summary>
        private CadFontFace FaceOfCode(string contents)
        {
            if (_document == null || _document.Fonts.IsOff)
            {
                return null;
            }

            string name = FontCodeOf(contents, out bool shx);

            if (name == null)
            {
                return null;
            }

            string key = "\\" + (shx ? "F" : "f") + name;

            if (!_fonts.TryGetValue(key, out TextFont font))
            {
                font = new TextFont(shx ? Resolve(name, null, null) : Resolve(null, null, name), 0.0, 1.0);
                _fonts[key] = font;
            }

            return font.Face;
        }

        /// <summary>The face of a font as the document's fonts find it, a font not found said once on its <see cref="CadDocument.FontMissing"/>.</summary>
        private CadFontFace Resolve(string fileName, string bigFontFileName, string typeface)
        {
            CadFontFace face = (_document?.Fonts ?? FontsOff).Resolve(
                fileName,
                bigFontFileName,
                typeface,
                _document?.FontFolder,
                out string report
            );

            if (report != null)
            {
                _document?.SayFontMissing(report);
            }

            return face;
        }

        private sealed class TextFont
        {
            public TextFont(CadFontFace face, double oblique, double widthFactor)
            {
                Face = face ?? new CadFontFace(null, null, null, null);
                Oblique = oblique;
                WidthFactor = widthFactor > 0.0 && !double.IsInfinity(widthFactor) ? widthFactor : 1.0;
            }

            public CadFontFace Face { get; }

            public double Oblique { get; }

            public double WidthFactor { get; }
        }

        /// <summary>Draws an entity under a frame; <paramref name="shown"/> where its block has found it shown already, so that it is not read again.</summary>
        private void Draw(DxfEntity entity, Frame frame, DisplayList2 list, string forcedHandle, bool shown = false)
        {
            if (entity == null || frame.Depth > MaxDepth)
            {
                return;
            }

            string layerName = LayerOf(entity, frame);

            if (_building != null)
            {
                _runLayers |= _building.LayerBit(layerName);
            }
            else if (_host != null)
            {
                // What an external reference draws counts in the run of its INSERT, so that a change of grid|GRID in the
                // drawing showing it draws the INSERT anew.
                DisplayBuilder root = _host;

                while (root._host != null)
                {
                    root = root._host;
                }

                if (root._building != null)
                {
                    root._runLayers |= root._building.LayerBit(layerName);
                }
            }

            // The layer first: an entity's own visibility is read from its groups, near a microsecond each, which an entity on a layer
            // switched off or frozen never needs.
            if (!IsLayerVisible(layerName) || (!shown && !entity.Visible))
            {
                return;
            }

            string handle = forcedHandle ?? frame.RootHandle ?? entity.Handle ?? string.Empty;
            DisplayStyle style = StyleOf(entity, frame, layerName);

            switch (entity)
            {
                case DxfViewport viewport:
                    DrawViewport(viewport, frame, list, style, handle, layerName);
                    return;
                case DxfDimension dimension:
                    DrawBlock(dimension.DimBlock, Affine2.Identity, dimension, frame, list, handle, layerName, style);
                    return;
                case DxfBlockReference reference:
                    // The snap Insertion takes the point of an INSERT of the space itself, not of one nested in a block.
                    if (frame.RootHandle == null && forcedHandle == null)
                    {
                        GeoPoint3 at = reference.Position;
                        list.AddInsertionPoint(handle, frame.Matrix.X(at.X, at.Y), frame.Matrix.Y(at.X, at.Y));
                    }

                    DrawInsert(reference, frame, list, handle, layerName, style);
                    return;
                case DxfAttributeDefinition definition when frame.RootHandle != null && !definition.Constant:
                    // An INSERT shows the values of its ATTRIBs, not the definitions of its block, as AutoCAD shows it; a constant
                    // definition, which has no ATTRIB, shows its value.
                    return;
                case DxfDBText text:
                {
                    int before = list.Count;
                    DrawText(text, frame, list, style, handle, layerName);

                    // A TEXT of the space and an ATTRIB shown give the snap Insertion the point they are placed by.
                    if (
                        (text is DxfAttributeReference || (frame.RootHandle == null && forcedHandle == null))
                        && list.Count > before
                        && list.Items[list.Count - 1] is DisplayText2 drawn
                    )
                    {
                        list.AddInsertionPoint(handle, drawn.X, drawn.Y);
                    }

                    return;
                }

                case DxfMText mtext:
                {
                    int before = list.Count;
                    DrawMText(mtext, frame, list, style, handle, layerName);

                    if (
                        frame.RootHandle == null
                        && forcedHandle == null
                        && list.Count > before
                        && list.Items[list.Count - 1] is DisplayText2 drawn
                    )
                    {
                        list.AddInsertionPoint(handle, drawn.X, drawn.Y);
                    }

                    return;
                }
                case DxfHatch hatch:
                    DrawHatch(hatch, frame, list, style, handle, layerName);
                    return;
                case DxfMLeader mleader:
                    foreach (DxfEntity part in mleader.Explode())
                    {
                        Draw(part, frame, list, handle);
                    }

                    return;
                case DxfTolerance tolerance:
                    // A feature control frame is drawn by its lines, symbols and words, laid out in the drawing's fonts.
                    foreach (DxfEntity part in CadToleranceFrame.Of(tolerance, LayoutOf).Parts)
                    {
                        Draw(part, frame, list, handle);
                    }

                    return;
                case DxfTable table:
                {
                    // A table is drawn by the block AutoCAD drew it with, placed at its point: the block's entities stand turned
                    // along the table's direction already, as AutoCAD draws them and TransformBy carries them.
                    GeoPoint3 at = table.Position;
                    DrawBlock(
                        table.BlockTableRecord,
                        Affine2.Translation(at.X, at.Y),
                        table,
                        frame,
                        list,
                        handle,
                        layerName,
                        style
                    );
                    return;
                }

                case DxfLeader leader
                    when ReadAnnotation(leader) && TryExplode(leader, out IReadOnlyList<DxfEntity> parts):
                    // A leader is drawn as AutoCAD breaks it: its arrowhead, then its line or spline.
                    foreach (DxfEntity part in parts)
                    {
                        Draw(part, frame, list, handle);
                    }

                    return;
                case DxfDBPoint point when _pdmode != 0:
                    AddPoint(point, frame.Matrix, list, style, handle, layerName);
                    return;
                case DxfRasterImage picture when !(picture is DxfWipeout):
                    DrawImage(picture, frame, list, style, handle, layerName);
                    return;
                case DxfPolyline wide when wide.HasWidth:
                    if (AddWide(wide, frame.Matrix, list, style, handle, layerName))
                    {
                        return;
                    }

                    break;
            }

            IReadOnlyList<object> shapes = entity.GetGeometry();
            bool mask = entity is DxfWipeout;
            bool fill = entity is DxfSolid;

            foreach (object shape in shapes)
            {
                AddShape(shape, frame.Matrix, list, style, handle, layerName, fill, mask);
            }
        }

        /// <summary>Notes the words a leader points to, whose place and width its hook runs on by; always true.</summary>
        private bool ReadAnnotation(DxfLeader leader)
        {
            if (_building != null)
            {
                try
                {
                    Read(leader.Annotation?.Handle);
                }
                catch (Exception)
                {
                    // A leader whose annotation cannot be read points to none.
                }
            }

            return true;
        }

        private static bool TryExplode(DxfEntity entity, out IReadOnlyList<DxfEntity> parts)
        {
            try
            {
                parts = entity.Explode();
                return parts.Count > 0;
            }
            catch (Exception)
            {
                parts = null;
                return false;
            }
        }

        private void DrawInsert(
            DxfBlockReference reference,
            Frame frame,
            DisplayList2 list,
            string handle,
            string layerName,
            DisplayStyle style
        )
        {
            DxfBlockTableRecord block = reference.BlockTableRecord;

            if (block == null)
            {
                return;
            }

            GeoTransform3 transform = reference.BlockTransform;
            Affine2 own = new Affine2(
                transform[0, 0],
                transform[1, 0],
                transform[0, 1],
                transform[1, 1],
                transform[0, 3],
                transform[1, 3]
            );

            if (block.IsFromExternalReference)
            {
                DrawXref(reference, block, own, frame, list, handle, layerName, style);
                return;
            }

            int columns = 1;
            int rows = 1;
            double columnSpacing = 0.0;
            double rowSpacing = 0.0;

            if (reference is DxfMInsertBlock array)
            {
                columns = Math.Max(1, (int)array.Columns);
                rows = Math.Max(1, (int)array.Rows);
                columnSpacing = array.ColumnSpacing;
                rowSpacing = array.RowSpacing;
            }

            double rotation = own.Rotation;
            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    double dx = c * columnSpacing;
                    double dy = r * rowSpacing;
                    Affine2 placed = own.Then(Affine2.Translation(dx * cos - dy * sin, dx * sin + dy * cos));
                    DrawBlock(block, placed, reference, frame, list, handle, layerName, style);
                }
            }

            DrawAttributes(reference, frame, list, handle, layerName, style);
        }

        /// <summary>
        /// An INSERT of an external reference: the model space of the drawing it names, found and read by the document's
        /// <see cref="CadXrefManager"/>, drawn under the INSERT with its base point ($INSBASE) at the insertion point, picked as
        /// the INSERT, its layers named after the reference (GRID|WALLS) and its layer 0 taking the INSERT's, as AutoCAD shows
        /// one. Its own references are followed, but not its overlays, nor a drawing being drawn already. One unloaded shows
        /// nothing; one not found or not read shows its path at the insertion point, as AutoCAD shows it.
        /// </summary>
        private void DrawXref(
            DxfBlockReference reference,
            DxfBlockTableRecord block,
            Affine2 own,
            Frame frame,
            DisplayList2 list,
            string handle,
            string layerName,
            DisplayStyle style
        )
        {
            if (_document == null || (_host != null && block.IsFromOverlayReference))
            {
                return;
            }

            CadXrefManager xrefs = _document.Xrefs;
            CadXrefContent content = xrefs.ContentOf(block, Folder, _host == null, out string missing);

            if (content == null)
            {
                if (missing != null)
                {
                    DrawMissing(missing, reference, own, frame, list, handle, layerName, style);
                }

                return;
            }

            if (!xrefs.Enter(content))
            {
                return;
            }

            try
            {
                string key = block.Name + "|" + content.FullPath;

                if (!_referenced.TryGetValue(key, out DisplayBuilder builder))
                {
                    builder = new DisplayBuilder(
                        content.Database,
                        _document,
                        this,
                        block.Name + "|",
                        System.IO.Path.GetDirectoryName(content.FullPath)
                    );
                    _referenced[key] = builder;
                }

                // The drawing's base point comes to the insertion point, in place of the origin of the block that names it.
                GeoPoint3 origin = block.Origin;
                GeoPoint3 basePoint = GeoPoint3.Origin;

                try
                {
                    basePoint = content.Database.Insbase;
                }
                catch (Exception)
                {
                    basePoint = GeoPoint3.Origin;
                }

                Affine2 placed = Affine2.Translation(origin.X - basePoint.X, origin.Y - basePoint.Y).Then(own);
                Frame inner = frame.Child(placed.Then(frame.Matrix));
                inner.RootHandle = handle;
                inner.BlockLayer = layerName;
                inner.BlockRgb = style.Rgb;
                inner.BlockLineWeight = style.LineWeight;
                builder.DrawReferenced(inner, list);
            }
            finally
            {
                xrefs.Leave();
            }
        }

        /// <summary>The model space of a drawing referenced, drawn into the list of the drawing showing it under a frame.</summary>
        private void DrawReferenced(Frame frame, DisplayList2 list)
        {
            foreach (
                DxfEntity entity in (IEnumerable<DxfEntity>)ContentOf(_database.ModelSpace)?.Entities
                    ?? _database.ModelSpace
            )
            {
                try
                {
                    Draw(entity, frame, list, null);
                }
                catch (Exception)
                {
                    // One entity of a drawing referenced that cannot be drawn never stops the others.
                }
            }
        }

        /// <summary>The path of an external reference not found, written at its insertion point in the drawing's text height.</summary>
        private void DrawMissing(
            string path,
            DxfBlockReference reference,
            Affine2 own,
            Frame frame,
            DisplayList2 list,
            string handle,
            string layerName,
            DisplayStyle style
        )
        {
            double height = 2.5;

            try
            {
                height = _database.Textsize > 0.0 ? _database.Textsize : 2.5;
            }
            catch (Exception)
            {
                height = 2.5;
            }

            GeoPoint3 at = reference.Position;
            Affine2 m = frame.Matrix;
            list.Add(
                Text(
                    FaceOf(null),
                    path,
                    m.X(at.X, at.Y),
                    m.Y(at.X, at.Y),
                    height * own.Scale * m.Scale,
                    own.Rotation + m.Rotation,
                    1.0,
                    0,
                    0,
                    style,
                    handle,
                    layerName,
                    0.0,
                    false
                )
            );
        }

        /// <summary>
        /// An IMAGE: its picture (<see cref="DisplayImage2"/>), the file its definition names found as an external reference is,
        /// then its frame where IMAGEFRAME shows frames. A picture unloaded, not found or not shown (its Show image off) is drawn
        /// as its frame alone, whatever IMAGEFRAME says; one not found has its file's name written in its corner, as AutoCAD
        /// shows it.
        /// </summary>
        private void DrawImage(
            DxfRasterImage image,
            Frame frame,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layerName
        )
        {
            image.GetOrientation(out GeoPoint3 origin, out GeoVector3 u, out GeoVector3 v);
            Affine2 m = frame.Matrix;
            double x = m.X(origin.X, origin.Y);
            double y = m.Y(origin.X, origin.Y);
            double ux = m.X(origin.X + u.X, origin.Y + u.Y) - x;
            double uy = m.Y(origin.X + u.X, origin.Y + u.Y) - y;
            double vx = m.X(origin.X + v.X, origin.Y + v.Y) - x;
            double vy = m.Y(origin.X + v.X, origin.Y + v.Y) - y;
            double[] ring = null;
            IReadOnlyList<GeoPoint3> corners = image.GetVertices();

            if (image.IsClipped && corners.Count >= 4)
            {
                ring = new double[2 * (corners.Count - 1)];

                for (int i = 0; i + 1 < corners.Count; i++)
                {
                    ring[2 * i] = m.X(corners[i].X, corners[i].Y);
                    ring[2 * i + 1] = m.Y(corners[i].X, corners[i].Y);
                }
            }

            DxfRasterImageDef definition = image.ImageDef;
            string saved = definition?.SourceFileName ?? string.Empty;
            string found = null;
            bool loaded = definition != null && image.ShowImage;

            if (loaded && _document != null)
            {
                string name = CadXrefManager.ImageName(_database, definition);
                loaded = _host != null || name == null || _document.Xrefs.IsImageLoaded(name, definition);
                found = loaded ? _document.Xrefs.Locate(saved, Folder) : null;
            }

            var picture = new DisplayImage2(
                x,
                y,
                ux,
                uy,
                vx,
                vy,
                ring == null ? null : new[] { ring },
                ring != null && image.IsClipInverted,
                found,
                saved,
                style,
                handle,
                layerName
            )
            {
                IsTransparent = image.ImageTransparency,
                Brightness = image.Brightness,
                Contrast = image.Contrast,
                Fade = image.Fade,
                FileStamp = Stamp(found),
            };
            list.Add(picture);

            if (!_imageFrames && found != null)
            {
                return;
            }

            foreach (object shape in image.GetGeometry())
            {
                AddShape(shape, m, list, style, handle, layerName, false, false);
            }

            if (found == null && loaded && saved.Length > 0)
            {
                // The file's name in the corner of the frame, a tenth of its shorter side high.
                double width = Math.Sqrt(ux * ux + uy * uy);
                double height = Math.Sqrt(vx * vx + vy * vy);
                double size = Math.Min(width, height) / 10.0;

                if (size > 0.0)
                {
                    double ax = ux / width;
                    double ay = uy / width;
                    double bx = vx / height;
                    double by = vy / height;
                    list.Add(
                        Text(
                            FaceOf(null),
                            saved,
                            x + (ax + bx) * size / 2.0,
                            y + (ay + by) * size / 2.0,
                            size,
                            Math.Atan2(uy, ux),
                            1.0,
                            0,
                            0,
                            style,
                            handle,
                            layerName,
                            0.0,
                            false
                        )
                    );
                }
            }
        }

        /// <summary>The last write time of a file in ticks, so that a picture changed on disk is read again; nought for none.</summary>
        private static long Stamp(string path)
        {
            if (path == null)
            {
                return 0L;
            }

            try
            {
                return System.IO.File.GetLastWriteTimeUtc(path).Ticks;
            }
            catch (Exception)
            {
                return 0L;
            }
        }

        /// <summary>
        /// The ATTRIBs of an INSERT, its values, as a title block shows them: each a text where the file places it, in the
        /// space of the INSERT (its own place, turn and scale are in the ATTRIB already), under the frame the INSERT is in,
        /// picked as the INSERT, ByBlock taking the INSERT's look; an invisible one left out. A MINSERT shows them once.
        /// </summary>
        private void DrawAttributes(
            DxfBlockReference reference,
            Frame frame,
            DisplayList2 list,
            string handle,
            string layerName,
            DisplayStyle style
        )
        {
            Frame inner = frame.Child(frame.Matrix);
            inner.RootHandle = handle;
            inner.BlockLayer = layerName;
            inner.BlockRgb = style.Rgb;
            inner.BlockLineWeight = style.LineWeight;

            foreach (DxfAttributeReference attribute in reference.AttributeCollection)
            {
                try
                {
                    if (!attribute.IsErased && !attribute.Invisible)
                    {
                        Draw(attribute, inner, list, null);
                    }
                }
                catch (Exception)
                {
                    // One attribute that cannot be drawn never stops the others.
                }
            }
        }

        private void DrawBlock(
            DxfBlockTableRecord block,
            Affine2 placement,
            DxfEntity owner,
            Frame frame,
            DisplayList2 list,
            string handle,
            string layerName,
            DisplayStyle style
        )
        {
            if (block == null)
            {
                return;
            }

            Frame inner = frame.Child(placement.Then(frame.Matrix));
            inner.RootHandle = handle;
            inner.BlockLayer = layerName;
            inner.BlockRgb = style.Rgb;
            inner.BlockLineWeight = style.LineWeight;

            BlockContent content = ContentOf(block);

            if (_building != null && (content?.IsAnonymous ?? IsAnonymous(block)))
            {
                // A dimension's or a table's picture may be shared with a copy, and moved by an edit of the other.
                Read(block.Handle);
            }

            if (content == null)
            {
                foreach (DxfEntity entity in block)
                {
                    try
                    {
                        Draw(entity, inner, list, null);
                    }
                    catch (Exception)
                    {
                        // One entity of a block that cannot be drawn never stops the others.
                    }
                }

                return;
            }

            IReadOnlyList<DxfEntity> entities = content.Entities;
            sbyte[] shown = content.Shown;

            for (int i = 0; i < entities.Count; i++)
            {
                try
                {
                    // Whether an entity of the block is shown is read once a build, not once an INSERT.
                    DxfEntity entity = entities[i];

                    if (shown[i] == 0 && entity != null)
                    {
                        shown[i] = entity.Visible ? (sbyte)1 : (sbyte)-1;
                    }

                    if (shown[i] >= 0)
                    {
                        Draw(entity, inner, list, null, shown[i] > 0);
                    }
                }
                catch (Exception)
                {
                    // One entity of a block that cannot be drawn never stops the others.
                }
            }
        }

        private void DrawViewport(
            DxfViewport viewport,
            Frame frame,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layerName
        )
        {
            if (
                viewport.Number <= 1
                || !viewport.On
                || _modelCache == null
                || viewport.Height <= 0.0
                || viewport.ViewHeight <= 0.0
            )
            {
                return;
            }

            var frozen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfLayerTableRecord layer in viewport.GetFrozenLayers())
            {
                frozen.Add(layer.Name);
            }

            double scale = viewport.Height / viewport.ViewHeight;
            GeoPoint3 center = viewport.CenterPoint;
            GeoPoint2 view = CadViewports.FromView(viewport, viewport.ViewCenter);
            double twist = viewport.TwistAngle;
            list.Add(
                new DisplayViewport2(
                    center.X,
                    center.Y,
                    viewport.Width,
                    viewport.Height,
                    view.X,
                    view.Y,
                    scale,
                    twist,
                    ModelSeenThrough(viewport),
                    frozen,
                    style,
                    handle,
                    layerName
                )
            );
        }

        /// <summary>
        /// The display list of model space a viewport shows: that of model space as it stands, or, where a layer looks otherwise
        /// in the viewport (its colour, linetype or lineweight overridden there, as the Layer manager's VP columns set them),
        /// model space drawn again with those layers as the viewport has them, as AutoCAD regenerates each viewport.
        /// </summary>
        private DisplayList2 ModelSeenThrough(DxfViewport viewport)
        {
            List<DxfLayerViewportProperties> looks = null;

            foreach (DxfLayerTableRecord layer in _database.LayerTable)
            {
                if (layer.HasViewportOverrides(viewport))
                {
                    looks = looks ?? new List<DxfLayerViewportProperties>();
                    looks.Add(layer.GetViewportOverrides(viewport));
                }
            }

            if (looks == null || _database.ModelSpace == null)
            {
                return _modelCache;
            }

            var inner = new DisplayBuilder(_database, _document);

            foreach (DxfLayerViewportProperties look in looks)
            {
                inner._layers[look.Layer.Name] = new LayerState(look.Layer, look);
            }

            return inner.Build(_database.ModelSpace, false);
        }

        private void DrawText(
            DxfDBText text,
            Frame frame,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layerName
        )
        {
            // The spaces before and after the words kept: AutoCAD justifies the words by them (mirror-insert GHMI1), "ABC " on the
            // right of its baseline a space further left than "ABC".
            string value = CleanText(text.TextString, false).Trim('\n');

            if (value.Trim().Length == 0 || text.Height <= 0.0)
            {
                return;
            }

            bool simple = text.IsDefaultAlignment;
            GeoPoint3 at = simple ? text.Position : text.AlignmentPoint;
            int horizontal;

            switch (text.HorizontalMode)
            {
                case DxfTextHorizontalMode.TextCenter:
                case DxfTextHorizontalMode.TextMid:
                    horizontal = 1;
                    break;
                case DxfTextHorizontalMode.TextRight:
                    horizontal = 2;
                    break;
                default:
                    horizontal = 0;
                    break;
            }

            int vertical;

            switch (text.VerticalMode)
            {
                case DxfTextVerticalMode.TextBottom:
                    vertical = 1;
                    break;
                case DxfTextVerticalMode.TextVerticalMid:
                    vertical = 2;
                    break;
                case DxfTextVerticalMode.TextTop:
                    vertical = 3;
                    break;
                default:
                    vertical = 0;
                    break;
            }

            Affine2 m = frame.Matrix;
            double turn = Orient(m, text.Rotation, text.IsMirroredInX, text.IsMirroredInY, out bool backward);
            list.Add(
                Text(
                    FaceOf(text.TextStyleName),
                    value,
                    m.X(at.X, at.Y),
                    m.Y(at.X, at.Y),
                    text.Height * m.Scale,
                    turn,
                    text.WidthFactor,
                    horizontal,
                    vertical,
                    style,
                    handle,
                    layerName,
                    text.Oblique,
                    backward
                )
            );
        }

        /// <summary>
        /// The width the display gives the words of a text (<see cref="DisplayText2.EstimatedWidth"/>), in the text's own units:
        /// by the advances of its SHX font's letters, else 0.6 of its height a letter; nought for a text of no words.
        /// </summary>
        internal double WidthOf(DxfDBText text)
        {
            string value = CleanText(text.TextString);

            if (value.Length == 0 || !(text.Height > 0.0))
            {
                return 0.0;
            }

            return Text(
                FaceOf(text.TextStyleName),
                value,
                0.0,
                0.0,
                text.Height,
                0.0,
                text.WidthFactor,
                0,
                0,
                null,
                null,
                null,
                text.Oblique,
                false
            ).EstimatedWidth;
        }

        /// <summary>
        /// Where the words of a text start and where their ink runs, along its baseline from the point it is placed by, in the
        /// text's own units, as AutoCAD 2021's MIRROR measures them with MIRRTEXT 0 (probed in its console): in an SHX font by the
        /// strokes the display draws, the words justified by the box of their ink; in a TrueType font by the outlines of its
        /// file, the ink read as though the words started after any spaces before them, as AutoCAD reads it, and the words
        /// justified by their advances, centred from the ink's left where it is left of their start. "ABC" of arial.ttf 2.5 high
        /// on the left is inked from -0.0051 to 7.0430; centred, it starts at -3.5880. False where the font's letters are not read.
        /// </summary>
        internal bool InkOf(DxfDBText text, out double start, out double left, out double right)
        {
            start = 0.0;
            left = 0.0;
            right = 0.0;

            // The spaces before and after the words kept, which AutoCAD justifies by and the display leaves out.
            string value = CleanText(text.TextString, false);

            if (value.Trim().Length == 0 || !(text.Height > 0.0))
            {
                return false;
            }

            CadFontFace face = FaceOf(text.TextStyleName);
            double factor = text.WidthFactor <= 0.0 ? 1.0 : text.WidthFactor;
            int horizontal =
                text.HorizontalMode == DxfTextHorizontalMode.TextCenter
                || text.HorizontalMode == DxfTextHorizontalMode.TextMid
                    ? 1
                : text.HorizontalMode == DxfTextHorizontalMode.TextRight ? 2
                : 0;

            if (face.Shx != null)
            {
                // The ink from where the words start, then from the point they are justified at: the difference is their start.
                if (
                    !ShxInk(face, value, text.Height, factor, 0, out double from, out _)
                    || !ShxInk(face, value, text.Height, factor, horizontal, out left, out right)
                )
                {
                    return false;
                }

                start = left - from;
                return true;
            }

            CadTextInk ink = CadTextInk.Of(face, false, false);

            if (!ink.IsRead)
            {
                return false;
            }

            double size = text.Height * factor;
            double pen = 0.0;
            double skipped = 0.0;
            double first = double.MaxValue;
            double inkLeft = double.MaxValue;
            double inkRight = double.MinValue;
            bool begun = false;
            int i = 0;

            while (i < value.Length)
            {
                CadTextInk.Letter letter = ink.Next(value, ref i);

                if (letter.HasInk)
                {
                    begun = true;
                    first = Math.Min(first, pen + letter.Left);
                    inkLeft = Math.Min(inkLeft, pen - skipped + letter.Left);
                    inkRight = Math.Max(inkRight, pen - skipped + letter.Right);
                }
                else if (!begun)
                {
                    skipped += letter.Advance;
                }

                pen += letter.Advance;
            }

            if (inkLeft > inkRight)
            {
                return false;
            }

            double advance = pen * size;
            start =
                horizontal == 0 ? 0.0
                : horizontal == 1 ? -(advance + Math.Min(0.0, first * size)) / 2.0
                : -advance;
            left = start + (inkLeft * size);
            right = start + (inkRight * size);
            return true;
        }

        /// <summary>
        /// How far below and above its baseline the ink of a text runs, as AutoCAD 2021's MIRROR measures it with MIRRTEXT 0
        /// (probed in its console, mirror-insert GHMI1): in an SHX font by the strokes the display draws, in a TrueType font by
        /// the outlines of its file. "Q1" of arial.ttf 1.5 high reaches below its baseline by the tail of its Q, so that it is
        /// turned over about a line 0.705 above its baseline, not 0.75. False where the font's letters are not read, and where
        /// the words have no ink.
        /// </summary>
        internal bool InkHeightOf(DxfDBText text, out double bottom, out double top)
        {
            bottom = double.MaxValue;
            top = double.MinValue;
            string value = CleanText(text.TextString, false);

            if (value.Trim().Length == 0 || !(text.Height > 0.0))
            {
                return false;
            }

            CadFontFace face = FaceOf(text.TextStyleName);

            if (face.Shx == null)
            {
                // A TrueType font only where the style names one: an SHX font the display has not, drawn in another, is not
                // measured by that other, whose letters stand unlike the missing font's.
                CadTextInk ink = CadTextInk.Of(face, false, false);

                if (!ink.IsRead || !NamesTrueType(text.TextStyleName))
                {
                    return false;
                }

                int i = 0;

                while (i < value.Length)
                {
                    CadTextInk.Letter letter = ink.Next(value, ref i);

                    if (letter.HasInk)
                    {
                        bottom = Math.Min(bottom, letter.Bottom * text.Height);
                        top = Math.Max(top, letter.Top * text.Height);
                    }
                }

                return bottom <= top;
            }

            double factor = text.WidthFactor <= 0.0 ? 1.0 : text.WidthFactor;

            foreach (
                double[] stroke in ShxText.Strokes(
                    face,
                    value,
                    0.0,
                    0.0,
                    text.Height,
                    0.0,
                    factor,
                    0,
                    0,
                    0.0,
                    false,
                    0.0,
                    out _
                )
            )
            {
                for (int p = 1; p < stroke.Length; p += 2)
                {
                    bottom = Math.Min(bottom, stroke[p]);
                    top = Math.Max(top, stroke[p]);
                }
            }

            return bottom <= top;
        }

        /// <summary>Whether a text style names a TrueType font: a file of .ttf, .ttc or .otf, or a typeface; false for one not found.</summary>
        private bool NamesTrueType(string styleName)
        {
            try
            {
                DxfTextStyleTableRecord record = _database.TextStyleTable[
                    string.IsNullOrEmpty(styleName) ? "Standard" : styleName
                ];

                if (record == null)
                {
                    return false;
                }

                string file = record.FileName ?? string.Empty;
                return file.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrEmpty(record.Font.TypeFace);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The left and right of the strokes of some words in an SHX font, upright, from the point they are justified at; false where they have none.</summary>
        private static bool ShxInk(
            CadFontFace face,
            string value,
            double height,
            double factor,
            int horizontal,
            out double left,
            out double right
        )
        {
            left = double.MaxValue;
            right = double.MinValue;

            foreach (
                double[] stroke in ShxText.Strokes(
                    face,
                    value,
                    0.0,
                    0.0,
                    height,
                    0.0,
                    factor,
                    horizontal,
                    0,
                    0.0,
                    false,
                    0.0,
                    out _
                )
            )
            {
                for (int p = 0; p + 1 < stroke.Length; p += 2)
                {
                    left = Math.Min(left, stroke[p]);
                    right = Math.Max(right, stroke[p]);
                }
            }

            return left <= right;
        }

        /// <summary>
        /// A text drawn in a face: by the canvas in the face's name where it is no SHX font; else as the strokes of its letters
        /// (<see cref="ShxText"/>), its width that of their advances and its frame reaching down to the font's depth below the
        /// baseline, by which a text aligned at its bottom stands, as AutoCAD stands one.
        /// </summary>
        private static DisplayText2 Text(
            CadFontFace face,
            string value,
            double x,
            double y,
            double height,
            double rotation,
            double widthFactor,
            int horizontal,
            int vertical,
            DisplayStyle style,
            string handle,
            string layerName,
            double oblique,
            bool backward
        )
        {
            if (face.Shx == null)
            {
                return new DisplayText2(
                    value,
                    x,
                    y,
                    height,
                    rotation,
                    widthFactor,
                    horizontal,
                    vertical,
                    style,
                    handle,
                    layerName,
                    face.Name,
                    oblique,
                    backward
                );
            }

            double factor = widthFactor <= 0.0 ? 1.0 : widthFactor;
            double descent = height * face.Shx.Below / face.Shx.Above;
            List<double[]> strokes = ShxText.Strokes(
                face,
                value,
                x,
                y,
                height,
                rotation,
                factor,
                horizontal,
                vertical,
                oblique,
                backward,
                descent,
                out double width
            );
            return new DisplayText2(
                value,
                x,
                y,
                height,
                rotation,
                factor,
                horizontal,
                vertical,
                style,
                handle,
                layerName,
                face.Name,
                oblique,
                backward,
                strokes,
                width,
                descent
            );
        }

        private void DrawMText(
            DxfMText text,
            Frame frame,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layerName
        )
        {
            string value = CleanText(text.Text);

            if (value.Length == 0 || text.TextHeight <= 0.0)
            {
                return;
            }

            GeoPoint3 at = text.Location;
            int attach = (int)text.Attachment;
            int horizontal =
                attach == 2 || attach == 5 || attach == 8 ? 1
                : attach == 3 || attach == 6 || attach == 9 ? 2
                : 0;
            int vertical =
                attach <= 3 ? 3
                : attach <= 6 ? 2
                : 1;
            GeoVector3 direction = text.Direction;
            double rotation = Math.Atan2(direction.Y, direction.X);
            Affine2 m = frame.Matrix;
            double turn = Orient(m, rotation, false, false, out bool backward);
            CadFontFace face = FaceOfCode(text.Contents) ?? FaceOf(text.TextStyleName);

            // The text keeps its words, height and attachment as one text has them, for what reads it; it is drawn by its parts,
            // the words laid out as AutoCAD lays them.
            var whole = new DisplayText2(
                value,
                m.X(at.X, at.Y),
                m.Y(at.X, at.Y),
                text.TextHeight * m.Scale,
                turn,
                WidthOf(text.Contents, WidthFactorOf(text.TextStyleName)),
                horizontal,
                vertical,
                style,
                handle,
                layerName,
                face.Name,
                ObliqueOf(text.TextStyleName),
                backward
            );
            CadMTextLayout layout = LayoutOf(text);
            whole.SetMeasure(layout.ActualWidth * m.Scale, 0.0);
            whole.SetParts(MTextParts(text, layout, frame, style, handle, layerName));
            list.Add(whole);
        }

        /// <summary>
        /// The font an MTEXT's codes switch to before the first of its words, as AutoCAD writes them: <c>\fArial|b0|i0|c0|p34;</c>
        /// a typeface, <c>\Fromans|c0;</c> or <c>\Fromans.shx;</c> an SHX file; the last of them where there are several; null
        /// where they switch to none. A switch later in the words is not read, as the words are drawn in one font.
        /// </summary>
        private static string FontCodeOf(string contents, out bool shx)
        {
            string name = null;
            shx = false;
            int i = 0;

            while (i < contents.Length)
            {
                if (contents[i] == '{')
                {
                    i++;
                    continue;
                }

                if (contents[i] != '\\' || i + 1 >= contents.Length)
                {
                    break;
                }

                char code = contents[i + 1];

                if ("LlOoKk".IndexOf(code) >= 0)
                {
                    i += 2;
                    continue;
                }

                int end = contents.IndexOf(';', i + 2);

                if ("ACcFfHQTWp".IndexOf(code) < 0 || end < 0)
                {
                    break;
                }

                if (code == 'f' || code == 'F')
                {
                    string font = contents.Substring(i + 2, end - i - 2).Split('|')[0].Trim();
                    name = font.Length > 0 ? font : name;
                    shx = font.Length > 0 ? code == 'F' : shx;
                }

                i = end + 1;
            }

            return name;
        }

        /// <summary>
        /// The width factor of an MTEXT's letters: that its codes set before the first of its words, <c>\W0.8000;</c> for 0.8 as
        /// AutoCAD writes it, a factor ending in x taken of the one before; the width of its text style where they set none, as
        /// its style's slant is taken. A width set later in the words is not read, as the words are drawn at one width.
        /// </summary>
        private static double WidthOf(string contents, double styleFactor)
        {
            double factor = styleFactor;
            int i = 0;

            while (i < contents.Length)
            {
                if (contents[i] == '{')
                {
                    i++;
                    continue;
                }

                if (contents[i] != '\\' || i + 1 >= contents.Length)
                {
                    break;
                }

                char code = contents[i + 1];

                if ("LlOoKk".IndexOf(code) >= 0)
                {
                    i += 2;
                    continue;
                }

                int end = contents.IndexOf(';', i + 2);

                if ("ACcFfHQTWp".IndexOf(code) < 0 || end < 0)
                {
                    break;
                }

                string number = contents.Substring(i + 2, end - i - 2);
                bool times = number.EndsWith("x", StringComparison.OrdinalIgnoreCase);

                if (
                    code == 'W'
                    && double.TryParse(
                        times ? number.Substring(0, number.Length - 1) : number,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double read
                    )
                    && read > 0.0
                )
                {
                    factor = times ? factor * read : read;
                }

                i = end + 1;
            }

            return factor;
        }

        /// <summary>
        /// The angle a text's baseline is drawn at under a frame, and whether it is drawn backwards: written backwards (its flag
        /// 2), upside down (its flag 4), or in a block inserted mirrored, as AutoCAD draws text in a mirrored block, MIRRTEXT
        /// applying to MIRROR alone. Upside down is backwards turned half round. A text none of these turn over is drawn at its
        /// own angle and the frame's, as before.
        /// </summary>
        private static double Orient(Affine2 m, double rotation, bool backward, bool upsideDown, out bool drawnBackward)
        {
            if (!backward && !upsideDown && m.Determinant >= 0.0)
            {
                drawnBackward = false;
                return rotation + m.Rotation;
            }

            // The way the letters run and the way up, each turned over as the text says, carried by the frame.
            double cos = Math.Cos(rotation);
            double sin = Math.Sin(rotation);
            double ax = backward ? -cos : cos;
            double ay = backward ? -sin : sin;
            double bx = upsideDown ? sin : -sin;
            double by = upsideDown ? -cos : cos;
            double runX = m.A * ax + m.C * ay;
            double runY = m.B * ax + m.D * ay;
            double upX = m.A * bx + m.C * by;
            double upY = m.B * bx + m.D * by;
            double turn = Math.Atan2(runY, runX);
            drawnBackward = runX * upY - runY * upX < 0.0;

            if (!drawnBackward)
            {
                return turn;
            }

            // Drawn backwards, the letters run against the angle: the angle half a turn round, kept within a turn of nought.
            turn += Math.PI;
            return turn > Math.PI ? turn - 2.0 * Math.PI : turn;
        }

        private void DrawHatch(
            DxfHatch hatch,
            Frame frame,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layerName
        )
        {
            var rings = new List<double[]>();
            int loops = hatch.NumberOfLoops;

            for (int i = 0; i < loops; i++)
            {
                DxfHatchLoop loop = hatch.GetLoopAt(i);
                var chain = new List<double>();

                if (loop.IsPolyline)
                {
                    FlattenInto(loop.Polyline, frame.Matrix, chain);
                }
                else
                {
                    foreach (object curve in loop.Curves)
                    {
                        FlattenInto(curve, frame.Matrix, chain);
                    }
                }

                if (chain.Count >= 6)
                {
                    rings.Add(chain.ToArray());
                }
            }

            if (rings.Count == 0)
            {
                return;
            }

            if (hatch.IsGradient)
            {
                // A gradient is filled with the colour halfway along it.
                int mid = style.Rgb;

                try
                {
                    DxfColor color = hatch.EvaluateGradientColorAt(0.5f);
                    mid = color == null ? mid : color.ColorValue & 0xFFFFFF;
                }
                catch (Exception)
                {
                    mid = style.Rgb;
                }

                list.Add(
                    new DisplayFill2(rings, false, new DisplayStyle(mid, style.LineWeight, null), handle, layerName)
                );
                return;
            }

            if (hatch.IsSolidFill)
            {
                list.Add(new DisplayFill2(rings, false, style, handle, layerName));
                return;
            }

            double[] lines = PatternLines(hatch, frame.Matrix);

            if (lines != null)
            {
                // The pattern's own dashes are in its lines; the hatch's linetype is not laid over them, as AutoCAD lays none.
                list.Add(
                    new DisplaySegments2(lines, new DisplayStyle(style.Rgb, style.LineWeight, null), handle, layerName)
                    {
                        Rings = rings,
                    }
                );
                return;
            }

            // A pattern whose lines the hatch does not hold, or too many of them, is drawn by its boundary.
            foreach (double[] ring in rings)
            {
                list.Add(new DisplayPolyline2(ring, true, style, handle, layerName));
            }
        }

        /// <summary>
        /// The lines of a hatch's pattern cut by its loops, placed in the drawing; null where the hatch holds no lines of its
        /// pattern or would draw more than <see cref="MaxHatchLines"/>. A hatch of a block is read once per
        /// <see cref="Build"/> however many INSERTs show it, and placed by each: a wall block of 3 hatches inserted 1 500 times
        /// reads 3.
        /// </summary>
        private double[] PatternLines(DxfHatch hatch, Affine2 m)
        {
            if (!_patterns.TryGetValue(hatch, out HatchPattern pattern))
            {
                pattern = ReadPattern(hatch);
                _patterns[hatch] = pattern;
            }

            if (pattern == null)
            {
                return null;
            }

            IReadOnlyList<GeoLine2> lines = pattern.Lines;
            var xy = new double[lines.Count * 4];

            for (int i = 0; i < lines.Count; i++)
            {
                GeoLine2 line = lines[i];
                Place(
                    pattern.Ecs,
                    m,
                    line.StartPoint.X,
                    line.StartPoint.Y,
                    pattern.Elevation,
                    out xy[4 * i],
                    out xy[4 * i + 1]
                );
                Place(
                    pattern.Ecs,
                    m,
                    line.EndPoint.X,
                    line.EndPoint.Y,
                    pattern.Elevation,
                    out xy[4 * i + 2],
                    out xy[4 * i + 3]
                );
            }

            return xy;
        }

        /// <summary>
        /// The lines of a hatch's pattern cut by its loops, as <see cref="DxfHatch.GetHatchLinesData"/> gives them in the
        /// hatch's own system, with that system; null where the hatch holds none or would draw more than
        /// <see cref="MaxHatchLines"/>, which a rough count from the spacing and the dashes refuses before the lines are
        /// worked out.
        /// </summary>
        private static HatchPattern ReadPattern(DxfHatch hatch)
        {
            try
            {
                int definitions = hatch.NumberOfPatternDefinitions;

                if (definitions == 0)
                {
                    return null;
                }

                GeoAabb3 extents = hatch.GeometricExtents;
                double dx = extents.Max.X - extents.Min.X;
                double dy = extents.Max.Y - extents.Min.Y;
                double diagonal = Math.Sqrt(dx * dx + dy * dy);
                double estimate = 0.0;

                for (int i = 0; i < definitions; i++)
                {
                    DxfPatternDefinition definition = hatch.GetPatternDefinitionAt(i);
                    double spacing = Math.Abs(
                        Math.Cos(definition.Angle) * definition.OffsetY
                            - Math.Sin(definition.Angle) * definition.OffsetX
                    );

                    if (spacing <= diagonal * 1E-9)
                    {
                        return null;
                    }

                    double perLine = 1.0;
                    IReadOnlyList<double> dashes = definition.GetDashes();

                    if (dashes.Count > 0)
                    {
                        double period = 0.0;

                        foreach (double dash in dashes)
                        {
                            period += Math.Abs(dash);
                        }

                        perLine =
                            period <= diagonal * 1E-9
                                ? double.PositiveInfinity
                                : (diagonal / period + 1.0) * dashes.Count;
                    }

                    estimate += (diagonal / spacing + 1.0) * perLine;
                }

                if (estimate > 4.0 * MaxHatchLines)
                {
                    return null;
                }

                IReadOnlyList<GeoLine2> lines = hatch.GetHatchLinesData();

                if (lines.Count == 0 || lines.Count > MaxHatchLines)
                {
                    return null;
                }

                return new HatchPattern(lines, hatch.Ecs, hatch.Elevation);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The lines of a hatch's pattern in its own system, the system and its elevation, as read once.</summary>
        private sealed class HatchPattern
        {
            public HatchPattern(IReadOnlyList<GeoLine2> lines, GeoTransform3 ecs, double elevation)
            {
                Lines = lines;
                Ecs = ecs;
                Elevation = elevation;
            }

            public IReadOnlyList<GeoLine2> Lines { get; }

            public GeoTransform3 Ecs { get; }

            public double Elevation { get; }
        }

        /// <summary>A point of an entity's own system taken to the world and then into the drawing.</summary>
        private static void Place(
            GeoTransform3 ecs,
            Affine2 m,
            double x,
            double y,
            double z,
            out double px,
            out double py
        )
        {
            double wx = ecs[0, 0] * x + ecs[0, 1] * y + ecs[0, 2] * z + ecs[0, 3];
            double wy = ecs[1, 0] * x + ecs[1, 1] * y + ecs[1, 2] * z + ecs[1, 3];
            px = m.X(wx, wy);
            py = m.Y(wx, wy);
        }

        private static string CleanText(string value, bool trim = true)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (c == '%' && i + 2 < value.Length && value[i + 1] == '%')
                {
                    char code = char.ToLowerInvariant(value[i + 2]);

                    if (code == 'd')
                    {
                        builder.Append('°');
                        i += 2;
                        continue;
                    }

                    if (code == 'p')
                    {
                        builder.Append('±');
                        i += 2;
                        continue;
                    }

                    if (code == 'c')
                    {
                        builder.Append('Ø');
                        i += 2;
                        continue;
                    }
                }

                if (c == '\r')
                {
                    continue;
                }

                builder.Append(c);
            }

            return trim ? builder.ToString().Trim('\n', ' ') : builder.ToString();
        }

        private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
