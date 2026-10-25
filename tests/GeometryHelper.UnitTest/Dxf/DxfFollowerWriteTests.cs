using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Vertices written one at a time: the extents of the drawing hold each of them when the header is read or the file saved,
    /// the polyline being read once then rather than at each write; in a drawing whose header holds extents (ezdxf's write
    /// 1E+20 the wrong way round, which are left as they are).
    /// </summary>
    public class DxfFollowerWriteTests
    {
        [Fact]
        public void VerticesAppendedOneByOne_AreHeldByTheExtents_ReadAfterThem()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfReaderRoundTripTests.OneOfEach().ToString());
            var polyline = new DxfPolyline3d();
            db.ModelSpace.AppendEntity(polyline);

            for (int i = 0; i < 500; i++)
            {
                polyline.AppendVertex(new DxfPolylineVertex3d(new GeoPoint3(1000 + i, -2000 - i % 7, 3000)));
            }

            Assert.Equal(500, polyline.Count());
            Assert.Equal(1499.0, db.Extmax.X);
            Assert.Equal(-2006.0, db.Extmin.Y);
            Assert.Equal(3000.0, db.Extmax.Z);

            // A vertex moved one at a time is held too, read again from the saved file.
            polyline.Last().Position = new GeoPoint3(5000, 0, 0);
            DxfDatabase again = Opened(Saved(db));
            Assert.Equal(5000.0, again.Extmax.X);
        }

        [Fact]
        public void APolylineOfThePlaneBuiltOneVertexAtATime_IsReadBackWhole()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfReaderRoundTripTests.OneOfEach().ToString());
            var polyline = new DxfPolyline2d();
            db.ModelSpace.AppendEntity(polyline);

            for (int i = 0; i < 300; i++)
            {
                polyline.AppendVertex(new DxfVertex2d(new GeoPoint3(i, i % 2, 0), 0, 0, 0, 0));
            }

            foreach (DxfVertex2d vertex in polyline.Take(10))
            {
                vertex.Bulge = 0.25;
            }

            DxfPolyline2d read = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(db.ToText()), polyline.Handle);
            Assert.Equal(300, read.Count());
            Assert.Equal(0.25, read.ElementAt(9).Bulge);
            Assert.Equal(299.0, db.Extmax.X);
        }

        [Fact]
        public void ALightweightPolylineEditedOneVertexAtATime_ReadsWhatOtherWritesMadeOfIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfReaderRoundTripTests.OneOfEach().ToString());
            var polyline = new DxfPolyline();
            db.ModelSpace.AppendEntity(polyline);

            for (int i = 0; i < 200; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(i, i % 3), 0, 0, 0);
            }

            Assert.Equal(new GeoPoint2(5, 2), polyline.GetPoint2dAt(5));

            // Moved, turned round and given a width by writes of the whole record: each read after it.
            polyline.TransformBy(GeoTransform3.Translation(new GeoVector3(100, 0, 0)));
            Assert.Equal(new GeoPoint2(105, 2), polyline.GetPoint2dAt(5));
            polyline.ReverseCurve();
            Assert.Equal(new GeoPoint2(105, 2), polyline.GetPoint2dAt(194));
            polyline.ConstantWidth = 2.0;
            polyline.SetStartWidthAt(3, 1.0);
            polyline.RemoveVertexAt(0);
            polyline.AddVertexAt(199, new GeoPoint2(-1, -1), 0.5, 0, 0);
            polyline.SetPointAt(1, new GeoPoint2(7, 7));

            DxfPolyline again = ByHandle<DxfPolyline>(DxfDatabase.Parse(db.ToText()), polyline.Handle);
            Assert.Equal(200, again.NumberOfVertices);
            Assert.Equal(new GeoPoint2(7, 7), again.GetPoint2dAt(1));
            Assert.Equal(1.0, again.GetStartWidthAt(2));
            Assert.Equal(2.0, again.GetEndWidthAt(2));
            Assert.Equal(2.0, again.GetStartWidthAt(100));
            Assert.Equal(new GeoPoint2(-1, -1), again.GetPoint2dAt(199));
            Assert.Equal(0.5, again.GetBulgeAt(199));
            Assert.Equal(new GeoPoint2(105, 2), again.GetPoint2dAt(193));
        }

        [Fact]
        public void ALightweightPolylineMovedWithItsExtendedData_IsEditedOneVertexAtATime_ThatDataKept()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var polyline = new DxfPolyline();
            db.ModelSpace.AppendEntity(polyline);

            for (int i = 0; i < 20; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(i, 0), 0, 0, 0);
            }

            polyline.XData = new[] { new DxfTypedValue(1001, "GHPROBE"), new DxfTypedValue(1011, "1"), new DxfTypedValue(1021, "2"), new DxfTypedValue(1031, "0") };
            Assert.Equal(new GeoPoint2(3, 0), polyline.GetPoint2dAt(3));

            polyline.TransformBy(GeoTransform3.Translation(new GeoVector3(10, 0, 0)));
            polyline.AddVertexAt(20, new GeoPoint2(50, 5), 0, 0, 0);
            polyline.SetPointAt(3, new GeoPoint2(13, 1));
            polyline.RemoveVertexAt(0);

            DxfPolyline again = ByHandle<DxfPolyline>(DxfDatabase.Parse(db.ToText()), polyline.Handle);
            Assert.Equal(20, again.NumberOfVertices);
            Assert.Equal(new GeoPoint2(11, 0), again.GetPoint2dAt(0));
            Assert.Equal(new GeoPoint2(13, 1), again.GetPoint2dAt(2));
            Assert.Equal(new GeoPoint2(50, 5), again.GetPoint2dAt(19));
            Assert.Equal(new[] { "GHPROBE", "11", "2", "0" }, again.XData.Select(g => g.Value));
        }

        [Fact]
        public void APointOfAPolyfaceMeshNoFaceNames_ErasedFirst_LeavesTheExtentsToTheFaces()
        {
            // The far point (500, 500, 0) is first, the square of side 10 its face (2, 3, 4, 5); erased, the face is (1, 2, 3, 4).
            DxfDatabase db = DxfDatabase.Parse(new DxfWriter().Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0))).ToString());
            var mesh = new DxfPolyFaceMesh();
            db.ModelSpace.AppendEntity(mesh);
            mesh.AppendVertex(new DxfPolyFaceMeshVertex(new GeoPoint3(500, 500, 0)));

            foreach (GeoPoint3 corner in new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(0, 10, 0) })
            {
                mesh.AppendVertex(new DxfPolyFaceMeshVertex(corner));
            }

            mesh.AppendFaceRecord(new DxfFaceRecord(2, 3, 4, 5));
            Assert.Equal(10.0, db.Extmax.X);

            mesh.OfType<DxfPolyFaceMeshVertex>().First().Erase();

            DxfFaceRecord face = mesh.OfType<DxfFaceRecord>().Single();
            Assert.Equal(new short[] { 1, 2, 3, 4 }, new short[] { face.GetVertexAt(0), face.GetVertexAt(1), face.GetVertexAt(2), face.GetVertexAt(3) });
            Assert.Equal(4, mesh.NumVertices);
            Assert.Equal(new GeoPoint3(10, 10, 0), db.Extmax);
        }

        [Fact]
        public void PointsOfAPolyfaceMeshErasedOneAtATime_RenumberTheFaces_ThoseWrittenBetweenAmongThem()
        {
            DxfDatabase db = DxfDatabase.Parse(new DxfWriter().Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0))).ToString());
            var mesh = new DxfPolyFaceMesh();
            db.ModelSpace.AppendEntity(mesh);

            for (int i = 0; i < 40; i++)
            {
                mesh.AppendVertex(new DxfPolyFaceMeshVertex(new GeoPoint3(i % 10, i / 10, i % 3)));
            }

            // Faces on the points 1-4, 11-14, 21-24 and 31-34; the points 6, 16, 26 and 36 named by none.
            for (short a = 1; a < 40; a += 10)
            {
                mesh.AppendFaceRecord(new DxfFaceRecord(a, (short)(a + 1), (short)(a + 2), (short)-(a + 3)));
            }

            List<DxfPolyFaceMeshVertex> points = mesh.OfType<DxfPolyFaceMeshVertex>().ToList();
            points[5].Erase();
            mesh.OfType<DxfFaceRecord>().ElementAt(3).SetVertexAt(0, 7);
            points[15].Erase();
            points[25].Erase();

            DxfPolyFaceMesh again = ByHandle<DxfPolyFaceMesh>(DxfDatabase.Parse(db.ToText()), mesh.Handle);
            List<DxfFaceRecord> faces = again.OfType<DxfFaceRecord>().ToList();
            Assert.Equal(37, again.NumVertices);
            Assert.Equal(new short[] { 1, 10, 19, 7 }, faces.Select(f => f.GetVertexAt(0)).ToArray());
            Assert.Equal(new short[] { 4, 13, 22, 31 }, faces.Select(f => f.GetVertexAt(3)).ToArray());
            Assert.False(faces[3].IsEdgeVisibleAt(3));
        }

        [Fact]
        public void ALightweightPolylineWithACommentAmongItsVertices_IsEditedOneVertexAtATime_AsAnyOther()
        {
            var made = new DxfPolyline();
            made.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            made.AddVertexAt(1, new GeoPoint2(10, 0), 0, 0, 0);
            made.AddVertexAt(2, new GeoPoint2(20, 0), 0, 0, 0);
            DxfDatabase db = DxfDatabase.Parse(DxfReaderRoundTripTests.OneOfEach().ToString());
            db.ModelSpace.AppendEntity(made);
            string text = db.ToText();
            int second = text.IndexOf(" 10\n10\n", text.IndexOf("AcDbPolyline", System.StringComparison.Ordinal), System.StringComparison.Ordinal);
            text = (text.Substring(0, second) + "999\nfrom a hand\n" + text.Substring(second)).Replace("\n", "\r\n");
            db = DxfDatabase.Parse(text);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, made.Handle);

            polyline.AddVertexAt(1, new GeoPoint2(5, 5), 0, 0, 0);
            polyline.SetPointAt(3, new GeoPoint2(20, 9));
            polyline.RemoveVertexAt(0);

            DxfPolyline again = ByHandle<DxfPolyline>(DxfDatabase.Parse(db.ToText()), made.Handle);
            Assert.Equal(new[] { new GeoPoint2(5, 5), new GeoPoint2(10, 0), new GeoPoint2(20, 9) }, Enumerable.Range(0, again.NumberOfVertices).Select(again.GetPoint2dAt).ToArray());
        }
    }
}
