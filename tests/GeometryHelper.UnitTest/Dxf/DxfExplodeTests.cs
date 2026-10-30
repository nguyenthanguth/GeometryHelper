using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// <see cref="DxfEntity.Explode"/> of the entities AutoCAD explodes: polylines into lines and arcs, an MTEXT into texts, a
    /// dimension and a table into their pictures, a MINSERT into every cell, a leader into its lines or spline and arrowhead, a
    /// multileader into its words and leaders; the entities that do not break refuse.
    /// </summary>
    public class DxfExplodeTests
    {
        [Fact]
        public void APolylineExploded_GivesALineOrAnArcForEachSegment_WithItsLook()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var square = ByHandle<DxfPolyline>(db, "6C");

            DxfEntity[] parts = square.Explode().ToArray();

            // A closed square of side 10, its second side bulged by -0.25.
            Assert.Equal(new[] { "DxfLine", "DxfArc", "DxfLine", "DxfLine" }, parts.Select(p => p.GetType().Name));
            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(((DxfLine)parts[0]).StartPoint));
            Assert.Equal(10.0, ((DxfLine)parts[0]).EndPoint.X, 12);
            var arc = (DxfArc)parts[1];
            Assert.Equal(10.0, arc.StartPoint.DistanceTo(arc.EndPoint), 9);
            Assert.All(parts, p => Assert.Equal(square.LineWeight, p.LineWeight));
            Assert.All(parts, p => Assert.Equal(square.TrueColor, p.TrueColor));

            foreach (DxfEntity part in parts)
            {
                db.ModelSpace.AppendEntity(part);
            }

            Assert.Equal(4, DxfDatabase.Parse(db.ToText()).ModelSpace.Count(e => parts.Any(p => p.Handle == e.Handle)));
        }

        [Fact]
        public void PolylinesOfVertices_ExplodeIntoLines_ThreeDimensionalOnesTooAndClosedOnesClosed()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            DxfEntity[] flat = ByHandle<DxfPolyline2d>(db, "49").Explode().ToArray();
            DxfEntity[] space = ByHandle<DxfPolyline3d>(db, "44").Explode().ToArray();

            Assert.Equal(3, flat.Length);
            Assert.All(flat, p => Assert.IsType<DxfLine>(p));
            Assert.All(flat, p => Assert.Equal("Wand€Außen", p.Layer));
            Assert.Equal(2, space.Length);
            Assert.Equal(Bits(new GeoPoint3(4, 5, 6)), Bits(((DxfLine)space[1]).EndPoint));
        }

        [Fact]
        public void AnMTextExploded_GivesATextForEachLine()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var mtext = ByHandle<DxfMText>(db, "73");

            DxfDBText[] texts = mtext.Explode().Cast<DxfDBText>().ToArray();

            Assert.Equal(new[] { "Bold", "line two é" }, texts.Select(t => t.TextString));
            Assert.Equal(40.0 - 3.0, texts[0].Position.Y, 12);
            Assert.All(texts, t => Assert.Equal(3.0, t.Height));
        }

        [Fact]
        public void ADimensionAndATableExploded_GiveTheirPicturesPlacedWhereTheyDrawThem()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDimension dimension = db.ModelSpace.OfType<DxfDimension>().Single(d => d.Handle == "61");
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();

            DxfEntity[] lines = dimension.Explode().ToArray();
            DxfEntity[] cells = table.Explode().ToArray();

            Assert.Equal(dimension.DimBlock.Count(e => !(e is DxfDBPoint && e.Layer == "Defpoints")), lines.Length);
            DxfLine first = dimension.DimBlock.OfType<DxfLine>().First();
            Assert.Equal(Bits(first.StartPoint), Bits(lines.OfType<DxfLine>().First().StartPoint));
            Assert.Equal(table.BlockTableRecord.Count(), cells.Length);
            DxfLine top = cells.OfType<DxfLine>().First();
            Assert.Equal(Bits(new GeoPoint3(300.0, -100.0, 0.0)), Bits(top.StartPoint));
            Assert.Equal(Bits(new GeoPoint3(370.0, -100.0, 0.0)), Bits(top.EndPoint));
            Assert.Contains(cells, c => c is DxfMText m && m.Contents == "Parts");
        }

        [Fact]
        public void AMInsertExploded_GivesTheBlockInEveryCell()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var ring = new DxfBlockTableRecord { Name = "GH_RING" };
            db.BlockTable.Add(ring);
            ring.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 2.0));
            var array = new DxfMInsertBlock(new GeoPoint3(100, 0, 0), ring, 3, 2, 40.0, 25.0);
            db.ModelSpace.AppendEntity(array);

            DxfCircle[] circles = array.Explode().Cast<DxfCircle>().ToArray();

            Assert.Equal(6, circles.Length);
            Assert.Equal(new[] { 100.0, 140.0, 180.0, 100.0, 140.0, 180.0 }, circles.Select(c => Math.Round(c.Center.X, 9)));
            Assert.Equal(new[] { 0.0, 0.0, 0.0, 25.0, 25.0, 25.0 }, circles.Select(c => Math.Round(c.Center.Y, 9)));
            Assert.Throws<NotSupportedException>(() => array.ExplodeToOwnerSpace());
        }

        [Fact]
        public void AStraightLeaderExploded_GivesItsArrowheadAndOnePolyline_AndASplinedOneASpline()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var leader = ByHandle<DxfLeader>(db, "89");
            double size = leader.Dimasz * (leader.Dimscale > 0.0 ? leader.Dimscale : 1.0);

            DxfEntity[] parts = leader.Explode().ToArray();

            // As AutoCAD 2021's Explode gives them: the arrowhead, its corner on the right, the tip and its corner on the left,
            // then one polyline from the arrow's back.
            Assert.Equal(new[] { "DxfSolid", "DxfPolyline" }, parts.Select(p => p.GetType().Name));
            var line = (DxfPolyline)parts[1];
            Assert.Equal(Bits(new GeoPoint3(20, 10, 0)), Bits(line.GetPoint3dAt(line.NumberOfVertices - 1)));
            var head = (DxfSolid)parts[0];
            GeoPoint3 tip = head.GetPointAt(1);
            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(tip));
            GeoPoint3 a = head.GetPointAt(0);
            GeoPoint3 b = head.GetPointAt(2);
            Assert.Equal(size, tip.DistanceTo(new GeoPoint3((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0)), 9);
            Assert.Equal(size / 3.0, a.DistanceTo(b), 9);

            Assert.IsType<DxfPolyline>(Assert.Single(ByHandle<DxfLeader>(db, "B3").Explode()));
            Assert.Single(ByHandle<DxfLeader>(db, "B2").Explode().OfType<DxfSpline>());
            Assert.IsType<DxfMText>(ByHandle<DxfMLeader>(db, "8A").Explode()[0]);
            Assert.Throws<NotSupportedException>(() => db.ModelSpace.OfType<DxfCircle>().First().Explode());
        }

        [Fact]
        public void APolylineOnTheNormalDown_ExplodesIntoLinesAndAnArcOnThatNormal_TheirWallsDownToo()
        {
            // As ezdxf's virtual_entities gives them: the arc on (0, 0, -1) about (-10, 5, -5), from 270 to 90 degrees.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0, 1, 1);
            polyline.AddVertexAt(1, new GeoPoint2(10, 0), 1, 0, 0);
            polyline.AddVertexAt(2, new GeoPoint2(10, 10), 0, 0, 0);
            polyline.Closed = true;
            polyline.Elevation = 5;
            polyline.Thickness = 2;
            polyline.Normal = new GeoVector3(0, 0, -1);
            db.ModelSpace.AppendEntity(polyline);

            DxfEntity[] parts = polyline.Explode().ToArray();

            Assert.Equal(new[] { "DxfLine", "DxfArc", "DxfLine" }, parts.Select(p => p.GetType().Name));
            var line = (DxfLine)parts[0];
            Assert.Equal(Bits(new GeoVector3(0, 0, -1)), Bits(line.Normal));
            Assert.Equal(2.0, line.Thickness);
            Near(new GeoPoint3(-10, 0, -5), line.EndPoint);
            var arc = (DxfArc)parts[1];
            Assert.Equal(Bits(new GeoVector3(0, 0, -1)), Bits(arc.Normal));
            Assert.Equal(2.0, arc.Thickness);
            Near(new GeoPoint3(-10, 5, -5), arc.Center);
            Assert.Equal(1.5 * Math.PI, arc.StartAngle, 9);
            Assert.Equal(0.5 * Math.PI, arc.EndAngle, 9);
            Near(new GeoPoint3(-10, 0, -5), arc.StartPoint);
            Near(new GeoPoint3(-10, 10, -5), arc.EndPoint);
            Assert.Equal(Bits(new GeoVector3(0, 0, -1)), Bits(((DxfLine)parts[2]).Normal));
        }

        [Fact]
        public void APolylineOfVerticesOnATiltedNormal_ExplodesIntoArcsOnThatNormal_EitherWayTheyBulge()
        {
            // As ezdxf's virtual_entities gives them: both arcs on the polyline's normal, the second, bulged -0.5, run from its end.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var polyline = new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0) }, 3, false, 0, 0, new[] { 0.5, -0.5, 0.0 });
            polyline.Thickness = 1.5;
            polyline.Normal = new GeoVector3(0.3, -0.4, 0.866);
            db.ModelSpace.AppendEntity(polyline);
            GeoVector3 normal = polyline.Normal;

            DxfArc[] arcs = polyline.Explode().Cast<DxfArc>().ToArray();

            Assert.Equal(2, arcs.Length);
            Assert.All(arcs, a => Assert.Equal(Bits(normal), Bits(a.Normal)));
            Assert.All(arcs, a => Assert.Equal(1.5, a.Thickness));
            Assert.All(arcs, a => Assert.Equal(6.25, a.Radius, 9));
            Near(new GeoPoint3(2.951477, 4.398031, 4.473098), arcs[0].Center);
            Near(new GeoPoint3(0.90002, -1.200026, 2.598057), arcs[0].StartPoint);
            Near(new GeoPoint3(8.90002, 4.799974, 2.598057), arcs[0].EndPoint);
            Near(new GeoPoint3(9.301963, 10.51405, 5.098112), arcs[1].Center);
            Near(new GeoPoint3(3.703905, 11.728126, 7.598167), arcs[1].StartPoint);
            Near(new GeoPoint3(8.90002, 4.799974, 2.598057), arcs[1].EndPoint);
        }

        [Fact]
        public void AClosedPolylineOfTwoVertices_ExplodesIntoOnePartForEachSegment_AsADonutDoes()
        {
            // As ezdxf's virtual_entities gives them: a donut of two half circles about (5, 0, 0), (-5, 0, 0) on (0, 0, -1).
            foreach (double z in new[] { 1.0, -1.0 })
            {
                DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
                var donut = new DxfPolyline();
                donut.AddVertexAt(0, new GeoPoint2(0, 0), 1, 0, 0);
                donut.AddVertexAt(1, new GeoPoint2(10, 0), 1, 0, 0);
                donut.Closed = true;
                donut.Normal = new GeoVector3(0, 0, z);
                var ring = new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, 0, true, 0, 0, new[] { 1.0, 1.0 });
                ring.Normal = new GeoVector3(0, 0, z);
                var half = new DxfPolyline();
                half.AddVertexAt(0, new GeoPoint2(0, 0), 1, 0, 0);
                half.AddVertexAt(1, new GeoPoint2(10, 0), 0, 0, 0);
                half.Closed = true;
                half.Normal = new GeoVector3(0, 0, z);
                db.ModelSpace.AppendEntity(donut);
                db.ModelSpace.AppendEntity(ring);
                db.ModelSpace.AppendEntity(half);
                var far = new GeoPoint3(10 * z, 0, 0);

                foreach (DxfEntity polyline in new DxfEntity[] { donut, ring })
                {
                    DxfArc[] arcs = polyline.Explode().Cast<DxfArc>().ToArray();

                    Assert.Equal(2, arcs.Length);
                    Assert.All(arcs, a => Near(new GeoPoint3(5 * z, 0, 0), a.Center));
                    Assert.All(arcs, a => Assert.Equal(Bits(new GeoVector3(0, 0, z)), Bits(a.Normal)));
                    Assert.All(arcs, a => Assert.Equal(Math.PI, a.TotalAngle, 9));
                    Near(new GeoPoint3(0, 0, 0), arcs[0].StartPoint);
                    Near(far, arcs[0].EndPoint);
                    Near(far, arcs[1].StartPoint);
                    Near(new GeoPoint3(0, 0, 0), arcs[1].EndPoint);
                }

                DxfEntity[] parts = half.Explode().ToArray();
                Assert.Equal(new[] { "DxfArc", "DxfLine" }, parts.Select(p => p.GetType().Name));
                Assert.Equal(Math.PI, ((DxfArc)parts[0]).TotalAngle, 9);
            }
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            Assert.True(expected.DistanceTo(actual) < 1E-5, expected + " was " + actual);
        }
    }
}
