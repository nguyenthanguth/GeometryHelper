using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A body of thousands of triangles meeting at one corner is checked in time that grows with its faces, and finds what
    /// it found before.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The edges were matched by a sweep along an axis, each against every other that overlaps it there, and the edges of a
    /// cone running to its apex overlap each other along every axis though none lies along another: a cone of 16 000 sides
    /// took 44 s to validate under .NET Framework 4.8 (6.3 s under .NET 8), one of 4 000 2.6 s, and with its base left out
    /// 37 s to say it was open. Now the edges that
    /// lie along one line are found from the points near each, and the body of 16 000 sides takes 0.14 s. The limits here
    /// are far above that, to stand a slow machine, and far below what the sweep took.
    /// </para>
    /// <para>
    /// The cones are 1 000 across the base and 1 500 high, their rims cut with no sine or cosine so that no two runtimes
    /// differ in a bit.
    /// </para>
    /// </remarks>
    public class ValidateFanTests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        /// <summary>
        /// The faces of a cone: its base, wound downwards, then each side from one corner of the rim to the next and up to the
        /// apex. The rim has four times <paramref name="perQuarter"/> corners, round a circle of 1 000.
        /// </summary>
        private static List<GeoFace3> ConeFaces(int perQuarter)
        {
            var rim = new List<GeoPoint3>();

            for (int quarter = 0; quarter < 4; quarter++)
            {
                for (int k = 0; k < perQuarter; k++)
                {
                    double t = (double)k / perQuarter;
                    double x = 1000.0 * (1.0 - t * t) / (1.0 + t * t), y = 1000.0 * 2.0 * t / (1.0 + t * t);
                    rim.Add(quarter == 0 ? new GeoPoint3(x, y, 0.0) : quarter == 1 ? new GeoPoint3(-y, x, 0.0) : quarter == 2 ? new GeoPoint3(-x, -y, 0.0) : new GeoPoint3(y, -x, 0.0));
                }
            }

            var apex = new GeoPoint3(0.0, 0.0, 1500.0);
            var faces = new List<GeoFace3>();
            var base_ = new List<GeoPoint3>(rim);
            base_.Reverse();
            faces.Add(new GeoFace3(new GeoPolygon3(base_, Within), null, Within));

            for (int i = 0; i < rim.Count; i++)
            {
                faces.Add(new GeoFace3(new GeoPolygon3(new[] { rim[i], rim[(i + 1) % rim.Count], apex }, Within), null, Within));
            }

            return faces;
        }

        private static (SolidValidation3 Validation, TimeSpan Time) Check(IEnumerable<GeoFace3> faces)
        {
            var solid = new GeoSolid3(faces);
            var clock = Stopwatch.StartNew();
            SolidValidation3 validation = solid.Validate(Within);
            return (validation, clock.Elapsed);
        }

        [Fact]
        public void ACone_OfSixteenThousandSides_IsValidInUnderASecond()
        {
            List<GeoFace3> faces = ConeFaces(4000);

            (SolidValidation3 validation, TimeSpan time) = Check(faces);

            Assert.Equal(16001, faces.Count);
            Assert.True(time < TimeSpan.FromSeconds(1), $"{time.TotalSeconds:F2} s");
            Assert.True(validation.IsValid);
            Assert.True(validation.IsClosed);
            Assert.True(validation.IsWoundAlike);
            Assert.Empty(validation.Issues);

            // A third of the base times the height; the base of 16 000 corners is 0.0000000000001 short of the circle's.
            Assert.InRange(validation.SignedVolume, faces[0].Area * 500.0 - 1.0, faces[0].Area * 500.0 + 1.0);
        }

        [Fact]
        public void ACone_WithItsBaseLeftOut_IsOpenAlongEveryEdgeOfTheRim()
        {
            List<GeoFace3> sides = ConeFaces(4000).Skip(1).ToList();

            (SolidValidation3 validation, TimeSpan time) = Check(sides);
            var solid = new GeoSolid3(sides);
            var clock = Stopwatch.StartNew();
            bool closed = solid.IsClosed(Within);
            TimeSpan closedTime = clock.Elapsed;

            Assert.True(time < TimeSpan.FromSeconds(1), $"validating {time.TotalSeconds:F2} s");
            Assert.True(closedTime < TimeSpan.FromSeconds(1), $"asking {closedTime.TotalSeconds:F2} s");
            Assert.False(closed);
            Assert.False(validation.IsClosed);
            Assert.Equal(16000, validation.Issues.Count);
            Assert.All(validation.Issues, issue => Assert.Equal(SolidIssueKind.OpenEdge, issue.Kind));

            // Each side is open along its own edge of the rim, and none along any other.
            Assert.Equal(Enumerable.Range(0, sides.Count), validation.Issues.Select(issue => issue.Faces.Single()).OrderBy(face => face));
        }

        [Fact]
        public void ASideTurnedTheWrongWay_IsTheSameWayOnAllThreeOfItsEdges()
        {
            List<GeoFace3> faces = ConeFaces(500);
            faces[1234] = faces[1234].Flip();

            SolidValidation3 validation = Check(faces).Validation;

            Assert.True(validation.IsClosed);
            Assert.False(validation.IsWoundAlike);
            Assert.Equal(3, validation.Issues.Count);
            Assert.All(validation.Issues, issue => Assert.Equal(SolidIssueKind.SameWayEdge, issue.Kind));
            Assert.All(validation.Issues, issue => Assert.Contains(1234, issue.Faces));
        }

        [Fact]
        public void ASideGivenTwice_IsOpenOnAllThreeOfItsEdges()
        {
            List<GeoFace3> faces = ConeFaces(500);
            faces.Add(faces[777]);

            SolidValidation3 validation = Check(faces).Validation;

            Assert.False(validation.IsClosed);
            Assert.Equal(3, validation.Issues.Count);
            Assert.All(validation.Issues, issue => Assert.Equal(SolidIssueKind.OpenEdge, issue.Kind));
            Assert.All(validation.Issues, issue => Assert.Equal(3, issue.Faces.Count));
            Assert.All(validation.Issues, issue => Assert.Contains(777, issue.Faces));
            Assert.All(validation.Issues, issue => Assert.Contains(faces.Count - 1, issue.Faces));
        }

        [Fact]
        public void AnEdgeOfTheBase_CutInThreeBesideAnUncutEdgeOfASide_StillCloses()
        {
            // The first edge of the rim, from the first corner to the second, is a corner or two longer in the side than in the
            // base, which has two more points on it: a long edge beside three short ones.
            List<GeoFace3> faces = ConeFaces(500);
            List<GeoPoint3> rim = faces[0].Boundary.Vertices.Reverse().ToList();
            GeoPoint3 a = rim[0], b = rim[1];
            var cut = new List<GeoPoint3>
            {
                a,
                new GeoPoint3(a.X + (b.X - a.X) / 3.0, a.Y + (b.Y - a.Y) / 3.0, 0.0),
                new GeoPoint3(a.X + 2.0 * (b.X - a.X) / 3.0, a.Y + 2.0 * (b.Y - a.Y) / 3.0, 0.0),
            };
            cut.AddRange(rim.Skip(1));
            cut.Reverse();
            faces[0] = new GeoFace3(new GeoPolygon3(cut, Within), null, Within);

            SolidValidation3 validation = Check(faces).Validation;

            Assert.True(validation.IsClosed);
            Assert.True(validation.IsWoundAlike);
            Assert.Empty(validation.Issues);
            Assert.True(new GeoSolid3(faces).IsClosed(Within));
        }
    }
}
