using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Batch 3 of acad.pgp's 2D commands: SOLID, REGION, HATCHTOBACK, GRADIENT and CLASSICINSERT here, -BOUNDARY in
    /// CadCommands.BoundaryTyped.cs, -HATCH in CadCommands.HatchTyped.cs and SPLINEDIT in CadCommands.SplineEdit.cs.
    /// </summary>
    internal static partial class CadCommands
    {
        internal static void AddShapesBatchTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("SOLID", Solid));
            registry.Add(new CadCommand("-BOUNDARY", BoundaryTyped));
            registry.Add(new CadCommand("-HATCH", HatchTyped));
            registry.Add(new CadCommand("GRADIENT", Gradient));
            registry.Add(new CadCommand("HATCHTOBACK", HatchToBack));
            registry.Add(new CadCommand("REGION", Region, true));
            registry.Add(new CadCommand("SPLINEDIT", SplineEdit));
            registry.Add(new CadCommand("CLASSICINSERT", Insert));
        }

        /// <summary>
        /// SOLID: "Specify first point:", second, third and "Specify fourth point or &lt;exit&gt;:", the corners kept in the
        /// order they are picked (the area runs 1, 2, 4, 3 round, so 1 and 2 are one side and 3 and 4 the other); Enter at
        /// the fourth makes a triangle of the third corner twice. The command goes on with "Specify third point:", the third
        /// and fourth corners of the last solid the first and second of the next, until Enter. Each solid is made as it is
        /// completed, the whole command one undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> Solid(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify first point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 first = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify second point", first);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 second = c.Input.Point;

            while (true)
            {
                GeoPoint2 one = first;
                GeoPoint2 two = second;
                yield return CadPrompt
                    .ForPoint("Specify third point", two)
                    .Previewing(CadPreview.MakingOne(c, input => SolidOf(c, one, two, input.Point, input.Point)));

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 third = c.Input.Point;
                yield return CadPrompt
                    .ForPoint("Specify fourth point or <exit>", third)
                    .Previewing(CadPreview.MakingOne(c, input => SolidOf(c, one, two, third, input.Point)));

                // Enter is a triangle: the third corner is the fourth too.
                GeoPoint2 fourth = c.Input.IsPoint ? c.Input.Point : third;
                c.Apply(null, s => s.Append(c.Space, SolidOf(c, one, two, third, fourth)));
                first = third;
                second = fourth;
            }
        }

        private static DxfSolid SolidOf(
            CadCommandContext c,
            GeoPoint2 a,
            GeoPoint2 b,
            GeoPoint2 third,
            GeoPoint2 fourth
        ) => Current(c, new DxfSolid(P3(a), P3(b), P3(third), P3(fourth)));

        /// <summary>
        /// GRADIENT: HATCH with the type set to a gradient, as AutoCAD's GRADIENT sets the Hatch Type of the ribbon: the same
        /// prompts, and the type stays a gradient for the HATCH that follows, as the ribbon's does.
        /// </summary>
        private static IEnumerable<CadPrompt> Gradient(CadCommandContext c)
        {
            if (c.Document.HatchSettings.Type != CadHatchType.Gradient)
            {
                CadHatchSettings settings = c.Document.HatchSettings.Clone();
                settings.Type = CadHatchType.Gradient;
                c.Document.HatchSettings = settings;
            }

            foreach (CadPrompt prompt in Hatch(c))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// HATCHTOBACK: every hatch of the current space sent behind the other objects, keeping their order among themselves,
        /// as one undo step; "1 hatch object(s) sent to back." or "No hatch objects found.".
        /// </summary>
        private static IEnumerable<CadPrompt> HatchToBack(CadCommandContext c)
        {
            List<DxfEntity> hatches = c.Space.Block.OfType<DxfHatch>().Cast<DxfEntity>().ToList();

            if (hatches.Count == 0)
            {
                c.Write("No hatch objects found.");
                yield break;
            }

            CadEditResult result = c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Draw order", null, "Back") },
                s => c.Space.Block.CreateDrawOrderTable().MoveToBottom(hatches)
            );

            if (result.Succeeded)
            {
                c.Write(
                    hatches.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + " hatch object(s) sent to back."
                );
            }
        }

        /// <summary>
        /// REGION: "Select objects:", the closed objects among them (lines, arcs, polylines, circles, ellipses and splines that
        /// close a loop) counted as AutoCAD counts them, "2 loops extracted.". A region is ACIS data, which the presenter
        /// cannot write yet, so no region is made, and it says so: "0 Regions created." where nothing closes, the sentence
        /// of the ACIS limit where something does.
        /// </summary>
        private static IEnumerable<CadPrompt> Region(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            IReadOnlyList<CadBoundaryLoop> loops = CadBoundaryFinder.FromObjects(
                c.Document,
                c.Selected,
                c.Session.Tolerance,
                out int _
            );
            c.Write(Count(loops.Count, "loop", "loops") + " extracted.");

            if (loops.Count == 0)
            {
                c.Write("0 Regions created.");
            }
            else
            {
                c.Write(RegionNotWritten + " 0 Regions created.");
            }
        }

        /// <summary>Why a region is not made: it is ACIS data.</summary>
        private const string RegionNotWritten = "A region is ACIS data, which the presenter cannot write yet.";
    }
}
