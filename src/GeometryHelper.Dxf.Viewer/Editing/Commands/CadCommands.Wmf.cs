using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// WMFIN, which brings a Windows Metafile in as a block, as AutoCAD 2021's console does (probed through standard input),
    /// and the variables WMFBKGND and WMFFOREGND. AutoCAD 2021's IMPORT takes no .wmf (it answers "Can't find file." to one
    /// that is there), so the metafile is WMFIN's alone.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The one type of the file dialog WMFIN shows.</summary>
        private const string MetafileType = "Metafile (*.wmf)";

        /// <summary>The half of the console's view, VIEWSIZE 10 at 942 by 534 pixels, used where no view of the canvas gives one.</summary>
        private static readonly double[] ConsoleBox = { 8.82739, 5.0 };

        internal static void AddWmfTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("WMFIN", WmfIn));

            // Whether the background of a metafile is kept (1) or left out (0), and which of its colours is the foreground:
            // AutoCAD 2021 keeps both as variables and its console draws the same metafile whatever they are, so they are only kept.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "WMFBKGND",
                    false,
                    0,
                    1,
                    s => (int)Kept(s, "wmfbkgnd", 0.0),
                    (c, v) => c.Remember("wmfbkgnd", v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "WMFFOREGND",
                    false,
                    0,
                    1,
                    s => (int)Kept(s, "wmfforegnd", 0.0),
                    (c, v) => c.Remember("wmfforegnd", v)
                )
            );
        }

        /// <summary>
        /// WMFIN: the window's Open dialog for a metafile, or with FILEDIA 0, or with no window to show it, "Enter name of WMF
        /// file to import (no extension):", a name with .wmf too taken as well. The picture is read (<see cref="CadWmf"/>) and
        /// made block WMF0, WMF1 and so on of 2D polylines and texts, laid out with its top left at the origin of the block, y
        /// down, and fitted with its aspect kept into half the view (<c>x*f</c>, <c>-y*f</c> for the logical x and y, f the
        /// smaller of the half view's width over the window's width and its height over its height, the origin of the window
        /// not taken). The block is of inches, so its INSERT is placed as INSERT places one, at the factor of the drawing's
        /// unit (25.4 in millimetres), with the questions of AutoCAD's: "Units: Unitless   Conversion:   25.4000", the
        /// insertion point (Basepoint, Scale, X, Y, Z, Rotate, Explode and REpeat among its options), the scales and the
        /// rotation. A name that is no file is "Cannot find the specified metafile"; a file that is no metafile is refused.
        /// </summary>
        private static IEnumerable<CadPrompt> WmfIn(CadCommandContext c)
        {
            var job = new CadExportJob("Import WMF", new[] { MetafileType }, MetafileType, null) { Opens = true };
            string file;

            if (c.Session.FileDialogs && c.Session.Request("WMFIN", c.Document, job))
            {
                if (job.FileName == null)
                {
                    yield break;
                }

                file = job.FileName;
            }
            else
            {
                yield return CadPrompt.ForString("Enter name of WMF file to import (no extension)");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                {
                    yield break;
                }

                file = c.Input.Text.Trim().Trim('"');
            }

            string folder = c.Document.DrawingFolder;
            string found = MetafileFound(file, folder);

            if (found == null)
            {
                c.Write("Cannot find the specified metafile");
                yield break;
            }

            CadWmfPicture picture;

            try
            {
                picture = CadWmf.Read(File.ReadAllBytes(found));
            }
            catch (InvalidDataException exception)
            {
                c.Write("The file is no Windows Metafile: " + exception.Message);
                yield break;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                c.Write("The file cannot be read: " + exception.Message);
                yield break;
            }

            if (picture.Shapes.Count == 0)
            {
                c.Write("The metafile draws nothing: nothing was imported.");
                yield break;
            }

            string name = "WMF0";

            for (int n = 1; c.Database.BlockTable.Has(name); n++)
            {
                name = "WMF" + n.ToString(CultureInfo.InvariantCulture);
            }

            DxfBlockTableRecord made = null;
            string block = name;
            double[] box = WmfBox(c);
            CadEditResult result = c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Insert block", null, block) },
                s => made = WmfBlock(s, block, picture, box)
            );

            if (!result.Succeeded || made == null)
            {
                yield break;
            }

            var placing = new Placing { Scale = new GeoVector3(1.0, 1.0, 1.0), Wmf = true };

            do
            {
                placing.Placed = false;

                foreach (CadPrompt prompt in Place(c, block, placing, true, true, true, placing.Explode))
                {
                    yield return prompt;
                }
            } while (placing.Repeat && placing.Placed);
        }

        /// <summary>The file a name typed is: as typed where it is a file, with .wmf where it has no extension, and beside the drawing where it is not a full path; null where there is none.</summary>
        private static string MetafileFound(string typed, string folder)
        {
            string[] names = Path.HasExtension(typed) ? new[] { typed } : new[] { typed + ".wmf", typed };

            foreach (string candidate in names)
            {
                try
                {
                    string full =
                        Path.IsPathRooted(candidate) || folder == null ? candidate : Path.Combine(folder, candidate);

                    if (File.Exists(full))
                    {
                        return full;
                    }

                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // A name with a character a path cannot hold is no file.
                }
            }

            return null;
        }

        /// <summary>Half the view in its own units, width and height: of the canvas's view, else the console's.</summary>
        private static double[] WmfBox(CadCommandContext c)
        {
            CadView view = c.Session.WorkView;
            DisplayBox seen = view != null ? view.VisibleBox : DisplayBox.Empty;

            return seen.IsEmpty ? ConsoleBox : new[] { (seen.MaxX - seen.MinX) / 2.0, (seen.MaxY - seen.MinY) / 2.0 };
        }

        /// <summary>
        /// How a picture is laid out: the scale of the picture (its width and height are those of the window, so the pen's width
        /// scales with it), the signs of x and y, and the scale of a pen's width. A window of width and height above nought is
        /// fitted by the smaller of the two scales and drawn with y down; one of a width below nought is turned over in x and
        /// fitted by its height; one of a height below nought is turned over in y and fitted by its width; one of both below
        /// nought is fitted by the larger of the two scales, drawn upright, and has a pen of no width, as AutoCAD 2021 gives them.
        /// </summary>
        private sealed class WmfLayout
        {
            public WmfLayout(CadWmfPicture picture, double[] box)
            {
                double byWidth = box[0] / picture.ExtentX;
                double byHeight = box[1] / picture.ExtentY;
                Sx = 1.0;
                Sy = -1.0;
                PenScale = 1.0;

                if (picture.NegativeX && picture.NegativeY)
                {
                    F = Math.Max(byWidth, byHeight);
                    PenScale = 0.0;
                }
                else if (picture.NegativeX)
                {
                    F = byHeight;
                    Sx = -1.0;
                }
                else if (picture.NegativeY)
                {
                    F = byWidth;
                    Sy = 1.0;
                }
                else
                {
                    F = Math.Min(byWidth, byHeight);
                }
            }

            /// <summary>The scale of the picture, from a logical unit to a unit of the block.</summary>
            public double F { get; }

            /// <summary>The sign of x: below nought where the picture is turned over in x.</summary>
            public double Sx { get; }

            /// <summary>The sign of y: below nought, as the picture is drawn y down, unless it is turned over in y.</summary>
            public double Sy { get; }

            /// <summary>The factor of a pen's width: nought where both extents are below nought.</summary>
            public double PenScale { get; }
        }

        /// <summary>Makes the block of a picture: a block of inches, a polyline or a text for each shape, the text styles they need.</summary>
        private static DxfBlockTableRecord WmfBlock(CadEditScope s, string name, CadWmfPicture picture, double[] box)
        {
            var layout = new WmfLayout(picture, box);
            var record = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0.0, 0.0, 0.0) };
            s.Database.BlockTable.Add(record);
            Keep(() => record.Units = DxfUnits.Inches, record.Units != DxfUnits.Inches);

            foreach (CadWmfShape shape in picture.Shapes)
            {
                DxfEntity made = shape.Text == null ? WmfPolyline(shape, layout) : WmfText(s.Database, shape, layout);
                made.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, WmfColourIndex(shape.Rgb));
                record.AppendEntity(made);
            }

            return record;
        }

        private static DxfPolyline2d WmfPolyline(CadWmfShape shape, WmfLayout layout)
        {
            var points = new List<GeoPoint3>(shape.Points.Count);

            foreach (double[] point in shape.Points)
            {
                points.Add(new GeoPoint3(layout.Sx * point[0] * layout.F, layout.Sy * point[1] * layout.F, 0.0));
            }

            double width = shape.Width * layout.F * layout.PenScale;
            return new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 0.0, shape.Closed, width, width, null);
        }

        /// <summary>The ascent of Arial's cell as a part of the em, taken for a face whose font file is not found.</summary>
        private const double ArialAscent = 0.905;

        /// <summary>The descent of Arial's cell, and the height of its capitals, likewise.</summary>
        private const double ArialDescent = 0.212;

        private const double ArialCapHeight = 0.716;

        /// <summary>
        /// How much further than its upright ascent (in ems) AutoCAD 2021 reaches along a text turned to no quarter of a turn,
        /// measured at an em of 400 units through its console, for the faces named; any other face is given the mean, 0.1.
        /// </summary>
        private static readonly Dictionary<string, double> TurnedExtra = new Dictionary<string, double>(
            StringComparer.OrdinalIgnoreCase
        )
        {
            { "Arial", 0.1525 },
            { "Arial Narrow", 0.18 },
            { "Calibri", 0.09 },
            { "Consolas", 0.1075 },
            { "Courier New", 0.205 },
            { "Georgia", 0.05 },
            { "Microsoft Sans Serif", 0.0975 },
            { "Segoe UI", 0.235 },
            { "Tahoma", 0.05 },
            { "Times New Roman", 0.1675 },
            { "Trebuchet MS", 0.0625 },
            { "Verdana", 0.0625 },
        };

        /// <summary>
        /// A TEXT of the shape, as AutoCAD 2021 makes it from a metafile (measured through its console). The ascent of the font's
        /// cell, its Windows ascent times the em rounded to a unit of the file, is how far the baseline lies below the reference
        /// point (the top left of the cell), and the text's height is 0.8 of it: 72 units for an Arial of 80, which is 0.72 of the
        /// em, 0.905 for Arial's baseline where the guess was 0.9. A text turned a quarter reaches 0.0175 of the em further, and
        /// one turned to any other angle by a measure of its face (<see cref="TurnedExtra"/>). The bottom of the cell is the
        /// font's descent times the em times the ascent over the capitals' height (1.263 for Arial) above the point. A text
        /// aligned to the centre or the right is not justified: it stays left justified and starts the width of the text,
        /// or half of it, back from the point, the width being the font's advances times that same ratio.
        /// </summary>
        private static DxfDBText WmfText(DxfDatabase database, CadWmfShape shape, WmfLayout layout)
        {
            CadFontMetrics metrics = CadFontMetrics.Of(CadTrueTypeFonts.PathOf(shape.Face, shape.Bold, shape.Italic));
            double ascent = metrics != null ? metrics.Ascent : ArialAscent;
            double descent = metrics != null ? metrics.Descent : ArialDescent;
            double cap = metrics != null && metrics.CapHeight > 0.0 ? metrics.CapHeight : ArialCapHeight;
            double ratio = ascent / cap;
            double quarters = shape.Turn / (Math.PI / 2.0);
            double extra = 0.0;

            if (Math.Abs(quarters - Math.Round(quarters)) > 1E-6)
            {
                extra = (TurnedExtra.TryGetValue(shape.Face, out double known) ? known : 0.1) * shape.Em;
            }
            else if ((Math.Abs((long)Math.Round(quarters)) & 1) == 1)
            {
                extra = Math.Ceiling(0.0175 * shape.Em - 1E-9);
            }

            double rise = Math.Max(1.0, Math.Round(shape.Em * ascent)) + Math.Round(extra);
            double f = layout.F;
            double down =
                (shape.Align & 24) == 24 ? 0.0
                : (shape.Align & 8) != 0 ? -descent * shape.Em * ratio
                : rise;
            double across = 0.0;

            if ((shape.Align & 6) != 0)
            {
                double width =
                    (metrics != null ? metrics.Width(shape.Text) : 0.5 * shape.Text.Length) * shape.Em * ratio;
                across = (shape.Align & 6) == 6 ? width / 2.0 : width;
            }

            double sin = Math.Sin(shape.Turn);
            double cos = Math.Cos(shape.Turn);
            return new DxfDBText
            {
                TextString = shape.Text,
                Height = 0.8 * rise * f,
                TextStyleName = WmfStyle(database, shape),
                Position = new GeoPoint3(
                    layout.Sx * shape.X * f + (down * sin - across * cos) * f,
                    layout.Sy * shape.Y * f - (down * cos + across * sin) * f,
                    0.0
                ),
                Rotation = shape.Turn,
            };
        }

        /// <summary>
        /// The text style of a look of a face, "WMF-Arial0" for the first look of Arial the file uses, "WMF-Arial1" for the second
        /// and so on: made of the face with its bold and italic file, or the one the drawing has already with the look. A look is
        /// the face (letters of either case the same), bold (a weight of 700), italic, character set and pitch and family, as
        /// AutoCAD 2021 tells them apart; underline and strike-out do not make another.
        /// </summary>
        private static string WmfStyle(DxfDatabase database, CadWmfShape shape)
        {
            for (int n = 0; ; n++)
            {
                string name = "WMF-" + shape.Face + n.ToString(CultureInfo.InvariantCulture);

                if (!database.TextStyleTable.Has(name))
                {
                    string path = CadTrueTypeFonts.PathOf(shape.Face, shape.Bold, shape.Italic);
                    var style = new DxfTextStyleTableRecord { Name = name };
                    database.TextStyleTable.Add(style);
                    style.FileName = path == null ? CadTrueTypeFonts.FileOf(shape.Face) : Path.GetFileName(path);
                    style.BigFontFileName = string.Empty;
                    style.Font = new DxfFontDescriptor(
                        shape.Face,
                        shape.Bold,
                        shape.Italic,
                        shape.Charset,
                        shape.Pitch
                    );
                    return style.Name;
                }

                DxfFontDescriptor font = database.TextStyleTable[name].Font;

                if (
                    string.Equals(font.TypeFace, shape.Face, StringComparison.OrdinalIgnoreCase)
                    && font.Bold == shape.Bold
                    && font.Italic == shape.Italic
                    && font.CharacterSet == shape.Charset
                    && font.PitchAndFamily == shape.Pitch
                )
                {
                    return database.TextStyleTable[name].Name;
                }
            }
        }

        /// <summary>
        /// The colours AutoCAD 2021's WMFIN chooses a pen or brush colour from, by index: the first seven, the 24 hues of 15 degrees
        /// in the ten each has (full, then half washed out, each at the four lower levels 166, 128, 76 and 38 of 255 for the even
        /// ones and 166, 127, 77 and 38 for the odd) and the greys 250 to 255 at 84, 118, 152, 187, 221 and 255. Not the colours
        /// the indexes give on screen (12 is (204, 0, 0) there and (166, 0, 0) here): found by probing 72 000 colours through
        /// the console, of which this table gives the same index for 99.7 per cent. Index 0, 8, 9 and 10 are never chosen.
        /// </summary>
        private static readonly Lazy<int[]> WmfPalette = new Lazy<int[]>(() =>
        {
            var palette = new int[256];

            for (int i = 1; i <= 7; i++)
            {
                palette[i] = Rgb(DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)i));
            }

            int[] even = { 166, 128, 76, 38 };
            int[] odd = { 166, 127, 77, 38 };

            for (int hue = 0; hue < 24; hue++)
            {
                DxfColor full = DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)(10 + 10 * hue));
                DxfColor wash = DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)(11 + 10 * hue));
                palette[10 + 10 * hue] = Rgb(full);
                palette[11 + 10 * hue] = Rgb(wash);

                for (int level = 0; level < 4; level++)
                {
                    palette[12 + 10 * hue + 2 * level] = Scaled(full, even[level]);
                    palette[13 + 10 * hue + 2 * level] = Scaled(wash, odd[level]);
                }
            }

            int[] greys = { 84, 118, 152, 187, 221, 255 };

            for (int i = 0; i < greys.Length; i++)
            {
                palette[250 + i] = (greys[i] << 16) | (greys[i] << 8) | greys[i];
            }

            return palette;
        });

        private static int Rgb(DxfColor colour) => (colour.Red << 16) | (colour.Green << 8) | colour.Blue;

        private static int Scaled(DxfColor colour, int level)
        {
            int Part(byte part) => (int)Math.Round(part * level / 255.0, MidpointRounding.AwayFromZero);

            return (Part(colour.Red) << 16) | (Part(colour.Green) << 8) | Part(colour.Blue);
        }

        /// <summary>
        /// The index AutoCAD 2021's WMFIN gives a colour: the nearest of <see cref="WmfPalette"/> by distance in red, green and
        /// blue, the lowest index of the nearest, 7 standing at black as at white (a pen of either draws in the foreground).
        /// </summary>
        private static short WmfColourIndex(int rgb)
        {
            int[] palette = WmfPalette.Value;
            int red = (rgb >> 16) & 0xFF;
            int green = (rgb >> 8) & 0xFF;
            int blue = rgb & 0xFF;
            long black = (long)red * red + (long)green * green + (long)blue * blue;
            long best = long.MaxValue;
            short index = 7;

            for (short i = 1; i <= 255; i++)
            {
                if (palette[i] == 0)
                {
                    continue;
                }

                long dr = ((palette[i] >> 16) & 0xFF) - red;
                long dg = ((palette[i] >> 8) & 0xFF) - green;
                long db = (palette[i] & 0xFF) - blue;
                long distance = dr * dr + dg * dg + db * db;

                if (i == 7)
                {
                    distance = Math.Min(distance, black);
                }

                if (distance < best)
                {
                    best = distance;
                    index = i;
                }
            }

            return index;
        }
    }
}
