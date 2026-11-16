using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The few grips of a revision cloud while REVCLOUDGRIPS is on, as AutoCAD 2021's <c>GetGripPoints</c> gives them (probed
    /// in its console through a plugin): the grips of the shape the cloud goes round, not one for each vertex.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// How much longer the arcs of a cloud drawn again by a grip are than the mean chord of the cloud before: 1.1, found to
        /// the digit in 14 clouds (a cloud with arcs 10 long is redrawn with 11; one of 40 with 41.656854, 1.1 times its mean
        /// chord of 37.869867).
        /// </summary>
        private const double RevCloudRegrowth = 1.1;

        /// <summary>The shape a cloud goes round, as its few grips see it.</summary>
        internal sealed class RevCloudShape
        {
            /// <summary>Gets or sets whether every vertex of the cloud lies on one circle.</summary>
            public bool IsCircle { get; set; }

            /// <summary>Gets or sets the centre of the circle.</summary>
            public GeoPoint2 Center { get; set; }

            /// <summary>Gets or sets the radius of the circle.</summary>
            public double Radius { get; set; }

            /// <summary>Gets or sets the corners of the path round which the cloud goes, from its first vertex.</summary>
            public List<GeoPoint2> Corners { get; set; }
        }

        /// <summary>
        /// Gets whether a cloud shows its few grips: REVCLOUDGRIPS is on and the entity is a cloud of six vertices or more
        /// that goes round a shape (see <see cref="RevCloudShapeOf"/>).
        /// </summary>
        internal static bool RevCloudHasFewGrips(CadDocument document, DxfEntity entity) =>
            document.RevCloudGrips && RevCloudShapeOf(entity) != null;

        /// <summary>
        /// The shape a cloud goes round, null for what shows every vertex: a polyline that is no cloud, one that is open,
        /// below six vertices or lying below the plan. A cloud whose vertices lie on one circle is a circle. Any other is
        /// its path: the vertices where the direction changes, its first vertex always, and a corner the arcs cut off (a
        /// vertex before it and one after it, the chord between them leaving both lines of the sides) the point where the
        /// two lines meet; a cloud round a rectangle 60 by 40 with arcs 10 long has the four corners, and one round the
        /// polygon (100, 0), (160, 0), (160, 40), (130, 60), (100, 40) has those five, the corner (130, 60) among them,
        /// which is no vertex of it.
        /// </summary>
        internal static RevCloudShape RevCloudShapeOf(DxfEntity entity)
        {
            if (
                !(entity is DxfPolyline cloud)
                || !cloud.Closed
                || !RevCloudStored(cloud).HasValue
                || cloud.NumberOfVertices < RevCloudFewest
                || cloud.Normal.Z <= 0.0
                || Math.Abs(cloud.Normal.X) > 1E-12
                || Math.Abs(cloud.Normal.Y) > 1E-12
            )
            {
                return null;
            }

            int n = cloud.NumberOfVertices;
            var at = new GeoPoint2[n];

            for (int i = 0; i < n; i++)
            {
                at[i] = cloud.GetPoint2dAt(i);
            }

            if (RevCloudCircle(at, out GeoPoint2 center, out double radius))
            {
                return new RevCloudShape
                {
                    IsCircle = true,
                    Center = center,
                    Radius = radius,
                };
            }

            // The unit direction of each chord, from a vertex to the next.
            var way = new GeoVector2[n];

            for (int i = 0; i < n; i++)
            {
                GeoVector2 chord = at[(i + 1) % n] - at[i];
                double length = chord.Length;

                if (length < 1E-9)
                {
                    return null;
                }

                way[i] = new GeoVector2(chord.X / length, chord.Y / length);
            }

            bool Turns(int i)
            {
                GeoVector2 a = way[(i + n - 1) % n];
                GeoVector2 b = way[i];
                return Math.Abs(a.X * b.Y - a.Y * b.X) > 1E-6 || a.X * b.X + a.Y * b.Y < 0.0;
            }

            var turns = new bool[n];
            var gone = new bool[n];
            var corner = (GeoPoint2[])at.Clone();

            for (int i = 0; i < n; i++)
            {
                turns[i] = Turns(i);
            }

            // Two vertices that both turn, the chord between them leaving the lines of the chords before and after it: the
            // arcs cut off a corner there, and its point is where the two lines meet, ahead of the first and behind the
            // second, over the chord.
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;

                if (!turns[i] || !turns[j] || gone[i] || gone[j])
                {
                    continue;
                }

                GeoVector2 a = way[(i + n - 1) % n];
                GeoVector2 b = way[j];
                double cross = a.X * b.Y - a.Y * b.X;

                if (Math.Abs(cross) < 1E-3)
                {
                    continue;
                }

                GeoVector2 between = at[j] - at[i];
                double ahead = (between.X * b.Y - between.Y * b.X) / cross;
                GeoPoint2 meet = at[i] + a * ahead;
                GeoVector2 behind = meet - at[j];
                double along =
                    ((meet.X - at[i].X) * between.X + (meet.Y - at[i].Y) * between.Y) / between.LengthSquared;

                if (ahead <= 0.0 || behind.X * b.X + behind.Y * b.Y >= 0.0 || along < 0.0 || along > 1.0)
                {
                    continue;
                }

                // The meeting takes the place of the first vertex; at the end of the cloud it takes that of the first of all.
                int slot = j == 0 ? 0 : i;
                corner[slot] = meet;
                gone[j == 0 ? i : j] = true;
            }

            var corners = new List<GeoPoint2>();

            for (int i = 0; i < n; i++)
            {
                if (!gone[i] && (i == 0 || turns[i]))
                {
                    corners.Add(corner[i]);
                }
            }

            return corners.Count < 3 ? null : new RevCloudShape { Corners = corners };
        }

        /// <summary>Whether points lie on one circle, within a millionth of its radius, as a least squares circle through them.</summary>
        private static bool RevCloudCircle(GeoPoint2[] at, out GeoPoint2 center, out double radius)
        {
            center = default;
            radius = 0.0;
            double mx = at.Average(p => p.X);
            double my = at.Average(p => p.Y);
            double suu = 0.0;
            double suv = 0.0;
            double svv = 0.0;
            double right1 = 0.0;
            double right2 = 0.0;

            foreach (GeoPoint2 p in at)
            {
                double u = p.X - mx;
                double v = p.Y - my;
                suu += u * u;
                suv += u * v;
                svv += v * v;
                right1 += 0.5 * (u * u * u + u * v * v);
                right2 += 0.5 * (v * v * v + v * u * u);
            }

            double determinant = suu * svv - suv * suv;

            if (Math.Abs(determinant) < 1E-12 * (suu + svv) * (suu + svv))
            {
                return false;
            }

            double uc = (right1 * svv - right2 * suv) / determinant;
            double vc = (right2 * suu - right1 * suv) / determinant;
            var found = new GeoPoint2(mx + uc, my + vc);
            double mean = at.Average(p => (p - found).Length);
            center = found;
            radius = mean;

            return mean > 1E-9 && at.All(p => Math.Abs((p - found).Length - mean) <= 1E-6 * mean + 1E-9);
        }

        /// <summary>
        /// The few grips of a cloud, null where it shows every vertex. A circle has its centre, then its quadrants to the right,
        /// to the left, above and below, as AutoCAD's own circle does. Any other cloud has, for each corner of its path from
        /// the first, a vertex grip and one at the middle of the side that follows it: a cloud round the rectangle (0, 0) to
        /// (60, 40) has (0, 0), (0, 20), (0, 40), (30, 40), (60, 40), (60, 20), (60, 0) and (30, 0).
        /// </summary>
        internal static List<CadGrip> RevCloudFewGrips(DxfEntity entity)
        {
            RevCloudShape shape = RevCloudShapeOf(entity);

            if (shape == null)
            {
                return null;
            }

            var grips = new List<CadGrip>();

            void Add(CadGripKind kind, GeoPoint2 at) => grips.Add(new CadGrip(grips.Count, kind, at));

            if (shape.IsCircle)
            {
                GeoPoint2 c = shape.Center;
                double r = shape.Radius;
                Add(CadGripKind.Center, c);
                Add(CadGripKind.Quadrant, new GeoPoint2(c.X + r, c.Y));
                Add(CadGripKind.Quadrant, new GeoPoint2(c.X - r, c.Y));
                Add(CadGripKind.Quadrant, new GeoPoint2(c.X, c.Y + r));
                Add(CadGripKind.Quadrant, new GeoPoint2(c.X, c.Y - r));
                return grips;
            }

            int m = shape.Corners.Count;

            for (int i = 0; i < m; i++)
            {
                GeoPoint2 from = shape.Corners[i];
                GeoPoint2 to = shape.Corners[(i + 1) % m];
                GeoVector2 side = to - from;
                double length = side.Length;
                Add(CadGripKind.Vertex, from);
                grips.Add(
                    new CadGrip(
                        grips.Count,
                        CadGripKind.SegmentMiddle,
                        new GeoPoint2((from.X + to.X) / 2.0, (from.Y + to.Y) / 2.0),
                        length > 0.0 ? new GeoVector2(side.X / length, side.Y / length) : default
                    )
                );
            }

            return grips;
        }

        /// <summary>
        /// Drags a grip of a cloud: the shape it goes round is changed, a corner moved to the point, a side moved by as much as
        /// its middle was, the centre of a circle moved, a quadrant given the radius that takes it to the point, and the cloud
        /// is drawn again along the new shape from its first vertex, in place, as AutoCAD 2021 does it: the arcs a tenth
        /// longer than the mean chord of the cloud was, the last arc taking what is left, the arcs bulging the way they did.
        /// A cloud 60 by 40 with arcs 10 long, its top middle dragged up by 5, is 19 vertices 11 apart along its sides. With
        /// <paramref name="write"/> false nothing is written, and the answer is whether the cloud could be drawn.
        /// </summary>
        internal static bool RevCloudRegrow(DxfPolyline cloud, int gripIndex, GeoPoint2 to, bool write)
        {
            RevCloudShape shape = RevCloudShapeOf(cloud);

            if (shape == null)
            {
                return false;
            }

            List<CadGrip> grips = RevCloudFewGrips(cloud);

            if (gripIndex < 0 || gripIndex >= grips.Count)
            {
                return false;
            }

            GeoVector2 moved = to - grips[gripIndex].Point;
            DxfCurve path;

            if (shape.IsCircle)
            {
                GeoPoint2 center = gripIndex == 0 ? shape.Center + moved : shape.Center;
                double radius = gripIndex == 0 ? shape.Radius : (to - shape.Center).Length;

                if (radius < 1E-9)
                {
                    return false;
                }

                path = new DxfCircle(
                    new GeoPoint3(center.X, center.Y, cloud.Elevation),
                    new GeoVector3(0.0, 0.0, 1.0),
                    radius
                );
            }
            else
            {
                List<GeoPoint2> corners = shape.Corners.ToList();
                int m = corners.Count;

                if (gripIndex % 2 == 0)
                {
                    corners[gripIndex / 2] = to;
                }
                else
                {
                    corners[gripIndex / 2] += moved;
                    corners[(gripIndex / 2 + 1) % m] += moved;
                }

                var closed = new DxfPolyline();

                for (int i = 0; i < m; i++)
                {
                    closed.AddVertexAt(i, corners[i], 0.0, 0.0, 0.0);
                }

                closed.Closed = true;
                path = closed;
            }

            int n = cloud.NumberOfVertices;
            double chords = 0.0;

            for (int i = 0; i < n; i++)
            {
                chords += (cloud.GetPoint2dAt((i + 1) % n) - cloud.GetPoint2dAt(i)).Length;
            }

            double spacing = RevCloudRegrowth * chords / n;
            double width = cloud.GetEndWidthAt(0);
            double approximate = RevCloudStored(cloud).Value;
            var look = new RevCloudLook(spacing, spacing, false, approximate);
            List<GeoPoint2> along = RevCloudAlong(path, true, look);

            if (along.Count < 2)
            {
                return false;
            }

            if (write)
            {
                double bulge = RevCloudSide(cloud) * RevCloudBulge;
                List<PeditPoint> points = along
                    .Select(p => new PeditPoint
                    {
                        At = p,
                        Bulge = bulge,
                        Start = 0.0,
                        End = width,
                    })
                    .ToList();
                PeditWrite(cloud, points, true, DxfPoly2dType.SimplePoly);
                RevCloudMark(cloud, approximate);
            }

            return true;
        }
    }
}
