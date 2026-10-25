using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The POLYLINE of the plane as a <see cref="DxfPolyline2d"/> with its <see cref="DxfVertex2d"/> records: read as ezdxf 1.4.5
    /// reads it, walked, its vertices set in their own records, made in code for R12 to R2018, erased, and moved as ezdxf
    /// moves one.
    /// </summary>
    public class DxfPolyline2dTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        #region Read as ezdxf reads it

        [Fact]
        public void ThePolylinesOfThePlaneOfEzdxf_AreReadAsEzdxfReadsThem()
        {
            // As ezdxf 1.4.5 reads them: 49 of R2000 closed on the layer Wand€Außen, its vertices 4B, 4C, 4D at (0, 0),
            // (5, 0), (5, 5), owner 17 as the file writes it; 4C of R12 the same, with no handles of its vertices' owners.
            foreach (KeyValuePair<string, string> drawing in new[] { new KeyValuePair<string, string>(DxfFileEzdxfDrawings.R2000, "49"), new KeyValuePair<string, string>(DxfFileEzdxfDrawings.R12, "4C") })
            {
                DxfDatabase db = DxfDatabase.Parse(drawing.Key);
                DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(db, drawing.Value);
                List<DxfVertex2d> vertices = polyline.ToList();

                Assert.True(polyline.Closed);
                Assert.Equal(0.0, polyline.Elevation);
                Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(polyline.Normal));
                Assert.Equal(DxfPoly2dType.SimplePoly, polyline.PolyType);
                Assert.Equal(3, vertices.Count);
                Assert.Equal(Bits(new GeoPoint3(5, 0, 0)), Bits(vertices[1].Position));
                Assert.Equal(Bits(new GeoPoint3(5, 5, 0)), Bits(polyline.VertexPosition(vertices[2])));
                Assert.Equal(0.0, vertices[1].Bulge);
                Assert.Equal(DxfVertex2dType.SimpleVertex, vertices[0].VertexType);
                Assert.Same(vertices[1], polyline.ToList()[1]);
                Assert.Equal(polyline.Layer, vertices[0].Layer);
                Assert.Same(polyline, vertices[0].Owner);
                Assert.Equal(polyline.Handle, vertices[0].OwnerHandle ?? polyline.Handle);
                Assert.Equal(10.0 + Math.Sqrt(50.0), polyline.Length);
                Assert.Equal(12.5, polyline.Area);
            }

            DxfPolyline2d r2000 = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "49");
            Assert.Equal(new[] { "4B", "4C", "4D" }, r2000.Select(v => v.Handle).ToArray());
            Assert.Equal("Wand€Außen", r2000.Layer);
        }

        [Fact]
        public void APolylinesShape_IsTheReaders()
        {
            DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "49");
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tolerance.Default });

            Assert.Equal(read.Shapes.Single(s => s.Handle == "49").Shape, Assert.Single(polyline.GetGeometry()));
            Assert.Equal(polyline.GetGeometry()[0], polyline.GetGeCurve());
        }

        #endregion

        #region Nothing changed

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in Drawings)
            {
                SettingWhatItHasChangesNothing<DxfPolyline2d>(text, polyline =>
                {
                    polyline.Closed = polyline.Closed;
                    polyline.Elevation = polyline.Elevation;
                    polyline.Normal = polyline.Normal;
                    polyline.Thickness = polyline.Thickness;
                    polyline.DefaultStartWidth = polyline.DefaultStartWidth;
                    polyline.DefaultEndWidth = polyline.DefaultEndWidth;
                    polyline.ConstantWidth = polyline.ConstantWidth;
                    polyline.PolyType = polyline.PolyType;
                    polyline.LinetypeGenerationOn = polyline.LinetypeGenerationOn;
                    polyline.Layer = polyline.Layer;
                    polyline.Straighten();
                    polyline.TransformBy(GeoTransform3.Identity);

                    foreach (DxfVertex2d vertex in polyline)
                    {
                        vertex.Position = vertex.Position;
                        vertex.Bulge = vertex.Bulge;
                        vertex.StartWidth = vertex.StartWidth;
                        vertex.EndWidth = vertex.EndWidth;
                        vertex.Tangent = vertex.Tangent;
                        vertex.TangentUsed = vertex.TangentUsed;
                        vertex.TransformBy(GeoTransform3.Identity);
                    }
                });
            }
        }

        #endregion

        #region Changed

        [Fact]
        public void AVertexSet_IsItsOwnRecordWrittenAnew_ItsHandleKept()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(db, "49");
            DxfVertex2d vertex = polyline.ToList()[1];

            vertex.Position = new GeoPoint3(4.5, 0.25, 0.0);
            vertex.Bulge = 0.5;
            vertex.StartWidth = 0.125;
            vertex.Tangent = Math.PI / 4;
            vertex.TangentUsed = true;
            polyline.Elevation = 2.0;
            polyline.Closed = false;

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "49", "4C");
            DxfPolyline2d again = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(saved), "49");
            DxfVertex2d read = again.ToList()[1];
            Assert.Equal("4C", read.Handle);
            Assert.Equal(Bits(new GeoPoint3(4.5, 0.25, 2.0)), Bits(read.Position));
            Assert.Equal(0.5, read.Bulge);
            Assert.Equal(0.125, read.StartWidth);
            Assert.Equal(Math.PI / 4, read.Tangent, 15);
            Assert.True(read.TangentUsed);
            Assert.Equal(2.0, again.Elevation);
            Assert.False(again.Closed);
            Assert.Contains(" 42\n0.5\n 70\n2\n", Records(saved).Single(r => HandleOf(r) == "4C"), StringComparison.Ordinal);
        }

        [Fact]
        public void AVertexAppendedInsertedAndErased_WritesTheVerticesAnew()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(db, "4C");
            DxfVertex2d first = polyline.First();
            var appended = new DxfVertex2d(new GeoPoint3(0, 5, 0), 0.0, 0.0, 0.0, 0.0);
            var inserted = new DxfVertex2d(new GeoPoint3(2.5, -1, 0), 0.0, 0.0, 0.0, 0.0);

            polyline.AppendVertex(appended);
            polyline.InsertVertexAt(first, inserted);
            polyline.ToList()[2].Erase();

            List<DxfVertex2d> vertices = polyline.ToList();
            Assert.Equal(4, vertices.Count);
            Assert.Same(first, vertices[0]);
            Assert.Same(inserted, vertices[1]);
            Assert.Same(appended, vertices[3]);
            Assert.Equal(Bits(new GeoPoint3(5, 5, 0)), Bits(vertices[2].Position));
            Assert.Throws<InvalidOperationException>(() => polyline.AppendVertex(appended));

            DxfPolyline2d again = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(db.ToText()), "4C");
            Assert.Equal(new[] { 0.0, 2.5, 5.0, 0.0 }, again.Select(v => v.Position.X).ToArray());
            Assert.Equal(16.5 + Math.Sqrt(7.25), again.Length, 12);
        }

        [Fact]
        public void APolylineKeepsTwoVerticesAtLeast_ErasingBelowIsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(db, "4C");

            polyline.First().Erase();
            string text = db.ToText();

            Assert.Throws<InvalidOperationException>(() => polyline.First().Erase());
            Assert.Equal(2, polyline.Count());
            Assert.Equal(text, db.ToText());
        }

        #endregion

        #region Made in code

        [Fact]
        public void APolylineMadeInCode_IsAppendedToEachVersion_ItsVerticesAndEndWithIt()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var polyline = new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0) }, 1.5, false, 0.0, 0.0, new[] { 0.0, 1.0, 0.0 }) { Layer = "rebar" };

                db.ModelSpace.AppendEntity(polyline);

                string saved = db.ToText();
                DxfPolyline2d again = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(saved), polyline.Handle);
                Assert.Equal(3, again.Count());
                Assert.Equal(1.5, again.Elevation);
                Assert.Equal(1.0, again.ToList()[1].Bulge);
                Assert.Equal(10.0 + 5.0 * Math.PI, again.Length, 12);
                Assert.Equal("rebar", again.Layer);
                Assert.Contains(Records(saved), r => r.StartsWith("  0\nSEQEND\n", StringComparison.Ordinal));
            }

        }

        [Fact]
        public void APolylineErased_TakesItsVerticesAndEndWithIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(db, "49");
            DxfVertex2d vertex = polyline.First();

            polyline.Erase();

            string saved = db.ToText();
            Assert.DoesNotContain(Records(saved), r => HandleOf(r) == "49" || HandleOf(r) == "4B" || HandleOf(r) == "4D");
            Assert.True(polyline.IsErased);
            Assert.True(polyline.First().IsErased);
            Assert.True(vertex.IsErased);
            Assert.Throws<InvalidOperationException>(() => vertex.Bulge = 1.0);
        }

        #endregion

        #region Moved as ezdxf moves one

        [Fact]
        public void APolylineTurned_IsWhereEzdxfPutsIt()
        {
            // ezdxf 1.4.5's transform() of 49: the extrusion (0.3947397981737998, -0.0713924994178759, 0.9160150668873175), the
            // elevation (0, 0, 6.5943981460456715), the vertices (-3.1404543833265546, -8.826180337930698), (0.2617706712097556,
            // -12.490176063676762), (3.925766396955818, -9.087951009140452).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline2d polyline = ByHandle<DxfPolyline2d>(db, "49");

            polyline.TransformBy(DxfPolylineTests.Turn());

            string saved = db.ToText();
            DxfPolyline2d again = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(saved), "49");
            List<DxfVertex2d> vertices = again.ToList();
            DxfPolylineTests.Near(new GeoVector3(0.3947397981737998, -0.0713924994178759, 0.9160150668873175), again.Normal);
            Assert.Equal(6.5943981460456715, again.Elevation, 9);
            DxfPolylineTests.Near(new GeoPoint2(-3.1404543833265546, -8.826180337930698), new GeoPoint2(vertices[0].Position.X, vertices[0].Position.Y));
            DxfPolylineTests.Near(new GeoPoint2(0.2617706712097556, -12.490176063676762), new GeoPoint2(vertices[1].Position.X, vertices[1].Position.Y));
            DxfPolylineTests.Near(new GeoPoint2(3.925766396955818, -9.087951009140452), new GeoPoint2(vertices[2].Position.X, vertices[2].Position.Y));
            Assert.Equal(new[] { "4B", "4C", "4D" }, vertices.Select(v => v.Handle).ToArray());
            DxfPolylineTests.Near(DxfPolylineTests.Turn().Transform(new GeoPoint3(5, 5, 0)), again.VertexPosition(vertices[2]));
        }

        #endregion
    }
}
