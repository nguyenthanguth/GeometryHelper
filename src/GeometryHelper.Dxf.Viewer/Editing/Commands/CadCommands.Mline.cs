using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Multilines, as AutoCAD 2021 asks for them: MLINE here, MLEDIT in CadCommands.MlineEdit.cs and MLSTYLE in
    /// CadCommands.MlineStyle.cs, with the variables CMLJUST, CMLSCALE and CMLSTYLE. The prompts and the records were read
    /// from AutoCAD 2021's accoreconsole.
    /// </summary>
    internal static partial class CadCommands
    {
        internal static void AddMlineTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("MLINE", Mline));
            registry.Add(new CadCommand("MLEDIT", MlineEditDialog));
            registry.Add(new CadCommand("MLSTYLE", MlineStyleDialog));

            // How new multilines stand on the points picked, their scale and their style.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "CMLJUST",
                    true,
                    0,
                    2,
                    s => s.Document.Database.Cmljust,
                    (c, v) => c.Database.Cmljust = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "CMLSCALE",
                    true,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => s.Document.Database.Cmlscale,
                    (c, v) => c.Database.Cmlscale = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Text(
                    "CMLSTYLE",
                    true,
                    s => CurrentMlineStyleName(s.Document.Database),
                    CurrentMlineStyleSet
                )
            );
        }

        /// <summary>
        /// MLINE: "Current settings: Justification = Top, Scale = 20.00, Style = STANDARD", then "Specify start point or
        /// [Justification/Scale/STyle]:", each option asking its question and coming back; then "Specify next point:", with
        /// Undo from the second point on and Close from the third, until Enter. The multiline is drawn, under the cursor, as the
        /// points so far and the cursor make it, and made when the command ends.
        /// </summary>
        private static IEnumerable<CadPrompt> Mline(CadCommandContext c)
        {
            EnsureStandardMlineStyle(c);
            var points = new List<GeoPoint2>();

            while (true)
            {
                DxfMlineStyle current = CurrentMlineStyle(c.Database);

                if (current == null)
                {
                    c.Write("The multiline style the header names is not in the drawing.");
                    yield break;
                }

                c.Write(
                    "Current settings: Justification = "
                        + JustificationName(c.Database.Cmljust)
                        + ", Scale = "
                        + MlineScaleText(c.Database.Cmlscale)
                        + ", Style = "
                        + current.Name
                );
                yield return CadPrompt.ForPoint(
                    "Specify start point",
                    null,
                    new[] { "Justification", "Scale", "STyle" }
                );

                if (c.Input.Is("Justification"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter justification type",
                        new[] { "Top", "Zero", "Bottom" },
                        JustificationName(c.Database.Cmljust).ToLowerInvariant()
                    );

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        int value =
                            c.Input.Is("Zero") ? 1
                            : c.Input.Is("Bottom") ? 2
                            : 0;
                        SetMlineSetting(
                            c,
                            "$CMLJUST",
                            JustificationName(c.Database.Cmljust),
                            JustificationName(value),
                            d => d.Cmljust = value
                        );
                    }

                    continue;
                }

                if (c.Input.Is("Scale"))
                {
                    yield return CadPrompt.ForDistance(
                        "Enter mline scale",
                        null,
                        null,
                        MlineScaleText(c.Database.Cmlscale)
                    );

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        double value = c.Input.Number;
                        SetMlineSetting(c, "$CMLSCALE", MlineScaleText(c.Database.Cmlscale), MlineScaleText(value), d =>
                            d.Cmlscale = value
                        );
                    }

                    continue;
                }

                if (c.Input.Is("STyle"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForString("Enter mline style name or [?]");
                        string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                        if (typed.Length == 0)
                        {
                            break;
                        }

                        if (typed == "?")
                        {
                            ListMlineStyles(c);
                            continue;
                        }

                        DxfMlineStyle found = FindMlineStyle(c.Database, typed);

                        if (found != null)
                        {
                            SetMlineStyle(c, found);
                            break;
                        }
                    }

                    continue;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                points.Add(c.Input.Point);
                break;
            }

            DxfMlineStyle style = CurrentMlineStyle(c.Database);
            var just = (DxfMlineJustification)c.Database.Cmljust;
            double scale = c.Database.Cmlscale;

            while (true)
            {
                var placed = new List<GeoPoint2>(points);
                string[] keywords =
                    points.Count >= 3 ? new[] { "Close", "Undo" }
                    : points.Count == 2 ? new[] { "Undo" }
                    : null;
                yield return CadPrompt
                    .ForPoint("Specify next point", points[points.Count - 1], keywords)
                    .Previewing(
                        CadPreview.Making(c, input => MlinePreview(c, style, just, scale, placed, input.Point))
                    );

                if (c.Input.Is("Undo"))
                {
                    points.RemoveAt(points.Count - 1);
                    continue;
                }

                if (c.Input.Is("Close"))
                {
                    AppendMline(c, style, just, scale, points, true);
                    yield break;
                }

                if (c.Input.IsPoint)
                {
                    if ((c.Input.Point - points[points.Count - 1]).Length > c.Session.Tolerance.EqualPoint)
                    {
                        points.Add(c.Input.Point);
                    }

                    continue;
                }

                if (points.Count >= 2)
                {
                    AppendMline(c, style, just, scale, points, false);
                }

                yield break;
            }
        }

        /// <summary>A multiline of the style, justification and scale through the points, not yet in a drawing, with the current look.</summary>
        private static DxfMlineBuilder MlineBuilder(
            DxfMlineStyle style,
            DxfMlineJustification justification,
            double scale,
            IReadOnlyList<GeoPoint2> points,
            bool closed
        )
        {
            var builder = new DxfMlineBuilder(style)
            {
                Scale = scale,
                Justification = justification,
                IsClosed = closed,
            };

            foreach (GeoPoint2 point in points)
            {
                builder.Points.Add(P3(point));
            }

            return builder;
        }

        private static void AppendMline(
            CadCommandContext c,
            DxfMlineStyle style,
            DxfMlineJustification justification,
            double scale,
            IReadOnlyList<GeoPoint2> points,
            bool closed
        ) =>
            c.Apply(null, s =>
                s.Append(c.Space, Current(c, MlineBuilder(style, justification, scale, points, closed).Build()))
            );

        /// <summary>The multiline the points so far and the cursor make, as the lines and arcs it breaks into, for the preview.</summary>
        private static IEnumerable<DxfEntity> MlinePreview(
            CadCommandContext c,
            DxfMlineStyle style,
            DxfMlineJustification justification,
            double scale,
            List<GeoPoint2> placed,
            GeoPoint2 cursor
        )
        {
            var through = new List<GeoPoint2>(placed);

            if ((cursor - placed[placed.Count - 1]).Length > c.Session.Tolerance.EqualPoint)
            {
                through.Add(cursor);
            }

            if (through.Count < 2)
            {
                return Array.Empty<DxfEntity>();
            }

            DxfMlineBuilder builder = MlineBuilder(style, justification, scale, through, false);
            return builder.Explode(Current(c, builder.Build()));
        }

        /// <summary>Gives a drawing with no style STANDARD the one AutoCAD starts every drawing with, as one undo step with the command.</summary>
        private static void EnsureStandardMlineStyle(CadCommandContext c)
        {
            DxfDBDictionary styles = c.Database.MLStyleDictionary;

            if (styles != null && styles.Contains("Standard"))
            {
                return;
            }

            c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Multiline styles", null, "STANDARD") },
                s => s.Database.AddMlineStyle("Standard", new DxfMlineStyleSettings())
            );
        }

        /// <summary>The style new multilines take, the one the header names; null where the drawing has none of that name.</summary>
        private static DxfMlineStyle CurrentMlineStyle(DxfDatabase database) => database.CmlstyleID as DxfMlineStyle;

        private static string CurrentMlineStyleName(DxfDatabase database) =>
            CurrentMlineStyle(database)?.Name ?? "STANDARD";

        /// <summary>The style of a name in the drawing's dictionary of multiline styles, letters of either case the same; null for none.</summary>
        private static DxfMlineStyle FindMlineStyle(DxfDatabase database, string name)
        {
            DxfDBDictionary styles = database.MLStyleDictionary;
            return styles != null && styles.Contains(name) ? styles.GetAt(name) as DxfMlineStyle : null;
        }

        /// <summary>The name of the multiline style a name finds, by its entry in the dictionary.</summary>
        private static string MlineStyleKey(DxfDatabase database, DxfMlineStyle style) =>
            database.MLStyleDictionary.NameAt(style);

        private static string JustificationName(int value) =>
            value == 1 ? "Zero"
            : value == 2 ? "Bottom"
            : "Top";

        private static string MlineScaleText(double value) =>
            (Math.Abs(value) < 5E-3 ? 0.0 : value).ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>Makes a style the one new multilines take, one undo step.</summary>
        private static void SetMlineStyle(CadCommandContext c, DxfMlineStyle style)
        {
            DxfMlineStyle before = CurrentMlineStyle(c.Database);
            string old = before == null ? string.Empty : MlineStyleKey(c.Database, before);
            string now = MlineStyleKey(c.Database, style);

            SetMlineSetting(c, "$CMLSTYLE", old, now, d => d.CmlstyleID = style);
        }

        /// <summary>Sets a header variable of multilines as a step of undo, as every setting is.</summary>
        private static void SetMlineSetting(
            CadCommandContext c,
            string name,
            string old,
            string now,
            Action<DxfDatabase> set
        )
        {
            if (!string.Equals(old, now, StringComparison.Ordinal))
            {
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, name, old, now) }, s =>
                    set(s.Database)
                );
            }
        }

        /// <summary>CMLSTYLE set by SETVAR: a style of the drawing, letters of either case the same; one it has not is refused.</summary>
        private static string CurrentMlineStyleSet(CadCommandContext c, string typed)
        {
            DxfMlineStyle found = FindMlineStyle(c.Database, typed.Trim());

            if (found == null)
            {
                return "Cannot find multiline style \"" + typed + "\".";
            }

            SetMlineStyle(c, found);
            return null;
        }

        /// <summary>The list of styles the "?" of MLINE's style option writes: "Loaded mline styles:", a header and a line for each.</summary>
        private static void ListMlineStyles(CadCommandContext c)
        {
            c.Write(string.Empty);
            c.Write("Loaded mline styles:");
            c.Write(string.Empty);
            c.Write("      Name          Description");
            c.Write("---------------- ------------------");

            foreach (DxfMlineStyle style in MlineStyles(c.Database))
            {
                string name = style.Name;
                c.Write(style.Description.Length == 0 ? name : name.PadRight(17) + style.Description);
            }

            c.Write(string.Empty);
        }

        /// <summary>The styles of the drawing by name, letters of either case the same, two of one name in the order of the dictionary.</summary>
        private static List<DxfMlineStyle> MlineStyles(DxfDatabase database)
        {
            var styles = new List<DxfMlineStyle>();

            if (database.MLStyleDictionary != null)
            {
                foreach (DxfDBDictionaryEntry entry in database.MLStyleDictionary)
                {
                    if (entry.Value is DxfMlineStyle style)
                    {
                        styles.Add(style);
                    }
                }
            }

            return styles
                .Select((style, index) => new { style, index })
                .OrderBy(s => s.style.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(s => s.index)
                .Select(s => s.style)
                .ToList();
        }
    }
}
