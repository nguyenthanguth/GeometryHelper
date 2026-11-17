using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// SETBYLAYER with the prompts and the words of AutoCAD 2021's own string table ("Select objects or [Settings]",
    /// "Change ByBlock to ByLayer? [Yes/No]", "Include blocks? [Yes/No]", "1 object modified, 2 objects did not need to be
    /// changed.", "Enter a property to toggle [Color/LType/LWeight/Transparency/Material/PStyle] &lt;done&gt;:"), and the
    /// variable SETBYLAYERMODE, 255 in AutoCAD 2021's console.
    /// </summary>
    public class SetByLayerTests
    {
        private static DxfLine Red(double y) =>
            new DxfLine
            {
                StartPoint = new GeoPoint3(0, y, 0),
                EndPoint = new GeoPoint3(100, y, 0),
                Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1),
                Linetype = "DASHED",
                LineWeight = DxfLineWeight.LineWeight050,
                Transparency = new DxfTransparency(128),
            };

        private static CadDocument Drawing(out string red, out string plain, out string byBlock)
        {
            CadDocument document = Blank();
            var dashed = new DxfLinetypeTableRecord { Name = "DASHED", NumDashes = 2 };
            dashed.SetDashLengthAt(0, 12.5);
            dashed.SetDashLengthAt(1, -6.25);
            document.Database.LinetypeTable.Add(dashed);
            DxfLine a = Red(0);
            var b = new DxfLine { StartPoint = new GeoPoint3(0, 20, 0), EndPoint = new GeoPoint3(100, 20, 0) };
            var c = new DxfLine
            {
                StartPoint = new GeoPoint3(0, 40, 0),
                EndPoint = new GeoPoint3(100, 40, 0),
                Color = DxfColor.ByBlock,
                Linetype = "ByBlock",
            };
            document.ApplyEdit("draw", null, s =>
            {
                s.Append(document.ModelSpace, a);
                s.Append(document.ModelSpace, b);
                s.Append(document.ModelSpace, c);
            });
            red = a.Handle;
            plain = b.Handle;
            byBlock = c.Handle;
            return document;
        }

        [Fact]
        public void TheRunAsksAutoCadsPrompts_AndSaysWhichObjectsWereModified()
        {
            CadDocument document = Drawing(out string red, out string plain, out _);
            CadCommandSession session = Session(document);

            Run(session, "SETBYLAYER");
            Assert.Contains(
                "Current active settings: Color Linetype Lineweight Transparency Material",
                session.History
            );
            Assert.Equal("Select objects or [Settings]:", session.PromptText);
            Run(session, new[] { red, plain }, Enter);
            Assert.Equal("Change ByBlock to ByLayer? [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Include blocks? [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);

            DxfLine line = Entity<DxfLine>(document, red);
            Assert.True(line.Color.IsByLayer);
            Assert.Equal("ByLayer", line.Linetype);
            Assert.Equal(DxfLineWeight.ByLayer, line.LineWeight);
            Assert.False(line.Transparency.IsByAlpha);
            Assert.Contains("1 object modified, 1 object did not need to be changed.", session.History);
            Assert.False(session.IsRunning);
            Assert.Equal("SETBYLAYER", document.NextUndo.Name);
        }

        [Fact]
        public void ByBlockIsLeftAlone_WhereItIsNotToBeChanged_AndChangedWhereItIs()
        {
            CadDocument document = Drawing(out _, out _, out string byBlock);
            CadCommandSession session = Session(document);

            Run(session, "SETBYLAYER", new[] { byBlock }, Enter, "N", "Y");
            Assert.Contains("All object properties already set to ByLayer; no objects modified.", session.History);
            Assert.True(Entity<DxfLine>(document, byBlock).Color.IsByBlock);

            Run(session, "SETBYLAYER", new[] { byBlock }, Enter);
            Assert.Equal("Change ByBlock to ByLayer? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y", Enter);
            Assert.True(Entity<DxfLine>(document, byBlock).Color.IsByLayer);
            Assert.Equal("ByLayer", Entity<DxfLine>(document, byBlock).Linetype);
            Assert.Contains("1 object modified.", session.History);
        }

        [Fact]
        public void IncludeBlocks_ChangesTheObjectsOfTheBlockDefinitionAndTheAttributes_AndNoLeavesThemAsTheyAre()
        {
            CadDocument document = Blank();
            DxfDatabase db = document.Database;
            var block = new DxfBlockTableRecord { Name = "GB", Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            block.AppendEntity(
                new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 10.0)
                {
                    Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3),
                }
            );
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block)
            {
                Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5),
            };
            db.ModelSpace.AppendEntity(insert);
            document = CadDocument.From(DxfDatabase.Parse(db.ToText()), "blocks.dxf");
            string handle = All<DxfBlockReference>(document.ModelSpace).Single().Handle;
            CadCommandSession session = Session(document);

            Run(session, "SETBYLAYER", new[] { handle }, Enter, "Y", "N");
            DxfBlockTableRecord kept = document.Database.BlockTable["GB"];
            Assert.Equal(3, kept.OfType<DxfCircle>().Single().Color.ColorIndex);
            Assert.True(Entity<DxfBlockReference>(document, handle).Color.IsByLayer);
            Assert.Contains("1 object modified.", session.History);

            // The answers are the variable's two last bits now: include blocks off.
            Run(session, "SETBYLAYER");
            Run(session, "ALL", Enter);
            Assert.Equal("Change ByBlock to ByLayer? [Yes/No] <Yes>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Include blocks? [Yes/No] <No>:", session.PromptText);
            Run(session, "Y");
            Assert.True(document.Database.BlockTable["GB"].OfType<DxfCircle>().Single().Color.IsByLayer);
            Assert.Contains("1 object modified, 1 object did not need to be changed.", session.History);
        }

        [Fact]
        public void Settings_TogglesOnePropertyAtATime_AndTheVariableKeepsIt()
        {
            CadDocument document = Drawing(out string red, out _, out _);
            CadCommandSession session = Session(document);

            Run(session, "SETBYLAYER", "S");
            Assert.Equal(
                "Enter a property to toggle [Color/LType/LWeight/Transparency/Material] <done>:",
                session.PromptText
            );
            Run(session, "C", "LW", Enter);
            Assert.Equal("Select objects or [Settings]:", session.PromptText);
            Assert.Contains("Current active settings: Linetype Transparency Material", session.History);

            Run(session, new[] { red }, Enter, Enter, Enter);

            DxfLine line = Entity<DxfLine>(document, red);
            Assert.Equal(1, line.Color.ColorIndex);
            Assert.Equal(DxfLineWeight.LineWeight050, line.LineWeight);
            Assert.Equal("ByLayer", line.Linetype);

            Run(session, "SETBYLAYERMODE");
            Assert.Equal("Enter new value for SETBYLAYERMODE <250>:", session.PromptText);
        }

        [Fact]
        public void PStyle_IsRefusedWhereThePlotStylesFollowTheColour_AndNothingToChangeIsSaid()
        {
            CadDocument document = Drawing(out string red, out _, out _);
            CadCommandSession session = Session(document);

            Run(session, "SETBYLAYER", "S", "PStyle");
            Assert.Contains("Plot style cannot be changed when PSTYLEMODE is 1.", session.History);

            Run(session, "C", "LT", "LW", "T", "M", Enter, new[] { red }, Enter, Enter, Enter);
            Assert.Contains("No properties selected to change.", session.History);
            Assert.Equal(1, Entity<DxfLine>(document, red).Color.ColorIndex);
        }

        [Fact]
        public void APlotStyleAnObjectNames_IsTakenOut_WhereTheDrawingHasNamedPlotStyles()
        {
            var db = new DxfDatabase("AC1027");
            db.SetHeader("$PSTYLEMODE", new DxfTypedValue(290, "0"));
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
            db.ModelSpace.AppendEntity(line);
            string style = db.PlotStyleNameDictionary.First().Value.Handle;
            string text = db.ToText();
            int at = text.IndexOf(
                "100\nAcDbLine\n",
                text.IndexOf("\n  5\n" + line.Handle + "\n", System.StringComparison.Ordinal),
                System.StringComparison.Ordinal
            );
            CadDocument document = CadDocument.From(
                DxfDatabase.Parse(text.Insert(at, "390\n" + style + "\n")),
                "plot.dxf"
            );
            CadCommandSession session = Session(document);

            Run(session, "SETBYLAYER");
            Assert.Contains("PlotStyle", session.History.Last(h => h.StartsWith("Current active settings")));
            Run(session, new[] { line.Handle }, Enter, Enter, Enter);

            Assert.Contains("1 object modified.", session.History);

            // The line, not the layer, which names it too.
            string written = document.Database.ToText();
            int start = written.IndexOf("\n  5\n" + line.Handle + "\n", System.StringComparison.Ordinal);
            string common = written.Substring(
                start,
                written.IndexOf("100\nAcDbLine\n", start, System.StringComparison.Ordinal) - start
            );
            Assert.DoesNotContain("390\n", common);
        }
    }
}
