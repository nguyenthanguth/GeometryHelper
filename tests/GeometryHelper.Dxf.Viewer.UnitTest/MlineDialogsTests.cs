using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MLSTYLE and MLEDIT driven as the window's Multiline Style and Multiline Edit Tools dialogs answer them: the buttons of
    /// the style dialog acting at once in MLSTYLE's one undo step, Set Current taken on OK, and the tool picked asked as its
    /// typed option. With no window both keep their command-line form.
    /// </summary>
    public class MlineDialogsTests
    {
        private static readonly string Folder = TestRun.Folder("dialogs5");

        private static CadCommandSession Start(CadDocument document)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return session;
        }

        // An assertion failed inside the dialog's answer, which the session would only write as the command's failure.
        private Exception _fault;

        private void Answer<T>(CadCommandSession session, string name, Action<T> step)
        {
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == name)
                {
                    try
                    {
                        step((T)e.Payload);
                    }
                    catch (Exception fault)
                    {
                        _fault = _fault ?? fault;
                    }

                    e.Handled = true;
                }
            };
        }

        private CadCommandSession Ask(CadCommandSession session, params object[] inputs)
        {
            Run(session, inputs);

            if (_fault != null)
            {
                throw new InvalidOperationException("The dialog's answer failed.", _fault);
            }

            return session;
        }

        private static DxfMlineStyleSettings Wall(double half, string description)
        {
            var settings = new DxfMlineStyleSettings { Description = description, StartSquareCap = true };
            settings.Elements.Clear();
            settings.Elements.Add(
                new DxfMlineStyleElement(half, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), "BYLAYER")
            );
            settings.Elements.Add(
                new DxfMlineStyleElement(-half, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), "BYLAYER")
            );
            return settings;
        }

        [Fact]
        public void Mlstyle_TheDialogsButtonsActAtOnce_AndSetCurrentIsTakenOnOk_AllOneUndoStep()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);
            Answer<CadMlineStyleRequest>(session, "MLSTYLE", r =>
            {
                Assert.Equal(new[] { "STANDARD" }, r.Names);
                Assert.Equal("STANDARD", r.Current);
                Assert.Null(r.Add("Wall-240", Wall(120, "Brick wall 240")));
                Assert.Equal("Multiline style wall-240 already exists.", r.Add("wall-240", Wall(120, "")));
                Assert.Null(r.Add("Spare", Wall(50, "")));
                Assert.Equal(new[] { "SPARE", "STANDARD", "WALL-240" }, r.Names);
                Assert.Equal(120.0, r.SettingsOf("WALL-240").Elements[0].Offset);
                Assert.Null(r.Modify("WALL-240", Wall(150, "Brick wall 300")));
                Assert.Equal("Brick wall 300", r.SettingsOf("wall-240").Description);
                Assert.Equal("The STANDARD multiline style cannot be renamed.", r.Rename("STANDARD", "BASE"));
                Assert.Null(r.Rename("WALL-240", "Wall-300"));
                Assert.NotNull(r.WhyNotDeleted("STANDARD"));
                Assert.Null(r.Delete("SPARE"));
                Assert.Equal(new[] { "STANDARD", "WALL-300" }, r.Names);
                r.Current = "WALL-300";
                r.IsAccepted = true;
            });

            Ask(session, "MLSTYLE");

            Assert.Equal("WALL-300", ((DxfMlineStyle)document.Database.CmlstyleID).Name);
            Assert.Equal(150.0, ((DxfMlineStyle)document.Database.CmlstyleID).Elements[0].Offset);

            Run(session, "U");

            // The one step takes back the STANDARD the command gave the blank drawing too.
            Assert.Null(document.Database.MLStyleDictionary);
        }

        [Fact]
        public void Mlstyle_CancelKeepsWhatTheButtonsDid_ButNotTheStyleSetCurrent()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);
            Answer<CadMlineStyleRequest>(session, "MLSTYLE", r =>
            {
                Assert.Null(r.Add("WALL", Wall(100, "")));
                r.Current = "WALL";
            });

            Ask(session, "MLSTYLE");

            Assert.True(document.Database.MLStyleDictionary.Contains("WALL"));
            Assert.Equal("STANDARD", ((DxfMlineStyle)document.Database.CmlstyleID).Name);
        }

        [Fact]
        public void Mlstyle_ModifyOfAStyleAMultilineIsDrawnBy_TakesOnlyTheDescription()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);
            Run(session, "MLINE", P(0, 0), P(100, 0), Enter);
            Answer<CadMlineStyleRequest>(session, "MLSTYLE", r =>
            {
                Assert.True(r.IsLocked("STANDARD"));
                Assert.Null(r.Modify("STANDARD", Wall(300, "Drawn")));
                Assert.Equal("Drawn", r.SettingsOf("STANDARD").Description);
                Assert.Equal(0.5, r.SettingsOf("STANDARD").Elements[0].Offset);
            });

            Ask(session, "MLSTYLE");

            Assert.Equal("Drawn", ((DxfMlineStyle)document.Database.CmlstyleID).Description);
        }

        [Fact]
        public void Mlstyle_SaveThenLoad_ListsTheFilesStyles_AndAddsOne()
        {
            string file = Path.Combine(Folder, "walls-" + Guid.NewGuid().ToString("N") + ".mln");
            CadDocument source = Blank();
            CadCommandSession first = Start(source);
            Answer<CadMlineStyleRequest>(first, "MLSTYLE", r =>
            {
                Assert.Null(r.Add("WALL", Wall(100, "Wall 200")));
                Assert.Null(r.Save("WALL", file));
            });
            Ask(first, "MLSTYLE");

            CadDocument target = Blank();
            CadCommandSession second = Start(target);
            Answer<CadMlineStyleRequest>(second, "MLSTYLE", r =>
            {
                Assert.Equal(new[] { "WALL" }, r.NamesInFile(file, out string refusal));
                Assert.Null(refusal);
                Assert.Null(r.Load(file, "WALL"));
                Assert.Equal("Multiline style WALL already exists.", r.Load(file, "WALL"));
                r.NamesInFile(Path.Combine(Folder, "none.mln"), out refusal);
                Assert.StartsWith("Cannot find multiline style file", refusal);
            });
            Ask(second, "MLSTYLE");

            Assert.Equal("Wall 200", ((DxfMlineStyle)target.Database.MLStyleDictionary.GetAt("WALL")).Description);
        }

        [Fact]
        public void Mlstyle_WithNoWindow_AsksOnTheCommandLine()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Ask(session, "MLSTYLE");

            Assert.Contains("Current multiline style: STANDARD", session.History);
        }

        [Fact]
        public void Mledit_TheToolsAreAutoCadsTwelveInItsRowsOfFour()
        {
            Assert.Equal(
                new[] { "CC", "CT", "CJ", "CS", "OC", "OT", "AV", "CA", "MC", "MT", "DV", "WA" },
                CadMlineEditRequest.Tools.Select(t => t.Key)
            );
            Assert.Equal("Weld All", CadMlineEditRequest.Tools[11].Value);
        }

        [Fact]
        public void Mledit_TheToolPicked_IsAskedAsItsTypedOption_AndNoneEndsTheCommand()
        {
            CadDocument typed = Blank();
            CadCommandSession byKeyboard = Start(typed);
            Run(byKeyboard, "MLINE", P(0, 0), P(100, 0), Enter);
            Run(byKeyboard, "MLEDIT", "CA", P(30, 0), P(60, 0), Enter);

            CadDocument dialog = Blank();
            CadCommandSession byDialog = Start(dialog);
            Run(byDialog, "MLINE", P(0, 0), P(100, 0), Enter);
            string tool = "CA";
            Answer<CadMlineEditRequest>(byDialog, "MLEDIT", r => r.Tool = tool);
            Ask(byDialog, "MLEDIT", P(30, 0), P(60, 0), Enter);

            Run(byKeyboard, "EXPLODE", "ALL", Enter);
            Run(byDialog, "EXPLODE", "ALL", Enter);
            Assert.Equal(4, All<DxfLine>(typed.ModelSpace).Count);
            Assert.Equal(4, All<DxfLine>(dialog.ModelSpace).Count);

            tool = null;
            Ask(byDialog, "MLEDIT");

            Assert.False(byDialog.IsRunning);
        }
    }
}
