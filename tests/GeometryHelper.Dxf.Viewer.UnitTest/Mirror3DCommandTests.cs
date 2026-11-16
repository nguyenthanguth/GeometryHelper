using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MIRROR3D in the presenter, as AutoCAD 2021's console runs it (probed with a line from (10, 10) to (30, 20), a circle at
    /// (50, 50), a text and a polyline, with the XY, YZ and ZX options, three points, Zaxis, View, Object and Last, written by
    /// entget): the prompts in its words, the plane taken from what is answered, the copy mirrored as <c>TransformBy</c> mirrors it
    /// and the source kept unless Yes is answered. A plan has no Z, so three points lie in the XY plane, which turns an object's
    /// normal over and leaves it where it stands: the circle at (50, 50) comes to (-50, 50) of the normal (0, 0, -1).
    /// </summary>
    public class Mirror3DCommandTests
    {
        private const string FirstPrompt =
            "Specify first point of mirror plane (3 points) or [Object/Last/Zaxis/View/XY/YZ/ZX/3points] <3points>:";

        private static T Copy<T>(CadDocument document, HashSet<string> before)
            where T : DxfEntity => All<T>(document.ModelSpace).Single(e => !before.Contains(e.Handle));

        private static HashSet<string> Handles<T>(CadDocument document)
            where T : DxfEntity => new HashSet<string>(All<T>(document.ModelSpace).Select(e => e.Handle));

        [Fact]
        public void TheYzPlaneThroughAPoint_MirrorsALineAcrossTheUprightPlane_AndKeepsTheSource()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            HashSet<string> before = Handles<DxfLine>(document);

            Run(session, "MIRROR3D", new[] { "A1" }, Enter);
            Assert.Equal(FirstPrompt, session.PromptText);
            Run(session, "YZ");
            Assert.Equal("Specify point on YZ plane <0,0,0>:", session.PromptText);
            Run(session, P(40, 0));
            Assert.Equal("Delete source objects? [Yes/No] <N>:", session.PromptText);
            Run(session, Enter);

            // AutoCAD 2021: a line from (10, 10) to (30, 20) across the YZ plane through (40, 0) is from (70, 10) to (50, 20).
            DxfLine copy = Copy<DxfLine>(document, before);
            Near(new GeoPoint3(80, 0, 0), copy.StartPoint);
            Near(new GeoPoint3(-20, 0, 0), copy.EndPoint);
            Near(new GeoPoint3(0, 0, 0), Entity<DxfLine>(document, "A1").StartPoint);
            Assert.Equal("MIRROR3D", document.NextUndo.Name);
        }

        [Fact]
        public void TheZxPlaneThroughAPoint_MirrorsAcrossALevelLine()
        {
            CadDocument document = Plan();
            HashSet<string> before = Handles<DxfLine>(document);

            Run(Session(document), "MIRROR3D", new[] { "A2" }, Enter, "ZX", P(0, 30), Enter);

            // AutoCAD 2021: y turned about 30, a line from (10, 10) to (30, 20) comes to (10, 50) to (30, 40).
            DxfLine copy = Copy<DxfLine>(document, before);
            Near(new GeoPoint3(0, 60, 0), copy.StartPoint);
            Near(new GeoPoint3(0, 10, 0), copy.EndPoint);
        }

        [Fact]
        public void TheXyPlane_TurnsTheNormalOver_AndLeavesTheLineWhereItStands()
        {
            CadDocument document = Plan();
            HashSet<string> before = Handles<DxfLine>(document);

            Run(Session(document), "MIRROR3D", new[] { "A2" }, Enter, "XY", P(0, 0), Enter);

            // AutoCAD 2021 (a line from (10, 10) to (30, 20)): the same points, (210 0 0 -1), the line seen from below.
            DxfLine copy = Copy<DxfLine>(document, before);
            Near(new GeoPoint3(0, 0, 0), copy.StartPoint);
            Near(new GeoPoint3(0, 50, 0), copy.EndPoint);
            Assert.True(copy.Normal.IsEqualTo(new GeoVector3(0, 0, -1), Tight));
        }

        [Fact]
        public void ThreePointsOfAPlan_LieInTheXyPlane_AndEnterAtTheFirstPromptAsksForThem()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            HashSet<string> before = Handles<DxfLine>(document);

            Run(session, "MIRROR3D", new[] { "A2" }, Enter, P(0, 0));
            Assert.Equal("Specify second point on mirror plane:", session.PromptText);
            Run(session, P(10, 0));
            Assert.Equal("Specify third point on mirror plane:", session.PromptText);
            Run(session, P(0, 10));
            Assert.Equal("Delete source objects? [Yes/No] <N>:", session.PromptText);
            Run(session, Enter);
            Assert.True(Copy<DxfLine>(document, before).Normal.IsEqualTo(new GeoVector3(0, 0, -1), Tight));

            // Enter takes the default, 3points: the first point is asked as the others are.
            before = Handles<DxfLine>(document);
            Run(session, "MIRROR3D", new[] { "A2" }, Enter, Enter);
            Assert.Equal("Specify first point on mirror plane:", session.PromptText);
            Run(session, P(0, 0), P(10, 0), P(0, 10), Enter);
            Assert.True(Copy<DxfLine>(document, before).Normal.IsEqualTo(new GeoVector3(0, 0, -1), Tight));
        }

        [Fact]
        public void ThreeCollinearPoints_AreRefused_AsAreTwoThatCoincide()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "MIRROR3D", new[] { "A3" }, Enter, P(0, 0), P(0, 0));
            Assert.Equal("The 1st and 2nd point coincide.", session.History.Last());
            Assert.Equal("Specify second point on mirror plane:", session.PromptText);

            Run(session, P(10, 0), P(20, 0));
            Assert.Equal("The three points are collinear.", session.History.Last());
            Assert.Equal("Specify third point on mirror plane:", session.PromptText);

            Run(session, P(0, 10));
            Assert.Equal("Delete source objects? [Yes/No] <N>:", session.PromptText);
        }

        [Fact]
        public void Zaxis_TakesAPointOnThePlaneAndOneOnItsNormal_AndRefusesTwoThatCoincide()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            HashSet<string> before = Handles<DxfLine>(document);

            Run(session, "MIRROR3D", new[] { "A1" }, Enter, "Zaxis");
            Assert.Equal("Specify point on mirror plane:", session.PromptText);
            Run(session, P(40, 0));
            Assert.Equal("Specify point on Z-axis (normal) of mirror plane:", session.PromptText);
            Run(session, P(40, 0));
            Assert.Equal("The two points coincide.", session.History.Last());
            Run(session, P(50, 0), Enter);

            // The plane through (40, 0) square to the x axis: the same as the YZ plane there.
            DxfLine copy = Copy<DxfLine>(document, before);
            Near(new GeoPoint3(80, 0, 0), copy.StartPoint);
            Near(new GeoPoint3(-20, 0, 0), copy.EndPoint);
        }

        [Fact]
        public void Object_TakesThePlaneOfACircleToMirrorALineAcross_AndRefusesALine()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            HashSet<string> before = Handles<DxfLine>(document);

            Run(session, "MIRROR3D", new[] { "A2" }, Enter, "Object");
            Assert.Equal("Select a circle, arc, or 2D-polyline segment:", session.PromptText);
            Run(session, (object)new[] { "A1" });
            Assert.Equal("Improper type of object picked.", session.History.Last());
            Assert.Equal("Select a circle, arc, or 2D-polyline segment:", session.PromptText);
            Run(session, (object)new[] { "A3" });
            Assert.Equal("Delete source objects? [Yes/No] <N>:", session.PromptText);
            Run(session, Enter);

            Assert.True(Copy<DxfLine>(document, before).Normal.IsEqualTo(new GeoVector3(0, 0, -1), Tight));
        }

        [Fact]
        public void View_AsksForAPointOnThePlane_ZeroByDefault()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            HashSet<string> before = Handles<DxfLine>(document);

            Run(session, "MIRROR3D", new[] { "A2" }, Enter, "View");
            Assert.Equal("Specify point on view plane <0,0,0>:", session.PromptText);
            Run(session, Enter, Enter);

            Assert.True(Copy<DxfLine>(document, before).Normal.IsEqualTo(new GeoVector3(0, 0, -1), Tight));
        }

        [Fact]
        public void Last_RepeatsTheLastPlane_AndWhereThereIsNoneTheXyPlaneThroughTheOrigin()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            HashSet<string> before = Handles<DxfLine>(document);

            Run(session, "MIRROR3D", new[] { "A2" }, Enter, "Last", Enter);

            // None used yet: the XY plane, which leaves the line where it is and turns its normal over.
            DxfLine first = Copy<DxfLine>(document, before);
            Near(new GeoPoint3(0, 0, 0), first.StartPoint);
            Assert.True(first.Normal.IsEqualTo(new GeoVector3(0, 0, -1), Tight));

            Run(session, "MIRROR3D", new[] { "A2" }, Enter, "YZ", P(40, 0), Enter);
            before = Handles<DxfLine>(document);
            Run(session, "MIRROR3D", new[] { "A2" }, Enter, "Last", Enter);

            DxfLine again = Copy<DxfLine>(document, before);
            Near(new GeoPoint3(80, 0, 0), again.StartPoint);
        }

        [Fact]
        public void Yes_ReplacesTheSourceByItsMirrorImage()
        {
            CadDocument document = Plan();

            Run(Session(document), "MIRROR3D", new[] { "A2" }, Enter, "YZ", P(40, 0), "Yes");

            Near(new GeoPoint3(80, 0, 0), Entity<DxfLine>(document, "A2").StartPoint);
            Assert.Single(All<DxfLine>(document.ModelSpace), l => !l.IsErased && l.Layer == "0");
        }

        /// <summary>An annotative-less hatch of a user pattern at 30 degrees, lines 2 apart, over (42, 50) to (58, 58).</summary>
        private static DxfHatch UserHatch(CadDocument document)
        {
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = 30.0 * Math.PI / 180.0;
            hatch.PatternSpace = 2.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            document.Database.ModelSpace.AppendEntity(hatch);
            return hatch;
        }

        [Fact]
        public void AHatch_FollowsMirrhatch_AsMirrorDoes()
        {
            CadDocument document = Blank();
            DxfHatch hatch = UserHatch(document);
            CadCommandSession session = Session(document);
            HashSet<string> before = Handles<DxfHatch>(document);

            // AutoCAD 2021 (probed with MIRROR3D across the YZ plane through (0, 0), hatches of a user pattern and ANSI31): MIRRHATCH 0,
            // its default, leaves the line of the pattern at 30 degrees; 1 turns it to 150.
            Run(session, "MIRROR3D", new[] { hatch.Handle }, Enter, "YZ", P(0, 0), Enter);
            DxfHatch kept = Copy<DxfHatch>(document, before);
            Assert.Equal(30.0, kept.GetPatternDefinitionAt(0).Angle * 180.0 / Math.PI, 9);

            Run(session, "MIRRHATCH", "1");
            before = Handles<DxfHatch>(document);
            Run(session, "MIRROR3D", new[] { hatch.Handle }, Enter, "YZ", P(0, 0), Enter);
            DxfHatch turned = Copy<DxfHatch>(document, before);
            Assert.Equal(150.0, turned.GetPatternDefinitionAt(0).Angle * 180.0 / Math.PI, 9);
        }
    }
}
