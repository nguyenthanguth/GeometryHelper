using System;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The 3DFACE as a <see cref="DxfFace"/>: read as ezdxf 1.4.5 reads it, its corners and edges set and read back to the bit,
    /// made in code for R12 to R2018, erased, and moved as ezdxf moves one.
    /// </summary>
    public class DxfFaceTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        [Fact]
        public void TheFacesOfEzdxf_AreReadAsEzdxfReadsThem_ToTheBit()
        {
            // As ezdxf 1.4.5 reads 42 of R2000 and 45 of R12: (0, 0, 0), (1, 0, 0), (1, 1, 0.5), (0, 1, 0), every edge drawn.
            foreach (string[] drawing in new[] { new[] { DxfFileEzdxfDrawings.R2000, "42" }, new[] { DxfFileEzdxfDrawings.R12, "45" } })
            {
                DxfFace face = ByHandle<DxfFace>(DxfDatabase.Parse(drawing[0]), drawing[1]);

                Assert.Equal(Bits(new GeoPoint3(1, 1, 0.5)), Bits(face.GetVertexAt(2)));
                Assert.Equal(Bits(new GeoPoint3(0, 1, 0)), Bits(face.GetVertexAt(3)));
                Assert.True(face.IsEdgeVisibleAt(0));
                Assert.True(face.IsEdgeVisibleAt(3));
                Assert.Throws<ArgumentOutOfRangeException>(() => face.GetVertexAt(4));
            }

            DxfFace r2000 = ByHandle<DxfFace>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "42");
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tolerance.Default });
            Assert.Equal(read.Shapes.Where(s => s.Handle == "42").Select(s => s.Shape), r2000.GetGeometry());
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in Drawings.Concat(new[] { DxfReaderRoundTripTests.OneOfEach().ToString() }))
            {
                SettingWhatItHasChangesNothing<DxfFace>(text, face =>
                {
                    for (short i = 0; i < 4; i++)
                    {
                        face.SetVertexAt(i, face.GetVertexAt(i));

                        if (face.IsEdgeVisibleAt(i))
                        {
                            face.MakeEdgeVisibleAt(i);
                        }
                        else
                        {
                            face.MakeEdgeInvisibleAt(i);
                        }
                    }

                    face.Layer = face.Layer;
                    face.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void ACornerMovedAndAnEdgeHidden_AreThatRecordWrittenAnew()
        {
            string text = DxfFileEzdxfDrawings.R12;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfFace face = ByHandle<DxfFace>(db, "45");

            face.SetVertexAt(2, new GeoPoint3(2.5, 5.5, 3.5));
            face.MakeEdgeInvisibleAt(1);
            face.MakeEdgeInvisibleAt(3);
            face.MakeEdgeVisibleAt(3);

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "45");
            DxfFace again = ByHandle<DxfFace>(DxfDatabase.Parse(saved), "45");
            Assert.Equal(Bits(new GeoPoint3(2.5, 5.5, 3.5)), Bits(again.GetVertexAt(2)));
            Assert.False(again.IsEdgeVisibleAt(1));
            Assert.True(again.IsEdgeVisibleAt(3));
            Assert.Contains(" 70\n2\n", Records(saved).Single(r => HandleOf(r) == "45"), StringComparison.Ordinal);
        }

        [Fact]
        public void AFaceMadeInCode_IsAppendedToEachVersion_AndErased()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var face = new DxfFace(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0), new GeoPoint3(1, 1, 0), true, false, true, true) { ColorIndex = 3 };

                db.ModelSpace.AppendEntity(face);

                DxfFace again = ByHandle<DxfFace>(DxfDatabase.Parse(db.ToText()), face.Handle);
                Assert.Equal(Bits(new GeoPoint3(1, 1, 0)), Bits(again.GetVertexAt(3)));
                Assert.False(again.IsEdgeVisibleAt(1));
                Assert.Equal(3, again.ColorIndex);
                Assert.IsType<GeoTriangle3>(Assert.Single(again.GetGeometry()));

                face.Erase();
                Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == face.Handle);
                Assert.Throws<InvalidOperationException>(() => face.SetVertexAt(0, new GeoPoint3(1, 1, 1)));
            }
        }

        [Fact]
        public void AFaceTurned_IsWhereEzdxfPutsIt()
        {
            // ezdxf 1.4.5's transform() of 42: (10, -5, 2.5), (10.781639173907026, -4.449882769295642, 2.2060421215614197),
            // (10.496079788779713, -3.653548885229945, 2.9370059938933926), (9.517070715785788, -4.1679698662253655, 2.7729563388883145).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            ByHandle<DxfFace>(db, "42").TransformBy(DxfPolylineTests.Turn());

            DxfFace again = ByHandle<DxfFace>(DxfDatabase.Parse(db.ToText()), "42");
            DxfPolylineTests.Near(new GeoPoint3(10, -5, 2.5), again.GetVertexAt(0));
            DxfPolylineTests.Near(new GeoPoint3(10.781639173907026, -4.449882769295642, 2.2060421215614197), again.GetVertexAt(1));
            DxfPolylineTests.Near(new GeoPoint3(10.496079788779713, -3.653548885229945, 2.9370059938933926), again.GetVertexAt(2));
            DxfPolylineTests.Near(new GeoPoint3(9.517070715785788, -4.1679698662253655, 2.7729563388883145), again.GetVertexAt(3));
        }
    }
}
