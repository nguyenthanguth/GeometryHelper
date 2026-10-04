using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// What <see cref="GeoSolid3.Validate(Tolerance)"/> finds of the faces of a body: where they do not close, which way
    /// they run their edges, which way they are wound, and what is worth knowing.
    /// </summary>
    public class SolidValidationTests
    {
        private static GeoSolid3 Box(double x0, double y0, double z0, double x1, double y1, double z1)
            => new GeoAabb3(new GeoPoint3(x0, y0, z0), new GeoPoint3(x1, y1, z1)).ToObb().ToSolid();

        private static GeoFace3 Face(params GeoPoint3[] corners) => new GeoFace3(new GeoPolygon3(corners));

        private static readonly Tolerance Fine = new Tolerance(1E-4, 1E-6, Tolerance.DefaultEqualAngleRad, 1E-4);

        /// <summary>A face keeping corners closer than the default tolerance, which its polygon would make one.</summary>
        private static GeoFace3 FineFace(params GeoPoint3[] corners) => new GeoFace3(new GeoPolygon3(corners, Fine), null, Fine);

        private static IEnumerable<SolidIssue3> Of(SolidValidation3 check, SolidIssueKind kind) => check.Issues.Where(i => i.Kind == kind);

        [Fact]
        public void ABox_IsValid_WithNothingToReport()
        {
            SolidValidation3 check = Box(0, 0, 0, 10, 10, 10).Validate();

            Assert.True(check.IsValid);
            Assert.True(check.IsClosed);
            Assert.True(check.IsWoundAlike);
            Assert.Equal(1000.0, check.SignedVolume, 9);
            Assert.Empty(check.Issues);
            Assert.Equal("SolidValidation3[valid, volume 1000]", check.ToString());
        }

        [Fact]
        public void ABoxWithoutItsTop_IsOpenAlongTheRimOfTheGap()
        {
            GeoSolid3 box = Box(0, 0, 0, 10, 10, 10);
            int top = Enumerable.Range(0, box.Faces.Count).Single(f => box.Faces[f].Normal.Z > 0.5);
            var open = new GeoSolid3(box.Faces.Where((face, f) => f != top));

            SolidValidation3 check = open.Validate();

            Assert.False(open.IsClosed());
            Assert.False(check.IsClosed);
            Assert.False(check.IsValid);

            SolidIssue3[] rim = Of(check, SolidIssueKind.OpenEdge).ToArray();
            Assert.Equal(4, rim.Length);

            foreach (SolidIssue3 edge in rim)
            {
                Assert.Equal(10.0, edge.Size, 9);
                Assert.Equal(10.0, edge.Location.Z, 9);
                Assert.Single(edge.Faces);
                Assert.Equal(1, edge.Forward + edge.Backward);
            }
        }

        [Fact]
        public void ABoxWithOneFaceTurned_IsClosed_ButItsEdgesAreRunTheSameWay()
        {
            GeoSolid3 box = Box(0, 0, 0, 10, 10, 10);
            var turned = new GeoSolid3(box.Faces.Select((face, f) => f == 2 ? face.Flip() : face));

            SolidValidation3 check = turned.Validate();

            // Counting the faces on each edge, and not which way they run it, calls it closed.
            Assert.True(turned.IsClosed());
            Assert.True(check.IsClosed);
            Assert.False(check.IsWoundAlike);
            Assert.False(check.IsValid);

            SolidIssue3[] sameWay = Of(check, SolidIssueKind.SameWayEdge).ToArray();
            Assert.Equal(4, sameWay.Length);

            foreach (SolidIssue3 edge in sameWay)
            {
                Assert.Contains(2, edge.Faces);
                Assert.Equal(2, edge.Faces.Count);
                Assert.Equal(2, System.Math.Abs(edge.Forward - edge.Backward));
            }
        }

        [Fact]
        public void ABoxWoundInwards_IsInsideOut()
        {
            var inwards = new GeoSolid3(Box(0, 0, 0, 10, 10, 10).Faces.Select(face => face.Flip()));

            SolidValidation3 check = inwards.Validate();

            Assert.True(check.IsClosed);
            Assert.True(check.IsWoundAlike);
            Assert.False(check.IsValid);
            Assert.Equal(-1000.0, check.SignedVolume, 9);
            Assert.Equal(-1000.0, Assert.Single(check.Issues, i => i.Kind == SolidIssueKind.InsideOut).Size, 9);
            Assert.True(inwards.TurnOutwards().Validate().IsValid);
        }

        [Fact]
        public void TwoBlocksMeetingAlongAnEdge_AreValid_WithTheEdgeNoted()
        {
            var pair = new GeoSolid3(Box(0, 0, 0, 10, 10, 10).Faces.Concat(Box(10, 10, 0, 20, 20, 10).Faces));

            SolidValidation3 check = pair.Validate();

            Assert.True(check.IsValid);
            SolidIssue3 edge = Assert.Single(check.Issues);
            Assert.Equal(SolidIssueKind.NonManifoldEdge, edge.Kind);
            Assert.Equal(4, edge.Faces.Count);
            Assert.Equal(2, edge.Forward);
            Assert.Equal(2, edge.Backward);
            Assert.Equal(10.0, edge.Size, 9);
            Assert.True(edge.Location.IsEqualTo(new GeoPoint3(10, 10, 5)));
        }

        [Fact]
        public void ALongEdgeBesideTwoShortOnes_IsMatchedStretchByStretch()
        {
            // A box whose front face is two, side by side: the top's front edge runs beside both of theirs.
            var p = new[]
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0),
                new GeoPoint3(0, 0, 10), new GeoPoint3(10, 0, 10), new GeoPoint3(10, 10, 10), new GeoPoint3(0, 10, 10),
            };
            GeoPoint3 bottomMiddle = new GeoPoint3(4, 0, 0), topMiddle = new GeoPoint3(4, 0, 10);
            var faces = new[]
            {
                Face(p[0], p[3], p[2], p[1], bottomMiddle),
                Face(p[4], topMiddle, p[5], p[6], p[7]),
                Face(p[0], bottomMiddle, topMiddle, p[4]),
                Face(bottomMiddle, p[1], p[5], topMiddle),
                Face(p[1], p[2], p[6], p[5]),
                Face(p[2], p[3], p[7], p[6]),
                Face(p[3], p[0], p[4], p[7]),
            };

            SolidValidation3 check = new GeoSolid3(faces).Validate();

            Assert.True(check.IsValid);
            Assert.Empty(check.Issues);

            // The bottom and the top carry the corner of the split; with them as plain rectangles, the long edge each lays
            // against the two short ones is still matched stretch by stretch.
            faces[0] = Face(p[0], p[3], p[2], p[1]);
            faces[1] = Face(p[4], p[5], p[6], p[7]);

            Assert.Empty(new GeoSolid3(faces).Validate().Issues);
        }

        [Fact]
        public void APlateWithAHoleThrough_IsReadWithEachFaceOnTheLeftOfTheRimItRuns()
        {
            GeoSolid3 plate = Box(0, 0, 0, 100, 100, 20).WithOpenings(new[] { Box(40, 40, -1, 60, 60, 21) });

            Assert.True(plate.TryCutOpenings(out GeoSolid3 material));
            Assert.Contains(material.Faces, face => face.Holes.Count > 0);

            SolidValidation3 check = material.Validate();

            Assert.True(check.IsValid);
            Assert.Empty(check.Issues);
            Assert.Equal(200000.0 - 8000.0, check.SignedVolume, 6);
        }

        [Fact]
        public void AChamferThinnerThanTheTolerance_IsNoted_AndTheBodyStaysValid()
        {
            // The edge of a box along y at x = 10, z = 10, cut off 0.0004 each way.
            const double c = 0.0004;
            var faces = new[]
            {
                FineFace(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(10, 0, 0)),
                FineFace(new GeoPoint3(0, 0, 10), new GeoPoint3(10 - c, 0, 10), new GeoPoint3(10 - c, 10, 10), new GeoPoint3(0, 10, 10)),
                FineFace(new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(10, 10, 10 - c), new GeoPoint3(10, 0, 10 - c)),
                FineFace(new GeoPoint3(10 - c, 0, 10), new GeoPoint3(10, 0, 10 - c), new GeoPoint3(10, 10, 10 - c), new GeoPoint3(10 - c, 10, 10)),
                FineFace(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 0, 10 - c), new GeoPoint3(10 - c, 0, 10), new GeoPoint3(0, 0, 10)),
                FineFace(new GeoPoint3(0, 10, 0), new GeoPoint3(0, 10, 10), new GeoPoint3(10 - c, 10, 10), new GeoPoint3(10, 10, 10 - c), new GeoPoint3(10, 10, 0)),
                FineFace(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 10), new GeoPoint3(0, 10, 10), new GeoPoint3(0, 10, 0)),
            };
            var chamfered = new GeoSolid3(faces);

            SolidValidation3 check = chamfered.Validate();

            Assert.True(chamfered.IsClosed());
            Assert.True(check.IsValid);

            SolidIssue3 sliver = Assert.Single(check.Issues, i => i.Kind == SolidIssueKind.SliverFace);
            Assert.Equal(3, Assert.Single(sliver.Faces));
            Assert.InRange(sliver.Size, 0.0005, 0.0006);

            // Its two ends, and the corner of each end of the box they meet.
            Assert.Equal(4, Of(check, SolidIssueKind.ShortEdge).Count());
            Assert.All(Of(check, SolidIssueKind.ShortEdge), edge => Assert.Equal(c * System.Math.Sqrt(2.0), edge.Size, 9));

            // Within the tolerance the chamfer is a line, which the top, the side and both its own long edges lie along.
            SolidIssue3 line = Assert.Single(check.Issues, i => i.Kind == SolidIssueKind.NonManifoldEdge);
            Assert.Equal(new[] { 1, 2, 3 }, line.Faces);
            Assert.Equal(2, line.Forward);
            Assert.Equal(2, line.Backward);

            // Within a ten-thousandth the chamfer is a face like any other.
            Assert.Empty(chamfered.Validate(new Tolerance(1E-4, 1E-6, Tolerance.DefaultEqualAngleRad, 1E-4)).Issues);
        }

        [Fact]
        public void AFaceOutOfFlat_IsNoted_ByHowFarItsFurthestCornerStandsOff()
        {
            // The top corner over (10, 10) raised by 0.04, the three faces round it built within a looser tolerance.
            var loose = new Tolerance(0.1, 0.1, Tolerance.DefaultEqualAngleRad, 0.1);
            GeoPoint3 raised = new GeoPoint3(10, 10, 10.04);
            var faces = new[]
            {
                Face(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(10, 0, 0)),
                new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(0, 0, 10), new GeoPoint3(10, 0, 10), raised, new GeoPoint3(0, 10, 10) }, loose), null, loose),
                new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), raised, new GeoPoint3(10, 0, 10) }, loose), null, loose),
                new GeoFace3(new GeoPolygon3(new[] { new GeoPoint3(0, 10, 0), new GeoPoint3(0, 10, 10), raised, new GeoPoint3(10, 10, 0) }, loose), null, loose),
                Face(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 0, 10), new GeoPoint3(0, 0, 10)),
                Face(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 10), new GeoPoint3(0, 10, 10), new GeoPoint3(0, 10, 0)),
            };

            SolidValidation3 check = new GeoSolid3(faces).Validate();

            Assert.True(check.IsValid);
            SolidIssue3 top = Assert.Single(check.Issues, i => i.Kind == SolidIssueKind.NotFlatFace && i.Faces[0] == 1);
            Assert.InRange(top.Size, 0.01, 0.04);
            Assert.All(check.Issues, i => Assert.Equal(SolidIssueKind.NotFlatFace, i.Kind));
            Assert.Empty(new GeoSolid3(faces).Validate(loose).Issues);
        }

        [Fact]
        public void AnIssueSaysWhatItIsAndWhere()
        {
            GeoSolid3 box = Box(0, 0, 0, 10, 10, 10);
            SolidValidation3 check = new GeoSolid3(box.Faces.Skip(1)).Validate();

            Assert.StartsWith("SolidValidation3[not valid, volume ", check.ToString());
            Assert.EndsWith("; OpenEdge 4]", check.ToString());
            Assert.Matches(@"^SolidIssue3\[OpenEdge 10 long at \(.+\), face \d; [01] one way, [01] the other\]$", check.Issues[0].ToString());
        }
    }
}
