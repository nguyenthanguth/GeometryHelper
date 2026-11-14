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

        /// <summary>Makes the block of a picture: a block of inches, a polyline or a text for each shape, the text styles they need.</summary>
        private static DxfBlockTableRecord WmfBlock(CadEditScope s, string name, CadWmfPicture picture, double[] box)
        {
            double f = Math.Min(box[0] / picture.ExtentX, box[1] / picture.ExtentY);
            var record = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0.0, 0.0, 0.0) };
            s.Database.BlockTable.Add(record);
            Keep(() => record.Units = DxfUnits.Inches, record.Units != DxfUnits.Inches);

            foreach (CadWmfShape shape in picture.Shapes)
            {
                DxfEntity made = shape.Text == null ? WmfPolyline(shape, f) : WmfText(s.Database, shape, f);
                made.Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, WmfColourIndex(shape.Rgb));
                record.AppendEntity(made);
            }

            return record;
        }

        private static DxfPolyline2d WmfPolyline(CadWmfShape shape, double f)
        {
            var points = new List<GeoPoint3>(shape.Points.Count);

            foreach (double[] point in shape.Points)
            {
                points.Add(new GeoPoint3(point[0] * f, -point[1] * f, 0.0));
            }

            double width = shape.Width * f;
            return new DxfPolyline2d(DxfPoly2dType.SimplePoly, points, 0.0, shape.Closed, width, width, null);
        }

        /// <summary>
        /// A TEXT of the shape: its height 0.72 of the em, as AutoCAD 2021 gives Arial's capitals; the reference point, the top
        /// left of the font's cell, taken to the baseline 0.9 of the em below it (the baseline itself, or the bottom of the cell,
        /// by the alignment), turned with the font; a text aligned to the centre or the right justified there.
        /// </summary>
        private static DxfDBText WmfText(DxfDatabase database, CadWmfShape shape, double f)
        {
            double em = shape.Em * f;
            double down =
                (shape.Align & 24) == 24 ? 0.0
                : (shape.Align & 8) != 0 ? -0.2 * em
                : 0.9 * em;
            var anchor = new GeoPoint3(
                shape.X * f + down * Math.Sin(shape.Turn),
                -shape.Y * f - down * Math.Cos(shape.Turn),
                0.0
            );
            var text = new DxfDBText
            {
                TextString = shape.Text,
                Height = 0.72 * em,
                TextStyleName = WmfStyle(database, shape),
                Position = anchor,
                Rotation = shape.Turn,
            };

            if ((shape.Align & 6) != 0)
            {
                text.Justify = (shape.Align & 6) == 6 ? DxfAttachmentPoint.BaseCenter : DxfAttachmentPoint.BaseRight;
                text.AlignmentPoint = anchor;
            }

            return text;
        }

        /// <summary>
        /// The text style of a look of a face, "WMF-Arial0" for the first look of Arial the file uses, "WMF-Arial1" for the second
        /// and so on: made of the face with its bold and italic file, or the one the drawing has already with the look.
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
                    style.Font = new DxfFontDescriptor(shape.Face, shape.Bold, shape.Italic, 0, 34);
                    return style.Name;
                }

                DxfFontDescriptor font = database.TextStyleTable[name].Font;

                if (
                    string.Equals(font.TypeFace, shape.Face, StringComparison.Ordinal)
                    && font.Bold == shape.Bold
                    && font.Italic == shape.Italic
                )
                {
                    return database.TextStyleTable[name].Name;
                }
            }
        }

        /// <summary>The index of AutoCAD's nearest a colour, 7 for black as for white, which a pen of either draws in the foreground.</summary>
        private static short WmfColourIndex(int rgb) =>
            rgb == 0 ? (short)7 : DxfColor.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb).ColorIndex;
    }
}
