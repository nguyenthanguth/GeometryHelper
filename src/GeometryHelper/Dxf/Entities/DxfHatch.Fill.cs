using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfHatch
    {
        /// <summary>
        /// Gets the SOLIDs a HATCH filled solid or with a gradient is written as in R12, as AutoCAD 2021 writes one saved as R12:
        /// each filled loop, less the loops within it that are holes, cut into triangles (the third corner written again as the
        /// fourth) with the look of the HATCH; a gradient is the one colour of the HATCH, not the bands AutoCAD makes of it. A
        /// HATCH whose normal is not (0, 0, 1) gives none, for a SOLID is made here in the world plane.
        /// </summary>
        /// <returns>The entities, in no file.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a loop has an edge that is no shape.</exception>
        internal IReadOnlyList<DxfEntity> FillSolids()
        {
            Snapshot snapshot = Measured(Snap());
            var parts = new List<DxfEntity>();
            GeoVector3 normal = snapshot.Normal;

            if (Math.Abs(normal.X) > 1E-9 || Math.Abs(normal.Y) > 1E-9 || normal.Z < 0.0)
            {
                return parts.AsReadOnly();
            }

            int count = snapshot.Loops.Count;
            double tolerance = Tolerance.Default.EqualPoint;
            var depths = new int[count];

            for (int i = 0; i < count; i++)
            {
                depths[i] = Depth(snapshot, i, tolerance);
            }

            DxfHatchStyle style = HatchStyle;
            var look = new Look(this);

            for (int i = 0; i < count; i++)
            {
                if (style == DxfHatchStyle.Normal ? depths[i] % 2 != 0 : depths[i] != 0)
                {
                    continue;
                }

                var holes = new List<GeoPolygon2>();

                for (int j = 0; j < count; j++)
                {
                    if (
                        depths[j] == depths[i] + 1
                        && (style == DxfHatchStyle.Normal || (style == DxfHatchStyle.Outer && depths[j] == 1))
                        && Within(snapshot.Outlines[j], snapshot.Outlines[i], tolerance)
                    )
                    {
                        holes.Add(new GeoPolygon2(snapshot.Outlines[j].Points));
                    }
                }

                GeoTriangle2[] triangles;

                try
                {
                    triangles = new GeoFace2(new GeoPolygon2(snapshot.Outlines[i].Points), holes).TriangulateSurface(
                        Tolerance.Default
                    );
                }
                catch (ArgumentException)
                {
                    // A loop of no area, which has nothing to fill.
                    continue;
                }

                foreach (GeoTriangle2 triangle in triangles)
                {
                    var solid = new DxfSolid(
                        new GeoPoint3(triangle.A.X, triangle.A.Y, snapshot.Elevation),
                        new GeoPoint3(triangle.B.X, triangle.B.Y, snapshot.Elevation),
                        new GeoPoint3(triangle.C.X, triangle.C.Y, snapshot.Elevation)
                    );
                    look.GiveTo(solid);
                    parts.Add(solid);
                }
            }

            return parts.AsReadOnly();
        }
    }
}
