using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The parametric commands of AutoCAD 2021's Parametric tab (GCHORIZONTAL and the other geometric constraints, DCLINEAR and
    /// the other dimensional ones, -PARAMETERS, AUTOCONSTRAIN, DELCONSTRAINT, CONSTRAINTBAR), what they write into the
    /// drawing in the form AutoCAD writes it, and how an edit of a constrained object carries the others with it.
    /// </summary>
    public class ConstraintCommandTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static int Occurrences(string text, string word)
        {
            int count = 0;
            int at = 0;

            while ((at = text.IndexOf("\n" + word + "\n", at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += word.Length;
            }

            return count;
        }

        private static CadConstraintSet SetOf(CadDocument document) =>
            CadConstraints.Open(document, document.ModelSpace).Set;

        private static DxfLine LineAt(CadDocument document, int index) => All<DxfLine>(document.ModelSpace)[index];

        private static CadDocument Reopened(CadDocument document) =>
            CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "again.dxf");

        /// <summary>Two lines, the first from (0, 0) to (10, 1), the second from (0, 5) to (8, 7).</summary>
        private static CadDocument TwoLines()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 1), Enter, "LINE", P(0, 5), P(8, 7), Enter);
            return document;
        }

        [Fact]
        public void GcHorizontalOnALine_LevelsItAndKeepsItsStart()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Start(document, "GCHORIZONTAL");

            Assert.Equal("Select an object or [2Points] <2Points>:", session.PromptText);
            Run(session, P(5, 0.5));
            Assert.False(session.IsRunning);

            DxfLine line = LineAt(document, 0);
            Near(new GeoPoint3(0, 0, 0), line.StartPoint, 1E-6);
            Assert.Equal(0.0, line.EndPoint.Y, 6);
            CadConstraintSet set = SetOf(document);
            Assert.Single(set.Geometric);
            Assert.Equal(CadGeoKind.Horizontal, set.Geometric[0].Kind);
            Assert.Null(set.Refusal);
        }

        [Fact]
        public void TheConstraintsOfADrawing_AreKeptInAnAssociativeNetworkAsAutoCadKeepsThem()
        {
            CadDocument document = TwoLines();
            Start(document, "GCHORIZONTAL", P(5, 0.5));
            string text = document.Database.ToText();

            Assert.Contains("ACDBASSOC2DCONSTRAINTGROUP", text, StringComparison.Ordinal);
            Assert.Contains("ACDBASSOCGEOMDEPENDENCY", text, StringComparison.Ordinal);
            Assert.Contains("AcConstrainedBoundedLine", text, StringComparison.Ordinal);
            Assert.Contains("AcHorizontalConstraint", text, StringComparison.Ordinal);
            Assert.Contains("AcConstrainedDatumLine", text, StringComparison.Ordinal);
            Assert.Contains("AcDbAssocSingleEdgePersSubentId", text, StringComparison.Ordinal);

            DxfLine line = LineAt(document, 0);
            Assert.Single(line.GetPersistentReactorIds());
            AssertReopens(document);

            CadConstraintSet again = SetOf(Reopened(document));
            Assert.Single(again.Geometric);
            Assert.Equal(CadGeoKind.Horizontal, again.Geometric[0].Kind);
            Assert.Null(again.Refusal);
        }

        [Fact]
        public void AGroupHoldsTheNodesOfEachObject_ItsEndsAndTheirConstraints()
        {
            CadDocument document = TwoLines();
            Start(document, "GCPARALLEL", P(5, 0.5), P(4, 6));
            string text = document.Database.ToText();

            // The class of each node is listed once in the group's list of nodes: two lines, four ends, four holds, one parallel.
            Assert.Equal(2, Occurrences(text, "AcConstrainedBoundedLine"));
            Assert.Equal(4, Occurrences(text, "AcConstrainedImplicitPoint"));
            Assert.Equal(4, Occurrences(text, "AcPointCurveConstraint"));
            Assert.Equal(1, Occurrences(text, "AcParallelConstraint"));
        }

        [Fact]
        public void GcParallel_TurnsTheSecondLineToTheFirst()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Start(document, "GCPARALLEL");

            Assert.Equal("Select first object:", session.PromptText);
            Run(session, P(5, 0.5));
            Assert.Equal("Select second object:", session.PromptText);
            Run(session, P(4, 6));

            DxfLine a = LineAt(document, 0);
            DxfLine b = LineAt(document, 1);
            Near(new GeoPoint3(10, 1, 0), a.EndPoint, 1E-6);
            GeoVector3 u = a.StartPoint.GetVectorTo(a.EndPoint);
            GeoVector3 v = b.StartPoint.GetVectorTo(b.EndPoint);
            Assert.Equal(0.0, u.X * v.Y - u.Y * v.X, 5);
        }

        [Fact]
        public void GcCoincident_JoinsTwoEndsAndTheSecondMovesToTheFirst()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 0), Enter, "LINE", P(12, 3), P(20, 3), Enter);
            CadCommandSession session = Start(document, "GCCOINCIDENT", P(10, 0));

            Assert.Equal("Select second point or [Object] <Object>:", session.PromptText);
            Run(session, P(12, 3));

            Near(new GeoPoint3(10, 0, 0), LineAt(document, 1).StartPoint, 1E-6);
            Near(new GeoPoint3(10, 0, 0), LineAt(document, 0).EndPoint, 1E-6);
        }

        [Fact]
        public void AnObjectThatCannotBeConstrained_IsRefusedAndAskedAgain()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 1), Enter, "TEXT", P(0, 20), "2.5", "0", "Hi", Enter);
            CadCommandSession session = Start(document, "GCHORIZONTAL");
            Run(session, P(1, 21));

            Assert.True(session.IsRunning);
            Assert.Contains(session.History, h => h.Contains("Invalid selection", StringComparison.Ordinal));
        }

        [Fact]
        public void AConstraintThatConflictsWithOthers_ChangesNothing_AndSaysSo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LINE", P(0, 0), P(10, 1), Enter, "GCHORIZONTAL", P(5, 0.5));
            string before = document.Database.ToText();
            Run(session, "GCVERTICAL", P(5, 0));

            Assert.Equal(before, document.Database.ToText());
            Assert.Contains(session.History, h => h.Contains("cannot be satisfied", StringComparison.Ordinal));
        }

        [Fact]
        public void ARepeatedConstraint_IsRefusedAsOverConstraining()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LINE", P(0, 0), P(10, 1), Enter, "GCHORIZONTAL", P(5, 0.5));
            Run(session, "GCHORIZONTAL", P(5, 0));

            Assert.Single(SetOf(document).Geometric);
            Assert.Contains(session.History, h => h.Contains("over-constrain", StringComparison.Ordinal));
        }

        [Fact]
        public void TheConstraintsOfACommand_AreOneUndoStep()
        {
            CadDocument document = TwoLines();
            string before = document.Database.ToText();
            Start(document, "GCPARALLEL", P(5, 0.5), P(4, 6));
            Assert.NotEqual(before, document.Database.ToText());

            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
            Assert.True(SetOf(document).IsEmpty);
            Assert.True(document.Redo());
            Assert.Single(SetOf(document).Geometric);
        }

        [Fact]
        public void GcTangent_TouchesACircleToALine()
        {
            CadDocument document = Blank();
            Start(document, "CIRCLE", P(0, 0), "5", "LINE", P(-10, 8), P(10, 8), Enter);
            Start(document, "GCTANGENT", P(0, 5), P(0, 8));

            DxfCircle circle = All<DxfCircle>(document.ModelSpace).Single();
            DxfLine line = LineAt(document, 0);
            double d =
                Math.Abs(
                    (line.EndPoint.X - line.StartPoint.X) * (circle.Center.Y - line.StartPoint.Y)
                        - (line.EndPoint.Y - line.StartPoint.Y) * (circle.Center.X - line.StartPoint.X)
                ) / line.Length;
            Assert.Equal(circle.Radius, d, 5);
            Near(new GeoPoint3(0, 0, 0), circle.Center, 1E-6);
        }

        [Fact]
        public void GcFixOnALine_HoldsBothEndsWhereTheyAre()
        {
            CadDocument document = Blank();
            Start(document, "LINE", P(0, 0), P(10, 1), Enter, "GCFIX", "Object", P(5, 0.5));

            Assert.Equal(2, SetOf(document).Geometric.Count(c => c.Kind == CadGeoKind.Fix));
        }

        [Fact]
        public void GcConcentricAndGcEqual_ShapeTwoCircles()
        {
            CadDocument document = Blank();
            Start(document, "CIRCLE", P(0, 0), "5", "CIRCLE", P(30, 4), "8");
            Start(document, "GCCONCENTRIC", P(5, 0), P(38, 4));
            Start(document, "GCEQUAL", P(5, 0), P(8, 0));

            List<DxfCircle> circles = All<DxfCircle>(document.ModelSpace);
            Near(circles[0].Center, circles[1].Center, 1E-6);
            Assert.Equal(circles[0].Radius, circles[1].Radius, 6);
        }

        [Fact]
        public void GeomConstraint_AsksWhichConstraintAndRunsIt()
        {
            CadDocument document = TwoLines();
            CadCommandSession session = Start(document, "GEOMCONSTRAINT");

            Assert.Equal(
                "Enter constraint type [Horizontal/Vertical/Perpendicular/PArallel/Tangent/SMooth/Coincident/CONcentric/COLlinear/Symmetric/Equal/Fix] <Coincident>:",
                session.PromptText
            );
            Run(session, "Horizontal", P(5, 0.5));

            Assert.Equal(CadGeoKind.Horizontal, SetOf(document).Geometric[0].Kind);
        }

        [Fact]
        public void AConstraintOnAPolylineSegment_IsKeptWithTheIdsOfItsVertices()
        {
            CadDocument document = Blank();
            Start(document, "PLINE", P(0, 0), P(10, 1), P(12, 8), Enter, "GCHORIZONTAL", P(5, 0.5));
            DxfPolyline polyline = All<DxfPolyline>(document.ModelSpace).Single();

            Assert.Equal(0.0, polyline.GetPoint2dAt(1).Y - polyline.GetPoint2dAt(0).Y, 6);
            CadConstraintSet again = SetOf(Reopened(document));
            Assert.Single(again.Geometric);
            Assert.Equal(0, again.Geometric[0].Refs[0].Segment);
            Assert.Contains("AcDbAssocEdgePersSubentId", document.Database.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheConstraintsOfADrawing_SurviveADwg_VerticalAndSymmetricToo()
        {
            CadDocument document = Blank();
            Start(
                document,
                "LINE",
                P(0, 0),
                P(10, 1),
                Enter,
                "LINE",
                P(0, 5),
                P(8, 7),
                Enter,
                "LINE",
                P(5, -10),
                P(5, 10),
                Enter,
                "LINE",
                P(30, 0),
                P(40, 3),
                Enter
            );
            Start(document, "GCVERTICAL", P(35, 1.5));
            Start(document, "GCSYMMETRIC", "2Points", P(1, 0.1), P(1, 5.2), P(5, 8));
            Start(document, "DCALIGNED", P(1, 0.1), P(9, 0.9), P(5, -6), Enter);
            string path = Path.Combine(Path.GetTempPath(), "constraints-" + Guid.NewGuid().ToString("N") + ".dwg");

            try
            {
                DwgWriter.Write(document.Database, path);
                CadDocument back = CadDocument.From(DwgReader.Open(path), "back.dwg");

                CadConstraintSet set = SetOf(back);
                Assert.Null(set.Refusal);
                Assert.Equal(
                    SetOf(document).Geometric.Select(c => c.Kind).ToArray(),
                    set.Geometric.Select(c => c.Kind).ToArray()
                );
                Assert.Contains(set.Geometric, c => c.Kind == CadGeoKind.Vertical);
                Assert.Contains(set.Geometric, c => c.Kind == CadGeoKind.Symmetric);
                Assert.Equal("d1", Assert.Single(set.Dimensional).Name);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
