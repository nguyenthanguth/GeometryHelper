using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What a plot draws an item as, after its plot style: the colour, and where the style sets them the lineweight, the
    /// linetype and the shape of the line's ends and corners. Made by <see cref="CadPlotStyleResolver"/>; never changed.
    /// </summary>
    public sealed class CadPlotPen
    {
        internal CadPlotPen(
            int rgb,
            bool hasLineWeight,
            double lineWeight,
            bool hasLinetype,
            double[] dashes,
            CadPlotLineEnd lineEnd,
            CadPlotLineJoin lineJoin
        )
        {
            Rgb = rgb;
            HasLineWeight = hasLineWeight;
            LineWeight = lineWeight;
            HasLinetype = hasLinetype;
            Dashes = dashes;
            LineEnd = lineEnd;
            LineJoin = lineJoin;
        }

        /// <summary>Gets the colour plotted, 0xRRGGBB, after the style's colour, grayscale and screening.</summary>
        public int Rgb { get; }

        /// <summary>Gets whether the style sets the lineweight; false, the object's own is plotted.</summary>
        public bool HasLineWeight { get; }

        /// <summary>Gets the lineweight the style sets, in millimetres, where <see cref="HasLineWeight"/>.</summary>
        public double LineWeight { get; }

        /// <summary>Gets whether the style sets the linetype; false, the object's own is plotted.</summary>
        public bool HasLinetype { get; }

        /// <summary>Gets the dash pattern the style sets, in millimetres of the sheet, as on and off lengths in turn (an on length of nought is a dot); null for a solid line, where <see cref="HasLinetype"/>.</summary>
        public double[] Dashes { get; }

        /// <summary>Gets the shape of a line's end.</summary>
        public CadPlotLineEnd LineEnd { get; }

        /// <summary>Gets the shape of a corner.</summary>
        public CadPlotLineJoin LineJoin { get; }
    }

    /// <summary>
    /// Looks up the plot style of the items of a plot, as AutoCAD plots with a plot style table: with a colour-dependent
    /// table (.ctb) the style of the item's colour of AutoCAD's index; with a named one (.stb) the style named by the item's
    /// entity or, ByLayer, by its layer, Normal where none is named. It is made once for a plot
    /// (<see cref="Create(CadDocument, CadPlotSettings)"/>) and read for each item (<see cref="PenFor(DisplayItem2)"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A colour that is not one of AutoCAD's 255 (a true colour) takes the style of the index nearest it, and plots in its
    /// own colour where the style's colour is the object's. An object's white (colour 7 on a dark screen, or within 2 per
    /// cent of it) plots black where the style's colour is the object's, as on the white sheet; a colour the style sets
    /// plots as it is set, white too.
    /// </para>
    /// <para>
    /// A named style is found for an item by its entity's group 380 and 390 (0 or none: ByLayer, 1: ByBlock, taken as
    /// Normal, 2: Normal, 3: the entry of the drawing's ACAD_PLOTSTYLENAME the handle names) and then the layer's group 390;
    /// the items of a block reference share its entity, so a reference given a style gives it to all it draws, and one
    /// that is ByLayer lets each item take its own layer's. A drawing with no entry of that name, or no dictionary, plots
    /// Normal. Where no table is named, or its file is not found and is none of the built-in ones, the plot is as before
    /// tables were files: <see cref="CadPlotStyles.Map"/> of the legacy table the name stands for.
    /// </para>
    /// <para>A resolver keeps what it has looked up and is not safe to use from two threads: each plot makes its own.</para>
    /// </remarks>
    public sealed class CadPlotStyleResolver
    {
        private readonly CadDocument _document;
        private readonly CadPlotStyleSheet _sheet;
        private readonly CadPlotStyleTable _legacy;
        private readonly Dictionary<CadPlotStyle, Dictionary<int, CadPlotPen>> _pens =
            new Dictionary<CadPlotStyle, Dictionary<int, CadPlotPen>>();
        private readonly Dictionary<int, CadPlotPen> _legacyPens = new Dictionary<int, CadPlotPen>();
        private readonly Dictionary<int, int> _indexes = new Dictionary<int, int>();
        private readonly Dictionary<string, string> _handleNames = new Dictionary<string, string>(
            StringComparer.Ordinal
        );
        private Dictionary<string, string> _namesOfHandles;

        private CadPlotStyleResolver(CadDocument document, CadPlotStyleSheet sheet, CadPlotStyleTable legacy)
        {
            _document = document;
            _sheet = sheet;
            _legacy = legacy;
        }

        /// <summary>
        /// Makes the resolver of a plot: the table the settings name (<see cref="CadPlotSettings.EffectiveStyleSheet"/>), read
        /// from the document's <see cref="CadDocument.PlotStyleFolder"/>.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="settings">How it is plotted.</param>
        /// <returns>The resolver.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the settings are null.</exception>
        public static CadPlotStyleResolver Create(CadDocument document, CadPlotSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return Create(document, settings.EffectiveStyleSheet);
        }

        /// <summary>Makes the resolver of a table of a document's folder, by its file name.</summary>
        /// <param name="document">The document.</param>
        /// <param name="styleSheet">The table's file name, as <c>monochrome.ctb</c>; empty for none.</param>
        /// <returns>The resolver; with no table it gives the colours as displayed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadPlotStyleResolver Create(CadDocument document, string styleSheet)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string name = (styleSheet ?? string.Empty).Trim();
            CadPlotStyleSheet sheet = name.Length == 0 ? null : CadPlotStyleFolder.Load(document.PlotStyleFolder, name);
            CadPlotStyleTable legacy =
                name.IndexOf("monochrome", StringComparison.OrdinalIgnoreCase) >= 0 ? CadPlotStyleTable.Monochrome
                : name.IndexOf("grayscale", StringComparison.OrdinalIgnoreCase) >= 0 ? CadPlotStyleTable.Grayscale
                : CadPlotStyleTable.None;
            return new CadPlotStyleResolver(document, sheet, legacy);
        }

        /// <summary>Gets the table the plot reads; null where there is none, or it was not found and the colours go through the three legacy tables' rules.</summary>
        public CadPlotStyleSheet Sheet => _sheet;

        /// <summary>Gets whether the table is a named one: styles are found by the name an item's entity or layer gives.</summary>
        public bool IsNamed => _sheet != null && _sheet.IsNamed;

        /// <summary>Gets the pen of an item: its colour, and what its style sets.</summary>
        /// <param name="item">The item.</param>
        /// <returns>The pen.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the item is null.</exception>
        public CadPlotPen PenFor(DisplayItem2 item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return PenFor(item.Style.Rgb, item.Layer, item.Handle);
        }

        /// <summary>Gets the pen of an object of a colour, a layer and an entity.</summary>
        /// <param name="rgb">The colour the object is drawn in, 0xRRGGBB.</param>
        /// <param name="layer">The layer it is drawn on; null for none.</param>
        /// <param name="handle">The handle of its entity, or of the outermost block reference it is in; null for none.</param>
        /// <returns>The pen.</returns>
        public CadPlotPen PenFor(int rgb, string layer, string handle)
        {
            rgb &= 0xFFFFFF;

            if (_sheet == null)
            {
                if (!_legacyPens.TryGetValue(rgb, out CadPlotPen legacy))
                {
                    legacy = new CadPlotPen(
                        CadPlotStyles.Map(_legacy, rgb),
                        false,
                        0.0,
                        false,
                        null,
                        CadPlotLineEnd.Object,
                        CadPlotLineJoin.Object
                    );
                    _legacyPens[rgb] = legacy;
                }

                return legacy;
            }

            CadPlotStyle style = _sheet.IsNamed ? _sheet.StyleNamed(StyleNameOf(layer, handle)) : null;

            if (style == null)
            {
                style = _sheet.IsNamed ? _sheet.Styles[0] : _sheet.StyleForColorIndex(IndexOf(rgb));
            }

            if (!_pens.TryGetValue(style, out Dictionary<int, CadPlotPen> byColor))
            {
                byColor = new Dictionary<int, CadPlotPen>();
                _pens[style] = byColor;
            }

            if (!byColor.TryGetValue(rgb, out CadPlotPen pen))
            {
                pen = Make(style, rgb);
                byColor[rgb] = pen;
            }

            return pen;
        }

        /// <summary>
        /// Gets the colour an object of a colour plots in where it has no plot style of its own to name: through the style of
        /// the colour in a colour-dependent table, Normal's in a named one.
        /// </summary>
        /// <param name="rgb">The colour, 0xRRGGBB.</param>
        /// <returns>The colour plotted, 0xRRGGBB.</returns>
        public int PlottedColor(int rgb) => PenFor(rgb, null, null).Rgb;

        /// <summary>
        /// Gets the name of the plot style an entity on a layer has in a drawing of named plot styles: its own where it names
        /// one, else its layer's, else Normal.
        /// </summary>
        /// <param name="layer">The layer; null for none.</param>
        /// <param name="handle">The entity's handle; null for none.</param>
        /// <returns>The style's name as the drawing spells it.</returns>
        public string StyleNameOf(string layer, string handle)
        {
            if (!string.IsNullOrEmpty(handle))
            {
                if (!_handleNames.TryGetValue(handle, out string name))
                {
                    name = OfEntity(handle);
                    _handleNames[handle] = name;
                }

                if (name != null)
                {
                    return name;
                }
            }

            return OfLayer(layer) ?? CadPlotStyleSheet.NormalName;
        }

        private CadPlotPen Make(CadPlotStyle style, int rgb)
        {
            int color;

            if (style.UsesObjectColor)
            {
                color = CadPlotStyles.Map(CadPlotStyleTable.None, rgb);
            }
            else
            {
                color = style.Rgb;
            }

            if (style.Grayscale)
            {
                int r = (color >> 16) & 0xFF;
                int g = (color >> 8) & 0xFF;
                int b = color & 0xFF;
                int grey = (int)Math.Round(0.299 * r + 0.587 * g + 0.114 * b, MidpointRounding.AwayFromZero);
                color = (grey << 16) | (grey << 8) | grey;
            }

            int screening = style.EffectiveScreening;

            if (screening < 100)
            {
                color = Screened(color, screening);
            }

            double mm = _sheet.LineWeightOf(style);
            bool hasLinetype = style.Linetype != CadPlotStyle.ObjectLinetype;
            return new CadPlotPen(
                color,
                mm > 0.0,
                mm,
                hasLinetype,
                hasLinetype ? CadPlotPatterns.Dashes(style.Linetype, style.LinePatternSize) : null,
                style.LineEnd,
                style.LineJoin
            );
        }

        /// <summary>A colour screened to a per cent: each channel part way to white, 255 - (255 - channel) × per cent / 100.</summary>
        private static int Screened(int rgb, int percent)
        {
            int result = 0;

            for (int shift = 16; shift >= 0; shift -= 8)
            {
                double channel = (rgb >> shift) & 0xFF;
                int value = (int)Math.Round(255.0 - (255.0 - channel) * percent / 100.0, MidpointRounding.AwayFromZero);
                result |= Math.Max(0, Math.Min(255, value)) << shift;
            }

            return result;
        }

        /// <summary>The colour of AutoCAD's index nearest a colour, 1 to 255; the first of the nearest, so white is 7.</summary>
        private int IndexOf(int rgb)
        {
            if (!_indexes.TryGetValue(rgb, out int index))
            {
                index = DxfColor.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb).ColorIndex;
                index = Math.Max(1, Math.Min(CadPlotStyleSheet.ColorCount, index));
                _indexes[rgb] = index;
            }

            return index;
        }

        /// <summary>The plot style name an entity gives itself; null where it is ByLayer (no group 380, or 0) or the handle is of none.</summary>
        private string OfEntity(string handle)
        {
            DxfEntity entity = _document.FindEntity(handle);

            if (entity == null)
            {
                return null;
            }

            DxfFile file = entity.Item.File;
            string type = file.CommonValueOf(entity.Item, 380)?.Trim();
            string pointer = file.CommonValueOf(entity.Item, 390)?.Trim();

            if (type == "1" || type == "2")
            {
                return CadPlotStyleSheet.NormalName;
            }

            if (type == "3" || (type == null && pointer != null))
            {
                return NameOfHandle(pointer);
            }

            return null;
        }

        /// <summary>The plot style name of a layer: the entry its group 390 names; null where it names none.</summary>
        private string OfLayer(string layer)
        {
            DxfDatabase database = _document.Database;

            if (string.IsNullOrEmpty(layer) || !database.LayerTable.Has(layer))
            {
                return null;
            }

            DxfLayerTableRecord record = database.LayerTable[layer];
            string pointer = record.Item.File.ValueOf(record.Item, 390)?.Trim();
            return NameOfHandle(pointer);
        }

        /// <summary>The name of the entry of ACAD_PLOTSTYLENAME with a handle, as the drawing spells it; null where it has none.</summary>
        private string NameOfHandle(string handle)
        {
            if (string.IsNullOrEmpty(handle))
            {
                return null;
            }

            if (_namesOfHandles == null)
            {
                _namesOfHandles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                DxfDBDictionary names = _document.Database.PlotStyleNameDictionary;

                foreach (
                    DxfDBDictionaryEntry entry in (IEnumerable<DxfDBDictionaryEntry>)names
                        ?? Array.Empty<DxfDBDictionaryEntry>()
                )
                {
                    if (entry.Value?.Handle != null && !_namesOfHandles.ContainsKey(entry.Value.Handle))
                    {
                        _namesOfHandles[entry.Value.Handle] = entry.Key;
                    }
                }
            }

            return _namesOfHandles.TryGetValue(handle, out string name) ? name : null;
        }
    }

    /// <summary>The dash patterns of the linetypes a plot style can plot in.</summary>
    internal static class CadPlotPatterns
    {
        /// <summary>The patterns of the linetypes 1 to 30, each in units of a quarter of the style's pattern size, as on and off lengths in turn; Solid (0) has none.</summary>
        private static readonly double[][] Patterns =
        {
            null,
            new[] { 2.0, 1.0 },
            new[] { 0.0, 1.0 },
            new[] { 2.0, 1.0, 0.0, 1.0 },
            new[] { 1.0, 1.0 },
            new[] { 2.0, 1.0 },
            new[] { 4.0, 1.0 },
            new[] { 1.0, 0.5, 1.0, 2.0 },
            new[] { 2.0, 0.5, 2.0, 2.0 },
            new[] { 4.0, 0.5, 4.0, 2.0 },
            new[] { 3.0, 1.0, 1.0, 1.0 },
            new[] { 2.0, 1.0, 1.0, 1.0, 1.0, 1.0 },
            new[] { 4.0, 1.0, 1.0, 1.0 },
            new[] { 4.0, 1.0, 0.0, 1.0, 0.0, 1.0 },
            new[] { 4.0, 1.0, 0.0, 1.0 },
            new[] { 2.0, 1.0, 0.0, 1.0, 1.0, 1.0, 0.0, 1.0 },
            new[] { 0.0, 4.0 },
            new[] { 4.0, 1.0 },
            new[] { 4.0, 4.0 },
            new[] { 8.0, 1.0, 0.0, 1.0 },
            new[] { 8.0, 1.0, 0.0, 1.0, 0.0, 1.0 },
            new[] { 8.0, 1.0, 0.0, 1.0, 0.0, 1.0, 0.0, 1.0 },
            new[] { 0.0, 1.0 },
            new[] { 8.0, 1.0, 2.0, 1.0 },
            new[] { 8.0, 1.0, 2.0, 1.0, 2.0, 1.0 },
            new[] { 4.0, 1.0, 0.0, 1.0 },
            new[] { 4.0, 1.0, 4.0, 1.0, 0.0, 1.0 },
            new[] { 4.0, 1.0, 4.0, 1.0, 0.0, 1.0, 0.0, 1.0 },
            new[] { 4.0, 1.0, 0.0, 1.0, 0.0, 1.0 },
            new[] { 4.0, 1.0, 4.0, 1.0, 0.0, 1.0, 0.0, 1.0, 0.0, 1.0 },
            new[] { 4.0, 1.0, 0.0, 1.0, 0.0, 1.0, 0.0, 1.0 },
        };

        /// <summary>The dashes of a linetype in millimetres: its pattern, a unit of which is a quarter of the pattern size in inches; null for Solid or a value outside 0 to 30.</summary>
        public static double[] Dashes(int linetype, double patternSizeInches)
        {
            if (linetype < 1 || linetype >= Patterns.Length)
            {
                return null;
            }

            double unit = (patternSizeInches > 0.0 ? patternSizeInches : 0.5) * 25.4 / 4.0;
            double[] pattern = Patterns[linetype];
            var dashes = new double[pattern.Length];

            for (int i = 0; i < pattern.Length; i++)
            {
                dashes[i] = pattern[i] * unit;
            }

            return dashes;
        }
    }
}
