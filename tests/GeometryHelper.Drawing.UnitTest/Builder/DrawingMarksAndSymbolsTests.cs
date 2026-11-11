using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Drawing.UnitTest.Annotation.MarksKit;
using static GeometryHelper.Drawing.UnitTest.Builder.BuilderKit;

namespace GeometryHelper.Drawing.UnitTest.Builder
{
    /// <summary>
    /// The marks and symbols of a whole drawing: placed by the marker, kept in the document, moved by hand and updated;
    /// centre lines, weld symbols, section, detail and level marks written with the views; and the sheet on its paper.
    /// </summary>
    public class DrawingMarksAndSymbolsTests
    {
        private const string MarkId = "V-FRONT/M-TC2-endplate";

        private readonly ITestOutputHelper _output;

        public DrawingMarksAndSymbolsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static bool Inside(GeoRectangle2 box, GeoPoint2 p) =>
            p.X >= box.Center.X - box.Width / 2 - Tol.EqualPoint
            && p.X <= box.Center.X + box.Width / 2 + Tol.EqualPoint
            && p.Y >= box.Center.Y - box.Height / 2 - Tol.EqualPoint
            && p.Y <= box.Center.Y + box.Height / 2 + Tol.EqualPoint;

        private static bool Inside(GeoRectangle2 box, GeoRectangle2 inner) =>
            inner.GetVertices().All(p => Inside(box, p));

        /// <summary>TC5 with a fillet weld a6 250 long where the column's flange meets the base plate.</summary>
        private static DrawingSource TC5WithAWeld()
        {
            var objects = TestBodies.TC5().ToSource().Objects.ToList();
            objects.Add(
                Weld("TC5-weld", new GeoPoint3(130, 0, 30), "Fillet", "Arrow", "a6", "250", "TC5-base", "TC5-column")
            );
            return new DrawingSource(objects);
        }

        /// <summary>TC2, its beam giving the elevation of its top, 3 600.</summary>
        private static DrawingSource TC2WithAnElevation()
        {
            DrawingSource tc2 = TC2();
            var objects = tc2
                .Objects.Select(o =>
                    o.Id != "TC2-beam"
                        ? o
                        : new DrawingObject(
                            o.Id,
                            o.Kind,
                            o.Solid,
                            o.Frame,
                            o.Name,
                            o.Mark,
                            o.Profile,
                            o.Material,
                            o.ParentId,
                            o.IsMain,
                            new[] { new KeyValuePair<string, string>(LevelMark.ElevationAttribute, "3600") }
                        )
                )
                .ToList();
            return new DrawingSource(objects);
        }

        [Fact]
        public void TheMarksOfADrawingArePlacedByTheMarkerClearOfItsDimensionsAndKeptInItsDocument()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            DrawingView front = drawing.Views[0];

            DrawingMark mark = Assert.Single(front.Marks);
            Assert.Equal("M-TC2-endplate", mark.Id);
            Assert.Equal(MarkKind.Part, mark.Kind);
            Assert.Equal("P1", mark.Text);
            Assert.Equal(2, mark.Leader.Count);
            Assert.Empty(Marker.FindOverlaps(front.Marks, front.Dimensions, Tol));

            DrawingElement element = drawing.Elements.Single(e => e.Id == MarkId);
            Assert.Equal("Mark", element.Kind);
            Assert.Equal("V-FRONT", element.ViewId);
            Assert.Equal(mark.Links, element.Links);
            Assert.Equal(
                new[] { "Kind", "Text", "TextBox", "Leader", "TextHeight", "Scale" },
                element.Values.Select(v => v.Key)
            );

            // Written as the mark writes itself: a LINE to the plate, its arrow, and the text in the middle of its box.
            List<DxfEntity> entities = Tagged(drawing.Database.ModelSpace.Cast<DxfEntity>(), MarkId);
            Assert.Equal(new[] { "DxfLine", "DxfSolid", "DxfMText" }, entities.Select(e => e.GetType().Name));
            Assert.All(entities, e => Assert.Equal(DrawingMark.Layer, e.Layer));
            var text = (DxfMText)entities[2];
            Assert.Equal(front.ModelOrigin.X + mark.TextBox.Center.X, text.Location.X, 9);
            Assert.Equal(front.ModelOrigin.Y + mark.TextBox.Center.Y, text.Location.Y, 9);
            Assert.Equal(front.ModelOrigin.X + mark.Anchor.X, ((DxfLine)entities[0]).EndPoint.X, 9);

