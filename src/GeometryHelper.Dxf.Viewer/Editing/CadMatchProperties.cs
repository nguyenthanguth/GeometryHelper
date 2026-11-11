using System;
using System.Collections.Generic;
using System.Reflection;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The properties MATCHPROP copies from its source, as the check boxes of AutoCAD's Property Settings dialog: the basic
    /// ones of every object and the special ones of an object of the source's kind. Plot style, Material, Shadow display and
    /// Center object are not among them: the dialog shows them greyed.
    /// </summary>
    [Flags]
    public enum CadMatchProperties
    {
        /// <summary>None.</summary>
        None = 0,

        /// <summary>The colour.</summary>
        Color = 1,

        /// <summary>The layer.</summary>
        Layer = 2,

        /// <summary>The linetype.</summary>
        Linetype = 4,

        /// <summary>The linetype scale.</summary>
        LinetypeScale = 8,

        /// <summary>The lineweight.</summary>
        Lineweight = 16,

        /// <summary>The transparency.</summary>
        Transparency = 32,

        /// <summary>The thickness, of a line, circle, arc, point, text, polyline or 2D solid.</summary>
        Thickness = 64,

        /// <summary>A dimension's style and its overrides of the style.</summary>
        Dimension = 128,

        /// <summary>A text's or an MTEXT's text style and height; a text's oblique angle and width factor from a text.</summary>
        Text = 256,

        /// <summary>A hatch's pattern, its angle and its scale, or its spacing and whether it is double.</summary>
        Hatch = 512,

        /// <summary>A polyline's width, where the source's is one along its whole length, and its linetype generation.</summary>
        Polyline = 1024,

        /// <summary>A viewport's scale, whether its display is locked, and the layers frozen in it.</summary>
        Viewport = 2048,

        /// <summary>A table's table style.</summary>
        Table = 4096,

        /// <summary>A multileader's multileader style.</summary>
        Multileader = 8192,

        /// <summary>Every property above, as AutoCAD checks them all until they are changed.</summary>
        All =
            Color
            | Layer
            | Linetype
            | LinetypeScale
            | Lineweight
            | Transparency
            | Thickness
            | Dimension
            | Text
            | Hatch
            | Polyline
            | Viewport
            | Table
            | Multileader,
    }

    /// <summary>Copies the properties of one object onto another, as MATCHPROP does.</summary>
    internal static class CadPropertyMatcher
    {
        /// <summary>The words of "Current active settings:", in AutoCAD's order.</summary>
        private static readonly KeyValuePair<CadMatchProperties, string>[] Words =
        {
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Color, "Color"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Layer, "Layer"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Linetype, "Ltype"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.LinetypeScale, "Ltscale"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Lineweight, "Lineweight"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Transparency, "Transparency"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Thickness, "Thickness"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Dimension, "Dim"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Text, "Text"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Hatch, "Hatch"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Polyline, "Polyline"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Viewport, "Viewport"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Table, "Table"),
            new KeyValuePair<CadMatchProperties, string>(CadMatchProperties.Multileader, "Multileader"),
        };

        /// <summary>The settings as AutoCAD lists them: "Color Layer Ltype Ltscale ...".</summary>
        internal static string Describe(CadMatchProperties which)
        {
            var words = new List<string>();

            foreach (KeyValuePair<CadMatchProperties, string> word in Words)
            {
                if ((which & word.Key) != 0)
                {
                    words.Add(word.Value);
                }
            }

            return string.Join(" ", words);
        }

        /// <summary>Copies the properties chosen from one object of a drawing onto another.</summary>
        internal static void Apply(DxfDatabase db, DxfEntity from, DxfEntity to, CadMatchProperties which)
        {
            if ((which & CadMatchProperties.Layer) != 0)
            {
                to.Layer = from.Layer;
            }

            if ((which & CadMatchProperties.Color) != 0)
            {
                to.Color = from.Color;
            }

            if ((which & CadMatchProperties.Linetype) != 0)
            {
                to.Linetype = from.Linetype;
            }

            if ((which & CadMatchProperties.LinetypeScale) != 0)
            {
                to.LinetypeScale = from.LinetypeScale;
            }

            if ((which & CadMatchProperties.Lineweight) != 0)
            {
                to.LineWeight = from.LineWeight;
            }

            if ((which & CadMatchProperties.Transparency) != 0 && !Equals(to.Transparency, from.Transparency))
            {
                to.Transparency = from.Transparency;
            }

            if ((which & CadMatchProperties.Thickness) != 0)
            {
                PropertyInfo source = Thickness(from);
                PropertyInfo target = Thickness(to);

                if (source != null && target != null)
                {
                    target.SetValue(to, source.GetValue(from));
                }
            }

            if (
                (which & CadMatchProperties.Dimension) != 0
                && from is DxfDimension dimension
                && to is DxfDimension other
            )
            {
                CadDimStyleEditor.Match(db, dimension, other);
            }

            if ((which & CadMatchProperties.Text) != 0)
            {
                Text(db, from, to);
            }

            if ((which & CadMatchProperties.Hatch) != 0 && from is DxfHatch hatch && to is DxfHatch fill)
            {
                Hatch(hatch, fill);
            }

            if ((which & CadMatchProperties.Polyline) != 0 && from is DxfPolyline polyline && to is DxfPolyline line)
            {
                Polyline(polyline, line);
            }

            if ((which & CadMatchProperties.Viewport) != 0 && from is DxfViewport viewport && to is DxfViewport view)
            {
                view.Locked = viewport.Locked;

                if (viewport.Height > 0.0 && view.Height > 0.0)
                {
                    view.CustomScale = viewport.CustomScale;
                }

                view.ThawAllLayersInViewport();
                view.FreezeLayersInViewport(viewport.GetFrozenLayers());
            }

            if (
                (which & CadMatchProperties.Table) != 0
                && from is DxfTable table
                && to is DxfTable grid
                && table.TableStyle != null
                && !ReferenceEquals(table.TableStyle, grid.TableStyle)
            )
            {
                grid.TableStyle = table.TableStyle;
                CadTableEditor.Redraw(grid, CadDocument.Showing(db));
            }

            if (
                (which & CadMatchProperties.Multileader) != 0
                && from is DxfMLeader mleader
                && to is DxfMLeader leader
                && mleader.MLeaderStyle != null
            )
            {
                leader.MLeaderStyle = mleader.MLeaderStyle;
            }
        }

        private static PropertyInfo Thickness(DxfEntity entity)
        {
            PropertyInfo property = entity
                .GetType()
                .GetProperty("Thickness", BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.PropertyType == typeof(double) && property.CanRead && property.CanWrite
                ? property
                : null;
        }

        /// <summary>The text style and height between texts and MTEXTs, the oblique angle and width factor from a text to a text.</summary>
        private static void Text(DxfDatabase db, DxfEntity from, DxfEntity to)
        {
            string style;
            double height;

            switch (from)
            {
                case DxfDBText text:
                    style = text.TextStyleName;
                    height = text.Height;
                    break;
                case DxfMText mtext:
                    style = mtext.TextStyleName;
                    height = mtext.TextHeight;
                    break;
                default:
                    return;
            }

            bool known = !string.IsNullOrEmpty(style) && db.TextStyleTable.Has(style);

            switch (to)
            {
                case DxfDBText text:
                    if (known)
                    {
                        text.TextStyleName = style;
                    }

                    text.Height = height;

                    if (from is DxfDBText source)
                    {
                        text.Oblique = source.Oblique;
                        text.WidthFactor = source.WidthFactor;
                    }

                    break;
                case DxfMText mtext:
                    if (known)
                    {
                        mtext.TextStyleName = style;
                    }

                    mtext.TextHeight = height;
                    break;
            }
        }

        /// <summary>The pattern, its angle and scale, or the spacing and double of one made by the user; a gradient is not copied.</summary>
        private static void Hatch(DxfHatch from, DxfHatch to)
        {
            if (from.IsGradient)
            {
                return;
            }

            to.SetHatchPattern(from.PatternType, from.PatternName);

            if (from.IsSolidFill)
            {
                return;
            }

            to.PatternAngle = from.PatternAngle;

            if (from.PatternType == DxfHatchPatternType.UserDefined)
            {
                to.PatternSpace = from.PatternSpace;
                to.PatternDouble = from.PatternDouble;
            }
            else
            {
                to.PatternScale = from.PatternScale;
            }
        }

        /// <summary>The linetype generation, and the width where the source has one width all along, as AutoCAD copies them.</summary>
        private static void Polyline(DxfPolyline from, DxfPolyline to)
        {
            to.Plinegen = from.Plinegen;

            if (from.NumberOfVertices == 0)
            {
                return;
            }

            double width = from.GetStartWidthAt(0);
            double tolerance = Tolerance.Default.EqualPoint;

            for (int i = 0; i < from.NumberOfVertices; i++)
            {
                if (
                    Math.Abs(from.GetStartWidthAt(i) - width) > tolerance
                    || Math.Abs(from.GetEndWidthAt(i) - width) > tolerance
                )
                {
                    return;
                }
            }

            for (int i = 0; i < to.NumberOfVertices; i++)
            {
                to.SetStartWidthAt(i, width);
                to.SetEndWidthAt(i, width);
            }

            to.ConstantWidth = width;
        }
    }
}
