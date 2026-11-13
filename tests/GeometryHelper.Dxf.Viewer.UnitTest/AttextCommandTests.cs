using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// ATTEXT and -ATTEXT, each checked against what AutoCAD 2021's console wrote for the same drawing and templates: the
    /// prompts word for word, a comma-delimited and a space-delimited file field by field, the DXF extract's records, the
    /// numbers a block field gets, and what is told of a template or a value that does not fit.
    /// </summary>
    public class AttextCommandTests : IDisposable
    {
        private const string Type = "Enter extraction type or enable object selection [Cdf/Sdf/Dxf/Objects] <C>:";

        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "attext-" + Guid.NewGuid().ToString("N")
        );

        public AttextCommandTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        private string At(string name) => Path.Combine(_folder, name);

        private string Template(string name, params string[] lines)
        {
            string path = At(name);
            File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n");
            return path;
        }

        private string[] Lines(string name) =>
            File.ReadAllText(At(name)).Split(new[] { "\r\n" }, StringSplitOptions.None);

        /// <summary>
        /// A drawing saved in the temporary folder and opened, with the block ITEM (NAME, PRICE), the block PLATE (NAME, NOTE)
        /// and the block PLAIN, which has no attribute, and inserts in this order: ITEM Bolt M8 at (10, 20); ITEM Nut on layer
        /// PARTS at (30.5, 40.25, 5) turned 30 degrees and scaled 2 by 3; ITEM "Washer, flat" at (-5, 0) with the price "abc";
        /// PLATE at (100, 100); PLAIN; ITEM Last at (0, 0).
        /// </summary>
        private CadDocument Parts(out List<string> handles)
        {
            DxfDatabase db = CadTemplate.CreateMetric();
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "PARTS" });
            DxfBlockTableRecord item = Block(db, "ITEM", "NAME", "PRICE");
            DxfBlockTableRecord plate = Block(db, "PLATE", "NAME", "NOTE");
            DxfBlockTableRecord plain = Block(db, "PLAIN");
            var made = new List<DxfBlockReference>
            {
                Insert(db, item, new GeoPoint3(10, 20, 0), "0", 0.0, 1.0, 1.0, "Bolt M8", "1.5"),
                Insert(db, item, new GeoPoint3(30.5, 40.25, 5), "PARTS", Math.PI / 6.0, 2.0, 3.0, "Nut", "12"),
                Insert(db, item, new GeoPoint3(-5, 0, 0), "0", 0.0, 1.0, 1.0, "Washer, flat", "abc"),
                Insert(db, plate, new GeoPoint3(100, 100, 0), "0", 0.0, 1.0, 1.0, "Plate A", "thin"),
                Insert(db, plain, new GeoPoint3(1, 1, 0), "0", 0.0, 1.0, 1.0),
                Insert(db, item, new GeoPoint3(0, 0, 0), "0", 0.0, 1.0, 1.0, "Last", "9"),
            };
            handles = made.Select(m => m.Handle).ToList();
            string path = At("parts.dxf");
            db.Save(path);
            return CadDocument.Open(path);
        }

        private static DxfBlockTableRecord Block(DxfDatabase db, string name, params string[] tags)
        {
            var block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 5.0));
            double y = 0.0;

            foreach (string tag in tags)
            {
                block.AppendEntity(new DxfAttributeDefinition(new GeoPoint3(0, y, 0), string.Empty, tag, tag, null));
                y -= 4.0;
            }

            return block;
        }

        private static DxfBlockReference Insert(
            DxfDatabase db,
            DxfBlockTableRecord block,
            GeoPoint3 at,
            string layer,
            double turn,
            double scaleX,
            double scaleY,
            params string[] values
        )
        {
            var insert = new DxfBlockReference(at, block) { Layer = layer };
            db.ModelSpace.AppendEntity(insert);
            insert.Rotation = turn;
            insert.ScaleFactors = new GeoVector3(scaleX, scaleY, 1.0);
            int i = 0;

            foreach (DxfAttributeDefinition definition in block.OfType<DxfAttributeDefinition>())
            {
                var attribute = new DxfAttributeReference();
                attribute.SetAttributeFromBlock(definition, insert.BlockTransform);
                attribute.TextString = values[i++];
                insert.AttributeCollection.AppendAttribute(attribute);
            }

            return insert;
        }

        private string FullTemplate() =>
            Template(
                "full.txt",
                "BL:LEVEL     N002000",
                "BL:NAME      C008000",
                "BL:X         N007002",
                "BL:Y         N007002",
                "BL:NUMBER    N003000",
                "BL:LAYER     C008000",
                "BL:ORIENT    N008003",
                "BL:XSCALE    N006002",
                "NAME         C012000",
                "PRICE        N008002"
            );

        [Fact]
        public void ACommaDelimitedExtract_HasTheFieldsOfTheTemplateInOrder_AsAutoCadWritesThem()
        {
            CadDocument document = Parts(out _);
            CadCommandSession session = Run(Session(document), "DDATTEXT");

            Assert.Equal(Type, session.PromptText);
            Run(session, "C");
            Assert.Equal("Enter template file name:", session.PromptText);
            Run(session, FullTemplate());
            Assert.Equal("Enter extract file name <parts>:", session.PromptText);
            Run(session, At("out.txt"));

            Assert.False(session.IsRunning);
            Assert.Equal(
                new[]
                {
                    " 1,'ITEM', 10.00, 20.00, 1,'0', 0.000, 1.00,'Bolt M8', 1.50",
                    " 1,'ITEM', 30.50, 40.25, 2,'PARTS', 30.000, 2.00,'Nut', 12.00",
                    " 1,'ITEM',-5.00, 0.00, 3,'0', 0.000, 1.00,'Washer, flat', 0.00",
                    " 1,'PLATE', 100.00, 100.00, 4,'0', 0.000, 1.00,'Plate A', 0.00",
                    " 1,'ITEM', 0.00, 0.00, 6,'0', 0.000, 1.00,'Last', 9.00",
                    string.Empty,
                },
                Lines("out.txt")
            );
            Assert.Contains("** Bad numeric value for field PRICE, record 3", session.History);
            Assert.Equal("5 records in extract file.", session.History.Last());
        }

        [Fact]
        public void EveryBlockField_GivesWhatAutoCadWrote_ForTheTwoFirstInsertsOfItsConsole()
        {
            CadDocument document = Parts(out List<string> handles);
            string template = Template(
                "every.txt",
                "BL:LEVEL     N002000",
                "BL:NAME      C008000",
                "BL:X         N007002",
                "BL:Y         N007002",
                "BL:Z         N005001",
                "BL:NUMBER    N003000",
                "BL:HANDLE    C005000",
                "BL:LAYER     C008000",
                "BL:ORIENT    N008003",
                "BL:XSCALE    N006002",
                "BL:YSCALE    N006002",
                "BL:ZSCALE    N006002",
                "BL:XEXTRUDE  N005001",
                "BL:YEXTRUDE  N005001",
                "BL:ZEXTRUDE  N005001",
                "NAME         C012000",
                "PRICE        N008002"
            );
            Run(Session(document), "-ATTEXT", "C", template, "every-cdf");
            Run(Session(document), "-ATTEXT", "S", template, "every-sdf");

            // The lines of AutoCAD 2021's -ATTEXT for these inserts, with the handles of this drawing for those of its own.
            Assert.Equal(
                " 1,'ITEM', 10.00, 20.00, 0.0, 1,'"
                    + handles[0]
                    + "','0', 0.000, 1.00, 1.00, 1.00, 0.0, 0.0, 1.0,'Bolt M8', 1.50",
                Lines("every-cdf.txt")[0]
            );
            Assert.Equal(
                " 1,'ITEM', 30.50, 40.25, 5.0, 2,'"
                    + handles[1]
                    + "','PARTS', 30.000, 2.00, 3.00, 1.00, 0.0, 0.0, 1.0,'Nut', 12.00",
                Lines("every-cdf.txt")[1]
            );
            Assert.Equal(
                " 1ITEM      10.00  20.00  0.0  1"
                    + handles[0].PadRight(5)
                    + "0          0.000  1.00  1.00  1.00  0.0  0.0  1.0Bolt M8         1.50",
                Lines("every-sdf.txt")[0]
            );
            Assert.Equal(
                " 1ITEM      30.50  40.25  5.0  2"
                    + handles[1].PadRight(5)
                    + "PARTS     30.000  2.00  3.00  1.00  0.0  0.0  1.0Nut            12.00",
                Lines("every-sdf.txt")[1]
            );
        }

        [Fact]
        public void ASpaceDelimitedExtract_FillsEachFieldToItsWidth_NumbersRightAndTextLeft()
        {
            CadDocument document = Parts(out _);
            Run(Session(document), "-ATTEXT", "S", FullTemplate(), At("out.txt"));

            string[] lines = Lines("out.txt");
            Assert.Equal(6, lines.Length);
            Assert.Equal(
                " 1ITEM      10.00  20.00  1" + "0       " + "   0.000" + "  1.00" + "Bolt M8     " + "    1.50",
                lines[0]
            );
            Assert.Equal(
                " 1ITEM      30.50  40.25  2" + "PARTS   " + "  30.000" + "  2.00" + "Nut         " + "   12.00",
                lines[1]
            );
            Assert.Equal(
                " 1ITEM      -5.00   0.00  3" + "0       " + "   0.000" + "  1.00" + "Washer, flat" + "    0.00",
                lines[2]
            );
            Assert.All(lines.Take(5), l => Assert.Equal(69, l.Length));
        }

        [Fact]
        public void ATextTooLongForItsField_IsCut_AndANumberToo_ToldOncePerRecord()
        {
            CadDocument document = Parts(out _);
            string narrow = Template(
                "narrow.txt",
                "NAME         C003000",
                "PRICE        N003001",
                "BL:NAME      C002000"
            );
            CadCommandSession session = Run(Session(document), "-ATTEXT", "C", narrow, At("narrow-out.txt"));

            Assert.Equal(
                new[]
                {
                    "'Bol',1.5,'IT'",
                    "'Nut',12.,'IT'",
                    "'Was',0.0,'IT'",
                    "'Pla',0.0,'PL'",
                    "'Las',9.0,'IT'",
                    string.Empty,
                },
                Lines("narrow-out.txt")
            );
            string[] told = session.History.Where(l => l.StartsWith("** ")).ToArray();
            Assert.Equal(
                new[]
                {
                    "** Field overflow in record 1",
                    "** Field overflow in record 2",
                    "** Bad numeric value for field PRICE, record 3",
                    "** Field overflow in record 3",
                    "** Field overflow in record 4",
                    "** Field overflow in record 5",
                },
                told
            );

            Run(session, "-ATTEXT", "S", narrow, At("narrow-fixed.txt"));
            Assert.Equal("Bol1.5IT", Lines("narrow-fixed.txt")[0]);
            Assert.Equal("Nut12.IT", Lines("narrow-fixed.txt")[1]);
        }

        [Fact]
        public void TheDelimiterAndTheQuote_AreTheTemplatesOwn_AndTheFieldsMayBeInAnyCase()
        {
            CadDocument document = Parts(out _);
            string custom = Template(
                "custom.txt",
                "C:DELIM      ;",
                "C:QUOTE      \"",
                "name         c012000",
                "price        n008002"
            );
            Run(Session(document), "-ATTEXT", "C", custom, At("custom-out.txt"));

            Assert.Equal("\"Bolt M8\"; 1.50", Lines("custom-out.txt")[0]);
            Assert.Equal("\"Washer, flat\"; 0.00", Lines("custom-out.txt")[2]);
        }

        [Fact]
        public void AnAttributeTheBlockHasNot_IsBlankOrNought_AndATemplateFieldOfAnotherTagIsOneToo()
        {
            CadDocument document = Parts(out _);
            string template = Template(
                "tags.txt",
                "NOTE         C010000",
                "BL:FOO       N001000",
                "PRICE        N005001"
            );
            Run(Session(document), "-ATTEXT", "C", template, At("tags-out.txt"));

            Assert.Equal(
                new[] { "'',0, 1.5", "'',0, 12.0", "'',0, 0.0", "'thin',0, 0.0", "'',0, 9.0", string.Empty },
                Lines("tags-out.txt")
            );
        }

        [Fact]
        public void ADxfExtract_HoldsTheInsertsTheirAttributesAndTheirEnds_AsAutoCadWritesIt()
        {
            CadDocument document = Parts(out List<string> handles);
            CadCommandSession session = Run(Session(document), "ATTEXT", "D");

            Assert.Equal("Enter extract file name <parts>:", session.PromptText);
            Run(session, At("out.dxx"));

            Assert.Equal("20 objects in extract file.", session.History.Last());
            string[] lines = File.ReadAllText(At("out.dxx")).Split(new[] { "\r\n" }, StringSplitOptions.None);
            string text = string.Join("|", lines);
            Assert.StartsWith(
                "  0|INSERT|  5|"
                    + handles[0]
                    + "|100|AcDbEntity|  8|0| 66|     1|100|AcDbBlockReference|  2|ITEM| 10|10.0| 20|20.0| 30|0.0|  0|ATTRIB|",
                text
            );
            Assert.EndsWith("|  0|SEQEND|100|AcDbEntity|  8|0|100|AcDbSequenceEnd|  0|EOF|", text);

            // The second insert is turned and scaled; the scale along z, which is 1, and a turn of nought are left out.
            Assert.Contains(
                "  0|INSERT|  5|"
                    + handles[1]
                    + "|100|AcDbEntity|  8|PARTS| 66|     1|100|AcDbBlockReference|  2|ITEM| 10|30.5| 20|40.25| 30|5.0| 41|2.0| 42|3.0| 50|30.0|",
                text
            );
            Assert.Contains("|100|AcDbAttribute|  1|Bolt M8|  2|NAME| 70|     0|", text);
            Assert.Equal(5, lines.Count(l => l == "INSERT"));
            Assert.Equal(5, lines.Count(l => l == "SEQEND"));
            Assert.Equal(10, lines.Count(l => l == "ATTRIB"));
        }

        [Fact]
        public void Objects_AsksForTheObjectsAndThenTheType_AndExtractsOnlyTheInsertsSelected()
        {
            CadDocument document = Parts(out List<string> handles);
            CadCommandSession session = Run(Session(document), "-ATTEXT");
            Assert.Equal(Type, session.PromptText);
            Run(session, "O");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { handles[3], handles[0] }, Enter);
            Assert.Equal("Enter attribute extraction type [Cdf/Sdf/Dxf] <C>:", session.PromptText);
            Run(session, "C");
            string template = Template("names.txt", "BL:NAME      C008000", "NAME         C012000");
            Run(session, template, At("objects.txt"));

            Assert.Equal(new[] { "'ITEM','Bolt M8'", "'PLATE','Plate A'", string.Empty }, Lines("objects.txt"));
            Assert.Equal("2 records in extract file.", session.History.Last());
        }

        [Fact]
        public void EnterAtTheTypeIsCdf_AndEnterAtTheTemplateIsTheLastOne_AndAtTheExtractTheDrawingsName()
        {
            CadDocument document = Parts(out _);
            string template = Template("names.txt", "NAME         C012000");
            CadCommandSession session = Run(Session(document), "-ATTEXT", Enter, template, "first.txt");
            Assert.Equal(
                new[] { "'Bolt M8'", "'Nut'", "'Washer, flat'", "'Plate A'", "'Last'", string.Empty },
                Lines("first.txt")
            );

            // The template is offered the next time, as AutoCAD offers it; the extract's name is the drawing's, ".txt" given it.
            Run(session, "-ATTEXT", "C");
            Assert.Equal("Enter template file name <" + template + ">:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Enter extract file name <parts>:", session.PromptText);
            Run(session, Enter);
            Assert.True(File.Exists(At("parts.txt")));

            Run(session, "-ATTEXT", "D", Enter);
            Assert.True(File.Exists(At("parts.dxx")));
        }

        [Fact]
        public void ATemplateThatIsNotFound_IsToldAndAskedAgain_ANameWithNoExtensionGetsTxt()
        {
            CadDocument document = Parts(out _);
            Template("names.txt", "NAME         C012000");
            CadCommandSession session = Run(Session(document), "-ATTEXT", "C", At("nothere.txt"));

            Assert.Equal("\"nothere.txt\": Can't find file.", session.History.Last());
            Assert.Equal("Enter template file name:", session.PromptText);
            Run(session, At("names"), "second");
            Assert.False(session.IsRunning);
            Assert.True(File.Exists(At("second.txt")));
        }

        [Fact]
        public void ATemplateThatDoesNotFit_IsToldAsAutoCadTellsIt_AndNothingIsWritten()
        {
            CadDocument document = Parts(out _);

            foreach (
                var fault in new[]
                {
                    new[] { "** Invalid field specification:", "NAME         Z012000" },
                    new[] { "** Invalid field specification:", "NAME" },
                    new[] { "** Duplicate field specification:", "name         C012000" },
                }
            )
            {
                string template = Template("bad.txt", "NAME         C012000", fault[1], "PRICE        N008002");

                if (fault[0].StartsWith("** Duplicate", StringComparison.Ordinal))
                {
                    template = Template("bad.txt", "NAME         C012000", "PRICE        N008002", fault[1]);
                }

                CadCommandSession session = Run(Session(document), "-ATTEXT", "C", template);

                Assert.False(session.IsRunning);
                IReadOnlyList<string> history = session.History;
                Assert.Equal(fault[0], history[history.Count - 2]);
                Assert.Equal(fault[1], history[history.Count - 1]);
            }

            CadCommandSession empty = Run(Session(document), "-ATTEXT", "C", Template("empty.txt"));
            Assert.Equal("** No Attribute fields specified.", empty.History.Last());
            Assert.False(empty.IsRunning);
        }

        [Fact]
        public void TheExtractFileMayNotBeTheTemplate_ItIsAskedAgain()
        {
            CadDocument document = Parts(out _);
            string template = Template("names.txt", "NAME         C012000");
            CadCommandSession session = Run(Session(document), "-ATTEXT", "C", template, template);

            Assert.Equal("The template and extract files cannot be the same.", session.History.Last());
            Assert.Equal("Enter extract file name <parts>:", session.PromptText);
            Assert.Equal("NAME         C012000", File.ReadAllLines(template)[0]);
            Run(session, "other");
            Assert.True(File.Exists(At("other.txt")));
        }

        [Fact]
        public void ADrawingWithNoBlockWithAttributes_GivesAnEmptyFile_AndSaysSo()
        {
            DxfDatabase db = CadTemplate.CreateMetric();
            string path = At("empty.dxf");
            db.Save(path);
            CadDocument document = CadDocument.Open(path);
            string template = Template("names.txt", "NAME         C012000");

            CadCommandSession session = Run(Session(document), "-ATTEXT", "C", template, "none");
            Assert.Equal("0 records in extract file.", session.History.Last());
            Assert.Equal(0, new FileInfo(At("none.txt")).Length);

            Run(session, "-ATTEXT", "D", "none");
            Assert.Equal("0 objects in extract file.", session.History.Last());
            Assert.Equal("  0\r\nEOF\r\n", File.ReadAllText(At("none.dxx")));
        }

        [Fact]
        public void TheDialog_SelectsObjectsAndShowsAgain_ThenWritesTheExtractOfTheTemplate()
        {
            CadDocument document = Parts(out List<string> handles);
            CadCommandSession session = Session(document);
            string template = FullTemplate();
            var shown = new List<int>();
            session.UiRequested += (sender, e) =>
            {
                if (e.Name != "ATTEXT")
                {
                    return;
                }

                var request = (CadAttextRequest)e.Payload;
                shown.Add(request.Selected?.Count ?? -1);
                e.Handled = true;

                if (shown.Count == 1)
                {
                    Assert.Equal(CadAttextFormat.Cdf, request.Format);
                    Assert.Equal(At("parts.txt"), request.OutputFile);
                    request.IsSelecting = true;
                    return;
                }

                request.TemplateFile = template;
                request.OutputFile = At("chosen");
                request.IsAccepted = true;
            };

            Run(session, "ATTEXT", new[] { handles[0], handles[3] }, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(new[] { -1, 2 }, shown);
            Assert.Equal("2 records in extract file.", session.History.Last());
            Assert.Equal(
                new[]
                {
                    " 1,'ITEM', 10.00, 20.00, 1,'0', 0.000, 1.00,'Bolt M8', 1.50",
                    " 1,'PLATE', 100.00, 100.00, 2,'0', 0.000, 1.00,'Plate A', 0.00",
                    string.Empty,
                },
                Lines("chosen.txt")
            );
        }

        [Fact]
        public void TheDialog_RefusesATemplateThatIsNotThere_AndWritesNothing()
        {
            CadDocument document = Parts(out _);
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "ATTEXT")
                {
                    var request = (CadAttextRequest)e.Payload;
                    request.TemplateFile = At("missing.txt");
                    request.IsAccepted = true;
                    e.Handled = true;
                }
            };

            Run(session, "ATTEXT");

            Assert.Equal("\"missing.txt\": Can't find file.", session.History.Last());
            Assert.False(File.Exists(At("parts.txt")));
        }
    }
}
