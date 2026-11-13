using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// IMAGEADJUST and CTABLESTYLE of AutoCAD 2021's acad.pgp, each checked against what AutoCAD 2021's console asked and did
    /// to the same drawing: the prompts word for word, the value offered, what is refused and what is left as it is.
    /// </summary>
    public class PgpR32Tests
    {
        private const string Option = "Enter image option [Contrast/Fade/Brightness] <Brightness>:";

        /// <summary>
        /// A metric drawing with three images of 40 by 30 along y = 0 (each a handle of the list) and a line, a fourth image on
        /// the locked layer Locked, and a table style GH_SCHEDULE beside Standard.
        /// </summary>
        private static CadDocument WithImages(out List<string> images, out string locked)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Locked" });
            database.LayerTable["Locked"].IsLocked = true;
            DxfDBDictionary dictionary = DxfRasterImageDef.CreateImageDictionary(database);
            var definition = new DxfRasterImageDef("logo.png", new GeoVector2(40, 30));
            dictionary.SetAt("logo", definition);
            var made = new List<DxfRasterImage>();

            for (int i = 0; i < 4; i++)
            {
                var image = new DxfRasterImage(definition);
                image.SetOrientation(new GeoPoint3(i * 60, 0, 0), new GeoVector3(40, 0, 0), new GeoVector3(0, 30, 0));
                database.ModelSpace.AppendEntity(image);
                made.Add(image);
            }

            made[3].Layer = "Locked";
            CadDocument document = CadDocument.From(database, "images.dxf");
            images = made.Take(3).Select(i => i.Handle).ToList();
            locked = made[3].Handle;
            return document;
        }

        private static DxfRasterImage Image(CadDocument document, string handle) =>
            Entity<DxfRasterImage>(document, handle);

        [Fact]
        public void TheAliasesOfTheRoundThreeCommands_ReachTheirCommandsAndVariables()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            foreach (
                string[] pair in new[]
                {
                    new[] { "IAD", "IMAGEADJUST" },
                    new[] { "DDATTEXT", "ATTEXT" },
                    new[] { "CT", "CTABLESTYLE" },
                }
            )
            {
                Assert.Equal(pair[1], CadShortcuts.Default.Aliases[pair[0]]);
            }

            Assert.Equal("IMAGEADJUST", registry.Find("IAD")?.Name);
            Assert.Equal("-IMAGEADJUST", registry.Find("-IMAGEADJUST")?.Name);
            Assert.Equal("ATTEXT", registry.Find("DDATTEXT")?.Name);
            Assert.Equal("-ATTEXT", registry.Find("-ATTEXT")?.Name);
            Assert.Equal("CTABLESTYLE", registry.FindVariable("CT")?.Name);
        }

        #region IMAGEADJUST

        [Fact]
        public void AnImageAdjusted_AsksAutoCadsWords_TakesTheOptionByItsFirstLetter_AndIsOneUndoStep()
        {
            CadDocument document = WithImages(out List<string> images, out _);
            CadCommandSession session = Run(Session(document), "IAD");

            Assert.Equal("Select image(s):", session.PromptText);
            Run(session, new[] { images[0] }, Enter);
            Assert.Equal(Option, session.PromptText);
            Run(session, "B");
            Assert.Equal("Enter brightness value (0-100) <50>:", session.PromptText);
            Run(session, "70");

            Assert.False(session.IsRunning);
            Assert.Equal((byte)70, Image(document, images[0]).Brightness);
            Assert.Equal((byte)50, Image(document, images[0]).Contrast);
            Assert.Equal((byte)0, Image(document, images[0]).Fade);
            Assert.Equal((byte)50, Image(document, images[1]).Brightness);
            AssertReopens(document);

            Assert.True(document.Undo());
            Assert.Equal((byte)50, Image(document, images[0]).Brightness);
        }

        [Fact]
        public void ImageAdjust_TheDialogStartsAtTheImagesOwnValues_AndOkGivesAllThree_AsOneUndoStep()
        {
            CadDocument document = WithImages(out List<string> images, out string locked);
            CadCommandSession session = Session(document);
            Run(session, "-IMAGEADJUST", new[] { images[0] }, Enter, "B", "70");
            // What each dialog started at: count, brightness, contrast, fade.
            var asked = new List<int[]>();
            string file = "none";
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "IMAGEADJUST")
                {
                    var request = (CadImageAdjustRequest)e.Payload;
                    asked.Add(new[] { request.Count, request.Brightness, request.Contrast, request.Fade });
                    file = request.ImageFile;
                    request.Contrast = 30;
                    request.Fade = 25;
                    request.IsAccepted = asked.Count == 1;
                    e.Handled = true;
                }
            };

            Run(session, "IAD", new[] { images[0] }, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { 1, 70, 50, 0 }, asked[0]);
            Assert.Null(file);
            Assert.Equal((byte)70, Image(document, images[0]).Brightness);
            Assert.Equal((byte)30, Image(document, images[0]).Contrast);
            Assert.Equal((byte)25, Image(document, images[0]).Fade);
            Assert.Equal("IMAGEADJUST", document.NextUndo.Name);

            // Several images start at AutoCAD's 50, 50 and 0; Cancel changes nothing, and one locked is left.
            Run(session, "IMAGEADJUST", new[] { images[1], locked }, Enter);
            Assert.Equal(new[] { 2, 50, 50, 0 }, asked[1]);
            Assert.Equal((byte)50, Image(document, images[1]).Contrast);

            Assert.True(document.Undo());
            Assert.Equal((byte)50, Image(document, images[0]).Contrast);
            Assert.Equal((byte)70, Image(document, images[0]).Brightness);
        }

        [Fact]
        public void ContrastAndFade_AskTheirOwnWords_AndEnterOffersTheOneImagesOwnValue()
        {
            CadDocument document = WithImages(out List<string> images, out _);
            CadCommandSession session = Session(document);

            Run(session, "-IMAGEADJUST", new[] { images[0] }, Enter, "Contrast");
            Assert.Equal("Enter contrast value (0-100) <50>:", session.PromptText);
            Run(session, "20");
            Run(session, "-IMAGEADJUST", new[] { images[0] }, Enter, "F");
            Assert.Equal("Enter fade value (0-100) <0>:", session.PromptText);
            Run(session, "40");

            // The next time the values it offers are its own, and Enter keeps them: no step of undo is made.
            Run(session, "IMAGEADJUST", new[] { images[0] }, Enter, "C");
            Assert.Equal("Enter contrast value (0-100) <20>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal((byte)20, Image(document, images[0]).Contrast);
            Assert.Equal((byte)40, Image(document, images[0]).Fade);
            Assert.Equal("-IMAGEADJUST", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal((byte)0, Image(document, images[0]).Fade);
            Assert.Equal((byte)20, Image(document, images[0]).Contrast);
        }

        [Fact]
        public void EnterAtTheOptionTakesBrightness_AndEnterAtTheValueOfSeveralImagesGivesThemTheFactorysValue()
        {
            CadDocument document = WithImages(out List<string> images, out _);
            CadCommandSession session = Session(document);
            Run(session, "IMAGEADJUST", new[] { images[0] }, Enter, "B", "70");
            Run(session, "IMAGEADJUST", new[] { images[1] }, Enter, "Contrast", "20");

            // Two images selected: the value offered is the factory's whatever they hold, as AutoCAD 2021 offers it.
            Run(session, "IMAGEADJUST", new[] { images[1], images[0] }, Enter, Enter);
            Assert.Equal("Enter brightness value (0-100) <50>:", session.PromptText);
            Run(session, Enter);

            Assert.Equal((byte)50, Image(document, images[0]).Brightness);
            Assert.Equal((byte)50, Image(document, images[1]).Brightness);
            Assert.Equal((byte)20, Image(document, images[1]).Contrast);

            Run(session, "IMAGEADJUST", new[] { images[0], images[1] }, Enter, "F");
            Assert.Equal("Enter fade value (0-100) <0>:", session.PromptText);
        }

        [Fact]
        public void AValueThatIsNotAWholeNumberFrom0To100_IsRefusedAndAskedAgain_AsAutoCadAsksIt()
        {
            CadDocument document = WithImages(out List<string> images, out _);
            CadCommandSession session = Run(Session(document), "IMAGEADJUST", new[] { images[0] }, Enter, "C");

            foreach (string typed in new[] { "150", "abc", "-5", "30.5" })
            {
                Run(session, typed);
                Assert.Equal("Value must be an integer between 0 and 100.", session.History.Last());
                Assert.Equal("Enter contrast value (0-100) <50>:", session.PromptText);
            }

            Run(session, "20");
            Assert.False(session.IsRunning);
            Assert.Equal((byte)20, Image(document, images[0]).Contrast);
        }

        [Fact]
        public void ObjectsThatAreNoImage_AreNotCounted_AndNothingSelectedEndsTheCommand()
        {
            CadDocument document = WithImages(out List<string> images, out _);
            CadCommandSession session = Session(document);
            Run(session, "LINE", P(0, 100), P(10, 100), Enter);
            string line = All<DxfLine>(document.ModelSpace).Single().Handle;

            Run(session, "IMAGEADJUST", new[] { line }, Enter);
            Assert.False(session.IsRunning);

            // A line and an image: the image is adjusted, and the value it offers is its own.
            Run(session, "IMAGEADJUST", new[] { line, images[2] }, Enter, "B");
            Assert.Equal("Enter brightness value (0-100) <50>:", session.PromptText);
            Run(session, "33");
            Assert.Equal((byte)33, Image(document, images[2]).Brightness);
        }

        [Fact]
        public void AnImageOnALockedLayer_IsLeftAsItIs_AfterTheValueIsGiven()
        {
            CadDocument document = WithImages(out List<string> images, out string locked);
            CadCommandSession session = Run(
                Session(document),
                "IMAGEADJUST",
                new[] { locked, images[0] },
                Enter,
                "B",
                "70"
            );

            Assert.Contains("The image is on a locked layer.", session.History);
            Assert.Equal((byte)50, Image(document, locked).Brightness);
            Assert.Equal((byte)70, Image(document, images[0]).Brightness);

            session = Run(Session(document), "IMAGEADJUST", new[] { locked }, Enter, "B", "70");
            Assert.Contains("The image is on a locked layer.", session.History);
            Assert.Equal((byte)50, Image(document, locked).Brightness);
        }

        #endregion

        #region CTABLESTYLE

        private static CadDocument WithSchedule()
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric(), "metric.dxf");
            Assert.True(CadTableStyleEditor.AddStandard(document).Succeeded);
            Assert.True(CadTableStyleEditor.Add(document, "GH_SCHEDULE", null).Succeeded);
            return document;
        }

        [Fact]
        public void Ctablestyle_AsksAutoCadsWords_TakesAnyCase_AndIsOneUndoStep()
        {
            CadDocument document = WithSchedule();
            CadCommandSession session = Run(Session(document), "CTABLESTYLE");

            Assert.Equal("Enter new value for CTABLESTYLE <\"Standard\">:", session.PromptText);
            Run(session, "gh_schedule");

            Assert.False(session.IsRunning);
            Assert.Equal("GH_SCHEDULE", CadTableStyleEditor.Current(document.Database));
            AssertReopens(document);
            Assert.True(document.Undo());
            Assert.Equal("Standard", CadTableStyleEditor.Current(document.Database));

            // The alias and SETVAR reach the same variable.
            Run(session, "CT", "GH_SCHEDULE");
            Assert.Equal("GH_SCHEDULE", CadTableStyleEditor.Current(document.Database));
            Run(session, "SETVAR", "CTABLESTYLE", "Standard");
            Assert.Equal("Standard", CadTableStyleEditor.Current(document.Database));
        }

        [Fact]
        public void Ctablestyle_ANameOfNoStyle_IsRefusedAsAutoCadRefusesIt_AndNotAskedAgain()
        {
            CadDocument document = WithSchedule();
            CadCommandSession session = Run(Session(document), "CTABLESTYLE", "Nope");

            Assert.Equal("Cannot set CTABLESTYLE to that value.", session.History.Last());
            Assert.False(session.IsRunning);
            Assert.Equal("Standard", CadTableStyleEditor.Current(document.Database));

            Run(session, "SETVAR", "CTABLESTYLE", "Nope");
            Assert.Equal("Cannot set CTABLESTYLE to that value.", session.History.Last());
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void Ctablestyle_IsListedBySetvar_AsATextInTheDrawing()
        {
            CadDocument document = WithSchedule();
            CadSystemVariable variable = Session(document).Registry.FindVariable("CTABLESTYLE");

            Assert.NotNull(variable);
            Assert.Equal("\"Standard\"", variable.GetValue(Session(document)));
            Assert.True(variable.IsInDrawing);
        }

        #endregion
    }
}
