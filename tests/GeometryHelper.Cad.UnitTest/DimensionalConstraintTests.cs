using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// Dimensional constraints (DCLINEAR and the others, DCCONVERT), the parameters they make (d1, rad1, ang1) and the user
    /// parameters of -PARAMETERS, and how an edit of constrained objects carries the others with it or is taken back.
    /// </summary>
    public class DimensionalConstraintTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static CadConstraintSet SetOf(CadDocument document) =>
            CadConstraints.Open(document, document.ModelSpace).Set;

        private static DxfLine LineAt(CadDocument document, int index) => All<DxfLine>(document.ModelSpace)[index];

        /// <summary>A line from (0, 0) to (10, 1) with a horizontal distance of 10 (d1), its line drawn through (5, 4).</summary>
        private static CadDocument LineOfTen(out CadCommandSession session)
        {
            CadDocument document = Blank();
            session = Start(
                document,
                "LINE",
                P(0, 0),
                P(10, 1),
                Enter,
                "DCHORIZONTAL",
                P(0, 0),
                P(10, 1),
                P(5, 4),
                Enter
            );
            return document;
        }

        private static CadEditResult Edit(CadDocument document, string handle, Action<DxfEntity> change) =>
            document.ApplyEdit("GRIP", new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle) }, s =>
                change(document.FindEntity(handle))
            );

        [Fact]
        public void DcHorizontal_MakesAParameterAndADimensionOnAutoCadsHiddenLayer()
        {
            CadDocument document = LineOfTen(out _);

            CadConstraintSet set = SetOf(document);
            CadDimConstraint dim = Assert.Single(set.Dimensional);
            Assert.Equal(CadDimKind.Horizontal, dim.Kind);
            Assert.Equal("d1", dim.Name);
            Assert.True(dim.Dynamic);
            Assert.Equal(10.0, set.Parameter("d1").Value, 9);
            Assert.False(set.Parameter("d1").IsUser);

            DxfDimension shown = Assert.IsAssignableFrom<DxfDimension>(document.FindEntity(dim.DimensionHandle));
            Assert.Equal("*ADSK_CONSTRAINTS", shown.Layer);
            Assert.StartsWith("d1=10", shown.DimensionText, StringComparison.Ordinal);
            string text = document.Database.ToText();
            Assert.Contains("ACDBASSOCVARIABLE", text, StringComparison.Ordinal);
            Assert.Contains("ACDBASSOCVALUEDEPENDENCY", text, StringComparison.Ordinal);
            Assert.Contains("ASSOCDIMDEPENDENCYBODY", text, StringComparison.Ordinal);
            Assert.Contains("AcDistanceConstraint", text, StringComparison.Ordinal);
            AssertReopens(document);
        }

        [Fact]
        public void TheDimensionalConstraintsOfAFile_AreReadBack_WithTheirNamesAndValues()
        {
            CadDocument document = LineOfTen(out _);
            CadDocument again = CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "again.dxf");

            CadConstraintSet set = SetOf(again);
            Assert.Null(set.Refusal);
            CadDimConstraint dim = Assert.Single(set.Dimensional);
            Assert.Equal(CadDimKind.Horizontal, dim.Kind);
            Assert.Equal("d1", dim.Name);
            Assert.Equal(10.0, set.Parameter("d1").Value, 9);
            Assert.NotNull(again.FindEntity(dim.DimensionHandle));
        }

        [Fact]
        public void ANewValueOfAParameter_StretchesTheLineToIt()
        {
            CadDocument document = LineOfTen(out CadCommandSession session);

            CadEditResult result = CadParameters.Edit(document, "d1", "20");

            Assert.True(result.Succeeded, result.Message);
            DxfLine line = LineAt(document, 0);
            Near(new GeoPoint3(0, 0, 0), line.StartPoint, 1E-6);
            Assert.Equal(20.0, line.EndPoint.X, 6);
            Assert.Equal(1.0, line.EndPoint.Y, 5);
            CadDimConstraint dim = SetOf(document).Dimensional[0];
            Assert.StartsWith(
                "d1=20",
                ((DxfDimension)document.FindEntity(dim.DimensionHandle)).DimensionText,
                StringComparison.Ordinal
            );
            Assert.True(document.Undo());
            Assert.Equal(10.0, LineAt(document, 0).EndPoint.X, 6);
        }

        [Fact]
        public void AnExpressionNamingAnotherParameter_FollowsItWhenItChanges()
        {
            CadDocument document = LineOfTen(out _);
            Assert.True(CadParameters.New(document, "up1", "d1*2+3").Succeeded);

            Assert.Equal(23.0, CadParameters.Rows(document).Single(r => r.Name == "up1").Value, 9);
            Assert.True(CadParameters.Edit(document, "d1", "20").Succeeded);

            Assert.Equal(43.0, CadParameters.Rows(document).Single(r => r.Name == "up1").Value, 9);
            Assert.True(CadParameters.Rows(document).Single(r => r.Name == "up1").IsUser);
        }

        [Fact]
        public void ADimensionDrivenByAnotherParameter_IsSolvedWithIt()
        {
            CadDocument document = Blank();
            Start(
                document,
                "LINE",
                P(0, 0),
                P(10, 0),
                Enter,
                "LINE",
                P(0, 20),
                P(5, 20),
                Enter,
                "DCHORIZONTAL",
                P(0, 0),
                P(10, 0),
                P(5, 4),
                Enter,
                "DCHORIZONTAL",
                P(0, 20),
                P(5, 20),
                P(2, 24),
                "d2=d1/2"
            );

            Assert.Equal(5.0, SetOf(document).Parameter("d2").Value, 9);
            Assert.True(CadParameters.Edit(document, "d1", "30").Succeeded);

            DxfLine second = LineAt(document, 1);
            Assert.Equal(15.0, second.EndPoint.X - second.StartPoint.X, 6);
        }

        [Fact]
        public void AnExpressionThatCannotBeWorkedOut_IsRefusedAndNothingChanges()
        {
            CadDocument document = LineOfTen(out _);
            string before = document.Database.ToText();

            CadEditResult result = CadParameters.Edit(document, "d1", "nothing*2");

            Assert.False(result.Succeeded);
            Assert.Contains("nothing", result.Message, StringComparison.Ordinal);
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void TheParametersOfTheCommandLine_ListNewEditRenameAndDeleteAsAutoCadsDo()
        {
            CadDocument document = LineOfTen(out CadCommandSession session);

            Run(session, "-PARAMETERS", "New", "up1", "d1*2+3");
            Run(session, "-PARAMETERS", "?");
            Assert.Contains(session.History, h =>
                h == "Parameter: d1          Expression: 10                    Value: 10.0000"
            );
            Assert.Contains(session.History, h =>
                h == "Parameter: up1         Expression: d1*2+3                Value: 23.0000"
            );

            Run(session, "-PARAMETERS", "Edit", "d1");
            Assert.Contains(session.History, h => h == "Old Expression = 10, Value = 10.0000");
            Run(session, "20");
            Assert.Equal(20.0, LineAt(document, 0).EndPoint.X, 6);

            Run(session, "-PARAMETERS", "Rename", "up1", "twice");
            Assert.Equal("d1*2+3", SetOf(document).Parameter("twice").Expression);

            Run(session, "-PARAMETERS", "Delete", "twice");
            Assert.Null(SetOf(document).Parameter("twice"));
        }

        [Fact]
        public void ADimensionalParameter_CannotBeDeletedWhileItsConstraintStands()
        {
            CadDocument document = LineOfTen(out _);

            CadEditResult result = CadParameters.Delete(document, "d1");

            Assert.False(result.Succeeded);
            Assert.NotNull(SetOf(document).Parameter("d1"));
        }

        [Fact]
        public void ATypedValue_ChangesTheLineAtOnce()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 1), Enter, "DCALIGNED", P(0, 0), P(10, 1), P(5, 8), "d1=25");

            DxfLine line = LineAt(document, 0);
            Assert.Equal(25.0, line.Length, 6);
            Near(new GeoPoint3(0, 0, 0), line.StartPoint, 1E-6);
            Assert.Equal(25.0, SetOf(document).Parameter("d1").Value, 9);
        }

        [Fact]
        public void DcRadiusAndDcDiameter_ResizeCircles_WithTheNamesAutoCadGives()
        {
            CadDocument document = Blank();
            Start(document, "CIRCLE", P(0, 0), "4", "CIRCLE", P(30, 0), "4");
            Start(document, "DCRADIUS", P(4, 0), P(8, 4), "rad1=6");
            Start(document, "DCDIAMETER", P(34, 0), P(38, 4), Enter);

            List<DxfCircle> circles = All<DxfCircle>(document.ModelSpace);
            Assert.Equal(6.0, circles[0].Radius, 6);
            Assert.Equal(4.0, circles[1].Radius, 6);
            CadConstraintSet set = SetOf(document);
            Assert.Equal(new[] { "rad1", "dia1" }, set.Parameters.Select(p => p.Name).ToArray());
            Assert.Equal(8.0, set.Parameter("dia1").Value, 9);
            Assert.Equal(CadDimKind.Radius, set.Dimensional[0].Kind);
            Assert.Equal(CadDimKind.Diameter, set.Dimensional[1].Kind);
            AssertReopens(document);
        }

        [Fact]
        public void DcAngular_MeasuresTheTurnFromTheFirstLineToTheSecond_AndAFiftyTurnsTheSecond()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(60, 0), P(70, 2), Enter, "LINE", P(60, 0), P(66, 8), Enter);
            Start(document, "DCANGULAR", P(65, 1), P(63, 4), P(64, 3), Enter);

            CadConstraintSet set = SetOf(document);
            CadDimConstraint dim = Assert.Single(set.Dimensional);
            Assert.Equal(CadDimKind.Angular, dim.Kind);
            Assert.Equal("ang1", dim.Name);
            Assert.Equal(41.8201698801, set.Parameter("ang1").Value, 6);

            Assert.True(CadParameters.Edit(document, "ang1", "50").Succeeded);

            DxfLine a = LineAt(document, 0);
            DxfLine b = LineAt(document, 1);
            double turn =
                Math.Atan2(b.EndPoint.Y - b.StartPoint.Y, b.EndPoint.X - b.StartPoint.X)
                - Math.Atan2(a.EndPoint.Y - a.StartPoint.Y, a.EndPoint.X - a.StartPoint.X);
            Assert.Equal(50.0, turn * 180.0 / Math.PI, 5);
            Near(new GeoPoint3(70, 2, 0), a.EndPoint, 1E-5);
        }

        [Fact]
        public void TheAnnotationalForm_DrawsTheDimensionOnTheCurrentLayer()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LINE", P(0, 0), P(10, 1), Enter, "CCONSTRAINTFORM", "1");
            Run(session, "DCHORIZONTAL", P(0, 0), P(10, 1), P(5, 4), Enter);

            CadDimConstraint dim = Assert.Single(SetOf(document).Dimensional);
            Assert.False(dim.Dynamic);
            Assert.Equal("0", document.FindEntity(dim.DimensionHandle).Layer);
            Assert.False(document.Database.LayerTable.Has("*ADSK_CONSTRAINTS"));
        }

        [Fact]
        public void ADimensionThatRepeatsTheOthers_IsRefusedAsOverConstraining()
        {
            CadDocument document = LineOfTen(out CadCommandSession session);
            Run(session, "DCHORIZONTAL", P(1, 0.1), P(9, 0.9), P(5, 6), Enter);

            Assert.Single(SetOf(document).Dimensional);
            Assert.Contains(session.History, h => h.Contains("over-constrain", StringComparison.Ordinal));
        }

        [Fact]
        public void DimConstraint_ShowsTheFormAndAsksWhichDimension()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "DIMCONSTRAINT");

            Assert.Contains(session.History, h => h == "Current settings:  Constraint form = Dynamic");
            Assert.Equal(
                "Enter a dimensional constraint option [Linear/Horizontal/Vertical/Aligned/ANgular/Radius/Diameter/Form/Convert] <Aligned>:",
                session.PromptText
            );
        }

        [Fact]
        public void DcForm_SetsTheFormTheNextDimensionsAreMadeIn()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "DCFORM");

            Assert.Equal("Enter constraint form [Annotational/Dynamic] <Dynamic>:", session.PromptText);
            Run(session, "Annotational");
            Assert.Equal(1, session.Constraints.Form);
        }

        #region An edit of constrained objects

        [Fact]
        public void ADraggedEnd_IsPulledBackToTheDimension_AsFarAsItCanMove()
        {
            CadDocument document = LineOfTen(out _);
            string handle = LineAt(document, 0).Handle;

            CadEditResult result = Edit(document, handle, e => ((DxfLine)e).EndPoint = new GeoPoint3(15, 4, 0));

            Assert.True(result.Succeeded, result.Message);
            DxfLine line = LineAt(document, 0);
            Near(new GeoPoint3(0, 0, 0), line.StartPoint, 1E-6);
            Assert.Equal(10.0, line.EndPoint.X, 6);
            Assert.Equal(4.0, line.EndPoint.Y, 6);
        }

        [Fact]
        public void AMovedLine_CarriesTheLineJoinedToIt()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 0), Enter, "LINE", P(12, 3), P(20, 3), Enter);
            Start(document, "GCCOINCIDENT", P(10, 0), P(12, 3));
            string first = LineAt(document, 0).Handle;

            CadEditResult result = Edit(document, first, e =>
            {
                var line = (DxfLine)e;
                line.StartPoint = new GeoPoint3(5, 5, 0);
                line.EndPoint = new GeoPoint3(15, 5, 0);
            });

            Assert.True(result.Succeeded, result.Message);
            Near(new GeoPoint3(15, 5, 0), LineAt(document, 1).StartPoint, 1E-6);
            Assert.Contains(result.Records, r => r.Handle == LineAt(document, 1).Handle);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Near(new GeoPoint3(10, 0, 0), LineAt(document, 1).StartPoint, 1E-6);
        }

        [Fact]
        public void AMovedFixedLine_IsPutBackWhereItIsFixed()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 0), Enter, "GCFIX", "Object", P(5, 0));
            string handle = LineAt(document, 0).Handle;

            CadEditResult result = Edit(document, handle, e =>
            {
                var line = (DxfLine)e;
                line.StartPoint = new GeoPoint3(5, 5, 0);
                line.EndPoint = new GeoPoint3(15, 5, 0);
            });

            Assert.True(result.Succeeded, result.Message);
            Near(new GeoPoint3(0, 0, 0), LineAt(document, 0).StartPoint, 1E-6);
            Near(new GeoPoint3(10, 0, 0), LineAt(document, 0).EndPoint, 1E-6);
        }

        [Fact]
        public void ConstraintsNoPlaceMeets_TakeAnEditBack_WithConstraintsCannotBeSatisfied()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 1), Enter);
            string handle = LineAt(document, 0).Handle;

            // Two constraints that contradict each other, as a file from elsewhere may hold them.
            CadConstraints.Work work = CadConstraints.Open(document, document.ModelSpace);
            work.Take(handle);
            work.Set.Geometric.Add(
                new CadGeoConstraint(CadGeoKind.Horizontal, new[] { new CadRef(handle, -1, CadPart.Whole) })
            );
            work.Set.Geometric.Add(
                new CadGeoConstraint(CadGeoKind.Vertical, new[] { new CadRef(handle, -1, CadPart.Whole) })
            );
            document.ApplyEdit(
                "CONSTRAINT",
                new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, CadConstraints.OwnProperty) },
                s => CadConstraints.Write(work)
            );
            string before = document.Database.ToText();

            CadEditResult result = Edit(document, handle, e => ((DxfLine)e).EndPoint = new GeoPoint3(12, 1, 0));

            Assert.False(result.Succeeded);
            Assert.Equal("Constraints cannot be satisfied.", result.Message);
            Assert.Equal(10.0, LineAt(document, 0).EndPoint.X, 9);
            Assert.Equal(before, document.Database.ToText());
        }

        [Fact]
        public void AnErasedObject_TakesItsConstraintsAndTheirDimensionsWithIt()
        {
            CadDocument document = LineOfTen(out _);
            string handle = LineAt(document, 0).Handle;
            string dimension = SetOf(document).Dimensional[0].DimensionHandle;

            CadEditResult result = document.ApplyEdit(
                "ERASE",
                new[] { new CadEditRecord(CadEditRecordKind.Erased, handle) },
                s => document.FindEntity(handle).Erase()
            );

            Assert.True(result.Succeeded, result.Message);
            Assert.True(SetOf(document).IsEmpty);
            Assert.Null(document.FindEntity(dimension));
            Assert.DoesNotContain("AcDistanceConstraint", document.Database.ToText(), StringComparison.Ordinal);
        }

        #endregion

        #region AUTOCONSTRAIN, DELCONSTRAINT, bars

        [Fact]
        public void AutoConstrain_AppliesWhatTheObjectsNearlyMeet()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 0.05), Enter, "LINE", P(10.005, 0), P(10, 10), Enter);
            string a = LineAt(document, 0).Handle;
            string b = LineAt(document, 1).Handle;

            CadCommandSession session = Start(document, "AUTOCONSTRAIN");
            Assert.Equal("Select objects or [Settings]:", session.PromptText);
            Run(session, new[] { a, b }, Enter);

            CadConstraintSet set = SetOf(document);
            Assert.Contains(set.Geometric, c => c.Kind == CadGeoKind.Horizontal);
            Assert.Contains(set.Geometric, c => c.Kind == CadGeoKind.Vertical);
            Assert.Contains(set.Geometric, c => c.Kind == CadGeoKind.Coincident);
            Assert.Equal(0.0, LineAt(document, 0).EndPoint.Y - LineAt(document, 0).StartPoint.Y, 6);
            Near(LineAt(document, 0).EndPoint, LineAt(document, 1).StartPoint, 1E-6);
        }

        [Fact]
        public void AutoConstrain_LeavesOutWhatTheToleranceDoesNotReach()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 3), Enter);
            CadCommandSession session = Start(document, "AUTOCONSTRAIN");
            Run(session, new[] { LineAt(document, 0).Handle }, Enter);

            Assert.True(SetOf(document).IsEmpty);
        }

        [Fact]
        public void DelConstraint_RemovesEveryConstraintOfTheObjectsSelected()
        {
            CadDocument document = LineOfTen(out CadCommandSession session);
            Run(session, "GCHORIZONTAL", P(5, 1));
            string line = LineAt(document, 0).Handle;
            string dimension = SetOf(document).Dimensional[0].DimensionHandle;

            CadCommandSession again = Start(document, "DELCONSTRAINT");
            Assert.Contains(again.History, h => h == "All constraints will be removed from selected objects");
            Run(again, new[] { line }, Enter);

            Assert.True(SetOf(document).IsEmpty);
            Assert.Null(document.FindEntity(dimension));
            Assert.Contains(again.History, h => h.EndsWith("constraint(s) removed", StringComparison.Ordinal));
            Assert.Empty(LineAt(document, 0).GetPersistentReactorIds());
        }

        [Fact]
        public void ConstraintBars_ShowTheIconsOfSelectedObjects_UntilHiddenByConstraintBar()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 1), Enter, "GCHORIZONTAL", P(5, 0.5));
            string handle = LineAt(document, 0).Handle;
            CadConstraintSettings settings = new CadConstraintSettings();

            document.ConstraintBars.EndCommand();
            Assert.Empty(document.ConstraintBars.BarsFor(document.ModelSpace, new string[0], settings));
            CadConstraintBar bar = Assert.Single(
                document.ConstraintBars.BarsFor(document.ModelSpace, new[] { handle }, settings)
            );
            Assert.Equal(CadConstraintType.Horizontal, Assert.Single(bar.Icons).Type);
            Assert.Equal(5.0, bar.X, 6);

            CadCommandSession session = Start(document, "CONSTRAINTBAR");
            Run(session, new[] { handle }, Enter);
            Assert.Equal("Enter an option [Show/Hide/Reset] <Show>:", session.PromptText);
            Run(session, "Hide");
            Assert.Empty(document.ConstraintBars.BarsFor(document.ModelSpace, new[] { handle }, settings));
            Assert.True(document.ConstraintBars.IsHidden(handle));

            Run(session, "CONSTRAINTBAR", new[] { handle }, Enter, "Show");
            Assert.Single(document.ConstraintBars.BarsFor(document.ModelSpace, new string[0], settings));
        }

        [Fact]
        public void AnIconOfABar_CanBeDeleted_AsOneUndoStep()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 1), Enter, "GCHORIZONTAL", P(5, 0.5));
            string handle = LineAt(document, 0).Handle;
            CadConstraintBar bar = document
                .ConstraintBars.BarsFor(document.ModelSpace, new[] { handle }, new CadConstraintSettings())
                .Single();

            CadEditResult result = document.ConstraintBars.Delete(document.ModelSpace, bar.Icons[0].Index);

            Assert.True(result.Succeeded, result.Message);
            Assert.True(SetOf(document).IsEmpty);
            Assert.True(document.Undo());
            Assert.Single(SetOf(document).Geometric);
        }

        [Fact]
        public void TheConstraintVariables_StartAsAutoCadStartsThem()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "CCONSTRAINTFORM");
            Assert.Equal("Enter new value for CCONSTRAINTFORM <0>:", session.PromptText);
            Run(session, Esc);

            foreach (
                KeyValuePair<string, string> pair in new Dictionary<string, string>
                {
                    ["CONSTRAINTBARDISPLAY"] = "3",
                    ["CONSTRAINTNAMEFORMAT"] = "2",
                    ["CONSTRAINTBARMODE"] = "4095",
                    ["CONSTRAINTSOLVEMODE"] = "1",
                    ["PARAMETERCOPYMODE"] = "1",
                    ["CONSTRAINTINFER"] = "0",
                    ["DYNCONSTRAINTMODE"] = "1",
                }
            )
            {
                session = Start(document, pair.Key);
                Assert.Equal("Enter new value for " + pair.Key + " <" + pair.Value + ">:", session.PromptText);
                Run(session, Esc);
            }

            session = Start(document, "PARAMETERSSTATUS");
            Assert.Contains(session.History, h => h == "PARAMETERSSTATUS = 0 (read only)");
        }

        #endregion
    }
}
