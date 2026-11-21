using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The rows an extension or a plugin gives the Properties panel with the factories of <see cref="CadPropertyRow"/>: the
    /// text each shows in the drawing's units, the kind it reports, and the refusals, the setter not called for text that does
    /// not read.
    /// </summary>
    public class PropertyRowFactoryTests
    {
        private static CadDocument Document(int lunits = 2, int luprec = 4, int aunits = 0, int auprec = 0)
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, null), "rows.dxf");
            document.Database.Lunits = lunits;
            document.Database.Luprec = luprec;
            document.Database.Aunits = aunits;
            document.Database.Auprec = auprec;
            return document;
        }

        [Fact]
        public void TheRowsOfTheOldConstructors_ReportTextChoiceOrCheck()
        {
            Assert.Equal(CadPropertyKind.Text, new CadPropertyRow("A", "b", true).Kind);
            Assert.Equal(CadPropertyKind.Text, new CadPropertyRow("A", "b", s => null).Kind);
            Assert.Equal(CadPropertyKind.Choice, new CadPropertyRow("A", "b", false, new[] { "b", "c" }).Kind);
            Assert.Equal(CadPropertyKind.Check, CadPropertyRow.Check("A", true, s => null).Kind);

            CadPropertyRow readOnly = CadPropertyRow.Check("A", true, null);
            Assert.Equal(CadPropertyKind.Text, readOnly.Kind);
            Assert.False(readOnly.IsCheck);
            Assert.True(readOnly.IsReadOnly);
        }

        [Fact]
        public void ANumber_IsWrittenAsTypedInTheInvariantCulture_AndReadBack()
        {
            double? got = null;
            CadPropertyRow row = CadPropertyRow.Number("Pitch", 12.5, v =>
            {
                got = v;
                return null;
            });

            Assert.Equal("12.5", row.Value);
            Assert.Equal(CadPropertyKind.Number, row.Kind);
            Assert.Null(row.Setter(" 7.25 "));
            Assert.Equal(7.25, got.Value, 12);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData("1,5,5")]
        public void ATextThatIsNotANumber_IsRefusedWithThePresentersWords_AndTheSetterIsNotCalled(string typed)
        {
            int calls = 0;
            CadPropertyRow row = CadPropertyRow.Number("Diameter", 1.0, v =>
            {
                calls++;
                return null;
            });

            Assert.Equal("Diameter is a number.", row.Setter(typed));
            Assert.Equal(0, calls);
        }

        [Fact]
        public void ALength_IsWrittenAndReadInTheUnitsOfTheDrawing()
        {
            double? got = null;
            Func<double, string> setter = v =>
            {
                got = v;
                return null;
            };

            CadPropertyRow decimalRow = CadPropertyRow.Length("Diameter", 12.5, Document(2, 4), setter);
            Assert.Equal("12.5000", decimalRow.Value);
            Assert.Equal(CadPropertyKind.Length, decimalRow.Kind);
            Assert.Null(decimalRow.Setter("12.5"));
            Assert.Equal(12.5, got.Value, 12);

            CadPropertyRow architectural = CadPropertyRow.Length("Diameter", 14.0, Document(4, 4), setter);
            Assert.Equal("1'-2\"", architectural.Value);
            Assert.Null(architectural.Setter("1'2\""));
            Assert.Equal(14.0, got.Value, 12);

            CadPropertyRow fewer = CadPropertyRow.Length("Diameter", 12.5, Document(2, 1), setter);
            Assert.Equal("12.5", fewer.Value);
        }

        [Fact]
        public void ALengthThatDoesNotRead_IsRefused_AndTheSetterIsNotCalled()
        {
            int calls = 0;
            CadPropertyRow row = CadPropertyRow.Length("Diameter", 1.0, Document(), v =>
            {
                calls++;
                return null;
            });

            Assert.Equal("Diameter is a number.", row.Setter("abc"));
            Assert.Equal(0, calls);
        }

        [Fact]
        public void AnAngle_IsWrittenInRadiansAsTheDrawingWritesAngles_AndReadBack()
        {
            double? got = null;
            CadPropertyRow row = CadPropertyRow.Angle("Rotation", Math.PI / 2.0, Document(aunits: 0, auprec: 0), v =>
            {
                got = v;
                return null;
            });

            Assert.Equal("90", row.Value);
            Assert.Equal(CadPropertyKind.Angle, row.Kind);
            Assert.Null(row.Setter("45"));
            Assert.Equal(Math.PI / 4.0, got.Value, 12);
            Assert.Equal("Rotation is a number.", row.Setter("abc"));

            Assert.Equal("90.00", CadPropertyRow.Angle("R", Math.PI / 2.0, Document(auprec: 2), null).Value);
        }

        [Fact]
        public void APoint_IsThreeLengthRows_AndTypingOneChangesThatCoordinateAlone()
        {
            GeoPoint3? got = null;
            IReadOnlyList<CadPropertyRow> rows = CadPropertyRow.Point(
                "Center",
                new GeoPoint3(1, 2, 3),
                Document(2, 2),
                p =>
                {
                    got = p;
                    return null;
                }
            );

            Assert.Equal(
                new[] { "Center X", "Center Y", "Center Z" },
                new[] { rows[0].Label, rows[1].Label, rows[2].Label }
            );
            Assert.Equal(new[] { "1.00", "2.00", "3.00" }, new[] { rows[0].Value, rows[1].Value, rows[2].Value });
            Assert.All(rows, r => Assert.Equal(CadPropertyKind.Length, r.Kind));

            Assert.Null(rows[1].Setter("7"));

            Assert.Equal(1.0, got.Value.X, 12);
            Assert.Equal(7.0, got.Value.Y, 12);
            Assert.Equal(3.0, got.Value.Z, 12);
            Assert.Equal("Center X is a number.", rows[0].Setter("x"));
        }

        [Fact]
        public void ARowWithNoSetter_IsOnlyRead()
        {
            CadDocument document = Document();
            DxfColor red = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);

            Assert.All(
                new[]
                {
                    CadPropertyRow.Number("N", 1.0, null),
                    CadPropertyRow.Length("L", 1.0, document, null),
                    CadPropertyRow.Angle("A", 1.0, document, null),
                    CadPropertyRow.Color("C", red, null),
                    CadPropertyRow.Layer("Y", "0", null),
                    CadPropertyRow.Linetype("T", "Continuous", null),
                    CadPropertyRow.Lineweight("W", DxfLineWeight.ByLayer, null),
                },
                row =>
                {
                    Assert.True(row.IsReadOnly);
                    Assert.Null(row.Setter);
                }
            );
            Assert.All(CadPropertyRow.Point("P", new GeoPoint3(1, 2, 3), document, null), r =>
                Assert.True(r.IsReadOnly)
            );
        }

        [Fact]
        public void AColour_IsWrittenAndReadAsThePaletteDoes_AndAnotherTextIsRefused()
        {
            DxfColor got = null;
            int calls = 0;
            CadPropertyRow row = CadPropertyRow.Color(
                "Mark color",
                DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1),
                c =>
                {
                    got = c;
                    calls++;
                    return null;
                }
            );

            Assert.Equal("Red", row.Value);
            Assert.Equal(CadPropertyKind.Color, row.Kind);

            Assert.Null(row.Setter("Yellow"));
            Assert.Equal(2, got.ColorIndex);
            Assert.Null(row.Setter("255,128,0"));
            Assert.True(got.IsByColor);
            Assert.Equal(128, got.Green);
            Assert.Null(row.Setter("ByLayer"));
            Assert.True(got.IsByLayer);
            Assert.Equal(3, calls);

            string refusal = row.Setter("nonsense");

            Assert.StartsWith("A colour is ByLayer, ByBlock", refusal);
            Assert.Equal(3, calls);
        }

        [Fact]
        public void ALayerAndALinetype_GoToTheSetterAsTyped_ItsOwnRefusalWrittenUnchanged()
        {
            string layer = null;
            string linetype = null;
            CadPropertyRow layers = CadPropertyRow.Layer("Mark layer", "Mark", v =>
            {
                layer = v;
                return v == "Nope" ? "Circle tools: no layer Nope." : null;
            });
            CadPropertyRow linetypes = CadPropertyRow.Linetype("Mark linetype", "Continuous", v =>
            {
                linetype = v;
                return null;
            });

            Assert.Equal(CadPropertyKind.Layer, layers.Kind);
            Assert.Equal(CadPropertyKind.Linetype, linetypes.Kind);
            Assert.Equal("Mark", layers.Value);
            Assert.Null(layers.Setter("Visible"));
            Assert.Equal("Visible", layer);
            Assert.Equal("Circle tools: no layer Nope.", layers.Setter("Nope"));
            Assert.Null(linetypes.Setter("HIDDEN"));
            Assert.Equal("HIDDEN", linetype);
        }

        [Fact]
        public void ALineweight_IsWrittenAndReadAsThePaletteDoes_AndAnotherTextIsRefused()
        {
            DxfLineWeight? got = null;
            int calls = 0;
            CadPropertyRow row = CadPropertyRow.Lineweight("Mark weight", DxfLineWeight.ByLayer, w =>
            {
                got = w;
                calls++;
                return null;
            });

            Assert.Equal("ByLayer", row.Value);
            Assert.Equal(CadPropertyKind.Lineweight, row.Kind);
            Assert.Equal("0.25 mm", CadPropertyRow.Lineweight("W", (DxfLineWeight)25, null).Value);

            Assert.Null(row.Setter("0.25 mm"));
            Assert.Equal((DxfLineWeight)25, got.Value);
            Assert.Null(row.Setter("ByBlock"));
            Assert.Equal(DxfLineWeight.ByBlock, got.Value);
            Assert.StartsWith("A lineweight is ByLayer", row.Setter("0.31 mm"));
            Assert.Equal(2, calls);
        }

        [Fact]
        public void ARowWithoutALabelOrADocument_IsRefusedAtOnce()
        {
            Assert.Throws<ArgumentNullException>(() => CadPropertyRow.Number(null, 1.0, null));
            Assert.Throws<ArgumentNullException>(() => CadPropertyRow.Length("L", 1.0, null, null));
            Assert.Throws<ArgumentNullException>(() => CadPropertyRow.Angle("A", 1.0, null, null));
            Assert.Throws<ArgumentNullException>(() => CadPropertyRow.Point("P", new GeoPoint3(1, 2, 3), null, null));
            Assert.Throws<ArgumentNullException>(() => CadPropertyRow.Color("C", null, null));
        }
    }
}
