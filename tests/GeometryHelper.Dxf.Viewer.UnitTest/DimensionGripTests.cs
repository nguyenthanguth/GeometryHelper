using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>The grips of dimensions, as AutoCAD 2021 gives them, dragged with no window: the points rewritten, the picture drawn anew.</summary>
    public class DimensionGripTests
    {
        /// <summary>A blank drawing with a level dimension of 100 from (0, 0) to (100, 0), its line 30 below.</summary>
        private static CadDocument WithDimension(out CadCommandSession session, out string handle)
        {
            CadDocument document = Blank();
            document.Database.DimStyleTable["Standard"].Dimtad = 1;
            session = Session(document);
            Run(session, "DLI", P(0, 0), P(100, 0), P(50, -30));
            handle = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
            return document;
        }

        private static string Words(DxfDimension dimension) =>
            dimension.DimBlock.OfType<DxfMText>().Single(m => !m.IsErased).Contents;

        [Fact]
        public void ALinearDimensionHasItsTwoOriginsTheTwoEndsOfItsLineAndItsWords()
        {
            CadDocument document = WithDimension(out _, out string handle);
            IReadOnlyList<CadGrip> grips = CadGrips.Of(document.FindEntity(handle));

            Assert.Equal(
                new[]
                {
                    CadGripKind.DefinitionPoint,
                    CadGripKind.DefinitionPoint,
                    CadGripKind.DimensionLine,
                    CadGripKind.DimensionLine,
                    CadGripKind.Text,
                },
                grips.Select(g => g.Kind)
            );
            Near(P(0, 0), grips[0].Point);
            Near(P(100, 0), grips[1].Point);
            Near(P(0, -30), grips[2].Point);
            Near(P(100, -30), grips[3].Point);
            Assert.Equal(50, grips[4].Point.X, 9);
            Assert.True(grips[4].Point.Y > -30, "The words stand above the line at DIMTAD 1.");
        }

        [Fact]
        public void ASecondOriginDragged_MeasuresAnew_TheLineStaysWhereItWas_OneUndoStep()
        {
            CadDocument document = WithDimension(out _, out string handle);
            string start = document.Database.ToText();

            Assert.True(document.GripDrag(handle, 1, P(150, 10)).Succeeded);

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Near(new GeoPoint3(150, 10, 0), dimension.XLine2Point);
            Near(new GeoPoint3(150, -30, 0), dimension.DimLinePoint);
            Assert.Equal(150, dimension.Measurement, 9);
            Assert.Contains("150", Words(dimension), StringComparison.Ordinal);
            Assert.Equal("GRIP", document.NextUndo.Name);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void AnEndOfTheLineDragged_MovesTheLineThroughThePoint_AndWordsPlacedGoWithIt()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);

            Assert.True(document.GripDrag(handle, 2, P(20, -60)).Succeeded);
            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Near(new GeoPoint3(100, -60, 0), dimension.DimLinePoint);
            Assert.Equal(100, dimension.Measurement, 9);
            Near(P(0, -60), CadGrips.Of(dimension)[2].Point);

            // Words placed by DIMTEDIT at (80, -40) go down by the 20 the line goes down by.
            Run(session, "DIMTEDIT", new[] { handle }, P(80, -40));
            double line = Entity<DxfRotatedDimension>(document, handle).DimLinePoint.Y;
            Assert.True(document.GripDrag(handle, 3, P(100, line - 20)).Succeeded);
            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.False(dimension.UsingDefaultTextPosition);
            Assert.Equal(line - 20, dimension.DimLinePoint.Y, 9);
            Near(new GeoPoint3(80, -60, 0), dimension.TextPosition);
        }

        [Fact]
        public void TheWordsDragged_GoWhereDimteditPutsThem_TheLineWithThemAtDimtmove0()
        {
            CadDocument document = WithDimension(out _, out string handle);
            var dimension = Entity<DxfRotatedDimension>(document, handle);
            DxfDimStyleTableRecord style = dimension.GetDimstyleData();
            double scale = style.Dimscale > 0.0 ? style.Dimscale : 1.0;
            double lift = Math.Abs(style.Dimgap) * scale + dimension.Dimtxt * scale / 2.0;

            Assert.True(document.GripDrag(handle, 4, P(60, -50)).Succeeded);

            dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.False(dimension.UsingDefaultTextPosition);
            // As AutoCAD 2021's DIMTEDIT: the line through the point, the words above it there.
            Near(new GeoPoint3(60, -50, 0), dimension.TextPosition);
            Near(new GeoPoint3(100, -50, 0), dimension.DimLinePoint);
            Near(
                new GeoPoint3(60, -50 + lift, 0),
                dimension.DimBlock.OfType<DxfMText>().Single(m => !m.IsErased).Location,
                0.05
            );
            Near(P(60, -50), CadGrips.Of(dimension)[4].Point);
        }

        [Fact]
        public void AnAlignedDimensionsOriginDragged_KeepsItsLineAsFarFromWhatItMeasures()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DAL", P(0, 0), P(60, 60), P(-20, 20));
            var dimension = Assert.Single(All<DxfAlignedDimension>(document.ModelSpace));
            double off = 40.0 / Math.Sqrt(2.0);

            Assert.True(document.GripDrag(dimension.Handle, 1, P(100, 0)).Succeeded);

            dimension = Entity<DxfAlignedDimension>(document, dimension.Handle);
            Assert.Equal(100, dimension.Measurement, 9);
            Near(new GeoPoint3(100, off, 0), dimension.DimLinePoint, 1E-9);
            AssertReopens(document);
        }

        [Fact]
        public void ARadiusChordPointGoesRoundItsCircle_AndADiametersTwoPointsTurnTogether()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "C", P(300, 0), "20");
            Run(session, "DRA", P(320, 0), P(340, 10));
            Run(session, "C", P(0, 0), "20");
            Run(session, "DDI", P(-20, 0), P(-40, -40));
            var radial = Assert.Single(All<DxfRadialDimension>(document.ModelSpace));
            var diametric = Assert.Single(All<DxfDiametricDimension>(document.ModelSpace));

            Assert.Equal(
                new[] { CadGripKind.DefinitionPoint, CadGripKind.Text },
                CadGrips.Of(radial).Select(g => g.Kind)
            );
            Assert.Equal(
                new[] { CadGripKind.DefinitionPoint, CadGripKind.DefinitionPoint, CadGripKind.Text },
                CadGrips.Of(diametric).Select(g => g.Kind)
            );

            Assert.True(document.GripDrag(radial.Handle, 0, P(300, 50)).Succeeded);
            radial = Entity<DxfRadialDimension>(document, radial.Handle);
            Near(new GeoPoint3(300, 20, 0), radial.ChordPoint, 1E-9);
            Assert.Equal(20, radial.Measurement, 9);

            Assert.True(document.GripDrag(diametric.Handle, 0, P(0, 100)).Succeeded);
            diametric = Entity<DxfDiametricDimension>(document, diametric.Handle);
            Near(new GeoPoint3(0, 20, 0), diametric.ChordPoint, 1E-9);
            Near(new GeoPoint3(0, -20, 0), diametric.FarChordPoint, 1E-9);
            Assert.Equal(40, diametric.Measurement, 9);

            Assert.True(document.GripDrag(radial.Handle, 0, P(300, 0)).Refused);
            AssertReopens(document);
        }

        [Fact]
        public void AnAnglesVertexLinePointsArcAndWords_AreItsGrips_AndALinePointDraggedMeasuresAnew()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DAN", Enter, P(0, 0), P(100, 0), P(100, 100), P(40, 15));
            var angular = Assert.Single(All<DxfPoint3AngularDimension>(document.ModelSpace));
            IReadOnlyList<CadGrip> grips = CadGrips.Of(angular);

            Assert.Equal(
                new[]
                {
                    CadGripKind.DefinitionPoint,
                    CadGripKind.DefinitionPoint,
                    CadGripKind.DefinitionPoint,
                    CadGripKind.DimensionLine,
                    CadGripKind.Text,
                },
                grips.Select(g => g.Kind)
            );
            Near(P(0, 0), grips[0].Point);
            Assert.Equal(Math.PI / 4.0, angular.Measurement, 9);

            int line = (grips[2].Point - P(100, 100)).Length < 1E-9 ? 2 : 1;
            Assert.True(document.GripDrag(angular.Handle, line, P(0, 100)).Succeeded);
            angular = Entity<DxfPoint3AngularDimension>(document, angular.Handle);
            Assert.Equal(Math.PI / 2.0, angular.Measurement, 9);

            Assert.True(document.GripDrag(angular.Handle, 3, P(60, 10)).Succeeded);
            Near(new GeoPoint3(60, 10, 0), Entity<DxfPoint3AngularDimension>(document, angular.Handle).ArcPoint);
            AssertReopens(document);
        }

        [Fact]
        public void AnOrdinatesFeatureAndLeaderEnd_AreItsGrips()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DOR", P(300, 40), P(360, 45));
            var ordinate = Assert.Single(All<DxfOrdinateDimension>(document.ModelSpace));
            IReadOnlyList<CadGrip> grips = CadGrips.Of(ordinate);
            string words = Words(ordinate);

            Assert.Equal(2, grips.Count);
            Near(P(300, 40), grips[0].Point);
            Near(new GeoPoint2(ordinate.LeaderEndPoint.X, ordinate.LeaderEndPoint.Y), grips[1].Point);

            Assert.True(document.GripDrag(ordinate.Handle, 0, P(310, 47)).Succeeded);
            ordinate = Entity<DxfOrdinateDimension>(document, ordinate.Handle);
            Near(new GeoPoint3(310, 47, 0), ordinate.DefiningPoint);
            Assert.NotEqual(words, Words(ordinate));
        }

        [Fact]
        public void StretchOfAGripEdit_OnADimensionsOrigin_MeasuresAnewAsTheDragDoes()
        {
            CadDocument document = WithDimension(out CadCommandSession session, out string handle);

            Assert.True(session.StartGripEdit(new[] { new KeyValuePair<string, int>(handle, 1) }));
            Run(session, P(150, 10));

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.False(session.IsRunning);
            Assert.Equal(150, dimension.Measurement, 9);
            Near(new GeoPoint3(150, -30, 0), dimension.DimLinePoint);
            Assert.Contains("150", Words(dimension), StringComparison.Ordinal);
        }

        [Fact]
        public void AnOriginDraggedOntoTheOther_WouldMeasureNothingAndIsRefused()
        {
            CadDocument document = WithDimension(out _, out string handle);

            CadEditResult refused = document.GripDrag(handle, 1, P(0, 40));

            Assert.True(refused.Refused);
            Assert.Equal("The dimension would measure nothing with its grip there.", refused.Message);
            Assert.Equal("DIMLINEAR", document.NextUndo.Name);
        }
    }
}
