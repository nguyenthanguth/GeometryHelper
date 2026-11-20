using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The dimension variables as system variables, typed alone as a drawing routine types them (AutoCAD 2021, run DD:
    /// DIMSCALE, DIMDLE, DIMBLK1 and the others each asking "Enter new value for NAME &lt;...&gt;:", then -DIMSTYLE Save keeping
    /// them as a style). They are the variables of the drawing's header that new dimensions take; the style a Save makes holds
    /// them, and AutoLISP's <c>getvar</c> and <c>setvar</c> reach them through the same variables.
    /// </summary>
    public sealed class DimVariableTests
    {
        // The 73 dimension variables of the library but DIMSCALE (which has always been a variable), by the names AutoCAD gives them.
        private static readonly string[] DimensionVariableNames =
        {
            "DIMPOST",
            "DIMAPOST",
            "DIMASZ",
            "DIMEXO",
            "DIMDLI",
            "DIMEXE",
            "DIMRND",
            "DIMDLE",
            "DIMTP",
            "DIMTM",
            "DIMFXL",
            "DIMJOGANG",
            "DIMTXT",
            "DIMCEN",
            "DIMTSZ",
            "DIMALTF",
            "DIMLFAC",
            "DIMTVP",
            "DIMTFAC",
            "DIMGAP",
            "DIMALTRND",
            "DIMTFILL",
            "DIMTFILLCLR",
            "DIMTOL",
            "DIMLIM",
            "DIMTIH",
            "DIMTOH",
            "DIMSE1",
            "DIMSE2",
            "DIMTAD",
            "DIMZIN",
            "DIMAZIN",
            "DIMARCSYM",
            "DIMALT",
            "DIMALTD",
            "DIMTOFL",
            "DIMSAH",
            "DIMTIX",
            "DIMSOXD",
            "DIMCLRD",
            "DIMCLRE",
            "DIMCLRT",
            "DIMADEC",
            "DIMDEC",
            "DIMTDEC",
            "DIMALTU",
            "DIMALTTD",
            "DIMAUNIT",
            "DIMFRAC",
            "DIMLUNIT",
            "DIMDSEP",
            "DIMTMOVE",
            "DIMJUST",
            "DIMSD1",
            "DIMSD2",
            "DIMTOLJ",
            "DIMTZIN",
            "DIMALTZ",
            "DIMALTTZ",
            "DIMUPT",
            "DIMATFIT",
            "DIMFXLON",
            "DIMTXTDIRECTION",
            "DIMTXSTY",
            "DIMLDRBLK",
            "DIMBLK",
            "DIMBLK1",
            "DIMBLK2",
            "DIMLTYPE",
            "DIMLTEX1",
            "DIMLTEX2",
            "DIMLWD",
            "DIMLWE",
        };

        private static CadDocument WithTextStyle()
        {
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Text Dim", FileName = "romans.shx" });
            return CadDocument.From(database, "dim.dxf");
        }

        private static CadCommandSession Start(CadDocument document)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return session;
        }

        [Fact]
        public void EveryDimensionVariableOfTheLibrary_IsASystemVariableUnderAutoCadsName()
        {
            CadCommandSession session = Start(Blank());

            foreach (string name in DimensionVariableNames)
            {
                CadSystemVariable variable = session.Registry.FindVariable(name);

                Assert.NotNull(variable);
                Assert.Equal(name, variable.Name);
                Assert.False(variable.IsReadOnly);
                Assert.True(variable.IsInDrawing);
            }

            // The fixed length and its switch, and the direction of the text, as AutoCAD names them.
            Assert.NotNull(session.Registry.FindVariable("DIMFXL"));
            Assert.NotNull(session.Registry.FindVariable("DIMFXLON"));
            Assert.NotNull(session.Registry.FindVariable("DIMTXTDIRECTION"));
            Assert.Null(session.Registry.FindVariable("DIMFXLEN"));
        }

        [Fact]
        public void TheVariablesTypedAsDdTypesThem_AskAsAutoCadAsks()
        {
            CadDocument document = WithTextStyle();
            CadCommandSession session = Start(document);

            Run(session, "DIMASZ");
            Assert.Equal("Enter new value for DIMASZ <2.5000>:", session.PromptText); // AutoCAD 2021 (dd): "DIMASZ <0.1800>", the metric drawing's own
            Run(session, "1", "DIMSAH");
            Assert.Equal("Enter new value for DIMSAH <OFF>:", session.PromptText); // AutoCAD 2021 (dd): <OFF>
            Run(session, "ON", "DIMBLK1");
            Assert.Equal("Enter new value for DIMBLK1, or . for default <\"\">:", session.PromptText); // AutoCAD 2021 (dd)
            Run(session, Enter, "DIMTXSTY");
            Assert.Equal("Enter new value for DIMTXSTY <\"Standard\">:", session.PromptText); // AutoCAD 2021 (dd)
            Run(session, "Text Dim", "DIMCLRT");
            Assert.Equal("Enter new value for DIMCLRT <0>:", session.PromptText); // AutoCAD 2021 (dd): <0>
            Run(session, "3", "DIMTOH");
            Assert.Equal("Enter new value for DIMTOH <OFF>:", session.PromptText); // AutoCAD 2021 (dd): <ON> in its imperial template, ISO-25 here has it off
            Run(session, "ON");
            Assert.False(session.IsRunning);

            Assert.Equal(1.0, document.Database.Dimasz);
            Assert.True(document.Database.Dimsah);
            Assert.Null(document.Database.Dimblk1);
            Assert.Equal("Text Dim", document.Database.Dimtxsty.Name);
            Assert.Equal(3, document.Database.Dimclrt.ColorIndex);
            Assert.True(document.Database.Dimtoh);
        }

        [Fact]
        public void TheSequenceOfDd_ThenDimstyleSave_KeepsEachVariableInTheStyleRecord()
        {
            CadDocument document = WithTextStyle();
            CadCommandSession session = Start(document);

            Run(
                session,
                "DIMSCALE",
                "1",
                "DIMDLE",
                "1",
                "DIMDLI",
                "1",
                "DIMEXE",
                "1",
                "DIMEXO",
                "1",
                "DIMBLK1",
                Enter,
                "DIMBLK2",
                Enter,
                "DIMASZ",
                "1",
                "DIMSAH",
                "ON",
                "DIMCEN",
                "1",
                "DIMTXSTY",
                "Text Dim",
                "DIMCLRT",
                "3",
                "DIMGAP",
                "1",
                "DIMDEC",
                "0",
                "DIMTOH",
                "ON",
                "DIMTIX",
                "ON"
            );

            // The header holds what was typed, and DIMBLK1 and DIMBLK2 left on Enter are as they were.
            DxfDatabase header = document.Database;
            Assert.Equal(1.0, header.Dimscale);
            Assert.Equal(1.0, header.Dimdle);
            Assert.Equal(1.0, header.Dimdli);
            Assert.Equal(1.0, header.Dimexe);
            Assert.Equal(1.0, header.Dimexo);
            Assert.Null(header.Dimblk1);
            Assert.Null(header.Dimblk2);
            Assert.Equal(1.0, header.Dimasz);
            Assert.True(header.Dimsah);
            Assert.Equal(1.0, header.Dimcen);
            Assert.Equal("Text Dim", header.Dimtxsty.Name);
            Assert.Equal(3, header.Dimclrt.ColorIndex);
            Assert.Equal(1.0, header.Dimgap);
            Assert.Equal(0, header.Dimdec);
            Assert.True(header.Dimtoh);
            Assert.True(header.Dimtix);

            Run(session, "-DIMSTYLE", "S", "1-1");

            DxfDimStyleTableRecord style = document.Database.DimStyleTable["1-1"];
            Assert.Equal("1-1", document.Database.Dimstyle);
            Assert.Equal(1.0, style.Dimscale);
            Assert.Equal(1.0, style.Dimdle);
            Assert.Equal(1.0, style.Dimdli);
            Assert.Equal(1.0, style.Dimexe);
            Assert.Equal(1.0, style.Dimexo);
            Assert.Equal(1.0, style.Dimasz);
            Assert.True(style.Dimsah);
            Assert.Equal(1.0, style.Dimcen);
            Assert.Equal("Text Dim", style.Dimtxsty.Name);
            Assert.Equal(3, style.Dimclrt.ColorIndex);
            Assert.Equal(1.0, style.Dimgap);
            Assert.Equal(0, style.Dimdec);
            Assert.True(style.Dimtoh);
            Assert.True(style.Dimtix);
            AssertReopens(document);
        }

        [Fact]
        public void DimblkNone_MakesTheNoneArrowheadBlock_AndAFullStopGivesTheClosedFilledOneBack()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Run(session, "DIMBLK1", "none"); // AutoCAD 2021 (dd): DIMBLK1 None, the block _None made
            Assert.Equal("_None", document.Database.Dimblk1.Name);
            Assert.True(document.Database.BlockTable.Has("_None"));

            Run(session, "DIMBLK1");
            Assert.Equal("Enter new value for DIMBLK1, or . for default <\"None\">:", session.PromptText);
            Run(session, ".");
            Assert.Null(document.Database.Dimblk1);

            Run(session, "DIMBLK2", "nosuchblock");
            Assert.Equal("Block \"nosuchblock\" not found.", session.History.Last());
            Assert.Null(document.Database.Dimblk2);
        }

        [Fact]
        public void ARefusedValue_IsWrittenInAutoCadsWordsAndAskedAgain()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            Run(session, "DIMDEC", "9");
            Assert.Equal("Requires an integer between 0 and 8.", session.History.Last());
            Assert.Equal("Enter new value for DIMDEC <" + document.Database.Dimdec + ">:", session.PromptText);
            Run(session, Esc, "DIMSAH", "2");
            Assert.Equal("Requires 0 or 1 only, or option keyword.", session.History.Last());
            Assert.False(document.Database.Dimsah);
            Run(session, Esc, "DIMTXSTY", "NoSuchStyle");
            Assert.Equal("Cannot find text style \"NoSuchStyle\".", session.History.Last());
            Assert.Equal("Standard", document.Database.Dimtxsty.Name);
        }

        [Fact]
        public void EachChange_IsOneStepOfUndo()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);
            double before = document.Database.Dimasz;

            Run(session, "DIMASZ", "7", "DIMSAH", "ON");
            Assert.Equal(7.0, document.Database.Dimasz);
            Assert.True(document.Database.Dimsah);

            Assert.True(document.Undo());
            Assert.False(document.Database.Dimsah);
            Assert.Equal(7.0, document.Database.Dimasz);
            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.Dimasz);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ASetFromLisp_TakesTheTypeOfEachKindOfVariable_AndRefusesWhatItCannotHold()
        {
            CadDocument document = WithTextStyle();
            CadCommandSession session = Start(document);
            DxfDatabase header = document.Database;

            bool Set(string name, object value)
            {
                bool taken = session.Registry.FindVariable(name).SetFromLisp(session, value);
                session.CloseGroup();
                return taken;
            }

            object Get(string name) => session.Registry.FindVariable(name).ValueForLisp(session);

            // A real; an angle in radians; a whole number within its range; a switch.
            Assert.True(Set("DIMASZ", 1.5));
            Assert.Equal(1.5, header.Dimasz);
            Assert.Equal(1.5, Get("DIMASZ"));
            Assert.False(Set("DIMASZ", -1.0));
            Assert.True(Set("DIMJOGANG", Math.PI / 6.0));
            Assert.Equal(Math.PI / 6.0, (double)Get("DIMJOGANG"), 9);
            Assert.True(Set("DIMDEC", 3));
            Assert.Equal(3, Get("DIMDEC"));
            Assert.False(Set("DIMDEC", 9));
            Assert.False(Set("DIMDEC", "3"));
            Assert.True(Set("DIMSAH", 1));
            Assert.Equal(1, Get("DIMSAH"));
            Assert.False(Set("DIMSAH", 2));

            // A colour by its index, a lineweight by its number.
            Assert.True(Set("DIMCLRT", 5));
            Assert.Equal(5, header.Dimclrt.ColorIndex);
            Assert.False(Set("DIMCLRT", 300));
            Assert.True(Set("DIMLWD", 25));
            Assert.Equal(25, Get("DIMLWD"));
            Assert.False(Set("DIMLWD", 26));

            // A text, one letter, an arrowhead and a text style, by name.
            Assert.True(Set("DIMPOST", "<> mm"));
            Assert.Equal("<> mm", Get("DIMPOST"));
            Assert.True(Set("DIMDSEP", ","));
            Assert.Equal(",", Get("DIMDSEP"));
            Assert.False(Set("DIMDSEP", ",,"));
            Assert.True(Set("DIMBLK", "ArchTick"));
            Assert.Equal("_ArchTick", header.Dimblk.Name);
            Assert.Equal("ArchTick", Get("DIMBLK"));
            Assert.True(Set("DIMBLK", "."));
            Assert.Null(header.Dimblk);
            Assert.Equal(string.Empty, Get("DIMBLK"));
            Assert.True(Set("DIMTXSTY", "text dim"));
            Assert.Equal("Text Dim", Get("DIMTXSTY"));
            Assert.False(Set("DIMTXSTY", "Missing"));
            Assert.False(Set("DIMTXSTY", 5));
        }

        [Fact]
        public void EveryDimensionVariable_TakesItsOwnValueBackFromLisp_AndChangesNothing()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document);

            foreach (string name in DimensionVariableNames)
            {
                CadSystemVariable variable = session.Registry.FindVariable(name);
                object value = variable.ValueForLisp(session);

                Assert.True(variable.SetFromLisp(session, value), name + " refused its own value " + value);
                session.CloseGroup();
            }

            Assert.False(document.CanUndo);
        }
    }
}
