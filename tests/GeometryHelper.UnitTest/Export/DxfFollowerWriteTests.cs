using System.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
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
