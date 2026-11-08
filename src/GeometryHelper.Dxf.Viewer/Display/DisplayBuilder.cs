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

        private readonly DxfDatabase _database;
        private readonly Dictionary<string, LayerState> _layers = new Dictionary<string, LayerState>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, double[]> _dashes = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TextFont> _fonts = new Dictionary<string, TextFont>(StringComparer.OrdinalIgnoreCase);
        private readonly double _ltScale;
        private readonly DisplayStyle _defaultStyle = new DisplayStyle(0xFFFFFF, 0.0, null);
        private DisplayList2 _modelCache;

        public DisplayBuilder(DxfDatabase database)
        {
            _database = database;
            double scale = 1.0;

            try
            {
                scale = database.Ltscale;
            }
            catch (Exception)
            {
                scale = 1.0;
            }

            _ltScale = scale > 0.0 ? scale : 1.0;

            foreach (DxfLayerTableRecord layer in database.LayerTable)
            {
                _layers[layer.Name] = new LayerState(layer);
            }
        }

        /// <summary>Gives the builder the display list of model space its viewports draw, made before it or kept from another builder.</summary>
        internal void UseModel(DisplayList2 model) => _modelCache = model;

        internal bool IsLayerVisible(string name) => !_layers.TryGetValue(name ?? "0", out LayerState state) || state.Visible;

        public DisplayList2 Build(DxfBlockTableRecord space, bool isModel)
        {
            var list = new DisplayList2();
            var root = new Frame(Affine2.Identity);

            foreach (DxfEntity entity in space)
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

            if (isModel)
            {
                _modelCache = list;
            }

            return list;
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
            }

            public bool Visible { get; }

            public int Rgb { get; }

            public double LineWeight { get; }

            public string Linetype { get; }

            public DxfLayerTableRecord Record { get; }
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

            public Frame Child(Affine2 matrix) => new Frame(matrix)
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
            return layer == "0" && frame.BlockLayer != null ? frame.BlockLayer : layer;
        }

        private DisplayStyle StyleOf(DxfEntity entity, Frame frame, string layerName)
        {
            _layers.TryGetValue(layerName, out LayerState layer);
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

            if (string.IsNullOrEmpty(linetype) || string.Equals(linetype, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                linetype = layer != null ? layer.Linetype : "Continuous";
            }
            else if (string.Equals(linetype, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                linetype = frame.BlockLinetype;
            }

            double[] dashes = DashesOf(linetype, entity.LinetypeScale * frame.Matrix.Scale);
            return new DisplayStyle(rgb, weight, dashes);
        }

        private double[] DashesOf(string linetype, double scale)
        {
            if (string.IsNullOrEmpty(linetype) || string.Equals(linetype, "Continuous", StringComparison.OrdinalIgnoreCase))
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

        /// <summary>The font of a text style: the typeface it names, else the file of its font; null where it has neither.</summary>
        private string FontOf(string styleName) => Style(styleName).Name;

        /// <summary>The slant of a text style's letters in radians.</summary>
        private double ObliqueOf(string styleName) => Style(styleName).Oblique;

        private TextFont Style(string styleName)
        {
            string key = styleName ?? string.Empty;

            if (!_fonts.TryGetValue(key, out TextFont font))
            {
                font = new TextFont(null, 0.0);

                try
                {
                    DxfTextStyleTableRecord record = _database.TextStyleTable[string.IsNullOrEmpty(styleName) ? "Standard" : styleName];

                    if (record != null)
                    {
                        string typeFace = record.Font.TypeFace;
                        font = new TextFont(string.IsNullOrEmpty(typeFace) ? record.FileName : typeFace, record.ObliquingAngle);
                    }
                }
                catch (Exception)
                {
                    font = new TextFont(null, 0.0);
                }

                _fonts[key] = font;
            }

            return font;
        }

        private sealed class TextFont
        {
            public TextFont(string name, double oblique)
            {
                Name = string.IsNullOrEmpty(name) ? null : name;
                Oblique = oblique;
            }

            public string Name { get; }

            public double Oblique { get; }
        }

        private void Draw(DxfEntity entity, Frame frame, DisplayList2 list, string forcedHandle)
        {
            if (entity == null || !entity.Visible || frame.Depth > MaxDepth)
            {
                return;
            }

            string layerName = LayerOf(entity, frame);

            if (!IsLayerVisible(layerName))
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
                    DrawInsert(reference, frame, list, handle, layerName, style);
                    return;
                case DxfAttributeDefinition definition when frame.RootHandle != null && !definition.Constant:
                    // An INSERT shows the values of its ATTRIBs, not the definitions of its block, as AutoCAD shows it; a constant
                    // definition, which has no ATTRIB, shows its value.
                    return;
                case DxfDBText text:
                    DrawText(text, frame, list, style, handle, layerName);
                    return;
                case DxfMText mtext:
                    DrawMText(mtext, frame, list, style, handle, layerName);
                    return;
                case DxfHatch hatch:
                    DrawHatch(hatch, frame, list, style, handle, layerName);
                    return;
                case DxfMLeader mleader:
                    foreach (DxfEntity part in mleader.Explode())
                    {
                        Draw(part, frame, list, handle);
                    }

                    return;
                case DxfTable table:
                    {
                        // A table is drawn by the block AutoCAD drew it with, placed at its point along its direction.
                        GeoPoint3 at = table.Position;
                        GeoVector3 direction = table.Direction;
                        Affine2 placement = Affine2.ScaleRotate(1.0, 1.0, Math.Atan2(direction.Y, direction.X)).Then(Affine2.Translation(at.X, at.Y));
                        DrawBlock(table.BlockTableRecord, placement, table, frame, list, handle, layerName, style);
                        return;
                    }

                case DxfLeader leader when TryExplode(leader, out IReadOnlyList<DxfEntity> parts):
                    // A leader is drawn as AutoCAD breaks it: its arrowhead, then its line or spline.
                    foreach (DxfEntity part in parts)
                    {
                        Draw(part, frame, list, handle);
                    }

                    return;
            }

            IReadOnlyList<object> shapes = entity.GetGeometry();
            bool mask = entity is DxfWipeout;
            bool fill = entity is DxfSolid;

            foreach (object shape in shapes)
            {
                AddShape(shape, frame.Matrix, list, style, handle, layerName, fill, mask);
            }
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

        private void DrawInsert(DxfBlockReference reference, Frame frame, DisplayList2 list, string handle, string layerName, DisplayStyle style)
        {
            DxfBlockTableRecord block = reference.BlockTableRecord;

            if (block == null)
            {
                return;
            }

            GeoTransform3 transform = reference.BlockTransform;
            Affine2 own = new Affine2(transform[0, 0], transform[1, 0], transform[0, 1], transform[1, 1], transform[0, 3], transform[1, 3]);
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
        /// The ATTRIBs of an INSERT, its values, as a title block shows them: each a text where the file places it, in the
        /// space of the INSERT (its own place, turn and scale are in the ATTRIB already), under the frame the INSERT is in,
        /// picked as the INSERT, ByBlock taking the INSERT's look; an invisible one left out. A MINSERT shows them once.
        /// </summary>
        private void DrawAttributes(DxfBlockReference reference, Frame frame, DisplayList2 list, string handle, string layerName, DisplayStyle style)
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

        private void DrawBlock(DxfBlockTableRecord block, Affine2 placement, DxfEntity owner, Frame frame, DisplayList2 list, string handle, string layerName, DisplayStyle style)
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
        }

        private void DrawViewport(DxfViewport viewport, Frame frame, DisplayList2 list, DisplayStyle style, string handle, string layerName)
        {
            if (viewport.Number <= 1 || !viewport.On || _modelCache == null || viewport.Height <= 0.0 || viewport.ViewHeight <= 0.0)
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
            GeoPoint2 view = viewport.ViewCenter;
            double twist = viewport.TwistAngle;
            list.Add(new DisplayViewport2(center.X, center.Y, viewport.Width, viewport.Height, view.X, view.Y, scale, twist, _modelCache, frozen, style, handle, layerName));
        }

        private void DrawText(DxfDBText text, Frame frame, DisplayList2 list, DisplayStyle style, string handle, string layerName)
        {
            string value = CleanText(text.TextString);

            if (value.Length == 0 || text.Height <= 0.0)
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
            list.Add(new DisplayText2(value, m.X(at.X, at.Y), m.Y(at.X, at.Y), text.Height * m.Scale, turn, text.WidthFactor, horizontal, vertical, style, handle, layerName, FontOf(text.TextStyleName), text.Oblique, backward));
        }

        private void DrawMText(DxfMText text, Frame frame, DisplayList2 list, DisplayStyle style, string handle, string layerName)
        {
            string value = CleanText(text.Text);

            if (value.Length == 0 || text.TextHeight <= 0.0)
            {
                return;
            }

            GeoPoint3 at = text.Location;
            int attach = (int)text.Attachment;
            int horizontal = attach == 2 || attach == 5 || attach == 8 ? 1 : attach == 3 || attach == 6 || attach == 9 ? 2 : 0;
            int vertical = attach <= 3 ? 3 : attach <= 6 ? 2 : 1;
            GeoVector3 direction = text.Direction;
            double rotation = Math.Atan2(direction.Y, direction.X);
            Affine2 m = frame.Matrix;
            double turn = Orient(m, rotation, false, false, out bool backward);
            list.Add(new DisplayText2(value, m.X(at.X, at.Y), m.Y(at.X, at.Y), text.TextHeight * m.Scale, turn, 1.0, horizontal, vertical, style, handle, layerName, FontOf(text.TextStyleName), ObliqueOf(text.TextStyleName), backward));
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

        private void DrawHatch(DxfHatch hatch, Frame frame, DisplayList2 list, DisplayStyle style, string handle, string layerName)
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

                list.Add(new DisplayFill2(rings, false, new DisplayStyle(mid, style.LineWeight, null), handle, layerName));
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
                list.Add(new DisplaySegments2(lines, new DisplayStyle(style.Rgb, style.LineWeight, null), handle, layerName));
                return;
            }

            // A pattern whose lines the hatch does not hold, or too many of them, is drawn by its boundary.
            foreach (double[] ring in rings)
            {
                list.Add(new DisplayPolyline2(ring, true, style, handle, layerName));
            }
        }

        /// <summary>
        /// The lines of a hatch's pattern cut by its loops, as <see cref="DxfHatch.GetHatchLinesData"/> gives them, placed in
        /// the drawing; null where the hatch holds no lines of its pattern or would draw more than <see cref="MaxHatchLines"/>,
        /// which a rough count from the spacing and the dashes refuses before the lines are worked out.
        /// </summary>
        private static double[] PatternLines(DxfHatch hatch, Affine2 m)
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
                    double spacing = Math.Abs(Math.Cos(definition.Angle) * definition.OffsetY - Math.Sin(definition.Angle) * definition.OffsetX);

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

                        perLine = period <= diagonal * 1E-9 ? double.PositiveInfinity : (diagonal / period + 1.0) * dashes.Count;
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

                GeoTransform3 ecs = hatch.Ecs;
                double elevation = hatch.Elevation;
                var xy = new double[lines.Count * 4];

                for (int i = 0; i < lines.Count; i++)
                {
                    GeoLine2 line = lines[i];
                    Place(ecs, m, line.StartPoint.X, line.StartPoint.Y, elevation, out xy[4 * i], out xy[4 * i + 1]);
                    Place(ecs, m, line.EndPoint.X, line.EndPoint.Y, elevation, out xy[4 * i + 2], out xy[4 * i + 3]);
                }

                return xy;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A point of an entity's own system taken to the world and then into the drawing.</summary>
        private static void Place(GeoTransform3 ecs, Affine2 m, double x, double y, double z, out double px, out double py)
        {
            double wx = ecs[0, 0] * x + ecs[0, 1] * y + ecs[0, 2] * z + ecs[0, 3];
            double wy = ecs[1, 0] * x + ecs[1, 1] * y + ecs[1, 2] * z + ecs[1, 3];
            px = m.X(wx, wy);
            py = m.Y(wx, wy);
        }

        private static string CleanText(string value)
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

            return builder.ToString().Trim('\n', ' ');
        }

        private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
