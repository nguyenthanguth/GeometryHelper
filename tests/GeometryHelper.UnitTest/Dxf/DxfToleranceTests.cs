using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dwg;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The TOLERANCE as a <see cref="DxfTolerance"/>: read from the DXF AutoCAD 2021 wrote, made in code and written as AutoCAD
    /// writes one, its style's variables read through its overrides, and moved, turned, scaled and mirrored as AutoCAD 2021's
    /// TransformBy moves one (accoreconsole probes GHTOL1 and GHTOL4, 2026-11-12).
    /// </summary>
    public class DxfToleranceTests
    {
        private const string Full = "{\\Fgdt;j}%%v{\\Fgdt;n}0.1{\\Fgdt;m}%%vA%%vB%%vC";

        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>A frame of the words given at a point along a direction in a new drawing, with AutoCAD's overrides 147 then 140 where asked.</summary>
        private static DxfTolerance Frame(
            DxfDatabase db,
            string text,
            GeoPoint3 at,
            GeoVector3 direction,
            bool overrides
        )
        {
            var frame = new DxfTolerance
            {
                Text = text,
                Location = at,
                Direction = direction,
            };
            db.ModelSpace.AppendEntity(frame);

            if (overrides)
            {
                frame.XData = new[]
                {
                    new DxfTypedValue(1001, "ACAD"),
                    new DxfTypedValue(1000, "DSTYLE"),
                    new DxfTypedValue(1002, "{"),
                    new DxfTypedValue(1070, "147"),
                    new DxfTypedValue(1040, "0.625"),
                    new DxfTypedValue(1070, "140"),
                    new DxfTypedValue(1040, "2.5"),
                    new DxfTypedValue(1002, "}"),
                };
            }

            return frame;
        }

        private static string Dstyle(DxfEntity entity) =>
            string.Join(
                " ",
                entity.GetXDataForApplication("ACAD").Skip(1).Select(g => g.TypeCode + ":" + g.Value.Trim())
            );

        [Fact]
        public void TheFramesAutoCadWrote_AreRead_WithTheirWordsPointDirectionAndStyle()
        {
            DxfDatabase db = Opened(DwgAnswerKey.Key("leaders_AC1032"));
            List<DxfTolerance> frames = db.ModelSpace.OfType<DxfTolerance>().ToList();

            Assert.Equal(2, frames.Count);
            Assert.Equal("{\\Fgdt;j}%%v{\\Fgdt;n}0.5{\\Fgdt;m}%%v%%vA%%vB", frames[0].Text);
            Assert.True(frames[0].Location.IsEqualTo(new GeoPoint3(0, 100, 0), Tol));
            Assert.True(frames[0].Direction.IsEqualTo(new GeoVector3(1, 0, 0), Tol));
            Assert.True(frames[0].Normal.IsEqualTo(new GeoVector3(0, 0, 1), Tol));
            Assert.Equal("Standard", frames[0].DimensionStyleName);
            Assert.Equal("{\\Fgdt;r}%%v0.1", frames[1].Text);
            Assert.True(frames[1].Direction.IsEqualTo(new GeoVector3(Math.Sqrt(0.5), Math.Sqrt(0.5), 0), Tol));
        }

        [Fact]
        public void AFrameMadeInCode_IsWrittenAsAutoCadWritesOne_ItsRowsAsCaretJ()
        {
            var db = new DxfDatabase("AC1032");
            DxfTolerance frame = Frame(
                db,
                Full + "\n{\\Fgdt;c}%%v0.05",
                new GeoPoint3(10, 20, 0),
                new GeoVector3(1, 0, 0),
                false
            );

            // No direction nor normal where they are the world's x and z, as AutoCAD 2021 writes the first frame of its key.
            string record = Records(db.ToText())
                .Single(r => r.StartsWith("  0\nTOLERANCE\n", StringComparison.Ordinal));
            Assert.Contains("100\nAcDbFcf\n  3\nStandard\n 10\n10\n 20\n20\n 30\n0\n  1\n" + Full + "^J", record);
            Assert.DoesNotContain("\n 11\n", record);
            Assert.DoesNotContain("\n210\n", record);

            // Read back, the rows are a line feed apart.
            DxfTolerance read = Opened(Saved(db)).ModelSpace.OfType<DxfTolerance>().Single();
            Assert.Equal(Full + "\n{\\Fgdt;c}%%v0.05", read.Text);

            // Turned, the direction comes after the words.
            frame.Direction = new GeoVector3(0.6, 0.8, 0);
            record = Records(db.ToText()).Single(r => r.StartsWith("  0\nTOLERANCE\n", StringComparison.Ordinal));
            Assert.Contains("^J{\\Fgdt;c}%%v0.05\n 11\n0.6\n 21\n0.8", record);
        }

        [Fact]
        public void GetDimstyleData_ReadsTheOverrides_ThenTheStyle()
        {
            var db = new DxfDatabase("AC1032");
            DxfTolerance plain = Frame(db, Full, new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0), false);
            DxfTolerance given = Frame(db, Full, new GeoPoint3(0, 10, 0), new GeoVector3(1, 0, 0), true);
            DxfDimStyleTableRecord style = db.DimStyleTable["Standard"];

            Assert.Equal(style.Dimtxt, plain.GetDimstyleData().Dimtxt);
            Assert.Equal(style.Dimgap, plain.GetDimstyleData().Dimgap);
            Assert.Equal(2.5, given.GetDimstyleData().Dimtxt);
            Assert.Equal(0.625, given.GetDimstyleData().Dimgap);
            Assert.Equal(style.Dimscale, given.GetDimstyleData().Dimscale);
        }

        [Fact]
        public void Mirrored_ItIsTurnedOver_AsAutoCadsTransformByTurnsIt()
        {
            // AutoCAD 2021: the frame at (10, 20) along (0.6, 0.8) mirrored in the y axis stands at (-10, 20) along (-0.6, 0.8),
            // its normal (0, 0, -1), its overrides as they were.
            var db = new DxfDatabase("AC1032");
            DxfTolerance frame = Frame(db, Full, new GeoPoint3(10, 20, 0), new GeoVector3(0.6, 0.8, 0), true);
            frame.TransformBy(GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0))));

            Assert.True(frame.Location.IsEqualTo(new GeoPoint3(-10, 20, 0), Tol));
            Assert.True(frame.Direction.IsEqualTo(new GeoVector3(-0.6, 0.8, 0), Tol));
            Assert.True(frame.Normal.IsEqualTo(new GeoVector3(0, 0, -1), Tol));
            Assert.Equal("1000:DSTYLE 1002:{ 1070:147 1040:0.625 1070:140 1040:2.5 1002:}", Dstyle(frame));
        }

        [Fact]
        public void Turned_ItsDirectionTurns_AndItsOverridesStayAsTheyWere()
        {
            // AutoCAD 2021's ROTATE of 30 degrees about the origin: (10, 20) to (-1.339746, 22.320508), along (0.866025, 0.5).
            var db = new DxfDatabase("AC1032");
            DxfTolerance frame = Frame(db, Full, new GeoPoint3(10, 20, 0), new GeoVector3(1, 0, 0), true);
            frame.TransformBy(GeoTransform3.RotationZ(Math.PI / 6.0));

            Assert.True(frame.Location.IsEqualTo(new GeoPoint3(-1.339746, 22.320508, 0), Tol));
            Assert.True(frame.Direction.IsEqualTo(new GeoVector3(Math.Sqrt(3) / 2.0, 0.5, 0), Tol));
            Assert.True(frame.Normal.IsEqualTo(new GeoVector3(0, 0, 1), Tol));
            Assert.Equal("1000:DSTYLE 1002:{ 1070:147 1040:0.625 1070:140 1040:2.5 1002:}", Dstyle(frame));
        }

        [Fact]
        public void Scaled_ItWritesItsTextHeightThenItsGap_AsAutoCadWritesThem()
        {
            // AutoCAD 2021's SCALE by 2: the overrides 147 then 140 become 140 of 5 then 147 of 1.25.
            var db = new DxfDatabase("AC1032");
            DxfTolerance frame = Frame(db, Full, new GeoPoint3(10, 20, 0), new GeoVector3(1, 0, 0), true);
            frame.TransformBy(GeoTransform3.Scaling(2.0));

            Assert.True(frame.Location.IsEqualTo(new GeoPoint3(20, 40, 0), Tol));
            Assert.Equal("1000:DSTYLE 1002:{ 1070:140 1040:5 1070:147 1040:1.25 1002:}", Dstyle(frame));

            // A copy scaled 3 of a frame of no overrides holds the style's height and gap scaled, as GetTransformedCopy gives it.
            DxfTolerance plain = Frame(
                db,
                "{\\Fgdt;j}%%v0.1",
                new GeoPoint3(10, 20, 0),
                new GeoVector3(1, 0, 0),
                false
            );
            DxfDimStyleTableRecord style = db.DimStyleTable["Standard"];
            var copy = (DxfTolerance)plain.GetTransformedCopy(GeoTransform3.Scaling(3.0));
            Assert.True(copy.Location.IsEqualTo(new GeoPoint3(30, 60, 0), Tol));
            Assert.Equal(style.Dimtxt * 3.0, copy.GetDimstyleData().Dimtxt, 12);
            Assert.Equal(style.Dimgap * 3.0, copy.GetDimstyleData().Dimgap, 12);
            Assert.Null(plain.GetXDataForApplication("ACAD"));
        }
    }
}
