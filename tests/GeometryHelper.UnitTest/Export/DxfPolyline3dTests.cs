using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The 3D POLYLINE as a <see cref="DxfPolyline3d"/> with its <see cref="DxfPolylineVertex3d"/> records: read as ezdxf 1.4.5
    /// reads it, walked, its vertices set in their own records, made in code for R12 to R2018, and moved as ezdxf moves one.
    /// </summary>
    public class DxfPolyline3dTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        [Fact]
        public void TheThreeDPolylinesOfEzdxf_AreReadAsEzdxfReadsThem()
        {
            // As ezdxf 1.4.5 reads them: 44 of R2000 and 47 of R12, open, (0, 0, 0), (1, 2, 3), (4, 5, 6), each vertex flagged 32.
            foreach (KeyValuePair<string, string> drawing in new[] { new KeyValuePair<string, string>(DxfFileEzdxfDrawings.R2000, "44"), new KeyValuePair<string, string>(DxfFileEzdxfDrawings.R12, "47") })
            {
                DxfPolyline3d polyline = ByHandle<DxfPolyline3d>(DxfDatabase.Parse(drawing.Key), drawing.Value);
                List<DxfPolylineVertex3d> vertices = polyline.ToList();

                Assert.False(polyline.Closed);
                Assert.Equal(DxfPoly3dType.SimplePoly, polyline.PolyType);
                Assert.Equal(new[] { Bits(new GeoPoint3(0, 0, 0)), Bits(new GeoPoint3(1, 2, 3)), Bits(new GeoPoint3(4, 5, 6)) }, vertices.Select(v => Bits(v.Position)).ToArray());
                Assert.Equal(DxfVertex3dType.SimpleVertex, vertices[1].VertexType);
                Assert.Equal(Math.Sqrt(14) + Math.Sqrt(27), polyline.Length, 12);
                Assert.Equal(2.0, polyline.EndParam);
                Assert.Equal(Bits(new GeoPoint3(2.5, 3.5, 4.5)), Bits(polyline.GetPointAtParameter(1.5)));
                Assert.Equal(Bits(new GeoVector3(3, 3, 3)), Bits(polyline.GetFirstDerivative(1.5)));
                Assert.Equal(1.5, polyline.GetParameterAtPoint(new GeoPoint3(2.5, 3.5, 4.5)), 12);
                Assert.Equal(polyline.GetGeometry()[0], polyline.GetGeCurve());
            }

            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tolerance.Default });
            DxfPolyline3d r2000 = ByHandle<DxfPolyline3d>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "44");
            Assert.Equal(read.Shapes.Single(s => s.Handle == "44").Shape, Assert.Single(r2000.GetGeometry()));
            Assert.Equal(new[] { "46", "47", "48" }, r2000.Select(v => v.Handle).ToArray());
        }

        [Fact]
        public void AVertexAppendedBeforeItsPolylineIsAdded_HasItsHandleAndOwnerOnceThePolylineIs()
        {
            // The vertex went into the polyline while both were made in code, and kept reading the file it was made in after
            // the polyline was added: its handle and its owner's were null until the drawing was read again.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            var polyline = new DxfPolyline3d();
            var vertex = new DxfPolylineVertex3d(new GeoPoint3(1, 2, 3));
            polyline.AppendVertex(vertex);
            polyline.AppendVertex(new DxfPolylineVertex3d(new GeoPoint3(4, 5, 6)));

            db.ModelSpace.AppendEntity(polyline);

            Assert.NotNull(vertex.Handle);
            Assert.Equal(polyline.Handle, vertex.OwnerHandle);
            Assert.Same(polyline, vertex.Owner);
            Assert.Equal(new[] { vertex.Handle, polyline.Last().Handle }, polyline.Select(v => v.Handle).ToArray());
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in Drawings)
            {
                SettingWhatItHasChangesNothing<DxfPolyline3d>(text, polyline =>
                {
                    polyline.Closed = polyline.Closed;
                    polyline.PolyType = polyline.PolyType;
                    polyline.Layer = polyline.Layer;
                    polyline.Straighten();
                    polyline.TransformBy(GeoTransform3.Identity);

                    foreach (DxfPolylineVertex3d vertex in polyline)
                    {
                        vertex.Position = vertex.Position;
                        vertex.TransformBy(GeoTransform3.Identity);
                    }
                });
            }
        }

        [Fact]
        public void AVertexMoved_IsItsOwnRecordWrittenAnew_AndAClosedPolylineRunsBackToItsFirst()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfPolyline3d polyline = ByHandle<DxfPolyline3d>(db, "44");

            polyline.ToList()[2].Position = new GeoPoint3(6.5, 7.5, -2.5);
            polyline.Closed = true;

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "44", "48");
            DxfPolyline3d again = ByHandle<DxfPolyline3d>(DxfDatabase.Parse(saved), "44");
            Assert.True(again.Closed);
            Assert.Equal(Bits(new GeoPoint3(6.5, 7.5, -2.5)), Bits(again.ToList()[2].Position));
            Assert.Equal(3.0, again.EndParam);
            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(again.EndPoint));
        }

        [Fact]
        public void APolylineKeepsTwoVerticesAtLeast_ErasingBelowIsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline3d polyline = ByHandle<DxfPolyline3d>(db, "44");

            polyline.ElementAt(1).Erase();

            Assert.Throws<InvalidOperationException>(() => polyline.First().Erase());
            Assert.Equal(new[] { "46", "48" }, polyline.Select(v => v.Handle).ToArray());
        }

        [Fact]
        public void APolylineMadeInCode_IsAppendedToEachVersion_AndReversed()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var polyline = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(3, 4, 0) }, false);
                polyline.AppendVertex(new DxfPolylineVertex3d(new GeoPoint3(3, 4, 12)));

                db.ModelSpace.AppendEntity(polyline);
                polyline.ReverseCurve();

                DxfPolyline3d again = ByHandle<DxfPolyline3d>(DxfDatabase.Parse(db.ToText()), polyline.Handle);
                Assert.Equal(17.0, again.Length);
                Assert.Equal(Bits(new GeoPoint3(3, 4, 6)), Bits(again.GetPointAtParameter(0.5)));
                Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(again.EndPoint));
            }

            Assert.Throws<NotSupportedException>(() => new DxfPolyline3d(DxfPoly3dType.CubicSplinePoly, new GeoPoint3[0], false));
        }

        [Fact]
        public void APolylineTurned_IsWhereEzdxfPutsIt_ItsVerticesKeepingTheirHandles()
        {
            // ezdxf 1.4.5's transform() of 44: (10, -5, 2.5), (11, -3, 5.5), (13.080349063599838, 0.9322645951833524, 8.185040582011153).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline3d polyline = ByHandle<DxfPolyline3d>(db, "44");

            polyline.TransformBy(DxfPolylineTests.Turn());

            List<DxfPolylineVertex3d> vertices = ByHandle<DxfPolyline3d>(DxfDatabase.Parse(db.ToText()), "44").ToList();
            DxfPolylineTests.Near(new GeoPoint3(10, -5, 2.5), vertices[0].Position);
            DxfPolylineTests.Near(new GeoPoint3(11, -3, 5.5), vertices[1].Position);
            DxfPolylineTests.Near(new GeoPoint3(13.080349063599838, 0.9322645951833524, 8.185040582011153), vertices[2].Position);
            Assert.Equal(new[] { "46", "47", "48" }, vertices.Select(v => v.Handle).ToArray());
        }
    }
}
