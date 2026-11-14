using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfHatch
    {
        /// <summary>
        /// Gets the SOLIDs and LINEs a HATCH filled solid or with a gradient is written as in R12, as AutoCAD 2021 writes one saved
        /// as R12: each filled loop, less the loops within it that are holes, cut into horizontal trapezoids (see
        /// <see cref="DxfHatchStrips"/>), each trapezoid two triangles in a plane turned over (the normal (0, 0, -1), x negated),
        /// the third corner written again as the fourth, with the look of the HATCH. A rectangle 100 by 60 gives two SOLIDs and
        /// the two LINEs of its sides; the LINEs are the non-horizontal sides of the trapezoids. A gradient is the one colour of
        /// the HATCH, with no lines, not the bands AutoCAD makes of it. A HATCH whose normal is not (0, 0, 1) is cut in its own plane, the LINEs in the world and
        /// the SOLIDs' corners as the plane of the opposite normal reads the world points (a rectangle 100 by 60 on the normal (1, 1, 1)
        /// has its SOLIDs on the extrusion (-0.577, -0.577, -0.577)). A bulge, an arc and an ellipse are cut into the chords
        /// <see cref="Edge.Chords(double, double)"/> counts.
        /// </summary>
        /// <returns>The SOLIDs, then the LINEs, in no file.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a loop has an edge that is no shape.</exception>
        internal IReadOnlyList<DxfEntity> FillSolids()
        {
            Snapshot snapshot = Measured(Snap());
            var solids = new List<DxfEntity>();
            var lines = new List<DxfEntity>();
            GeoVector3 normal = snapshot.Normal;
            bool flat = Math.Abs(normal.X) <= 1E-9 && Math.Abs(normal.Y) <= 1E-9 && normal.Z > 0.0;

            int count = snapshot.Loops.Count;
            double tolerance = Tolerance.Default.EqualPoint;
            var depths = new int[count];

            var outlines = new IReadOnlyList<GeoPoint2>[count];

            for (int i = 0; i < count; i++)
            {
                depths[i] = Depth(snapshot, i, tolerance);

                // A whole circle is cut as AutoCAD cuts it for a fill: into the chords of its radius.
                outlines[i] = LoopData.Read(snapshot.Loops[i]).Outline(true);
            }

            DxfHatchStyle style = HatchStyle;
            bool edges = !IsGradient;
            var look = new Look(this);
            double elevation = snapshot.Elevation;
            GeoVector3 turned = flat ? new GeoVector3(0.0, 0.0, -1.0) : normal.Multiply(-1.0);
            var trapezoids = new List<DxfHatchStrips.Trapezoid>();

            for (int i = 0; i < count; i++)
            {
                if (style == DxfHatchStyle.Normal ? depths[i] % 2 != 0 : depths[i] != 0)
                {
                    continue;
                }

                var region = new List<IReadOnlyList<GeoPoint2>> { outlines[i] };

                for (int j = 0; j < count; j++)
                {
                    if (
                        depths[j] == depths[i] + 1
                        && (style == DxfHatchStyle.Normal || (style == DxfHatchStyle.Outer && depths[j] == 1))
                        && Within(snapshot.Outlines[j], snapshot.Outlines[i], tolerance)
                    )
                    {
                        region.Add(outlines[j]);
                    }
                }

                trapezoids.AddRange(DxfHatchStrips.Cut(region, Tolerance.Default));
            }

            trapezoids.Sort(DxfHatchStrips.Compare);

            foreach (DxfHatchStrips.Trapezoid trapezoid in trapezoids)
            {
                if (!trapezoid.BottomPoint)
                {
                    solids.Add(
                        Triangle(
                            look,
                            trapezoid.TopLeft,
                            trapezoid.BottomRight,
                            trapezoid.BottomLeft,
                            elevation,
                            turned,
                            normal,
                            flat
                        )
                    );
                }

                if (!trapezoid.TopPoint)
                {
                    solids.Add(
                        Triangle(
                            look,
                            trapezoid.TopLeft,
                            trapezoid.TopRight,
                            trapezoid.BottomRight,
                            elevation,
                            turned,
                            normal,
                            flat
                        )
                    );
                }

                if (!edges)
                {
                    continue;
                }

                // The left side goes up and the right side down; of a triangle the right is written first.
                bool rightFirst = trapezoid.TopPoint || trapezoid.BottomPoint;

                for (int side = 0; side < 2; side++)
                {
                    bool right = (side == 0) == rightFirst;

                    if (right ? trapezoid.RightPointed : trapezoid.LeftPointed)
                    {
                        continue;
                    }

                    GeoPoint2 from = right ? trapezoid.TopRight : trapezoid.BottomLeft;
                    GeoPoint2 to = right ? trapezoid.BottomRight : trapezoid.TopLeft;
                    var line = new DxfLine(
                        flat
                            ? new GeoPoint3(from.X, from.Y, elevation)
                            : OcsToWorld(normal, new GeoPoint3(from.X, from.Y, elevation)),
                        flat
                            ? new GeoPoint3(to.X, to.Y, elevation)
                            : OcsToWorld(normal, new GeoPoint3(to.X, to.Y, elevation))
                    );
                    look.GiveTo(line);
                    lines.Add(line);
                }
            }

            solids.AddRange(lines);
            return solids.AsReadOnly();
        }

        /// <summary>A SOLID of three corners in the plane turned over, as AutoCAD writes the triangles of a fill: x negated and the height too, so that it stands at the elevation of the HATCH.</summary>
        private static DxfSolid Triangle(
            Look look,
            GeoPoint2 a,
            GeoPoint2 b,
            GeoPoint2 c,
            double elevation,
            GeoVector3 turned,
            GeoVector3 normal,
            bool flat
        )
        {
            // In a plane other than the drawing's the corners are the world points of the HATCH's plane, in the plane of the
            // normal turned the other way.
            var solid = flat
                ? new DxfSolid(
                    new GeoPoint3(-a.X, a.Y, -elevation),
                    new GeoPoint3(-b.X, b.Y, -elevation),
                    new GeoPoint3(-c.X, c.Y, -elevation)
                )
                : new DxfSolid(
                    WorldToOcs(turned, OcsToWorld(normal, new GeoPoint3(a.X, a.Y, elevation))),
                    WorldToOcs(turned, OcsToWorld(normal, new GeoPoint3(b.X, b.Y, elevation))),
                    WorldToOcs(turned, OcsToWorld(normal, new GeoPoint3(c.X, c.Y, elevation)))
                );

            // AutoCAD writes the extrusion on a SOLID of a HATCH in a plane other than the drawing's even where it comes to (0, 0, 1),
            // the plane of a HATCH turned the other way up: set from another direction first, so that the groups are written.
            if (!flat && Math.Abs(turned.X) <= 1E-9 && Math.Abs(turned.Y) <= 1E-9)
            {
                solid.Normal = new GeoVector3(0.0, 0.0, -turned.Z);
            }

            solid.Normal = turned;
            look.GiveTo(solid);
            return solid;
        }
    }
}
