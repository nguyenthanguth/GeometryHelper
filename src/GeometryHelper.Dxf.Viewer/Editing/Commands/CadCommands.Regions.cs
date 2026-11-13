using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// REGION, and the regions -BOUNDARY makes: the loops of the objects chosen, each made a region of ACIS data as AutoCAD 2021
    /// writes it (<see cref="DxfUnknownEntity.CreateRegion"/>).
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// REGION: "Select objects:", then every closed loop the objects make: a circle, a whole ellipse or a closed polyline
        /// alone, and lines, arcs, elliptical arcs and open polylines that join end to end round a loop, each loop a region of its
        /// own (a circle inside a rectangle is a second region, not a hole), "5 loops extracted." and "5 Regions created." as
        /// AutoCAD says it, the regions made as one undo step. Objects that close no loop, and a closed spline, which ACIS holds
        /// as a spline surface the presenter does not write, make none.
        /// </summary>
        private static IEnumerable<CadPrompt> Region(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            List<List<DxfCurve>> loops = RegionLoops(c.Document, c.Selected, c.Session.Tolerance, out int splines);
            c.Write(Count(loops.Count, "loop", "loops") + " extracted.");

            if (splines > 0)
            {
                c.Write(
                    Count(splines, "closed spline", "closed splines")
                        + (splines == 1 ? " was" : " were")
                        + " not made a region: the presenter does not write the spline surfaces of ACIS."
                );
            }

            int made = MakeRegions(
                c,
                loops.Select(l => new KeyValuePair<List<DxfCurve>, List<List<DxfCurve>>>(l, null))
            );
            c.Write(Count(made, "Region", "Regions") + " created.");
        }

        /// <summary>
        /// Makes a region of each loop, the loops of the holes of it given with it, on the current layer, as one undo step; says why
        /// where the drawing's version has no regions. How many were made.
        /// </summary>
        private static int MakeRegions(
            CadCommandContext c,
            IEnumerable<KeyValuePair<List<DxfCurve>, List<List<DxfCurve>>>> loops
        )
        {
            var shapes = new List<DxfRegionShape>();

            foreach (KeyValuePair<List<DxfCurve>, List<List<DxfCurve>>> loop in loops)
            {
                try
                {
                    shapes.Add(DxfRegionShape.Create(loop.Key, loop.Value, c.Session.Tolerance));
                }
                catch (ArgumentException)
                {
                    // A loop out of one plane, or one that does not close within the tolerance, makes no region.
                }
            }

            if (shapes.Count == 0)
            {
                return 0;
            }

            var regions = new List<DxfUnknownEntity>();

            try
            {
                foreach (DxfRegionShape shape in shapes)
                {
                    regions.Add(Current(c, DxfUnknownEntity.CreateRegion(shape, c.Database)));
                }
            }
            catch (NotSupportedException e)
            {
                c.Write(e.Message);
                return 0;
            }

            CadEditResult result = c.Apply(null, s =>
            {
                foreach (DxfUnknownEntity region in regions)
                {
                    s.Append(c.Space, region);
                }
            });

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                return 0;
            }

            return regions.Count;
        }

        /// <summary>
        /// The closed loops of the objects: each circle, whole ellipse and closed polyline alone, and each set of lines, arcs,
        /// elliptical arcs and open polylines whose ends meet two and two within the point tolerance round one loop; the curves of
        /// an area where three ends meet, or one end meets none, are not a loop. In the order the objects were chosen.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The handles of the objects.</param>
        /// <param name="tolerance">The tolerance: ends within its point tolerance meet.</param>
        /// <param name="splines">How many closed splines were passed over.</param>
        internal static List<List<DxfCurve>> RegionLoops(
            CadDocument document,
            IEnumerable<string> handles,
            Tolerance tolerance,
            out int splines
        )
        {
            var loops = new List<List<DxfCurve>>();
            var open = new List<DxfCurve>();
            splines = 0;

            foreach (string handle in handles)
            {
                switch (document.FindEntity(handle))
                {
                    case DxfCircle circle:
                        loops.Add(new List<DxfCurve> { circle });
                        break;
                    case DxfEllipse ellipse when ellipse.Closed:
                        loops.Add(new List<DxfCurve> { ellipse });
                        break;
                    case DxfPolyline closed when closed.Closed && closed.NumberOfVertices > 1:
                        loops.Add(new List<DxfCurve> { closed });
                        break;
                    case DxfPolyline2d old when old.Closed && old.PolyType == DxfPoly2dType.SimplePoly:
                        loops.Add(new List<DxfCurve> { old });
                        break;
                    case DxfSpline spline when spline.Closed:
                        splines++;
                        break;
                    case DxfLine line:
                        open.Add(line);
                        break;
                    case DxfArc arc:
                        open.Add(arc);
                        break;
                    case DxfEllipse piece:
                        open.Add(piece);
                        break;
                    case DxfPolyline chain when !chain.Closed && chain.NumberOfVertices > 1:
                        open.Add(chain);
                        break;
                }
            }

            loops.AddRange(Joined(open, tolerance.EqualPoint));
            return loops;
        }

        /// <summary>
        /// The sets of curves that make one closed loop: ends are gathered into points within <paramref name="tolerance"/> of one
        /// another, and a set of curves joined through those points is a loop where every point holds exactly two ends.
        /// </summary>
        private static List<List<DxfCurve>> Joined(List<DxfCurve> curves, double tolerance)
        {
            var loops = new List<List<DxfCurve>>();

            if (curves.Count == 0)
            {
                return loops;
            }

            double cell = Math.Max(tolerance, 1E-9);
            var grid = new Dictionary<(long, long, long), List<int>>();
            var points = new List<GeoPoint3>();
            var parent = new List<int>();
            int Find(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }

                return i;
            }

            int Node(GeoPoint3 p)
            {
                long cx = (long)Math.Floor(p.X / cell);
                long cy = (long)Math.Floor(p.Y / cell);
                long cz = (long)Math.Floor(p.Z / cell);

                for (long x = cx - 1; x <= cx + 1; x++)
                {
                    for (long y = cy - 1; y <= cy + 1; y++)
                    {
                        for (long z = cz - 1; z <= cz + 1; z++)
                        {
                            if (grid.TryGetValue((x, y, z), out List<int> near))
                            {
                                foreach (int n in near)
                                {
                                    if (points[n].DistanceTo(p) <= tolerance)
                                    {
                                        return n;
                                    }
                                }
                            }
                        }
                    }
                }

                int made = points.Count;
                points.Add(p);
                parent.Add(made);

                if (!grid.TryGetValue((cx, cy, cz), out List<int> own))
                {
                    own = new List<int>();
                    grid.Add((cx, cy, cz), own);
                }

                own.Add(made);
                return made;
            }

            var ends = new List<(int First, int Last)>();

            foreach (DxfCurve curve in curves)
            {
                ends.Add((Node(curve.StartPoint), Node(curve.EndPoint)));
            }

            // Curves are joined through their points; a point holds the count of the ends that meet at it.
            var degree = new int[points.Count];

            foreach ((int first, int last) in ends)
            {
                degree[first]++;
                degree[last]++;
                parent[Find(first)] = Find(last);
            }

            var sets = new Dictionary<int, List<int>>();

            for (int i = 0; i < curves.Count; i++)
            {
                int root = Find(ends[i].First);

                if (!sets.TryGetValue(root, out List<int> members))
                {
                    members = new List<int>();
                    sets.Add(root, members);
                }

                members.Add(i);
            }

            foreach (List<int> members in sets.Values.OrderBy(m => m[0]))
            {
                if (members.All(i => degree[ends[i].First] == 2 && degree[ends[i].Last] == 2))
                {
                    loops.Add(members.Select(i => curves[i]).ToList());
                }
            }

            return loops;
        }
    }
}
