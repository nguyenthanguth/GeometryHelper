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

                yield return CadPrompt.ForString("Enter name for new scale");
                string name = (c.Input.Text ?? string.Empty).Trim();

                if (name.Length == 0)
                {
                    continue;
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

        /// <summary>Reset of -SCALELISTEDIT: every scale in no use taken out, and the default scales the list lacks added after those kept.</summary>
        private static void ResetScales(CadEditScope s)
        {
            DxfObjectContextCollection list = ScaleList(s.Database);

            foreach (DxfAnnotationScale scale in Scales(s.Database).Where(x => Removable(s.Database, x.Name)).ToList())
            {
                list.RemoveContext(scale.Name);
            }

            foreach (string name in CadViewports.StandardScales)
            {
                if (!list.HasContext(name) && CadViewports.TryReadScale(name, out _))
                {
                    string[] parts = name.Split(':');
                    list.AddContext(
                        new DxfAnnotationScale
                        {
                            Name = name,
                            PaperUnits = double.Parse(parts[0], CultureInfo.InvariantCulture),
                            DrawingUnits = double.Parse(parts[1], CultureInfo.InvariantCulture),
                        }
                    );
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
