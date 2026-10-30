using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// <see cref="DxfEntity.Explode"/> of the entities AutoCAD explodes that the library refused before: a polyface and a polygon
    /// mesh into 3DFACEs, an MLINE into its lines and caps, a splined leader into a SPLINE and its arrowhead, a pattern hatch into
    /// its lines, a multileader into its words, lines and arrows; the bodies of AutoCAD's modeler still refuse, saying why.
    /// </summary>
    public class DxfExplodeMoreTests
    {
        [Fact]
        public void APolyfaceMeshExploded_GivesA3DFaceForEachFace_ItsHiddenEdgesHiddenAndATriangleEndingOnItsThird()
        {
            // As ezdxf's virtual_entities gives them: a pyramid of a square base 10 wide and 6 high, its base hiding the edge from
            // its second corner, a side hiding the edge from its third back to its first, and one side in colour 3 of its own.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var mesh = new DxfPolyFaceMesh();
            db.ModelSpace.AppendEntity(mesh);
            mesh.ColorIndex = 5;
            mesh.LineWeight = DxfLineWeight.LineWeight035;

            foreach (GeoPoint3 point in new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0), new GeoPoint3(5, 5, 6) })
            {
                mesh.AppendVertex(new DxfPolyFaceMeshVertex(point));
            }

            mesh.AppendFaceRecord(new DxfFaceRecord(1, -2, 3, 4));
            mesh.AppendFaceRecord(new DxfFaceRecord(1, 2, -5, 0));
            var green = new DxfFaceRecord(2, 3, 5, 0);
            green.ColorIndex = 3;
            mesh.AppendFaceRecord(green);

            DxfFace[] faces = mesh.Explode().Cast<DxfFace>().ToArray();

            Assert.Equal(3, faces.Length);
            Assert.Equal(Bits(new GeoPoint3(10, 0, 0)), Bits(faces[0].GetVertexAt(1)));
            Assert.Equal(Bits(new GeoPoint3(0, 10, 0)), Bits(faces[0].GetVertexAt(3)));
            Assert.Equal(new[] { true, false, true, true }, Enumerable.Range(0, 4).Select(i => faces[0].IsEdgeVisibleAt((short)i)));
            Assert.Equal(Bits(new GeoPoint3(5, 5, 6)), Bits(faces[1].GetVertexAt(2)));
            Assert.Equal(Bits(new GeoPoint3(5, 5, 6)), Bits(faces[1].GetVertexAt(3)));

            // The edge from the third corner back to the first is the fourth of a 3DFACE of three corners: hidden with the third.
            Assert.Equal(new[] { true, true, false, false }, Enumerable.Range(0, 4).Select(i => faces[1].IsEdgeVisibleAt((short)i)));
            Assert.Equal(new[] { 5, 5, 3 }, faces.Select(f => f.ColorIndex));
            Assert.All(faces, f => Assert.Equal(DxfLineWeight.LineWeight035, f.LineWeight));

            foreach (DxfFace face in faces)
            {
                db.ModelSpace.AppendEntity(face);
            }

            Assert.Equal(3, DxfDatabase.Parse(db.ToText()).ModelSpace.OfType<DxfFace>().Count(e => faces.Any(f => f.Handle == e.Handle)));
        }

        [Fact]
        public void ThePolyfaceMeshOfEzdxf_ExplodesIntoTheFacesOfItsCube()
        {
            foreach (string[] drawing in new[] { new[] { DxfFileEzdxfDrawings.R2000, "4E" }, new[] { DxfFileEzdxfDrawings.R12, "51" } })
            {
                DxfPolyFaceMesh mesh = ByHandle<DxfPolyFaceMesh>(DxfDatabase.Parse(drawing[0]), drawing[1]);

                DxfFace[] faces = mesh.Explode().Cast<DxfFace>().ToArray();

                Assert.Equal(mesh.OfType<DxfFaceRecord>().Count(), faces.Length);
                Assert.All(faces, f => Assert.Equal(mesh.Layer, f.Layer));
                Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(faces[0].GetVertexAt(0)));
            }
        }

        [Fact]
        public void APolygonMeshExploded_GivesA3DFaceForEachCell_RowAfterRow_ClosedWhereItIsClosed()
        {
            // As ezdxf's virtual_entities gives them: a grid of 3 rows of 4 points 10 apart, each cell from its corner (m, n)
            // to (m + 1, n), (m + 1, n + 1) and (m, n + 1).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var points = new List<GeoPoint3>();

            for (int m = 0; m < 3; m++)
            {
                for (int n = 0; n < 4; n++)
                {
                    points.Add(new GeoPoint3(n * 10, m * 10, m * n));
                }
            }

            var open = new DxfPolygonMesh(DxfPolyMeshType.SimpleMesh, 3, 4, points, false, false);
            var closed = new DxfPolygonMesh(DxfPolyMeshType.SimpleMesh, 3, 4, points, true, true);
            db.ModelSpace.AppendEntity(open);
            db.ModelSpace.AppendEntity(closed);
            open.Layer = "Wand€Außen";

            DxfFace[] cells = open.Explode().Cast<DxfFace>().ToArray();
            DxfFace[] round = closed.Explode().Cast<DxfFace>().ToArray();

            Assert.Equal(6, cells.Length);
            Assert.Equal(
                new[] { Bits(points[0]), Bits(points[4]), Bits(points[5]), Bits(points[1]) },
                Enumerable.Range(0, 4).Select(i => Bits(cells[0].GetVertexAt((short)i))));
            Assert.Equal(Bits(points[7]), Bits(cells[2].GetVertexAt(2)));
            Assert.Equal(Bits(points[4]), Bits(cells[3].GetVertexAt(0)));
            Assert.All(cells, c => Assert.Equal("Wand€Außen", c.Layer));
            Assert.All(cells, c => Assert.True(Enumerable.Range(0, 4).All(i => c.IsEdgeVisibleAt((short)i))));
            Assert.Equal(12, round.Length);
            Assert.Equal(
                new[] { Bits(points[3]), Bits(points[7]), Bits(points[4]), Bits(points[0]) },
                Enumerable.Range(0, 4).Select(i => Bits(round[3].GetVertexAt((short)i))));
            Assert.Equal(
                new[] { Bits(points[11]), Bits(points[3]), Bits(points[0]), Bits(points[8]) },
                Enumerable.Range(0, 4).Select(i => Bits(round[11].GetVertexAt((short)i))));
        }

        [Fact]
        public void ASmoothedPolygonMeshExploded_GivesTheFacesOfTheSurfaceItShows_NotOfItsFrame()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var points = new List<GeoPoint3>();

            for (int m = 0; m < 4; m++)
            {
                for (int n = 0; n < 4; n++)
                {
                    points.Add(new GeoPoint3(n * 10, m * 10, (m % 2) * (n % 2) * 5));
                }
            }

            var mesh = new DxfPolygonMesh(DxfPolyMeshType.SimpleMesh, 4, 4, points, false, false);
            db.ModelSpace.AppendEntity(mesh);
            mesh.SurfaceFit(DxfPolyMeshType.CubicSurfaceMesh, 5, 6);
            List<GeoPoint3> surface = mesh.Where(v => v.VertexType == DxfVertex3dType.FitVertex).Select(v => v.Position).ToList();

            DxfFace[] faces = mesh.Explode().Cast<DxfFace>().ToArray();

            // 5 by 6 spans of an open mesh, 6 rows of 7 points as AutoCAD's PEDIT smooths (check 14), 5 by 6 faces.
            Assert.Equal(42, surface.Count);
            Assert.Equal(5 * 6, faces.Length);
            Assert.Equal(Bits(surface[0]), Bits(faces[0].GetVertexAt(0)));
            Assert.Equal(Bits(surface[7]), Bits(faces[0].GetVertexAt(1)));
            Assert.Equal(Bits(surface[41]), Bits(faces[29].GetVertexAt(2)));

            mesh.Straighten();
            Assert.Equal(9, mesh.Explode().Count);
        }

        [Fact]
        public void AnMLineOfTheStyleStandardExploded_GivesALineForEachElementAlongEachSegment()
        {
            // Through (0, 0), (20, 0) and (20, 10), the two elements of Standard 0.5 either side, the corner mitered.
            DxfDatabase db = WithRecords(Mline("B02", "Standard", "22", 1, Corner(new[] { 0.5, 0.0 }, new[] { -0.5, 0.0 })), string.Empty);
            var mline = ByHandle<DxfUnknownEntity>(db, "B02");

            DxfLine[] lines = mline.Explode().Cast<DxfLine>().ToArray();

            Assert.Equal(4, lines.Length);
            Near(new GeoPoint3(0, 0.5, 0), lines[0].StartPoint);
            Near(new GeoPoint3(19.5, 0.5, 0), lines[0].EndPoint);
            Near(new GeoPoint3(0, -0.5, 0), lines[1].StartPoint);
            Near(new GeoPoint3(20.5, -0.5, 0), lines[1].EndPoint);
            Near(new GeoPoint3(19.5, 10, 0), lines[2].EndPoint);
            Near(new GeoPoint3(20.5, 10, 0), lines[3].EndPoint);
            Assert.All(lines, l => Assert.Equal(256, l.ColorIndex));
            Assert.All(lines, l => Assert.Equal("BYLAYER", l.Linetype));

            foreach (DxfLine line in lines)
            {
                db.ModelSpace.AppendEntity(line);
            }

            Assert.Equal(4, DxfDatabase.Parse(db.ToText()).ModelSpace.Count(e => lines.Any(l => l.Handle == e.Handle)));
        }

        [Fact]
        public void AnMLineBrokenAlongASegment_GivesALineForEachPieceLeft()
        {
            // The lower element of the first segment broken from 5 to 8 along it, as MLEDIT cuts one: its parameters -0.5, 0, 5, 8.
            DxfDatabase db = WithRecords(Mline("B03", "Standard", "22", 1, Corner(new[] { 0.5, 0.0 }, new[] { -0.5, 0.0, 5.0, 8.0 })), string.Empty);

            DxfLine[] lines = ByHandle<DxfUnknownEntity>(db, "B03").Explode().Cast<DxfLine>().ToArray();

            Assert.Equal(5, lines.Length);
            Near(new GeoPoint3(0, -0.5, 0), lines[1].StartPoint);
            Near(new GeoPoint3(5, -0.5, 0), lines[1].EndPoint);
            Near(new GeoPoint3(8, -0.5, 0), lines[2].StartPoint);
            Near(new GeoPoint3(20.5, -0.5, 0), lines[2].EndPoint);
        }

        [Fact]
        public void AnMLineOfAStyleWithCapsAndMiters_GivesThemInAutoCADsOrder_AndLeavesOffTheCapsItSuppresses()
        {
            // A style of two elements, red 0.5 to the left and blue 0.5 to the right, with lines across both ends, an outer arc at
            // both ends and a miter line at each joint (70 of 2 + 16 + 64 + 256 + 1024).
            const string Caps = "  0\nMLINESTYLE\n  5\nB01\n330\n10\n100\nAcDbMlineStyle\n  2\nCAPS\n 70\n1362\n  3\n\n 62\n256\n 51\n90.0\n 52\n90.0\n 71\n2\n 49\n0.5\n 62\n1\n  6\nBYLAYER\n 49\n-0.5\n 62\n5\n  6\nBYLAYER\n";
            string corner = Corner(new[] { 0.5, 0.0 }, new[] { -0.5, 0.0 });
            DxfDatabase db = WithRecords(Mline("B04", "CAPS", "B01", 1, corner) + Mline("B05", "CAPS", "B01", 1 | 4 | 8, corner), Caps);

            DxfEntity[] parts = ByHandle<DxfUnknownEntity>(db, "B04").Explode().ToArray();
            DxfEntity[] bare = ByHandle<DxfUnknownEntity>(db, "B05").Explode().ToArray();

            Assert.Equal(
                new[] { "Line", "Line", "Arc", "Arc", "Line", "Line", "Line", "Line", "Line", "Line", "Line", "Line", "Arc", "Arc" },
                parts.Select(p => p.GetType().Name.Substring(3)));
            Assert.Equal(new[] { 1, 5, 1, 5, 1, 5, 1, 5, 1, 5, 1, 5, 5, 1 }, parts.Select(p => p.ColorIndex));
            Near(new GeoPoint3(0, 0, 0), ((DxfLine)parts[0]).EndPoint);
            var start = (DxfArc)parts[2];
            Near(new GeoPoint3(0, 0, 0), start.Center);
            Assert.Equal(0.5, start.Radius, 12);
            Near(new GeoPoint3(0, 0.5, 0), start.StartPoint);
            Near(new GeoPoint3(-0.5, 0, 0), start.EndPoint);
            Near(new GeoPoint3(20, 0, 0), ((DxfLine)parts[8]).EndPoint);
            var end = (DxfArc)parts[12];
            Near(new GeoPoint3(20, 10, 0), end.Center);
            Near(new GeoPoint3(20.5, 10, 0), end.StartPoint);
            Near(new GeoPoint3(20, 10.5, 0), end.EndPoint);
            Assert.Equal(6, bare.Length);
            Assert.All(bare, p => Assert.IsType<DxfLine>(p));
        }

        [Fact]
        public void AnMLineWhoseStyleIsNotInItsDrawing_AndTheBodiesOfAutoCADsModeler_Refuse()
        {
            const string Region = "  0\nREGION\n  5\nB07\n330\n17\n100\nAcDbEntity\n  8\n0\n100\nAcDbModelerGeometry\n 70\n1\n";
            const string Solid = "  0\n3DSOLID\n  5\nB08\n330\n17\n100\nAcDbEntity\n  8\n0\n100\nAcDbModelerGeometry\n 70\n1\n";
            DxfDatabase db = WithRecords(Mline("B06", "NONE", "FFF", 1, Corner(new[] { 0.5, 0.0 }, new[] { -0.5, 0.0 })) + Region + Solid, string.Empty);

            Assert.Throws<InvalidOperationException>(() => ByHandle<DxfUnknownEntity>(db, "B06").Explode());
            Assert.Contains("modeler", Assert.Throws<NotSupportedException>(() => ByHandle<DxfUnknownEntity>(db, "B07").Explode()).Message, StringComparison.Ordinal);
            Assert.Contains("modeler", Assert.Throws<NotSupportedException>(() => ByHandle<DxfUnknownEntity>(db, "B08").Explode()).Message, StringComparison.Ordinal);
            Assert.Contains("eNotApplicable", Assert.Throws<NotSupportedException>(() => db.ModelSpace.OfType<DxfCircle>().First().Explode()).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ASplinedLeaderExploded_GivesItsArrowhead_ThenTheSplineFromTheArrowsBack_ItsKnotsTheChords()
        {
            // As AutoCAD 2021's Explode gives it: the arrowhead, then a cubic through the four vertices from the arrow's back by
            // the chord, leaving along the first segment and arriving along the last, its knots the lengths of the chords.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var leader = new DxfLeader();

            foreach (GeoPoint3 vertex in new[] { new GeoPoint3(0, 20, 0), new GeoPoint3(10, 30, 0), new GeoPoint3(25, 28, 0), new GeoPoint3(40, 35, 0) })
            {
                leader.AppendVertex(vertex);
            }

            leader.IsSplined = true;
            db.ModelSpace.AppendEntity(leader);
            leader.ColorIndex = 3;

            DxfEntity[] parts = leader.Explode().ToArray();

            Assert.Equal(new[] { "DxfSolid", "DxfSpline" }, parts.Select(p => p.GetType().Name));
            DxfNurbsData nurbs = ((DxfSpline)parts[1]).NurbsData;
            Assert.Equal(3, nurbs.Degree);
            double size = leader.Dimasz * (leader.Dimscale > 0.0 ? leader.Dimscale : 1.0);
            GeoPoint3 back = new GeoPoint3(0, 20, 0).Add(new GeoVector3(10, 10, 0).Normalize().Multiply(size));
            Assert.Equal(6, nurbs.GetControlPoints().Count);
            Assert.True(back.DistanceTo(nurbs.GetControlPoints()[0]) < 1E-9);
            Assert.True(new GeoPoint3(40, 35, 0).DistanceTo(nurbs.GetControlPoints()[5]) < 1E-9);
            double c1 = back.DistanceTo(new GeoPoint3(10, 30, 0));
            double c2 = c1 + new GeoPoint3(10, 30, 0).DistanceTo(new GeoPoint3(25, 28, 0));
            double c3 = c2 + new GeoPoint3(25, 28, 0).DistanceTo(new GeoPoint3(40, 35, 0));
            Assert.Equal(new[] { 0, 0, 0, 0, c1, c2, c3, c3, c3, c3 }.Select(k => Math.Round(k, 9)), nurbs.GetKnots().Select(k => Math.Round(k, 9)));
            Assert.All(parts, p => Assert.Equal(3, p.ColorIndex));
            Assert.Equal(Bits(new GeoPoint3(0, 20, 0)), Bits(((DxfSolid)parts[0]).GetPointAt(1)));

            foreach (DxfEntity part in parts)
            {
                db.ModelSpace.AppendEntity(part);
            }

            Assert.Equal(2, DxfDatabase.Parse(db.ToText()).ModelSpace.Count(e => parts.Any(p => p.Handle == e.Handle)));
        }

        [Fact]
        public void AHatchOfLinesExploded_GivesTheLinesInsideItsLoops_ByTheEvenOddRuleOfItsStyle()
        {
            // A square 30 by 20 round a circle of radius 5 round a square 4 by 4, lines at 45 degrees 3.175 apart, as ANSI31.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var centre = new GeoPoint3(15, 10, 0);
            DxfHatch Made(DxfHatchStyle style, bool islands)
            {
                var hatch = new DxfHatch { ColorIndex = 4 };
                hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
                hatch.PatternAngle = Math.PI / 4.0;
                hatch.PatternSpace = 3.175;
                hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) }, null);

                if (islands)
                {
                    hatch.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.Outermost, new object[] { new GeoCircle3(centre, new GeoVector3(0, 0, 1), 5) }));
                    hatch.AppendLoop(DxfHatchLoopTypes.Default, new[] { new GeoPoint2(13, 8), new GeoPoint2(17, 8), new GeoPoint2(17, 12), new GeoPoint2(13, 12) }, null);
                }

                hatch.HatchStyle = style;
                db.ModelSpace.AppendEntity(hatch);
                return hatch;
            }

            DxfLine[] plain = Made(DxfHatchStyle.Normal, false).Explode().Cast<DxfLine>().ToArray();
            DxfLine[] normal = Made(DxfHatchStyle.Normal, true).Explode().Cast<DxfLine>().ToArray();
            DxfLine[] outer = Made(DxfHatchStyle.Outer, true).Explode().Cast<DxfLine>().ToArray();
            DxfLine[] ignore = Made(DxfHatchStyle.Ignore, true).Explode().Cast<DxfLine>().ToArray();

            DxfHatch circled = Made(DxfHatchStyle.Normal, false);
            circled.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.Outermost, new object[] { new GeoCircle3(centre, new GeoVector3(0, 0, 1), 5) }));

            Assert.Equal(11, plain.Length);
            Assert.Equal(14, circled.Explode().Count);
            Assert.All(plain, l => Assert.Equal(4, l.ColorIndex));
            Assert.All(plain, l => Assert.Equal(l.StartPoint.X - l.StartPoint.Y, l.EndPoint.X - l.EndPoint.Y, 9));
            Assert.Equal(ignore.Select(l => Bits(l.StartPoint) + Bits(l.EndPoint)), plain.Select(l => Bits(l.StartPoint) + Bits(l.EndPoint)));

            // Each end on the square, the circle or the small square, to the bit of a double; no line runs inside the circle but
            // in the small square, where the normal style hatches again and the outer style does not.
            bool OnEdge(GeoPoint3 p)
                => Math.Abs(p.X) < 1E-9 || Math.Abs(p.X - 30) < 1E-9 || Math.Abs(p.Y) < 1E-9 || Math.Abs(p.Y - 20) < 1E-9
                    || Math.Abs(p.DistanceTo(centre) - 5) < 1E-9 || ((Math.Abs(p.X - 13) < 1E-9 || Math.Abs(p.X - 17) < 1E-9 || Math.Abs(p.Y - 8) < 1E-9 || Math.Abs(p.Y - 12) < 1E-9) && p.DistanceTo(centre) < 3);

            GeoPoint3 Middle(DxfLine l) => new GeoPoint3((l.StartPoint.X + l.EndPoint.X) / 2, (l.StartPoint.Y + l.EndPoint.Y) / 2, 0);
            bool InSmall(GeoPoint3 p) => p.X > 13 && p.X < 17 && p.Y > 8 && p.Y < 12;

            Assert.All(normal.Concat(outer), l => Assert.True(OnEdge(l.StartPoint) && OnEdge(l.EndPoint), l.StartPoint + " " + l.EndPoint));
            Assert.All(normal, l => Assert.True(Middle(l).DistanceTo(centre) > 5 || InSmall(Middle(l))));
            Assert.Contains(normal, l => InSmall(Middle(l)));
            Assert.All(outer, l => Assert.True(Middle(l).DistanceTo(centre) > 5));
            Assert.Equal(outer.Length + normal.Count(l => InSmall(Middle(l))), normal.Length);
        }

        [Fact]
        public void AHatchOfDashesAndDots_LaysThemFromTheBaseOfEachLine_ADotALineOfNoLength()
        {
            // One pattern line along x, 2 apart, a dash 2, a gap 1, a dot and a gap 1, in the square from (0, 0.5) to (10, 9.5):
            // the lines y = 2, 4, 6 and 8, each a dash from 0 to 2, a dot at 3, a dash from 4 to 6, a dot at 7, a dash from 8 to 10.
            const string Dashed = "  0\nHATCH\n  5\nB10\n330\n17\n100\nAcDbEntity\n  8\n0\n 62\n1\n100\nAcDbHatch\n 10\n0.0\n 20\n0.0\n 30\n0.0\n210\n0.0\n220\n0.0\n230\n1.0\n"
                + "  2\nGH_DASHED\n 70\n0\n 71\n0\n 91\n1\n 92\n3\n 72\n0\n 73\n1\n 93\n4\n 10\n0.0\n 20\n0.5\n 10\n10.0\n 20\n0.5\n 10\n10.0\n 20\n9.5\n 10\n0.0\n 20\n9.5\n 97\n0\n"
                + " 75\n0\n 76\n2\n 52\n0.0\n 41\n1.0\n 77\n0\n 78\n1\n 53\n0.0\n 43\n0.0\n 44\n0.0\n 45\n0.0\n 46\n2.0\n 79\n4\n 49\n2.0\n 49\n-1.0\n 49\n0.0\n 49\n-1.0\n 98\n0\n";
            DxfDatabase db = WithRecords(Dashed, string.Empty);

            DxfLine[] parts = ByHandle<DxfHatch>(db, "B10").Explode().Cast<DxfLine>().ToArray();

            Assert.Equal(20, parts.Length);
            Assert.Equal(
                new[] { 0.0, 2, 3, 3, 4, 6, 7, 7, 8, 10 },
                parts.Take(5).SelectMany(l => new[] { Math.Round(l.StartPoint.X, 12), Math.Round(l.EndPoint.X, 12) }));
            Assert.Equal(new[] { 2.0, 4, 6, 8 }, parts.Where((l, i) => i % 5 == 0).Select(l => l.StartPoint.Y));
            Assert.All(parts, l => Assert.Equal(1, l.ColorIndex));
        }

        [Fact]
        public void AHatchOnATiltedNormal_ExplodesIntoLinesInItsPlane_AndASolidFillAGradientAndAPatternItHoldsNoLinesOfRefuse()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfHatch user = ByHandle<DxfHatch>(db, "AE");
            GeoVector3 normal = user.Normal;

            DxfLine[] lines = user.Explode().Cast<DxfLine>().ToArray();

            Assert.NotEmpty(lines);
            Assert.All(lines, l => Assert.Equal(user.Elevation, normal.X * l.StartPoint.X + normal.Y * l.StartPoint.Y + normal.Z * l.StartPoint.Z, 9));
            Assert.All(lines, l => Assert.Equal(user.Elevation, normal.X * l.EndPoint.X + normal.Y * l.EndPoint.Y + normal.Z * l.EndPoint.Z, 9));
            Assert.Throws<NotSupportedException>(() => ByHandle<DxfHatch>(db, "AD").Explode());
            Assert.Throws<NotSupportedException>(() => ByHandle<DxfHatch>(db, "AF").Explode());
            var named = new DxfHatch();
            named.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
            named.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10) }, null);
            Assert.Contains("ANSI31", Assert.Throws<NotSupportedException>(() => named.Explode()).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AMultileaderExploded_GivesItsWordsOrBlock_ThenItsDoglegAndForEachLineAPolylineAndItsArrow()
        {
            // As AutoCAD 2021's Explode gives them, the closed filled arrow a SOLID: the words "note" held at (0, 20), the dogleg
            // 8 long from (-10, 18) to (-2, 18), the leader a polyline from 4 along from (-20, 20) to (-10, 18), then its arrow.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader words = ByHandle<DxfMLeader>(db, "8A");
            DxfMLeader block = ByHandle<DxfMLeader>(db, "BA");

            DxfEntity[] parts = words.Explode().ToArray();
            DxfEntity[] marked = block.Explode().ToArray();

            Assert.Equal(new[] { "DxfMText", "DxfLine", "DxfPolyline", "DxfSolid" }, parts.Select(p => p.GetType().Name));
            var mtext = (DxfMText)parts[0];
            Assert.Equal("note", mtext.Contents);
            Assert.Equal(Bits(new GeoPoint3(0, 20, 0)), Bits(mtext.Location));
            var dogleg = (DxfLine)parts[1];
            Near(new GeoPoint3(-10, 18, 0), dogleg.StartPoint);
            Near(new GeoPoint3(-2, 18, 0), dogleg.EndPoint);
            GeoVector3 along = new GeoPoint3(-20, 20, 0).GetVectorTo(new GeoPoint3(-10, 18, 0)).Normalize();
            var line = (DxfPolyline)parts[2];
            Assert.Equal(2, line.NumberOfVertices);
            Near(new GeoPoint3(-20, 20, 0).Add(along.Multiply(4.0)), line.GetPoint3dAt(0));
            Near(new GeoPoint3(-10, 18, 0), line.GetPoint3dAt(1));
            var arrow = (DxfSolid)parts[3];
            Assert.Equal(Bits(new GeoPoint3(-20, 20, 0)), Bits(arrow.GetPointAt(1)));
            Assert.All(parts.Skip(1).Take(2), p => Assert.Equal(DxfLineWeight.ByBlock, p.LineWeight));
            Assert.Equal(words.LineWeight, arrow.LineWeight);

            var insert = Assert.IsType<DxfBlockReference>(marked[0]);
            Assert.Equal("GKTAG", insert.Name);
            Assert.Equal(15.0, insert.Rotation * 180.0 / Math.PI, 9);
            Assert.Equal(new[] { "DxfBlockReference", "DxfLine", "DxfPolyline", "DxfSolid" }, marked.Select(p => p.GetType().Name));

            foreach (DxfEntity part in parts.Concat(marked))
            {
                db.ModelSpace.AppendEntity(part);
            }

            Assert.Equal(8, DxfDatabase.Parse(db.ToText()).ModelSpace.Count(e => parts.Concat(marked).Any(p => p.Handle == e.Handle)));
        }

        [Fact]
        public void AClosedMLine_GivesEachElementAlongEverySegment_TheClosingOneAmongThem_AMiterAtEveryJoint_AndNoCaps()
        {
            // The square of side 20 closed, of the style CAPS (caps at both ends, miters), its elements 0.5 inside and outside.
            const string Caps = "  0\nMLINESTYLE\n  5\nB01\n330\n10\n100\nAcDbMlineStyle\n  2\nCAPS\n 70\n1362\n  3\n\n 62\n256\n 51\n90.0\n 52\n90.0\n 71\n2\n 49\n0.5\n 62\n1\n  6\nBYLAYER\n 49\n-0.5\n 62\n5\n  6\nBYLAYER\n";
            double s = Math.Sqrt(0.5);
            double[] Both(double sign) => new[] { sign * 0.5 / s, 0.0 };
            string vertices = Vertex(new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { s, s, 0 }, Both(1), Both(-1))
                + Vertex(new[] { 20.0, 0, 0 }, new[] { 0.0, 1, 0 }, new[] { -s, s, 0 }, Both(1), Both(-1))
                + Vertex(new[] { 20.0, 20, 0 }, new[] { -1.0, 0, 0 }, new[] { -s, -s, 0 }, Both(1), Both(-1))
                + Vertex(new[] { 0.0, 20, 0 }, new[] { 0.0, -1, 0 }, new[] { s, -s, 0 }, Both(1), Both(-1));
            DxfDatabase db = WithRecords(Mline("B09", "CAPS", "B01", 1 | 2, vertices, 4), Caps);

            DxfEntity[] parts = ByHandle<DxfUnknownEntity>(db, "B09").Explode().ToArray();

            Assert.Equal(16, parts.Length);
            Assert.All(parts, p => Assert.IsType<DxfLine>(p));
            var lines = parts.Cast<DxfLine>().ToArray();
            Near(new GeoPoint3(0.5, 0.5, 0), lines[0].StartPoint);
            Near(new GeoPoint3(19.5, 0.5, 0), lines[0].EndPoint);
            Near(new GeoPoint3(-0.5, 20.5, 0), lines[7].StartPoint);
            Near(new GeoPoint3(-0.5, -0.5, 0), lines[7].EndPoint);
            Near(new GeoPoint3(0.5, 0.5, 0), lines[8].StartPoint);
            Near(new GeoPoint3(0, 0, 0), lines[8].EndPoint);
            Near(new GeoPoint3(0.5, 19.5, 0), lines[14].StartPoint);
        }

        [Fact]
        public void ASplinedMultileaderExploded_GivesItsDogleg_ThenOneSplineFromTheBackOfItsArrowToTheStartOfTheDogleg()
        {
            // As AutoCAD 2021's Explode gives it: the leader 8A splined runs from 4 back from (-20, 20) towards (-10, 18) and
            // arrives there along its dogleg, (1, 0), its knots the length of its chord; the dogleg a LINE of its own.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader leader = ByHandle<DxfMLeader>(db, "8A");
            leader.LeaderLineType = DxfLeaderType.SplineLeader;

            DxfEntity[] parts = leader.Explode().ToArray();

            Assert.Equal(new[] { "DxfMText", "DxfLine", "DxfSpline", "DxfSolid" }, parts.Select(p => p.GetType().Name));
            DxfNurbsData nurbs = ((DxfSpline)parts[2]).NurbsData;
            GeoVector3 along = new GeoVector3(10, -2, 0).Normalize();
            GeoPoint3 back = new GeoPoint3(-20, 20, 0).Add(along.Multiply(4.0));
            double chord = back.DistanceTo(new GeoPoint3(-10, 18, 0));
            GeoPoint3[] expected = { back, back.Add(along.Multiply(chord / 3.0)), new GeoPoint3(-10 - chord / 3.0, 18, 0), new GeoPoint3(-10, 18, 0) };
            Assert.Equal(4, nurbs.GetControlPoints().Count);
            Assert.All(expected.Zip(nurbs.GetControlPoints(), (e, a) => e.DistanceTo(a)), d => Assert.True(d < 1E-8, d.ToString()));
            Assert.Equal(new[] { 0.0, 0, 0, 0, chord, chord, chord, chord }.Select(k => Math.Round(k, 9)), nurbs.GetKnots().Select(k => Math.Round(k, 9)));
        }

        [Fact]
        public void AMultileaderOfABlockExploded_GivesItsAttributesTheValuesItHolds_PlacedTurnedAndScaledWithTheBlock()
        {
            // BA places GKTAG at (900, 0, 0) twice the size, turned by 15 degrees; A at (0, 1) and B at (0, -1) of the block, 1 high.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader leader = ByHandle<DxfMLeader>(db, "BA");
            DxfBlockTableRecord block = leader.BlockContent;
            var a = new DxfAttributeDefinition(new GeoPoint3(0, 1, 0), "a", "A", string.Empty, null) { Height = 1.0 };
            var b = new DxfAttributeDefinition(new GeoPoint3(0, -1, 0), "b", "B", string.Empty, null) { Height = 1.0 };
            block.AppendEntity(a);
            block.AppendEntity(b);
            leader.SetBlockAttribute(a, new DxfAttributeReference { TextString = "x1" });
            leader.SetBlockAttribute(b, new DxfAttributeReference { TextString = "y2" });

            var insert = (DxfBlockReference)leader.Explode()[0];
            DxfAttributeReference[] attributes = insert.AttributeCollection.ToArray();

            Assert.Equal(new[] { "NUM 42", "A x1", "B y2" }, attributes.Select(t => t.Tag + " " + t.TextString));
            double turn = 15.0 * Math.PI / 180.0;
            Near(new GeoPoint3(900 - 2 * Math.Sin(turn), 2 * Math.Cos(turn), 0), attributes[1].Position);
            Near(new GeoPoint3(900 + 2 * Math.Sin(turn), -2 * Math.Cos(turn), 0), attributes[2].Position);
            Assert.Equal(2.0, attributes[1].Height, 12);
            Assert.Equal(turn, attributes[1].Rotation, 12);
        }

        [Fact]
        public void AHatchOfMoreThanTheBoundOfParts_OrSoFarFromItsBaseThatItsLinesAreOne_IsRefusedBeforeAnythingIsMade()
        {
            // One line of dashes 0.0005 long and 0.001 apart along a stretch 2 000.0005 long: 2 000 001 dashes, one past the 2 000 000;
            // 1 999.9995 long it gives 2 000 000 and is exploded, which takes 6 s and is not tested here.
            const string Dense = "  0\nHATCH\n  5\nB10\n330\n17\n100\nAcDbEntity\n  8\n0\n100\nAcDbHatch\n 10\n0.0\n 20\n0.0\n 30\n0.0\n210\n0.0\n220\n0.0\n230\n1.0\n"
                + "  2\nGH_DENSE\n 70\n0\n 71\n0\n 91\n1\n 92\n3\n 72\n0\n 73\n1\n 93\n4\n 10\n0.0\n 20\n-0.5\n 10\n2000.0005\n 20\n-0.5\n 10\n2000.0005\n 20\n0.5\n 10\n0.0\n 20\n0.5\n 97\n0\n"
                + " 75\n0\n 76\n2\n 52\n0.0\n 41\n1.0\n 77\n0\n 78\n1\n 53\n0.0\n 43\n0.0\n 44\n0.0\n 45\n0.0\n 46\n1000.0\n 79\n2\n 49\n0.0005\n 49\n-0.0005\n 98\n0\n";
            DxfDatabase db = WithRecords(Dense, string.Empty);
            var far = new DxfHatch();
            far.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            far.PatternSpace = 1E-6;
            far.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 1E10), new GeoPoint2(1, 1E10), new GeoPoint2(1, 1E10 + 1E-4), new GeoPoint2(0, 1E10 + 1E-4) }, null);

            Assert.Contains("2000000", Assert.Throws<InvalidOperationException>(() => ByHandle<DxfHatch>(db, "B10").Explode()).Message, StringComparison.Ordinal);
            Assert.Contains("2^52", Assert.Throws<InvalidOperationException>(() => far.Explode()).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AHatchOfAThousandAndOneSquaresSideBySide_GivesOneLineAcrossThemAll_ItsFourMillionCrossingsHeld()
        {
            // Lines along x 0.005 apart across 1 001 squares 10 by 10 in a row: 2 000 lines, each crossing 2 002 edges, joined
            // where two squares meet, from x = 0 to 10 010.
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternSpace = 0.005;

            for (int i = 0; i < 1001; i++)
            {
                hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(i * 10, 0), new GeoPoint2(i * 10 + 10, 0), new GeoPoint2(i * 10 + 10, 10), new GeoPoint2(i * 10, 10) }, null);
            }

            DxfLine[] lines = hatch.Explode().Cast<DxfLine>().ToArray();

            Assert.Equal(2000, lines.Length);
            Assert.All(lines, l => Assert.Equal(10010.0, l.EndPoint.X - l.StartPoint.X, 9));
        }

        [Fact]
        public void AHatchRoundAnArcOfAHugeStartAngle_IsCutAsTheSameArcWithinOneTurn()
        {
            // A circle of radius 5 about (50, 5) given as an arc edge from 1E20 to 1E20 degrees, a whole turn, and from 7E18 to
            // 7E18 + 360, lines along x 1.5 apart: the lines y = 1.5 to 9, 6 of them, as the same circle from 0 to 360 gives.
            string Circle(string handle, string from, string to)
                => "  0\nHATCH\n  5\n" + handle + "\n330\n17\n100\nAcDbEntity\n  8\n0\n100\nAcDbHatch\n 10\n0.0\n 20\n0.0\n 30\n0.0\n210\n0.0\n220\n0.0\n230\n1.0\n"
                    + "  2\nGH_ROUND\n 70\n0\n 71\n0\n 91\n1\n 92\n1\n 93\n1\n 72\n2\n 10\n50.0\n 20\n5.0\n 40\n5.0\n 50\n" + from + "\n 51\n" + to + "\n 73\n1\n 97\n0\n"
                    + " 75\n0\n 76\n2\n 52\n0.0\n 41\n1.0\n 77\n0\n 78\n1\n 53\n0.0\n 43\n0.0\n 44\n0.0\n 45\n0.0\n 46\n1.5\n 79\n0\n 98\n0\n";
            DxfDatabase db = WithRecords(Circle("B11", "0.0", "360.0") + Circle("B12", "1E20", "1E20") + Circle("B13", "7E18", "7000000000000000360"), string.Empty);

            DxfLine[] plain = ByHandle<DxfHatch>(db, "B11").Explode().Cast<DxfLine>().ToArray();

            Assert.Equal(6, plain.Length);

            foreach (string handle in new[] { "B12", "B13" })
            {
                DxfLine[] huge = ByHandle<DxfHatch>(db, handle).Explode().Cast<DxfLine>().ToArray();

                Assert.Equal(6, huge.Length);
                Assert.All(huge.Zip(plain, (a, b) => a.StartPoint.DistanceTo(b.StartPoint) + a.EndPoint.DistanceTo(b.EndPoint)), d => Assert.True(d < 1E-9, d.ToString()));
            }
        }

        /// <summary>The drawing of ezdxf's R2018 with entities added at the end of its ENTITIES and objects at the end of its OBJECTS, its handle seed raised past them.</summary>
        private static DxfDatabase WithRecords(string entities, string objects)
        {
            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\nC00\n");
            int end = text.IndexOf("  0\nENDSEC\n", text.IndexOf("\nENTITIES\n", StringComparison.Ordinal), StringComparison.Ordinal);
            text = text.Insert(end, entities);
            end = text.IndexOf("  0\nENDSEC\n", text.IndexOf("\nOBJECTS\n", StringComparison.Ordinal), StringComparison.Ordinal);
            return DxfDatabase.Parse(text.Insert(end, objects));
        }

        /// <summary>
        /// The vertices of an MLINE through (0, 0), (20, 0) and (20, 10), its corner mitered, as ezdxf writes them: each with the
        /// direction of the segment it starts and its miter, and the parameters of its two elements, those of the corner's
        /// offsets stretched by the miter.
        /// </summary>
        private static string Corner(double[] upper, double[] lower)
        {
            double s = Math.Sqrt(0.5);
            double[] Stretched(double[] p) => new[] { p[0] / s, 0.0 };
            return Vertex(new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, upper, lower)
                + Vertex(new[] { 20.0, 0, 0 }, new[] { 0.0, 1, 0 }, new[] { -s, s, 0 }, Stretched(upper), Stretched(lower))
                + Vertex(new[] { 20.0, 10, 0 }, new[] { 0.0, 1, 0 }, new[] { -1.0, 0, 0 }, new[] { upper[0], 0.0 }, new[] { lower[0], 0.0 });
        }

        private static string Vertex(double[] at, double[] along, double[] miter, params double[][] elements)
        {
            var text = new System.Text.StringBuilder();

            foreach (var point in new[] { new { Code = 11, Value = at }, new { Code = 12, Value = along }, new { Code = 13, Value = miter } })
            {
                for (int i = 0; i < 3; i++)
                {
                    text.Append(' ').Append(point.Code + 10 * i).Append('\n').Append(Number(point.Value[i])).Append('\n');
                }
            }

            foreach (double[] element in elements)
            {
                text.Append(" 74\n").Append(element.Length).Append('\n');

                foreach (double parameter in element)
                {
                    text.Append(" 41\n").Append(Number(parameter)).Append('\n');
                }

                text.Append(" 75\n0\n");
            }

            return text.ToString();
        }

        /// <summary>An MLINE of colour 2 in model space, of a style by its name and handle and of flags (71), justified on its zero line, through its vertices, three where not said.</summary>
        private static string Mline(string handle, string style, string styleHandle, int flags, string vertices, int count = 3)
            => "  0\nMLINE\n  5\n" + handle + "\n330\n17\n100\nAcDbEntity\n  8\n0\n 62\n2\n100\nAcDbMline\n  2\n" + style + "\n340\n" + styleHandle
                + "\n 40\n1.0\n 70\n1\n 71\n" + flags + "\n 72\n" + count + "\n 73\n2\n 10\n0.0\n 20\n0.0\n 30\n0.0\n210\n0.0\n220\n0.0\n230\n1.0\n" + vertices;

        private static string Number(double value) => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            Assert.True(expected.DistanceTo(actual) < 1E-9, expected + " was " + actual);
        }
    }
}
