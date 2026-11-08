using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Annotation.AnnotationKit;
using static GeometryHelper.Drawing.UnitTest.Annotation.MarksKit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    public class MarksPictureTests
    {
        private readonly ITestOutputHelper _output;

        public MarksPictureTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TheMarksOfTheTestBodiesAreDrawnWithTheirDimensionsAndCentreLinesToPicturesToLookAt()
        {
            var cases = new List<(string Name, DrawingObject[] Objects, GeoCoordinateSystem3 Frame, double Scale)>
            {
                ("tc1-top-1to5", Objects(TestBodies.TC1()), Top, 5),
                ("tc1-front-1to5", Objects(TestBodies.TC1()), Front, 5),
                ("tc5-top-1to10", Objects(TestBodies.TC5()), Top, 10),
                ("tc5-front-1to10", Objects(TestBodies.TC5()), Front, 10),
                ("tc5-right-1to10", Objects(TestBodies.TC5()), Right, 10),
            };

            foreach ((TestBody body, double scale) in new[] { (TestBodies.TC2(), 20.0), (TestBodies.TC6(), 20.0) })
            {
                GeoCoordinateSystem3 frame = body.ToSource().Frame;

                foreach (ViewKind kind in new[] { ViewKind.Front, ViewKind.Top, ViewKind.Right })
                {
                    cases.Add((body.Name.ToLowerInvariant() + "-" + kind.ToString().ToLowerInvariant() + "-1to" + scale, Objects(body), StandardViews.GetFrame(frame, kind).CoordinateSystem, scale));
                }
            }

            foreach ((string name, DrawingObject[] objects, GeoCoordinateSystem3 frame, double scale) in cases)
            {
                Marked marked = Mark(objects, frame, scale);
                IReadOnlyList<DrawingCentreLine> centres = DrawingCentreLine.Find(marked.View, objects, new SymbolSettings(), scale, Tol);
                var database = new DxfDatabase("AC1027");
                var placement = new ViewPlacement2("V-1", new GeoPoint2(0, 0), scale);
                marked.View.WriteTo(database);
                Scale(database, scale);

                foreach (DrawingDimension dimension in marked.Dimensions)
                {
                    dimension.WriteTo(database, placement);
                }

                foreach (DrawingCentreLine line in centres)
                {
                    line.WriteTo(database, placement);
                }

                foreach (DrawingMark mark in marked.Marks)
                {
                    mark.WriteTo(database, placement);
                }

                string path = SavePicture(database, name);
                _output.WriteLine(path + "  " + string.Join(", ", marked.Marks.Select(m => m.Text)) + "; " + centres.Count + " centre lines");
                Assert.True(new FileInfo(path).Length > 0);
            }
        }

        [Fact]
        public void TheWeldSymbolsAndTheSectionDetailAndLevelMarksAreDrawnToASheetToLookAt()
        {
            // A row of welds for each kind, one for each side, on a plate seen from the front at 1:10.
            var objects = new List<DrawingObject> { Part("P-1", Box(0, 0, -400, 1600, 20, 0), true) };
            string[] types = { "Fillet", "SquareButt", "VButt", "BevelButt" };
            string[] sides = { "Arrow", "Other", "Both" };

            for (int t = 0; t < types.Length; t++)
            {
                for (int s = 0; s < sides.Length; s++)
                {
                    objects.Add(Weld("W-" + t + s, new GeoPoint3(100 + s * 500, 0, -100 * t - 50), types[t], sides[s], t % 2 == 0 ? "a5" : "s8", s == 1 ? null : "200", "P-1"));
                }
            }

            ProjectedView2 view = Project(objects.ToArray(), Front);
            IReadOnlyList<WeldSymbol> welds = WeldSymbol.Find(view, objects, Front, new SymbolSettings(), 10);
            var database = new DxfDatabase("AC1027");
            var placement = new ViewPlacement2("V-1", new GeoPoint2(0, 0), 10);
            view.WriteTo(database);
            Scale(database, 10);

            foreach (WeldSymbol weld in welds)
            {
                weld.WriteTo(database, placement);
            }

            new SectionMark("S-A", "A", new GeoLine2(1700, -450, 1700, 50), new GeoVector2(-1, 0), 25).WriteTo(database, placement);
            DetailMark.Circle("D-B", "B", new GeoPoint2(1300, -350), 80, 25).WriteTo(database, placement);
            DetailMark.Rectangle("D-C", "C", new GeoRectangle2(new GeoPoint2(1300, -100), 200, 120), 25).WriteTo(database, placement);
            new LevelMark("L-1", new GeoPoint2(1900, 0), 0, 25).WriteTo(database, placement);
            new LevelMark("L-2", new GeoPoint2(1900, -400), -400, 25).WriteTo(database, placement);

            string path = SavePicture(database, "symbols-sheet-1to10");
            _output.WriteLine(path + "  " + welds.Count + " weld symbols");
            Assert.Equal(12, welds.Count);

            // The welds of the first and the third kind alone, close up.
            var closeUp = new DxfDatabase("AC1027");

            foreach (WeldSymbol weld in welds.Where(w => w.Type == WeldType.Fillet || w.Type == WeldType.VButt))
            {
                weld.WriteTo(closeUp, placement);
            }

            _output.WriteLine(SavePicture(closeUp, "symbols-welds-close-up"));
        }

        /// <summary>The view's own lines taken down to the scale, so that they sit under the annotation written at it.</summary>
        private static void Scale(DxfDatabase database, double scale)
        {
            GeoTransform3 shrink = GeoTransform3.Scaling(1.0 / scale);

            foreach (DxfEntity entity in database.ModelSpace.ToList())
            {
                entity.TransformBy(shrink);
            }
        }
    }
}
