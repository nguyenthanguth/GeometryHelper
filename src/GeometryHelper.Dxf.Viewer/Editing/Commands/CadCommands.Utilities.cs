using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The commands of the Utilities panel and the drawing utilities: SELECTALL, MEASUREGEOM (Distance, Radius, Angle, ARea),
    /// UNITS and -UNITS on the command line, LAYOUT's Rename and Set, SCRIPT, which feeds the lines of a .scr file to the
    /// command line as typed, and CECOLOR, the colour of new objects.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] LengthFormats =
        {
            "Report formats:          (Examples)",
            "  1. Scientific         1.55E+01",
            "  2. Decimal              15.50",
            "  3. Engineering        1'-3.50\"",
            "  4. Architectural    1'-3 1/2\"",
            "  5. Fractional          15 1/2",
        };

        private static readonly string[] AngleFormats =
        {
            "Systems of angle measure:        (Examples)",
            "  1. Decimal degrees         45.0000",
            "  2. Degrees/minutes/seconds 45d0'0\"",
            "  3. Grads                   50.0000g",
            "  4. Radians                 0.7854r",
            "  5. Surveyor's units        N 45d0'0\" E",
        };

        private static readonly string[] Directions =
        {
            "Direction for angle 0:",
            "        East   3 o'clock  =    0",
            "        North 12 o'clock  =   90",
            "        West   9 o'clock  =  180",
            "        South  6 o'clock  =  270",
        };

        /// <summary>SELECTALL (Ctrl+A): every object of the space shown selected, those on frozen layers left out.</summary>
        private static IEnumerable<CadPrompt> SelectAll(CadCommandContext c)
        {
            var all = new List<string>();

            foreach (DxfEntity entity in c.Space.Block)
            {
                bool frozen = c.Database.LayerTable.Has(entity.Layer) && c.Database.LayerTable[entity.Layer].IsFrozen;

                if (
                    !string.IsNullOrEmpty(entity.Handle)
                    && !frozen
                    && !(entity is DxfViewport viewport && viewport.Number == 1)
                )
                {
                    all.Add(entity.Handle);
                }
            }

            c.Document.Selection.Set(all);
            c.Write(all.Count.ToString(CultureInfo.InvariantCulture) + " found");
            yield break;
        }

        /// <summary>MEASUREGEOM: one measure after another, as DIST, AREA and the radius or angle of what is picked, until eXit.</summary>
        private static IEnumerable<CadPrompt> MeasureGeom(CadCommandContext c)
        {
            string offered = "Distance";

            while (true)
            {
                yield return CadPrompt.ForKeyword(
                    "Enter an option",
                    new[] { "Distance", "Radius", "Angle", "ARea", "eXit" },
                    offered
                );
                IEnumerable<CadPrompt> measure;

                if (c.Input.Is("Distance"))
                {
                    measure = Dist(c);
                }
                else if (c.Input.Is("Radius"))
                {
                    measure = MeasureRadius(c);
                }
                else if (c.Input.Is("Angle"))
                {
                    measure = MeasureAngle(c);
                }
                else if (c.Input.Is("ARea"))
                {
                    measure = Area(c);
                }
                else
                {
                    yield break;
                }

                foreach (CadPrompt prompt in measure)
                {
                    yield return prompt;
                }

                offered = "eXit";
            }
        }

        private static IEnumerable<CadPrompt> MeasureRadius(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select arc or circle");
            DxfEntity picked =
                c.Input.Kind == CadInputKind.Selection ? c.Document.FindEntity(c.Input.Handles[0]) : null;
            double radius =
                picked is DxfCircle circle ? circle.Radius
                : picked is DxfArc arc ? arc.Radius
                : double.NaN;

            if (double.IsNaN(radius))
            {
                c.Write(picked == null ? "Nothing selected." : "Object selected is not an arc or a circle.");
                yield break;
            }

            c.Write("Radius = " + Length(radius));
            c.Write("Diameter = " + Length(2.0 * radius));
        }

        /// <summary>The angle an arc spans, two lines make where they meet (towards their far ends), or three points make at the first.</summary>
        private static IEnumerable<CadPrompt> MeasureAngle(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select arc, circle, line, or", null, "specify vertex");
            double angle;

            if (c.Input.Kind == CadInputKind.Selection)
            {
                DxfEntity picked = c.Document.FindEntity(c.Input.Handles[0]);

                if (picked is DxfArc arc)
                {
                    angle = CadGrips.Sweep(arc.StartAngle, arc.EndAngle);
                }
                else if (picked is DxfLine line)
                {
                    yield return CadPrompt.ForEntity("Select second line");

                    if (
                        c.Input.Kind != CadInputKind.Selection
                        || !(c.Document.FindEntity(c.Input.Handles[0]) is DxfLine other)
                        || ReferenceEquals(other, line)
                    )
                    {
                        c.Write("Object selected is not a second line.");
                        yield break;
                    }

                    if (!Meet(line, other, c.Session.Tolerance, out GeoPoint2 vertex))
                    {
                        c.Write("Lines are parallel.");
                        yield break;
                    }

                    angle = Between(FarEnd(line, vertex) - vertex, FarEnd(other, vertex) - vertex);
                }
                else
                {
                    c.Write("Object selected is not an arc or a line.");
                    yield break;
                }
            }
            else if (c.Input.IsNone)
            {
                yield return CadPrompt.ForPoint("Specify angle vertex");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 vertex = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify first angle endpoint", vertex);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 first = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second angle endpoint", vertex);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                angle = Between(first - vertex, c.Input.Point - vertex);
            }
            else
            {
                yield break;
            }

            // Written as a direction of that many degrees is, counted by ANGBASE and ANGDIR, as AutoCAD 2021 writes it: 30
            // degrees as 60 where angles are counted from north, clockwise.
            c.Write("Angle = " + Degrees(c, c.Session.CountAngle(angle)) + "°");
        }

        private static GeoPoint2 FarEnd(DxfLine line, GeoPoint2 from)
        {
            var start = new GeoPoint2(line.StartPoint.X, line.StartPoint.Y);
            var end = new GeoPoint2(line.EndPoint.X, line.EndPoint.Y);
            return (start - from).Length > (end - from).Length ? start : end;
        }

        /// <summary>The angle between two ways, nought to pi; nought where either has no length.</summary>
        private static double Between(GeoVector2 u, GeoVector2 v)
        {
            double lengths = u.Length * v.Length;
            return lengths > 0.0 ? Math.Acos(Math.Max(-1.0, Math.Min(1.0, (u.X * v.X + u.Y * v.Y) / lengths))) : 0.0;
        }

        /// <summary>
        /// UNITS: the window's Drawing Units, shown again after Direction Control's pick of two points, its values written to
        /// the header on OK as one undo step; with no window, the prompts of -UNITS.
        /// </summary>
        private static IEnumerable<CadPrompt> Units(CadCommandContext c)
        {
            var request = new CadUnitsRequest(c.Database);

            while (true)
            {
                request.Action = CadDialogAction.Cancel;

                if (!c.Session.Request("UNITS", c.Document, request))
                {
                    foreach (CadPrompt prompt in UnitsTyped(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (request.Action != CadDialogAction.SelectObjects)
                {
                    break;
                }

                yield return CadPrompt.ForPoint("Pick angle");

                if (!c.Input.IsPoint)
                {
                    continue;
                }

                GeoPoint2 from = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second point", from);

                if (c.Input.IsPoint && (c.Input.Point - from).Length > 0.0)
                {
                    GeoVector2 way = c.Input.Point - from;
                    double angle = Math.Atan2(way.Y, way.X);
                    request.BaseAngle = angle < 0.0 ? angle + 2.0 * Math.PI : angle;
                }
            }

            if (request.Action != CadDialogAction.Ok)
            {
                yield break;
            }

            DxfDatabase database = c.Database;
            var records = new List<CadEditRecord>();
            Setting(records, "$LUNITS", database.Lunits, request.LengthUnits);
            Setting(records, "$LUPREC", database.Luprec, request.LengthPrecision);
            Setting(records, "$AUNITS", database.Aunits, request.AngleUnits);
            Setting(records, "$AUPREC", database.Auprec, request.AnglePrecision);
            Setting(records, "$ANGBASE", database.Angbase, request.BaseAngle);
            Setting(records, "$ANGDIR", database.Angdir ? 1 : 0, request.Clockwise ? 1 : 0);
            Setting(records, "$INSUNITS", (int)database.Insunits, (int)request.InsertionUnits);

            if (records.Count > 0)
            {
                c.Apply(records, s =>
                {
                    s.Database.Lunits = request.LengthUnits;
                    s.Database.Luprec = request.LengthPrecision;
                    s.Database.Aunits = request.AngleUnits;
                    s.Database.Auprec = request.AnglePrecision;
                    s.Database.Angbase = request.BaseAngle;
                    s.Database.Angdir = request.Clockwise;

                    if (s.Database.Insunits != request.InsertionUnits)
                    {
                        s.Database.Insunits = request.InsertionUnits;
                    }
                });
            }
        }

        /// <summary>
        /// -UNITS: how lengths and angles are shown ($LUNITS, $LUPREC, $AUNITS, $AUPREC), the direction of angle nought ($ANGBASE)
        /// and whether angles grow clockwise ($ANGDIR), asked in AutoCAD's words; Enter keeps each, and what changed is set at the
        /// end as one undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> UnitsTyped(CadCommandContext c)
        {
            DxfDatabase database = c.Database;
            var answer = new Ref<int>();

            foreach (string line in LengthFormats)
            {
                c.Write(line);
            }

            foreach (CadPrompt prompt in WholeIn(c, "Enter choice, 1 to 5", 1, 5, database.Lunits, answer))
            {
                yield return prompt;
            }

            int lunits = answer.Value;
            int luprec;

            if (lunits == 4 || lunits == 5)
            {
                c.Write("Enter denominator of smallest fraction to display");

                while (true)
                {
                    foreach (
                        CadPrompt prompt in WholeIn(
                            c,
                            "(1, 2, 4, 8, 16, 32, 64, 128, or 256)",
                            1,
                            256,
                            1 << Math.Min(8, database.Luprec),
                            answer
                        )
                    )
                    {
                        yield return prompt;
                    }

                    if ((answer.Value & (answer.Value - 1)) == 0)
                    {
                        break;
                    }

                    c.Write("Requires 1, 2, 4, 8, 16, 32, 64, 128, or 256.");
                }

                luprec = (int)Math.Round(Math.Log(answer.Value, 2.0));
            }
            else
            {
                foreach (
                    CadPrompt prompt in WholeIn(
                        c,
                        "Enter number of digits to right of decimal point (0 to 8)",
                        0,
                        8,
                        database.Luprec,
                        answer
                    )
                )
                {
                    yield return prompt;
                }

                luprec = answer.Value;
            }

            foreach (string line in AngleFormats)
            {
                c.Write(line);
            }

            foreach (CadPrompt prompt in WholeIn(c, "Enter choice, 1 to 5", 1, 5, database.Aunits + 1, answer))
            {
                yield return prompt;
            }

            int aunits = answer.Value - 1;

            foreach (
                CadPrompt prompt in WholeIn(
                    c,
                    "Enter number of fractional places for display of angles (0 to 8)",
                    0,
                    8,
                    database.Auprec,
                    answer
                )
            )
            {
                yield return prompt;
            }

            int auprec = answer.Value;

            foreach (string line in Directions)
            {
                c.Write(line);
            }

            double angbase = database.Angbase;
            string shown = (angbase * 180.0 / Math.PI).ToString(
                "F" + auprec.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture
            );
            yield return CadPrompt.ForAngle("Enter direction for angle 0", null, null, shown).Plain();

            if (c.Input.Kind == CadInputKind.Number)
            {
                angbase = c.Input.Number;
            }
            else if (!c.Input.IsNone)
            {
                yield break;
            }

            yield return CadPrompt.ForKeyword(
                "Measure angles clockwise?",
                new[] { "Yes", "No" },
                database.Angdir ? "Yes" : "No"
            );

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            bool angdir = c.Input.Is("Yes");
            var records = new List<CadEditRecord>();
            Setting(records, "$LUNITS", database.Lunits, lunits);
            Setting(records, "$LUPREC", database.Luprec, luprec);
            Setting(records, "$AUNITS", database.Aunits, aunits);
            Setting(records, "$AUPREC", database.Auprec, auprec);
            Setting(records, "$ANGBASE", database.Angbase, angbase);
            Setting(records, "$ANGDIR", database.Angdir ? 1 : 0, angdir ? 1 : 0);

            if (records.Count > 0)
            {
                c.Apply(records, s =>
                {
                    s.Database.Lunits = lunits;
                    s.Database.Luprec = luprec;
                    s.Database.Aunits = aunits;
                    s.Database.Auprec = auprec;
                    s.Database.Angbase = angbase;
                    s.Database.Angdir = angdir;
                });
            }
        }

        /// <summary>Asks a whole number from <paramref name="low"/> to <paramref name="high"/> until one is given; Enter keeps the one offered.</summary>
        private static IEnumerable<CadPrompt> WholeIn(
            CadCommandContext c,
            string message,
            int low,
            int high,
            int offered,
            Ref<int> result
        )
        {
            result.Value = offered;

            while (true)
            {
                yield return CadPrompt.ForInteger(message, null, offered.ToString(CultureInfo.InvariantCulture));

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number >= low && c.Input.Number <= high)
                {
                    result.Value = (int)c.Input.Number;
                    yield break;
                }

                c.Write(
                    "Requires an integer between "
                        + low.ToString(CultureInfo.InvariantCulture)
                        + " and "
                        + high.ToString(CultureInfo.InvariantCulture)
                        + "."
                );
            }
        }

        /// <summary>
        /// CECOLOR: the colour new objects are drawn in ($CECOLOR), as AutoCAD asks it: ByLayer, ByBlock, a name from red to
        /// white or an index 1 to 255, one undo step; Enter keeps it. The header holds an index alone, so Truecolor, red, green
        /// and blue, and COlorbook are refused, said on the command line.
        /// </summary>
        private static IEnumerable<CadPrompt> CurrentColor(CadCommandContext c) =>
            CurrentColorAsked(c, "Enter new object color", ColorShown);

        /// <summary>The prompt of CECOLOR and -COLOR, each in its own words and with its own way of offering the colour.</summary>
        private static IEnumerable<CadPrompt> CurrentColorAsked(
            CadCommandContext c,
            string message,
            Func<DxfColor, string> offered
        )
        {
            DxfColor old = c.Database.Cecolor;
            string shown = offered(old);
            yield return CadPrompt.ForKeywordOrText(message, new[] { "Truecolor", "COlorbook" }, shown);

            if (c.Input.Is("Truecolor") || c.Input.Is("COlorbook"))
            {
                c.Write(
                    c.Input.Is("Truecolor")
                        ? "The drawing keeps the colour of new objects as an index: give an object a true colour in Properties."
                        : "Color books are not available here."
                );
                yield break;
            }

            if (c.Input.Kind != CadInputKind.Text)
            {
                yield break;
            }

            string error = CadPropertyEditor.ParseColor(c.Input.Text.Trim(), out DxfColor color);

            if (error == null && color.IsByColor)
            {
                error = "The drawing keeps the colour of new objects as an index: ByLayer, ByBlock or 1 to 255.";
            }

            if (error != null)
            {
                c.Write(error);
                yield break;
            }

            if (!color.Equals(old))
            {
                c.Apply(
                    new[]
                    {
                        new CadEditRecord(
                            CadEditRecordKind.SettingChanged,
                            null,
                            "$CECOLOR",
                            ColorShown(old),
                            CadPropertyEditor.ColorName(color)
                        ),
                    },
                    s => s.Database.Cecolor = color
                );
            }
        }

        private static void Setting(List<CadEditRecord> records, string name, double old, double value)
        {
            if (old != value)
            {
                records.Add(
                    new CadEditRecord(CadEditRecordKind.SettingChanged, null, name, Values(old), Values(value))
                );
            }
        }

        private static CadSpace FindLayout(CadCommandContext c, string name)
        {
            foreach (CadSpace space in c.Document.Spaces)
            {
                if (string.Equals(space.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return space;
                }
            }

            return null;
        }

        /// <summary>
        /// SCRIPT: the lines of a script file, each fed to the command line as typed and Enter pressed once SCRIPT has ended, a
        /// blank line being Enter and a line that starts with a semicolon a comment; the first line refused stops it.
        /// </summary>
        private static IEnumerable<CadPrompt> Script(CadCommandContext c)
        {
            string offered = c.Document.HasFile ? Path.ChangeExtension(c.Document.FileName, ".scr") : null;
            yield return CadPrompt.ForString("Enter script file name", offered);
            string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : offered;

            if (string.IsNullOrEmpty(name))
            {
                yield break;
            }

            string[] lines = null;

            try
            {
                name = Path.HasExtension(name) ? name : name + ".scr";

                if (!Path.IsPathRooted(name) && c.Document.HasFile)
                {
                    name = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(c.Document.FileName)), name);
                }

                lines = File.ReadAllLines(name);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                )
            {
                GeometryHelperLog.Debug("CadCommands: the script " + name + " could not be read: " + exception.Message);
            }

            if (lines == null)
            {
                c.Write("Cannot find script file \"" + name + "\".");
                yield break;
            }

            c.Session.QueueScript(new List<string>(lines));
        }
    }
}
