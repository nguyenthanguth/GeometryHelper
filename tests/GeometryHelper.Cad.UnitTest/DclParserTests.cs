using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Dcl;
using Xunit;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The DCL parser (L5 part P, D301-D309): the grammar, the values, comments, prototypes, includes, the errors of D307 and
    /// the predefined tiles of D306. The language is written from the AutoCAD 2021 DCL reference (rows marked
    /// <c>reference</c>); where the reference is silent the spec's own choice is marked <c>ours: D3nn</c>. The console
    /// measured nothing of the language (it cannot open a dialog); the load messages and the refusal of a predefined name
    /// are measured in the full AutoCAD (rows marked <c>AutoCAD 2021 (GUI g1)</c>, D338).
    /// </summary>
    public class DclParserTests
    {
        private const string Path = @"C:\dcl\test.dcl";

        private static DclFile Parse(string text, IDictionary<string, string> files = null, List<string> asked = null)
        {
            return DclParser.Parse(text, Path, (name, from) =>
            {
                asked?.Add(name + "|" + from);
                return files != null && files.TryGetValue(name, out string content)
                    ? new KeyValuePair<string, string>(@"C:\dcl\" + name, content)
                    : (KeyValuePair<string, string>?)null;
            });
        }

        private static DclException Fail(string text, IDictionary<string, string> files = null)
        {
            return Assert.Throws<DclException>(() => Parse(text, files));
        }

        private static DclTile Dialog(string inner, string head = "") =>
            Parse(head + "\nd : dialog {\n" + inner + "\n}").NewDialog("d");

        // ---- Grammar (D301) ----

        [Fact]
        public void AnEmptyFile_HasOnlyThePredefinedTiles()
        {
            // ours: D306 (the predefined set is in every file)
            DclFile file = Parse("");

            Assert.Equal(Path, file.Path);
            Assert.True(file.Definitions.ContainsKey("ok_cancel"));
            Assert.All(file.Definitions.Values, tile => Assert.NotEqual("dialog", tile.Kind));
        }

        [Fact]
        public void ADefinitionOfAParentWithAttributes_IsReadIntoATileOfThatKind()
        {
            // reference: a definition is "name : parent { attributes }"
            DclFile file = Parse("hello : dialog { label = \"Greetings\"; initial_focus = \"name\"; }");

            DclTile dialog = file.Definitions["hello"];
            Assert.Equal("dialog", dialog.Kind);
            Assert.Equal("hello", dialog.Name);
            Assert.Equal("Greetings", dialog.Get("label"));
            Assert.Equal("name", dialog.Get("initial_focus"));
            Assert.Equal(Path, dialog.File);
            Assert.Equal(1, dialog.Line);
        }

        [Fact]
        public void AttributeValues_KeepTheirKindAndTheirTextAsWritten()
        {
            // reference: values are strings, integers, reals and names (D302)
            DclTile tile = Dialog(
                ": edit_box { key = \"k\"; edit_width = 60; width = -1; aspect_ratio = 1.5; height = .5; fixed_width = true; alignment = centered; }"
            );

            DclTile box = tile.Children.Single();
            Assert.Equal(DclValueKind.String, box.Attributes["key"].Kind);
            Assert.Equal("k", box.Attributes["key"].Text);
            Assert.Equal(DclValueKind.Integer, box.Attributes["edit_width"].Kind);
            Assert.Equal("60", box.Attributes["edit_width"].Text);
            Assert.Equal(DclValueKind.Integer, box.Attributes["width"].Kind);
            Assert.Equal("-1", box.Attributes["width"].Text);
            Assert.Equal(DclValueKind.Real, box.Attributes["aspect_ratio"].Kind);
            Assert.Equal("1.5", box.Attributes["aspect_ratio"].Text);
            Assert.Equal(DclValueKind.Real, box.Attributes["height"].Kind);
            Assert.Equal(".5", box.Attributes["height"].Text);
            Assert.Equal(DclValueKind.Name, box.Attributes["fixed_width"].Kind);
            Assert.Equal("true", box.Attributes["fixed_width"].Text);
            Assert.Equal(DclValueKind.Name, box.Attributes["alignment"].Kind);
            Assert.Equal("centered", box.Attributes["alignment"].Text);
            Assert.Equal(60, box.GetInteger("edit_width"));
            Assert.Equal(1.5, box.GetReal("aspect_ratio"));
            Assert.True(box.IsTrue("fixed_width"));
        }

        [Fact]
        public void AttributeLines_AreTheLinesTheyAreWrittenOn()
        {
            DclTile tile = Dialog("\n: text {\n\n label = \"a\";\n value = \"b\";\n}");

            DclTile text = tile.Children.Single();
            Assert.Equal(4, text.Line);
            Assert.Equal(6, text.Attributes["label"].Line);
            Assert.Equal(7, text.Attributes["value"].Line);
        }

        [Fact]
        public void TrueAndFalse_AreComparedIgnoringCase()
        {
            // ours: D302
            DclTile dialog = Dialog(": toggle { a = TRUE; b = False; c = tRuE; d = \"true\"; e = yes; }")
                .Children.Single();

            Assert.True(dialog.IsTrue("a"));
            Assert.False(dialog.IsTrue("b"));
            Assert.True(dialog.IsTrue("c"));
            Assert.True(dialog.IsTrue("d"));
            Assert.False(dialog.IsTrue("e"));
            Assert.False(dialog.IsTrue("missing"));
        }

        [Fact]
        public void StringEscapes_AreResolved()
        {
            // ours: D302 (quote, backslash, newline and tab; any other \x keeps the x)
            DclTile text = Dialog(": text { label = \"say \\\"hi\\\" \\\\ one\\ntwo\\tthree \\q\"; }")
                .Children.Single();

            Assert.Equal("say \"hi\" \\ one\ntwo\tthree q", text.Get("label"));
        }

        [Fact]
        public void ABackslashAtTheEndOfAString_IsKeptWhenItIsEscaped()
        {
            DclTile text = Dialog(": text { label = \"folder\\\\\"; }").Children.Single();

            Assert.Equal("folder\\", text.Get("label"));
        }

        [Fact]
        public void CommentsOfBothKinds_AreSkippedAndLinesAreStillCounted()
        {
            // reference: // to the end of the line and /* ... */ are comments (D301)
            const string text =
                "// a comment with \"quotes\" and { braces }\n"
                + "/* a block\n   over lines : x = 1; */\n"
                + "d : dialog { // after the brace\n"
                + "  label = /* inside */ \"L\";\n"
                + "}\n";

            DclFile file = Parse(text);

            Assert.Equal("L", file.Definitions["d"].Get("label"));
            Assert.Equal(4, file.Definitions["d"].Line);
            Assert.Equal(5, file.Definitions["d"].Attributes["label"].Line);
        }

        [Fact]
        public void WhiteSpaceIsFree_IncludingCarriageReturnsAndOneLine()
        {
            // reference: white space, CR and LF between tokens (D301)
            DclFile crlf = Parse("d:dialog{\r\nlabel=\"A\";\r\n:text{label=\"B\";}\r\n}");
            DclFile oneLine = Parse("d : dialog { label = \"A\" ; : text { label = \"B\" ; } }");

            foreach (DclFile file in new[] { crlf, oneLine })
            {
                DclTile d = file.Definitions["d"];
                Assert.Equal("A", d.Get("label"));
                Assert.Equal("B", d.Children.Single().Get("label"));
            }
        }

        [Fact]
        public void AByteOrderMarkAtTheStart_IsWhiteSpace()
        {
            DclFile file = Parse("\uFEFFd : dialog { label = \"A\"; }");

            Assert.Equal("A", file.Definitions["d"].Get("label"));
        }

        [Fact]
        public void TheSemicolonAfterABrace_IsOptional()
        {
            // reference: a closing brace may be followed by a semicolon (D301)
            DclFile file = Parse(
                "a : dialog { : row { : text { label = \"x\"; }; : text { label = \"y\"; } }; };\n"
                    + "b : dialog { : row { : text { label = \"x\"; } : text { label = \"y\"; } } }\n"
            );

            Assert.Equal(2, file.Definitions["a"].Children.Single().Children.Count);
            Assert.Equal(2, file.Definitions["b"].Children.Single().Children.Count);
        }

        [Fact]
        public void ABareName_IsATileOfThatDefinitionWithNothingChanged()
        {
            // reference: "ok_cancel;" puts the standard row in the dialog (D301)
            DclTile dialog = Dialog("ok_cancel;");

            DclTile row = dialog.Children.Single();
            Assert.Equal("ok_cancel", row.Name);
            Assert.Equal("cluster", row.Kind);
            Assert.Equal(3, row.Children.Count(child => child.Kind == "button" || child.Kind == "spacer"));
        }

        [Fact]
        public void AnInnerTile_HasTheNameItWasWrittenAs()
        {
            DclTile dialog = Dialog(": edit_box { key = \"a\"; } : ok_button { } : row { }");

            Assert.Equal(
                new[] { "edit_box", "ok_button", "row" },
                dialog.Children.Select(child => child.Name).ToArray()
            );
            Assert.Equal(
                new[] { "edit_box", "button", "cluster" },
                dialog.Children.Select(child => child.Kind).ToArray()
            );
        }

        [Fact]
        public void ChildrenKnowTheirParent_AndTheTopHasNone()
        {
            DclTile dialog = Dialog(": row { : text { } }");

            DclTile row = dialog.Children.Single();
            Assert.Null(dialog.Parent);
            Assert.Same(dialog, row.Parent);
            Assert.Same(row, row.Children.Single().Parent);
        }

        [Fact]
        public void TheSameAttributeTwiceInATile_TakesTheLastValue()
        {
            // ours: D301 (not said; the last one wins as in a derived tile)
            DclTile text = Dialog(": text { label = \"a\"; label = \"b\"; }").Children.Single();

            Assert.Equal("b", text.Get("label"));
        }

        // ---- Case (D303) ----

        [Fact]
        public void TileNames_AreCaseSensitive()
        {
            // reference: DCL is case-sensitive
            DclException error = Fail("d : dialog { Ok_Cancel; }");

            Assert.Equal("Tile \"Ok_Cancel\" is not defined.", error.What);
            Assert.True(Parse("Dialog : dialog { }").Definitions.ContainsKey("Dialog"));
            Assert.False(Parse("D : dialog { }").Definitions.ContainsKey("d"));
        }

        [Fact]
        public void AttributeNames_AreCaseSensitive()
        {
            DclTile text = Dialog(": text { Label = \"a\"; }").Children.Single();

            Assert.Null(text.Get("label"));
            Assert.Equal("a", text.Get("Label"));
        }

        [Fact]
        public void KeysAreCaseSensitive_AndTheFirstTileWithAKeyIsFound()
        {
            // reference: DCL is case-sensitive; ours: D308 (the first tile with a key, depth-first)
            DclTile dialog = Dialog(
                ": column { : text { key = \"Text\"; label = \"first\"; } } : text { key = \"Text\"; label = \"second\"; } : text { key = \"text\"; }"
            );

            Assert.Equal("first", dialog.Find("Text").Get("label"));
            Assert.NotSame(dialog.Find("Text"), dialog.Find("text"));
            Assert.Null(dialog.Find("TEXT"));
        }

        // ---- Syntax errors (D307) ----

        [Fact]
        public void ANameBeginningWithADigit_IsASyntaxError()
        {
            // ours: D301; AutoCAD 2021 (GUI g1): the message is "syntax error." and the Symbol on a line of its own
            DclException error = Fail("1d : dialog { }");

            Assert.Equal("1d", error.Symbol);
            Assert.Equal("syntax error.\nSymbol: \"1d\".", error.What);
            Assert.Equal(1, error.Line);
        }

        [Theory]
        [InlineData("d : dialog {\n  label = \"a\";\n  key = ;\n}", ";", 3)]
        [InlineData("d : dialog {\n  label \"a\";\n}", "\"a\"", 2)]
        [InlineData("d : dialog { label = a b; }", "b", 1)]
        [InlineData("d : dialog { ) }", ")", 1)]
        [InlineData("d : dialog { # }", "#", 1)]
        [InlineData("d : dialog { width = 10px; }", "10px", 1)]
        [InlineData("d : dialog { width = 1.2.3; }", "1.2.3", 1)]
        [InlineData("d : dialog { width = -; }", "-", 1)]
        [InlineData("d : { }", "{", 1)]
        [InlineData("d dialog { }", "dialog", 1)]
        [InlineData("d : dialog ; ", ";", 1)]
        [InlineData("\n\n= 5;", "=", 3)]
        [InlineData("d : dialog { }\n}", "}", 2)]
        [InlineData("@inc \"x\";", "@inc", 1)]
        [InlineData("@include x;", "x", 1)]
        [InlineData("@include \"x\" d : dialog { }", "d", 1)]
        public void ASyntaxError_NamesTheSymbolAndTheLine(string text, string symbol, int line)
        {
            // AutoCAD 2021 (GUI g1): "Error in dialog file", the path and line, "syntax error." and the Symbol, a line each
            // (the symbols and lines of these rows are ours: D307)
            DclException error = Fail(text);

            Assert.Equal(symbol, error.Symbol);
            Assert.Equal(line, error.Line);
            Assert.Equal("syntax error.\nSymbol: \"" + symbol + "\".", error.What);
            Assert.Equal(Path, error.File);
            Assert.False(error.Semantic);
            Assert.Equal(
                "Error in dialog file\n\""
                    + Path
                    + "\", line "
                    + line
                    + ":\nsyntax error.\nSymbol: \""
                    + symbol
                    + "\".",
                error.Message
            );
        }

        [Theory]
        [InlineData("d : dialog {\n  label = \"a\"\n  : button { key = \"b\"; }\n}", 3)]
        [InlineData("d : dialog { label = \"a\" }", 1)]
        [InlineData("d : dialog {\n  ok_cancel\n}", 3)]
        [InlineData("d : dialog {\n  ok_cancel\n  : text { }\n}", 3)]
        [InlineData("d : dialog { a : b { } }", 1)]
        [InlineData("@include \"x\"\n: row { }", 2)]
        public void ATileOrAClosingBraceWhereASemicolonIsDue_IsAMissingSemicolon(string text, int line)
        {
            // AutoCAD 2021 (GUI g1): "missing semicolon." on the line of the next entry, no Symbol line (a label with no ";"
            // before a ": button" on line 3 gave line 3); a "}" in its place is the same by the lead's reading (g1 gave only the tile)
            DclException error = Fail(text);

            Assert.Equal("missing semicolon.", error.What);
            Assert.Null(error.Symbol);
            Assert.Equal(line, error.Line);
            Assert.False(error.Semantic);
            Assert.Equal(
                "Error in dialog file\n\"" + Path + "\", line " + line + ":\nmissing semicolon.",
                error.Message
            );
        }

        [Theory]
        [InlineData("d : dialog { label = \"a\" key = \"k\"; }", "key")]
        [InlineData("d : dialog { ok_cancel width; }", "width")]
        [InlineData("d : dialog { label = \"a\" 5; }", "5")]
        public void AnythingElseWhereASemicolonIsDue_IsASyntaxErrorAtThatToken(string text, string symbol)
        {
            // ours: D338 (only a tile or a "}" is read as a missing semicolon; anything else names its token)
            DclException error = Fail(text);

            Assert.Equal(symbol, error.Symbol);
            Assert.Equal("syntax error.\nSymbol: \"" + symbol + "\".", error.What);
        }

        [Theory]
        [InlineData("d : dialog {", 1)]
        [InlineData("d : dialog {\n  label = \"a\";\n", 2)]
        [InlineData("d : dialog {\n  : row {\n}\n", 3)]
        [InlineData("d : dialog { }\n/* never closed\n\n", 3)]
        [InlineData("d : dialog { label = \"a\"; /* open", 1)]
        [InlineData("d :", 1)]
        [InlineData("d", 1)]
        public void TheEndOfTheText_InsideATile_IsEndOfFile(string text, int line)
        {
            // ours: D307 ("end of file" is the symbol; the line is the last the text has)
            DclException error = Fail(text);

            Assert.Equal("end of file", error.Symbol);
            Assert.Equal("syntax error.\nSymbol: \"end of file\".", error.What);
            Assert.Equal(line, error.Line);
        }

        [Fact]
        public void AStringLeftOpenAtTheEndOfItsLine_IsASyntaxErrorOnThatLine()
        {
            // ours: D301 (a string does not run over a line break)
            DclException error = Fail("d : dialog {\n label = \"not closed;\n key = \"k\";\n}");

            Assert.Equal(2, error.Line);
            Assert.Equal("\"not closed;", error.Symbol);
        }

        [Fact]
        public void AFileWithASyntaxError_GivesNoFile()
        {
            Assert.Throws<DclException>(() => Parse("good : dialog { }\nbad : dialog { label = ; }"));
        }

        // ---- Prototypes (D304) ----

        [Fact]
        public void ATileOwnAttributes_LieOverTheParents()
        {
            // ours: D304
            DclFile file = Parse(
                "base1 : button { label = \"A\"; key = \"k\"; width = 10; }\n"
                    + "derived : base1 { label = \"B\"; height = 2; }\n"
            );

            DclTile derived = file.Definitions["derived"];
            Assert.Equal("button", derived.Kind);
            Assert.Equal("derived", derived.Name);
            Assert.Equal("B", derived.Get("label"));
            Assert.Equal("k", derived.Get("key"));
            Assert.Equal(10, derived.GetInteger("width"));
            Assert.Equal(2, derived.GetInteger("height"));
            Assert.Equal("A", file.Definitions["base1"].Get("label"));
        }

        [Fact]
        public void AChainOfThreeDefinitions_IsFollowedToThePrimitive()
        {
            DclFile file = Parse(
                "a : edit_box { edit_width = 5; label = \"a\"; }\nb : a { label = \"b\"; }\nc : b { key = \"c\"; }\n"
            );

            DclTile c = file.Definitions["c"];
            Assert.Equal("edit_box", c.Kind);
            Assert.Equal("5", c.Get("edit_width"));
            Assert.Equal("b", c.Get("label"));
            Assert.Equal("c", c.Key);
        }

        [Fact]
        public void ADefinitionMayNameAParentThatComesLaterInTheFile()
        {
            // ours: D304 (the chain is followed when the file is loaded, so the order of definitions does not matter)
            DclFile file = Parse("first : second { key = \"f\"; }\nsecond : toggle { label = \"s\"; }\n");

            Assert.Equal("toggle", file.Definitions["first"].Kind);
            Assert.Equal("s", file.Definitions["first"].Get("label"));
        }

        [Fact]
        public void TheChildrenOfAParent_ComeBeforeTheOnesTheDerivedTileLists()
        {
            // ours: D304
            DclFile file = Parse(
                "pair : row { : text { key = \"p1\"; } : text { key = \"p2\"; } }\n"
                    + "triple : pair { : text { key = \"p3\"; } }\n"
            );

            Assert.Equal(
                new[] { "p1", "p2", "p3" },
                file.Definitions["triple"].Children.Select(child => child.Key).ToArray()
            );
            Assert.Equal(2, file.Definitions["pair"].Children.Count);
        }

        [Fact]
        public void AnInnerTileOfADerivedDefinition_TakesThatDefinitionsAttributesAndChildren()
        {
            DclTile dialog = Dialog(
                ": mybox { label = \"over\"; }",
                "mybox : boxed_column { label = \"under\"; key = \"b\"; : text { key = \"t\"; } }"
            );

            DclTile box = dialog.Children.Single();
            Assert.Equal("mybox", box.Name);
            Assert.Equal("cluster", box.Kind);
            Assert.Equal("over", box.Get("label"));
            Assert.Equal("b", box.Key);
            Assert.True(box.IsTrue("boxed"));
            Assert.Equal("t", box.Children.Single().Key);
        }

        [Fact]
        public void ADefinitionOfADefinitionWithChildren_CopiesThemSoTheTwoShareNothing()
        {
            DclFile file = Parse("pair : row { : text { key = \"p1\"; } }\nother : pair { }\n");

            DclTile first = file.Definitions["pair"].Children.Single();
            DclTile second = file.Definitions["other"].Children.Single();

            Assert.NotSame(first, second);
            Assert.Same(file.Definitions["other"], second.Parent);
            Assert.Same(file.Definitions["pair"], first.Parent);
        }

        [Fact]
        public void TheLastOfTwoDefinitionsOfOneName_Wins()
        {
            // ours: D304 (DclFile: later ones win)
            DclFile file = Parse("x : button { label = \"1\"; }\nx : button { label = \"2\"; }\n");

            Assert.Equal("2", file.Definitions["x"].Get("label"));
        }

        [Theory]
        [InlineData("ok_button", "ok_button : button { label = \"Yes\"; key = \"accept\"; is_default = true; }")]
        [InlineData("row", "row : cluster { layout = horizontal; }")]
        [InlineData("ok_cancel", "ok_cancel : row { ok_button; }")]
        [InlineData("spacer_1", "spacer_1 : spacer { }")]
        [InlineData("errtile", "errtile : text { key = \"error\"; }")]
        public void AFileThatDefinesAPredefinedName_IsRefused(string name, string definition)
        {
            // AutoCAD 2021 (GUI g1): loading base.dcl itself gave "Error in dialog file", the path and line, "warning: redefining
            // object." and Symbol: "row", and load_dialog gave -1; D304 and D306 ("the file's own wins") are withdrawn
            DclException error = Fail("// one\n\n" + definition + "\nd : dialog { " + name + "; }");

            Assert.Equal("warning: redefining object.\nSymbol: \"" + name + "\".", error.What);
            Assert.Equal(name, error.Symbol);
            Assert.Equal(3, error.Line);
            Assert.Equal(Path, error.File);
            Assert.False(error.Semantic);
            Assert.Equal(
                "Error in dialog file\n\""
                    + Path
                    + "\", line 3:\nwarning: redefining object.\nSymbol: \""
                    + name
                    + "\".",
                error.Message
            );
        }

        [Fact]
        public void AnIncludedFileThatDefinesAPredefinedName_IsRefusedWithThatFileAndLine()
        {
            // ours: D338 (the same refusal for an included file, reported at its own file and line)
            var files = new Dictionary<string, string> { ["lib.dcl"] = "x : text { }\n\nok_cancel : row { }" };

            DclException error = Fail("@include \"lib.dcl\";", files);

            Assert.Equal("warning: redefining object.\nSymbol: \"ok_cancel\".", error.What);
            Assert.Equal(@"C:\dcl\lib.dcl", error.File);
            Assert.Equal(3, error.Line);
        }

        [Fact]
        public void ADefinitionOfANameThatOnlyLooksLikeAPredefinedOne_IsAccepted()
        {
            // ours: D338 (names are case-sensitive, D303: Ok_Button is not ok_button)
            DclFile file = Parse("Ok_Button : button { label = \"Yes\"; }\nd : dialog { Ok_Button; ok_cancel; }");

            Assert.Equal("Yes", file.NewDialog("d").Children[0].Get("label"));
            Assert.Equal("  OK  ", file.Definitions["ok_button"].Get("label"));
        }

        [Fact]
        public void ACycleOfParents_IsAnError()
        {
            // ours: D304 and D307 (the spec gives no text for a cycle: "Tile \"a\" loops.")
            DclException error = Fail("a : b { }\nb : a { }\n");

            Assert.Equal("Tile \"a\" loops.", error.What);
            Assert.Equal(1, error.Line);
            Assert.Equal(Path, error.File);
        }

        [Fact]
        public void ADefinitionThatContainsItself_IsAnError()
        {
            // ours: D304 and D307
            DclException self = Fail("a : a { }");
            DclException inside = Fail("\na : row {\n  : a { }\n}");

            Assert.Equal("Tile \"a\" loops.", self.What);
            Assert.Equal("Tile \"a\" loops.", inside.What);
            Assert.Equal(2, inside.Line);
        }

        [Fact]
        public void AParentThatIsNotDefined_IsAnErrorAtItsLine()
        {
            // AutoCAD 2021 (GUI g1): an undefined tile is a semantic error, "Semantic error(s) in DCL file <path>."; the second
            // line AutoCAD gives is "See file acad.dce for details." and ours names the tile (no acad.dce is written)
            DclException top = Fail("\n\nx : nothing_like_it { }");
            DclException inner = Fail("d : dialog {\n  : row {\n    : missing_tile { }\n  }\n}");
            DclException bare = Fail("d : dialog {\n  missing_bare;\n}");

            Assert.Equal("Tile \"nothing_like_it\" is not defined.", top.What);
            Assert.Equal(3, top.Line);
            Assert.Equal("Tile \"missing_tile\" is not defined.", inner.What);
            Assert.Equal(3, inner.Line);
            Assert.Equal("Tile \"missing_bare\" is not defined.", bare.What);
            Assert.Equal(2, bare.Line);
            Assert.Null(top.Symbol);
            Assert.True(top.Semantic);
            Assert.Equal(
                "Semantic error(s) in DCL file " + Path + ".\nTile \"nothing_like_it\" is not defined.",
                top.Message
            );
        }

        [Fact]
        public void TheOtherErrors_AreNotSemantic()
        {
            // ours: D307 (a cycle and an include that is not found stay "Error in dialog file" at their line)
            DclException cycle = Fail("a : b { }\nb : a { }\n");
            DclException include = Fail("\n@include \"none.dcl\";");

            Assert.False(cycle.Semantic);
            Assert.Equal("Error in dialog file\n\"" + Path + "\", line 1:\nTile \"a\" loops.", cycle.Message);
            Assert.False(include.Semantic);
            Assert.Equal(
                "Error in dialog file\n\"" + Path + "\", line 2:\nInclude file \"none.dcl\" not found.",
                include.Message
            );
        }

        [Fact]
        public void AnUnusedDefinitionWithAnUndefinedParent_IsStillAnError()
        {
            // ours: D307 (every definition of the file is resolved when the file is loaded)
            Assert.Throws<DclException>(() => Parse("good : dialog { }\nunused : nothing { }"));
        }

        [Fact]
        public void NewDialog_GivesACopyThatSharesNothingWithTheDefinition()
        {
            // ours: D308
            DclFile file = Parse("d : dialog { : edit_box { key = \"e\"; } }");

            DclTile one = file.NewDialog("d");
            DclTile two = file.NewDialog("d");
            one.Find("e").Value = "changed";
            one.Find("e").Items.Add("x");

            Assert.NotSame(one, two);
            Assert.NotSame(one.Find("e"), two.Find("e"));
            Assert.Null(two.Find("e").Value);
            Assert.Empty(two.Find("e").Items);
            Assert.Null(file.Definitions["d"].Find("e").Value);
        }

        [Fact]
        public void NewDialog_OfANameThatIsNoDialog_IsNull()
        {
            DclFile file = Parse("b : button { }\nd : dialog { }");

            Assert.Null(file.NewDialog("b"));
            Assert.Null(file.NewDialog("nothing"));
            Assert.Null(file.NewDialog("ok_cancel"));
            Assert.NotNull(file.NewDialog("d"));
        }

        [Fact]
        public void TwoFilesParsedFromOneText_ShareNoTile()
        {
            // ours: D308
            DclFile one = Parse("d : dialog { ok_cancel; }");
            DclFile two = Parse("d : dialog { ok_cancel; }");

            Assert.NotSame(one.Definitions["ok_cancel"], two.Definitions["ok_cancel"]);
            one.Definitions["ok_cancel"].Value = "x";
            Assert.Null(two.Definitions["ok_cancel"].Value);
        }

        // ---- Includes (D305) ----

        [Fact]
        public void AnIncludedFile_AddsItsDefinitions()
        {
            // ours: D305
            var files = new Dictionary<string, string> { ["lib.dcl"] = "mybutton : button { label = \"Mine\"; }" };
            var asked = new List<string>();

            DclFile file = Parse("@include \"lib.dcl\";\nd : dialog { mybutton; }", files, asked);

            Assert.Equal("Mine", file.NewDialog("d").Children.Single().Get("label"));
            Assert.Equal(new[] { "lib.dcl|" + Path }, asked.ToArray());
            Assert.Equal(@"C:\dcl\lib.dcl", file.Definitions["mybutton"].File);
        }

        [Fact]
        public void TheIncludeIsGivenTheNameAsWrittenAndTheIncludingFile()
        {
            var files = new Dictionary<string, string>
            {
                ["sub/a"] = "@include \"b\";\nfromA : text { }",
                ["b"] = "fromB : text { }",
            };
            var asked = new List<string>();

            Parse("@include \"sub/a\";", files, asked);

            Assert.Equal(new[] { "sub/a|" + Path, @"b|C:\dcl\sub/a" }, asked.ToArray());
        }

        [Fact]
        public void TheFilesOwnDefinitions_WinOverTheIncludedOnes()
        {
            // ours: D305
            var files = new Dictionary<string, string>
            {
                ["lib.dcl"] = "x : button { label = \"included\"; key = \"i\"; }",
            };

            DclFile before = Parse("@include \"lib.dcl\";\nx : button { label = \"own\"; }", files);
            DclFile after = Parse("x : button { label = \"own\"; }\n@include \"lib.dcl\";", files);

            Assert.Equal("own", before.Definitions["x"].Get("label"));
            Assert.Equal("own", after.Definitions["x"].Get("label"));
            Assert.Null(before.Definitions["x"].Key);
        }

        [Fact]
        public void OfTwoIncludedFiles_TheLaterWinsAName()
        {
            var files = new Dictionary<string, string>
            {
                ["one"] = "x : button { label = \"one\"; }",
                ["two"] = "x : button { label = \"two\"; }",
            };

            DclFile file = Parse("@include \"one\";\n@include \"two\";", files);

            Assert.Equal("two", file.Definitions["x"].Get("label"));
        }

        [Fact]
        public void AnIncludedDefinition_CanBeDerivedFromByTheIncludingFile()
        {
            var files = new Dictionary<string, string>
            {
                ["lib"] = "colored : button { key = \"c\"; }\nsub : colored { label = \"sub\"; }",
            };

            DclTile dialog = Parse("@include \"lib\";\nmine : sub { width = 3; }\nd : dialog { mine; }", files)
                .NewDialog("d");

            DclTile mine = dialog.Children.Single();
            Assert.Equal("button", mine.Kind);
            Assert.Equal("c", mine.Key);
            Assert.Equal("sub", mine.Get("label"));
            Assert.Equal(3, mine.GetInteger("width"));
        }

        [Fact]
        public void AnIncludeThatIsNotFound_IsAnErrorAtTheIncludeLine()
        {
            // ours: D307
            DclException error = Fail("\n// comment\n@include \"nothing.dcl\";\nd : dialog { }");

            Assert.Equal("Include file \"nothing.dcl\" not found.", error.What);
            Assert.Equal(3, error.Line);
            Assert.Equal(Path, error.File);
            Assert.Null(error.Symbol);
        }

        [Fact]
        public void AFileThatIncludesItself_Loops()
        {
            // ours: D307
            var files = new Dictionary<string, string> { ["self.dcl"] = "@include \"self.dcl\";" };

            DclException error = Fail("@include \"self.dcl\";", files);

            Assert.Equal("Include of \"self.dcl\" loops.", error.What);
            Assert.Equal(1, error.Line);
            Assert.Equal(@"C:\dcl\self.dcl", error.File);
        }

        [Fact]
        public void TwoFilesThatIncludeEachOther_Loop()
        {
            var files = new Dictionary<string, string>
            {
                ["a.dcl"] = "x : text { }\n@include \"b.dcl\";",
                ["b.dcl"] = "\n@include \"a.dcl\";",
            };

            DclException error = Fail("@include \"a.dcl\";", files);

            Assert.Equal("Include of \"a.dcl\" loops.", error.What);
            Assert.Equal(2, error.Line);
            Assert.Equal(@"C:\dcl\b.dcl", error.File);
        }

        [Fact]
        public void AFileThatIncludesTheIncludingFile_Loops()
        {
            // ours: D305 (the including file is in the chain too)
            var files = new Dictionary<string, string> { ["back.dcl"] = "@include \"test.dcl\";" };
            DclException error = Assert.Throws<DclException>(() =>
                DclParser.Parse("@include \"back.dcl\";", @"C:\dcl\TEST.dcl", (name, from) => files.TryGetValue(
                        name,
                        out string content
                    )
                        ? new KeyValuePair<string, string>(@"C:\dcl\" + name, content)
                    : name == "test.dcl" ? new KeyValuePair<string, string>(@"C:\dcl\test.dcl", "")
                    : (KeyValuePair<string, string>?)null)
            );

            Assert.Equal("Include of \"test.dcl\" loops.", error.What);
        }

        [Fact]
        public void OneFileIncludedTwice_IsNotALoop()
        {
            var files = new Dictionary<string, string>
            {
                ["a"] = "@include \"c\";\na1 : text { }",
                ["b"] = "@include \"c\";\nb1 : text { }",
                ["c"] = "c1 : text { }",
            };

            DclFile file = Parse("@include \"a\";\n@include \"b\";", files);

            Assert.True(file.Definitions.ContainsKey("a1"));
            Assert.True(file.Definitions.ContainsKey("b1"));
            Assert.True(file.Definitions.ContainsKey("c1"));
        }

        [Fact]
        public void AnErrorInAnIncludedFile_IsReportedWithThatFileAndLine()
        {
            // ours: D307
            var files = new Dictionary<string, string> { ["bad.dcl"] = "x : text { }\n\ny : text { label = ; }" };

            DclException syntax = Fail("@include \"bad.dcl\";", files);
            DclException undefined = Fail(
                "@include \"undef.dcl\";",
                new Dictionary<string, string> { ["undef.dcl"] = "\nz : nowhere { }" }
            );

            Assert.Equal(@"C:\dcl\bad.dcl", syntax.File);
            Assert.Equal(3, syntax.Line);
            Assert.Equal(@"C:\dcl\undef.dcl", undefined.File);
            Assert.Equal(2, undefined.Line);
            Assert.Equal("Tile \"nowhere\" is not defined.", undefined.What);
        }

        [Theory]
        [InlineData("base.dcl")]
        [InlineData("base")]
        [InlineData("BASE.DCL")]
        public void IncludingTheBaseFile_DoesNothingMoreThanThePredefinedSet(string name)
        {
            // ours: D305 (and the callback is not asked: AutoCAD's own base.dcl is never read)
            var asked = new List<string>();

            DclFile file = Parse("@include \"" + name + "\";\nd : dialog { ok_cancel; }", null, asked);

            Assert.Empty(asked);
            Assert.NotNull(file.NewDialog("d"));
        }

        [Fact]
        public void IncludingAcadDcl_IsLookedForLikeAnyFile_AndIsNotFoundWhereThereIsNone()
        {
            // ours: D305 (acad.dcl is AutoCAD's own file, read at run time where the search finds it, never shipped)
            var asked = new List<string>();

            DclException error = Assert.Throws<DclException>(() => Parse("@include \"acad.dcl\";", null, asked));

            Assert.Equal("Include file \"acad.dcl\" not found.", error.What);
            Assert.Equal(new[] { "acad.dcl|" + Path }, asked.ToArray());
        }

        [Fact]
        public void ANullIncludeFinder_FindsNothing()
        {
            DclException error = Assert.Throws<DclException>(() => DclParser.Parse("@include \"x\";", Path, null));

            Assert.Equal("Include file \"x\" not found.", error.What);
        }

        // ---- Predefined tiles (D306) ----

        [Theory]
        [InlineData("row", "cluster", "horizontal")]
        [InlineData("column", "cluster", "vertical")]
        [InlineData("boxed_row", "cluster", "horizontal")]
        [InlineData("boxed_column", "cluster", "vertical")]
        [InlineData("radio_row", "radio_cluster", "horizontal")]
        [InlineData("radio_column", "radio_cluster", "vertical")]
        [InlineData("boxed_radio_row", "radio_cluster", "horizontal")]
        [InlineData("boxed_radio_column", "radio_cluster", "vertical")]
        public void TheClusters_AreClustersWithALayout(string name, string kind, string layout)
        {
            // reference: row, column, radio_row, radio_column and their boxed forms
            DclTile tile = Parse("").Definitions[name];

            Assert.Equal(kind, tile.Kind);
            Assert.Equal(name, tile.Name);
            Assert.Equal(layout, tile.Get("layout"));
            Assert.Empty(tile.Children);
        }

        [Theory]
        [InlineData("row", false)]
        [InlineData("column", false)]
        [InlineData("radio_row", false)]
        [InlineData("radio_column", false)]
        [InlineData("boxed_row", true)]
        [InlineData("boxed_column", true)]
        [InlineData("boxed_radio_row", true)]
        [InlineData("boxed_radio_column", true)]
        public void TheBoxedClusters_AreBoxedWithABlankLabel_AndTheOthersAreNot(string name, bool boxed)
        {
            // ours: D306 (boxed, label " "; row and column have no margins)
            DclTile tile = Parse("").Definitions[name];

            Assert.Equal(boxed, tile.IsTrue("boxed"));
            Assert.Equal(boxed ? " " : null, tile.Get("label"));
            Assert.Equal(boxed ? null : "none", tile.Get("horizontal_margin"));
            Assert.Equal(boxed ? null : "none", tile.Get("vertical_margin"));
        }

        [Fact]
        public void TheTextTiles_AreTextsAndClusters()
        {
            DclFile file = Parse("");

            Assert.Equal("text", file.Definitions["text_part"].Kind);
            Assert.Equal("cluster", file.Definitions["concatenation"].Kind);
            Assert.Equal("horizontal", file.Definitions["concatenation"].Get("layout"));
            Assert.Equal("cluster", file.Definitions["paragraph"].Kind);
            Assert.Equal("vertical", file.Definitions["paragraph"].Get("layout"));
        }

        [Fact]
        public void TheSpacers_AreZeroByZeroAndOneByOne()
        {
            // ours: D306
            DclFile file = Parse("");

            Assert.Equal("spacer", file.Definitions["spacer_0"].Kind);
            Assert.Equal(0, file.Definitions["spacer_0"].GetInteger("width"));
            Assert.Equal(0, file.Definitions["spacer_0"].GetInteger("height"));
            Assert.Equal("spacer", file.Definitions["spacer_1"].Kind);
            Assert.Equal(1, file.Definitions["spacer_1"].GetInteger("width"));
            Assert.Equal(1, file.Definitions["spacer_1"].GetInteger("height"));
        }

        [Theory]
        [InlineData("ok_button", "  OK  ", "accept", "is_default")]
        [InlineData("cancel_button", "Cancel", "cancel", "is_cancel")]
        [InlineData("help_button", "&Help", "help", "is_help")]
        [InlineData("info_button", "&Info...", "info", null)]
        public void TheExitButtons_HaveTheirLabelKeyAndRole(string name, string label, string key, string role)
        {
            // reference: ok_button, cancel_button, help_button and info_button (keys accept, cancel, help, info)
            DclTile tile = Parse("").Definitions[name];

            Assert.Equal("button", tile.Kind);
            Assert.Equal(label, tile.Get("label"));
            Assert.Equal(key, tile.Key);
            Assert.True(role == null || tile.IsTrue(role));
            Assert.True(tile.IsTrue("fixed_width"));
            Assert.Equal(8, tile.GetInteger("width"));
            Assert.Equal("centered", tile.Get("alignment"));
        }

        [Fact]
        public void TheBaseButtons_AreFixedWidthEight_AndOnlyTheDefaultOneIsTheDefault()
        {
            // ours: D306
            DclFile file = Parse("");

            Assert.Equal("button", file.Definitions["default_button"].Kind);
            Assert.True(file.Definitions["default_button"].IsTrue("is_default"));
            Assert.Equal("button", file.Definitions["retirement_button"].Kind);
            Assert.False(file.Definitions["retirement_button"].IsTrue("is_default"));
            Assert.True(file.Definitions["retirement_button"].IsTrue("fixed_width"));
            Assert.Equal(8, file.Definitions["retirement_button"].GetInteger("width"));
            Assert.Equal("centered", file.Definitions["retirement_button"].Get("alignment"));
            Assert.False(file.Definitions["cancel_button"].IsTrue("is_default"));
            Assert.False(file.Definitions["help_button"].IsTrue("is_cancel"));
        }

        [Theory]
        [InlineData("ok_only", "accept")]
        [InlineData("ok_cancel", "accept,cancel")]
        [InlineData("ok_cancel_help", "accept,cancel,help")]
        [InlineData("ok_cancel_help_info", "accept,cancel,help,info")]
        public void TheExitRows_AreCentredRowsOfTheirButtonsWithSpacersOfTwoBetween(string name, string keys)
        {
            // ours: D306 (centred rows with spacers of width 2 between the buttons)
            DclTile row = Parse("").Definitions[name];

            Assert.Equal("cluster", row.Kind);
            Assert.Equal("horizontal", row.Get("layout"));
            Assert.True(row.IsTrue("fixed_width"));
            Assert.Equal("centered", row.Get("alignment"));
            Assert.Equal(
                keys.Split(','),
                row.Children.Where(child => child.Kind == "button").Select(child => child.Key).ToArray()
            );

            DclTile[] spacers = row.Children.Where(child => child.Kind == "spacer").ToArray();
            Assert.Equal(keys.Split(',').Length - 1, spacers.Length);
            Assert.All(spacers, spacer => Assert.Equal(2, spacer.GetInteger("width")));
            Assert.All(spacers, spacer => Assert.Equal(0, spacer.GetInteger("height")));

            // buttons and spacers alternate, a button first and last
            Assert.Equal("button", row.Children[0].Kind);
            Assert.Equal("button", row.Children[row.Children.Count - 1].Kind);
        }

        [Fact]
        public void OkOnly_IsAlsoTheCancelTile()
        {
            // ours: D306 (ok_only: OK alone, also is_cancel)
            DclTile ok = Parse("").Definitions["ok_only"].Children.Single();

            Assert.Equal("accept", ok.Key);
            Assert.True(ok.IsTrue("is_default"));
            Assert.True(ok.IsTrue("is_cancel"));
            Assert.Equal("ok_button", ok.Name);
        }

        [Fact]
        public void ErrTile_IsAnEmptyTextWithTheKeyError_ThirtyFiveWide()
        {
            // ours: D306
            DclTile tile = Parse("").Definitions["errtile"];

            Assert.Equal("text", tile.Kind);
            Assert.Equal("error", tile.Key);
            Assert.Equal(35, tile.GetInteger("width"));
            Assert.Equal("", tile.Get("value"));
        }

        [Theory]
        [InlineData("ok_cancel_help_errtile", "ok_cancel_help")]
        [InlineData("ok_cancel_err", "ok_cancel")]
        public void TheRowsWithAnErrorTile_AreAColumnOfTheRowAndTheErrorTileUnderIt(string name, string row)
        {
            // AutoCAD 2021 (GUI g1, all.png): the error tile is under the buttons (OK at 383, the tile at 411); ours: D306 for the rest
            DclTile tile = Parse("").Definitions[name];

            Assert.Equal("cluster", tile.Kind);
            Assert.Equal("vertical", tile.Get("layout"));
            Assert.Equal(new[] { row, "errtile" }, tile.Children.Select(child => child.Name).ToArray());
            Assert.Equal("error", tile.Children[1].Key);
            Assert.Equal("accept", tile.Find("accept").Key);
        }

        [Fact]
        public void TheImageAndEditTiles_HaveTheirUsualSizes()
        {
            // ours: D306
            DclFile file = Parse("");

            Assert.Equal("image", file.Definitions["image_block"].Kind);
            Assert.Equal("image", file.Definitions["icon_image"].Kind);
            Assert.Equal(1, file.Definitions["image_block"].GetReal("aspect_ratio"));
            Assert.Equal(1, file.Definitions["icon_image"].GetReal("aspect_ratio"));
            Assert.Equal("edit_box", file.Definitions["edit12_box"].Kind);
            Assert.Equal(12, file.Definitions["edit12_box"].GetInteger("edit_width"));
            Assert.Equal("edit_box", file.Definitions["edit32_box"].Kind);
            Assert.Equal(32, file.Definitions["edit32_box"].GetInteger("edit_width"));
        }

        [Fact]
        public void EveryPredefinedTile_IsAPrimitive_OneOfTheFourteenPrimitives()
        {
            // ours: D309
            string[] primitives =
            {
                "dialog",
                "cluster",
                "radio_cluster",
                "button",
                "edit_box",
                "list_box",
                "popup_list",
                "toggle",
                "radio_button",
                "text",
                "image",
                "image_button",
                "slider",
                "spacer",
            };
            DclFile file = Parse("");

            foreach (DclTile tile in file.Definitions.Values)
            {
                Assert.Contains(tile.Kind, primitives);

                foreach (DclTile below in tile.Descendants())
                {
                    Assert.Contains(below.Kind, primitives);
                }
            }
        }

        [Fact]
        public void EveryPrimitive_CanBeUsedAsAParent()
        {
            // ours: D309
            string[] primitives =
            {
                "cluster",
                "radio_cluster",
                "button",
                "edit_box",
                "list_box",
                "popup_list",
                "toggle",
                "radio_button",
                "text",
                "image",
                "image_button",
                "slider",
                "spacer",
            };

            DclTile dialog = Dialog(string.Concat(primitives.Select(kind => ": " + kind + " { }\n")));

            Assert.Equal(primitives, dialog.Children.Select(child => child.Kind).ToArray());
        }

        [Fact]
        public void AnAttributeThePresenterDoesNotKnow_IsKept()
        {
            // ours: D309 (get_attr reads it)
            DclTile text = Dialog(": text { made_up_attribute = \"kept\"; other = 12; }").Children.Single();

            Assert.Equal("kept", text.Get("made_up_attribute"));
            Assert.Equal(12, text.GetInteger("other"));
        }

        // ---- Texts like the two acceptance routines' (written for the test, in our own words) ----

        [Fact]
        public void ADialogLikeTheOneARoutineWritesToATempFile_IsRead()
        {
            // the shape of a small edit dialog: initial focus, an edit box that accepts on Enter, a spacer and OK/Cancel
            const string text =
                "EDIT : dialog {\n"
                + "  label = \"Edit text\";\n"
                + "  initial_focus = \"txt\";\n"
                + "  : edit_box { key = \"txt\"; label = \"Text:\"; edit_width = 60; allow_accept = true; }\n"
                + "  spacer_1;\n"
                + "  ok_cancel;\n"
                + "}\n";

            DclTile dialog = Parse(text).NewDialog("EDIT");

            Assert.Equal("Edit text", dialog.Get("label"));
            Assert.Equal("txt", dialog.Get("initial_focus"));
            Assert.Equal(
                new[] { "edit_box", "spacer_1", "ok_cancel" },
                dialog.Children.Select(child => child.Name).ToArray()
            );
            Assert.True(dialog.Find("txt").IsTrue("allow_accept"));
            Assert.Equal(60, dialog.Find("txt").GetInteger("edit_width"));
            Assert.Equal("accept", dialog.Find("accept").Key);
            Assert.Equal("cancel", dialog.Find("cancel").Key);
            Assert.Equal(4, dialog.Find("txt").Line);
        }

        [Fact]
        public void ADialogWithAMultipleSelectListBox_IsRead()
        {
            const string text =
                "ListBox : dialog {\n"
                + "  label = \"Tags\";\n"
                + "  : text { label = \"Pick the tags:\"; }\n"
                + "  spacer;\n"
                + "  : list_box { key = \"lst\"; multiple_select = true; width = 30; height = 8; }\n"
                + "  spacer;\n"
                + "  ok_cancel;\n"
                + "}\n";

            DclTile dialog = Parse(text).NewDialog("ListBox");

            DclTile list = dialog.Find("lst");
            Assert.Equal("list_box", list.Kind);
            Assert.True(list.IsTrue("multiple_select"));
            Assert.Equal(30, list.GetInteger("width"));
            Assert.Equal(8, list.GetInteger("height"));
            Assert.Equal(
                new[] { "text", "spacer", "list_box", "spacer", "cluster" },
                dialog.Children.Select(child => child.Kind).ToArray()
            );
        }

        [Fact]
        public void TheSameTextParsedTwice_GivesTheSameTree()
        {
            // the order of every list comes from the text, not from a hash
            const string text =
                "d : dialog { : row { : text { key = \"a\"; } : toggle { key = \"b\"; } } ok_cancel_help_errtile; }";

            string[] Dump(DclFile file) =>
                file
                    .Definitions.Values.Select(tile =>
                        tile.Name
                        + ":"
                        + string.Join(
                            ",",
                            tile.Descendants()
                                .Select(below =>
                                    below.Name
                                    + "="
                                    + string.Join(
                                        "/",
                                        below.Attributes.Keys.OrderBy(key => key, StringComparer.Ordinal)
                                    )
                                )
                        )
                    )
                    .OrderBy(line => line, StringComparer.Ordinal)
                    .ToArray();

            Assert.Equal(Dump(Parse(text)), Dump(Parse(text)));
        }

        [Fact]
        public void ANullText_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => DclParser.Parse(null, Path, null));
            Assert.Throws<ArgumentNullException>(() => DclParser.Parse("", null, null));
        }
    }
}
