using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The presenter's own linetypes that carry the names AutoCAD users type (CENTER, HIDDEN, DASHED and the rest) have the
    /// lengths AutoCAD gives them, so that one drawing looks the same in both programs: millimetres, which are the inch
    /// values times 25.4, in a drawing whose MEASUREMENT is 1, and inches where it is 0. Loading picks them by the drawing's
    /// MEASUREMENT; a linetype the drawing has already is never changed.
    /// </summary>
    public class BuiltInLinetypeUnitsTests
    {
        private static readonly string[] Bases =
        {
            "BORDER",
            "CENTER",
            "DASHDOT",
            "DASHED",
            "DIVIDE",
            "DOT",
            "HIDDEN",
            "PHANTOM",
        };

        private static CadDocument Metric()
        {
            CadDocument document = Blank();
            document.Database.Measurement = DxfMeasurementValue.Metric;
            return document;
        }

        private static CadDocument Imperial()
        {
            CadDocument document = Blank();
            document.Database.Measurement = DxfMeasurementValue.English;
            return document;
        }

        private static CadLinetypeLoad LoadOwn(CadDocument document, params string[] names) =>
            CadLinetypes.Load(
                document,
                CadLinetypeFile.BuiltIn.Where(l => names.Contains(l.Name)),
                CadLinetypeFile.BuiltInName
            );

        private static double[] Dashes(CadDocument document, string name) =>
            CadLinetypes.DashesOf(document.Database, name).ToArray();

        [Fact]
        public void CenterLoadedIntoAMetricDrawing_HasThirtyOneAndThreeQuarterMillimetres()
        {
            CadDocument document = Metric();

            CadLinetypeLoad load = LoadOwn(document, "CENTER");

            Assert.True(load.Result.Succeeded);
            Assert.Equal(new[] { "CENTER" }, load.Loaded.ToArray());
            Assert.Equal(new[] { 31.75, -6.35, 6.35, -6.35 }, Dashes(document, "CENTER"));
        }

        [Fact]
        public void CenterLoadedIntoAnImperialDrawing_HasAnInchAndAQuarter()
        {
            CadDocument document = Imperial();

            CadLinetypeLoad load = LoadOwn(document, "CENTER");

            Assert.True(load.Result.Succeeded);
            Assert.Equal(new[] { 1.25, -0.25, 0.25, -0.25 }, Dashes(document, "CENTER"));
        }

        [Fact]
        public void HiddenLoadedIntoAMetricDrawing_IsADashOfSixAndAThirdAndAGapOfThree()
        {
            CadDocument document = Metric();

            LoadOwn(document, "HIDDEN");

            Assert.Equal(new[] { 6.35, -3.175 }, Dashes(document, "HIDDEN"));
        }

        [Fact]
        public void ACenterInTheDrawing_IsNeverChangedByLoadingTheOwnOne_InEitherUnit()
        {
            foreach (
                DxfMeasurementValue measurement in new[] { DxfMeasurementValue.Metric, DxfMeasurementValue.English }
            )
            {
                CadDocument document = Blank();
                document.Database.Measurement = measurement;
                CadLinetypes.Add(
                    document.Database,
                    CadLinetypeFile.ParsePattern("CENTER", "My own center", "A,12,-1.5,3,-1.5")
                );

                CadLinetypeLoad load = LoadOwn(document, "CENTER");

                Assert.Equal(new[] { "CENTER" }, load.Present.ToArray());
                Assert.Empty(load.Loaded);
                Assert.Equal(new[] { 12.0, -1.5, 3.0, -1.5 }, Dashes(document, "CENTER"));
            }
        }

        [Fact]
        public void EveryOwnNameOfAutoCad_InAMetricDrawing_IsItsInchesTimesTwentyFiveAndFourTenths()
        {
            CadDocument metric = Metric();
            CadDocument imperial = Imperial();
            string[] names = Bases
                .SelectMany(b => new[] { b, b + "2", b + "X2" })
                .Concat(new[] { "GAS_LINE" })
                .ToArray();

            Assert.Empty(LoadOwn(metric, names).Refused);
            Assert.Empty(LoadOwn(imperial, names).Refused);

            foreach (string name in names)
            {
                double[] inches = Dashes(imperial, name);
                double[] millimetres = Dashes(metric, name);
                Assert.Equal(inches.Length, millimetres.Length);

                for (int i = 0; i < inches.Length; i++)
                {
                    // GAS_LINE's gap of 0.2 inch is 5.08 millimetres and the others are exact to the figure given.
                    Assert.Equal(inches[i] * 25.4, millimetres[i], 9);
                }
            }
        }

        [Fact]
        public void TheDashesOfTheNamesOfAutoCad_AreThoseOfTheTwoLinFiles()
        {
            CadDocument document = Metric();
            LoadOwn(
                document,
                "BORDER",
                "DASHDOT2",
                "DIVIDEX2",
                "DOT",
                "PHANTOM",
                "PHANTOM2",
                "CENTER2",
                "CENTERX2",
                "DASHED"
            );

            Assert.Equal(new[] { 12.7, -6.35, 12.7, -6.35, 0.0, -6.35 }, Dashes(document, "BORDER"));
            Assert.Equal(new[] { 6.35, -3.175, 0.0, -3.175 }, Dashes(document, "DASHDOT2"));
            Assert.Equal(new[] { 25.4, -12.7, 0.0, -12.7, 0.0, -12.7 }, Dashes(document, "DIVIDEX2"));
            Assert.Equal(new[] { 0.0, -6.35 }, Dashes(document, "DOT"));
            Assert.Equal(new[] { 31.75, -6.35, 6.35, -6.35, 6.35, -6.35 }, Dashes(document, "PHANTOM"));
            Assert.Equal(new[] { 15.875, -3.175, 3.175, -3.175, 3.175, -3.175 }, Dashes(document, "PHANTOM2"));
            Assert.Equal(new[] { 19.05, -3.175, 3.175, -3.175 }, Dashes(document, "CENTER2"));
            Assert.Equal(new[] { 63.5, -12.7, 12.7, -12.7 }, Dashes(document, "CENTERX2"));
            Assert.Equal(new[] { 12.7, -6.35 }, Dashes(document, "DASHED"));
        }

        [Fact]
        public void TheIsoTypes_KeepTheirMillimetres_InAnImperialDrawingToo()
        {
            CadDocument document = Imperial();

            LoadOwn(document, "ACAD_ISO02W100", "ACAD_ISO04W100");

            Assert.Equal(new[] { 12.0, -3.0 }, Dashes(document, "ACAD_ISO02W100"));
            Assert.Equal(new[] { 24.0, -3.0, 0.0, -3.0 }, Dashes(document, "ACAD_ISO04W100"));
        }

        [Fact]
        public void TheOwnSetItself_StaysInMillimetres_WhateverTheDrawingIs()
        {
            CadLinetypeDefinition center = CadLinetypeFile.BuiltIn.Single(l => l.Name == "CENTER");

            Assert.Equal(new[] { 31.75, -6.35, 6.35, -6.35 }, center.Elements.Select(e => e.Length).ToArray());
            CadDocument document = Imperial();
            LoadOwn(document, "CENTER");
            Assert.Equal(new[] { 31.75, -6.35, 6.35, -6.35 }, center.Elements.Select(e => e.Length).ToArray());
        }

        [Fact]
        public void ACenterMarkInAnImperialDrawing_LoadsCenter2InInches()
        {
            CadDocument document = Imperial();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "CIRCLE", P(50, 50), "20");
            Run(session, "CENTERMARK", P(70, 50), Enter);

            Assert.Equal(new[] { 0.75, -0.125, 0.125, -0.125 }, Dashes(document, "CENTER2"));
        }

        [Fact]
        public void TheLayerCommandsLtype_LoadsTheOwnLinetypeInTheDrawingsUnits()
        {
            CadDocument metric = Metric();
            CadDocument imperial = Imperial();
            Run(Session(metric), "-LAYER", "LTYPE", "HIDDEN", "0", Enter);
            Run(Session(imperial), "-LAYER", "LTYPE", "HIDDEN", "0", Enter);

            Assert.Equal(new[] { 6.35, -3.175 }, Dashes(metric, "HIDDEN"));
            Assert.Equal(new[] { 0.25, -0.125 }, Dashes(imperial, "HIDDEN"));
        }

        [Fact]
        public void TheShippedLinFile_Parses_AndHasTheMillimetresOfAutoCadForCenterAndHidden()
        {
            string path = Path.Combine(
                CadSupportFolders.Default.FolderOf(CadSupportKind.Linetypes),
                "GeometryHelper.lin"
            );

            IReadOnlyList<CadLinetypeDefinition> shipped = CadLinetypeFile.Read(path);

            Assert.All(shipped, l => Assert.Null(l.Fault));
            Assert.Equal(
                new[] { 31.75, -6.35, 6.35, -6.35 },
                shipped.Single(l => l.Name == "CENTER").Elements.Select(e => e.Length).ToArray()
            );
            Assert.Equal(
                new[] { 6.35, -3.175 },
                shipped.Single(l => l.Name == "HIDDEN").Elements.Select(e => e.Length).ToArray()
            );
            Assert.Equal(
                new[] { 12.7, -5.08, -6.35 },
                shipped.Single(l => l.Name == "GAS_LINE").Elements.Select(e => e.Length).ToArray()
            );
        }

        [Fact]
        public void TheSampleTemplatesCenterAndHidden_CarryTheMillimetresOfAutoCad()
        {
            DxfDatabase sample = CadDrawingFile.Open(
                Path.Combine(
                    CadSupportFolders.Default.FolderOf(CadSupportKind.DrawingTemplates),
                    CadTemplate.SampleFileName
                )
            );

            Assert.Equal(new[] { 31.75, -6.35, 6.35, -6.35 }, CadLinetypes.DashesOf(sample, "CENTER").ToArray());
            Assert.Equal(new[] { 6.35, -3.175 }, CadLinetypes.DashesOf(sample, "HIDDEN").ToArray());
        }
    }
}
