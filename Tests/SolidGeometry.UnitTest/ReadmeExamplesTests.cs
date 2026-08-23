using System;
using System.Collections.Generic;
using CommonGeometry;
using CommonGeometry.Enums;
using SolidGeometry;
using SolidGeometry.Core;
using SolidGeometry.Geometry;
using Xunit;

namespace SolidGeometry.UnitTest
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

            var tree = SolidGeometry.Spatial.GeoBvh3.FromSolid(solid);

            Assert.Equal(15.0, tree.DistanceTo(point), 9);
            Assert.True(tree.GetClosestPoint(point).IsEqualTo(new GeoPoint3(3, 6, 10)));
            Assert.Equal(2, tree.GetIntersections(ray).Length);

            var otherTree = SolidGeometry.Spatial.GeoBvh3.FromSolid(
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
    }
}
