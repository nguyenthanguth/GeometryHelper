using System;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Fields as AutoCAD 2021 does them. The expected words are those AutoCAD 2021's own fields gave in its console for the
    /// same formats (an area of 314.159265, a length of 50 inches, 17 November 2026 at 03:21:05), and the FIELD objects written
    /// are compared group by group with those it wrote for the same texts.
    /// </summary>
    public class FieldTests
    {
        private static readonly DateTime Moment = new DateTime(2026, 11, 17, 3, 21, 5);
        private static readonly CultureInfo English = CultureInfo.InvariantCulture;

        private static CadFieldContext Context(CadDocument document, int reason = CadFieldContexts.Demand) =>
            new CadFieldContext(document, reason)
            {
                Now = Moment,
                Culture = English,
                Login = "Jane",
            };

        /// <summary>A drawing of R2018 holding a circle of radius 10 (handle C1), a line from (0, 0) to (30, 40) (L1) and a TEXT (T1).</summary>
        private static CadDocument Plan(string version = "AC1032")
        {
            var database = new DxfDatabase(version);
            database.ModelSpace.AppendEntity(new DxfCircle { Center = new GeoPoint3(100, 100, 0), Radius = 10 });
            database.ModelSpace.AppendEntity(
                new DxfLine { StartPoint = new GeoPoint3(0, 0, 0), EndPoint = new GeoPoint3(30, 40, 0) }
            );
            database.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    TextString = "x",
                    Position = new GeoPoint3(0, -50, 0),
                    Height = 2.5,
                }
            );
            return CadDocument.From(database, "plan.dxf");
        }

        private static T First<T>(CadDocument document)
            where T : DxfEntity => document.Database.ModelSpace.OfType<T>().First();

        private static string Number(double value, string format, bool angle = false) =>
            CadFieldFormat.Parse(format).Number(value, angle);

        [Theory]
        [InlineData("%lu2%pr0", "314")]
        [InlineData("%lu2%pr2", "314.16")]
        [InlineData("%lu1%pr3", "3.142E+02")]
        [InlineData("%lu3%pr1", "26'-2.2\"")]
        [InlineData("%lu4%pr0", "26'-2\"")]
        [InlineData("%lu5%pr2", "314 1/4")]
        [InlineData("%lu2%pr2%ps[A=,mm2]", "A=314.16mm2")]
        [InlineData("%lu2%pr2%ct8[0.0001]", "0.03")]
        [InlineData("%lu2%pr2%ds44", "314,16")]
        public void AnAreaOf314_159265_IsWrittenAsTheFormatSaysItAsAutoCADWritesIt(string format, string expected) =>
            Assert.Equal(expected, Number(314.159265358979, format));

        [Theory]
        [InlineData("%lu2%pr2%ct8[1000]%th44", "600,000.00")]
        [InlineData("%lu2%pr2%ct8[1000]%th32", "600 000.00")]
        [InlineData("%lu2%pr2%ct8[1000]%th0", "600000.00")]
        [InlineData("%lu2%pr2%ct8[1000]%th1", "600,000.00")]
        [InlineData("%lu2%pr0%ct8[1000]", "600000")]
        [InlineData("%lu2%pr3%zs8", "600")]
        [InlineData("%lu2%pr3%zs0", "600.000")]
        [InlineData("%lu2%pr2%ct8[1.0E-6]%ps[,m2]", "0.00m2")]
        public void AnAreaOf600_ThousandsFactorsAndZerosFollowTheFormat(string format, string expected) =>
            Assert.Equal(expected, Number(600.0, format));

        [Theory]
        [InlineData("%lu4%pr4", "4'-2\"")]
        [InlineData("%lu3%pr3", "4'-2.000\"")]
        [InlineData("%lu5%pr3", "50")]
        [InlineData("%lu1%pr1", "5.0E+01")]
        public void ALengthOfFiftyInchesIsInFeetAndInchesAsTheUnitsSay(string format, string expected) =>
            Assert.Equal(expected, Number(50.0, format));

        [Theory]
        [InlineData("%au0%pr0", "53")]
        [InlineData("%au1%pr0", "53d")]
        [InlineData("%au1%pr2", "53d8'")]
        [InlineData("%au2%pr2", "59.03g")]
        [InlineData("%au3%pr2", "0.93r")]
        [InlineData("%au4%pr2", "N 36d52' E")]
        [InlineData("%au0%pr3%zs8", "53.13")]
        public void AnAngleOf0_9273Radians_IsInTheUnitsItIsAskedIn(string format, string expected) =>
            Assert.Equal(expected, Number(Math.Atan2(40, 30), format, true));

        [Theory]
        [InlineData("M/d/yyyy h:mm:ss tt", "11/17/2026 3:21:05 AM")]
        [InlineData("dddd, MMMM dd, yyyy", "Tuesday, November 17, 2026")]
        [InlineData("h:mm tt", "3:21 AM")]
        [InlineData("MMM d yyyy", "Nov 17 2026")]
        [InlineData("yyyy.MM.dd", "2026.11.17")]
        [InlineData("d MMMM yyyy", "17 November 2026")]
        [InlineData("dd/MM/yy HH:mm:ss", "17/11/26 03:21:05")]
        [InlineData("ddd d'th' MMMM", "Tue 17A3 November")]
        [InlineData("", "11/17/2026")]
        public void ADateIsWrittenByTheLettersOfItsFormatAsAutoCADWritesIt(string format, string expected) =>
            Assert.Equal(expected, CadFieldFormat.Date(Moment, format, English));

        [Theory]
        [InlineData("%tc1", "SOME COMMENTS. NEW SENTENCE HERE")]
        [InlineData("%tc2", "some comments. new sentence here")]
        [InlineData("%tc3", "SOME cOMMENTS. nEW sentence HERE")]
        [InlineData("%tc4", "SOME COMMENTS. NEW Sentence HERE")]
        [InlineData("", "sOME cOMMENTS. nEW sentence HERE")]
        public void WordsTakeTheCaseOfTheirFormat(string format, string expected) =>
            Assert.Equal(expected, CadFieldFormat.Parse(format).Words("sOME cOMMENTS. nEW sentence HERE"));

        [Theory]
        [InlineData("$(upper,abc)", "ABC")]
        [InlineData("$(strlen,hello)", "5")]
        [InlineData("$(substr,hello,2,3)", "ell")]
        [InlineData("$(if,$(>,2,1),yes,no)", "yes")]
        [InlineData("$(rtos,3.14159,2,3)", "3.142")]
        [InlineData("$(*,2.5,2)", "5")]
        [InlineData("$(/,7,2)", "3.5")]
        [InlineData("$(eq,a,a)", "1")]
        [InlineData("$(-,10,3)", "7")]
        [InlineData("$(fix,3.9)", "3")]
        [InlineData("$(and,1,0)", "0")]
        [InlineData("$(angtos,1.5708,0,2)", "90.00")]
        [InlineData("$(strlen,)", "0")]
        [InlineData("$(+,1,2)", "3")]
        public void ADieselExpressionWorksOutAsAutoCADWorksItOut(string expression, string expected) =>
            Assert.Equal(expected, new CadDiesel(null, Moment, English).Evaluate(expression));

        [Fact]
        public void TheDateOfDieselIsTheJulianDateAutoCADGives()
        {
            var diesel = new CadDiesel(null, new DateTime(2026, 11, 17, 3, 33, 46), English);
            Assert.StartsWith("2461362.1484", diesel.Evaluate("$(getvar,date)"));
            Assert.Equal("2026-11-17", diesel.Evaluate("$(edtime,$(getvar,date),YYYY-MO-DD)"));
        }

        [Theory]
        [InlineData("2*3+1", "7", true)]
        [InlineData("(3+4)/2", "3.5", false)]
        [InlineData("10/4", "2.5", false)]
        [InlineData("sqrt(16)", "4", false)]
        [InlineData("8/2", "4", true)]
        public void AFormulaIsWholeWhereItsNumbersAndResultAre(string formula, string expected, bool whole)
        {
            Assert.True(CadFieldFormula.TryEvaluate(formula, out double value, out bool isWhole));
            Assert.Equal(expected, value.ToString("0.###", English));
            Assert.Equal(whole, isWhole);
            Assert.False(CadFieldFormula.TryEvaluate("2+", out _, out _));
            Assert.False(CadFieldFormula.TryEvaluate("1/0", out _, out _));
        }

        [Fact]
        public void ATextGivenADateField_ShowsTheDate_AndHoldsFieldObjectsAsAutoCADWritesThem()
        {
            CadDocument document = Plan();
            DxfDBText text = First<DxfDBText>(document);

            Assert.True(CadFields.Set(text, "Drawn %<\\AcVar Date \\f \"dd/MM/yyyy\">%", Context(document)));

            Assert.Equal("Drawn 17/11/2026", text.TextString);
            Assert.Equal("Drawn %<\\AcVar Date \\f \"dd/MM/yyyy\">%", CadFields.CodesOf(text));
            DxfField field = text.GetField();
            Assert.True(field.IsTextField);
            Assert.Equal("Drawn %<\\_FldIdx 0>%", field.GetFieldCode());
            DxfField child = Assert.Single(field.GetChildren());

            string saved = document.Database.ToText();
            // The groups of AutoCAD 2021's own FIELDs of the same text (the checksum 7540 counts each letter as many times as its place).
            Assert.Contains(
                "100\nAcDbField\n  1\n_text\n  2\nDrawn %<\\_FldIdx 0>%\n 90\n1\n360\n"
                    + child.Handle
                    + "\n 97\n0\n 91\n63\n 92\n0\n 94\n9\n 95\n2\n 96\n0\n300\n\n 93\n1\n  6\nACFD_FIELDTEXT_CHECKSUM\n 93\n2\n 90\n2\n140\n7540.0\n 94\n0\n300\n\n302\n\n304\nACVALUE_END\n  7\nACFD_FIELD_VALUE\n 93\n3\n 90\n0\n 94\n0\n300\n\n302\n\n304\nACVALUE_END\n301\n\n 98\n0\n",
                saved,
                StringComparison.Ordinal
            );
            Assert.Contains(
                "100\nAcDbField\n  1\nAcVar\n  2\n\\AcVar Date \\f \"dd/MM/yyyy\"\n 90\n0\n 97\n0\n 91\n63\n 92\n0\n 94\n59\n 95\n2\n 96\n0\n300\n\n 93\n1\n  6\nVariable\n 93\n2\n 90\n4\n  1\nDate\n 94\n0\n300\n\n302\n\n304\nACVALUE_END\n  7\nACFD_FIELD_VALUE\n 93\n4\n 90\n8\n 92\n16\n310\nEA070B00020011000300150005000000\n",
                saved,
                StringComparison.Ordinal
            );
            Assert.Contains(
                " 94\n0\n300\ndd/MM/yyyy\n302\n17/11/2026\n304\nACVALUE_END\n301\n17/11/2026\n 98\n10\n",
                saved,
                StringComparison.Ordinal
            );
        }

        [Fact]
        public void AnMTextGivenFieldsInItsFormatting_ShowsTheirWords_AndKeepsTheCodesAndTheChecksumAutoCADKeeps()
        {
            CadDocument document = Plan();
            document.Database.SummaryInfo = new DxfDatabaseSummaryInfoBuilder
            {
                Author = "Jane Author",
                Title = "My Title",
            }.ToDatabaseSummaryInfo();
            var mtext = new DxfMText
            {
                Location = new GeoPoint3(0, 0, 0),
                TextHeight = 2.5,
                Contents = "x",
            };
            document.Database.ModelSpace.AppendEntity(mtext);

            CadFields.Set(mtext, "{\\fArial|b1;Bold} %<\\AcVar Author>%\\Pline2 %<\\AcVar Title>%", Context(document));

            Assert.Equal("{\\fArial|b1;Bold} Jane Author\\Pline2 My Title", mtext.Contents);
            Assert.Equal("{\\fArial|b1;Bold} %<\\AcVar Author>%\\Pline2 %<\\AcVar Title>%", CadFields.CodesOf(mtext));
            Assert.Equal(new[] { "Jane Author", "My Title" }, CadFields.ShownOf(mtext));
            // AutoCAD 2021 kept 94113 for the same words: each letter counted as many times as its place.
            Assert.Contains("140\n94113.0\n", document.Database.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void TheValuesOfTheVariablesOfTheDrawingAreThoseAutoCADShows()
        {
            CadDocument document = Plan();
            document.Database.SummaryInfo = new DxfDatabaseSummaryInfoBuilder
            {
                Author = "Jane Author",
                Comments = "sOME cOMMENTS. nEW sentence HERE",
                HyperlinkBase = "http://x.y/base",
            }.ToDatabaseSummaryInfo();
            DxfDBText text = First<DxfDBText>(document);
            CadFieldContext context = Context(document);

            string[,] cases =
            {
                { "%<\\AcVar Author \\f \"%tc1\">%", "JANE AUTHOR" },
                { "%<\\AcVar Comments \\f \"%tc4\">%", "SOME COMMENTS. NEW Sentence HERE" },
                { "%<\\AcVar HyperlinkBase>%", "http://x.y/base" },
                { "%<\\AcVar Login>%", "Jane" },
                { "%<\\AcVar CreateDate \\f \"M/d/yyyy\">%", "----" },
                { "%<\\AcVar Filesize>%", "----" },
                { "%<\\AcVar Filename>%", "plan.dxf" },
                { "%<\\AcVar Filename \\f \"%fn2\">%", "plan" },
                { "%<\\AcVar Filename \\f \"%fn4\">%", "dxf" },
                { "%<\\AcVar CTAB>%", "Model" },
                { "%<\\AcVar DIMTXT>%", "2.500000" },
                { "%<\\AcVar INSUNITS>%", "4" },
                { "%<\\AcVar Nonsense>%", "####" },
                { "%<\\AcDiesel $(getvar,ctab)>%", "Model" },
                { "%<\\AcExpr 2*3+1>%", "7" },
                { "%<\\AcExpr 10/4 \\f \"%lu2%pr3\">%", "2.500" },
                { "%<\\AcExpr (3+4)/2>%", "3.500000" },
            };

            for (int i = 0; i < cases.GetLength(0); i++)
            {
                CadFields.Set(text, cases[i, 0], context);
                Assert.True(
                    cases[i, 1] == text.TextString,
                    cases[i, 0] + " gave " + text.TextString + " and not " + cases[i, 1]
                );
            }
        }

        [Fact]
        public void TheFieldOfAnObjectsAreaIsEvaluatedAgainByUPDATEFIELD_AfterTheCircleChanged_AndUndoGivesTheOldWordsBack()
        {
            CadDocument document = Plan();
            DxfCircle circle = First<DxfCircle>(document);
            DxfDBText text = First<DxfDBText>(document);
            string handle = text.Handle;
            string decimalHandle = ulong.Parse(circle.Handle, NumberStyles.HexNumber).ToString(English);
            string codes = "Area %<\\AcObjProp Object(%<\\_ObjId " + decimalHandle + ">%).Area \\f \"%lu2%pr2\">%";

            Assert.True(CadFields.Set(text, codes, Context(document)));
            Assert.Equal("Area 314.16", text.TextString);
            Assert.Equal(codes, CadFields.CodesOf(text));

            circle.Radius = 20;
            Assert.Equal("Area 314.16", text.TextString);

            CadCommandSession session = Run(Session(document), "UPDATEFIELD", new[] { handle }, Enter);

            Assert.Equal("Area 1256.64", Entity<DxfDBText>(document, handle).TextString);
            Assert.Contains("1 field(s) found. 1 field(s) updated.", session.History.Last());
            Assert.Equal("1256.64", Assert.Single(CadFields.ShownOf(Entity<DxfDBText>(document, handle))));
            Assert.Equal(codes, CadFields.CodesOf(Entity<DxfDBText>(document, handle)));
            Assert.Equal("UPDATEFIELD", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal("Area 314.16", Entity<DxfDBText>(document, handle).TextString);
        }

        [Fact]
        public void AFieldOfAnObjectErased_ShowsFourHashes_AndAFieldOfAPropertyTheObjectLacksToo()
        {
            CadDocument document = Plan();
            DxfDBText text = First<DxfDBText>(document);
            DxfLine line = First<DxfLine>(document);
            string number = ulong.Parse(line.Handle, NumberStyles.HexNumber).ToString(English);

            CadFields.Set(text, "%<\\AcObjProp Object(%<\\_ObjId " + number + ">%).Radius>%", Context(document));
            Assert.Equal("####", text.TextString);
            CadFields.Set(
                text,
                "%<\\AcObjProp Object(%<\\_ObjId " + number + ">%).Length \\f \"%lu2%pr1\">%",
                Context(document)
            );
            Assert.Equal("50.0", text.TextString);
            line.Erase();
            CadFields.Update(text, Context(document));
            Assert.Equal("####", text.TextString);
        }

        [Fact]
        public void ATextWhoseFieldsAreConvertedToText_KeepsTheWordsAndHoldsNoField()
        {
            CadDocument document = Plan();
            DxfDBText text = First<DxfDBText>(document);
            CadFields.Set(text, "By %<\\AcVar Login>%", Context(document));
            Assert.True(CadFields.Has(text));

            text.ConvertFieldToText();

            Assert.False(CadFields.Has(text));
            Assert.Equal("By Jane", text.TextString);
            Assert.Equal("By Jane", CadFields.CodesOf(text));
        }

        [Fact]
        public void WordsWithNoFieldInThem_TakeTheFieldOutOfATextThatHadOne()
        {
            CadDocument document = Plan();
            DxfDBText text = First<DxfDBText>(document);
            CadFields.Set(text, "By %<\\AcVar Login>%", Context(document));

            Assert.False(CadFields.Set(text, "Plain words", Context(document)));

            Assert.Equal("Plain words", text.TextString);
            Assert.False(CadFields.Has(text));
        }

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1021")]
        [InlineData("AC1018")]
        public void TheFieldsOfATextAreKeptByADxfAndByADwgOfTheSameVersion(string version)
        {
            CadDocument document = Plan(version);
            DxfCircle circle = First<DxfCircle>(document);
            DxfDBText text = First<DxfDBText>(document);
            string number = ulong.Parse(circle.Handle, NumberStyles.HexNumber).ToString(English);
            string codes =
                "A %<\\AcObjProp Object(%<\\_ObjId "
                + number
                + ">%).Area \\f \"%lu2%pr2\">% on %<\\AcVar Date \\f \"dd/MM/yyyy\">%";
            CadFields.Set(text, codes, Context(document));
            string words = text.TextString;
            Assert.Equal("A 314.16 on 17/11/2026", words);

            // As a DXF.
            DxfDatabase again = DxfDatabase.Parse(document.Database.ToText());
            DxfDBText fromDxf = again.ModelSpace.OfType<DxfDBText>().First();
            CadDocument reopened = CadDocument.From(again, "again.dxf");
            Assert.Equal(codes, CadFields.CodesOf(fromDxf));
            Assert.Equal(words, fromDxf.TextString);
            Assert.Equal(new[] { "314.16", "17/11/2026" }, CadFields.ShownOf(fromDxf));

            // As a DWG.
            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(document.Database, stream, version);
                stream.Position = 0;
                DxfDatabase fromDwg = DwgReader.Open(stream);
                DxfDBText read = fromDwg.ModelSpace.OfType<DxfDBText>().First();
                Assert.Equal(codes, CadFields.CodesOf(read));
                Assert.Equal(words, read.TextString);
                Assert.Equal(new[] { "314.16", "17/11/2026" }, CadFields.ShownOf(read));
            }
        }

        [Fact]
        public void ADrawingOlderThanR2004HoldsNoField_SoTheWordsAreWrittenAsTheyComeOut()
        {
            CadDocument document = Plan("AC1015");
            DxfDBText text = First<DxfDBText>(document);

            Assert.False(CadFields.Set(text, "By %<\\AcVar Login>%", Context(document)));

            Assert.Equal("By Jane", text.TextString);
            Assert.False(CadFields.Has(text));
        }

        [Fact]
        public void FIELDEVAL_KeepsTheBitsInTheDrawing_AndStartsAt31()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Assert.Equal(31, CadFields.Eval(document.Database));
            Assert.Equal("31", session.Registry.FindVariable("FIELDEVAL").GetValue(session));

            Run(session, "FIELDEVAL", "7");

            Assert.Equal(7, CadFields.Eval(document.Database));
            Assert.Equal(7, CadFields.Eval(DxfDatabase.Parse(document.Database.ToText())));
            Assert.True(document.Undo());
            Assert.Equal(31, CadFields.Eval(document.Database));
        }

        [Fact]
        public void REGEN_EvaluatesTheFieldsAgainOnlyWhereFIELDEVALHoldsTheRegenBit()
        {
            CadDocument document = Plan();
            DxfDBText text = First<DxfDBText>(document);
            string handle = text.Handle;
            var old = new CadFieldContext(document) { Now = new DateTime(2000, 1, 2), Culture = English };
            CadFields.Set(text, "%<\\AcVar Date \\f \"yyyy\">%", old);
            Assert.Equal("2000", text.TextString);

            CadFields.SetEval(document.Database, 15);
            Run(Session(document), "REGEN");
            Assert.Equal("2000", Entity<DxfDBText>(document, handle).TextString);

            CadFields.SetEval(document.Database, 31);
            Run(Session(document), "REGEN");
            Assert.Equal(DateTime.Now.Year.ToString(English), Entity<DxfDBText>(document, handle).TextString);
        }

        [Fact]
        public void ADrawingOpenedEvaluatesItsFieldsAgain_OnlyWhereFIELDEVALHoldsTheOpenBit()
        {
            CadDocument document = Plan();
            var old = new CadFieldContext(document) { Now = new DateTime(2000, 1, 2), Culture = English };
            CadFields.Set(First<DxfDBText>(document), "%<\\AcVar Date \\f \"yyyy\">%", old);
            CadFields.SetEval(document.Database, 30);
            string saved = document.Database.ToText();

            CadDocument skipped = CadDocument.From(DxfDatabase.Parse(saved), "a.dxf");
            Assert.Equal("2000", First<DxfDBText>(skipped).TextString);

            CadFields.SetEval(document.Database, 31);
            CadDocument opened = CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "b.dxf");
            Assert.Equal(DateTime.Now.Year.ToString(English), First<DxfDBText>(opened).TextString);
        }

        [Fact]
        public void FIELDDISPLAY_IsOneAtTheStart_AndSetByTheVariable()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Assert.True(document.FieldDisplay);
            Assert.Equal("1", session.Registry.FindVariable("FIELDDISPLAY").GetValue(session));

            Run(session, "FIELDDISPLAY", "0");

            Assert.False(document.FieldDisplay);
        }

        [Fact]
        public void FIELD_ShowsTheDialog_AsksForAnObjectBetween_AndMakesAnMTextOfTheFieldAtTheStartPoint()
        {
            CadDocument document = Plan();
            DxfCircle circle = First<DxfCircle>(document);
            CadCommandSession session = Session(document);
            int shown = 0;
            session.UiRequested += (s, e) =>
            {
                if (e.Name != "FIELD")
                {
                    return;
                }

                var request = (CadFieldRequest)e.Payload;
                shown++;
                e.Handled = true;

                if (shown == 1)
                {
                    request.Choose(CadFieldRequest.NamesOf("Objects").First(n => n.Name == "Object"));
                    request.Action = CadFieldAction.SelectObject;
                    return;
                }

                Assert.Equal(circle.Handle, request.ObjectHandle);
                Assert.Contains(request.Properties, p => p.Name == "Area");
                request.ChooseProperty("Area");
                request.Precision = 1;
                Assert.Equal("314.2", request.Preview);
                Assert.StartsWith("%<\\AcObjProp Object(%<\\_ObjId ", request.Code);
                Assert.EndsWith(").Area \\f \"%lu2%pr1\">%", request.Code);
                request.Action = CadFieldAction.Ok;
            };

            Run(session, "FIELD");
            Assert.Equal("Select an object:", session.PromptText);
            Run(session, (object)new[] { circle.Handle });
            Assert.Contains("Specify start point of multiline text", session.PromptText);
            Run(session, P(10, 20));

            Assert.Equal(2, shown);
            DxfMText made = document.Database.ModelSpace.OfType<DxfMText>().Single();
            Assert.Equal("314.2", made.Contents);
            Assert.True(CadFields.Has(made));
            Assert.Equal(10.0, made.Location.X, 6);
            Assert.Equal(20.0, made.Location.Y, 6);
            Assert.Equal("FIELD", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Empty(document.Database.ModelSpace.OfType<DxfMText>());
        }

        [Fact]
        public void FIELD_CancelledInTheDialog_MakesNothing()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            session.UiRequested += (s, e) =>
            {
                e.Handled = true;
                ((CadFieldRequest)e.Payload).Action = CadFieldAction.Cancel;
            };

            Run(session, "FIELD");

            Assert.Empty(document.Database.ModelSpace.OfType<DxfMText>());
            Assert.Equal(
                string.Empty,
                session.PromptText.Trim().Length == 0 ? string.Empty
                    : "Command:".Equals(session.PromptText) ? string.Empty
                    : session.PromptText
            );
        }

        [Fact]
        public void TheDialogGivesTheFieldExpressionOfWhatItsOptionsSay_AndLoadsAFieldBackToEditIt()
        {
            CadDocument document = Plan();
            var request = new CadFieldRequest(Context(document));

            Assert.Equal("%<\\AcVar Date \\f \"M/d/yyyy\">%", request.Code);
            Assert.Equal("11/17/2026", request.Preview);
            request.DateFormat = "dd/MM/yyyy";
            Assert.Equal("%<\\AcVar Date \\f \"dd/MM/yyyy\">%", request.Code);
            Assert.Equal("17/11/2026", request.Preview);

            request.SetCategory("Document");
            Assert.Contains(
                request.Name.Name,
                new[]
                {
                    "Author",
                    "Comments",
                    "Filename",
                    "Filesize",
                    "HyperlinkBase",
                    "Keywords",
                    "LastSavedBy",
                    "Subject",
                    "Title",
                }
            );
            request.Choose(CadFieldRequest.NamesOf("Document").First(n => n.Name == "Filename"));
            request.FormatIndex = 2;
            Assert.Equal("%<\\AcVar Filename \\f \"%fn2\">%", request.Code);
            Assert.Equal("plan", request.Preview);

            var again = new CadFieldRequest(Context(document));
            Assert.True(again.Load("%<\\AcVar Date \\f \"dd/MM/yyyy\">%"));
            Assert.Equal("Date", again.Name.Name);
            Assert.Equal("dd/MM/yyyy", again.DateFormat);
            Assert.True(again.Load("%<\\AcExpr 2*3+1>%"));
            Assert.Equal("Formula", again.Name.Name);
            Assert.Equal("2*3+1", again.Expression);
            Assert.Equal("7", again.Preview);
            Assert.False(again.Load("not a field"));
        }

        [Fact]
        public void TheCategoriesAreThoseOfAutoCAD_EachNameInTheCategoryItBelongsTo_AllHoldingEvery()
        {
            Assert.Equal(
                new[] { "All", "Date & Time", "Document", "Linked", "Objects", "Other", "Plot" },
                CadFieldRequest.CategoryNames
            );
            Assert.Equal(
                new[] { "CreateDate", "Date", "PlotDate", "SaveDate" },
                CadFieldRequest.NamesOf("Date & Time").Select(n => n.Name)
            );
            Assert.Equal(new[] { "Formula", "Object" }, CadFieldRequest.NamesOf("Objects").Select(n => n.Name));
            Assert.Equal(
                new[] { "DieselExpression", "SystemVariable" },
                CadFieldRequest.NamesOf("Other").Select(n => n.Name)
            );
            Assert.Equal(
                CadFieldRequest.CategoryNames.Skip(1).SelectMany(c => CadFieldRequest.NamesOf(c)).Count(),
                CadFieldRequest.NamesOf("All").Count
            );
        }

        [Fact]
        public void TEXTEDIT_OfATextWithFields_AsksForTheWordsWithTheirCodes_AndTheWordsEachFieldShows()
        {
            CadDocument document = Plan();
            DxfDBText text = First<DxfDBText>(document);
            string handle = text.Handle;
            CadFields.Set(text, "By %<\\AcVar Login>% on %<\\AcVar Date \\f \"yyyy\">%", Context(document));
            CadCommandSession session = Run(Session(document), "TEXTEDIT", new[] { handle });

            CadInPlaceEdit edit = session.Prompt.InPlace;
            Assert.Equal("By %<\\AcVar Login>% on %<\\AcVar Date \\f \"yyyy\">%", edit.Value);

            // Words of another field, typed in the box as its code, are made a field; the date is evaluated again.
            Run(session, "By %<\\AcVar Login>% on %<\\AcVar Date \\f \"yyyy\">% in %<\\AcVar CTAB>%");

            DxfDBText edited = Entity<DxfDBText>(document, handle);
            Assert.Equal(
                "By " + Environment.UserName + " on " + DateTime.Now.Year.ToString(English) + " in Model",
                edited.TextString
            );
            Assert.Equal(3, CadFields.ShownOf(edited).Count);
            Assert.Equal("TEXTEDIT", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal("By Jane on 2026", Entity<DxfDBText>(document, handle).TextString);
        }

        [Fact]
        public void TheMTextEditorOfWordsWithFields_ShowsEachFieldAsOnePlace_AndWritesItsCode()
        {
            const string codes =
                "Area %<\\AcObjProp Object(%<\\_ObjId 122>%).Area \\f \"%lu2%pr2\">% and %<\\AcVar Author>%";
            var editor = new CadMTextEditor(
                codes,
                2.5,
                CadMTextFrame.Of(0.0),
                1.0,
                null,
                false,
                'l',
                new[] { "314.16", "Jane" }
            );

            Assert.Equal("Area 314.16 and Jane", editor.Content.PlainText);
            Assert.Equal(codes, editor.Contents);
            Assert.True(editor.HasFields);
            Assert.False(editor.IsModified);
            Assert.NotNull(editor.FieldCodeAt(5));
            Assert.Equal("%<\\AcObjProp Object(%<\\_ObjId 122>%).Area \\f \"%lu2%pr2\">%", editor.FieldCodeAt(5));
            Assert.Null(editor.FieldCodeAt(0));

            editor.MoveTo(editor.Length);
            editor.InsertField("Nov", "%<\\AcVar Date \\f \"MMM\">%");
            Assert.Equal(codes + "%<\\AcVar Date \\f \"MMM\">%", editor.Contents);
            Assert.True(editor.IsModified);

            editor.ReplaceField(5, "50.00", "%<\\AcObjProp Object(%<\\_ObjId 122>%).Length \\f \"%lu2%pr2\">%");
            Assert.Equal("Area 50.00 and Jane" + "Nov", editor.Content.PlainText);
            Assert.StartsWith("Area %<\\AcObjProp Object(%<\\_ObjId 122>%).Length", editor.Contents);
            Assert.Throws<ArgumentException>(() => editor.ReplaceField(0, "x", "%<\\AcVar Date>%"));
            Assert.Throws<ArgumentException>(() => editor.InsertField("x", "not a field"));
        }

        [Fact]
        public void AnMTextMadeWithAFieldInItsEditor_IsMadeWithTheFieldEvaluated()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "MTEXT", P(0, 0), P(50, -10));
            Assert.Equal(CadInPlaceKind.MText, session.Prompt.InPlace.Kind);

            Run(session, "Title %<\\AcVar Login>%");

            DxfMText made = document.Database.ModelSpace.OfType<DxfMText>().Single();
            Assert.Equal("Title " + Environment.UserName, made.Contents);
            Assert.True(CadFields.Has(made));
            Assert.Equal("Title %<\\AcVar Login>%", CadFields.CodesOf(made));
        }

        [Fact]
        public void TheContentsOfATextWithFieldsSetInProperties_AreWords_SoTheFieldGoes()
        {
            CadDocument document = Plan();
            DxfDBText text = First<DxfDBText>(document);
            string handle = text.Handle;
            CadFields.Set(text, "By %<\\AcVar Login>%", Context(document));

            Assert.True(CadPropertyEditor.Set(document, new[] { handle }, "Contents", "Plain").Succeeded);

            Assert.Equal("Plain", Entity<DxfDBText>(document, handle).TextString);
            Assert.False(CadFields.Has(Entity<DxfDBText>(document, handle)));
        }

        [Fact]
        public void TheGreyGroundOfAField_IsKeptOnTheTextsItem_WhereFIELDDISPLAYIsOne_AndOnlyThen()
        {
            CadDocument document = Plan();
            DxfDBText text = First<DxfDBText>(document);
            string handle = text.Handle;
            CadFields.Set(text, "By %<\\AcVar Login>%", Context(document));
            document.Regen();

            DisplayText2 ground = document
                .GetDisplay(document.ModelSpace)
                .Items.OfType<DisplayText2>()
                .Single(i => i.Handle == handle);
            Assert.True(ground.IsField);

            Run(Session(document), "FIELDDISPLAY", "0");

            DisplayText2 plain = document
                .GetDisplay(document.ModelSpace)
                .Items.OfType<DisplayText2>()
                .Single(i => i.Handle == handle);
            Assert.False(plain.IsField);
            DisplayText2 other = document
                .GetDisplay(document.ModelSpace)
                .Items.OfType<DisplayText2>()
                .SingleOrDefault(i => i.Handle != handle);
            Assert.Null(other);
        }

        [Fact]
        public void APlotAndASave_EvaluateTheFieldsAgainOnlyWhereFIELDEVALHoldsTheirBit()
        {
            CadDocument document = Plan();
            DxfDBText text = First<DxfDBText>(document);
            string handle = text.Handle;
            var old = new CadFieldContext(document) { Now = new DateTime(2000, 1, 2), Culture = English };
            CadFields.Set(text, "%<\\AcVar Date \\f \"yyyy\">%", old);
            string year = DateTime.Now.Year.ToString(English);

            // No plot bit, no save bit: nothing is evaluated again.
            CadFields.SetEval(document.Database, 25);
            Session(document).Request("-PLOT", document);
            Assert.Equal("2000", Entity<DxfDBText>(document, handle).TextString);
            string path = Path.Combine(Path.GetTempPath(), "fields-" + Guid.NewGuid().ToString("N") + ".dxf");

            try
            {
                document.SaveAs(path);
                Assert.Equal("2000", Entity<DxfDBText>(document, handle).TextString);

                CadFields.SetEval(document.Database, 27);
                document.SaveAs(path);
                Assert.Equal(year, Entity<DxfDBText>(document, handle).TextString);
                Assert.Equal(year, First<DxfDBText>(CadDocument.Open(path)).TextString);
            }
            finally
            {
                File.Delete(path);
            }

            CadFields.Set(Entity<DxfDBText>(document, handle), "%<\\AcVar Date \\f \"yyyy\">%", old);
            CadFields.SetEval(document.Database, 31);
            Session(document).Request("-PLOT", document);
            Assert.Equal(year, Entity<DxfDBText>(document, handle).TextString);
        }
    }
}
