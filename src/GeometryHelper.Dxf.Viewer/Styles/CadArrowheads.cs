using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// AutoCAD's arrowheads of dimensions and leaders: the names its Dimension Style Manager lists, the blocks it makes for
    /// them when a style first uses one (<c>_ARCHTICK</c>, <c>_DOT</c>), and their shapes, one unit long with the tip at the
    /// origin and the body towards -X, as AutoCAD and ezdxf draw them.
    /// </summary>
    /// <remarks>
    /// Closed filled has no block: a style names none for it. A style that names a block of another name (a user arrow) is
    /// listed under that name and drawn closed filled by the presenter.
    /// </remarks>
    public static class CadArrowheads
    {
        /// <summary>The name of AutoCAD's own arrowhead, which needs no block.</summary>
        public const string ClosedFilled = "Closed filled";

        private static readonly KeyValuePair<string, string>[] Table =
        {
            new KeyValuePair<string, string>(ClosedFilled, string.Empty),
            new KeyValuePair<string, string>("Closed blank", "_CLOSEDBLANK"),
            new KeyValuePair<string, string>("Dot", "_DOT"),
            new KeyValuePair<string, string>("Architectural tick", "_ARCHTICK"),
            new KeyValuePair<string, string>("Oblique", "_OBLIQUE"),
            new KeyValuePair<string, string>("Open", "_OPEN"),
            new KeyValuePair<string, string>("Origin indicator", "_ORIGIN"),
            new KeyValuePair<string, string>("Right angle", "_OPEN90"),
            new KeyValuePair<string, string>("Open 30", "_OPEN30"),
            new KeyValuePair<string, string>("Dot small", "_DOTSMALL"),
            new KeyValuePair<string, string>("Dot blank", "_DOTBLANK"),
            new KeyValuePair<string, string>("Dot small blank", "_SMALL"),
            new KeyValuePair<string, string>("Box", "_BOXBLANK"),
            new KeyValuePair<string, string>("Box filled", "_BOXFILLED"),
            new KeyValuePair<string, string>("Datum triangle", "_DATUMBLANK"),
            new KeyValuePair<string, string>("Datum triangle filled", "_DATUMFILLED"),
            new KeyValuePair<string, string>("Integral", "_INTEGRAL"),
            new KeyValuePair<string, string>("None", "_NONE"),
        };

        /// <summary>Gets the names of the arrowheads, in the order of AutoCAD's list: Closed filled, Closed blank, Dot, ... None.</summary>
        public static IReadOnlyList<string> Names { get; } = Array.ConvertAll(Table, p => p.Key);

        /// <summary>Gets the name of the arrowhead a style's block draws: Closed filled for none, the block's own name for a user arrow.</summary>
        /// <param name="block">The block a style names, or null.</param>
        /// <returns>The name, as "Architectural tick".</returns>
        public static string NameOf(DxfBlockTableRecord block)
        {
            if (block == null)
            {
                return ClosedFilled;
            }

            string name = block.Name ?? string.Empty;

            foreach (KeyValuePair<string, string> pair in Table)
            {
                if (pair.Value.Length > 0 && string.Equals(pair.Value, name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Key;
                }
            }

            return name;
        }

        /// <summary>
        /// Gets the block of an arrowhead in a drawing, made as AutoCAD makes it the first time a style uses it: the shape one
        /// unit long, colour ByBlock, on layer 0; null for Closed filled. A user arrow is the block of that name, where the
        /// drawing has it. Call it inside an edit of the document, as it may add a block.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The name of the arrowhead, as <see cref="Names"/> lists it.</param>
        /// <returns>The block, or null for Closed filled and for a name the drawing has no block of.</returns>
        public static DxfBlockTableRecord Ensure(DxfDatabase database, string name)
        {
            string block = BlockOf(name);

            if (block == null)
            {
                return name != null && database.BlockTable.Has(name) ? database.BlockTable[name] : null;
            }

            if (block.Length == 0)
            {
                return null;
            }

            if (database.BlockTable.Has(block))
            {
                return database.BlockTable[block];
            }

            var record = new DxfBlockTableRecord { Name = block };
            database.BlockTable.Add(record);

            foreach (DxfEntity entity in Shape(block, new GeoPoint2(0.0, 0.0), new GeoVector2(1.0, 0.0), 1.0))
            {
                record.AppendEntity(entity);
            }

            return record;
        }

        /// <summary>Whether the dimension line runs to the tip of the arrowhead and on past the extension lines by DIMDLE: the ticks, Oblique and Architectural tick.</summary>
        internal static bool IsTick(string blockName)
            => string.Equals(blockName, "_ARCHTICK", StringComparison.OrdinalIgnoreCase) || string.Equals(blockName, "_OBLIQUE", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The entities of an arrowhead of a block name (empty for Closed filled), its tip at a point, pointing a way, a size
        /// long, colour ByBlock: lines, arcs, circles and filled SOLIDs, a filled dot as a fan of SOLIDs.
        /// </summary>
        internal static List<DxfEntity> Shape(string blockName, GeoPoint2 tip, GeoVector2 way, double size)
        {
            var parts = new List<DxfEntity>();
            GeoVector2 x = way * size;
            var y = new GeoVector2(-x.Y, x.X);
            Func<double, double, GeoPoint3> at = (u, v) => CadCommands.P3(tip + x * u + y * v);
            string name = (blockName ?? string.Empty).ToUpperInvariant();
            double angle = Math.Atan2(way.Y, way.X);

            switch (name)
            {
                case "":
                    parts.Add(new DxfSolid(at(0, 0), at(-1, 1.0 / 6.0), at(-1, -1.0 / 6.0)));
                    break;
                case "_CLOSEDBLANK":
                    Lines(parts, at, -1, 1.0 / 6.0, 0, 0, -1, -1.0 / 6.0, -1, 1.0 / 6.0);
                    break;
                case "_DOT":
                    Disc(parts, at, 0.5, 0.0);
                    Lines(parts, at, -0.5, 0, -1, 0);
                    break;
                case "_ARCHTICK":
                    double w = 0.075 / Math.Sqrt(2.0);
                    parts.Add(new DxfSolid(at(-0.5 + w, -0.5 - w), at(-0.5 - w, -0.5 + w), at(0.5 + w, 0.5 - w), at(0.5 - w, 0.5 + w)));
                    break;
                case "_OBLIQUE":
                    Lines(parts, at, -0.5, -0.5, 0.5, 0.5);
                    break;
                case "_OPEN":
                case "_OPEN90":
                case "_OPEN30":
                    double h = Math.Sin((name == "_OPEN" ? 18.924644 : name == "_OPEN90" ? 90.0 : 30.0) * Math.PI / 360.0);
                    Lines(parts, at, -1, h, 0, 0, -1, -h);
                    Lines(parts, at, 0, 0, -1, 0);
                    break;
                case "_ORIGIN":
                    parts.Add(new DxfCircle(at(0, 0), new GeoVector3(0, 0, 1), 0.5 * size));
                    Lines(parts, at, 0, 0, -1, 0);
                    break;
                case "_DOTSMALL":
                    Disc(parts, at, 0.125, 0.0);
                    break;
                case "_DOTBLANK":
                    parts.Add(new DxfCircle(at(0, 0), new GeoVector3(0, 0, 1), 0.5 * size));
                    Lines(parts, at, -0.5, 0, -1, 0);
                    break;
                case "_SMALL":
                    parts.Add(new DxfCircle(at(0, 0), new GeoVector3(0, 0, 1), 0.25 * size));
                    break;
                case "_BOXBLANK":
                    Lines(parts, at, -0.5, -0.5, 0.5, -0.5, 0.5, 0.5, -0.5, 0.5, -0.5, -0.5);
                    Lines(parts, at, -0.5, 0, -1, 0);
                    break;
                case "_BOXFILLED":
                    parts.Add(new DxfSolid(at(-0.5, -0.5), at(0.5, -0.5), at(-0.5, 0.5), at(0.5, 0.5)));
                    Lines(parts, at, -0.5, 0, -1, 0);
                    break;
                case "_DATUMBLANK":
                    Lines(parts, at, 0, 0.577350269, 0, -0.577350269, -1, 0, 0, 0.577350269);
                    break;
                case "_DATUMFILLED":
                    parts.Add(new DxfSolid(at(0, 0.577350269), at(0, -0.577350269), at(-1, 0)));
                    break;
                case "_INTEGRAL":
                    double r = 0.3535534;
                    parts.Add(new DxfArc(at(-r, 0), r * size, Turned(angle - Math.PI / 2.0), Turned(angle)));
                    parts.Add(new DxfArc(at(r, 0), r * size, Turned(angle + Math.PI / 2.0), Turned(angle + Math.PI)));
                    break;
                case "_NONE":
                    break;
                default:
                    parts.Add(new DxfSolid(at(0, 0), at(-1, 1.0 / 6.0), at(-1, -1.0 / 6.0)));
                    break;
            }

            foreach (DxfEntity part in parts)
            {
                part.Color = DxfColor.ByBlock;
            }

            return parts;
        }

        /// <summary>The block name of an arrowhead's name: empty for Closed filled, null for a name not AutoCAD's.</summary>
        internal static string BlockOf(string name)
        {
            foreach (KeyValuePair<string, string> pair in Table)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        /// <summary>A run of lines through points given as u, v pairs of the arrowhead's frame.</summary>
        private static void Lines(List<DxfEntity> parts, Func<double, double, GeoPoint3> at, params double[] uv)
        {
            for (int i = 2; i + 1 < uv.Length; i += 2)
            {
                parts.Add(new DxfLine(at(uv[i - 2], uv[i - 1]), at(uv[i], uv[i + 1])));
            }
        }

        /// <summary>A filled disc about a point on the arrowhead's axis, as sixteen SOLIDs from its centre, as the drawing of a donut fills it.</summary>
        private static void Disc(List<DxfEntity> parts, Func<double, double, GeoPoint3> at, double radius, double u)
        {
            const int Sides = 16;

            for (int i = 0; i < Sides; i++)
            {
                double a = 2.0 * Math.PI * i / Sides;
                double b = 2.0 * Math.PI * (i + 1) / Sides;
                parts.Add(new DxfSolid(at(u, 0), at(u + radius * Math.Cos(a), radius * Math.Sin(a)), at(u + radius * Math.Cos(b), radius * Math.Sin(b))));
            }
        }

        private static double Turned(double angle)
        {
            angle -= Math.Floor(angle / (2.0 * Math.PI)) * 2.0 * Math.PI;
            return angle >= 2.0 * Math.PI ? 0.0 : angle;
        }
    }
}
