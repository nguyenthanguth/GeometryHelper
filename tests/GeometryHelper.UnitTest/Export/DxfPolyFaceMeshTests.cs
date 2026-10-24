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
    /// The polyface mesh as a <see cref="DxfPolyFaceMesh"/>: its points and faces read as ezdxf 1.4.5 reads them, the body the
    /// reader makes of it, made in code, and moved as ezdxf moves one.
    /// </summary>
    public class DxfPolyFaceMeshTests
    {
        [Fact]
        public void ThePolyfaceMeshOfEzdxf_IsReadAsEzdxfReadsIt()
        {
            // As ezdxf 1.4.5 reads 4E of R2000 and 51 of R12: 8 points of the unit cube (m_count 8), faces counted 4 (n_count 4),
            // the first face 1, 2, 3, 4.
            foreach (string[] drawing in new[] { new[] { DxfFileEzdxfDrawings.R2000, "4E" }, new[] { DxfFileEzdxfDrawings.R12, "51" } })
            {
                DxfPolyFaceMesh mesh = ByHandle<DxfPolyFaceMesh>(DxfDatabase.Parse(drawing[0]), drawing[1]);
                List<DxfVertex> records = mesh.ToList();
                List<DxfPolyFaceMeshVertex> points = records.OfType<DxfPolyFaceMeshVertex>().ToList();
                List<DxfFaceRecord> faces = records.OfType<DxfFaceRecord>().ToList();

                Assert.Equal(8, mesh.NumVertices);
                Assert.Equal(4, mesh.NumFaces);
                Assert.Equal(8, points.Count);
                Assert.Equal(4, faces.Count);
                Assert.Equal(Bits(new GeoPoint3(1, 1, 1)), Bits(points[6].Position));
                Assert.Equal(new short[] { 1, 2, 3, 4 }, Enumerable.Range(0, 4).Select(i => faces[0].GetVertexAt((short)i)).ToArray());
                Assert.True(faces[0].IsEdgeVisibleAt(2));
            }

            DxfPolyFaceMesh r2000 = ByHandle<DxfPolyFaceMesh>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "4E");
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tolerance.Default });
            Assert.Equal(read.Shapes.Where(s => s.Handle == "4E").Select(s => s.Shape), r2000.GetGeometry());
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                SettingWhatItHasChangesNothing<DxfPolyFaceMesh>(text, mesh =>
                {
                    mesh.Layer = mesh.Layer;
                    mesh.TransformBy(GeoTransform3.Identity);

                    foreach (DxfVertex vertex in mesh)
                    {
                        if (vertex is DxfPolyFaceMeshVertex point)
                        {
                            point.Position = point.Position;
                        }
                        else
                        {
                            var face = (DxfFaceRecord)vertex;

                            for (short i = 0; i < 4; i++)
                            {
                                face.SetVertexAt(i, face.GetVertexAt(i));
                            }
                        }
                    }
                });
            }
        }

        [Fact]
        public void AMeshMadeInCode_IsAppendedToEachVersion_ItsPointsBeforeItsFaces()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var mesh = new DxfPolyFaceMesh();
                mesh.AppendVertex(new DxfPolyFaceMeshVertex(new GeoPoint3(0, 0, 0)));
                mesh.AppendVertex(new DxfPolyFaceMeshVertex(new GeoPoint3(1, 0, 0)));
                mesh.AppendFaceRecord(new DxfFaceRecord(1, 2, 3, 0));
                mesh.AppendVertex(new DxfPolyFaceMeshVertex(new GeoPoint3(0, 1, 0)));

                db.ModelSpace.AppendEntity(mesh);

                DxfPolyFaceMesh again = ByHandle<DxfPolyFaceMesh>(DxfDatabase.Parse(db.ToText()), mesh.Handle);
                List<DxfVertex> records = again.ToList();
                Assert.Equal(3, again.NumVertices);
                Assert.Equal(1, again.NumFaces);
                Assert.IsType<DxfFaceRecord>(records[3]);
                Assert.Equal(Bits(new GeoPoint3(0, 1, 0)), Bits(((DxfPolyFaceMeshVertex)records[2]).Position));
                Assert.IsType<GeoFace3>(Assert.Single(again.GetGeometry()));
            }
        }

        [Fact]
        public void AMeshTurned_HasItsPointsWhereEzdxfPutsThem_ItsFacesKept()
        {
            // ezdxf 1.4.5's transform() of 4E: the point (1, 1, 1) to (10.693449687866613, -3.689245134938883, 3.395013527337051).
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            ByHandle<DxfPolyFaceMesh>(db, "4E").TransformBy(DxfPolylineTests.Turn());

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "50", "51", "52", "53", "55", "56", "57", "58");
            DxfPolyFaceMesh again = ByHandle<DxfPolyFaceMesh>(DxfDatabase.Parse(saved), "4E");
            DxfPolylineTests.Near(new GeoPoint3(10.693449687866613, -3.689245134938883, 3.395013527337051), again.OfType<DxfPolyFaceMeshVertex>().ElementAt(6).Position);
        }
    }
}
