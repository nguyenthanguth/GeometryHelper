using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// STATUS: writes the report AutoCAD 2021's STATUS writes: how many objects the drawing holds and where it is; the limits of
    /// the space worked in, with "(Off)" where LIMCHECK is off; what the space uses, with "**Over" on the corner that is outside
    /// the limits; what the display shows; the insertion base, the snap resolution and the grid spacing; the current space,
    /// layout, layer, colour, linetype, material, lineweight, elevation and thickness; the switches Fill, Grid, Ortho, Qtext,
    /// Snap and Tablet; the object snap modes; and the free space of the disk of the drawing. The snap, grid and object snap
    /// settings are those of the window's variables where it has them (SNAPUNIT, GRIDUNIT, SNAPMODE, GRIDMODE, ORTHOMODE,
    /// OSMODE), and AutoCAD's defaults where it has none. The undo file size and the memory lines of AutoCAD's report are not
    /// written: the presenter keeps no undo file.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Status(CadCommandContext c)
        {
            foreach (string line in StatusLines(c))
            {
                c.Write(line);
            }

            yield break;
        }

        /// <summary>The lines of the report of STATUS.</summary>
        internal static List<string> StatusLines(CadCommandContext c)
        {
            DxfDatabase database = c.Database;
            CadSpace space = c.Document.CurrentSpace;
            string kind = space.IsModel ? "Model" : "Paper";
            var lines = new List<string>();
            int objects = 0;

            foreach (CadSpace each in c.Document.Spaces)
            {
                foreach (DxfEntity entity in each.Block)
                {
                    objects++;
                }
            }

            lines.Add(
                objects.ToString(CultureInfo.InvariantCulture)
                    + " objects in "
                    + (string.IsNullOrEmpty(c.Document.FileName) ? c.Document.Title : c.Document.FileName)
            );
            CadLimits.Of(c.Document, out GeoPoint2 min, out GeoPoint2 max);
            string off = database.Limcheck ? string.Empty : "  (Off)";
            lines.Add(kind + " space limits are " + StatusCorner(min) + off);
            lines.Add(new string(' ', 23) + StatusCorner(max));

            DisplayBox used = c.Document.GetDisplay(space).Bounds;

            if (used.IsEmpty)
            {
                lines.Add((kind + " space uses").PadRight(23) + "*Nothing*");
            }
            else
            {
                double slack = c.Session.Tolerance.EqualPoint;
                bool below = used.MinX < min.X - slack || used.MinY < min.Y - slack;
                bool above = used.MaxX > max.X + slack || used.MaxY > max.Y + slack;
                lines.Add(
                    (kind + " space uses").PadRight(23)
                        + StatusCorner(new GeoPoint2(used.MinX, used.MinY))
                        + (below ? " **Over" : string.Empty)
                );
                lines.Add(
                    new string(' ', 23)
                        + StatusCorner(new GeoPoint2(used.MaxX, used.MaxY))
                        + (above ? " **Over" : string.Empty)
                );
            }

            DisplayBox shown = c.Session.View != null ? c.Session.WorkView.VisibleBox : DisplayBox.Empty;

            if (shown.IsEmpty)
            {
                shown = used.IsEmpty ? new DisplayBox(min.X, min.Y, max.X, max.Y) : used;
            }

            lines.Add("Display shows".PadRight(23) + StatusCorner(new GeoPoint2(shown.MinX, shown.MinY)));
            lines.Add(new string(' ', 23) + StatusCorner(new GeoPoint2(shown.MaxX, shown.MaxY)));
            GeoPoint3 insertion = database.Insbase;
            lines.Add(
                "Insertion base is".PadRight(23)
                    + StatusCorner(new GeoPoint2(insertion.X, insertion.Y))
                    + "   Z:"
                    + StatusFour(insertion.Z)
            );
            double[] snap = StatusNumbers(c, "SNAPUNIT", 10.0, 10.0);
            double[] grid = StatusNumbers(c, "GRIDUNIT", 10.0, 10.0);
            lines.Add("Snap resolution is".PadRight(23) + StatusCorner(new GeoPoint2(snap[0], snap[1])));
            lines.Add("Grid spacing is".PadRight(23) + StatusCorner(new GeoPoint2(grid[0], grid[1])));
            lines.Add(string.Empty);
            lines.Add("Current space:".PadRight(22) + (space.IsModel ? "Model space" : "Paper space"));
            lines.Add("Current layout:".PadRight(22) + (space.IsModel ? "Model" : space.Name));
            lines.Add("Current layer:".PadRight(22) + "\"" + database.Clayer + "\"");
            lines.Add("Current color:".PadRight(22) + ColorWords(database, database.Cecolor));
            lines.Add("Current linetype:".PadRight(22) + LinetypeWords(database, database.Celtype));
            lines.Add("Current material:".PadRight(22) + "BYLAYER -- \"Global\"");
            lines.Add("Current lineweight:".PadRight(22) + LineweightWords(database.Celweight));
            lines.Add(
                "Current elevation:".PadRight(22)
                    + Length(database.Elevation)
                    + "  thickness:"
                    + StatusFour(database.Thickness)
            );
            lines.Add(
                "Fill "
                    + OnOff(database.Fillmode)
                    + "  Grid "
                    + OnOff(StatusNumbers(c, "GRIDMODE", 0.0)[0] != 0.0)
                    + "  Ortho "
                    + OnOff(StatusNumbers(c, "ORTHOMODE", 0.0)[0] != 0.0)
                    + "  Qtext "
                    + OnOff(database.Qtextmode)
                    + "  Snap "
                    + OnOff(StatusNumbers(c, "SNAPMODE", 0.0)[0] != 0.0)
                    + "  Tablet off"
            );
            lines.Add("Object snap modes:".PadRight(22) + SnapModes((int)StatusNumbers(c, "OSMODE", 0.0)[0]));
            string disk = DiskLine(c.Document.FileName);

            if (disk != null)
            {
                lines.Add(disk);
            }

            return lines;
        }

        /// <summary>A corner as STATUS writes it: X: and Y: each in a field of ten, three spaces between.</summary>
        private static string StatusCorner(GeoPoint2 point) =>
            "X:" + StatusFour(point.X) + "   Y:" + StatusFour(point.Y);

        /// <summary>A number to four places in a field of ten, as STATUS writes coordinates.</summary>
        private static string StatusFour(double value) =>
            (Math.Abs(value) < 5E-5 ? 0.0 : value).ToString("0.0000", CultureInfo.InvariantCulture).PadLeft(10);

        private static string OnOff(bool on) => on ? "on" : "off";

        /// <summary>The numbers of a variable the window defines, or those given where the registry has none.</summary>
        private static double[] StatusNumbers(CadCommandContext c, string name, params double[] none)
        {
            double[] numbers = c.Session.Registry.FindVariable(name)?.GetNumbers(c.Session);
            return numbers != null && numbers.Length >= none.Length ? numbers : none;
        }

        private static string ColorWords(DxfDatabase database, DxfColor color)
        {
            if (color.IsByBlock)
            {
                return "BYBLOCK";
            }

            if (!color.IsByLayer)
            {
                return IndexWords(color);
            }

            DxfColor layerColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7);

            if (database.LayerTable.Has(database.Clayer))
            {
                layerColor = database.LayerTable[database.Clayer].Color;
            }

            return "BYLAYER -- " + IndexWords(layerColor);
        }

        /// <summary>A colour as STATUS names it: 7 (white) for an index of the first seven, 30 for another, 12,34,56 for a true colour.</summary>
        private static string IndexWords(DxfColor color)
        {
            if (color.IsByColor)
            {
                return CadPropertyEditor.ColorName(color);
            }

            int index = color.ColorIndex;
            string number = index.ToString(CultureInfo.InvariantCulture);
            return index >= 1 && index <= 7
                ? number + " (" + CadPropertyEditor.ColorName(color).ToLowerInvariant() + ")"
                : number;
        }

        private static string LinetypeWords(DxfDatabase database, string name) =>
            string.Equals(name, "ByLayer", StringComparison.OrdinalIgnoreCase)
                ? "BYLAYER -- \"" + LayerLinetype(database) + "\""
            : string.Equals(name, "ByBlock", StringComparison.OrdinalIgnoreCase) ? "BYBLOCK"
            : "\"" + name + "\"";

        /// <summary>The linetype of the current layer, Continuous where the layer is not found.</summary>
        private static string LayerLinetype(DxfDatabase database) =>
            database.LayerTable.Has(database.Clayer) ? database.LayerTable[database.Clayer].LinetypeName : "Continuous";

        private static string LineweightWords(DxfLineWeight weight) =>
            weight == DxfLineWeight.ByLayer ? "BYLAYER"
            : weight == DxfLineWeight.ByBlock ? "BYBLOCK"
            : weight == DxfLineWeight.ByLineWeightDefault ? "DEFAULT"
            : (((int)weight) / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm";

        /// <summary>The object snap modes of an OSMODE as STATUS lists them, in alphabetical order, "None" for none.</summary>
        private static string SnapModes(int mode)
        {
            var modes = new List<string>();
            var names = new SortedDictionary<string, int>(StringComparer.Ordinal)
            {
                ["Apparent intersection"] = 2048,
                ["Center"] = 4,
                ["Endpoint"] = 1,
                ["Extension"] = 4096,
                ["Insertion"] = 64,
                ["Intersection"] = 32,
                ["Midpoint"] = 2,
                ["Nearest"] = 512,
                ["Node"] = 8,
                ["Parallel"] = 8192,
                ["Perpendicular"] = 128,
                ["Quadrant"] = 16,
                ["Tangent"] = 256,
            };

            foreach (KeyValuePair<string, int> name in names)
            {
                if ((mode & name.Value) != 0)
                {
                    modes.Add(name.Key);
                }
            }

            return modes.Count == 0 ? "None" : string.Join(", ", modes);
        }

        /// <summary>"Free dwg disk (D:) space: 1263438.1 MBytes" for the disk of the drawing's file; null where it is not known.</summary>
        private static string DiskLine(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path))
                {
                    return null;
                }

                string root = Path.GetPathRoot(path);
                var drive = new DriveInfo(root);
                return "Free dwg disk ("
                    + root.TrimEnd('\\', '/')
                    + ") space: "
                    + (drive.AvailableFreeSpace / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture)
                    + " MBytes";
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
