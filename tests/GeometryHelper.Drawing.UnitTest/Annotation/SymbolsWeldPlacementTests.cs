using System.Collections.Generic;
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
    public class SymbolsWeldPlacementTests
    {
        private static readonly GeoRectangle2[] NoBoxes = new GeoRectangle2[0];

        private readonly ITestOutputHelper _output;

        public SymbolsWeldPlacementTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// TC5 with three welds: a fillet a6 250 long where the outside of a flange meets the base plate, one on both sides
        /// at the other flange, and a V butt weld s8 on the other side at the foot of a stiffener of the web.
        /// </summary>
        private static DrawingObject[] TC5WithWelds()
        {
            List<DrawingObject> objects = Objects(TestBodies.TC5()).ToList();
            objects.Add(
                Weld("TC5-weld-1", new GeoPoint3(130, 0, 30), "Fillet", "Arrow", "a6", "250", "TC5-base", "TC5-column")
            );
            objects.Add(
                Weld("TC5-weld-2", new GeoPoint3(-130, 0, 30), "Fillet", "Both", "a6", "250", "TC5-base", "TC5-column")
            );
            objects.Add(
                Weld(
                    "TC5-weld-3",
                    new GeoPoint3(10, 100, 30),
                    "VButt",
                    "Other",
                    "s8",
                    null,
                    "TC5-base",
                    "TC5-stiffener-3"
                )
            );
            return objects.ToArray();
        }

        /// <summary>A view of TC5 and its welds at a scale: its dimensions, its centre lines and its weld symbols placed.</summary>
        private static (
            ProjectedView2 View,
            IReadOnlyList<DrawingDimension> Dimensions,
            GeoLine2[] CentreLines,
            IReadOnlyList<WeldSymbol> Symbols
        ) Placed(DrawingObject[] objects, GeoCoordinateSystem3 frame, double scale)
        {
            ProjectedView2 view = Project(objects, frame);
            IReadOnlyList<DrawingDimension> dimensions = Annotator.Dimension(
                view,
                objects,
                frame,
                new DimensionSettings { Scale = scale },
                Tol
            );
            GeoLine2[] centres = DrawingCentreLine
                .Find(view, objects, new SymbolSettings(), scale, Tol)
                .Select(l => l.Line)
                .ToArray();
            IReadOnlyList<WeldSymbol> symbols = WeldSymbol.Find(
                view,
                objects,
                frame,
                dimensions,
                NoBoxes,
                centres,
                new SymbolSettings(),
                scale,
                Tol
            );
            return (view, dimensions, centres, symbols);
        }

        [Fact]
        public void TheWeldSymbolsOfTC5AreClearOfItsDimensionsCentreLinesAndOneAnotherInEveryViewAtEveryScale()
        {
            DrawingObject[] objects = TC5WithWelds();
            var found = new List<string>();
            int count = 0;

            foreach (
                (string name, GeoCoordinateSystem3 frame) in new[] { ("front", Front), ("top", Top), ("right", Right) }
            )
            {
                foreach (double scale in new[] { 2.0, 5.0, 10.0, 20.0 })
                {
                    var placed = Placed(objects, frame, scale);
                    IReadOnlyList<WeldSymbol> ruled = WeldSymbol.Find(
                        placed.View,
                        objects,
                        frame,
                        new SymbolSettings(),
                        scale
                    );
                    count += placed.Symbols.Count;

                    // Each symbol keeps its weld: the same symbols, their arrows ending at their welds' middles.
                    Assert.Equal(ruled.Select(w => w.Id), placed.Symbols.Select(w => w.Id));
                    Assert.Equal(ruled.Select(w => w.ArrowTip), placed.Symbols.Select(w => w.ArrowTip));
                    Assert.Equal(ruled.Select(w => w.Links), placed.Symbols.Select(w => w.Links));

                    foreach (
                        string overlap in WeldSymbol.FindOverlaps(
                            placed.Symbols,
                            placed.Dimensions,
                            NoBoxes,
                            placed.CentreLines,
                            Tol
                        )
                    )
                    {
                        found.Add(name + " 1:" + scale + ": " + overlap);
                    }
                }
            }

            found.ForEach(_output.WriteLine);
            Assert.Equal(3 * 4 * 3, count);
            Assert.Empty(found);
        }

        [Fact]
        public void InADrawingOfTC5TheWeldSymbolsKeepClearOfTheDimensionsAndCentreLinesAndTheMarksOfThem()
        {
            Drawing drawing = DrawingBuilder.Build(new DrawingSource(TC5WithWelds()), DrawingSettings.Assembly, Tol);
            var found = new List<string>();

            foreach (DrawingView view in drawing.Views)
            {
                GeoLine2[] centres = view.CentreLines.Select(l => l.Line).ToArray();
                found.AddRange(
                    WeldSymbol
                        .FindOverlaps(view.WeldSymbols, view.Dimensions, NoBoxes, centres, Tol)
                        .Select(o => view.Id + ": " + o)
                );
                GeoRectangle2[] welds = view.WeldSymbols.Select(w => w.Bounds).ToArray();
                GeoLine2[] arrows = view.WeldSymbols.Select(w => new GeoLine2(w.Knee, w.ArrowTip)).ToArray();
                found.AddRange(
                    Marker.FindOverlaps(view.Marks, view.Dimensions, welds, arrows, Tol).Select(o => view.Id + ": " + o)
                );
            }

            found.ForEach(_output.WriteLine);
            Assert.Equal(9, drawing.Views.Sum(v => v.WeldSymbols.Count));
            Assert.NotEmpty(drawing.Views.SelectMany(v => v.Marks));
            Assert.Empty(found);
        }

        [Fact]
        public void ASymbolWithNothingInTheWayStaysWhereTheRulePutsIt()
        {
            DrawingObject plate = Part("P-1", Box(0, 0, 0, 300, 200, 20), true);
            DrawingObject[] objects =
            {
                plate,
                Weld("W-1", new GeoPoint3(100, 0, 30), "Fillet", "Arrow", "a5", "200", "P-1", "P-2"),
            };
            ProjectedView2 view = Project(objects, Front);

            WeldSymbol ruled = Assert.Single(WeldSymbol.Find(view, objects, Front, new SymbolSettings(), 10));
            WeldSymbol placed = Assert.Single(
                WeldSymbol.Find(
                    view,
                    objects,
                    Front,
                    new DrawingDimension[0],
                    NoBoxes,
                    new GeoLine2[0],
                    new SymbolSettings(),
                    10,
                    Tol
                )
            );

            Assert.Equal(ruled.Knee, placed.Knee);
            Assert.Equal(ruled.ReferenceLine, placed.ReferenceLine);
            Assert.Equal(ruled.Bounds, placed.Bounds);
        }

        [Fact]
        public void ASymbolMovedLeftOfItsWeldRunsItsReferenceLineToTheLeftAndIsDrawnThere()
        {
            // A box kept clear of over the places right of the weld, from the weld up and down: the symbol goes left.
            DrawingObject[] objects = { Weld("W-1", new GeoPoint3(0, 0, 0), "Fillet", "Arrow", "a5", "200") };
            ProjectedView2 view = Project(objects, Front);
            var wall = new GeoRectangle2(new GeoPoint2(1000, 0), 1980, 4000);

            WeldSymbol symbol = Assert.Single(
                WeldSymbol.Find(
                    view,
                    objects,
                    Front,
                    new DrawingDimension[0],
                    new[] { wall },
                    new GeoLine2[0],
                    new SymbolSettings(),
                    10,
                    Tol
                )
            );

            Assert.True(symbol.Knee.X < symbol.ArrowTip.X, symbol.Knee.ToString());
            Assert.True(symbol.ReferenceLine.EndPoint.X < symbol.Knee.X, symbol.ReferenceLine.ToString());
            Near(7.6 * 25, symbol.ReferenceLine.Length);
            Assert.Empty(
                WeldSymbol.FindOverlaps(new[] { symbol }, new DrawingDimension[0], new[] { wall }, new GeoLine2[0], Tol)
            );

            // Drawn as on a line run to the right: "a5" first, then the triangle, then "200", all left of the knee.
            var database = new DxfDatabase("AC1027");
            symbol.WriteTo(database, new ViewPlacement2("V-1", new GeoPoint2(0, 0), 10));
            DxfMText[] texts = database.ModelSpace.OfType<DxfMText>().ToArray();
            Assert.Equal(new[] { "a5", "200" }, texts.Select(t => t.Contents));
            Assert.True(texts[0].Location.X < texts[1].Location.X);
            Assert.All(texts, t => Assert.True(t.Location.X < symbol.Knee.X / 10));
        }

        [Fact]
        public void TheWeldSymbolsOfTC5AreDrawnToASheetToLookAt()
        {
            DrawingObject[] objects = TC5WithWelds();

            foreach (
                (string name, GeoCoordinateSystem3 frame) in new[] { ("front", Front), ("top", Top), ("right", Right) }
            )
            {
                foreach (double scale in new[] { 2.0, 10.0, 20.0 })
                {
                    var placed = Placed(objects, frame, scale);
                    var database = new DxfDatabase("AC1027");
                    var placement = new ViewPlacement2("V-1", new GeoPoint2(0, 0), scale);
                    placed.View.WriteTo(database);
                    GeoTransform3 shrink = GeoTransform3.Scaling(1.0 / scale);

                    foreach (DxfEntity entity in database.ModelSpace.ToList())
                    {
                        entity.TransformBy(shrink);
                    }

                    foreach (DrawingDimension dimension in placed.Dimensions)
                    {
                        dimension.WriteTo(database, placement);
                    }

                    foreach (WeldSymbol weld in placed.Symbols)
                    {
                        weld.WriteTo(database, placement);
                    }

                    _output.WriteLine(SavePicture(database, "tc5-welds-" + name + "-1to" + scale));
                }
            }
        }
    }
}