            // Saved and opened, the document reads the same.
            string path = Path.Combine(Folder(), "tc2-marks.dxf");
            drawing.Save(path);
            Drawing opened = Drawing.Open(path, TC2(), Tol);
            Assert.Equal(drawing.Elements, opened.Elements);
        }

        [Fact]
        public void TheBoxOfEachViewHoldsTheLeadersAndTextsOfItsMarksAndItsSymbols()
        {
            DrawingSettings settings = DrawingSettings.Part;
            settings.Views.Details.Add(
                new DetailViewRequest("B", ViewKind.Front, new GeoRectangle2(2800, -200, 300, 400), 4)
            );

            foreach (
                Drawing drawing in new[]
                {
                    DrawingBuilder.Build(TC2(), settings, Tol),
                    DrawingBuilder.Build(TC5WithAWeld(), DrawingSettings.Assembly, Tol),
                }
            )
            {
                foreach (DrawingView view in drawing.Views)
                {
                    Assert.All(view.Marks, m =>
                        Assert.True(m.Leader.All(p => Inside(view.Box, p)) && Inside(view.Box, m.TextBox), m.ToString())
                    );
                    Assert.All(view.CentreLines, l =>
                        Assert.True(
                            Inside(view.Box, l.Line.StartPoint) && Inside(view.Box, l.Line.EndPoint),
                            l.ToString()
                        )
                    );
                    Assert.All(view.WeldSymbols, w =>
                        Assert.True(Inside(view.Box, w.Bounds) && Inside(view.Box, w.ArrowTip), w.ToString())
                    );
                    Assert.All(view.SectionMarks, s =>
                        Assert.True(s.LetterBoxes.All(b => Inside(view.Box, b)), s.ToString())
                    );
                    Assert.All(view.DetailMarks, d =>
                        Assert.True(Inside(view.Box, d.Bounds) && Inside(view.Box, d.LetterBox), d.ToString())
                    );
                }
            }
        }

        [Fact]
        public void AMarkMovedByHandKeepsItsArrowOnThePlateAndItsMoveAfterAnUpdate()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            CadDocument document = drawing.OpenInPresenter();
            DxfMText text = Tagged(document.Database.ModelSpace.Cast<DxfEntity>(), MarkId).OfType<DxfMText>().Single();
            Assert.True(
                document.Owners.TryApply(document, new CadEdit(text.Handle, CadEditKind.Move, newValue: "0,60"))
            );

            drawing.Update(TC2(3500));

