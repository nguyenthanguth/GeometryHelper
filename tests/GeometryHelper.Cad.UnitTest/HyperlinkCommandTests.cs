using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The hyperlinks of objects as AutoCAD 2021 keeps and asks them: HYPERLINK's Insert Hyperlink and Edit Hyperlink dialog
    /// (a file or web page, a view of the drawing, an e-mail address; Remove Link), -HYPERLINK's questions in the words of
    /// AutoCAD's own table of strings (Remove, Insert by Object and by Area), the links as the extended data of PE_URL, and
    /// where a link leads.
    /// </summary>
    public class HyperlinkCommandTests
    {
        private static CadCommandSession WithDialog(CadDocument document, Action<CadHyperlinkRequest> answer)
        {
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "HYPERLINK")
                {
                    answer((CadHyperlinkRequest)e.Payload);
                    e.Handled = true;
                }
            };
            return session;
        }

        [Fact]
        public void Hyperlink_AsksTheObjects_ThenInsertHyperlinkWritesTheLinkAsPeUrl_OneUndoStep()
        {
            CadDocument document = Plan();
            CadHyperlinkRequest seen = null;
            CadCommandSession session = WithDialog(document, request =>
            {
                seen = request;
                request.Address = "https://example.com/B-12.pdf";
                request.TextToDisplay = "Plate B-12";
                request.Action = CadDialogAction.Ok;
            });

            Run(session, "HYPERLINK");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { "A1", "A2" }, Enter);

            Assert.False(session.IsRunning);
            Assert.False(seen.IsEdit);
            Assert.Equal("Insert Hyperlink", seen.Title);
            Assert.Equal(2, seen.ObjectCount);

            foreach (string handle in new[] { "A1", "A2" })
            {
                DxfHyperLink link = Assert.Single(document.FindEntity(handle).Hyperlinks);
                Assert.Equal("https://example.com/B-12.pdf", link.Name);
                Assert.Equal("Plate B-12", link.Description);
                Assert.Equal(string.Empty, link.SubLocation);
            }

            Assert.Equal("HYPERLINK", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Empty(document.FindEntity("A1").Hyperlinks);
            Assert.Empty(document.FindEntity("A2").Hyperlinks);
        }

        [Fact]
        public void TheLinkIsWrittenToTheFileAsAutoCadWritesIt_AndReadBack()
        {
            CadDocument document = Plan();
            CadCommandSession session = WithDialog(document, request =>
            {
                request.Address = "plans\\B-12.pdf";
                request.Location = "page2";
                request.TextToDisplay = "Plate";
                request.Action = CadDialogAction.Ok;
            });

            Run(session, "HYPERLINK", new[] { "A1" }, Enter);

            DxfDatabase again = DxfDatabase.Parse(document.Database.ToText());
            DxfEntity line = again.ModelSpace.Cast<DxfEntity>().First(e => e.Handle == "A1");
            DxfHyperLink read = Assert.Single(line.Hyperlinks);
            Assert.Equal("plans\\B-12.pdf", read.Name);
            Assert.Equal("page2", read.SubLocation);
            Assert.Equal("Plate", read.Description);
            Assert.Contains(line.GetXDataForApplication("PE_URL"), g =>
                g.TypeCode == 1000 && g.Value == "plans\\B-12.pdf"
            );
        }

        [Fact]
        public void AnObjectThatHasALink_IsEditedInEditHyperlink_OkReplacesItAndRemoveLinkTakesItOut()
        {
            CadDocument document = Plan();
            document.FindEntity("A1").Hyperlinks.Add(new DxfHyperLink { Name = "old.pdf", Description = "Old" });
            CadHyperlinkRequest seen = null;
            CadCommandSession session = WithDialog(document, request =>
            {
                seen = request;
                request.Address = "new.pdf";
                request.Action = CadDialogAction.Ok;
            });

            Run(session, "HYPERLINK", new[] { "A1" }, Enter);

            Assert.True(seen.IsEdit);
            Assert.Equal("Edit Hyperlink", seen.Title);
            DxfHyperLink link = Assert.Single(document.FindEntity("A1").Hyperlinks);
            Assert.Equal("new.pdf", link.Name);
            Assert.Equal("Old", link.Description);

            CadCommandSession remover = WithDialog(document, request =>
            {
                Assert.Equal("new.pdf", request.Address);
                request.Remove = true;
                request.Action = CadDialogAction.Ok;
            });
            Run(remover, "HYPERLINK", new[] { "A1" }, Enter);

            Assert.Empty(document.FindEntity("A1").Hyperlinks);
            Assert.Contains("1 hyperlink deleted.", remover.History);
        }

        [Fact]
        public void AViewOfThisDrawing_IsALinkWithNoAddressAndTheViewAsItsPlace_AndAMailLinkIsMailtoWithItsSubject()
        {
            var db = new DxfDatabase("AC1027");
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)));
            db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 5, 0), new GeoPoint3(10, 5, 0)));
            db.ViewTable.Add(new DxfViewTableRecord { Name = "ENTRANCE" });
            CadDocument document = CadDocument.From(db, "views.dxf");
            string l1 = All<DxfLine>(document.ModelSpace)[0].Handle;
            string l2 = All<DxfLine>(document.ModelSpace)[1].Handle;
            CadHyperlinkRequest seen = null;
            CadCommandSession session = WithDialog(document, request =>
            {
                seen = request;
                request.Kind = CadHyperlinkKind.ViewOfThisDrawing;
                request.ViewName = "ENTRANCE";
                request.TextToDisplay = "Entrance";
                request.Action = CadDialogAction.Ok;
            });
            Run(session, "HYPERLINK", new[] { l1 }, Enter);

            Assert.Equal(new[] { "ENTRANCE" }, seen.Views);
            DxfHyperLink view = Assert.Single(document.FindEntity(l1).Hyperlinks);
            Assert.Equal(string.Empty, view.Name);
            Assert.Equal("ENTRANCE", view.SubLocation);

            CadCommandSession mail = WithDialog(document, request =>
            {
                request.Kind = CadHyperlinkKind.EmailAddress;
                request.EmailAddress = "detailing@example.com";
                request.Subject = "Plate B-12";
                request.Action = CadDialogAction.Ok;
            });
            Run(mail, "HYPERLINK", new[] { l2 }, Enter);

            Assert.Equal(
                "mailto:detailing@example.com?subject=Plate B-12",
                Assert.Single(document.FindEntity(l2).Hyperlinks).Name
            );

            // The dialog opened on a link reads its kind back.
            CadHyperlinkRequest again = null;
            Run(WithDialog(document, request =>
                {
                    again = request;
                    request.Action = CadDialogAction.Cancel;
                }), "HYPERLINK", new[] { l2 }, Enter);
            Assert.Equal(CadHyperlinkKind.EmailAddress, again.Kind);
            Assert.Equal("detailing@example.com", again.EmailAddress);
            Assert.Equal("Plate B-12", again.Subject);
            CadHyperlinkRequest viewAgain = null;
            Run(WithDialog(document, request =>
                {
                    viewAgain = request;
                    request.Action = CadDialogAction.Cancel;
                }), "HYPERLINK", new[] { l1 }, Enter);
            Assert.Equal(CadHyperlinkKind.ViewOfThisDrawing, viewAgain.Kind);
            Assert.Equal("ENTRANCE", viewAgain.ViewName);
        }

        [Fact]
        public void AnAddressTextOrPlaceOver255Characters_IsRefusedInAutoCadsWords_AndNothingIsWritten()
        {
            CadDocument document = Plan();
            string refusal = null;
            CadCommandSession session = WithDialog(document, request =>
            {
                request.Address = new string('a', 256);
                Assert.False(request.TryBuild(out _, out refusal));
                request.Address = "ok.pdf";
                request.TextToDisplay = new string('t', 256);
                Assert.False(request.TryBuild(out _, out string text));
                Assert.StartsWith("The text you entered in \"Text to display\" field", text);
                request.TextToDisplay = string.Empty;
                request.Location = new string('p', 256);
                Assert.False(request.TryBuild(out _, out string place));
                Assert.StartsWith("The location you specified", place);
                request.Address = string.Empty;
                Assert.False(request.TryBuild(out _, out string none));
                Assert.Equal("Type the file or web page name.", none);
                request.Action = CadDialogAction.Cancel;
            });

            Run(session, "HYPERLINK", new[] { "A1" }, Enter);

            Assert.StartsWith("The URL you entered contains too many characters.", refusal);
            Assert.Empty(document.FindEntity("A1").Hyperlinks);
        }

        [Fact]
        public void Hyperlink_WithNoWindow_PointsToTheTypedForm_AndNothingSelectedIsSaid()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "HYPERLINK", new[] { "A1" }, Enter);
            Assert.Contains("-HYPERLINK asks its questions on the command line", string.Join("|", session.History));

            Run(session, "HYPERLINK", Enter);
            Assert.Contains("Nothing selected.", session.History);
        }

        [Fact]
        public void AnObjectOnALockedLayer_IsLeftOut_AsAnyCommandDoesIt()
        {
            CadDocument document = Plan();
            CadCommandSession session = WithDialog(document, request => request.Action = CadDialogAction.Cancel);

            Run(session, "HYPERLINK", new[] { "A1", "A8" }, Enter);

            Assert.Contains("1 was on a locked layer.", session.History);
        }

        [Fact]
        public void TypedHyperlink_Insert_AsksInAutoCadsWords_AndAttachesTheLink()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "-HYPERLINK");
            Assert.Equal("Enter an option [Remove/Insert] <Insert>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter hyperlink insert option [Area/Object] <Object>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { "A1" }, Enter);
            Assert.Equal("Enter hyperlink <current drawing>:", session.PromptText);
            Run(session, "https://example.com/B-12.pdf");
            Assert.Equal("Enter named location <none>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter description <none>:", session.PromptText);
            Run(session, "Plate B-12");

            Assert.False(session.IsRunning);
            DxfHyperLink link = Assert.Single(document.FindEntity("A1").Hyperlinks);
            Assert.Equal("https://example.com/B-12.pdf", link.Name);
            Assert.Equal("Plate B-12", link.Description);
            Assert.Equal(string.Empty, link.SubLocation);
            Assert.True(document.Undo());
            Assert.Empty(document.FindEntity("A1").Hyperlinks);
        }

        [Fact]
        public void TypedHyperlink_AnObjectWithALink_AsksToOverwrite_NoAddsAnother_AndTheCurrentDrawingNeedsAPlace()
        {
            CadDocument document = Plan();
            document.FindEntity("A1").Hyperlinks.Add(new DxfHyperLink { Name = "first.pdf" });
            CadCommandSession session = Session(document);

            Run(session, "-HYPERLINK", Enter, Enter, new[] { "A1" }, Enter);
            Assert.Equal("Overwrite existing hyperlinks? [Yes/No] <Yes>:", session.PromptText);
            Run(session, "No", "second.pdf", Enter, Enter);

            Assert.Equal(new[] { "first.pdf", "second.pdf" }, document.FindEntity("A1").Hyperlinks.Select(l => l.Name));

            // The current drawing with no place named links to nothing.
            Run(session, "-HYPERLINK", Enter, Enter, new[] { "A2" }, Enter, Enter, Enter, Enter);
            Assert.Contains("No hyperlinks added.", session.History);
            Assert.Empty(document.FindEntity("A2").Hyperlinks);

            // A place alone is a link to a view of the drawing.
            Run(session, "-HYPERLINK", Enter, Enter, new[] { "A2" }, Enter, Enter, "ENTRANCE", Enter);
            DxfHyperLink view = Assert.Single(document.FindEntity("A2").Hyperlinks);
            Assert.Equal(string.Empty, view.Name);
            Assert.Equal("ENTRANCE", view.SubLocation);
        }

        [Fact]
        public void TypedHyperlink_Remove_TakesEveryLinkOutOfSeveralObjects_AndSaysHowMany()
        {
            CadDocument document = Plan();
            document.FindEntity("A1").Hyperlinks.Add(new DxfHyperLink { Name = "a.pdf" });
            document.FindEntity("A2").Hyperlinks.Add(new DxfHyperLink { Name = "b.pdf" });
            CadCommandSession session = Session(document);

            Run(session, "-HYPERLINK", "Remove");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { "A1", "A2", "A3" }, Enter);

            Assert.False(session.IsRunning);
            Assert.Empty(document.FindEntity("A1").Hyperlinks);
            Assert.Empty(document.FindEntity("A2").Hyperlinks);
            Assert.Contains("2 hyperlinks deleted.", session.History);

            Run(session, "-HYPERLINK", "Remove", new[] { "A3" }, Enter);
            Assert.Contains("No hyperlink to remove.", session.History);
        }

        [Fact]
        public void TypedHyperlink_Remove_OfOneObjectWithSeveralLinks_AsksWhichByNumberOrAll()
        {
            CadDocument document = Plan();
            DxfHyperLinkCollection links = document.FindEntity("A1").Hyperlinks;
            links.Add(new DxfHyperLink { Name = "a.pdf" });
            links.Add(new DxfHyperLink { Name = "b.pdf" });
            links.Add(new DxfHyperLink { Name = "c.pdf" });
            CadCommandSession session = Session(document);

            Run(session, "-HYPERLINK", "Remove", new[] { "A1" }, Enter);
            Assert.Equal("Enter number, hyperlink, or * for all:", session.PromptText);
            Run(session, "2");

            Assert.Equal(new[] { "a.pdf", "c.pdf" }, document.FindEntity("A1").Hyperlinks.Select(l => l.Name));
            Assert.Contains("1 hyperlink deleted.", session.History);

            Run(session, "-HYPERLINK", "Remove", new[] { "A1" }, Enter, "c.pdf");
            Assert.Equal(new[] { "a.pdf" }, document.FindEntity("A1").Hyperlinks.Select(l => l.Name));

            document.FindEntity("A1").Hyperlinks.Add(new DxfHyperLink { Name = "d.pdf" });
            Run(session, "-HYPERLINK", "Remove", new[] { "A1" }, Enter, "*");
            Assert.Empty(document.FindEntity("A1").Hyperlinks);
            Assert.Contains("2 hyperlinks deleted.", session.History);
        }

        [Fact]
        public void TypedHyperlink_Area_DrawsARectangleOnUrllayerCarryingTheLink()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);

            Run(session, "-HYPERLINK", Enter, "Area");
            Assert.Equal("First corner:", session.PromptText);
            Run(session, P(10, 10));
            Assert.Equal("Other corner:", session.PromptText);
            Run(session, P(60, 40), "https://example.com", Enter, "Area link");

            Assert.False(session.IsRunning);
            DxfPolyline area = All<DxfPolyline>(document.ModelSpace).Last();
            Assert.Equal("URLLAYER", area.Layer);
            Assert.True(area.Closed);
            Assert.Equal(4, area.NumberOfVertices);
            Near(P(10, 10), area.GetPoint2dAt(0));
            Near(P(60, 10), area.GetPoint2dAt(1));
            Near(P(60, 40), area.GetPoint2dAt(2));
            Near(P(10, 40), area.GetPoint2dAt(3));
            DxfHyperLink link = Assert.Single(area.Hyperlinks);
            Assert.Equal("https://example.com", link.Name);
            Assert.Equal("Area link", link.Description);
            Assert.True(document.Database.LayerTable.Has("URLLAYER"));

            Assert.True(document.Undo());
            Assert.DoesNotContain(All<DxfPolyline>(document.ModelSpace), p => p.Layer == "URLLAYER");
        }

        [Fact]
        public void TypedHyperlink_Area_OnAFrozenOrLockedUrllayer_SaysSoAndDrawsNothing()
        {
            CadDocument document = Plan();
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "URLLAYER", IsFrozen = true });
            CadCommandSession session = Session(document);

            Run(session, "-HYPERLINK", Enter, "Area");

            Assert.False(session.IsRunning);
            Assert.Contains("URLLAYER is frozen.  Thaw URLLAYER to use Insert by Area.", session.History);

            document.Database.LayerTable["URLLAYER"].IsFrozen = false;
            document.Database.LayerTable["URLLAYER"].IsLocked = true;
            Run(session, "-HYPERLINK", Enter, "Area");
            Assert.Contains("URLLAYER is locked. Unlock URLLAYER to use Insert by Area.", session.History);
        }

        [Theory]
        [InlineData("https://example.com/a.pdf", CadHyperlinkTargetKind.Web, "https://example.com/a.pdf")]
        [InlineData("www.example.com", CadHyperlinkTargetKind.Web, "http://www.example.com")]
        [InlineData("ftp://host/file.zip", CadHyperlinkTargetKind.Web, "ftp://host/file.zip")]
        [InlineData(
            "mailto:me@example.com?subject=Hi",
            CadHyperlinkTargetKind.Mail,
            "mailto:me@example.com?subject=Hi"
        )]
        public void AnAddress_IsToldAWebAddressOrAMailAddressFromAFile(
            string address,
            CadHyperlinkTargetKind kind,
            string expected
        )
        {
            CadHyperlinkTarget target = CadHyperlinks.Resolve(Plan(), new DxfHyperLink { Name = address });

            Assert.Equal(kind, target.Kind);
            Assert.Equal(expected, target.Address);
        }

        [Fact]
        public void AFileLink_IsMadeFromTheHyperlinkBase_OrTheFolderOfTheDrawing_AndADriveIsNotAScheme()
        {
            CadDocument document = Plan();
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gk-links");

            document.Database.HyperlinkBase = folder;
            CadHyperlinkTarget relative = CadHyperlinks.Resolve(document, new DxfHyperLink { Name = "plans\\a.pdf" });
            Assert.Equal(CadHyperlinkTargetKind.File, relative.Kind);
            Assert.Equal(System.IO.Path.GetFullPath(System.IO.Path.Combine(folder, "plans\\a.pdf")), relative.Address);

            CadHyperlinkTarget drive = CadHyperlinks.Resolve(document, new DxfHyperLink { Name = "C:\\plans\\a.pdf" });
            Assert.Equal(CadHyperlinkTargetKind.File, drive.Kind);
            Assert.Equal("C:\\plans\\a.pdf", drive.Address);

            document.Database.HyperlinkBase = "https://example.com/plans/";
            CadHyperlinkTarget onWeb = CadHyperlinks.Resolve(document, new DxfHyperLink { Name = "a.pdf" });
            Assert.Equal("https://example.com/plans/a.pdf", onWeb.Address);
        }

        [Fact]
        public void ALinkWithNoAddressNamesAViewOrNothing()
        {
            CadDocument document = Plan();

            CadHyperlinkTarget view = CadHyperlinks.Resolve(document, new DxfHyperLink { SubLocation = "ENTRANCE" });
            Assert.Equal(CadHyperlinkTargetKind.View, view.Kind);
            Assert.Equal("ENTRANCE", view.View);
            Assert.Equal(CadHyperlinkTargetKind.None, CadHyperlinks.Resolve(document, new DxfHyperLink()).Kind);
            Assert.Throws<ArgumentNullException>(() => CadHyperlinks.Resolve(document, null));
        }

        [Fact]
        public void TheTooltip_IsTheLinksTextThenCtrlClickToFollowLink_AndNullForAnObjectWithNone()
        {
            CadDocument document = Plan();
            DxfEntity line = document.FindEntity("A1");
            Assert.Null(CadHyperlinks.Tooltip(line));

            line.Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com/B-12.pdf", Description = "Plate B-12" });
            Assert.Equal("Plate B-12\nCTRL + click to follow link", CadHyperlinks.Tooltip(line));

            line.Hyperlinks.Clear();
            line.Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com/B-12.pdf" });
            Assert.Equal("https://example.com/B-12.pdf\nCTRL + click to follow link", CadHyperlinks.Tooltip(line));
            Assert.Null(CadHyperlinks.Tooltip(null));
        }

        [Fact]
        public void TheLinksAreRegisteredCommands()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();

            Assert.Equal("HYPERLINK", registry.Find("HYPERLINK").Name);
            Assert.Equal("-HYPERLINK", registry.Find("-HYPERLINK").Name);
            Assert.Equal("DWGPROPS", registry.Find("DWGPROPS").Name);
            Assert.Equal("GROUPEDIT", registry.Find("GROUPEDIT").Name);
            Assert.Equal("ATTREDEF", registry.Find("ATTREDEF").Name);
        }
    }
}
