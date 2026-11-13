using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Annotative dimensions in the presenter, against AutoCAD 2021's (probes of the plugin GHAN4 in accoreconsole: a DIMLINEAR
    /// of the template's style Annotative, 0.18 high, at CANNOSCALE 1:50, given 1:100 by -OBJECTSCALE; the same of the style
    /// Standard made annotative from its Properties): a context per scale, each with its own picture block laid out at that
    /// scale, drawn at CANNOSCALE.
    /// </summary>
    public class AnnotativeDimensionTests
    {
        private static CadDocument Scaled(bool styleAnnotative = true)
        {
            CadDocument document = Blank();
            DxfObjectContextCollection scales = document.Database.ObjectContextManager.GetContextCollection(
                "ACDB_ANNOTATIONSCALES"
            );

            foreach (double d in new[] { 1.0, 50.0, 100.0 })
            {
                scales.AddContext(
                    new DxfAnnotationScale
                    {
                        Name = "1:" + d.ToString("0"),
                        PaperUnits = 1,
                        DrawingUnits = d,
                    }
                );
            }

            // The style Annotative of acad.dwt: Standard's variables, annotative, DIMSCALE 0.
            var style = new DxfDimStyleTableRecord { Name = "Annotative" };
            document.Database.DimStyleTable.Add(style);
            style.Dimscale = 0.0;

            if (styleAnnotative)
            {
                style.Annotative = DxfAnnotativeStates.True;
                document.Database.Dimstyle = "Annotative";
                document.Database.SetDimstyleData(style);
            }
            else
            {
                StandardCurrent(document.Database);
            }

            document.Database.Cannoscale = "1:50";
            return document;
        }

        private static DxfAnnotationScale Scale(CadDocument document, string name) =>
            (DxfAnnotationScale)
                document.Database.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES").GetContext(name);

        private static string[] Names(DxfEntity entity) => entity.GetContexts().Select(c => c.Name).ToArray();

        private static DxfDimension Linear(CadDocument document)
        {
            Run(Session(document), "DIMLINEAR", P(0, 0), P(100, 0), P(50, 30));
            return Assert.Single(All<DxfDimension>(document.ModelSpace));
        }

        private static DxfBlockTableRecord Second(CadDocument document, DxfDimension dimension) =>
            document.Database.BlockTable.Single(b =>
                b.Name.StartsWith("*D", StringComparison.Ordinal) && b.Name != dimension.DimBlock.Name
            );

        private static DxfMText Words(DxfBlockTableRecord picture) => picture.OfType<DxfMText>().Single();

        private static double[] Segments(DxfBlockTableRecord picture) =>
            picture
                .OfType<DxfLine>()
                .Where(l => Math.Abs(l.StartPoint.Y - 30.0) < 1E-9 && Math.Abs(l.EndPoint.Y - 30.0) < 1E-9)
                .SelectMany(l => new[] { l.StartPoint.X, l.EndPoint.X })
                .ToArray();

        [Fact]
        public void ADimensionDrawnByAnAnnotativeStyle_IsAnnotativeAtCannoscale_DrawnAtThatScale()
        {
            CadDocument document = Scaled();
            DxfDimension dimension = Linear(document);

            Assert.Equal(DxfAnnotativeStates.True, dimension.Annotative);
            Assert.Equal(new[] { "1:50" }, Names(dimension));
            Assert.NotNull(dimension.GetXDataForApplication("AcadAnnotative"));

            // AutoCAD 2021 at 1:50 in the style Annotative (0.18 high, an arrow 0.18 long): words 9 high at (50, 30), the
            // arrows 9 long, and no DIMSCALE written over the style's.
            DxfBlockTableRecord picture = dimension.DimBlock;
            Assert.Equal(9.0, Words(picture).TextHeight, 6);
            Assert.Equal(50.0, Words(picture).Location.X, 6);
            Assert.Equal(30.0, Words(picture).Location.Y, 6);
            Assert.Equal(0.0, dimension.GetDimstyleData().Dimscale, 9);
            Assert.Contains("ACDB_ALDIMOBJECTCONTEXTDATA_CLASS", document.Database.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void ADimensionDrawnByAnotherStyle_IsNotAnnotative_AsBefore()
        {
            CadDocument document = Scaled(false);
            DxfDimension dimension = Linear(document);

            Assert.Equal(DxfAnnotativeStates.False, dimension.Annotative);
            Assert.Empty(dimension.GetContexts());
        }

        [Fact]
        public void ADimensionGivenASecondScale_HasAPictureOfItsOwn_LaidOutAtThatScale()
        {
            CadDocument document = Scaled();
            DxfDimension dimension = Linear(document);
            double first = Words(dimension.DimBlock).TextHeight;
            DxfBlockTableRecord defaultPicture = dimension.DimBlock;
            int blocks = document.Database.BlockTable.Count();

            CadCommandSession session = Run(
                Session(document),
                "-OBJECTSCALE",
                new[] { dimension.Handle },
                Enter,
                "A",
                "1:100"
            );
            Assert.Equal(
                "1 object updated to support annotation scale <1:100>.",
                session.History[session.History.Count - 1]
            );
            Run(session, Enter);

            Assert.Equal(new[] { "1:50", "1:100" }, Names(dimension));
            Assert.Equal(blocks + 1, document.Database.BlockTable.Count());
            Assert.Same(defaultPicture, dimension.DimBlock);
            DxfBlockTableRecord second = Second(document, dimension);
            Assert.NotSame(defaultPicture, second);

            // AutoCAD 2021 at 1:100 (probe GHAN4): the words 18 high, the arrows 18 long inside the extension lines, the dimension
            // line running on 18 past the right one, and the words out beyond it, at (177.47, 30).
            Assert.Equal(first * 2.0, Words(second).TextHeight, 6);
            Assert.Equal(new[] { 18.0, 82.0, 100.0, 118.0 }, Segments(second).OrderBy(x => x).ToArray());
            Assert.True(Words(second).Location.X > 118.0);
            Assert.Equal(30.0, Words(second).Location.Y, 6);
            Assert.Equal(9.0, Words(defaultPicture).TextHeight, 6);
        }

        [Fact]
        public void ADimensionWhoseStyleIsNotAnnotative_MadeAnnotativeFromItsProperties_KeepsItsPicture_ItsSizesPaperSizes()
        {
            CadDocument document = Scaled(false);
            DxfDimension dimension = Linear(document);
            double height = Words(dimension.DimBlock).TextHeight;

            CadEditResult result = CadPropertyEditor.Set(document, new[] { dimension.Handle }, "Annotative", "Yes");

            Assert.True(result.Succeeded);
            Assert.Equal(DxfAnnotativeStates.True, dimension.Annotative);
            Assert.Equal(new[] { "1:50" }, Names(dimension));

            // AutoCAD 2021 (GHAN4, a DIMLINEAR of Standard, 0.18 high, set annotative at 1:50): DIMSCALE 0 and each size over
            // the scale as an override (DIMTXT 0.0036), the picture as it was.
            DxfDimStyleTableRecord data = dimension.GetDimstyleData();
            Assert.Equal(0.0, data.Dimscale, 9);
            Assert.Equal(0.18 / 50.0, data.Dimtxt, 9);
            Assert.Equal(0.18 / 50.0, data.Dimasz, 9);
            Assert.Equal(0.09 / 50.0, data.Dimgap, 9);
            Assert.Equal(height, Words(dimension.DimBlock).TextHeight, 9);
        }

        [Fact]
        public void TheScaleTakenOffTheDefault_MakesTheNextTheDefault_AndTheOtherPictureGoes()
        {
            CadDocument document = Scaled();
            DxfDimension dimension = Linear(document);
            Run(Session(document), "-OBJECTSCALE", new[] { dimension.Handle }, Enter, "A", "1:100", Enter);
            DxfBlockTableRecord second = Second(document, dimension);
            string firstName = dimension.DimBlock.Name;
            string secondName = second.Name;

            Run(Session(document), "-OBJECTSCALE", new[] { dimension.Handle }, Enter, "D", "1:50", Enter);

            // As AutoCAD 2021 rewrote it: the record's block is that of 1:100, its words where they stand there.
            Assert.Equal(new[] { "1:100" }, Names(dimension));
            Assert.Equal(secondName, dimension.DimBlock.Name);
            Assert.False(document.Database.BlockTable.Has(firstName));
            Assert.True(dimension.TextPosition.X > 118.0);
        }

        [Fact]
        public void AMultileaderDrawnByAnAnnotativeStyle_IsAnnotativeAtCannoscale_AndGivenASecondScaleLaidOutAgain()
        {
            CadDocument document = BlankWithMLeaderStyle();
            DxfObjectContextCollection scales = document.Database.ObjectContextManager.GetContextCollection(
                "ACDB_ANNOTATIONSCALES"
            );

            foreach (double d in new[] { 1.0, 50.0, 100.0 })
            {
                scales.AddContext(
                    new DxfAnnotationScale
                    {
                        Name = "1:" + d.ToString("0"),
                        PaperUnits = 1,
                        DrawingUnits = d,
                    }
                );
            }

            StyleOf(document).Annotative = DxfAnnotativeStates.True;
            document.Database.Cannoscale = "1:50";
            Run(Session(document), "MLD", P(0, 0), P(30, 20), "B-12");
            DxfMLeader mleader = Assert.Single(All<DxfMLeader>(document.ModelSpace));

            Assert.Equal(DxfAnnotativeStates.True, mleader.Annotative);
            Assert.True(mleader.EnableAnnotationScale);
            Assert.Equal(new[] { "1:50" }, Names(mleader));
            Assert.Equal(50.0, mleader.Scale, 9);
            double height = mleader.TextHeight;

            Run(Session(document), "-OBJECTSCALE", new[] { mleader.Handle }, Enter, "A", "1:100", Enter);

            // AutoCAD 2021 (GHAN4, text 0.2 high at 1:50): the second context is laid out again, its sizes doubled.
            Assert.Equal(new[] { "1:50", "1:100" }, Names(mleader));
            Assert.Equal(50.0, mleader.Scale, 9);
            Assert.Equal(height, mleader.TextHeight, 9);
            Assert.Contains(
                "ACDB_MLEADEROBJECTCONTEXTDATA_CLASS",
                document.Database.ToText(),
                StringComparison.Ordinal
            );

            // Drawn at CANNOSCALE by the context data of that scale: the words twice as high at 1:100.
            double Drawn()
            {
                DisplayList2 list = new DisplayBuilder(document.Database).Build(document.Database.ModelSpace, true);
                return list.Items.OfType<DisplayText2>().Select(t => t.Height).DefaultIfEmpty(0.0).Max();
            }

            document.Database.Cannoscale = "1:50";
            double at50 = Drawn();
            document.Database.Cannoscale = "1:100";
            Assert.True(at50 > 0.0);
            Assert.Equal(at50 * 2.0, Drawn(), 6);
        }

        private static DxfMLeaderStyle StyleOf(CadDocument document) =>
            CadMLeaderStyleEditor.Find(document.Database, CadMLeaderStyleEditor.Current(document.Database));

        [Fact]
        public void AnInsertOfAnAnnotativeBlock_IsDrawnAtCannoscale_ItsAttributesMovedOutFromItsPoint()
        {
            CadDocument document = Scaled(false);
            DxfDatabase db = document.Database;
            var block = new DxfBlockTableRecord { Name = "ANBLK" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            block.Annotative = DxfAnnotativeStates.True;
            var insert = new DxfBlockReference(new GeoPoint3(300, 1000, 0), block);
            db.ModelSpace.AppendEntity(insert);
            insert.AddContext(Scale(document, "1:50"));
            insert.AddContext(Scale(document, "1:100"));
            insert.AttributeCollection.AppendAttribute(
                new DxfAttributeReference
                {
                    Tag = "T",
                    TextString = "V",
                    Position = new GeoPoint3(300, 1003, 0),
                    Height = 2.0,
                }
            );

            DisplayList2 Drawn(string scale)
            {
                db.Cannoscale = scale;
                return new DisplayBuilder(db).Build(db.ModelSpace, true);
            }

            // At 1:50, its default: the factors of its record (50), the attribute where it stands.
            DisplayList2 list = Drawn("1:50");
            DisplayText2 text = list.Items.OfType<DisplayText2>().Single();
            Assert.Equal(1003.0, text.Y, 6);
            Assert.Equal(2.0, text.Height, 6);
            double width50 = list.Items.Where(i => !(i is DisplayText2)).Max(i => i.Bounds.MaxX - i.Bounds.MinX);

            // At 1:100: twice the size about the point, as AutoCAD 2021 lays it out (factors 100 for 50), the attribute out twice as far.
            list = Drawn("1:100");
            text = list.Items.OfType<DisplayText2>().Single();
            Assert.Equal(1006.0, text.Y, 6);
            Assert.Equal(300.0, text.X, 6);
            Assert.Equal(4.0, text.Height, 6);
            Assert.Equal(
                width50 * 2.0,
                list.Items.Where(i => !(i is DisplayText2)).Max(i => i.Bounds.MaxX - i.Bounds.MinX),
                6
            );
        }

        [Fact]
        public void ChangingCannoscaleWithAnnoautoscaleOn_ShowsTheObjectsAtTheNewScale_ByTheLayersTheValueTakes()
        {
            foreach (int value in new[] { -4, 1, 2, 4 })
            {
                CadDocument document = Scaled(false);
                DxfDatabase db = document.Database;
                db.LayerTable.Add(new DxfLayerTableRecord { Name = "LOCKED" });
                db.LayerTable.Add(new DxfLayerTableRecord { Name = "OFF" });
                db.LayerTable["LOCKED"].IsLocked = true;
                db.LayerTable["OFF"].IsOff = true;
                var texts = new List<DxfDBText>();

                foreach (string layer in new[] { "0", "LOCKED", "OFF" })
                {
                    var text = new DxfDBText
                    {
                        Position = new GeoPoint3(1, 1, 0),
                        TextString = layer,
                        Height = 125.0,
                        Layer = layer,
                    };
                    db.ModelSpace.AppendEntity(text);
                    text.Annotative = DxfAnnotativeStates.True;
                    texts.Add(text);
                }

                document.AnnoAutoScale = value;
                CadCommandSession session = Session(document);
                Run(session, "CANNOSCALE");
                Run(session, "1:100");

                // AutoCAD 2021 (GHAN4, three texts at 1:50, CANNOSCALE changed to 1:100): 1 adds the scale to the text on a layer
                // that is on and not locked, 2 to the one on a locked layer too, 4 to the one on a layer that is off too; -4 to none.
                int given =
                    value < 0 ? 0
                    : value == 1 ? 1
                    : value == 2 ? 2
                    : 3;
                Assert.Equal(
                    new[] { 0, 1, 2 }.Select(i => texts[i].HasContext(Scale(document, "1:100"))).Count(b => b),
                    given
                );
                Assert.Equal("1:100", db.Cannoscale);
                Assert.Equal(value > 0, texts[0].HasContext(Scale(document, "1:100")));
                Assert.Equal(value >= 2, texts[1].HasContext(Scale(document, "1:100")));
                Assert.Equal(value >= 4, texts[2].HasContext(Scale(document, "1:100")));
            }
        }

        [Fact]
        public void HatchEdit_ReadsAndWritesAnnotative_TheDialogsBoxMakingTheHatchAnnotativeAtTheCurrentScale()
        {
            CadDocument document = Scaled(false);
            DxfDatabase db = document.Database;
            foreach (
                GeoPoint3[] side in new[]
                {
                    new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0) },
                    new[] { new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0) },
                    new[] { new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0) },
                    new[] { new GeoPoint3(0, 100, 0), new GeoPoint3(0, 0, 0) },
                }
            )
            {
                db.ModelSpace.AppendEntity(new DxfLine(side[0], side[1]));
            }

            Run(Session(document), "-HATCH", P(50, 50), Enter);
            DxfHatch hatch = Assert.Single(All<DxfHatch>(document.ModelSpace));
            double plain = hatch.GetPatternDefinitionAt(0).OffsetY;
            Assert.False(CadHatchSettings.Of(hatch).Annotative);

            void Edit(Action<CadHatchSettings> change)
            {
                CadCommandSession session = Session(document);
                session.UiRequested += (s, e) =>
                {
                    if (e.Name == "HATCHEDIT")
                    {
                        change(e.Document.HatchBeingEdited);
                        e.Handled = true;
                    }
                };
                Run(session, "HATCHEDIT", new[] { hatch.Handle });
            }

            // The box ticked at 1:50: annotative at that scale, the pattern lines 50 times as far apart.
            Edit(settings => settings.Annotative = true);
            Assert.Equal(DxfAnnotativeStates.True, hatch.Annotative);
            Assert.Equal(new[] { "1:50" }, Names(hatch));
            Assert.Equal(plain * 50.0, hatch.GetPatternDefinitionAt(0).OffsetY, 6);
            Assert.True(CadHatchSettings.Of(hatch).Annotative);

            // A scale it has been given stays when its pattern is laid again.
            Run(Session(document), "-OBJECTSCALE", new[] { hatch.Handle }, Enter, "A", "1:100", Enter);
            Edit(settings => settings.Scale = 2.0);
            Assert.Equal(new[] { "1:50", "1:100" }, Names(hatch));
            Assert.Equal(plain * 2.0 * 50.0, hatch.GetPatternDefinitionAt(0).OffsetY, 6);

            // The box cleared: plain again, its lines as the pattern lays them.
            Edit(settings => settings.Annotative = false);
            Assert.Equal(DxfAnnotativeStates.False, hatch.Annotative);
            Assert.Equal(plain * 2.0, hatch.GetPatternDefinitionAt(0).OffsetY, 6);
        }

        [Fact]
        public void TheDimensionStyleManagersAnnotativeBox_WritesTheStylesFlag_AndTheNextDimensionIsAnnotative()
        {
            CadDocument document = Scaled(false);
            DxfDimStyleTableRecord standard = document.Database.DimStyleTable["Standard"];
            Assert.Equal(DxfAnnotativeStates.False, standard.Annotative);

            CadEditResult result = CadDimStyleEditor.Apply(
                document,
                "Standard",
                new Dictionary<string, object> { [CadDimStyleEditor.AnnotativeKey] = true }
            );

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(DxfAnnotativeStates.True, standard.Annotative);
            Assert.NotNull(standard.GetXDataForApplication("AcadAnnotative"));
            DxfDimension dimension = Linear(document);
            Assert.Equal(DxfAnnotativeStates.True, dimension.Annotative);
            Assert.Equal(new[] { "1:50" }, Names(dimension));
            Assert.Equal(9.0, Words(dimension.DimBlock).TextHeight, 6);

            // A style started from this one is annotative too, as AutoCAD's New copies the box.
            result = CadDimStyleEditor.Add(document, "Copy", "Standard", null);
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(DxfAnnotativeStates.True, document.Database.DimStyleTable["Copy"].Annotative);

            result = CadDimStyleEditor.Apply(
                document,
                "Copy",
                new Dictionary<string, object> { [CadDimStyleEditor.AnnotativeKey] = false }
            );
            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(DxfAnnotativeStates.False, document.Database.DimStyleTable["Copy"].Annotative);
        }

        [Fact]
        public void ADimensionDrawnAgainAfterItsStyleChanged_HasEveryPictureLaidOutAtItsOwnScale()
        {
            CadDocument document = Scaled();
            DxfDimension dimension = Linear(document);
            Run(Session(document), "-OBJECTSCALE", new[] { dimension.Handle }, Enter, "A", "1:100", Enter);

            CadEditResult result = CadDimStyleEditor.Apply(
                document,
                "Annotative",
                new Dictionary<string, object> { ["Dimtxt"] = 0.36 }
            );

            Assert.True(result.Succeeded, result.Message);
            Assert.Equal(0.36 * 50.0, Words(dimension.DimBlock).TextHeight, 6);
            Assert.Equal(0.36 * 100.0, Words(Second(document, dimension)).TextHeight, 6);
            Assert.Equal(new[] { "1:50", "1:100" }, Names(dimension));
        }

        [Fact]
        public void AnAnnotativeDimensionMoved_TakesThePictureOfEveryScaleWithIt()
        {
            CadDocument document = Scaled();
            DxfDimension dimension = Linear(document);
            Run(Session(document), "-OBJECTSCALE", new[] { dimension.Handle }, Enter, "A", "1:100", Enter);
            DxfMText before = Words(Second(document, dimension));
            GeoPoint3 was = before.Location;

            Run(Session(document), "MOVE", new[] { dimension.Handle }, Enter, P(0, 0), P(1000, 500));

            DxfMText moved = Words(Second(document, dimension));
            Assert.Equal(was.X + 1000.0, moved.Location.X, 6);
            Assert.Equal(was.Y + 500.0, moved.Location.Y, 6);
            DxfMText first = Words(dimension.DimBlock);
            Assert.Equal(1050.0, first.Location.X, 6);
            Assert.Equal(530.0, first.Location.Y, 6);
            Assert.Equal(1050.0, dimension.TextPosition.X, 6);
        }

        [Fact]
        public void ACopyOfAnAnnotativeDimension_HasAPictureOfItsOwnForEachScale()
        {
            CadDocument document = Scaled();
            DxfDimension original = Linear(document);
            Run(Session(document), "-OBJECTSCALE", new[] { original.Handle }, Enter, "A", "1:100", Enter);
            DxfBlockTableRecord originalSecond = Second(document, original);
            double was = Words(originalSecond).Location.X;

            Run(Session(document), "COPY", new[] { original.Handle }, Enter, P(0, 0), P(1000, 0), Enter);

            List<DxfDimension> all = All<DxfDimension>(document.ModelSpace);
            Assert.Equal(2, all.Count);
            DxfDimension copy = all.Single(d => d.Handle != original.Handle);
            Assert.Equal(new[] { "1:50", "1:100" }, Names(copy));
            var names = new HashSet<string>(
                document
                    .Database.BlockTable.Where(b => b.Name.StartsWith("*D", StringComparison.Ordinal))
                    .Select(b => b.Name)
            );
            Assert.Equal(4, names.Count);

            // The original's second picture stays where it was; the copy's stands 1000 along.
            Assert.Equal(was, Words(originalSecond).Location.X, 6);
            DxfBlockTableRecord copySecond = document.Database.BlockTable.Single(b =>
                b.Name.StartsWith("*D", StringComparison.Ordinal)
                && b.Name != original.DimBlock.Name
                && b.Name != originalSecond.Name
                && b.Name != copy.DimBlock.Name
            );
            Assert.Equal(was + 1000.0, Words(copySecond).Location.X, 6);
        }

        [Fact]
        public void AnAnnotativeDimension_IsDrawnByThePictureOfCannoscale_AndNotAtAScaleItIsNotShownAt()
        {
            CadDocument document = Scaled();
            DxfDimension dimension = Linear(document);
            Run(Session(document), "-OBJECTSCALE", new[] { dimension.Handle }, Enter, "A", "1:100", Enter);

            double HeightDrawn()
            {
                DisplayList2 list = new DisplayBuilder(document.Database).Build(document.Database.ModelSpace, true);
                return list.Items.OfType<DisplayText2>().Select(t => t.Height).DefaultIfEmpty(0.0).Max();
            }

            document.Database.Cannoscale = "1:50";
            Assert.Equal(9.0, HeightDrawn(), 6);
            document.Database.Cannoscale = "1:100";
            Assert.Equal(18.0, HeightDrawn(), 6);
        }
    }
}
