using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The drawing NEW starts from: the presenter's metric template, the settings of acadiso.dwt made in code, each written in
    /// the header so that AutoCAD, which reads a variable left out as its imperial drawing has it, opens it as metric too.
    /// </summary>
    public class TemplateTests
    {
        private static string Header(DxfDatabase database, string name)
        {
            IReadOnlyList<DxfTypedValue> groups = database.GetHeader(name);
            Assert.True(groups != null, name + " is not written in the header.");
            return string.Join(";", groups.Select(g => g.TypeCode + "=" + g.Value));
        }

        [Fact]
        public void TheTemplateIsMetricInMillimetres_EachSettingWrittenInTheHeader()
        {
            DxfDatabase database = CadTemplate.CreateMetric();

            Assert.Equal("AC1027", database.Version);
            Assert.Equal("70=1", Header(database, "$MEASUREMENT"));
            Assert.Equal("70=4", Header(database, "$INSUNITS"));
            Assert.Equal("70=2", Header(database, "$LUNITS"));
            Assert.Equal("70=4", Header(database, "$LUPREC"));
            Assert.Equal("70=0", Header(database, "$AUNITS"));
            Assert.Equal("70=0", Header(database, "$AUPREC"));
            Assert.Equal("40=1", Header(database, "$LTSCALE"));
            Assert.Equal("70=0", Header(database, "$PDMODE"));
            Assert.Equal("40=0", Header(database, "$PDSIZE"));
            Assert.Equal("40=2.5", Header(database, "$TEXTSIZE"));
            Assert.Equal("10=420.0;20=297.0", Header(database, "$LIMMAX"));
            Assert.Equal("7=Standard", Header(database, "$TEXTSTYLE"));
            Assert.Equal("2=ISO-25", Header(database, "$DIMSTYLE"));
            Assert.Equal(DxfMeasurementValue.Metric, database.Measurement);
            Assert.Equal(DxfUnits.Millimeters, database.Insunits);
            Assert.Equal(4, database.Luprec);
            Assert.Equal(new GeoPoint2(420, 297), database.Limmax);
            Assert.Equal("ISO-25", database.Dimstyle);
        }

        [Fact]
        public void Iso25HasAutoCadsMetricValues_AndTheHeaderHoldsEachOfThem()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            DxfDimStyleTableRecord iso = database.DimStyleTable["ISO-25"];

            Assert.Equal(2.5, iso.Dimasz);
            Assert.Equal(2.5, iso.Dimtxt);
            Assert.Equal(2.5, iso.Dimcen);
            Assert.Equal(1.25, iso.Dimexe);
            Assert.Equal(0.625, iso.Dimexo);
            Assert.Equal(0.625, iso.Dimgap);
            Assert.Equal(3.75, iso.Dimdli);
            Assert.Equal(2, iso.Dimdec);
            Assert.Equal(2, iso.Dimtdec);
            Assert.Equal(1, iso.Dimtad);
            Assert.False(iso.Dimtih);
            Assert.False(iso.Dimtoh);
            Assert.True(iso.Dimtofl);
            Assert.Equal(',', iso.Dimdsep);
            Assert.Equal(8, iso.Dimzin);
            Assert.Equal("Standard", iso.Dimtxsty.Name);
            Assert.Null(iso.Dimblk);
            Assert.Equal("40=2.5", Header(database, "$DIMASZ"));
            Assert.Equal("70=44", Header(database, "$DIMDSEP"));
            Assert.Equal("70=1", Header(database, "$DIMTAD"));
            Assert.Equal("70=0", Header(database, "$DIMTIH"));

            // Every variable of the style is the one new dimensions take from the header, and the one the library reads for
            // AutoCAD's metric drawing where a header gives none.
            DxfDimStyleTableRecord header = database.GetDimstyleData();
            DxfDimStyleTableRecord metric = new DxfDatabase("AC1027").GetDimstyleData();

            foreach (
                PropertyInfo property in typeof(DxfDimStyleTableRecord)
                    .GetProperties()
                    .Where(p =>
                        p.Name.StartsWith("Dim", StringComparison.Ordinal) && p.GetIndexParameters().Length == 0
                    )
            )
            {
                string own = Text(property.GetValue(iso));
                Assert.True(
                    own == Text(property.GetValue(header)),
                    property.Name + " of ISO-25 is " + own + ", the header's " + Text(property.GetValue(header)) + "."
                );
                Assert.True(
                    own == Text(property.GetValue(metric)),
                    property.Name + " of ISO-25 is " + own + ", acadiso's " + Text(property.GetValue(metric)) + "."
                );
            }
        }

        private static string Text(object value)
        {
            switch (value)
            {
                case null:
                    return "(none)";
                case DxfSymbolTableRecord record:
                    return record.Name;
                case DxfColor color:
                    return color.ColorMethod + " " + color.ColorIndex;
                case double number:
                    return number.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }

        [Fact]
        public void TheTemplateHoldsLayer0_TheThreeLinetypes_StandardAndAnnotative_AndReadsBackAsWritten()
        {
            DxfDatabase database = CadTemplate.CreateMetric();

            Assert.Equal(new[] { "0" }, database.LayerTable.Cast<DxfLayerTableRecord>().Select(l => l.Name));
            Assert.Equal(
                new[] { "ByBlock", "ByLayer", "Continuous" },
                database.LinetypeTable.Cast<DxfLinetypeTableRecord>().Select(l => l.Name)
            );
            Assert.Equal(
                new[] { "Standard", "Annotative" },
                database.TextStyleTable.Cast<DxfTextStyleTableRecord>().Select(s => s.Name)
            );
            Assert.Equal("arial.ttf", database.TextStyleTable["Standard"].FileName);
            Assert.Equal(DxfAnnotativeStates.False, database.TextStyleTable["Standard"].Annotative);
            Assert.Equal(DxfAnnotativeStates.True, database.TextStyleTable["Annotative"].Annotative);
            Assert.Equal(
                new[] { "Standard", "ISO-25" },
                database.DimStyleTable.Cast<DxfDimStyleTableRecord>().Select(s => s.Name)
            );
            Assert.Empty(database.ModelSpace);

            string text = database.ToText();
            Assert.Equal(text, DxfDatabase.Parse(text).ToText());
        }

        [Fact]
        public void ADimensionDrawnInTheTemplate_IsInIso25_ItsTextWithADecimalComma()
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric(), "Drawing1.dxf");

            EditingDrawings.Run(
                EditingDrawings.Session(document),
                "DLI",
                EditingDrawings.P(0, 0),
                EditingDrawings.P(1234.5, 0),
                EditingDrawings.P(600, -30)
            );

            DxfRotatedDimension dimension = Assert.Single(
                EditingDrawings.All<DxfRotatedDimension>(document.ModelSpace)
            );
            Assert.Equal("ISO-25", dimension.DimensionStyleName);
            Assert.Contains(dimension.DimBlock.OfType<DxfMText>(), m => m.Text.Contains("1234,5"));
        }
    }
}
