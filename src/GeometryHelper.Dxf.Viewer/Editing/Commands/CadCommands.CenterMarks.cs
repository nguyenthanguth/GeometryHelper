using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// CENTERMARK and CENTERLINE of AutoCAD 2021, and the variables that size them (CENTEREXE, CENTERMARKEXE, CENTERLTYPE,
    /// CENTERLTSCALE, CENTERCROSSSIZE, CENTERCROSSGAP, CENTERLAYER).
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddCenterMarksTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("CENTERMARK", CenterMark));
            registry.Add(new CadCommand("CENTERLINE", CenterLine));
            registry.AddVariable(
                CadSystemVariable.Number(
                    "CENTEREXE",
                    true,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => s.Document.Database.CenterExe,
                    (c, v) => c.Database.CenterExe = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "CENTERMARKEXE",
                    true,
                    0,
                    1,
                    s => s.Document.Database.CenterMarkExe ? 1 : 0,
                    (c, v) => c.Database.CenterMarkExe = v != 0
                )
            );
            registry.AddVariable(
                CadSystemVariable.Text(
                    "CENTERLTYPE",
                    true,
                    s => s.Document.Database.CenterLtype,
                    (c, typed) =>
                        CenterLinetypeKnown(c.Database, typed)
                            ? SetCenter(c, "CENTERLTYPE", c.Database.CenterLtype, typed, (d, v) => d.CenterLtype = v)
                            : "Cannot set CENTERLTYPE to that value."
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "CENTERLTSCALE",
                    true,
                    CadSystemVariable.RealBound.Positive,
                    false,
                    s => s.Document.Database.CenterLtScale,
                    (c, v) => c.Database.CenterLtScale = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Text(
                    "CENTERCROSSSIZE",
                    true,
                    s => s.Document.Database.CenterCrossSize,
                    (c, typed) =>
                        DxfDatabase.IsCenterExpression(typed)
                            ? SetCenter(c, "CENTERCROSSSIZE", c.Database.CenterCrossSize, typed.Trim(), (d, v) =>
                                d.CenterCrossSize = v
                            )
                            : "Cannot set CENTERCROSSSIZE to that value."
                )
            );
            registry.AddVariable(
                CadSystemVariable.Text(
                    "CENTERCROSSGAP",
                    true,
                    s => s.Document.Database.CenterCrossGap,
                    (c, typed) =>
                        DxfDatabase.IsCenterExpression(typed)
                            ? SetCenter(c, "CENTERCROSSGAP", c.Database.CenterCrossGap, typed.Trim(), (d, v) =>
                                d.CenterCrossGap = v
                            )
                            : "Cannot set CENTERCROSSGAP to that value."
                )
            );
            registry.AddVariable(
                CadSystemVariable.Text(
                    "CENTERLAYER",
                    true,
                    s => s.Document.Database.CenterLayer,
                    (c, typed) =>
                        typed == "." || CadLayerEditor.IsLayerName(typed)
                            ? SetCenter(c, "CENTERLAYER", c.Database.CenterLayer, typed, (d, v) => d.CenterLayer = v)
                            : "Cannot set CENTERLAYER to that value."
                )
            );
        }

        /// <summary>Whether a linetype can be the one of the center marks: one the drawing has, or one of the presenter's own set it loads when a mark is drawn.</summary>
        private static bool CenterLinetypeKnown(DxfDatabase database, string name) =>
            name.Trim().Length > 0
            && (
                database.LinetypeTable.Has(name.Trim())
                || CadLinetypeFile.BuiltIn.Any(l =>
                    string.Equals(l.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)
                )
            );

        /// <summary>Sets one of the center variables as one undo step; null, as a variable taken.</summary>
        private static string SetCenter(
            CadCommandContext c,
            string name,
            string old,
            string value,
            Action<DxfDatabase, string> set
        )
        {
            value = value.Trim();

            if (!string.Equals(old, value, StringComparison.Ordinal))
            {
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, name, old, value) }, s =>
                    set(s.Database, value)
                );
            }

            return null;
        }

        /// <summary>
        /// CENTERMARK: asks for a circle or an arc until Enter, and draws its center mark (an INSERT of an anonymous block, the
        /// cross and the lines of CENTEREXE, associated with the circle), one undo step each; an object of another kind is
        /// refused with AutoCAD 2021's "Object selected is not a circle or arc." and asked again.
        /// </summary>
        private static IEnumerable<CadPrompt> CenterMark(CadCommandContext c)
        {
            if (string.Equals(c.Database.Version, "AC1009", StringComparison.OrdinalIgnoreCase))
            {
                c.Write("A drawing of R12 cannot hold a center mark: save it in a later version first.");
                yield break;
            }

            while (true)
            {
                yield return CadPrompt.ForEntity("Select circle or arc to add center mark");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                DxfEntity entity = c.Document.FindEntity(c.Input.Handles[0]);

                if (!(entity is DxfCircle) && !(entity is DxfArc))
                {
                    c.Write("Object selected is not a circle or arc.");
                    continue;
                }

                CadSpace space = c.Space;
                c.Apply(null, s => CadCenterMarks.MakeMark(c, s, space, entity));
            }
        }

        /// <summary>
        /// CENTERLINE: asks for a first line and a second, a line or a straight segment of a polyline each, and draws the center
        /// line between them (an INSERT of an anonymous block with the one line, associated with both). What is no line is
        /// refused with "Object selected is not a line." and asked again, the first line picked again with "Invalid. Select
        /// another line.".
        /// </summary>
        private static IEnumerable<CadPrompt> CenterLine(CadCommandContext c)
        {
            if (string.Equals(c.Database.Version, "AC1009", StringComparison.OrdinalIgnoreCase))
            {
                c.Write("A drawing of R12 cannot hold a center line: save it in a later version first.");
                yield break;
            }

            CadCenterMarks.Segment one;

            while (true)
            {
                yield return CadPrompt.ForEntity("Select first line");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (CadCenterMarks.TryGetSegment(c.Document.FindEntity(c.Input.Handles[0]), c.Input.Point, out one))
                {
                    break;
                }

                c.Write("Object selected is not a line.");
            }

            while (true)
            {
                yield return CadPrompt.ForEntity("Select second line");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (
                    !CadCenterMarks.TryGetSegment(
                        c.Document.FindEntity(c.Input.Handles[0]),
                        c.Input.Point,
                        out CadCenterMarks.Segment two
                    )
                )
                {
                    c.Write("Object selected is not a line.");
                    continue;
                }

                if (CadCenterMarks.Same(one, two))
                {
                    c.Write("Invalid. Select another line.");
                    continue;
                }

                if (!CadCenterMarks.HasWayBetween(one, two))
                {
                    c.Write("Invalid. Select another line.");
                    continue;
                }

                CadSpace space = c.Space;
                CadCenterMarks.Segment first = one;
                c.Apply(null, s => CadCenterMarks.MakeLine(c, s, space, first, two));
                yield break;
            }
        }
    }
}
