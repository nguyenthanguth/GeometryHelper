using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;
using GeometryHelper.Extension;
using GeometryHelper.Geometry;
using Xunit;
using System.Linq;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Runs every snippet printed in the README and checks the values it claims.
    /// <para>
    /// Documentation that is not executed drifts away from the code it describes. Keeping each example
    /// here means a change that breaks one is caught by the test run rather than by a reader.
    /// </para>
    /// </summary>
    public class ReadmeExamplesTests
    {
        [Fact]
        public void QuickStart()
        {
            var a = new GeoPoint3(0, 0, 0);
            var b = new GeoPoint3(3, 4, 0);

            double distance = a.DistanceTo(b);
            GeoVector3 direction = a.GetVectorTo(b);

            var plane = new GeoPlane3(GeoPoint3.Origin, GeoVector3.ZAxis);
            GeoPoint3 flat = plane.Project(new GeoPoint3(2, 3, 7));
            double signed = plane.SignedDistanceTo(new GeoPoint3(2, 3, 7));

            var box = new GeoObb3(GeoPoint3.Origin, 10, 20, 30);
            double volume = box.Volume;

            Assert.Equal(5.0, distance, 9);
            Assert.True(direction.IsEqualTo(new GeoVector3(3, 4, 0)));
            Assert.True(flat.IsEqualTo(new GeoPoint3(2, 3, 0)));
            Assert.Equal(7.0, signed, 9);
            Assert.Equal(6000.0, volume, 9);
        }

        [Fact]
        public void ACurveHasNoInteriorUntilItBecomesARegion()
        {
            var traced = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(0, 0, 0));

            var middle = new GeoPoint3(5, 5, 0);

            Assert.False(traced.IsPointOn(middle));
            Assert.Equal(5.0, traced.DistanceTo(middle), 9);
            Assert.True(traced.ToPolygon().Contains(middle));
            Assert.Equal(0.0, traced.ToPolygon().DistanceTo(middle), 9);
        }

        [Fact]
        public void APlanarRegionIsFlatSoAPointAboveItIsOutside()
        {
            var square = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

            Assert.Equal(PointLocation.Inside, square.Locate(new GeoPoint3(5, 5, 0)));
            Assert.Equal(PointLocation.OnSide, square.Locate(new GeoPoint3(5, 0, 0)));
            Assert.Equal(PointLocation.OutSide, square.Locate(new GeoPoint3(5, 5, 3)));
        }

        [Fact]
        public void FlatnessIsEnforced()
        {
            Assert.Throws<ArgumentException>(() => new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 5)));
        }

        [Fact]
        public void ABoundingBoxEnclosesThePointsGivenToIt()
        {
            var bounds = GeoAabb3.FromPoints(new[]
            {
                new GeoPoint3(1, 5, -2),
                new GeoPoint3(-3, 0, 4),
            });

            Assert.True(bounds.Min.IsEqualTo(new GeoPoint3(-3, 0, -2)));
            Assert.True(bounds.Max.IsEqualTo(new GeoPoint3(1, 5, 4)));
            Assert.Equal(120.0, bounds.Volume, 9);
        }

        [Fact]
        public void AnOrientedBoxSquaresUpItsAxes()
        {
            var box = new GeoObb3(
                GeoPoint3.Origin, 2, 2, 2,
                GeoVector3.XAxis,
                new GeoVector3(0.5, 1, 0));

            Assert.True(box.AxisX.IsPerpendicularTo(box.AxisY));
            Assert.True(box.AxisX.CrossProduct(box.AxisY).IsEqualTo(box.AxisZ));
        }

        [Fact]
        public void ASolidWithAnOpening()
        {
            GeoSolid3 slab = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)).ToObb().ToSolid();
            GeoSolid3 duct = new GeoAabb3(new GeoPoint3(4, 4, 4), new GeoPoint3(6, 6, 6)).ToObb().ToSolid();

            GeoSolid3 pierced = slab.WithOpenings(new[] { duct });

            Assert.Equal(1000.0, pierced.Volume, 6);
            Assert.Equal(992.0, pierced.NetVolume, 6);
            Assert.True(pierced.IsClosed());

            Assert.Equal(PointLocation.Inside, pierced.Locate(new GeoPoint3(1, 1, 1)));
            Assert.Equal(PointLocation.OutSide, pierced.Locate(new GeoPoint3(5, 5, 5)));
        }

        [Fact]
        public void EveryOperationIsReachableBothWays()
        {
            var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            var point = new GeoPoint3(4, 3, 0);

            Assert.Equal(3.0, Distance3.DistanceTo(line, point), 9);
            Assert.Equal(3.0, line.DistanceTo(point), 9);
            Assert.Equal(3.0, point.DistanceTo(line), 9);
        }

        [Fact]
        public void IntersectionRefusesWhatIsNotASinglePoint()
        {
            var crossing = new GeoLine3(new GeoPoint3(0, 0, -5), new GeoPoint3(0, 0, 5));
            var lyingIn = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            Assert.True(crossing.TryIntersectWith(GeoPlane3.XY, out GeoPoint3 hit));
            Assert.True(hit.IsEqualTo(new GeoPoint3(0, 0, 0)));
            Assert.False(lyingIn.TryIntersectWith(GeoPlane3.XY, out _));
        }

        [Fact]
        public void BoxCollision()
        {
            var first = new GeoObb3(GeoPoint3.Origin, 10, 10, 10);
            var beside = new GeoObb3(new GeoPoint3(5, 0, 0), 10, 10, 10);
            var apart = new GeoObb3(new GeoPoint3(11, 0, 0), 10, 10, 10);

            Assert.True(first.CollidesWith(beside));
            Assert.False(first.CollidesWith(apart));
        }

        [Fact]
        public void ASegmentExtrapolatesWhereAChainClamps()
        {
            var segment = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            Assert.True(segment.GetPointAtParameter(2.0).IsEqualTo(new GeoPoint3(20, 0, 0)));

            var chain = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0), new GeoPoint3(3, 4, 0));

            Assert.True(chain.GetPointAtDistance(5.0).IsEqualTo(new GeoPoint3(3, 2, 0)));
            Assert.True(chain.GetPointAtDistance(100.0).IsEqualTo(new GeoPoint3(3, 4, 0)));
        }

        [Fact]
        public void SplittingACurve()
        {
            var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            Assert.True(line.TrySplitAtDistance(4, out GeoLine3[] pieces));
            Assert.Equal(4.0, pieces[0].Length, 9);
            Assert.Equal(6.0, pieces[1].Length, 9);

            Assert.False(line.TrySplitAtDistance(0, out _));
            Assert.False(line.TrySplitBy(new GeoPoint3(5, 3, 0), out _));
        }

        [Fact]
        public void SplittingAConcaveRegion()
        {
            var uShape = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(9, 0, 0), new GeoPoint3(9, 10, 0),
                new GeoPoint3(6, 10, 0), new GeoPoint3(6, 4, 0), new GeoPoint3(3, 4, 0),
                new GeoPoint3(3, 10, 0), new GeoPoint3(0, 10, 0));

            var cutter = new GeoPlane3(new GeoPoint3(0, 7, 0), GeoVector3.YAxis);

            Assert.True(uShape.TrySplitBy(cutter, out GeoPolygon3[] above, out GeoPolygon3[] below));
            Assert.Equal(2, above.Length);
            Assert.Single(below);
        }

        [Fact]
        public void SplittingASolid()
        {
            GeoSolid3 cube = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)).ToObb().ToSolid();

            Assert.True(cube.TrySplitBy(GeoPlane3.XY.Offset(4), out GeoSolid3 upper, out GeoSolid3 lower));
            Assert.Equal(600.0, upper.Volume, 4);
            Assert.Equal(400.0, lower.Volume, 4);
            Assert.True(upper.IsClosed());
            Assert.True(lower.IsClosed());
        }

        [Fact]
        public void JoiningAShuffledSetOfPieces()
        {
            GeoPolyline3[] chains = Merge3.Join(new[]
            {
                new GeoPolyline3(new GeoPoint3(3, 4, 0), new GeoPoint3(3, 0, 0)),
                new GeoPolyline3(new GeoPoint3(3, 4, 0), new GeoPoint3(8, 4, 0)),
                new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0)),
            });

            Assert.Single(chains);
            Assert.Equal(4, chains[0].VertexCount);
            Assert.Equal(12.0, chains[0].Length, 9);
        }

        [Fact]
        public void IndexingALargeMesh()
        {
            GeoSolid3 solid = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)).ToObb().ToSolid();
            var point = new GeoPoint3(3, 6, 25);
            var ray = new GeoRay3(new GeoPoint3(3, 6, -20), GeoVector3.ZAxis);

            var tree = GeometryHelper.Spatial.GeoBvh3.FromSolid(solid);

            Assert.Equal(15.0, tree.DistanceTo(point), 9);
            Assert.True(tree.GetClosestPoint(point).IsEqualTo(new GeoPoint3(3, 6, 10)));
            Assert.Equal(2, tree.GetIntersections(ray).Length);

            var otherTree = GeometryHelper.Spatial.GeoBvh3.FromSolid(
                solid.TransformBy(GeoTransform3.Translation(new GeoVector3(5, 0, 0))));

            Assert.True(tree.CollidesWith(otherTree));
        }

        [Fact]
        public void CuttingACurveByAClosedBody()
        {
            GeoSolid3 solid = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)).ToObb().ToSolid();
            var chain = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(20, 5, 5));

            Assert.True(chain.TrySplitBy(solid, out GeoPolyline3[] inside, out GeoPolyline3[] outside));

            Assert.Single(inside);
            Assert.Equal(10.0, inside[0].Length, 9);
            Assert.Equal(2, outside.Length);
        }

        [Fact]
        public void CuttingACurveBySeveralBodiesAtOnce()
        {
            GeoSolid3 beam = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)).ToObb().ToSolid();
            GeoSolid3 slab = new GeoAabb3(new GeoPoint3(20, 0, 0), new GeoPoint3(30, 10, 10)).ToObb().ToSolid();

            var route = new GeoPolyline3(new GeoPoint3(-10, 5, 5), new GeoPoint3(60, 5, 5));

            Assert.True(route.TrySplitBy(new[] { beam, slab }, out GeoPolyline3[] embedded, out GeoPolyline3[] clear));

            Assert.Equal(2, embedded.Length);
            Assert.Equal(3, clear.Length);

            // The two bodies swallow ten units of the route each; the route itself is seventy long.
            double buried = 0.0;
            foreach (GeoPolyline3 piece in embedded) { buried += piece.Length; }
            Assert.Equal(20.0, buried, 9);

            // Two bodies meeting face to face leave no cut between them.
            GeoSolid3 abutting = new GeoAabb3(new GeoPoint3(10, 0, 0), new GeoPoint3(25, 10, 10)).ToObb().ToSolid();

            Assert.True(route.TrySplitBy(new[] { beam, abutting }, out GeoPolyline3[] joined, out _));

            Assert.Single(joined);
            Assert.Equal(25.0, joined[0].Length, 9);

            // An empty array leaves the whole route clear.
            Assert.False(route.TrySplitBy(new GeoSolid3[0], out GeoPolyline3[] none, out GeoPolyline3[] whole));

            Assert.Empty(none);
            Assert.Single(whole);
        }

        [Fact]
        public void ABoundedRegionCutsOnlyWhereItIsPiercedThrough()
        {
            var plate = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

            // Crosses z = 0 far outside the outline of the plate.
            var chain = new GeoPolyline3(new GeoPoint3(50, 50, -5), new GeoPoint3(50, 50, 5));

            Assert.False(chain.TrySplitBy(plate, out GeoPolyline3[] pieces));
            Assert.Single(pieces);

            Assert.True(chain.TrySplitBy(plate.GetPlane(), out GeoPolyline3[] more));
            Assert.Equal(2, more.Length);
        }

        [Fact]
        public void CuttingAPlateAlongALineAndAgainstABody()
        {
            var plate = new GeoPolygon3(
                new GeoPoint3(-5, -5, 5), new GeoPoint3(15, -5, 5),
                new GeoPoint3(15, 15, 5), new GeoPoint3(-5, 15, 5));

            var cutLine = new GeoPolyline3(new GeoPoint3(-5, 5, 5), new GeoPoint3(15, 5, 5));

            Assert.True(plate.TrySplitBy(cutLine, out GeoPolygon3[] halves));
            Assert.Equal(2, halves.Length);
            Assert.Equal(plate.Area, halves[0].Area + halves[1].Area, 6);

            GeoSolid3 solid = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)).ToObb().ToSolid();

            Assert.True(plate.TrySplitBy(solid, out GeoPolygon3[] embedded, out GeoPolygon3[] clear));

            double inside = 0.0;
            foreach (GeoPolygon3 piece in embedded)
            {
                inside += piece.Area;
            }

            double outside = 0.0;
            foreach (GeoPolygon3 piece in clear)
            {
                outside += piece.Area;
            }

            Assert.Equal(100.0, inside, 6);
            Assert.Equal(plate.Area, inside + outside, 6);
        }

        [Fact]
        public void TidyingUpASurfaceAfterCutting()
        {
            GeoSolid3 box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)).ToObb().ToSolid();
            var cutter = new GeoPlane3(new GeoPoint3(5, 0, 0), GeoVector3.XAxis);

            Assert.True(box.TrySplitBy(cutter, out GeoSolid3 upper, out GeoSolid3 lower));

            List<GeoFace3> glued = new List<GeoFace3>();
            foreach (GeoFace3 face in upper.Faces)
            {
                if (!cutter.ContainsAll(face.Boundary.Vertices))
                {
                    glued.Add(face);
                }
            }

            foreach (GeoFace3 face in lower.Faces)
            {
                if (!cutter.ContainsAll(face.Boundary.Vertices))
                {
                    glued.Add(face);
                }
            }

            GeoSolid3 subdivided = new GeoSolid3(glued);
            GeoSolid3 tidied = Merge3.CoplanarFaces(subdivided);

            Assert.Equal(6, tidied.Faces.Count);
            Assert.Equal(subdivided.Volume, tidied.Volume, 6);
            Assert.Equal(subdivided.SurfaceArea, tidied.SurfaceArea, 6);
        }

        [Fact]
        public void AClosedCurveWrapsRatherThanClampingOrExtrapolating()
        {
            var segment = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            Assert.True(segment.GetPointAtParameter(2.0).IsEqualTo(new GeoPoint3(20, 0, 0)));

            var chain = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(3, 0, 0), new GeoPoint3(3, 4, 0));
            Assert.True(chain.GetPointAtDistance(5.0).IsEqualTo(new GeoPoint3(3, 2, 0)));
            Assert.True(chain.GetPointAtDistance(100.0).IsEqualTo(new GeoPoint3(3, 4, 0)));

            var square = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));
            Assert.True(square.GetPointAtParameter(0.25).IsEqualTo(new GeoPoint3(10, 0, 0)));
            Assert.True(square.GetPointAtParameter(1.25).IsEqualTo(square.GetPointAtParameter(0.25)));
        }

        [Fact]
        public void CombiningTwoBodies()
        {
            GeoSolid3 first = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(10, 10, 10)).ToObb().ToSolid();
            GeoSolid3 second = new GeoAabb3(new GeoPoint3(5, 5, 5), new GeoPoint3(15, 15, 15)).ToObb().ToSolid();
            GeoSolid3 tool = second;

            Assert.True(first.TryUnion(second, out GeoSolid3 joined));
            Assert.True(first.TryIntersect(second, out GeoSolid3 shared));
            Assert.True(first.TrySubtract(tool, out GeoSolid3 left));

            Assert.Equal(125.0, shared.Volume, 4);
            Assert.Equal(875.0, left.Volume, 4);
            Assert.Equal(1875.0, joined.Volume, 4);

            // Nothing at all is an outcome, reported as false rather than as an empty body.
            GeoSolid3 farAway = new GeoAabb3(new GeoPoint3(50, 50, 50), new GeoPoint3(60, 60, 60)).ToObb().ToSolid();
            Assert.False(first.TryIntersect(farAway, out _));
        }

        [Fact]
        public void ToleranceEqualityIsSeparateFromExactEquality()
        {
            var a = GeoPoint3.Origin;
            var b = new GeoPoint3(1e-9, 0, 0);

            Assert.True(a.IsEqualTo(b));
            Assert.False(a.Equals(b));
            Assert.True(a.IsEqualTo(new GeoPoint3(0.05, 0, 0), new Tolerance(0.1, 0.1)));
        }

        [Fact]
        public void DegenerateInputIsRefusedRatherThanGuessedAt()
        {
            Assert.False(GeoVector3.Zero.IsParallelTo(GeoVector3.XAxis));
            Assert.False(GeoVector3.Zero.TryGetNormal(out _));
            Assert.Throws<InvalidOperationException>(() => GeoVector3.Zero.Normalize());
        }

        [Fact]
        public void ALocalFrameConvertsBothWays()
        {
            var frame = new GeoCoordinateSystem3(
                new GeoPoint3(10, -20, 30),
                new GeoVector3(1, 1, 0),
                new GeoVector3(-1, 1, 1));

            var point = new GeoPoint3(3, -7, 11);

            Assert.True(frame.ToGlobal(frame.ToLocal(point)).IsEqualTo(point));
        }

        [Fact]
        public void ATransformationAppliesRightToLeftAndCanBeUndone()
        {
            GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(10, 0, 0))
                .Multiply(GeoTransform3.RotationZ(Math.PI / 2));

            Assert.True(motion.Transform(GeoPoint3.Origin).IsEqualTo(new GeoPoint3(10, 0, 0)));
            Assert.True(motion.Inverse().Transform(motion.Transform(GeoPoint3.Origin)).IsEqualTo(GeoPoint3.Origin));
        }
        [Fact]
        public void PointChains_ThinThenChain()
        {
            var traced = new List<GeoPoint3>
            {
                new GeoPoint3(0, 0, 0), new GeoPoint3(0.0001, 0, 0), new GeoPoint3(5, 0, 0), new GeoPoint3(5, 0, 5),
            };

            // "The second point is a hair away from the first and goes." - 3 points.
            List<GeoPoint3> thinned = traced.RemoveConsecutiveNearPoints(new Tolerance(0.001, 0.001));
            Assert.Equal(3, thinned.Count);

            // "One segment per consecutive pair." - 2 segments.
            List<GeoLine3> segments = thinned.ToGeoLine3s();
            Assert.Equal(2, segments.Count);

            // "Distance is measured in all three dimensions, so two points that share X and Y but
            // differ in Z are not fused."
            var stacked = new List<GeoPoint3> { new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 5) };
            Assert.Equal(2, stacked.RemoveConsecutiveNearPoints(new Tolerance(0.001, 0.001)).Count);

            // "leaves the chain open - nothing joins the last point back to the first"
            Assert.Equal(thinned.Count - 1, segments.Count);
        }

        [Fact]
        public void ExtendingAndTrimming_EverySampleHolds()
        {
            var beam = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            Assert.Equal(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(15, 0, 0)), beam.Extend(5.0, LineEnd.End));
            Assert.Equal(new GeoLine3(new GeoPoint3(-2, 0, 0), new GeoPoint3(13, 0, 0)), beam.Extend(2.0, 3.0));
            Assert.Equal(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(8, 0, 0)), beam.ExtendToLength(8.0, LineEnd.End));

            var wall = new GeoPlane3(new GeoPoint3(20, 0, 0), GeoVector3.XAxis);
            Assert.True(beam.TryExtendTo(wall, LineEnd.End, out GeoLine3 reached));
            Assert.Equal(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(20, 0, 0)), reached);

            // "a segment running towards a body stops at its surface and one running out of a body is cut
            // where it leaves it"
            GeoSolid3 block = new GeoAabb3(new GeoPoint3(20, -5, -5), new GeoPoint3(30, 5, 5)).ToObb().ToSolid();
            Assert.True(beam.TryExtendTo(block, LineEnd.End, out GeoLine3 atSurface));
            Assert.True(atSurface.EndPoint.IsEqualTo(new GeoPoint3(20, 0, 0)));

            var leaving = new GeoLine3(new GeoPoint3(22, 0, 0), new GeoPoint3(35, 0, 0));
            Assert.True(leaving.TryTrimTo(block, LineEnd.End, out GeoLine3 trimmed));
            Assert.True(trimmed.EndPoint.IsEqualTo(new GeoPoint3(30, 0, 0)));

            // "fits an end to a boundary whichever side of it the end starts on"
            foreach (GeoLine3 line in new[]
                     {
                         new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(12, 0, 0)),
                         new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(27, 0, 0))
                     })
            {
                Assert.True(line.TryExtendTo(wall, LineEnd.End, out GeoLine3 fit) || line.TryTrimTo(wall, LineEnd.End, out fit));
                Assert.Equal(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(20, 0, 0)), fit);
            }

            // "each keeps its longer part and both end at the meeting point"
            Assert.True(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(8, 0, 0))
                .TryTrimExtendToCorner(new GeoLine3(new GeoPoint3(10, 2, 0), new GeoPoint3(10, 10, 0)), out GeoLine3 a, out GeoLine3 b));
            Assert.Equal(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)), a);
            Assert.Equal(new GeoLine3(new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0)), b);

            // "where they pass wider than that, it reports false"
            Assert.False(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(8, 0, 0))
                .TryTrimExtendToCorner(new GeoLine3(new GeoPoint3(10, 2, 1), new GeoPoint3(10, 10, 1)), out _, out _));
        }

        [Fact]
        public void Offsetting_EverySampleHolds()
        {
            var line = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));

            // "left, seen from the normal"
            Assert.Equal(new GeoLine3(new GeoPoint3(0, 2, 0), new GeoPoint3(10, 2, 0)), line.OffsetInPlane(2.0, GeoVector3.ZAxis));

            // "towards a direction; only its square part counts"
            GeoLine3 tilted = line.Offset(2.0, new GeoVector3(0, 1, 1));
            Assert.Equal(2.0, tilted.StartPoint.DistanceTo(line.StartPoint), 9);
            Assert.Equal(0.0, tilted.StartPoint.X, 9);

            // "sideways and up in one call"
            Assert.Equal(new GeoLine3(new GeoPoint3(0, 2, 3), new GeoPoint3(10, 2, 3)), line.Offset(2.0, 3.0, GeoVector3.ZAxis));

            // "the parallel through a point"
            Assert.Equal(new GeoLine3(new GeoPoint3(0, 7, 0), new GeoPoint3(10, 7, 0)), line.OffsetThrough(new GeoPoint3(3, 7, 0)));

            var square = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0));

            GeoPolygon3[] grown = square.Offset(2.0);
            Assert.Single(grown);
            Assert.Equal(4, grown[0].VertexCount);
            Assert.Equal(14.0 * 14.0, grown[0].Area, 9);
            Assert.All(grown[0].Vertices, v => Assert.Equal(0.0, v.Z, 9));

            Assert.True(square.Offset(2.0, OffsetJoin.Round).Length == 1);
            Assert.True(square.Offset(2.0, OffsetJoin.Round)[0].VertexCount > 4);
            Assert.Empty(square.Offset(-5.0));

            // "a GeoFace3 keeps its holes, which grow as the boundary shrinks"
            var plate = new GeoFace3(square, new[]
            {
                new GeoPolygon3(new GeoPoint3(3, 3, 0), new GeoPoint3(7, 3, 0), new GeoPoint3(7, 7, 0), new GeoPoint3(3, 7, 0))
            });
            GeoFace3 shrunk = Assert.Single(plate.Offset(-1.0));
            Assert.Single(shrunk.Holes);
            Assert.Equal(8.0 * 8.0 - 6.0 * 6.0, shrunk.Area, 9);

            // "a GeoPolyline3 needs a plane, like a segment"
            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0));
            GeoPolyline3 parallel = Assert.Single(chain.OffsetInPlane(1.0, GeoVector3.ZAxis));
            Assert.Equal(3, parallel.VertexCount);
            Assert.True(parallel.Vertices[1].IsEqualTo(new GeoPoint3(9, 1, 0)));

            // "a circle stays a circle"
            var circle = new GeoCircle3(new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 0), 5.0);
            Assert.True(circle.TryOffset(2.0, out GeoCircle3 wider));
            Assert.Equal(7.0, wider.Radius, 12);
            Assert.True(wider.Center.IsEqualTo(circle.Center));

            // "the result comes back exactly in that plane": the same square tilted keeps its own plane.
            var slope = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 10),
                new GeoPoint3(10, 10, 10), new GeoPoint3(0, 10, 0));
            GeoPolygon3 offsetSlope = Assert.Single(slope.Offset(1.0));
            GeoPlane3 carrier = new GeoPlane3(slope.Vertices[0], slope.Normal);
            Assert.All(offsetSlope.Vertices, v => Assert.Equal(0.0, carrier.SignedDistanceTo(v), 9));
        }

        [Fact]
        public void SelfIntersection_IsNotCheckedOnTheWayIn()
        {
            // "Self-intersection is ... not checked on the way in ... polygon.IsSimple() runs it when you
            // want it"
            var crossed = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(2, 8, 0), new GeoPoint3(6, -4, 0));

            Assert.False(crossed.IsSimple());

            // A boundary that encloses no net area has no normal, so it is refused outright rather than
            // built and reported not simple.
            Assert.Throws<ArgumentException>(() => new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0),
                new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0)));
            Assert.True(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0)).IsSimple());
        }

        [Fact]
        public void BetweenThePlaneAndSpace_EverySampleHolds()
        {
            // "GeoCoordinateSystem3 frame = plate.GetFrame();" on a plate tilted in space.
            var carrier = new GeoCoordinateSystem3(
                new GeoPoint3(12, -8, 30), new GeoVector3(1, 1, 0).Normalize(), new GeoVector3(-1, 1, 1).Normalize());

            GeoPolygon3 Plate(params double[] xy) => new GeoPolygon3(
                Enumerable.Range(0, xy.Length / 2)
                    .Select(i => PlanarMap.ToPoint3(carrier, new GeoPoint2(xy[2 * i], xy[2 * i + 1]))));

            GeoPolygon3 plate = Plate(0, 0, 60, 0, 60, 40, 0, 40);
            GeoPolygon3 opening = Plate(10, 10, 20, 10, 20, 20, 10, 20);

            GeoCoordinateSystem3 frame = plate.GetFrame();

            // "GeoFace2 flat = plate.ProjectToFace2(frame);" - here from the polygon, same thing.
            GeoFace2 flat = new GeoFace3(plate).ProjectToFace2(frame);
            Assert.Equal(60.0 * 40.0, flat.Area, 6);

            // "GeoFace2[] cut = flat.Subtract(openings);"
            GeoFace2[] cut = flat.Subtract(new GeoFace3(opening).ProjectToFace2(frame));
            Assert.Single(cut);
            Assert.Equal(60.0 * 40.0 - 10.0 * 10.0, cut[0].Area, 6);

            // "GeoFace3 back = cut[0].ToFace3(frame);"
            GeoFace3 back = cut[0].ToFace3(frame);
            Assert.Equal(cut[0].Area, back.Area, 6);
            Assert.True(back.Normal.IsParallelTo(plate.Normal));

            // "A frame taken from the shape itself always agrees" with the shape's normal.
            Assert.True(frame.ZAxis.IsEqualTo(plate.Normal));

            // "point.TryToPoint2(frame, out GeoPoint2 flat); // false when the point is off the plane"
            GeoPoint3 above = plate.Vertices[0].Add(plate.Normal.Multiply(5.0));
            Assert.False(above.TryToPoint2(frame, out _));

            // "point.ProjectToPoint2(frame); // always answers, by projecting"
            Assert.True(above.ProjectToPoint2(frame).IsEqualTo(plate.Vertices[0].ProjectToPoint2(frame),
                new Tolerance(1E-9, 1E-9)));

            // "Every flat type makes the trip"
            var chain = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(5, 0), new GeoPoint2(5, 5));
            Assert.Equal(chain.Length, chain.ToPolyline3(frame).Length, 9);
            Assert.Equal(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(3, 4)).Length,
                         new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(3, 4)).ToLine3(frame).Length, 9);
        }

        [Fact]
        public void AskingABodyAboutWhatIsAroundIt_EverySampleHolds()
        {
            GeoSolid3 slab = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100)).ToObb().ToSolid();

            var point = new GeoPoint3(300, 50, 50);
            var line = new GeoLine3(new GeoPoint3(300, 50, 0), new GeoPoint3(300, 50, 100));
            var ray = new GeoRay3(new GeoPoint3(300, 50, 50), new GeoVector3(1, 0, 0));
            var triangle = new GeoTriangle3(
                new GeoPoint3(300, 0, 0), new GeoPoint3(400, 0, 0), new GeoPoint3(300, 100, 0));
            var polygon = new GeoPolygon3(
                new GeoPoint3(300, 0, 0), new GeoPoint3(400, 0, 0),
                new GeoPoint3(400, 100, 0), new GeoPoint3(300, 100, 0));
            var polyline = new GeoPolyline3(new GeoPoint3(300, 0, 0), new GeoPoint3(300, 100, 0));
            var plane = new GeoPlane3(new GeoPoint3(300, 0, 0), new GeoVector3(1, 0, 0));
            var obb = new GeoAabb3(new GeoPoint3(300, 0, 0), new GeoPoint3(400, 100, 100)).ToObb();
            var aabb = new GeoAabb3(new GeoPoint3(300, 0, 0), new GeoPoint3(400, 100, 100));
            var face = new GeoFace3(polygon);
            GeoSolid3 otherSolid = obb.ToSolid();

            // Every one of them stands two hundred from the far face of the slab.
            foreach (double reach in new[]
            {
                slab.DistanceTo(point), slab.DistanceTo(line), slab.DistanceTo(ray),
                slab.DistanceTo(triangle), slab.DistanceTo(polygon), slab.DistanceTo(polyline),
                slab.DistanceTo(plane), slab.DistanceTo(obb), slab.DistanceTo(aabb),
                slab.DistanceTo(otherSolid)
            })
            {
                Assert.Equal(200.0, reach, 9);
            }

            // None of them reaches the body, and the ones with an inside are not fooled by that.
            Assert.False(slab.CollidesWith(line));
            Assert.False(slab.CollidesWith(ray));
            Assert.False(slab.CollidesWith(polyline));
            Assert.False(slab.CollidesWith(polygon));
            Assert.False(slab.CollidesWith(face));
            Assert.False(slab.CollidesWith(obb));
            Assert.False(slab.CollidesWith(otherSolid));

            Assert.Empty(slab.GetIntersections(line));
            Assert.Empty(slab.GetIntersections(ray));
            Assert.Empty(slab.GetIntersections(plane));

            // The shortest line is the distance, and it leaves the surface.
            foreach (GeoLine3 joining in new[]
            {
                slab.GetShortestLineTo(point), slab.GetShortestLineTo(line), slab.GetShortestLineTo(ray),
                slab.GetShortestLineTo(triangle), slab.GetShortestLineTo(otherSolid)
            })
            {
                Assert.Equal(200.0, joining.Length, 9);
                Assert.Equal(100.0, joining.StartPoint.X, 9);
            }

            // A point inside is nothing away, and still has a segment out to the skin.
            var inside = new GeoPoint3(50, 50, 10);

            Assert.True(slab.GetClosestPointOnBoundary(inside).IsEqualTo(new GeoPoint3(50, 50, 0)));
            Assert.Equal(0.0, slab.DistanceTo(inside), 12);
            Assert.Equal(10.0, slab.GetShortestLineTo(inside).Length, 9);
        }

        [Fact]
        public void ARayAgainstABody_EverySampleHolds()
        {
            GeoSolid3 slab = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100)).ToObb().ToSolid();

            var incoming = new GeoRay3(new GeoPoint3(-200, 50, 50), new GeoVector3(1, 0, 0));
            var leaving = new GeoRay3(new GeoPoint3(-200, 50, 50), new GeoVector3(-1, 0, 0));

            Assert.Equal(2, slab.GetIntersections(incoming).Length);
            Assert.Equal(0.0, slab.DistanceTo(incoming), 12);
            Assert.Equal(200.0, slab.DistanceTo(leaving), 9);

            // A circle in space is turned into a chain first, which says how close an answer is wanted.
            var circle = new GeoCircle3(new GeoPoint3(300, 50, 50), new GeoVector3(0, 0, 1), 20.0);

            Assert.Equal(180.0, slab.DistanceTo(circle.ToPolylineByChordTolerance(0.1)), 1);
        }

        [Fact]
        public void HowDeepInsideNotJustWhether_EverySampleHolds()
        {
            GeoAabb3 box = new GeoAabb3(GeoPoint3.Origin, new GeoPoint3(100, 100, 100));
            GeoSolid3 cube = box.ToObb().ToSolid();

            Assert.Equal(0.0, cube.DistanceTo(new GeoPoint3(50, 50, 50)), 12);
            Assert.Equal(-50.0, cube.SignedDistanceTo(new GeoPoint3(50, 50, 50)), 9);
            Assert.Equal(-10.0, cube.SignedDistanceTo(new GeoPoint3(50, 50, 10)), 9);
            Assert.Equal(20.0, cube.SignedDistanceTo(new GeoPoint3(50, 50, -20)), 9);
            Assert.Equal(-50.0, box.SignedDistanceTo(new GeoPoint3(50, 50, 50)), 9);

            GeoSolid3 duct = new GeoAabb3(new GeoPoint3(40, 40, -10), new GeoPoint3(60, 60, 110)).ToObb().ToSolid();
            GeoSolid3 pierced = cube.WithOpenings(new[] { duct });

            Assert.Equal(-10.0, pierced.SignedDistanceTo(new GeoPoint3(30, 50, 50)), 9);
            Assert.Equal(-30.0, cube.SignedDistanceTo(new GeoPoint3(30, 50, 50)), 9);
            Assert.Equal(10.0, pierced.SignedDistanceTo(new GeoPoint3(50, 50, 50)), 9);
        }

        [Fact]
        public void ArcsInSpace_EverySampleHolds()
        {
            var straight = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0));
            var bulged = new GeoEdge3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), 1.0, new GeoVector3(0, 0, 1));

            Assert.False(straight.IsArc);
            Assert.True(bulged.IsArc);
            Assert.Equal(50.0, bulged.ToArc().Radius, 7);
            Assert.Equal(100.0, bulged.GetChord().Length, 9);
            Assert.True(bulged.GetPlane().Normal.IsParallelTo(new GeoVector3(0, 0, 1)));

            GeoPolylineArc3 bar = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0),
                new GeoPoint3(300, 300, 0), new GeoPoint3(300, 300, 300)).Fillet(50.0);

            Assert.Equal(857.1, bar.Length, 1);
            Assert.False(bar.IsPlanar());

            // Shorter than the set-out by 2r - pi r / 2 at each of the two bends.
            Assert.Equal(900.0 - 2.0 * (2.0 * 50.0 - Math.PI * 50.0 / 2.0), bar.Length, 6);

            GeoPolygonArc3 tie = new GeoPolygonArc3(new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0),
                new GeoPoint3(300, 200, 0), new GeoPoint3(0, 200, 0))).Fillet(40.0);

            Assert.True(tie.Area > 0.0);
            Assert.True(tie.Contains(new GeoPoint3(150, 100, 0)));
            Assert.False(tie.Contains(new GeoPoint3(150, 100, 25)));
            Assert.Single(tie.Offset(25.0));
            Assert.Equal(tie.Area, tie.ToPolygonArc2().Area, 5);
        }

        [Fact]
        public void BetweenThePlaneAndSpaceWithArcs_EverySampleHolds()
        {
            var plate = new GeoPolygon3(
                new GeoPoint3(0, 0, 10), new GeoPoint3(100, 0, 10),
                new GeoPoint3(100, 100, 10), new GeoPoint3(0, 100, 10));

            GeoCoordinateSystem3 frame = plate.GetFrame();

            GeoPolylineArc3 edgeInModel = PlanarMap.ToPolylineArc3(frame, new GeoPolylineArc2(
                new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) },
                new[] { 0.0, -1.0, 0.0 }));

            Assert.True(PlanarMap.TryToPolylineArc2(frame, edgeInModel, out GeoPolylineArc2 laidOut));

            GeoPolylineArc2 moved = laidOut.Offset(10.0)[0];
            GeoPolylineArc3 backInModel = PlanarMap.ToPolylineArc3(frame, moved);

            Assert.True(backInModel.IsPlanar());
            Assert.Equal(moved.Length, backInModel.Length, 6);

            // The curve survives the round trip, which flattening would have thrown away.
            Assert.Contains(backInModel.GetEdges(), edge => edge.IsArc);
            Assert.True(edgeInModel.Flatten().Length < edgeInModel.Length);
        }

        [Fact]
        public void SamplingABentBar_EverySampleHolds()
        {
            GeoPolylineArc3 bar = new GeoPolyline3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0),
                new GeoPoint3(300, 300, 0), new GeoPoint3(300, 300, 300)).Fillet(50.0);

            GeoSolid3 slab = new GeoAabb3(
                new GeoPoint3(500, -500, -500), new GeoPoint3(600, 500, 500)).ToObb().ToSolid();

            Assert.Equal(200.0, bar.ToPolyline3(0.1).DistanceTo(slab), 6);
            Assert.False(bar.ToPolyline3(0.1).CollidesWith(slab));

            // A sampled chain lies inside the arcs it stands for, so it never claims the bar is nearer.
            foreach (GeoPoint3 vertex in bar.ToPolyline3(0.1).Vertices)
            {
                Assert.True(bar.DistanceTo(vertex) <= 1E-6);
            }
        }
    }
}
