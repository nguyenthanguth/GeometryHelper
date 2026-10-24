using System;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The SOLID as a <see cref="DxfSolid"/>: read as ezdxf 1.4.5 reads it, its corners set in the world and read back, made in
    /// code for R12 to R2018, and moved as ezdxf moves one, in the plane of its new normal.
    /// </summary>
    public class DxfSolidTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        [Fact]
        public void TheSolidsOfEzdxf_AreReadAsEzdxfReadsThem_ToTheBit()
        {
            // As ezdxf 1.4.5 reads 43 of R2000: (0, 0), (2, 0), (0, 2), (2, 2), the square of side 2; 7E of the arrow block
            // _CLOSEDFILLED a triangle whose fourth corner is its third, (-1, -0.16439898372402537) (-0x1.50b06a14b8fc7p-3).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfSolid solid = ByHandle<DxfSolid>(db, "43");
            DxfSolid arrow = ByHandle<DxfSolid>(db, "7E");

            Assert.Equal(Bits(new GeoPoint3(0, 2, 0)), Bits(solid.GetPointAt(2)));
            Assert.Equal(Bits(new GeoPoint3(2, 2, 0)), Bits(solid.GetPointAt(3)));
            Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(solid.Normal));
            Assert.Equal(0.0, solid.Thickness);
            Assert.Equal(Bits(new GeoPoint3(-1, -FromBits(0x3fc50b06a14b8fc7), 0)), Bits(arrow.GetPointAt(3)));
            Assert.Equal(0, arrow.ColorIndex);

            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tolerance.Default });
            Assert.Equal(read.Shapes.Single(s => s.Handle == "43").Shape, Assert.Single(solid.GetGeometry()));
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in Drawings)
            {
                SettingWhatItHasChangesNothing<DxfSolid>(text, solid =>
                {
                    for (short i = 0; i < 4; i++)
                    {
                        solid.SetPointAt(i, solid.GetPointAt(i));
                    }

                    solid.Normal = solid.Normal;
                    solid.Thickness = solid.Thickness;
                    solid.Layer = solid.Layer;
                    solid.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void ACornerAndAThicknessSet_AreThatRecordWrittenAnew()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfSolid solid = ByHandle<DxfSolid>(db, "43");

            solid.SetPointAt(2, new GeoPoint3(13.5, 6.0, 0.0));
            solid.Thickness = 0.5;

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "43");
            DxfSolid again = ByHandle<DxfSolid>(DxfDatabase.Parse(saved), "43");
            Assert.Equal(Bits(new GeoPoint3(13.5, 6, 0)), Bits(again.GetPointAt(2)));
            Assert.Equal(0.5, again.Thickness);
        }

        [Fact]
        public void ANormalSet_KeepsTheCornersWhereTheyStandInTheWorld()
        {
            // The square 43 of side 2 at the origin given the normal (0, 0, -1): its corner (2, 0, 0) is written (-2, 0, 0) in
            // the system of that normal, and stands where it stood.
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfSolid solid = ByHandle<DxfSolid>(db, "43");
            GeoPoint3[] before = Enumerable.Range(0, 4).Select(i => solid.GetPointAt((short)i)).ToArray();

            solid.Normal = new GeoVector3(0, 0, -2);

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "43");
            DxfSolid again = ByHandle<DxfSolid>(DxfDatabase.Parse(saved), "43");
            Assert.Equal(Bits(new GeoVector3(0, 0, -1)), Bits(again.Normal));

            for (short i = 0; i < 4; i++)
            {
                Assert.True(again.GetPointAt(i).IsEqualTo(before[i], Tolerance.Default), i + " " + again.GetPointAt(i));
            }

            string record = Records(saved).Single(r => HandleOf(r) == "43");
            Assert.True(record.Contains(" 11\n-2\n") || record.Contains(" 11\n-2.0\n"), record);
        }

        [Fact]
        public void ASolidMadeInCode_IsAppendedToEachVersion()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var solid = new DxfSolid(new GeoPoint3(0, 0, 0), new GeoPoint3(2, 0, 0), new GeoPoint3(0, 2, 0), new GeoPoint3(2, 2, 0));

                db.ModelSpace.AppendEntity(solid);

                DxfSolid again = ByHandle<DxfSolid>(DxfDatabase.Parse(db.ToText()), solid.Handle);
                Assert.Equal(Bits(new GeoPoint3(2, 2, 0)), Bits(again.GetPointAt(3)));
                Assert.Equal(4.0, ((GeoPolygon3)Assert.Single(again.GetGeometry())).Area, 12);
            }
        }

        [Fact]
        public void ASolidTurned_IsWhereEzdxfPutsIt()
        {
            // ezdxf 1.4.5's transform() of 43: the extrusion (0.3947397981737998, -0.0713924994178759, 0.9160150668873175) and
            // the corners (-3.1404543833265546, -8.826180337930698, 6.5943981460456715), (-1.7795643615120302, -10.291778628229123,
            // 6.594398146045671), (-1.6748560930281298, -7.4652903161161746, ...), (-0.31396607121360565, -8.930888606414598, ...)
            // in the system of the new extrusion.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            ByHandle<DxfSolid>(db, "43").TransformBy(DxfPolylineTests.Turn());

            DxfSolid again = ByHandle<DxfSolid>(DxfDatabase.Parse(db.ToText()), "43");
            DxfPolylineTests.Near(new GeoVector3(0.3947397981737998, -0.0713924994178759, 0.9160150668873175), again.Normal);
            string record = Records(db.ToText()).Single(r => HandleOf(r) == "43");
            Assert.Contains(" 10\n-3.14045438332655", record, StringComparison.Ordinal);
            Assert.Contains(" 21\n-10.29177862822912", record, StringComparison.Ordinal);
            DxfPolylineTests.Near(DxfPolylineTests.Turn().Transform(new GeoPoint3(2, 2, 0)), again.GetPointAt(3));
            DxfPolylineTests.Near(DxfPolylineTests.Turn().Transform(new GeoPoint3(0, 2, 0)), again.GetPointAt(2));
        }
    }
}