            DrawingView front = drawing.Views[0];
            DrawingMark mark = front.Marks.Single(m => m.Id == "M-TC2-endplate");
            List<DxfEntity> entities = Tagged(document.Database.ModelSpace.Cast<DxfEntity>(), MarkId);
            DxfMText moved = entities.OfType<DxfMText>().Single();
            DxfLine leader = entities.OfType<DxfLine>().Single();
            Assert.True(mark.Anchor.X > 3000, "The plate is now past 3 500.");
            Assert.Equal(front.ModelOrigin.X + mark.TextBox.Center.X, moved.Location.X, 9);
            Assert.Equal(front.ModelOrigin.Y + mark.TextBox.Center.Y + 60, moved.Location.Y, 9);
            Assert.Equal(front.ModelOrigin.X + mark.Leader[0].X, leader.StartPoint.X, 9);
            Assert.Equal(front.ModelOrigin.Y + mark.Leader[0].Y + 60, leader.StartPoint.Y, 9);
            Assert.Equal(front.ModelOrigin.X + mark.Anchor.X, leader.EndPoint.X, 9);
            Assert.Equal(front.ModelOrigin.Y + mark.Anchor.Y, leader.EndPoint.Y, 9);
        }

        [Fact]
        public void WhatTheMarksShowIsASettingCopiedWithTheDrawingAndKeptInItsDocument()
        {
            DrawingSettings settings = AssemblyOnA3();
            settings.Marks.Content.Part = new MarkFormat(
                new[] { MarkField.Mark, MarkField.Profile, MarkField.Attribute("WEIGHT") },
                " "
            );
            Drawing drawing = DrawingBuilder.Build(TC2(), settings, Tol);
            settings.Marks.Content.Part = new MarkFormat(new[] { MarkField.Name }, " ");

            Assert.Equal("P1 PL20*200", drawing.Views[0].Marks.Single().Text);
            Assert.Equal(
                new[] { "Mark", "Profile", "Attribute WEIGHT" },
                drawing.Settings.Marks.Content.Part.Fields.Select(f => f.ToString())
            );
            Assert.Equal(4.0, DrawingSettings.Assembly.Marks.Offset);

            string path = Path.Combine(Folder(), "tc2-mark-content.dxf");
            drawing.Save(path);
            DrawingSettings read = Drawing.Open(path, TC2(), Tol).Settings;
            Assert.Equal(
                new[] { "Mark", "Profile", "Attribute WEIGHT" },
                read.Marks.Content.Part.Fields.Select(f => f.ToString())
            );
            Assert.Equal(" ", read.Marks.Content.Part.Separator);
            Assert.Equal(" x ", read.Marks.Content.Bolt.Separator);
        }

        [Fact]
        public void TheSymbolSettingsAreCopiedWithTheDrawingAndKeptInItsDocument()
        {
            DrawingSettings settings = AssemblyOnA3();
            settings.Symbols.TextHeight = 3;
            settings.Symbols.CentreLineExtension = 2;
            settings.Symbols.LevelMarks = true;
            Drawing drawing = DrawingBuilder.Build(TC2(), settings, Tol);
            settings.Symbols.TextHeight = 9;

            string path = Path.Combine(Folder(), "tc2-symbol-settings.dxf");
            drawing.Save(path);
            SymbolSettings read = Drawing.Open(path, TC2(), Tol).Settings.Symbols;

            Assert.Equal(3.0, drawing.Settings.Symbols.TextHeight);
            Assert.Equal(3.0, read.TextHeight);
            Assert.Equal(2.0, read.CentreLineExtension);
            Assert.True(read.LevelMarks);
            Assert.Equal(WeldStandard.Iso2553, read.WeldStandard);
            Assert.False(DrawingSettings.Assembly.Symbols.LevelMarks);
            Assert.True(DrawingSettings.General.Symbols.LevelMarks);
        }

        [Fact]
        public void TheDrawingsOfTC1TC2AndTC5HaveTheCentreLinesOfTheirHolesOnTheCentreLayer()
        {
            var drawings = new[]
            {
                DrawingBuilder.Build(TestBodies.TC1().ToSource(), DrawingSettings.Part, Tol),
                DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol),
                DrawingBuilder.Build(TestBodies.TC5().ToSource(), DrawingSettings.Assembly, Tol),
            };

            foreach (Drawing drawing in drawings)
            {
                int lines = drawing.Views.Sum(v => v.CentreLines.Count);
                _output.WriteLine($"{drawing.Id}: {lines} centre lines");
                Assert.True(lines > 0, drawing.Id);
                List<DxfLine> written = drawing
                    .Database.ModelSpace.Cast<DxfEntity>()
                    .OfType<DxfLine>()
                    .Where(l =>
                        !l.IsErased
                        && l.Layer == "Centre"
                        && DrawingEntityTags.TryRead(l, out string id, out _)
                        && id.Contains("/CL-")
                    )
                    .ToList();
                Assert.Equal(lines, written.Count);
                Assert.All(drawing.Views.SelectMany(v => v.CentreLines.Select(l => v.Id + "/" + l.Id)), id =>
                    Assert.Contains(drawing.Elements, e => e.Id == id && e.Kind == "Symbol")
                );
            }

            // The plate of TC1 seen on its face, in the front view of its frame: two level lines and two upright ones through
            // its four holes.
            Assert.Equal(4, drawings[0].Views.Single(v => v.Kind == ViewKind.Front).CentreLines.Count);
        }

        [Fact]
        public void AWeldOfTheSourceGetsItsSymbolInTheViewsThatSeeIt()
        {
            Drawing drawing = DrawingBuilder.Build(TC5WithAWeld(), DrawingSettings.Assembly, Tol);
            Drawing without = DrawingBuilder.Build(TestBodies.TC5().ToSource(), DrawingSettings.Assembly, Tol);

            WeldSymbol weld = drawing.Views.SelectMany(v => v.WeldSymbols).First();
            Assert.Equal("W-TC5-weld", weld.Id);
            Assert.Equal("a6", weld.Size);
            Assert.Equal("250", weld.Length);
            Assert.Empty(without.Views.SelectMany(v => v.WeldSymbols));

            DrawingView view = drawing.Views.First(v => v.WeldSymbols.Count > 0);
            List<DxfEntity> written = Tagged(drawing.Database.ModelSpace.Cast<DxfEntity>(), view.Id + "/W-TC5-weld");
            Assert.NotEmpty(written);
            Assert.All(written, e => Assert.Equal(SymbolSettings.Layer, e.Layer));
            Assert.Contains(written, e => e is DxfMText t && t.Contents == "a6");
        }

        [Fact]
        public void TheViewASectionIsCutFromShowsItsSectionMarkAndTheViewOfADetailItsDetailMark()
        {
            DrawingSettings settings = DrawingSettings.Part;
            settings.Views.Details.Add(
                new DetailViewRequest("B", ViewKind.Front, new GeoRectangle2(2800, -200, 300, 400), 4)
            );
            Drawing drawing = DrawingBuilder.Build(TC2(), settings, Tol);
            DrawingView front = drawing.Views.Single(v => v.Id == "V-FRONT");

            // SECTION A-A looks as the right end view does, cut half way along the beam and its end plate, 3 020 long: in the
            // front view an upright line at 1 510 across the crop, the section looking towards the beam's start.
            SectionMark section = Assert.Single(front.SectionMarks);
            Assert.Equal("A", section.Letter);
            Assert.Equal(1510.0, section.CutLine.StartPoint.X, 6);
            Assert.Equal(1510.0, section.CutLine.EndPoint.X, 6);
            Assert.Equal(front.Crop.Height, section.CutLine.Length, 6);
            Assert.Equal(-1.0, section.Direction.X, 9);
            Assert.All(drawing.Views.Where(v => v.Id != "V-FRONT"), v => Assert.Empty(v.SectionMarks));

            DetailMark detail = Assert.Single(front.DetailMarks);
            Assert.Equal("B", detail.Letter);
            Assert.Equal(new GeoRectangle2(2800, -200, 300, 400), detail.Bounds);

            List<DxfEntity> cut = Tagged(drawing.Database.ModelSpace.Cast<DxfEntity>(), "V-FRONT/SM-A");
            Assert.Equal(2, cut.OfType<DxfMText>().Count(t => t.Contents == "A"));
            Assert.Contains(Tagged(drawing.Database.ModelSpace.Cast<DxfEntity>(), "V-FRONT/DM-B"), e =>
                e is DxfMText t && t.Contents == "B"
            );
        }

        [Fact]
        public void TheGeneralPresetMarksTheLevelOfAnObjectThatGivesItsElevationInItsElevationsOnly()
        {
            Drawing general = DrawingBuilder.Build(TC2WithAnElevation(), DrawingSettings.General, Tol);
            Drawing assembly = DrawingBuilder.Build(TC2WithAnElevation(), AssemblyOnA3(), Tol);

            DrawingView front = general.Views.Single(v => v.Id == "V-FRONT");
            LevelMark level = Assert.Single(front.LevelMarks);
            Assert.Equal("+3600", level.Text);
            Assert.Equal(3600.0, level.Elevation);
            Assert.Empty(general.Views.Single(v => v.Id == "V-TOP").LevelMarks);
            Assert.Empty(assembly.Views.SelectMany(v => v.LevelMarks));
            Assert.Contains(Tagged(general.Database.ModelSpace.Cast<DxfEntity>(), "V-FRONT/L-TC2-beam"), e =>
                e is DxfMText t && t.Contents == "+3600"
            );
        }

        [Fact]
        public void TheSameSourceGivesTheSameDxfBytesWithItsMarksAndSymbols()
        {
            DrawingSettings settings = DrawingSettings.Part;
            settings.Views.Details.Add(
                new DetailViewRequest("B", ViewKind.Front, new GeoRectangle2(2800, -200, 300, 400), 4)
            );

            Assert.Equal(
                Bytes(DrawingBuilder.Build(TC2(), settings, Tol).Database),
                Bytes(DrawingBuilder.Build(TC2(), settings, Tol).Database)
            );
            Assert.Equal(
                Bytes(DrawingBuilder.Build(TC5WithAWeld(), DrawingSettings.Assembly, Tol).Database),
                Bytes(DrawingBuilder.Build(TC5WithAWeld(), DrawingSettings.Assembly, Tol).Database)
            );
        }

        [Theory]
        [InlineData(PaperSize.A2, SheetOrientation.Landscape, "ISO_A2_(594.00_x_420.00_MM)", 594.0, 420.0)]
        [InlineData(PaperSize.A3, SheetOrientation.Landscape, "ISO_A3_(420.00_x_297.00_MM)", 420.0, 297.0)]
        [InlineData(PaperSize.A1, SheetOrientation.Portrait, "ISO_A1_(594.00_x_841.00_MM)", 594.0, 841.0)]
        public void TheSheetIsLaidOutOnPaperOfItsSize(
            PaperSize size,
            SheetOrientation orientation,
            string media,
            double width,
            double height
        )
        {
            DrawingSettings settings = DrawingSettings.Assembly;
            settings.Sheet.Size = size;
            settings.Sheet.Orientation = orientation;
            Drawing drawing = DrawingBuilder.Build(TC2(), settings, Tol);
            DxfLayout layout = SheetLayout(SavedAndOpened(drawing.Database));

            Assert.Equal(media, layout.CanonicalMediaName);
            Assert.Equal(new DxfExtents2d(new GeoPoint2(0, 0), new GeoPoint2(0, 0)), layout.PlotPaperMargins);

            // The paper the presenter draws is the sheet's edge, from the origin.
            CadSpace sheet = CadDocument
                .From(drawing.Database, "sheet.dxf")
                .Spaces.Single(s => s.Name == DrawingDxfWriter.LayoutName);
            Assert.Equal(0.0, sheet.PaperBounds.MinX, 9);
            Assert.Equal(0.0, sheet.PaperBounds.MinY, 9);
            Assert.Equal(width, sheet.PaperBounds.Width, 9);
            Assert.Equal(height, sheet.PaperBounds.Height, 9);
            Assert.Equal(drawing.Sheet.Width, sheet.PaperBounds.Width, 9);
        }

        [Fact]
        public void AnUpdateLeavesNoBlockOfADimensionItErased()
        {
            Drawing drawing = DrawingBuilder.Build(TC2(), AssemblyOnA3(), Tol);
            int Pictures() =>
                drawing.Database.BlockTable.Count(b =>
                    !b.IsErased && b.IsAnonymous && b.Name.StartsWith("*D", System.StringComparison.Ordinal)
                );
            int Dimensions() =>
                drawing.Database.ModelSpace.Cast<DxfEntity>().Count(e => !e.IsErased && e is DxfDimension);
            Assert.Equal(Dimensions(), Pictures());

            drawing.Update(TC2(3500));
            drawing.Update(TC2(4000));

            Assert.Equal(Dimensions(), Pictures());
            Assert.All(
                drawing.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfDimension>().Where(d => !d.IsErased),
                d => Assert.True(d.DimBlock.IsAnonymous)
            );
        }

        /// <summary>What the marks of a view keep clear of: the texts and lines of its symbols, worked out from what they show.</summary>
        private static void Symbols(DrawingView view, out List<GeoRectangle2> boxes, out List<GeoLine2> lines)
        {
            boxes = new List<GeoRectangle2>();
            lines = new List<GeoLine2>();
            lines.AddRange(view.CentreLines.Select(l => l.Line));

            foreach (WeldSymbol weld in view.WeldSymbols)
            {
                boxes.Add(weld.Bounds);
                lines.Add(new GeoLine2(weld.Knee, weld.ArrowTip));
            }

            foreach (SectionMark section in view.SectionMarks)
            {
                lines.Add(section.CutLine);
                boxes.AddRange(section.LetterBoxes);
            }

            foreach (DetailMark detail in view.DetailMarks)
            {
                GeoPoint2[] corners = detail.Bounds.GetVertices();
                lines.AddRange(corners.Select((c, i) => new GeoLine2(c, corners[(i + 1) % 4])));
                boxes.Add(detail.LetterBox);
            }

            boxes.AddRange(view.LevelMarks.Select(l => l.TextBox));
        }

        [Fact]
        public void NoMarkOfTC1TC2OrTC5StandsOverASymbolInAnyOfItsThreeViewsAtAnyOfSeveralScales()
        {
            var cases = new List<(string Name, DrawingSource Source, DrawingSettings Settings)>();

            foreach (double scale in new[] { 2.0, 5.0, 10.0, 20.0 })
            {
                DrawingSettings part = DrawingSettings.Part;
                part.Views.Details.Add(
                    new DetailViewRequest("B", ViewKind.Front, new GeoRectangle2(2800, -200, 300, 400), 2)
                );
                DrawingSettings assembly = DrawingSettings.Assembly;

                foreach (DrawingSettings settings in new[] { part, assembly })
                {
                    settings.Scales.Clear();
                    settings.Scales.Add(scale);
                    settings.Scales.Add(scale / 2);
                    settings.Sheet.Size = PaperSize.A0;

                    foreach (string id in new[] { "V-FRONT", "V-TOP", "V-RIGHT" })
                    {
                        settings.Views.MarkedViews.Add(id);
                    }
                }

                cases.Add(("TC1 1:" + scale, TestBodies.TC1().ToSource(), part));
                cases.Add(("TC2 1:" + scale, TC2(), part));
                cases.Add(("TC5 1:" + scale, TC5WithAWeld(), assembly));
            }

            int marks = 0,
                symbols = 0;

            foreach ((string name, DrawingSource source, DrawingSettings settings) in cases)
            {
                Drawing drawing = DrawingBuilder.Build(source, settings, Tol);

                foreach (DrawingView view in drawing.Views.Where(v => v.Marks.Count > 0))
                {
                    Symbols(view, out List<GeoRectangle2> boxes, out List<GeoLine2> lines);
                    marks += view.Marks.Count;
                    symbols += boxes.Count + lines.Count;
                    IReadOnlyList<string> overlaps = Marker.FindOverlaps(
                        view.Marks,
                        view.Dimensions,
                        boxes,
                        lines,
                        Tol
                    );
                    Assert.True(overlaps.Count == 0, name + " " + view.Id + ": " + string.Join("; ", overlaps));
                }
            }

            _output.WriteLine($"{marks} marks clear of {symbols} symbol boxes and lines");
            Assert.True(marks > 30 && symbols > 30);
        }

        [Fact]
        public void NoMarkOrWeldSymbolOfTC1TC2OrTC5StandsOnADimensionArrowInsideOrOutsideItsLineAtOneToFiveTenOrTwenty()
        {
            // TC5 with three welds: a fillet on the outside of each flange at the base plate, one of them on both sides, and a
            // V butt weld on the other side at the foot of a stiffener of the web.
            var tc5 = TestBodies.TC5().ToSource().Objects.ToList();
            tc5.Add(
                Weld("TC5-weld-1", new GeoPoint3(130, 0, 30), "Fillet", "Arrow", "a6", "250", "TC5-base", "TC5-column")
            );
            tc5.Add(
                Weld("TC5-weld-2", new GeoPoint3(-130, 0, 30), "Fillet", "Both", "a6", "250", "TC5-base", "TC5-column")
            );
            tc5.Add(
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
            var found = new List<string>();
            int arrows = 0,
                marks = 0,
                symbols = 0,
                views = 0;

            foreach (
                (bool big, double scale) in new[]
                {
                    (false, 5.0),
                    (false, 10.0),
                    (false, 20.0),
                    (true, 5.0),
                    (true, 10.0),
                    (true, 20.0),
                }
            )
            {
                foreach (
                    (string name, DrawingSource source) in new[]
                    {
                        ("TC1", TestBodies.TC1().ToSource()),
                        ("TC2", TC2()),
                        ("TC5", new DrawingSource(tc5)),
                    }
                )
                {
                    foreach (DrawingSettings settings in new[] { DrawingSettings.Part, DrawingSettings.Assembly })
                    {
                        settings.Scales.Clear();
                        settings.Scales.Add(scale);
                        settings.Sheet.Size = PaperSize.A0;

                        // The presets' own texts, and a dimension text four times the marks' and ten times the weld symbols', its
                        // arrows then wider than the margins the marks and symbols keep off lines.
                        if (big)
                        {
                            settings.Dimensions.TextHeight = 10;
                            settings.Marks.TextHeight = 2.5;
                            settings.Symbols.TextHeight = 1;
                        }

                        foreach (string id in new[] { "V-FRONT", "V-TOP", "V-RIGHT" })
                        {
                            settings.Views.MarkedViews.Add(id);
                        }

                        Drawing drawing = DrawingBuilder.Build(source, settings, Tol);

                        foreach (
                            DrawingView view in drawing.Views.Where(v =>
                                v.Kind == ViewKind.Front || v.Kind == ViewKind.Top || v.Kind == ViewKind.Right
                            )
                        )
                        {
                            Assert.Equal(scale, view.Scale);
                            views++;
                            marks += view.Marks.Count;
                            symbols += view.WeldSymbols.Count;

                            foreach (DrawingDimension d in view.Dimensions)
                            {
                                foreach (var arrow in Arrowheads(d))
                                {
                                    arrows++;
                                    found.AddRange(
                                        view.Marks.Where(m => Overlaps(m.TextBox, arrow))
                                            .Select(m =>
                                                name
                                                + " "
                                                + view.Id
                                                + " 1:"
                                                + scale
                                                + (big ? " big" : string.Empty)
                                                + ": "
                                                + m.Id
                                                + " over an arrow of "
                                                + d.Id
                                            )
                                    );
                                    found.AddRange(
                                        view.WeldSymbols.Where(w => Overlaps(w.Bounds, arrow))
                                            .Select(w =>
                                                name
                                                + " "
                                                + view.Id
                                                + " 1:"
                                                + scale
                                                + (big ? " big" : string.Empty)
                                                + ": "
                                                + w.Id
                                                + " over an arrow of "
                                                + d.Id
                                            )
                                    );
                                }
                            }
                        }
                    }
                }
            }

            found.ForEach(_output.WriteLine);
            _output.WriteLine($"{views} views, {marks} marks and {symbols} weld symbols kept off {arrows} arrows");
            Assert.True(
                arrows > 100 && marks > 30 && symbols > 10,
                $"{arrows} arrows, {marks} marks, {symbols} symbols"
            );
            Assert.Empty(found);
        }

        [Fact]
        public void InTheGeneralArrangementOfAHundredColumnBasesABoltMarkCrowdedOffTheMarginOfTheCentreLinesStandsClearOfThemByItsText()
        {
            // The plan at 1:200: a column mark C1 and a bolt mark "4 x M20 x 80", some 6 000 wide, for each of the 100 bases
            // 6 000 apart, and the 400 centre lines of their bolts.
            Drawing drawing = DrawingBuilder.Build(TestBodies.TC10(10, 10).ToSource(), DrawingSettings.General, Tol);
            DrawingView plan = drawing.Views.Single(v => v.Marks.Count > 0);
            Symbols(plan, out List<GeoRectangle2> boxes, out List<GeoLine2> lines);

            Assert.Equal(200.0, plan.Scale);
            Assert.Equal(400, lines.Count);
            Assert.Equal(100, plan.Marks.Count(m => m.Kind == MarkKind.Assembly));
            Assert.Equal(91, plan.Marks.Count(m => m.Kind == MarkKind.Bolt));
            Assert.Empty(Marker.FindOverlaps(plan.Marks, plan.Dimensions, boxes, lines, Tol));
        }

        [Fact]
        public void TheTextOfAMarkIsTakenAsWideAsThePresenterDrawsIt()
        {
            Drawing drawing = DrawingBuilder.Build(TC5WithAWeld(), DrawingSettings.Assembly, Tol);
            DrawingView front = drawing.Views.Single(v => v.Id == "V-FRONT");
            DrawingMark bolts = front.Marks.Single(m => m.Kind == MarkKind.Bolt);
            Assert.Equal("4 x M20 x 80", bolts.Text);

            // The presenter inks "4 x M20 x 80" 1 high about 11.4 wide; the box takes it as 12, and stands in the view's box,
            // so that the viewport shows it whole.
            Assert.True(
                InkWidth(bolts.Text) <= bolts.TextBox.Width / bolts.TextHeight,
                InkWidth(bolts.Text).ToString()
            );
            Assert.True(Inside(front.Box, bolts.TextBox));
            Assert.All(drawing.Views.SelectMany(v => v.Marks), m =>
                Assert.True(InkWidth(m.Text) <= m.TextBox.Width / m.TextHeight, m.Text)
            );
        }

        [Fact]
        public void TheMarkOfTheEndPlateOfTC2StandsOffTheBeamNotOverItsWeb()
        {
            foreach (DrawingSettings settings in new[] { AssemblyOnA3(), DrawingSettings.Assembly })
            {
                Drawing drawing = DrawingBuilder.Build(TC2(), settings, Tol);
                DrawingView front = drawing.Views[0];
                DrawingMark mark = front.Marks.Single(m => m.ObjectId == "TC2-endplate");
                var beam = front
                    .Lines.VisibleEdges.Where(e => e.ObjectIds.Contains("TC2-beam"))
                    .SelectMany(e => new[] { e.StartPoint, e.EndPoint })
                    .ToList();
                double x0 = beam.Min(p => p.X),
                    x1 = beam.Max(p => p.X),
                    y0 = beam.Min(p => p.Y),
                    y1 = beam.Max(p => p.Y);
                GeoRectangle2 text = mark.TextBox;
                bool apart =
                    text.Center.X - text.Width / 2 >= x1 - Tol.EqualPoint
                    || text.Center.X + text.Width / 2 <= x0 + Tol.EqualPoint
                    || text.Center.Y - text.Height / 2 >= y1 - Tol.EqualPoint
                    || text.Center.Y + text.Height / 2 <= y0 + Tol.EqualPoint;
                Assert.True(apart, $"P1 at ({text.Center.X}, {text.Center.Y}) over the beam ({x0}..{x1}, {y0}..{y1})");
            }
        }

        /// <summary>How wide the presenter inks a text 1 high, measured on a picture of it.</summary>
        private static double InkWidth(string text)
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(
                new DxfMText
                {
                    Location = new GeoPoint3(0, 0, 0),
                    TextHeight = 10,
                    Contents = text,
                    Attachment = DxfAttachmentPoint.MiddleCenter,
                }
            );
            CadDocument document = CadDocument.From(database, "ink.dxf");
            var cad = new CadView(2400, 200);
            cad.ZoomTo(new DisplayBox(-120, -10, 120, 10), 0.0);

            using (var bitmap = new SkiaSharp.SKBitmap(2400, 200))
            using (var canvas = new SkiaSharp.SKCanvas(bitmap))
            using (var renderer = new GeometryHelper.Dxf.Viewer.Wpf.SkiaCanvasRenderer())
            {
                renderer.Render(
                    canvas,
                    document,
                    document.ModelSpace,
                    cad,
                    new GeometryHelper.Dxf.Viewer.Wpf.CanvasOptions { Background = 0xFFFFFF }
                );
                canvas.Flush();
                int left = int.MaxValue,
                    right = -1;

                for (int y = 0; y < 200; y++)
                {
                    for (int x = 0; x < 2400; x++)
                    {
                        SkiaSharp.SKColor c = bitmap.GetPixel(x, y);

                        if (c.Red < 200 || c.Green < 200 || c.Blue < 200)
                        {
                            left = Math.Min(left, x);
                            right = Math.Max(right, x);
                        }
                    }
                }

                // Ten pixels to a unit of a text ten high: a hundred pixels to its height.
                return right < left ? 0.0 : (right - left) / 100.0;
            }
        }

        [Fact]
        public void TheDrawingsOfTheTestBodiesAreDrawnToPicturesToLookAt()
        {
            string folder = TestRun.Folder("integrate2");
            DrawingSettings tc2 = DrawingSettings.Part;
            tc2.Views.Details.Add(
                new DetailViewRequest("B", ViewKind.Front, new GeoRectangle2(2800, -200, 300, 400), 4)
            );
            var cases = new[]
            {
                ("tc1-part", DrawingBuilder.Build(TestBodies.TC1().ToSource(), DrawingSettings.Part, Tol)),
                ("tc2-part", DrawingBuilder.Build(TC2(), tc2, Tol)),
                ("tc5-assembly", DrawingBuilder.Build(TC5WithAWeld(), DrawingSettings.Assembly, Tol)),
                ("tc2-general", DrawingBuilder.Build(TC2WithAnElevation(), DrawingSettings.General, Tol)),
            };

            foreach ((string name, Drawing drawing) in cases)
            {
                _output.WriteLine(SavePicture(drawing.Database, DrawingDxfWriter.LayoutName, name + "-layout", folder));
                _output.WriteLine(SavePicture(drawing.Database, "Model", name + "-model", folder));
                drawing.Save(Path.Combine(folder, name + ".dxf"));
            }
        }
    }
}
