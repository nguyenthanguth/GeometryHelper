using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Annotation.AnnotationKit;
using static GeometryHelper.Drawing.UnitTest.Annotation.MarksKit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Annotation
{
    public class MarksPlacementTests
    {
        private readonly ITestOutputHelper _output;

        public MarksPlacementTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ThePlateOfTC1SeenFromAboveGetsOnePartMarkClearOfItsDimensions()
        {
            Marked marked = Mark(Objects(TestBodies.TC1()), Top, 5);

            DrawingMark mark = Assert.Single(marked.Marks);
            Assert.Equal("M-TC1-plate", mark.Id);
            Assert.Equal(MarkKind.Part, mark.Kind);
            Assert.Equal("P1", mark.Text);
            Assert.Equal(new[] { "TC1-plate" }, mark.ObjectIds);
            Near(17.5, mark.TextHeight);
            Near(17.5, mark.TextBox.Height);
            Near(2 * 17.5, mark.TextBox.Width);
            Assert.NotEmpty(marked.Dimensions);
            Assert.Empty(Marker.FindOverlaps(marked.Marks, marked.Dimensions, Tol));
            Assert.Empty(LeadersOffTheirObjects(marked));

            // The text stands below the plate, away from it and its dimensions, 4 off on paper, 20 at 1:5: the leader runs
            // 20 from the top of its box to the plate's lower side.
            Assert.Equal(2, mark.Leader.Count);
            Near(-20 - 17.5 / 2, mark.TextBox.Center.Y);
            Near(-20, mark.Leader[0].Y);
            Near(0, mark.Anchor.Y);
            Near(20, mark.Leader[0].DistanceTo(mark.Anchor));
            Assert.Equal(
                new DrawingLinks(
                    new[] { "TC1-plate" },
                    new[] { new DrawingLinkPoint3("TC1-plate", new GeoPoint3(150, 100, 10)) }
                ),
                mark.Links
            );
        }

        [Fact]
        public void EveryLeaderOfTheTestBodiesEndsOnItsObjectAndNoMarkOverlapsAnother()
        {
            var cases = new List<(string Name, DrawingObject[] Objects, GeoCoordinateSystem3 Frame, double Scale)>
            {
                ("tc1-top", Objects(TestBodies.TC1()), Top, 5),
                ("tc1-front", Objects(TestBodies.TC1()), Front, 5),
                ("tc5-top", Objects(TestBodies.TC5()), Top, 10),
                ("tc5-front", Objects(TestBodies.TC5()), Front, 10),
                ("tc5-right", Objects(TestBodies.TC5()), Right, 10),
                ("tc8-top", Objects(TestBodies.TC8()), Top, 5),
            };

            foreach (TestBody body in new[] { TestBodies.TC2(), TestBodies.TC6() })
            {
                GeoCoordinateSystem3 frame = body.ToSource().Frame;

                foreach (ViewKind kind in new[] { ViewKind.Front, ViewKind.Top, ViewKind.Right })
                {
                    cases.Add(
                        (
                            body.Name + "-" + kind,
                            Objects(body),
                            StandardViews.GetFrame(frame, kind).CoordinateSystem,
                            20
                        )
                    );
                }
            }

            var failures = new List<string>();

            foreach ((string name, DrawingObject[] objects, GeoCoordinateSystem3 frame, double scale) in cases)
            {
                Marked marked = Mark(objects, frame, scale);
                _output.WriteLine(name + ": " + string.Join(", ", marked.Marks.Select(m => m.Id + " " + m.Text)));
                failures.AddRange(
                    Marker.FindOverlaps(marked.Marks, marked.Dimensions, Tol).Select(o => name + ": " + o)
                );
                failures.AddRange(LeadersOffTheirObjects(marked).Select(o => name + ": " + o));

                // One mark for each object with a visible line that the default settings mark, and none for another.
                var seen = new HashSet<string>(marked.View.VisibleEdges.SelectMany(e => e.ObjectIds));
                string[] expected = objects
                    .Where(o => o.Kind == DrawingObjectKind.Part && o.Mark.Length > 0 && seen.Contains(o.Id))
                    .Select(o => "M-" + o.Id)
                    .ToArray();
                string[] parts = marked.Marks.Where(m => m.Kind != MarkKind.Bolt).Select(m => m.Id).ToArray();

                if (!expected.OrderBy(i => i, StringComparer.Ordinal).SequenceEqual(parts))
                {
                    failures.Add(
                        name + ": marks " + string.Join(", ", parts) + " against " + string.Join(", ", expected)
                    );
                }
            }

            failures.ForEach(_output.WriteLine);
            Assert.Empty(failures);
        }

        [Fact]
        public void NoMarkOfTC1TC2OrTC5SitsOnTheArrowsOfAShortDimensionDrawnOutsideIt()
        {
            var found = new List<string>();
            int arrows = 0;

            foreach (TestBody body in new[] { TestBodies.TC1(), TestBodies.TC2(), TestBodies.TC5() })
            {
                GeoCoordinateSystem3 frame = body.ToSource().Frame;

                foreach (ViewKind kind in new[] { ViewKind.Front, ViewKind.Top, ViewKind.Right })
                {
                    foreach (double scale in new[] { 10.0, 20.0 })
                    {
                        Marked marked = Mark(
                            Objects(body),
                            StandardViews.GetFrame(frame, kind).CoordinateSystem,
                            scale
                        );

                        foreach (DrawingDimension d in marked.Dimensions)
                        {
                            // A line shorter than two arrows has its arrows outside, each as long as the text is high and a
                            // third of that wide, its tip at an end of the line: taken here as the box round it.
                            if (!d.DimensionLine.HasValue || d.DimensionLine.Value.Length >= 2.0 * d.TextHeight)
                            {
                                continue;
                            }

                            GeoLine2 line = d.DimensionLine.Value;
                            double length = line.Length,
                                h = d.TextHeight;
                            double ux = (line.EndPoint.X - line.StartPoint.X) / length,
                                uy = (line.EndPoint.Y - line.StartPoint.Y) / length;

                            foreach (
                                (GeoPoint2 tip, double sign) in new[] { (line.StartPoint, -1.0), (line.EndPoint, 1.0) }
                            )
                            {
                                arrows++;
                                var back = new GeoPoint2(tip.X + sign * ux * h, tip.Y + sign * uy * h);
                                double minX = Math.Min(tip.X, back.X) - Math.Abs(uy) * h / 6,
                                    maxX = Math.Max(tip.X, back.X) + Math.Abs(uy) * h / 6;
                                double minY = Math.Min(tip.Y, back.Y) - Math.Abs(ux) * h / 6,
                                    maxY = Math.Max(tip.Y, back.Y) + Math.Abs(ux) * h / 6;

                                foreach (DrawingMark mark in marked.Marks)
                                {
                                    GeoRectangle2 box = mark.TextBox;
                                    double left = box.Center.X - box.Width / 2,
                                        right = box.Center.X + box.Width / 2;
                                    double bottom = box.Center.Y - box.Height / 2,
                                        top = box.Center.Y + box.Height / 2;

                                    if (
                                        left < maxX - Tol.EqualPoint
                                        && right > minX + Tol.EqualPoint
                                        && bottom < maxY - Tol.EqualPoint
                                        && top > minY + Tol.EqualPoint
                                    )
                                    {
                                        found.Add(
                                            body.Name
                                                + " "
                                                + kind
                                                + " 1:"
                                                + scale
                                                + ": "
                                                + mark.Id
                                                + " over an arrow of "
                                                + d.Id
                                        );
                                    }
                                }
                            }
                        }
                    }
                }
            }

            found.ForEach(_output.WriteLine);
            _output.WriteLine(arrows + " arrows outside short dimensions");
            Assert.NotEqual(0, arrows);
            Assert.Empty(found);
        }

        [Fact]
        public void APlateHiddenWholeBehindAnotherGetsNoMark()
        {
            DrawingObject big = Part("A-big", Box(0, 0, 10, 400, 300, 30), true);
            DrawingObject small = Part("B-small", Box(100, 100, 0, 200, 200, 10));
            DrawingObject[] objects =
            {
                new DrawingObject(big.Id, big.Kind, big.Solid, big.Frame, mark: "P1", isMain: true),
                new DrawingObject(small.Id, small.Kind, small.Solid, small.Frame, mark: "P2"),
            };

            Marked fromAbove = Mark(objects, Top, 5);
            Marked fromBelow = Mark(
                objects,
                new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.YAxis.Negate()),
                5
            );

            Assert.Equal(new[] { "M-A-big" }, fromAbove.Marks.Select(m => m.Id));
            Assert.Equal(new[] { "M-A-big", "M-B-small" }, fromBelow.Marks.Select(m => m.Id));
        }

        [Fact]
        public void TheFourBoltsOfTC5GetOneBoltMarkEndingAtABoltCentre()
        {
            Marked marked = Mark(Objects(TestBodies.TC5()), Top, 10);

            DrawingMark bolts = Assert.Single(marked.Marks, m => m.Kind == MarkKind.Bolt);
            Assert.Equal("4 x M20 x 80", bolts.Text);
            Assert.Equal("M-TC5-bolt-1", bolts.Id);
            Assert.Equal(new[] { "TC5-bolt-1", "TC5-bolt-2", "TC5-bolt-3", "TC5-bolt-4" }, bolts.ObjectIds);
            Assert.Contains(new[] { P(-150, -150), P(150, -150), P(-150, 150), P(150, 150) }, c =>
                c.DistanceTo(bolts.Anchor) <= Tol.EqualPoint
            );
        }

        [Fact]
        public void TheMainPartOfAnAssemblyGetsAnAssemblyMarkAndItsFittingsPartMarks()
        {
            Marked marked = Mark(Objects(TestBodies.TC5()), Front, 10);

            Assert.Equal(MarkKind.Assembly, marked.Marks.Single(m => m.ObjectId == "TC5-column").Kind);
            Assert.Equal("C1", marked.Marks.Single(m => m.ObjectId == "TC5-column").Text);
            Assert.Equal(MarkKind.Part, marked.Marks.Single(m => m.ObjectId == "TC5-base").Kind);
            Assert.Equal("P2", marked.Marks.Single(m => m.ObjectId == "TC5-base").Text);
        }

        [Fact]
        public void TheFieldsOfTheContentMakeTheTextAndAnEmptyFieldIsLeftOut()
        {
            var plate = new DrawingObject(
                "P-1",
                DrawingObjectKind.Part,
                Box(0, 0, 0, 300, 200, 20),
                GeoCoordinateSystem3.Global,
                mark: "P1",
                profile: "PL20*200",
                attributes: new[] { new KeyValuePair<string, string>("WEIGHT", "9.4") }
            );
            var content = new MarkContent
            {
                Part = new MarkFormat(
                    new[]
                    {
                        MarkField.Mark,
                        MarkField.Material,
                        MarkField.Profile,
                        MarkField.Length,
                        MarkField.Attribute("WEIGHT"),
                        MarkField.Count,
                    },
                    " "
                ),
            };

            Marked marked = Mark(new[] { plate }, Top, 5, content: content);

            Assert.Equal("P1 PL20*200 300 9.4 1", Assert.Single(marked.Marks).Text);
        }

        [Fact]
        public void PartsOfOneMarkCountTogetherAndALengthAttributeIsWrittenAsItIs()
        {
            DrawingObject[] objects =
            {
                new DrawingObject(
                    "S-1",
                    DrawingObjectKind.Part,
                    Box(0, 0, 0, 100, 10, 50),
                    GeoCoordinateSystem3.Global,
                    mark: "S1"
                ),
                new DrawingObject(
                    "S-2",
                    DrawingObjectKind.Part,
                    Box(300, 0, 0, 400, 10, 50),
                    GeoCoordinateSystem3.Global,
                    mark: "S1",
                    attributes: new[] { new KeyValuePair<string, string>("Length", "100.0") }
                ),
            };
            var content = new MarkContent
            {
                Part = new MarkFormat(new[] { MarkField.Count, MarkField.Mark, MarkField.Length }, "/"),
            };

            Marked marked = Mark(objects, Front, 5, content: content);

            Assert.Equal(new[] { "2/S1/100", "2/S1/100.0" }, marked.Marks.Select(m => m.Text));
        }

        [Fact]
        public void TheSettingsSwitchOffTheFittingsAndAnEmptyBoltFormatMarksNoBolts()
        {
            var settings = new MarkSettings { Fittings = false };
            var content = new MarkContent { Bolt = new MarkFormat(new MarkField[0], " ") };

            Marked marked = Mark(Objects(TestBodies.TC5()), Top, 10, settings, content);

            Assert.Equal(new[] { "M-TC5-column" }, marked.Marks.Select(m => m.Id));
        }

        [Fact]
        public void BoltsOfOneBoltGroupAttributeAreOneGroupWhateverTheirAssembly()
        {
            DrawingObject Bolt(string id, double x, string group) =>
                new DrawingObject(
                    id,
                    DrawingObjectKind.Bolt,
                    GeoSolid3.Cylinder(new GeoPoint3(x, 0, -10), new GeoPoint3(x, 0, 50), 8, 24, Tol),
                    new GeoCoordinateSystem3(new GeoPoint3(x, 0, -10), GeoVector3.ZAxis, GeoVector3.XAxis),
                    parentId: id,
                    attributes: new[]
                    {
                        new KeyValuePair<string, string>("BoltGroup", group),
                        new KeyValuePair<string, string>("Diameter", "16"),
                    }
                );

            DrawingObject[] objects = { Bolt("b1", 0, "G1"), Bolt("b2", 100, "G1"), Bolt("b3", 200, "G2") };

            Marked marked = Mark(objects, Top, 5, dimensions: false);

            Assert.Equal(new[] { "2 x M16 x 60", "1 x M16 x 60" }, marked.Marks.Select(m => m.Text));
            Assert.Equal(new[] { "b1", "b2" }, marked.Marks[0].ObjectIds);
        }

        [Fact]
        public void TenPlatesSideBySideGetTenMarksNoneOverAnotherOrADimension()
        {
            DrawingObject[] objects = Enumerable
                .Range(0, 10)
                .Select(i => new DrawingObject(
                    "P-" + i,
                    DrawingObjectKind.Part,
                    Box(i * 60, 0, 0, i * 60 + 50, 200, 10),
                    GeoCoordinateSystem3.Global,
                    mark: "P" + (i + 1),
                    isMain: i == 0
                ))
                .ToArray();

            Marked marked = Mark(objects, Top, 10);

            Assert.Equal(10, marked.Marks.Count);
            Assert.Empty(Marker.FindOverlaps(marked.Marks, marked.Dimensions, Tol));
            Assert.Empty(LeadersOffTheirObjects(marked));
        }

        [Fact]
        public void TheSameObjectsInAnotherOrderGiveTheSameMarksToTheBit()
        {
            DrawingObject[] objects = Objects(TestBodies.TC5());
            string first = string.Join("\n", Mark(objects, Front, 10).Marks.Select(Signature));
            string again = string.Join("\n", Mark(objects.Reverse().ToArray(), Front, 10).Marks.Select(Signature));

            Assert.Equal(first, again);
        }

        [Fact]
        public void FindOverlapsNamesATextOverAnotherAndALeaderAcrossAText()
        {
            Marked marked = Mark(Objects(TestBodies.TC5()), Front, 10);
            DrawingMark a = marked.Marks[0];
            DrawingMark b = marked.Marks[1];

            Assert.Empty(Marker.FindOverlaps(new[] { a, b }, new DrawingDimension[0], Tol));
            Assert.Contains(Marker.FindOverlaps(new[] { a, a }, new DrawingDimension[0], Tol), o =>
                o.Contains("text over the text of " + a.Id)
            );
        }

        [Fact]
        public void BadArgumentsAreRefused()
        {
            DrawingObject[] objects = Objects(TestBodies.TC1());
            ProjectedView2 view = Project(objects, Top);
            var none = new DrawingDimension[0];

            Assert.Throws<ArgumentNullException>(() =>
                Marker.Mark(null, objects, Top, none, new MarkSettings(), new MarkContent(), 1, Tol)
            );
            Assert.Throws<ArgumentNullException>(() =>
                Marker.Mark(view, objects, Top, none, null, new MarkContent(), 1, Tol)
            );
            Assert.Throws<ArgumentNullException>(() =>
                Marker.Mark(view, objects, Top, none, new MarkSettings(), null, 1, Tol)
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                Marker.Mark(view, objects, Top, none, new MarkSettings(), new MarkContent(), 0, Tol)
            );
            Assert.Throws<ArgumentException>(() =>
                Marker.Mark(view, objects.Concat(objects), Top, none, new MarkSettings(), new MarkContent(), 1, Tol)
            );
            Assert.Throws<ArgumentException>(() => MarkField.Attribute(""));
            Assert.Throws<ArgumentException>(() => new MarkFormat(new MarkField[] { null }, " "));
            Assert.Throws<ArgumentNullException>(() => new MarkContent { Bolt = null });
        }
    }
}
