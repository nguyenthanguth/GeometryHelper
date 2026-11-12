using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>-SCALELISTEDIT: the drawing's list of scales (ACAD_SCALELIST) listed, added to, taken from and reset.</summary>
    internal static partial class CadCommands
    {
        private const string InvalidRatio = "Invalid entry. Expects a ratio entered in the following format: \"1:1\"";

        /// <summary>The longest name AutoCAD 2021's -SCALELISTEDIT takes for a scale: 64 characters (65 is "Scale name too long.").</summary>
        private const int MaxScaleNameLength = 64;

        /// <summary>
        /// -SCALELISTEDIT, word for word as AutoCAD 2021's console asks it: ? lists the scales with their units, Add asks a name
        /// and a ratio (1:3, 2.5:1) and redefines a scale of that name where asked, Delete takes one out or * every one in no
        /// use, Reset puts back the default list (the metric scales 1:1 to 100:1) and Exit ends. Each change is one undo step.
        /// A scale in use, the unit scale and the current annotation scale are kept: "Scale 1:1 is referenced so it cannot
        /// be deleted."
        /// </summary>
        private static IEnumerable<CadPrompt> ScaleListEdit(CadCommandContext c)
        {
            while (true)
            {
                yield return CadPrompt.ForKeyword(
                    "Enter option",
                    new[] { "?", "Add", "Delete", "Reset", "Exit" },
                    "Add"
                );

                if (c.Input.Is("Exit"))
                {
                    yield break;
                }

                if (c.Input.Is("?"))
                {
                    ListScales(c);
                    continue;
                }

                if (c.Input.Is("Delete"))
                {
                    yield return CadPrompt.ForString("Enter scale name to delete or * to delete all unused scales");
                    DeleteScales(c, (c.Input.Text ?? string.Empty).Trim());
                    continue;
                }

                if (c.Input.Is("Reset"))
                {
                    yield return CadPrompt.ForKeyword("Reset scale list to defaults?", new[] { "Yes", "No" }, "No");

                    if (!c.Input.Is("Yes"))
                    {
                        c.Write("Scale list not reset.");
                        continue;
                    }

                    if (c.Apply(ScaleListRecord("Reset"), ResetScales).Succeeded)
                    {
                        c.Write("Scale list reset to default entries.");
                    }

                    continue;
                }

                if (!c.Input.IsNone && !c.Input.Is("Add"))
                {
                    yield break;
                }

                string name;

                while (true)
                {
                    yield return CadPrompt.ForString("Enter name for new scale");
                    name = (c.Input.Text ?? string.Empty).Trim();

                    if (name.Length == 0)
                    {
                        c.Write("Must enter a valid name.");
                    }
                    else if (name.Length > MaxScaleNameLength)
                    {
                        c.Write("Scale name too long.");
                    }
                    else
                    {
                        break;
                    }
                }

                DxfAnnotationScale existing = Scales(c.Database)
                    .FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    c.Write(string.Empty);
                    yield return CadPrompt.ForKeyword(
                        name + " already exists.  Redefine scale?",
                        new[] { "Yes", "No" },
                        "No"
                    );

                    if (!c.Input.Is("Yes"))
                    {
                        continue;
                    }
                }

                double paper = 0.0;
                double drawing = 0.0;

                while (true)
                {
                    yield return CadPrompt.ForString("Enter scale ratio");

                    if (c.Input.IsNone || TryReadRatio(c.Input.Text, out paper, out drawing))
                    {
                        break;
                    }

                    c.Write(InvalidRatio);
                }

                if (c.Input.IsNone)
                {
                    continue;
                }

                string scaleName = existing?.Name ?? name;
                double paperUnits = paper;
                double drawingUnits = drawing;
                CadEditResult result = c.Apply(ScaleListRecord(scaleName), s =>
                {
                    DxfAnnotationScale same = Scales(s.Database)
                        .FirstOrDefault(x => string.Equals(x.Name, scaleName, StringComparison.OrdinalIgnoreCase));

                    if (same != null)
                    {
                        same.PaperUnits = paperUnits;
                        same.DrawingUnits = drawingUnits;
                        return;
                    }

                    ScaleList(s.Database)
                        .AddContext(
                            new DxfAnnotationScale
                            {
                                Name = scaleName,
                                PaperUnits = paperUnits,
                                DrawingUnits = drawingUnits,
                            }
                        );
                });

                if (!result.Succeeded)
                {
                    c.Write(result.Message);
                }
            }
        }

        /// <summary>? of -SCALELISTEDIT: the scales numbered, with their paper and drawing units and the scale they give, as AutoCAD lists them.</summary>
        private static void ListScales(CadCommandContext c)
        {
            c.Write("    Scale Name             Paper Units  Drawing Units  Effective Scale");
            c.Write("    ----------             -----------  -------------  ---------------");
            int number = 0;

            foreach (DxfAnnotationScale scale in Scales(c.Database))
            {
                number++;
                string paper = scale.PaperUnits.ToString("0.0000", CultureInfo.InvariantCulture);
                c.Write(
                    number.ToString(CultureInfo.InvariantCulture).PadLeft(2)
                        + ": "
                        + scale.Name
                        + paper.PadLeft(Math.Max(34 - scale.Name.Length, paper.Length + 1))
                        + scale.DrawingUnits.ToString("0.0000", CultureInfo.InvariantCulture).PadLeft(14)
                        + scale.Scale.ToString("0.0000", CultureInfo.InvariantCulture).PadLeft(14)
                );
            }
        }

        /// <summary>Delete of -SCALELISTEDIT: one scale by its name, or * every one no object, CANNOSCALE or the unit scale keeps.</summary>
        private static void DeleteScales(CadCommandContext c, string name)
        {
            if (name.Length == 0)
            {
                return;
            }

            if (name == "*")
            {
                if (Scales(c.Database).Any(s => Removable(c.Database, s.Name)))
                {
                    c.Apply(ScaleListRecord("*"), s =>
                    {
                        foreach (
                            DxfAnnotationScale scale in Scales(s.Database)
                                .Where(x => Removable(s.Database, x.Name))
                                .ToList()
                        )
                        {
                            ScaleList(s.Database).RemoveContext(scale.Name);
                        }
                    });
                }

                return;
            }

            DxfAnnotationScale found = Scales(c.Database)
                .FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                c.Write("Scale " + name + " not found.");
                return;
            }

            if (Scales(c.Database).Count == 1)
            {
                c.Write("Scale list must contain at least one scale. " + found.Name + " not deleted.");
                return;
            }

            if (!Removable(c.Database, found.Name))
            {
                c.Write("Scale " + found.Name + " is referenced so it cannot be deleted.");
                return;
            }

            string named = found.Name;

            if (c.Apply(ScaleListRecord(named), s => ScaleList(s.Database).RemoveContext(named)).Succeeded)
            {
                c.Write(named + " scale deleted.");
            }
        }

        /// <summary>
        /// Reset of -SCALELISTEDIT: every scale in no use taken out, and the default scales the list lacks added after those
        /// kept, the metric or the imperial ones as the drawing's MEASUREMENT asks (<see cref="DefaultScales"/>).
        /// </summary>
        private static void ResetScales(CadEditScope s)
        {
            DxfObjectContextCollection list = ScaleList(s.Database);

            foreach (DxfAnnotationScale scale in Scales(s.Database).Where(x => Removable(s.Database, x.Name)).ToList())
            {
                list.RemoveContext(scale.Name);
            }

            foreach (CadScaleListRow scale in DefaultScales(s.Database))
            {
                if (!list.HasContext(scale.Name))
                {
                    list.AddContext(
                        new DxfAnnotationScale
                        {
                            Name = scale.Name,
                            PaperUnits = scale.PaperUnits,
                            DrawingUnits = scale.DrawingUnits,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// The default list of scales Reset puts back, as AutoCAD 2021's console resets it: the 17 metric scales 1:1 to 100:1,
        /// or, where the drawing's header has MEASUREMENT 0 (imperial), 1:1 and the 16 imperial ones from 1/128" = 1'-0" (0.0078125 to
        /// 12) to 1'-0" = 1'-0" (12 to 12). A drawing with no MEASUREMENT in its header takes the imperial list, as AutoCAD 2021
        /// reads such a header as MEASUREMENT 0.
        /// </summary>
        internal static List<CadScaleListRow> DefaultScales(DxfDatabase database)
        {
            var scales = new List<CadScaleListRow>();

            if (
                database != null
                && (database.GetHeader("$MEASUREMENT") == null || database.Measurement == DxfMeasurementValue.English)
            )
            {
                string[] inches =
                {
                    "1/128",
                    "1/64",
                    "1/32",
                    "1/16",
                    "3/32",
                    "1/8",
                    "3/16",
                    "1/4",
                    "3/8",
                    "1/2",
                    "3/4",
                    "1",
                    "1-1/2",
                    "3",
                    "6",
                };
                double[] paper =
                {
                    0.0078125,
                    0.015625,
                    0.03125,
                    0.0625,
                    0.09375,
                    0.125,
                    0.1875,
                    0.25,
                    0.375,
                    0.5,
                    0.75,
                    1,
                    1.5,
                    3,
                    6,
                };

                // 1:1 first, as AutoCAD's list after Reset has it, then the 16 imperial scales: 17 in all.
                scales.Add(new CadScaleListRow("1:1", 1, 1));

                for (int i = 0; i < inches.Length; i++)
                {
                    scales.Add(new CadScaleListRow(inches[i] + "\" = 1'-0\"", paper[i], 12));
                }

                scales.Add(new CadScaleListRow("1'-0\" = 1'-0\"", 12, 12));
                return scales;
            }

            foreach (string name in CadViewports.StandardScales)
            {
                string[] parts = name.Split(':');
                scales.Add(
                    new CadScaleListRow(
                        name,
                        double.Parse(parts[0], CultureInfo.InvariantCulture),
                        double.Parse(parts[1], CultureInfo.InvariantCulture)
                    )
                );
            }

            return scales;
        }

        /// <summary>
        /// SCALELISTEDIT: AutoCAD's Edit Drawing Scales, where the window shows it; OK writes the list as the dialog left it as
        /// one undo step (<see cref="WriteScaleList"/>). With no window, -SCALELISTEDIT's prompts, as AutoCAD's console asks them.
        /// </summary>
        private static IEnumerable<CadPrompt> ScaleListDialog(CadCommandContext c)
        {
            var request = new CadScaleListRequest(c.Document, ScaleRows(c.Database), DefaultScales(c.Database));

            if (!c.Session.Request("SCALELISTEDIT", c.Document, request))
            {
                foreach (CadPrompt prompt in ScaleListEdit(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (request.Action != CadDialogAction.Ok || !ScaleListChanged(c.Database, request.Scales))
            {
                yield break;
            }

            CadEditResult result = c.Apply(ScaleListRecord("Edit Drawing Scales"), s =>
                WriteScaleList(s.Database, request.Scales)
            );

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>The drawing's scales as the dialog lists them, each with whether the drawing keeps it and whether an object points at it.</summary>
        internal static List<CadScaleListRow> ScaleRows(DxfDatabase database)
        {
            var rows = new List<CadScaleListRow>();

            foreach (DxfAnnotationScale scale in Scales(database))
            {
                bool inUse;

                try
                {
                    inUse = database.File.CountUses().OfHandle(scale.Handle) > 0;
                }
                catch (Exception exception) when (exception is InvalidOperationException)
                {
                    inUse = true;
                }

                rows.Add(
                    new CadScaleListRow(
                        scale.Name,
                        scale.PaperUnits,
                        scale.DrawingUnits,
                        scale.Name,
                        !Removable(database, scale.Name),
                        inUse
                    )
                );
            }

            return rows;
        }

        /// <summary>Whether the list the dialog left differs from the drawing's: a scale taken out, added, renamed, given other units or moved.</summary>
        private static bool ScaleListChanged(DxfDatabase database, IReadOnlyList<CadScaleListRow> wanted)
        {
            List<DxfAnnotationScale> now = Scales(database);

            if (wanted == null || now.Count != wanted.Count)
            {
                return true;
            }

            for (int i = 0; i < now.Count; i++)
            {
                CadScaleListRow row = wanted[i];

                if (
                    !string.Equals(row.Original, now[i].Name, StringComparison.Ordinal)
                    || !string.Equals(row.Name, now[i].Name, StringComparison.Ordinal)
                    || BitConverter.DoubleToInt64Bits(row.PaperUnits)
                        != BitConverter.DoubleToInt64Bits(now[i].PaperUnits)
                    || BitConverter.DoubleToInt64Bits(row.DrawingUnits)
                        != BitConverter.DoubleToInt64Bits(now[i].DrawingUnits)
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The list of Edit Drawing Scales written into the drawing: the scales it no longer has taken out where the drawing
        /// lets them go, those it renamed given their names (through names of their own first, so that 1:2 and 1:4 may swap
        /// names), each given its units where nothing points at it, the new ones added, and every one moved to its place.
        /// </summary>
        internal static void WriteScaleList(DxfDatabase database, IReadOnlyList<CadScaleListRow> wanted)
        {
            DxfObjectContextCollection list = ScaleList(database);
            var kept = new HashSet<string>(
                wanted.Where(r => r.Original != null).Select(r => r.Original),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (DxfAnnotationScale gone in Scales(database).Where(s => !kept.Contains(s.Name)).ToList())
            {
                if (Removable(database, gone.Name))
                {
                    list.RemoveContext(gone.Name);
                }
            }

            var renamed = wanted
                .Where(r =>
                    r.Original != null
                    && list.HasContext(r.Original)
                    && !string.Equals(r.Original, r.Name, StringComparison.Ordinal)
                )
                .ToList();
            var handles = new Dictionary<CadScaleListRow, DxfAnnotationScale>();

            for (int i = 0; i < renamed.Count; i++)
            {
                var scale = (DxfAnnotationScale)list.GetContext(renamed[i].Original);
                scale.Name = "GH scale being renamed " + i.ToString(CultureInfo.InvariantCulture);
                handles[renamed[i]] = scale;
            }

            foreach (CadScaleListRow row in wanted)
            {
                DxfAnnotationScale scale =
                    handles.TryGetValue(row, out DxfAnnotationScale held) ? held
                    : row.Original != null ? list.GetContext(row.Original) as DxfAnnotationScale
                    : null;

                if (scale == null)
                {
                    list.AddContext(
                        new DxfAnnotationScale
                        {
                            Name = row.Name,
                            PaperUnits = row.PaperUnits,
                            DrawingUnits = row.DrawingUnits,
                        }
                    );
                    continue;
                }

                scale.Name = row.Name;

                if (!row.IsInUse)
                {
                    scale.PaperUnits = row.PaperUnits;
                    scale.DrawingUnits = row.DrawingUnits;
                }
            }

            for (int i = 0; i < wanted.Count; i++)
            {
                if (list.HasContext(wanted[i].Name))
                {
                    list.MoveContext(wanted[i].Name, i);
                }
            }
        }

        /// <summary>Whether a scale may be taken out of the list: not the unit scale, not CANNOSCALE, and pointed at by nothing.</summary>
        private static bool Removable(DxfDatabase database, string name)
        {
            try
            {
                return ScaleList(database).CanRemoveContext(name);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A ratio of -SCALELISTEDIT, as 1:3 or 2.5:1: two numbers above nought about a colon.</summary>
        internal static bool TryReadRatio(string text, out double paper, out double drawing)
        {
            paper = 0.0;
            drawing = 0.0;
            string[] parts = (text ?? string.Empty).Trim().Split(':');

            return parts.Length == 2
                && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out paper)
                && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out drawing)
                && paper > 0.0
                && drawing > 0.0
                && !double.IsInfinity(paper)
                && !double.IsInfinity(drawing);
        }

        private static IReadOnlyList<CadEditRecord> ScaleListRecord(string what) =>
            new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "Scale list", null, what) };

        private static DxfObjectContextCollection ScaleList(DxfDatabase database) =>
            database.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES");

        private static List<DxfAnnotationScale> Scales(DxfDatabase database)
        {
            try
            {
                return ScaleList(database)?.OfType<DxfAnnotationScale>().ToList() ?? new List<DxfAnnotationScale>();
            }
            catch (Exception)
            {
                return new List<DxfAnnotationScale>();
            }
        }
    }
}
