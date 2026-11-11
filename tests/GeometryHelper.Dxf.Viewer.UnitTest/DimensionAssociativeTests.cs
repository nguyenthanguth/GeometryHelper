using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Associative dimensions, as AutoCAD 2021 keeps them with DIMASSOC 2: a dimension whose points were snapped to objects
    /// follows them through MOVE, STRETCH, a grip and SCALE in the same step of undo, is untied when they are erased or it is
    /// moved on its own, and is tied anew by DIMREASSOCIATE and untied by DIMDISASSOCIATE. The numbers are those AutoCAD 2021's
    /// console gave for the same edits.
    /// </summary>
    public class DimensionAssociativeTests
    {
        private static readonly ObjectSnap Snaps = new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint | CadSnapModes.Midpoint | CadSnapModes.Center | CadSnapModes.Quadrant };

        /// <summary>Picks a point near the cursor with the object snaps on, as the window picks one.</summary>
        private static void Snap(CadCommandSession session, GeoPoint2 cursor)
        {
            CadSnapResult result = Snaps.Snap(session.Document.GetPickIndex(session.Document.ModelSpace), cursor, 2.0);
            Assert.NotEqual(CadSnapKind.None, result.Kind);
            Assert.True(session.Feed(result, false));
        }

        /// <summary>A blank drawing with a line from (0, 0) to (100, 0) and a DIMLINEAR snapped to both its ends, its line 20 above.</summary>
        private static CadDocument LineWithDimension(out CadCommandSession session, out string line, out string dimension)
        {
            CadDocument document = Blank();
            session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter);
            line = Assert.Single(All<DxfLine>(document.ModelSpace)).Handle;
            Run(session, "DIMLINEAR");
            Snap(session, P(0.4, 0.3));
            Snap(session, P(99.6, -0.3));
            Run(session, P(50, 20));
            dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
            return document;
        }

        private static string Words(DxfDimension dimension) => dimension.DimBlock.OfType<DxfMText>().Single(m => !m.IsErased).Contents;

        [Fact]
        public void ADimLinearSnappedToTheEndsOfALine_IsTiedToItsStartAndItsEnd()
        {
            CadDocument document = LineWithDimension(out _, out string line, out string handle);

            DxfDimAssoc assoc = Entity<DxfRotatedDimension>(document, handle).DimAssoc;
            Assert.NotNull(assoc);
            Assert.Equal(3, assoc.AssociativityFlags);
            Assert.Equal(new[] { DxfOsnapType.StartPoint, DxfOsnapType.Endpoint }, assoc.PointReferences.Select(r => r.OsnapType));
            Assert.All(assoc.PointReferences, r => Assert.Equal(line, r.ObjectHandle));
            Assert.Equal(new[] { assoc.Handle }, Entity<DxfLine>(document, line).GetPersistentReactorIds());
            Assert.Equal("DIMLINEAR", document.NextUndo.Name);
            AssertReopens(document);
        }

        [Fact]
        public void ADimLinearOfPointsTypedWithNoSnap_IsTiedToNothing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "DIMLINEAR", P(0, 0), P(100, 0), P(50, 20));

            Assert.Null(Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).DimAssoc);
        }

        [Fact]
        public void AtDimAssocOne_ASnappedDimension_IsTiedToNothing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DIMASSOC", "1", "LINE", P(0, 0), P(100, 0), Enter, "DIMLINEAR");
            Snap(session, P(0.4, 0.3));
            Snap(session, P(99.6, -0.3));
            Run(session, P(50, 20));

            Assert.Equal(1, document.Database.DimAssoc);
            Assert.Null(Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).DimAssoc);
        }

        [Fact]
        public void TheLineMoved_CarriesTheDimensionWhole_InTheSameUndoStep()
        {
            CadDocument document = LineWithDimension(out CadCommandSession session, out string line, out string handle);
            string before = document.Database.ToText();
            document.Selection.Set(new[] { line });
            CadDocumentChangedEventArgs changed = null;
            document.Changed += (s, e) => changed = e;

            Run(session, "MOVE", P(0, 0), P(10, 5));

            // The dimension is among the entities the step touched, so that the canvas draws it again with the line.
            Assert.Contains(handle, changed.Handles);
            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Near(new GeoPoint3(10, 5, 0), dimension.XLine1Point);
            Near(new GeoPoint3(110, 5, 0), dimension.XLine2Point);
            Near(new GeoPoint3(110, 25, 0), dimension.DimLinePoint);
            Assert.NotNull(dimension.DimAssoc);
            Assert.Equal("MOVE", document.NextUndo.Name);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void OneEndStretched_MovesThatOrigin_TheDimensionLineStaying_AndMeasuresAnew()
        {
            CadDocument document = LineWithDimension(out CadCommandSession session, out _, out string handle);

            Run(session, "STRETCH", P(110, -10), P(90, 10), Enter, P(0, 0), P(20, 30));

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Near(new GeoPoint3(0, 0, 0), dimension.XLine1Point);
            Near(new GeoPoint3(120, 30, 0), dimension.XLine2Point);
            Near(new GeoPoint3(120, 20, 0), dimension.DimLinePoint);
            Assert.Contains("120", Words(dimension), StringComparison.Ordinal);
            Assert.Equal("STRETCH", document.NextUndo.Name);
        }

        [Fact]
        public void TheDimensionFollowingAnEdit_IsDrawnAgainInThePatchedDisplay_AsARegenDrawsIt()
        {
            CadDocument document = LineWithDimension(out CadCommandSession session, out string line, out string handle);
            string before = Items(document, handle);
            Assert.NotEmpty(before);
            document.Selection.Set(new[] { line });

            Run(session, "MOVE", P(0, 0), P(10, 5));

            string patched = Items(document, handle);
            document.Regen();
            Assert.Equal(Items(document, handle), patched);
            Assert.NotEqual(before, patched);
        }

        /// <summary>The boxes of the display items of an entity in model space, as the canvas has them now.</summary>
        private static string Items(CadDocument document, string handle)
            => string.Join(";", document.GetDisplay(document.ModelSpace).Items.Where(i => i.Handle == handle).Select(i => FormattableString.Invariant($"{i.Bounds.MinX:R},{i.Bounds.MinY:R},{i.Bounds.MaxX:R},{i.Bounds.MaxY:R}")));

        [Fact]
        public void TheEndOfTheLineDraggedByItsGrip_IsFollowed_AndOneUndoTakesBothBack()
        {
            CadDocument document = LineWithDimension(out _, out string line, out string handle);
            string before = document.Database.ToText();

            Assert.True(document.GripDrag(line, 1, P(150, 0)).Succeeded);

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Near(new GeoPoint3(150, 0, 0), dimension.XLine2Point);
            Near(new GeoPoint3(150, 20, 0), dimension.DimLinePoint);
            Assert.Contains("150", Words(dimension), StringComparison.Ordinal);

            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
            Near(new GeoPoint3(100, 0, 0), Entity<DxfRotatedDimension>(document, handle).XLine2Point);
        }

        [Fact]
        public void Stretch_MovesTheOriginOfALinearDimensionInsideItsWindow_TheDimensionLineStaying()
        {
            // AutoCAD 2021's STRETCH moves the points of a dimension inside its windows, as it moves the ends of a line.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DIMLINEAR", P(0, 0), P(100, 0), P(50, 20));
            string handle = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;

            Run(session, "STRETCH", P(110, -10), P(90, 10), Enter, P(0, 0), P(20, 30));

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Near(new GeoPoint3(0, 0, 0), dimension.XLine1Point);
            Near(new GeoPoint3(120, 30, 0), dimension.XLine2Point);
            Near(new GeoPoint3(120, 20, 0), dimension.DimLinePoint);
            Assert.Contains("120", Words(dimension), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(120, 30, 50)]
        [InlineData(120, 10, 30)]
        [InlineData(120, -30, 20)]
        public void AnEndOfTheLineMoved_LeavesTheDimensionLineAsFarBeyondTheNearerOrigin_AsAutoCadLeavesIt(double x, double y, double line)
        {
            // AutoCAD 2021: the line (0, 0)-(100, 0), its DIMLINEAR at y = 20, the end set to (120, 30), (120, 10) and (120, -30):
            // the dimension line at y = 50, 30 and 20, 20 beyond the origin nearer it.
            CadDocument document = LineWithDimension(out _, out string drawn, out string handle);

            Assert.True(document.GripDrag(drawn, 1, P(x, y)).Succeeded);

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Near(new GeoPoint3(x, y, 0), dimension.XLine2Point);
            Assert.Equal(line, dimension.DimLinePoint.Y, 9);
        }

        [Fact]
        public void ARadiusOfACircleScaledTwice_DrawsItsLineOutAlongTheSameWay_TheCenterStaying()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "CIRCLE", P(200, 0), "25");
            string circle = Assert.Single(All<DxfCircle>(document.ModelSpace)).Handle;
            Run(session, "DIMRADIUS", P(225, 0), P(260, 10));
            var radius = Assert.Single(All<DxfRadialDimension>(document.ModelSpace));
            Assert.Equal(DxfOsnapType.Center, Assert.Single(radius.DimAssoc.PointReferences).OsnapType);
            GeoVector3 way = radius.ChordPoint - radius.Center;
            string before = document.Database.ToText();
            document.Selection.Set(new[] { circle });

            Run(session, "SCALE", P(200, 0), "2");

            radius = Entity<DxfRadialDimension>(document, radius.Handle);
            Near(new GeoPoint3(200, 0, 0), radius.Center);
            Assert.Equal(50, (radius.ChordPoint - radius.Center).Length, 9);
            Assert.Equal(0.0, Math.Abs(way.X * (radius.ChordPoint - radius.Center).Y - way.Y * (radius.ChordPoint - radius.Center).X), 6);
            Assert.Contains("50", Words(radius), StringComparison.Ordinal);
            Assert.Equal("SCALE", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void TheLineErased_UntiesTheDimension_WhichKeepsItsPoints_AndUndoTiesItAgain()
        {
            CadDocument document = LineWithDimension(out CadCommandSession session, out string line, out string handle);
            document.Selection.Set(new[] { line });

            Run(session, "ERASE");

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Null(dimension.DimAssoc);
            Assert.Null(dimension.ExtensionDictionary);
            Near(new GeoPoint3(100, 0, 0), dimension.XLine2Point);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.NotNull(Entity<DxfRotatedDimension>(document, handle).DimAssoc);
        }

        [Fact]
        public void TheDimensionMovedOnItsOwn_IsUntied_AsAutoCadUntiesIt()
        {
            CadDocument document = LineWithDimension(out CadCommandSession session, out string line, out string handle);
            document.Selection.Set(new[] { handle });

            Run(session, "MOVE", P(0, 0), P(0, 30));

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.Null(dimension.DimAssoc);
            Near(new GeoPoint3(0, 30, 0), dimension.XLine1Point);
            Assert.Empty(Entity<DxfLine>(document, line).GetPersistentReactorIds());
        }

        [Fact]
        public void TheLineAndItsDimensionMovedTogether_StayTied()
        {
            CadDocument document = LineWithDimension(out CadCommandSession session, out string line, out string handle);
            document.Selection.Set(new[] { line, handle });

            Run(session, "MOVE", P(0, 0), P(0, 30));

            var dimension = Entity<DxfRotatedDimension>(document, handle);
            Assert.NotNull(dimension.DimAssoc);
            Near(new GeoPoint3(0, 30, 0), dimension.XLine1Point);
            Near(new GeoPoint3(100, 50, 0), dimension.DimLinePoint);
        }

        [Fact]
        public void ACopyOfTheLine_CarriesNoTie_SoMovingItLeavesTheDimension()
        {
            CadDocument document = LineWithDimension(out CadCommandSession session, out string line, out string handle);
            document.Selection.Set(new[] { line });
            Run(session, "COPY", P(0, 0), P(0, 60), Enter);
            string copy = All<DxfLine>(document.ModelSpace).Single(l => l.Handle != line).Handle;

            Assert.Empty(Entity<DxfLine>(document, copy).GetPersistentReactorIds());
            document.Selection.Set(new[] { copy });
            Run(session, "MOVE", P(0, 0), P(0, 30));

            Near(new GeoPoint3(0, 0, 0), Entity<DxfRotatedDimension>(document, handle).XLine1Point);
            Assert.NotNull(Entity<DxfRotatedDimension>(document, handle).DimAssoc);
        }

        [Fact]
        public void ACopyOfTheDimension_IsTiedToNothing_TheOriginalStillTied()
        {
            CadDocument document = LineWithDimension(out CadCommandSession session, out string line, out string handle);
            document.Selection.Set(new[] { handle });
            Run(session, "COPY", P(0, 0), P(0, 60), Enter);
            DxfRotatedDimension copy = All<DxfRotatedDimension>(document.ModelSpace).Single(d => d.Handle != handle);

            Assert.Null(copy.DimAssoc);
            Assert.NotNull(Entity<DxfRotatedDimension>(document, handle).DimAssoc);
            Assert.Single(Entity<DxfLine>(document, line).GetPersistentReactorIds());
            AssertReopens(document);
        }

        [Fact]
        public void DimReassociate_TiesAnUntiedDimensionToAnotherLine_AndMovesItThere()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "DIMLINEAR", P(0, 0), P(100, 0), P(50, 20), "LINE", P(0, -50), P(80, -50), Enter);
            var dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            string other = All<DxfLine>(document.ModelSpace).Single(l => l.StartPoint.Y < -1).Handle;
            Assert.Null(dimension.DimAssoc);

            Run(session, "DIMREASSOCIATE", new[] { dimension.Handle }, Enter);
            Assert.Equal("Specify first extension line origin or [Select object] <next>:", session.PromptText);
            Snap(session, P(0.3, -49.7));
            Assert.Equal("Specify second extension line origin <next>:", session.PromptText);
            Snap(session, P(79.8, -50.2));

            dimension = Entity<DxfRotatedDimension>(document, dimension.Handle);
            Assert.All(dimension.DimAssoc.PointReferences, r => Assert.Equal(other, r.ObjectHandle));
            Near(new GeoPoint3(0, -50, 0), dimension.XLine1Point);
            Near(new GeoPoint3(80, -50, 0), dimension.XLine2Point);
            Assert.Contains("80", Words(dimension), StringComparison.Ordinal);
            Assert.Equal("DIMREASSOCIATE", document.NextUndo.Name);

            document.Selection.Set(new[] { other });
            Run(session, "MOVE", P(0, 0), P(0, -10));
            Near(new GeoPoint3(80, -60, 0), Entity<DxfRotatedDimension>(document, dimension.Handle).XLine2Point);
        }

        [Fact]
        public void DimDisassociate_UntiesTheDimension_AndSaysHowMany()
        {
            CadDocument document = LineWithDimension(out CadCommandSession session, out string line, out string handle);

            Run(session, "-DIMDISASSOCIATE", new[] { handle, line }, Enter);

            Assert.Contains("1 disassociated.", session.History);
            Assert.Null(Entity<DxfRotatedDimension>(document, handle).DimAssoc);
            Assert.Empty(Entity<DxfLine>(document, line).GetPersistentReactorIds());
            document.Selection.Set(new[] { line });
            Run(session, "MOVE", P(0, 0), P(0, 30));
            Near(new GeoPoint3(0, 0, 0), Entity<DxfRotatedDimension>(document, handle).XLine1Point);
        }

        /// <summary>The box of the entities of a dimension's picture.</summary>
        private static GeoAabb3 PictureBox(DxfDimension dimension)
            => dimension.DimBlock.Where(e => !e.IsErased).Aggregate(GeoAabb3.Empty, (box, e) => box.Union(e.GeometricExtents));

        [Fact]
        public void ADimensionMadeHere_MovedOnItsOwn_TakesItsPictureWithIt_AsItsBlockIsItsOwn()
        {
            // AutoCAD 2021 writes group 70 of a DIMLINEAR as 32, its block the dimension's own, and moves the picture with it.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DIMLINEAR", P(0, 0), P(100, 0), P(50, 20));
            var dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            GeoAabb3 before = PictureBox(dimension);
            document.Selection.Set(new[] { dimension.Handle });

            Run(session, "MOVE", P(0, 0), P(10, 5));

            GeoAabb3 after = PictureBox(Entity<DxfRotatedDimension>(document, dimension.Handle));
            Near(before.Min + new GeoVector3(10, 5, 0), after.Min);
            Near(before.Max + new GeoVector3(10, 5, 0), after.Max);

            // Written and read back, the block is still its own.
            CadDocument reopened = CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "moved.dxf");
            CadCommandSession again = Session(reopened);
            reopened.Selection.Set(new[] { dimension.Handle });
            Run(again, "MOVE", P(0, 0), P(10, 5));
            Near(before.Min + new GeoVector3(20, 10, 0), PictureBox(Entity<DxfRotatedDimension>(reopened, dimension.Handle)).Min);
        }

        [Fact]
        public void ACopyOfADimension_IsDrawnWhereTheCopyStands_ByAPictureOfItsOwn_AsAutoCadCopiesItsBlock()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "DIMLINEAR", P(0, 0), P(100, 0), P(50, 20));
            string original = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace)).Handle;
            GeoAabb3 before = PictureBox(Entity<DxfRotatedDimension>(document, original));
            document.Selection.Set(new[] { original });

            Run(session, "COPY", P(0, 0), P(0, 60), Enter);

            DxfRotatedDimension copy = All<DxfRotatedDimension>(document.ModelSpace).Single(d => d.Handle != original);
            Assert.NotEqual(Entity<DxfRotatedDimension>(document, original).DimBlock.Name, copy.DimBlock.Name);
            Near(before.Min + new GeoVector3(0, 60, 0), PictureBox(copy).Min);
            Near(before.Min, PictureBox(Entity<DxfRotatedDimension>(document, original)).Min);

            document.Selection.Set(new[] { copy.Handle });
            Run(session, "MOVE", P(0, 0), P(30, 0));
            Near(before.Min + new GeoVector3(30, 60, 0), PictureBox(Entity<DxfRotatedDimension>(document, copy.Handle)).Min);
            Near(before.Min, PictureBox(Entity<DxfRotatedDimension>(document, original)).Min);
            AssertReopens(document);
        }

        [Fact]
        public void ALineAndItsDimensionCopiedTogether_TheCopyIsTiedToTheCopiedLine_AsAutoCadTiesIt()
        {
            // AutoCAD 2021: COPY of a line and its dimension tied to its start and its middle wrote the copy's DIMASSOC with
            // StartPoint and Midpoint of the new line.
            CadDocument document = LineWithDimension(out CadCommandSession session, out string line, out string handle);
            document.Selection.Set(new[] { line, handle });

            Run(session, "COPY", P(0, 0), P(0, 60), Enter);

            string copiedLine = All<DxfLine>(document.ModelSpace).Single(l => l.Handle != line).Handle;
            DxfRotatedDimension copy = All<DxfRotatedDimension>(document.ModelSpace).Single(d => d.Handle != handle);
            Assert.NotNull(copy.DimAssoc);
            Assert.All(copy.DimAssoc.PointReferences, r => Assert.Equal(copiedLine, r.ObjectHandle));
            Assert.Equal(new[] { DxfOsnapType.StartPoint, DxfOsnapType.Endpoint }, copy.DimAssoc.PointReferences.Select(r => r.OsnapType));
            Assert.All(Entity<DxfRotatedDimension>(document, handle).DimAssoc.PointReferences, r => Assert.Equal(line, r.ObjectHandle));
            AssertReopens(document);

            document.Selection.Set(new[] { copiedLine });
            Run(session, "MOVE", P(0, 0), P(10, 0));

            Near(new GeoPoint3(10, 60, 0), Entity<DxfRotatedDimension>(document, copy.Handle).XLine1Point);
            Near(new GeoPoint3(0, 0, 0), Entity<DxfRotatedDimension>(document, handle).XLine1Point);
        }

        [Theory]
        [InlineData(0.0, 20.0, 20.0, 30.0, 115.149, 49.4029)]
        [InlineData(0.0, -20.0, 20.0, 30.0, 124.851, 10.597)]
        [InlineData(0.0, 20.0, 0.0, 50.0, 91.0557, 67.889)]
        [InlineData(0.0, 30.0, 0.0, -100.0, 121.213, -78.787)]
        public void AnAlignedDimensionWithOneEndStretched_KeepsItsLineAsFarFromTheLineMeasured_AsAutoCadKeepsIt(double x, double offset, double dx, double dy, double lineX, double lineY)
        {
            // The numbers are AutoCAD 2021's: an aligned dimension of a line from (0, 0) to (100, 0), its line 20 above, the
            // end stretched to (120, 30), has its dimension line point at (115.149, 49.4029): 20 from the line, square to it at
            // the second point.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 0), P(100, 0), Enter, "DIMALIGNED");
            Snap(session, P(0.4, 0.3));
            Snap(session, P(99.6, -0.3));
            Run(session, P(50 + x, offset));
            var dimension = Assert.Single(All<DxfAlignedDimension>(document.ModelSpace));
            Assert.NotNull(dimension.DimAssoc);

            string line = Assert.Single(All<DxfLine>(document.ModelSpace)).Handle;

            Assert.True(document.GripDrag(line, 1, P(100 + dx, dy)).Succeeded);

            dimension = Entity<DxfAlignedDimension>(document, dimension.Handle);
            Near(new GeoPoint3(100 + dx, dy, 0), dimension.XLine2Point);
            Near(new GeoPoint3(lineX, lineY, 0), dimension.DimLinePoint, 1E-3);
        }

        [Fact]
        public void DimAssoc_IsASystemVariableOfTheDrawing_TwoByDefault()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "SETVAR", "DIMASSOC");
            Assert.Equal("Enter new value for DIMASSOC <2>:", session.PromptText);
            Run(session, "3");
            Assert.Contains("Requires an integer between 0 and 2.", session.History);
            Run(session, "0");
            Assert.Equal(0, document.Database.DimAssoc);
        }
    }
}
