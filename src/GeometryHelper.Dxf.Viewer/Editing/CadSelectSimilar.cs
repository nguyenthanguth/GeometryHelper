using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What SELECTSIMILAR matches, as AutoCAD 2021 does through SELECTSIMILARMODE: objects of the same kind as one picked and,
    /// by the bits set, of the same colour, layer, linetype, linetype scale, lineweight, plot style, object style (the text,
    /// dimension, multileader or table style) or name (the block of an insert, the pattern of a hatch). The default, 130, is
    /// the layer and the name; 0 matches the kind alone. Colours are compared as the objects hold them, so an object BYLAYER
    /// is not the same as one of the layer's own colour, as AutoCAD compares them.
    /// </summary>
    internal static class CadSelectSimilar
    {
        /// <summary>The colour of the object.</summary>
        internal const int Color = 1;

        /// <summary>The layer of the object.</summary>
        internal const int Layer = 2;

        /// <summary>The linetype of the object.</summary>
        internal const int Linetype = 4;

        /// <summary>The linetype scale of the object.</summary>
        internal const int LinetypeScale = 8;

        /// <summary>The lineweight of the object.</summary>
        internal const int Lineweight = 16;

        /// <summary>The plot style of the object; the presenter keeps no named plot style on an object, so all are alike.</summary>
        internal const int PlotStyle = 32;

        /// <summary>The style of a text, dimension, multileader or table.</summary>
        internal const int ObjectStyle = 64;

        /// <summary>The name of a block of an insert, or the pattern of a hatch.</summary>
        internal const int Name = 128;

        /// <summary>The value of SELECTSIMILARMODE a new drawing starts with: Layer and Name.</summary>
        internal const int Default = Layer | Name;

        /// <summary>The most SELECTSIMILARMODE can be: every bit set.</summary>
        internal const int All = Color | Layer | Linetype | LinetypeScale | Lineweight | PlotStyle | ObjectStyle | Name;

        /// <summary>The settings, in the order the dialog lists them, each with its bit.</summary>
        internal static readonly KeyValuePair<string, int>[] Settings =
        {
            new KeyValuePair<string, int>("Color", Color),
            new KeyValuePair<string, int>("Layer", Layer),
            new KeyValuePair<string, int>("Linetype", Linetype),
            new KeyValuePair<string, int>("Linetype scale", LinetypeScale),
            new KeyValuePair<string, int>("Lineweight", Lineweight),
            new KeyValuePair<string, int>("Plot style", PlotStyle),
            new KeyValuePair<string, int>("Object style", ObjectStyle),
            new KeyValuePair<string, int>("Name", Name),
        };

        /// <summary>
        /// Finds the objects of a space like any of the seeds, by the bits of a mode: those of the same kind that match what the
        /// bits name. Objects on a layer that is off or frozen, hidden, or the paper's own viewport are left out, as a pick
        /// leaves them out. The result is in the order of the space.
        /// </summary>
        /// <param name="document">The drawing.</param>
        /// <param name="space">The space searched.</param>
        /// <param name="seeds">The handles of the objects picked.</param>
        /// <param name="mode">The bits of SELECTSIMILARMODE.</param>
        /// <returns>The handles of the objects found, the seeds that can be picked among them.</returns>
        internal static List<string> Find(CadDocument document, CadSpace space, IEnumerable<string> seeds, int mode)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);

            foreach (string handle in seeds)
            {
                DxfEntity seed = document.FindEntity(handle);

                if (seed != null)
                {
                    keys.Add(KeyOf(seed, mode));
                }
            }

            var found = new List<string>();

            if (keys.Count == 0)
            {
                return found;
            }

            foreach (DxfEntity entity in space.Block)
            {
                if (
                    !string.IsNullOrEmpty(entity.Handle)
                    && CanBePicked(document.Database, entity)
                    && keys.Contains(KeyOf(entity, mode))
                )
                {
                    found.Add(entity.Handle);
                }
            }

            return found;
        }

        /// <summary>Whether an object can be picked: visible, on a layer that is neither off nor frozen, and not the paper's own viewport.</summary>
        private static bool CanBePicked(DxfDatabase database, DxfEntity entity)
        {
            if (!entity.Visible || (entity is DxfViewport viewport && viewport.Number == 1))
            {
                return false;
            }

            if (!database.LayerTable.Has(entity.Layer))
            {
                return true;
            }

            DxfLayerTableRecord layer = database.LayerTable[entity.Layer];
            return !layer.IsFrozen && !layer.IsOff;
        }

        /// <summary>
        /// The key two objects share when they are alike under a mode: the kind of the object and the value of each property the
        /// mode names, the linetype scale to nine places, names without regard to case.
        /// </summary>
        internal static string KeyOf(DxfEntity entity, int mode)
        {
            var key = new StringBuilder(entity.GetType().Name);

            if ((mode & Color) != 0)
            {
                DxfColor color = entity.Color;
                key.Append('|')
                    .Append((int)color.ColorMethod)
                    .Append(':')
                    .Append(color.ColorIndex.ToString(CultureInfo.InvariantCulture))
                    .Append(':')
                    .Append(color.ColorValue.ToString(CultureInfo.InvariantCulture));
            }

            if ((mode & Layer) != 0)
            {
                key.Append('|').Append(entity.Layer.ToUpperInvariant());
            }

            if ((mode & Linetype) != 0)
            {
                key.Append('|').Append(entity.Linetype.ToUpperInvariant());
            }

            if ((mode & LinetypeScale) != 0)
            {
                key.Append('|').Append(Math.Round(entity.LinetypeScale, 9).ToString("R", CultureInfo.InvariantCulture));
            }

            if ((mode & Lineweight) != 0)
            {
                key.Append('|').Append((int)entity.LineWeight);
            }

            if ((mode & ObjectStyle) != 0)
            {
                key.Append('|').Append(StyleOf(entity).ToUpperInvariant());
            }

            if ((mode & Name) != 0)
            {
                key.Append('|').Append(NameOf(entity).ToUpperInvariant());
            }

            return key.ToString();
        }

        /// <summary>The style an object is drawn in: its text style, dimension style, multileader style or table style; empty for an object with none.</summary>
        private static string StyleOf(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfDBText text:
                    return text.TextStyleName ?? string.Empty;
                case DxfMText mtext:
                    return mtext.TextStyleName ?? string.Empty;
                case DxfDimension dimension:
                    return dimension.DimensionStyleName ?? string.Empty;
                case DxfLeader leader:
                    return leader.DimensionStyleName ?? string.Empty;
                case DxfTolerance tolerance:
                    return tolerance.DimensionStyleName ?? string.Empty;
                case DxfMLeader multileader:
                    return multileader.MLeaderStyle ?? string.Empty;
                case DxfTable table:
                    return table.TableStyleName ?? string.Empty;
                default:
                    return string.Empty;
            }
        }

        /// <summary>The name an object carries: the block of an insert, the pattern of a hatch; empty for an object with none.</summary>
        private static string NameOf(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfBlockReference insert:
                    return insert.Name ?? string.Empty;
                case DxfHatch hatch:
                    return hatch.PatternName ?? string.Empty;
                default:
                    return string.Empty;
            }
        }
    }
}
