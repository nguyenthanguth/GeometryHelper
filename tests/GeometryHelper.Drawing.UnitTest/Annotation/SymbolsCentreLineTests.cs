using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Annotation.AnnotationKit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    public class SymbolsCentreLineTests
    {
        private static IReadOnlyList<DrawingCentreLine> Find(
            DrawingObject[] objects,
            GeoCoordinateSystem3 frame,
            double scale
        ) => DrawingCentreLine.Find(Project(objects, frame), objects, new SymbolSettings(), scale, Tol);

        private static void IsLine(DrawingCentreLine line, double x0, double y0, double x1, double y1)
        {
            Near(x0, line.Line.StartPoint.X);
            Near(y0, line.Line.StartPoint.Y);
            Near(x1, line.Line.EndPoint.X);
            Near(y1, line.Line.EndPoint.Y);
        }

        [Fact]
        public void TheFourHolesOfTC1ShareTwoLevelLinesAndTwoUprightOnesRunningPastTheirRims()
        {
            IReadOnlyList<DrawingCentreLine> lines = Find(Objects(TestBodies.TC1()), Top, 5);

            // Holes 11 in radius, the lines 3 on paper past them, 15 at 1:5.
            Assert.Equal(new[] { "CL-1", "CL-2", "CL-3", "CL-4" }, lines.Select(l => l.Id));
            IsLine(lines[0], 24, 50, 276, 50);
            IsLine(lines[1], 24, 150, 276, 150);
            IsLine(lines[2], 50, 24, 50, 176);
            IsLine(lines[3], 250, 24, 250, 176);
            Assert.All(lines, l => Assert.Equal(new[] { "TC1-plate" }, l.ObjectIds));
            Assert.All(lines, l => Assert.Equal(new DrawingLinks(new[] { "TC1-plate" }), l.Links));
        }

        [Fact]
        public void AHoleOnItsOwnGetsACrossAndTheExtensionFollowsTheSettings()
        {
            DrawingObject[] objects = { Bar(300, 100, P(80, 40)) };
            IReadOnlyList<DrawingCentreLine> lines = DrawingCentreLine.Find(
                Project(objects, Top),
                objects,
                new SymbolSettings { CentreLineExtension = 2 },
                10,
                Tol
            );

            Assert.Equal(2, lines.Count);
            IsLine(lines[0], 80 - 11 - 20, 40, 80 + 11 + 20, 40);
            IsLine(lines[1], 80, 40 - 11 - 20, 80, 40 + 11 + 20);
        }

        [Fact]
        public void ABoltInAHoleOfThePlateGetsNoLineOfItsOwn()
        {
            IReadOnlyList<DrawingCentreLine> lines = Find(Objects(TestBodies.TC5()), Top, 10);

            // The base plate's four holes of 22 at 150 from the middle: two level lines, two upright, the bolts of 20 in them
            // taking no line; the lines run 30 past the rims of the holes.
            Assert.Equal(4, lines.Count);
            IsLine(lines[0], -191, -150, 191, -150);
            IsLine(lines[3], 150, -191, 150, 191);
            Assert.All(lines, l => Assert.Equal(new[] { "TC5-base" }, l.ObjectIds));
        }

        [Fact]
        public void AViewOfNoCircleHasNoCentreLine()
        {
            Assert.Empty(Find(Objects(TestBodies.TC1()), Front, 5));
        }

        [Fact]
        public void ACentreLineIsWrittenOnTheCentreLayerWithTheCenterLinetypeAndTagged()
        {
            IReadOnlyList<DrawingCentreLine> lines = Find(Objects(TestBodies.TC1()), Top, 5);
            var database = new DxfDatabase("AC1027");
            var placement = new ViewPlacement2("V-1", new GeoPoint2(10, 20), 5);

            lines[0].WriteTo(database, placement);

            DxfLine line = Assert.IsType<DxfLine>(Assert.Single(database.ModelSpace));
            Assert.Equal(ProjectedView2.CentreLayer, line.Layer);
            Assert.Equal("CENTER", ((DxfLayerTableRecord)database.LayerTable[ProjectedView2.CentreLayer]).LinetypeName);
            Near(10 + 24 / 5.0, line.StartPoint.X);
            Near(20 + 50 / 5.0, line.StartPoint.Y);
            Near(0.1, line.LinetypeScale);
            Assert.True(DrawingEntityTags.TryRead(line, out string id, out string view));
            Assert.Equal("CL-1", id);
            Assert.Equal("V-1", view);
        }

        [Fact]
        public void TheSameObjectsInAnotherOrderGiveTheSameLines()
        {
            DrawingObject[] objects = Objects(TestBodies.TC5());
            string first = string.Join(
                "\n",
                Find(objects, Top, 10).Select(l => l.Id + " " + l.Line + " " + string.Join(",", l.ObjectIds))
            );
            string again = string.Join(
                "\n",
                Find(objects.Reverse().ToArray(), Top, 10)
                    .Select(l => l.Id + " " + l.Line + " " + string.Join(",", l.ObjectIds))
            );

            Assert.Equal(first, again);
        }

        [Fact]
        public void BadArgumentsAreRefused()
        {
            DrawingObject[] objects = Objects(TestBodies.TC1());
            ProjectedView2 view = Project(objects, Top);

            Assert.Throws<ArgumentNullException>(() =>
                DrawingCentreLine.Find(null, objects, new SymbolSettings(), 1, Tol)
            );
            Assert.Throws<ArgumentNullException>(() => DrawingCentreLine.Find(view, objects, null, 1, Tol));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                DrawingCentreLine.Find(view, objects, new SymbolSettings(), -1, Tol)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() => new SymbolSettings { CentreLineExtension = 0 });
        }
    }
}
