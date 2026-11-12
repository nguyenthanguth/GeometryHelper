using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// -BOUNDARY, the command-line BOUNDARY with its Advanced options; BOUNDARY itself (CadCommands.Hatch.cs) takes the same
    /// settings, as AutoCAD's dialog and -BOUNDARY share theirs.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// -BOUNDARY: "Specify internal point or [Advanced options]:", each point's area made as BOUNDARY makes it, until
        /// Enter; Advanced options asks "Enter an option [Island detection/Object type]:" until Enter. The settings are the
        /// session's, shared with BOUNDARY: island detection on and polylines as AutoCAD starts. The candidate set of
        /// AutoCAD's Boundary set (everything visible, or objects selected anew) is not built: the area is found among all
        /// the objects of the space.
        /// </summary>
        private static IEnumerable<CadPrompt> BoundaryTyped(CadCommandContext c) => BoundaryPoints(c, true);

        /// <summary>
        /// The points of BOUNDARY and -BOUNDARY: each area found round a point made closed polylines on the current layer, "BOUNDARY
        /// created 2 polylines", the islands left out where island detection is off. An object type of Region makes nothing: a
        /// region is ACIS data, which the presenter cannot write yet, and the command says so.
        /// </summary>
        private static IEnumerable<CadPrompt> BoundaryPoints(CadCommandContext c, bool typed)
        {
            while (true)
            {
                yield return typed
                    ? CadPrompt.ForPoint("Specify internal point", null, new[] { "Advanced options" })
                    : CadPrompt.ForPoint("Pick internal point");

                if (typed && c.Input.Is("Advanced options"))
                {
                    foreach (CadPrompt prompt in BoundaryAdvanced(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                if (typed)
                {
                    c.Write("Selecting everything visible...");
                    c.Write("Analyzing the selected data...");
                    c.Write("Analyzing internal islands...");
                }

                IReadOnlyList<CadBoundaryLoop> found = CadBoundaryFinder.Find(
                    c.Document,
                    c.Space,
                    c.Input.Point,
                    c.Session.Tolerance,
                    c.Session.WorkView?.VisibleBox
                );

                if (found == null)
                {
                    c.Write("Valid hatch boundary not found.");
                    continue;
                }

                if (c.Remembered(BoundaryRegionKey, 0.0) != 0.0)
                {
                    c.Write(RegionNotWritten + " No region was made: choose Polyline as the object type.");
                    continue;
                }

                List<CadBoundaryLoop> loops =
                    c.Remembered(BoundaryIslandsKey, 1.0) != 0.0
                        ? found.ToList()
                        : found.Where(l => l.Depth == 0).ToList();
                CadEditResult result = c.Apply(null, s =>
                {
                    foreach (CadBoundaryLoop loop in loops)
                    {
                        s.Append(c.Space, Current(c, LoopPolyline(loop)));
                    }
                });

                c.Write(
                    result.Succeeded
                        ? "BOUNDARY created " + Count(loops.Count, "polyline", "polylines")
                        : result.Message
                );
            }
        }

        private const string BoundaryIslandsKey = "boundary.islands";
        private const string BoundaryRegionKey = "boundary.region";

        /// <summary>Advanced options of -BOUNDARY: island detection and the object type, each asked as AutoCAD asks it, until Enter.</summary>
        private static IEnumerable<CadPrompt> BoundaryAdvanced(CadCommandContext c)
        {
            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter an option", new[] { "Island detection", "Object type" });

                if (c.Input.Is("Island detection"))
                {
                    bool islands = c.Remembered(BoundaryIslandsKey, 1.0) != 0.0;
                    yield return CadPrompt.ForKeyword(
                        "Do you want island detection?",
                        new[] { "Yes", "No" },
                        islands ? "Y" : "N"
                    );

                    if (c.Input.Is("Yes") || c.Input.Is("No"))
                    {
                        c.Remember(BoundaryIslandsKey, c.Input.Is("Yes") ? 1.0 : 0.0);
                    }
                }
                else if (c.Input.Is("Object type"))
                {
                    bool region = c.Remembered(BoundaryRegionKey, 0.0) != 0.0;
                    yield return CadPrompt.ForKeyword(
                        "Enter type of boundary object",
                        new[] { "Region", "Polyline" },
                        region ? "Region" : "Polyline"
                    );

                    if (c.Input.Is("Region") || c.Input.Is("Polyline"))
                    {
                        c.Remember(BoundaryRegionKey, c.Input.Is("Region") ? 1.0 : 0.0);
                    }
                }
                else
                {
                    yield break;
                }
            }
        }
    }
}
