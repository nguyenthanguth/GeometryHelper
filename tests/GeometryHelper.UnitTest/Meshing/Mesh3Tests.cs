using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// Flat shapes of space meshed: laid out in their plane, meshed as the plane meshes, and put back with their own corners
    /// where they are; the grid standing where the placement says.
    /// </summary>
    public class Mesh3Tests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPoint2 Q(double x, double y) => new GeoPoint2(x, y);

        private static GeoPolygon2 Box2(double x0, double y0, double x1, double y1) => new GeoPolygon2(Q(x0, y0), Q(x1, y0), Q(x1, y1), Q(x0, y1));

        /// <summary>A rectangle of a wall in the plane y = 0, its corners at (x, 0, z), wound so that it faces -Y.</summary>
        private static GeoPolygon3 WallBox(double x0, double z0, double x1, double z1) => new GeoPolygon3(P(x0, 0, z0), P(x1, 0, z0), P(x1, 0, z1), P(x0, 0, z1));

        /// <summary>The wall of the guide of the plane, 7215 by 3012 with a door and a window, standing in the plane y = 0.</summary>
        private static GeoFace3 Wall() => new GeoFace3(WallBox(0, 0, 7215, 3012), new[] { WallBox(900, 0, 1800, 2100), WallBox(3600, 900, 5400, 2100) });

        private static int Whole(GeoMesh3 mesh) => Enumerable.Range(0, mesh.FaceCount).Count(mesh.IsWhole);

        private static IEnumerable<GeoPoint3> Corners(GeoFace3 face) => face.Boundary.Vertices.Concat(face.Holes.SelectMany(h => h.Vertices));

        #region A wall

        [Fact]
        public void AWallStandingUpLaysItsPanelsAsTheElevationOfThePlaneDoes()
        {
            GeoFace3 wall = Wall();
            GeoMesh3 panels = wall.ToMesh(MeshOptions.Grid(1200, 600, joint: 3), Tolerance);

            // The wall faces -Y, so its grid runs along X and up Z, and lays what the elevation lays in the plane.
            Assert.Equal(-1.0, wall.Normal.Y, 12);
            Assert.Equal(1.0, panels.Frame.XAxis.X, 12);
            Assert.Equal(1.0, panels.Frame.YAxis.Z, 12);
            Assert.Equal(29, panels.FaceCount);
            Assert.Equal(13, Whole(panels));
            Mesh3Assert.IsSound(panels, Mesh3Assert.Locate(wall, Tolerance), Corners(wall), panels.ToMesh2().Area, Tolerance, covers: false);

            var elevation = new GeoFace2(Box2(0, 0, 7215, 3012), new[] { Box2(900, 0, 1800, 2100), Box2(3600, 900, 5400, 2100) });
            GeoMesh2 drawn = elevation.ToMesh(MeshOptions.Grid(1200, 600, joint: 3), Tolerance);
            Assert.Equal(drawn.FaceCount, panels.FaceCount);
            Assert.Equal(drawn.VertexCount, panels.VertexCount);
            Assert.Equal(drawn.Area, panels.Area, 6);

            foreach (GeoPoint3 vertex in panels.Vertices)
            {
                Assert.Equal(0.0, vertex.Y);
            }
        }

        [Fact]
        public void AWallTurnedAndFarOutLaysTheSamePanelsAlongItself()
        {
            // Turned 37 degrees about Z and moved seven kilometres out, as a wall of a model stands.
            GeoTransform3 placed = GeoTransform3.Translation(new GeoVector3(512345.5, 7012345.25, 31.5)) * GeoTransform3.RotationZ(37.0 * Math.PI / 180.0);
            GeoFace3 wall = Wall().TransformBy(placed);
            GeoMesh3 panels = wall.ToMesh(MeshOptions.Grid(1200, 600, joint: 3), Tolerance);

            Assert.Equal(29, panels.FaceCount);
            Assert.Equal(13, Whole(panels));
            Mesh3Assert.IsSound(panels, Mesh3Assert.Locate(wall, Tolerance), Corners(wall), panels.ToMesh2().Area, Tolerance, covers: false);

            // The rows stay level, so every whole panel has two sides level and two upright.
            for (int f = 0; f < panels.FaceCount; f++)
            {
                if (!panels.IsWhole(f))
                {
                    continue;
                }

                GeoPolygon3 panel = panels.GetFace(f);
                Assert.Equal(1200.0 * 600.0, panel.Area, 3);

                foreach (GeoLine3 side in panel.GetEdges())
                {
                    GeoVector3 along = side.StartPoint.GetVectorTo(side.EndPoint).Normalize();
                    Assert.True(Math.Abs(along.Z) < 1E-9 || Math.Abs(Math.Abs(along.Z) - 1.0) < 1E-9, $"a side of a whole panel runs {along}");
                }
            }
        }

        #endregion

        #region Level faces and the plane

        private static readonly GeoPolygon2 L = new GeoPolygon2(Q(0, 0), Q(2000, 0), Q(2000, 1000), Q(1000, 1000), Q(1000, 2000), Q(0, 2000));

        public static IEnumerable<object[]> Kinds()
        {
            yield return new object[] { MeshKind.Triangles };
            yield return new object[] { MeshKind.Convex };
            yield return new object[] { MeshKind.Strips };
            yield return new object[] { MeshKind.Grid };
        }

        private static MeshOptions OptionsFor(MeshKind kind) => kind == MeshKind.Grid ? MeshOptions.Grid(300, 400, 10, GridAlignment.CenterCell) : new MeshOptions(kind);

        [Theory]
        [MemberData(nameof(Kinds))]
        public void ALevelFaceMeshesAsThePlaneMeshesIt(MeshKind kind)
        {
            var slab = new GeoFace2(L, new[] { Box2(300, 300, 700, 600) });
            GeoFace3 top = new GeoFace3(
                new GeoPolygon3(L.Vertices.Select(p => P(p.X, p.Y, 2500))),
                new[] { new GeoPolygon3(Box2(300, 300, 700, 600).Vertices.Select(p => P(p.X, p.Y, 2500))) });

            GeoMesh2 plane = slab.ToMesh(OptionsFor(kind), Tolerance);
            GeoMesh3 space = top.ToMesh(OptionsFor(kind), Tolerance);

            Assert.Equal(1.0, space.Normal.Z, 12);
            Assert.Equal(1.0, space.Frame.XAxis.X, 12);
            Assert.Equal(plane.FaceCount, space.FaceCount);
            Assert.Equal(plane.VertexCount, space.VertexCount);
            Assert.Equal(plane.Area, space.Area, 6);
            Mesh3Assert.IsSound(space, Mesh3Assert.Locate(top, Tolerance), Corners(top), slab.Area - (kind == MeshKind.Grid ? slab.Area - plane.Area : 0.0), Tolerance, covers: kind != MeshKind.Grid);

            // Face by face the same faces, moved up.
            var faces2 = plane.GetFaces().Select(f => string.Join(";", f.Vertices.Select(v => $"{v.X:F6},{v.Y:F6}")) ).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var faces3 = space.GetFaces().Select(f => string.Join(";", f.Vertices.Select(v => $"{v.X:F6},{v.Y:F6}"))).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            Assert.Equal(faces2, faces3);
        }

        [Fact]
        public void AFaceLevelWithinTheAngleToleranceRunsItsGridAlongX()
        {
            // Tilted half a degree about Y, the slab is level within the tolerance's degree: its grid runs along X, as a
            // level one's does. Tilted two degrees, it runs level along its slope, which is along Y.
            GeoPolygon3 Tilted(double degrees) => new GeoPolygon3(P(0, 0, 0), P(4000, 0, 0), P(4000, 3000, 0), P(0, 3000, 0))
                .TransformBy(GeoTransform3.RotationY(degrees * Math.PI / 180.0));

            GeoMesh3 nearly = Tilted(0.5).ToMesh(MeshOptions.Grid(1000, 1000), Tolerance);
            GeoMesh3 sloped = Tilted(2.0).ToMesh(MeshOptions.Grid(1000, 1000), Tolerance);

            Assert.True(nearly.Frame.XAxis.X > 0.9999, $"the nearly level slab runs its grid along {nearly.Frame.XAxis}");
            Assert.Equal(12, Whole(nearly));
            Assert.Equal(1.0, Math.Abs(sloped.Frame.XAxis.Y), 12);
            Assert.Equal(12, Whole(sloped));
        }

        [Fact]
        public void AFaceTurnedDownRunsItsFacesAboutItsOwnNormal()
        {
            // The soffit of a slab faces down, and its faces run counter-clockwise seen from below.
            GeoPolygon3 soffit = new GeoPolygon3(P(0, 0, 0), P(0, 3000, 0), P(4000, 3000, 0), P(4000, 0, 0));
            GeoMesh3 mesh = soffit.ToMesh(MeshOptions.Grid(1000, 1000), Tolerance);

            Assert.Equal(-1.0, soffit.Normal.Z, 12);
            Assert.Equal(-1.0, mesh.Normal.Z, 12);
            Assert.Equal(12, mesh.FaceCount);
            Assert.Equal(12, Whole(mesh));
            Mesh3Assert.IsSound(mesh, Mesh3Assert.Locate(soffit, Tolerance), soffit.Vertices, 12E6, Tolerance);
        }

        #endregion

        #region A roof

        [Fact]
        public void ARoofLaysItsTilesInLevelRowsUpTheSlope()
        {
            // A roof 30 degrees up from the eaves along Y, 6000 along the eaves and 3000 up the slope.
            double slope = 30.0 * Math.PI / 180.0;
            GeoPoint3 Up(double x, double s) => P(x, s * Math.Cos(slope), s * Math.Sin(slope));
            var roof = new GeoPolygon3(Up(0, 0), Up(6000, 0), Up(6000, 3000), Up(0, 3000));

            GeoMesh3 tiles = roof.ToMesh(MeshOptions.Grid(400, 300), Tolerance);

            Assert.Equal(15 * 10, tiles.FaceCount);
            Assert.Equal(15 * 10, Whole(tiles));
            Assert.Equal(0.0, tiles.Frame.XAxis.Z, 12);
            Assert.True(tiles.Frame.YAxis.Z > 0.49, "the second axis runs up the slope");
            Mesh3Assert.IsSound(tiles, Mesh3Assert.Locate(roof, Tolerance), roof.Vertices, 6000 * 3000, Tolerance);
        }

        [Fact]
        public void AnAngleTurnsTheGridAboutTheNormal()
        {
            GeoPolygon3 face = WallBox(0, 0, 3000, 3000);
            GeoMesh3 turned = face.ToMesh(new MeshOptions(MeshKind.Grid, 500, 500, angleRad: Math.PI / 4), Tolerance);

            // 45 degrees counter-clockwise seen from -Y, the side the wall faces: from +X towards +Z.
            Assert.Equal(Math.Sqrt(0.5), turned.Frame.XAxis.X, 12);
            Assert.Equal(Math.Sqrt(0.5), turned.Frame.XAxis.Z, 12);
            Mesh3Assert.IsSound(turned, Mesh3Assert.Locate(face, Tolerance), face.Vertices, 9E6, Tolerance);
            Assert.True(Whole(turned) > 0);
        }

        #endregion

        #region Placements

        [Fact]
        public void OwnSidesRunAGridAlongARectangleHoweverItLies()
        {
            // A plate 2400 by 1200 lying askew: turned about two axes, so that none of its sides is level.
            GeoTransform3 askew = GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7) * GeoTransform3.RotationZ(0.3);
            GeoPolygon3 plate = new GeoPolygon3(P(0, 0, 0), P(2400, 0, 0), P(2400, 1200, 0), P(0, 1200, 0)).TransformBy(askew);

            GeoMesh3 level = plate.ToMesh(MeshOptions.Grid(600, 400), Tolerance);
            GeoMesh3 own = plate.ToMesh(MeshOptions.Grid(600, 400), MeshPlacement3.Own, Tolerance);

            // Along its own long side, it divides into 4 by 3 whole cells; level, it does not.
            Assert.Equal(12, own.FaceCount);
            Assert.Equal(12, Whole(own));
            Assert.True(Whole(level) < 12);
            GeoVector3 longSide = plate.Vertices[0].GetVectorTo(plate.Vertices[1]).Normalize();
            Assert.Equal(1.0, Math.Abs(own.Frame.XAxis.DotProduct(longSide)), 9);
            Mesh3Assert.IsSound(own, Mesh3Assert.Locate(plate, Tolerance), plate.Vertices, 2400 * 1200, Tolerance);
            Mesh3Assert.IsSound(level, Mesh3Assert.Locate(plate, Tolerance), plate.Vertices, 2400 * 1200, Tolerance);
        }

        [Fact]
        public void OwnSidesComeOutTheSameWhicheverCornerTheLoopStartsAt()
        {
            GeoTransform3 askew = GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7);
            GeoPoint3[] corners = { P(0, 0, 0), P(2400, 0, 0), P(2400, 1200, 0), P(0, 1200, 0) };
            GeoPoint3[] moved = corners.Select(askew.Transform).ToArray();

            GeoVector3 first = new GeoPolygon3(moved).ToMesh(MeshOptions.Grid(600, 400), MeshPlacement3.Own, Tolerance).Frame.XAxis;

            for (int start = 1; start < 4; start++)
            {
                GeoPoint3[] rolled = moved.Skip(start).Concat(moved.Take(start)).ToArray();
                GeoVector3 axis = new GeoPolygon3(rolled).ToMesh(MeshOptions.Grid(600, 400), MeshPlacement3.Own, Tolerance).Frame.XAxis;
                Assert.True(axis.IsEqualTo(first, new Tolerance(1E-9, 1E-9)), $"starting at corner {start} the axis is {axis}, not {first}");
            }
        }

        [Fact]
        public void ADirectionLaysTheGridAlongIt()
        {
            GeoPolygon3 face = WallBox(0, 0, 4000, 2000);
            var direction = new GeoVector3(1, 0.5, 1);
            GeoMesh3 mesh = face.ToMesh(MeshOptions.Grid(500, 500), MeshPlacement3.Along(direction), Tolerance);

            // Laid onto the plane y = 0, the direction runs from +X up at 45 degrees.
            Assert.Equal(Math.Sqrt(0.5), mesh.Frame.XAxis.X, 12);
            Assert.Equal(0.0, mesh.Frame.XAxis.Y, 12);
            Assert.Equal(Math.Sqrt(0.5), mesh.Frame.XAxis.Z, 12);
            Mesh3Assert.IsSound(mesh, Mesh3Assert.Locate(face, Tolerance), face.Vertices, 8E6, Tolerance);
        }

        [Fact]
        public void ADirectionSquareToTheFaceIsRefused()
        {
            GeoPolygon3 face = WallBox(0, 0, 4000, 2000);

            Assert.Throws<ArgumentException>(() => face.ToMesh(MeshOptions.Grid(500, 500), MeshPlacement3.Along(new GeoVector3(0, 1, 0)), Tolerance));
        }

        [Fact]
        public void AFrameLaysItsXAxisOntoTheFaceOrItsYWhereXStandsSquare()
        {
            var frame = new GeoCoordinateSystem3(P(0, 0, 0), new GeoVector3(0, 1, 0), new GeoVector3(0, 0, 1));
            GeoPolygon3 front = WallBox(0, 0, 4000, 2000);
            GeoPolygon3 side = new GeoPolygon3(P(0, 0, 0), P(0, 0, 2000), P(0, 4000, 2000), P(0, 4000, 0));

            // The front faces -Y, square to the frame's X axis: it takes the frame's Y axis, which is Z.
            GeoMesh3 onFront = front.ToMesh(MeshOptions.Grid(500, 500), MeshPlacement3.Frame(frame), Tolerance);
            Assert.Equal(1.0, Math.Abs(onFront.Frame.XAxis.Z), 12);

            GeoMesh3 onSide = side.ToMesh(MeshOptions.Grid(500, 500), MeshPlacement3.Frame(frame), Tolerance);
            Assert.Equal(1.0, onSide.Frame.XAxis.Y, 12);
        }

        [Fact]
        public void AnOriginLinesTheRowsUpAcrossTheFacesOfABuilding()
        {
            // Two walls meeting at a corner, panels of 1200 by 600 with joints of 10 from one origin.
            GeoPolygon3 front = WallBox(0, 0, 5000, 2900);
            var side = new GeoPolygon3(P(5000, 0, 0), P(5000, 4000, 0), P(5000, 4000, 2900), P(5000, 0, 2900));
            // The origin stands nine rows above and seven columns beyond the walls; the grid it gives is the one through
            // (-3000, -2000, 150), since the rows stand 610 apart and the columns 1210.
            MeshPlacement3 placement = MeshPlacement3.World.At(P(-3000 - 7 * 1210, -2000 - 7 * 1210, 150 + 9 * 610));
            MeshOptions options = MeshOptions.Grid(1200, 600, joint: 10);

            GeoMesh3 a = front.ToMesh(options, placement, Tolerance);
            GeoMesh3 b = side.ToMesh(options, placement, Tolerance);
            Mesh3Assert.IsSound(a, Mesh3Assert.Locate(front, Tolerance), front.Vertices, a.ToMesh2().Area, Tolerance, covers: false);
            Mesh3Assert.IsSound(b, Mesh3Assert.Locate(side, Tolerance), side.Vertices, b.ToMesh2().Area, Tolerance, covers: false);

            // Every level side of every panel stands at 150 + k 610, or 150 + k 610 + 600, on both walls.
            foreach (GeoMesh3 mesh in new[] { a, b })
            {
                foreach (GeoPoint3 vertex in mesh.Vertices)
                {
                    if (vertex.Z <= 1E-9 || vertex.Z >= 2900 - 1E-9)
                    {
                        continue;
                    }

                    double from = (vertex.Z - 150) % 610;
                    from = from < 0 ? from + 610 : from;
                    Assert.True(Math.Abs(from) < 1E-6 || Math.Abs(from - 600) < 1E-6 || Math.Abs(from - 610) < 1E-6, $"a vertex stands at height {vertex.Z}, off the rows");
                }
            }

            // The columns: along the front from x = -3000, across the side from y = -2000.
            Assert.Contains(a.Vertices, v => Math.Abs(v.X - (-3000 + 3 * 1210)) < 1E-6);
            Assert.Contains(b.Vertices, v => Math.Abs(v.Y - (-2000 + 2 * 1210)) < 1E-6);
        }

        [Fact]
        public void AnOriginLinesTheJointsUpOnEveryFaceOfABox()
        {
            // The six faces of a box laid from one origin, cells of 1200 by 600 ten apart: a face's axis runs along the world's
            // one way or the other, the back wall's level one along -X and the bottom's second along -Y, and laid from the
            // origin along it their cells stood a joint off those of the faces beside them.
            GeoSolid3 box = new GeoAabb3(P(0, 0, 0), P(5000, 3000, 2800)).ToObb().ToSolid();
            MeshOptions options = MeshOptions.Grid(1200, 600, joint: 10);
            MeshPlacement3 placement = MeshPlacement3.World.At(P(0, 0, 0));
            double[] extent = { 5000, 3000, 2800 };

            bool OnLattice(double at, double size)
            {
                double pitch = size + 10;
                double r = at - Math.Floor(at / pitch) * pitch;
                return Math.Abs(r) < 1E-6 || Math.Abs(r - size) < 1E-6 || Math.Abs(r - pitch) < 1E-6;
            }

            foreach (GeoFace3 face in box.Faces)
            {
                GeoMesh3 mesh = face.ToMesh(options, placement, Tolerance);

                for (int axis = 0; axis < 2; axis++)
                {
                    GeoVector3 along = axis == 0 ? mesh.Frame.XAxis : mesh.Frame.YAxis;
                    int world = Math.Abs(along.X) > 0.5 ? 0 : Math.Abs(along.Y) > 0.5 ? 1 : 2;
                    double size = axis == 0 ? 1200 : 600;

                    foreach (GeoPoint3 vertex in mesh.Vertices)
                    {
                        double at = world == 0 ? vertex.X : world == 1 ? vertex.Y : vertex.Z;
                        Assert.True(
                            OnLattice(at, size) || Math.Abs(at) < 1E-6 || Math.Abs(at - extent[world]) < 1E-6,
                            $"the face facing {face.Normal} has a corner at {at} along {"XYZ"[world]}, off the cells laid from the origin");
                    }
                }
            }
        }

        [Fact]
        public void AnOriginFarOutPlacesTheGridAsOneNearBy()
        {
            // A wall seven kilometres out, the origin at the world's: the cells start at whole steps of 1210 and 610.
            var far = new GeoPolygon3(P(7012345.5, 0, 0), P(7017345.5, 0, 0), P(7017345.5, 0, 2900), P(7012345.5, 0, 2900));
            GeoMesh3 mesh = far.ToMesh(MeshOptions.Grid(1200, 600, joint: 10), MeshPlacement3.World.At(P(0, 0, 0)), Tolerance);

            foreach (GeoPoint3 vertex in mesh.Vertices)
            {
                double across = vertex.X % 1210.0;
                bool onColumn = Math.Abs(across) < 1E-5 || Math.Abs(across - 1200.0) < 1E-5 || Math.Abs(across - 1210.0) < 1E-5
                    || vertex.X == 7012345.5 || vertex.X == 7017345.5;
                Assert.True(onColumn, $"a vertex stands at x = {vertex.X:R}, off the columns");
            }

            Mesh3Assert.IsSound(mesh, Mesh3Assert.Locate(far, Tolerance), far.Vertices, mesh.ToMesh2().Area, Tolerance, covers: false);
        }

        [Fact]
        public void AnOriginOfThePlaneIsRefusedInSpace()
        {
            GeoPolygon3 face = WallBox(0, 0, 4000, 2000);

            Assert.Throws<ArgumentException>(() => face.ToMesh(MeshOptions.Grid(500, 500, Q(10, 10)), Tolerance));
        }

        #endregion

        #region Other shapes

        [Fact]
        public void ADiscOfNoSizeHasNoFaces()
        {
            // The disc a default circle is, with no radius and no normal: its frame was laid on the normal, and refused.
            Assert.Equal(0, default(GeoCircle3).ToMesh(MeshKind.Triangles).FaceCount);
            Assert.Equal(0, default(GeoCircle3).ToMesh(MeshOptions.Grid(10, 10), MeshPlacement3.Own, Tolerance).FaceCount);
            Assert.Equal(0, new GeoCircle3(P(1, 2, 3), GeoVector3.ZAxis, 0.005).ToMesh(MeshKind.Convex).FaceCount);
        }

        [Fact]
        public void ADiscInSpaceIsFannedFromItsCenter()
        {
            var circle = new GeoCircle3(P(100, 200, 300), new GeoVector3(1, 1, 1).Normalize(), 1500);
            GeoMesh3 fan = circle.ToMesh(MeshKind.Triangles);
            GeoPolygon3 rim = circle.ToPolygonByChordTolerance(0.0);

            Assert.Equal(rim.VertexCount, fan.FaceCount);
            Assert.Contains(circle.Center, fan.Vertices);
            Assert.True(fan.Normal.IsEqualTo(circle.Normal, new Tolerance(1E-12, 1E-12)));
            Assert.Equal(rim.Area, fan.Area, 3);

            GeoMesh3 grid = circle.ToMesh(MeshOptions.Grid(400, 400, 0, GridAlignment.CenterCell, GridAlignment.CenterCell), Tolerance);
            Assert.Equal(rim.Area, grid.Area, 3);
            Assert.True(Whole(grid) > 20);

            foreach (GeoPoint3 vertex in grid.Vertices)
            {
                Assert.True(vertex.DistanceTo(circle.Center) <= 1500 + 1E-6, $"{vertex} lies outside the disc");
                Assert.True(Math.Abs(circle.Center.GetVectorTo(vertex).DotProduct(circle.Normal)) < 1E-9, $"{vertex} lies off the disc's plane");
            }
        }

        [Fact]
        public void ALoopWithArcsKeepsItsCornersAndRunsAboutTheWayItTurns()
        {
            // A slot standing in the plane x = 1000, its round ends bulging out. Its corners run up, along and down, which
            // seen from +X is clockwise: its mesh runs about -X, the way the loop turns, whichever way its plane faces.
            foreach (GeoVector3 bulgeNormal in new[] { new GeoVector3(-1, 0, 0), new GeoVector3(1, 0, 0) })
            {
                double bulge = bulgeNormal.X < 0 ? 1.0 : -1.0;
                var slot = new GeoPolygonArc3(
                    new[] { P(1000, 0, 0), P(1000, 0, 500), P(1000, 2000, 500), P(1000, 2000, 0) },
                    new[] { bulge, 0.0, bulge, 0.0 },
                    new[] { bulgeNormal, bulgeNormal, bulgeNormal, bulgeNormal });

                GeoMesh3 mesh = slot.ToMesh(MeshOptions.Grid(250, 250), Tolerance);
                double area = slot.ToPolygonArc2().Flatten(0.0).Area;

                Assert.Equal(area, mesh.Area, 3);
                Assert.True(area > 2000 * 500, "the ends bulge out");
                Assert.Equal(-1.0, mesh.Normal.X, 12);

                foreach (GeoPoint3 corner in slot.Vertices)
                {
                    Assert.Contains(corner, mesh.Vertices);
                }

                foreach (GeoPoint3 vertex in mesh.Vertices)
                {
                    Assert.Equal(1000.0, vertex.X, 9);
                }
            }
        }

        [Fact]
        public void ALoopWithArcsMeshesAsItsFlattenedPolygon()
        {
            var slot = new GeoPolygonArc3(
                new[] { P(0, 0, 0), P(2000, 0, 0), P(2000, 0, 500), P(0, 0, 500) },
                new[] { 0.0, 1.0, 0.0, 1.0 },
                new[] { new GeoVector3(0, -1, 0), new GeoVector3(0, -1, 0), new GeoVector3(0, -1, 0), new GeoVector3(0, -1, 0) });
            GeoMesh3 mesh = slot.ToMesh(MeshKind.Triangles);

            Assert.Equal(slot.ToPolygonArc2().Flatten(0.0).Area, mesh.Area, 3);
            Assert.Equal(-1.0, mesh.Normal.Y, 12);

            foreach (GeoPoint3 corner in slot.Vertices)
            {
                Assert.Contains(corner, mesh.Vertices);
            }

            foreach (GeoPoint3 vertex in mesh.Vertices)
            {
                Assert.Equal(0.0, vertex.Y, 9);
            }
        }

        [Fact]
        public void ALoopWithArcsWhoseCornersCloseInOnEachOtherKeepsEachCornerItsOwn()
        {
            // Two corners 0.005 apart, apart within a tolerance of a millionth: laid out in the plane they were taken for one
            // within the global tolerance, and every corner after it was put back onto the next corner in space.
            var fine = new Tolerance(1E-6, 1E-9, 1E-6, 1E-6);
            GeoPoint3[] corners = { P(0, 0, 0), P(1000, 0, 0), P(1000, 0.005, 0), P(1000, 500, 0), P(0, 500, 0) };
            GeoMesh3 loop = new GeoPolygonArc3(corners, null, null, fine).ToMesh(MeshOptions.Triangles, fine);
            GeoMesh3 polygon = new GeoPolygon3(corners, fine).ToMesh(MeshOptions.Triangles, fine);

            Assert.Equal(polygon.Area, loop.Area, 6);
            Assert.All(corners, c => Assert.Contains(c, loop.Vertices));

            // A loop a hair out of flat, two of its corners 0.0126 apart in space and 0.004 in its plane, at the default
            // tolerance: its grid covered nine tenths of it.
            GeoPoint3[] tilted = { P(0, 0, 0), P(1000, 0, -0.006), P(1000, 0.004, 0.006), P(1000, 1000, 0), P(0, 1000, 0) };
            GeoMesh3 grid = new GeoPolygonArc3(tilted).ToMesh(MeshOptions.Grid(300, 300), Tolerance);

            Assert.Equal(new GeoPolygon3(tilted, Tolerance).ToMesh(MeshOptions.Grid(300, 300), Tolerance).Area, grid.Area, 6);
            Assert.InRange(grid.Area, 1E6 - 10, 1E6);

            // A sliver the plane's tolerance would take for no loop at all is meshed as its polygon is, and not refused.
            GeoPoint3[] sliver = { P(0, 0, 0), P(1000, 0, 0), P(1000, 0.005, 0), P(0, 0.005, 0) };
            Assert.Equal(new GeoPolygon3(sliver, fine).ToMesh(MeshOptions.Triangles, fine).Area, new GeoPolygonArc3(sliver, null, null, fine).ToMesh(MeshOptions.Triangles, fine).Area, 9);
        }

        [Fact]
        public void ATriangleOfSpaceMeshesAsAPolygonOfThree()
        {
            var triangle = new GeoTriangle3(P(0, 0, 0), P(3000, 0, 1000), P(0, 2000, 500));
            GeoMesh3 one = triangle.ToMesh(MeshKind.Triangles);

            Assert.Equal(1, one.FaceCount);
            Assert.Equal(triangle.Area, one.Area, 6);
            Assert.True(one.Normal.IsEqualTo(triangle.Normal, new Tolerance(1E-12, 1E-12)));

            GeoMesh3 grid = triangle.ToMesh(MeshOptions.Grid(300, 300), Tolerance);
            Assert.Equal(triangle.Area, grid.Area, 3);
            Mesh3Assert.IsSound(grid, p => triangle.Locate(p, Tolerance), new[] { triangle.A, triangle.B, triangle.C }, triangle.Area, Tolerance);
        }

        [Fact]
        public void ATriangleWithNoAreaHasNoFaces()
        {
            var flat = new GeoTriangle3(P(0, 0, 0), P(1000, 0, 0), P(2000, 0, 0));

            Assert.Equal(0, flat.ToMesh(MeshKind.Triangles).FaceCount);
        }

        #endregion

        #region A face a hair out of flat

        [Fact]
        public void TheTrianglesOfAShapeAHairOutOfFlatHoldTheirOwnCorners()
        {
            // Corners up to 0.0089 off the plane through the first: a triangle of the mesh turned about the mesh's normal, its
            // plane through its first corner, which stood off its others by more than the tolerance it holds them within.
            var polygon = new GeoPolygon3(
                new[] { P(1162.6, 1637.3, 0.0003), P(1289.7, 1951.2, 0.0042), P(300, 2064.8, -0.0012), P(-232.5, 2022, -0.0062), P(-2183.5, 751.7, 0.0085), P(-1062.7, -1443.7, -0.0014), P(-485.3, -2080.2, -0.0089), P(1999, -600.1, 0) },
                Tolerance);
            GeoMesh3 mesh = polygon.ToMesh(MeshOptions.Triangles, Tolerance);

            foreach (GeoPolygon3 face in mesh.GetFaces())
            {
                Assert.All(face.Vertices, v => Assert.True(face.Contains(v, Tolerance), $"{face} does not hold its corner {v}"));
                Assert.True(face.Normal.DotProduct(mesh.Normal) > 0.999999);
            }
        }

        [Fact]
        public void AFaceAHairOutOfFlatKeepsItsCornersAndItsSides()
        {
            // A slab face from a modeller: its corners stand up to 4 thousandths off the plane.
            var corners = new[] { P(0, 0, 0.004), P(6000, 0, -0.003), P(6000, 4000, 0.002), P(0, 4000, -0.004) };
            var face = new GeoFace3(new GeoPolygon3(corners, Tolerance), null, Tolerance);
            GeoMesh3 mesh = face.ToMesh(MeshOptions.Grid(700, 700), Tolerance);

            foreach (GeoPoint3 corner in corners)
            {
                Assert.Contains(corner, mesh.Vertices);
            }

            // A vertex on a side stands on the side in space, at the same share of its length.
            int onSides = 0;

            foreach (GeoPoint3 vertex in mesh.Vertices)
            {
                for (int i = 0; i < 4; i++)
                {
                    GeoPoint3 a = corners[i];
                    GeoPoint3 b = corners[(i + 1) % 4];
                    double t = a.GetVectorTo(vertex).DotProduct(a.GetVectorTo(b)) / a.GetVectorTo(b).LengthSquared;

                    if (t > 1E-9 && t < 1 - 1E-9 && new GeoLine3(a, b).DistanceTo(vertex) < 0.05)
                    {
                        onSides++;
                        Assert.True(new GeoLine3(a, b).DistanceTo(vertex) < 1E-9, $"{vertex} stands {new GeoLine3(a, b).DistanceTo(vertex)} off its side");
                    }
                }
            }

            Assert.True(onSides >= 2 * (9 + 6) - 4, $"only {onSides} vertices stand on the sides");

            // The triangles take the corners where they are too.
            GeoPoint3[] triangleCorners = mesh.ToTriangles().SelectMany(tr => new[] { tr.A, tr.B, tr.C }).ToArray();

            foreach (GeoPoint3 corner in corners)
            {
                Assert.Contains(corner, triangleCorners);
            }
            Mesh3Assert.IsSound(mesh, Mesh3Assert.Locate(face, Tolerance), corners, mesh.Area, Tolerance);
            Assert.Equal(face.Area, mesh.Area, 2);
        }

        #endregion

        #region The mesh

        [Fact]
        public void TheMeshMovesAndTurnsWithItsFrame()
        {
            GeoFace3 wall = Wall();
            GeoMesh3 mesh = wall.ToMesh(MeshOptions.Grid(1200, 600, joint: 3), Tolerance);

            GeoMesh3 moved = mesh.Translate(new GeoVector3(10, 20, 30));
            Assert.Equal(mesh.Vertices[5].Add(new GeoVector3(10, 20, 30)), moved.Vertices[5]);
            Assert.Equal(mesh.Area, moved.Area, 9);
            Assert.Equal(mesh.Frame.XAxis, moved.Frame.XAxis);
            Assert.Equal(mesh.Frame.Origin.Add(new GeoVector3(10, 20, 30)), moved.Frame.Origin);
            Mesh3Assert.IsSound(moved, Mesh3Assert.Locate(wall.Translate(new GeoVector3(10, 20, 30)), Tolerance), null, moved.Area, Tolerance, covers: false);

            GeoTransform3 turn = GeoTransform3.RotationAxis(new GeoVector3(3, -1, 2), 1.1);
            GeoMesh3 turned = mesh.TransformBy(turn);
            Assert.Equal(mesh.Area, turned.Area, 6);
            Assert.True(turned.Normal.IsEqualTo(turn.Transform(mesh.Normal), new Tolerance(1E-9, 1E-9)));
            Mesh3Assert.IsSound(turned, Mesh3Assert.Locate(wall.TransformBy(turn), Tolerance), null, turned.Area, Tolerance, covers: false);

            // A mirror turns the normal round with the faces' winding, and the faces still run about it.
            GeoMesh3 mirrored = mesh.TransformBy(GeoTransform3.Mirror(new GeoPlane3(P(0, 0, 0), new GeoVector3(1, 0, 0))));
            Assert.Equal(mesh.Area, mirrored.Area, 6);
            Assert.Equal(1.0, mirrored.Normal.Y, 12);
            Mesh3Assert.IsSound(mirrored, Mesh3Assert.Locate(wall.TransformBy(GeoTransform3.Mirror(new GeoPlane3(P(0, 0, 0), new GeoVector3(1, 0, 0)))), Tolerance), null, mirrored.Area, Tolerance, covers: false);

            // A scaling stretches the faces in the plane as well.
            GeoMesh3 stretched = mesh.TransformBy(GeoTransform3.Scaling(2.0));
            Assert.Equal(4.0 * mesh.Area, stretched.Area, 3);
            Assert.Equal(4.0 * mesh.Area, stretched.ToMesh2().Area, 3);
        }

        [Theory]
        [InlineData(0.05)]
        [InlineData(0.01)]
        [InlineData(1E-6)]
        public void AMeshScaledDownToADrawingKeepsItsFaces(double scale)
        {
            // A scaling of 1:20, or less: the frame was built from its axes as the scaling left them, shorter than the global
            // vector tolerance, and refused.
            var plate = new GeoPolygon3(P(0, 0, 0), P(2000, 0, 0), P(2000, 1000, 0), P(0, 1000, 0));
            GeoMesh3 mesh = plate.ToMesh(MeshOptions.Grid(500, 500), Tolerance);
            GeoMesh3 drawn = mesh.TransformBy(GeoTransform3.Scaling(scale));

            Assert.Equal(mesh.FaceCount, drawn.FaceCount);
            Assert.Equal(1.0, drawn.Area / (2E6 * scale * scale), 9);
            Assert.Equal(1.0, drawn.ToMesh2().Area / (2E6 * scale * scale), 9);
            Assert.Equal(1.0, drawn.Normal.Z, 12);
            Assert.Equal(1.0, drawn.Frame.XAxis.Length, 12);

            // Flattened into a line, the plane has no frame.
            Assert.Throws<ArgumentException>(() => mesh.TransformBy(GeoTransform3.Scaling(1, 0, 1)));
        }

        [Fact]
        public void EdgesBoundaryAndNeighboursAreThoseOfTheLayout()
        {
            GeoPolygon3 face = WallBox(0, 0, 3000, 2000);
            GeoMesh3 mesh = face.ToMesh(MeshOptions.Grid(1000, 1000), Tolerance);
            GeoMesh2 flat = mesh.ToMesh2();

            Assert.Equal(flat.GetEdges().Length, mesh.GetEdges().Length);
            Assert.Equal(flat.GetBoundaryEdges().Length, mesh.GetBoundaryEdges().Length);
            Assert.Equal(17, mesh.GetEdges().Length);
            Assert.Equal(10, mesh.GetBoundaryEdges().Length);
            Assert.Equal(10000.0, mesh.GetBoundaryEdges().Sum(e => e.Length), 6);

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                Assert.Equal(flat.GetAdjacentFaces(f), mesh.GetAdjacentFaces(f));
            }

            // The boundary runs with the material on its left, seen from the normal.
            foreach (GeoLine3 edge in mesh.GetBoundaryEdges())
            {
                GeoVector3 along = edge.StartPoint.GetVectorTo(edge.EndPoint);
                GeoPoint3 inward = edge.StartPoint.GetMiddlePoint(edge.EndPoint).Add(mesh.Normal.CrossProduct(along).Normalize().Multiply(1.0));
                Assert.Equal(PointLocation.Inside, face.Locate(inward, Tolerance));
            }
        }

        [Fact]
        public void AnObjFileGivesTheCellsAsTheyAre()
        {
            GeoFace3 wall = Wall();
            GeoMesh3 mesh = wall.ToMesh(MeshOptions.Grid(1200, 600, joint: 3), Tolerance);
            string obj = new ObjWriter().Add(mesh, "panels").ToString();
            string[] lines = obj.Split('\n');

            Assert.Contains("o panels", lines);
            int vertices = lines.Count(l => l.StartsWith("v ", StringComparison.Ordinal));
            int faces = lines.Count(l => l.StartsWith("f ", StringComparison.Ordinal));
            Assert.True(vertices >= mesh.VertexCount);

            // Every whole panel is one polygon of four corners.
            Assert.True(lines.Count(l => l.StartsWith("f ", StringComparison.Ordinal) && l.Split(' ').Length == 5) >= Whole(mesh));
            Assert.True(faces >= mesh.FaceCount);
        }

        [Fact]
        public void AnObjFileWritesAFaceTurningRightAtACornerAsItsTriangles()
        {
            // An L whose notch falls inside one cell: that cell is an L too, and a viewer fanning it would cover the notch.
            var l = new GeoPolygon3(P(0, 0, 0), P(2000, 0, 0), P(2000, 1000, 0), P(1500, 1000, 0), P(1500, 1500, 0), P(0, 1500, 0));
            GeoMesh3 mesh = l.ToMesh(MeshOptions.Grid(1200, 1200), Tolerance);
            int convex = 0;
            int concave = 0;

            foreach (GeoPolygon2 face in mesh.ToMesh2().GetFaces())
            {
                bool turnsRight = false;

                for (int i = 0; i < face.VertexCount; i++)
                {
                    GeoPoint2 a = face[(i + face.VertexCount - 1) % face.VertexCount];
                    GeoPoint2 b = face[i];
                    GeoPoint2 c = face[(i + 1) % face.VertexCount];
                    turnsRight |= (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X) < -1E-6;
                }

                if (turnsRight)
                {
                    concave++;
                }
                else if (face.VertexCount > 3)
                {
                    convex++;
                }
            }

            Assert.Equal(1, concave);
            string[] lines = new ObjWriter().Add(mesh, "l").ToString().Split('\n');
            var vertices = lines.Where(s => s.StartsWith("v ", StringComparison.Ordinal))
                .Select(s => s.Split(' ').Skip(1).Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray())
                .Select(c => Q(c[0], c[1]))
                .ToArray();
            int polygons = 0;

            foreach (string line in lines.Where(s => s.StartsWith("f ", StringComparison.Ordinal)))
            {
                int[] corners = line.Split(' ').Skip(1).Select(int.Parse).ToArray();

                if (corners.Length == 3)
                {
                    continue;
                }

                polygons++;

                for (int i = 0; i < corners.Length; i++)
                {
                    GeoPoint2 a = vertices[corners[(i + corners.Length - 1) % corners.Length] - 1];
                    GeoPoint2 b = vertices[corners[i] - 1];
                    GeoPoint2 c = vertices[corners[(i + 1) % corners.Length] - 1];
                    double turn = (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
                    Assert.True(turn >= -1E-6, $"the polygon {line} turns right at a corner");
                }
            }

            Assert.Equal(convex, polygons);
        }

        [Fact]
        public void NullsAreRefused()
        {
            GeoPolygon3 face = WallBox(0, 0, 3000, 2000);

            Assert.Throws<ArgumentNullException>(() => Mesh3.ToMesh((GeoPolygon3)null, MeshOptions.Triangles));
            Assert.Throws<ArgumentNullException>(() => Mesh3.ToMesh((GeoFace3)null, MeshOptions.Triangles));
            Assert.Throws<ArgumentNullException>(() => Mesh3.ToMesh((GeoPolygonArc3)null, MeshOptions.Triangles));
            Assert.Throws<ArgumentNullException>(() => face.ToMesh((MeshOptions)null));
            Assert.Throws<ArgumentNullException>(() => face.ToMesh(MeshOptions.Triangles, (MeshPlacement3)null));
            Assert.Throws<ArgumentException>(() => face.ToMesh(MeshKind.Grid));
        }

        #endregion

        #region The placement

        [Fact]
        public void PlacementsCompareByWhatTheySay()
        {
            Assert.Equal(MeshPlacement3.World, MeshPlacement3.World);
            Assert.NotEqual(MeshPlacement3.World, MeshPlacement3.Own);
            Assert.Equal(MeshPlacement3.Along(new GeoVector3(2, 0, 0)), MeshPlacement3.Along(new GeoVector3(1, 0, 0)));
            Assert.Equal(MeshPlacement3.World.At(P(1, 2, 3)), MeshPlacement3.World.At(P(1, 2, 3)));
            Assert.NotEqual(MeshPlacement3.World.At(P(1, 2, 3)), MeshPlacement3.World);
            Assert.Equal(MeshPlacement3.World.At(P(1, 2, 3)).GetHashCode(), MeshPlacement3.World.At(P(1, 2, 3)).GetHashCode());
            Assert.Equal(MeshAxes.Frame, MeshPlacement3.Frame(GeoCoordinateSystem3.Global).Axes);
            Assert.Equal(GeoCoordinateSystem3.Global, MeshPlacement3.Frame(GeoCoordinateSystem3.Global).CoordinateSystem);
            Assert.Equal(new GeoVector3(1, 0, 0), MeshPlacement3.Along(new GeoVector3(5, 0, 0)).Direction);
            Assert.Contains("Upright", MeshPlacement3.Upright.ToString());
            Assert.Contains("Origin", MeshPlacement3.Own.At(P(1, 2, 3)).ToString());
        }

        [Fact]
        public void APlacementRefusesWhatGivesNoWay()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshPlacement3.Along(new GeoVector3(0, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshPlacement3.Along(new GeoVector3(double.NaN, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeshPlacement3.World.At(P(double.PositiveInfinity, 0, 0)));
        }

        #endregion
    }
}
