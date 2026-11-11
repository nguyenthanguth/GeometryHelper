using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// <see cref="DxfDatabase.Recover(Stream, out DxfRepairReport)"/> and <see cref="DxfDatabase.Audit"/>: a sound drawing built in
    /// code, saved, damaged in its text in exactly one way, and recovered; the report names the damage, the other entities are
    /// as they were, and the drawing saved after is opened by <see cref="DxfDatabase.Open(Stream)"/> and recovered with nothing
    /// more to repair.
    /// </summary>
    public class DxfRecoverTests
    {
        /// <summary>
        /// A drawing of R2018: a block DOOR of a line 900 long and a circle of radius 25, inserted 12 times 1 000 apart, each
        /// INSERT with an attribute; a HATCH over a square of 30, a LWPOLYLINE of four vertices, a 2D POLYLINE of three, a TEXT,
        /// an aligned dimension and three LINEs, 500 long, at y = 1 100, 1 200 and 1 300.
        /// </summary>
        private static string Sound()
        {
            var db = new DxfDatabase("AC1032");
            var door = new DxfBlockTableRecord { Name = "DOOR" };
            db.BlockTable.Add(door);
            door.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(900, 0, 0)));
            door.AppendEntity(new DxfCircle(new GeoPoint3(450, 0, 0), new GeoVector3(0, 0, 1), 25));

            for (int i = 0; i < 12; i++)
            {
                var insert = new DxfBlockReference(new GeoPoint3(1000 * i, 0, 0), door);
                db.ModelSpace.AppendEntity(insert);
                insert.AttributeCollection.AppendAttribute(
                    new DxfAttributeReference(new GeoPoint3(1000 * i, 100, 0), "D" + i, "MARK", null)
                );
            }

            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(0, 500), new GeoPoint2(30, 500), new GeoPoint2(30, 530), new GeoPoint2(0, 530) },
                null
            );
            db.ModelSpace.AppendEntity(hatch);
            var polyline = new DxfPolyline();

            for (int i = 0; i < 4; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(100 * i, 800), 0, 0, 0);
            }

            db.ModelSpace.AppendEntity(polyline);
            db.ModelSpace.AppendEntity(
                new DxfPolyline2d(
                    DxfPoly2dType.SimplePoly,
                    new[] { new GeoPoint3(0, 900, 0), new GeoPoint3(50, 900, 0), new GeoPoint3(50, 950, 0) },
                    0,
                    false,
                    0,
                    0,
                    new[] { 0.0, 0.0, 0.0 }
                )
            );
            db.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    Position = new GeoPoint3(0, 1000, 0),
                    Height = 2.5,
                    TextString = "PLAN",
                }
            );
            var picture = new DxfBlockTableRecord { Name = "*D" };
            db.BlockTable.Add(picture);
            picture.AppendEntity(new DxfLine(new GeoPoint3(0, 1070, 0), new GeoPoint3(100, 1070, 0)));
            db.ModelSpace.AppendEntity(
                new DxfAlignedDimension(
                    new GeoPoint3(0, 1050, 0),
                    new GeoPoint3(100, 1050, 0),
                    new GeoPoint3(50, 1070, 0),
                    string.Empty,
                    null
                )
                {
                    DimBlock = picture,
                }
            );

            for (int i = 1; i <= 3; i++)
            {
                db.ModelSpace.AppendEntity(
                    new DxfLine(new GeoPoint3(0, 1000 + 100 * i, 0), new GeoPoint3(500, 1000 + 100 * i, 0))
                );
            }

            return db.ToText();
        }

        private static DxfDatabase Recovered(string text, out DxfRepairReport report) =>
            Recovered(Encoding.UTF8.GetBytes(text), out report);

        private static DxfDatabase Recovered(byte[] bytes, out DxfRepairReport report)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DxfDatabase.Recover(stream, out report);
            }
        }

        /// <summary>The index of the n-th record of a type, from 0: the LINEs 0 and 1 are those of the blocks DOOR and *D1, 2 to 4 those of model space.</summary>
        private static int Nth(List<string> records, string type, int nth = 0)
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].StartsWith("  0\n" + type + "\n", StringComparison.Ordinal) && nth-- == 0)
                {
                    return i;
                }
            }

            throw new InvalidOperationException("No such record.");
        }

        /// <summary>A record with the first group of a code given another value, or taken out where the value is null.</summary>
        private static string Group(string record, int code, string value)
        {
            string line = "\n" + code.ToString().PadLeft(3) + "\n";
            int at = record.IndexOf(line, StringComparison.Ordinal);
            int end = record.IndexOf('\n', at + line.Length);
            return value == null
                ? record.Remove(at, end - at)
                : record.Substring(0, at + line.Length) + value + record.Substring(end);
        }

        private static string ValueOf(string record, int code)
        {
            string line = "\n" + code.ToString().PadLeft(3) + "\n";
            int at = record.IndexOf(line, StringComparison.Ordinal) + line.Length;
            return record.Substring(at, record.IndexOf('\n', at) - at);
        }

        private static string Damaged(Func<List<string>, List<string>> damage) =>
            string.Concat(damage(Records(Sound())));

        private static List<DxfEntity> Model(DxfDatabase db) => db.ModelSpace.Cast<DxfEntity>().ToList();

        /// <summary>
        /// The entities of the sound drawing are all there but those left out: the 12 INSERTs of DOOR with an attribute each, the
        /// hatch, the polylines of four and three vertices, the text, the dimension and the lines; the block DOOR of two.
        /// </summary>
        private static void Intact(DxfDatabase db, int inserts = 12, int lines = 3, int vertices2d = 3)
        {
            List<DxfEntity> model = Model(db);
            Assert.Equal(inserts, model.OfType<DxfBlockReference>().Count());
            Assert.All(model.OfType<DxfBlockReference>().Where(i => i.Name == "DOOR"), i =>
                Assert.Single(i.AttributeCollection)
            );
            Assert.Single(model.OfType<DxfHatch>());
            Assert.Equal(4, Assert.Single(model.OfType<DxfPolyline>()).NumberOfVertices);
            Assert.Equal(vertices2d, Assert.Single(model.OfType<DxfPolyline2d>()).Count());
            Assert.Single(model.OfType<DxfDBText>());
            Assert.Equal(
                lines,
                model.OfType<DxfLine>().Count(l => Math.Abs(l.EndPoint.X - l.StartPoint.X - 500) < 1E-9)
            );
            Assert.Equal(2, db.BlockTable["DOOR"].Cast<object>().Count());
        }

        /// <summary>The drawing saved is opened as it is, and recovered with nothing more to repair.</summary>
        private static void Rereads(DxfDatabase db, params DxfRepairKind[] stillFound)
        {
            byte[] saved = Saved(db);
            Opened(saved);
            Recovered(saved, out DxfRepairReport again);
            Assert.Equal(stillFound, again.Repairs.Select(r => r.Kind).ToArray());
        }

        private static DxfRepair Only(DxfRepairReport report, DxfRepairKind kind)
        {
            DxfRepair repair = Assert.Single(report.Repairs);
            Assert.Equal(kind, repair.Kind);
            Assert.True(repair.IsFixed, repair.Message);
            return repair;
        }

        [Fact]
        public void ASoundDrawing_IsRecoveredWithNoRepair_AndItsOwnBytes()
        {
            string text = Sound();
            Assert.Equal(text, string.Concat(Records(text)));

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.Empty(report.Repairs);
            Assert.Equal("No errors found.", report.Summary);
            Assert.Equal(Encoding.UTF8.GetBytes(text), Saved(db));
            Intact(db);
            Assert.Empty(db.Audit(false).Repairs);
        }

        [Fact]
        public void ALineThatIsNotAGroupCode_IsLeftOut_AndTheNextReadAsTheCode()
        {
            string text = Damaged(r =>
            {
                int line = Nth(r, "LINE", 1);
                r[line] = r[line].Replace("100\nAcDbLine\n", "100\nAcDbLine\nnot a code\n");
                return r;
            });
            Assert.Throws<FormatException>(() => Opened(Encoding.UTF8.GetBytes(text)));

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            DxfRepair repair = Only(report, DxfRepairKind.BadGroupCode);
            Assert.Equal(
                "Line " + repair.Line + " is not a group code ('not a code'); it was left out.",
                repair.Message
            );
            Assert.Equal("not a code", text.Replace("\r", string.Empty).Split('\n')[repair.Line - 1]);
            Intact(db);
            Rereads(db);
        }

        [Fact]
        public void AFileCutShortAfterACode_IsClosedWithItsEndsecAndEof_TheEntitiesBeforeTheCutKept()
        {
            string sound = Sound();
            int cut = sound.LastIndexOf("100\nAcDbLine\n", StringComparison.Ordinal) + "100\nAcDbLine\n 10\n".Length;
            string text = sound.Substring(0, cut);
            Assert.Throws<FormatException>(() => Opened(Encoding.UTF8.GetBytes(text)));

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.Equal(
                new[] { DxfRepairKind.MissingValue, DxfRepairKind.Truncated, DxfRepairKind.MissingSection },
                report.Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.Equal(2, report.FixedCount);
            Assert.Equal(
                "The file has no OBJECTS section, though its records are of R13 or later: what it held is lost.",
                report.Repairs[2].Message
            );
            Assert.False(report.Repairs[2].IsFixed);

            // The last LINE stands with no point, read as a line from the origin: the two before it are whole.
            Assert.Equal(3, Model(db).OfType<DxfLine>().Count());
            Intact(db, lines: 2);
            Rereads(db, DxfRepairKind.MissingSection);
        }

        [Fact]
        public void EntitiesOutsideEverySection_AreMovedIntoEntities_AndGivenHandles()
        {
            string text = "  0\nLINE\n  8\n0\n 10\n0\n 20\n1400\n 30\n0\n 11\n500\n 21\n1400\n 31\n0\n" + Sound();
            Assert.Throws<FormatException>(() => Opened(Encoding.UTF8.GetBytes(text)));

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.Equal(
                new[] { DxfRepairKind.SectionStructure, DxfRepairKind.MissingHandle },
                report.Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.Equal(
                "The LINE on line 1 stands in no section; it was moved into ENTITIES.",
                report.Repairs[0].Message
            );
            Assert.Equal(report.Count, report.FixedCount);
            Intact(db, lines: 4);
            Rereads(db);
        }

        [Fact]
        public void SectionsOutOfOrder_ArePutInAutoCadsOrder()
        {
            string text = Damaged(r =>
            {
                int tables = Nth(r, "SECTION", 2);
                int end = r.FindIndex(tables, s => s.StartsWith("  0\nENDSEC\n", StringComparison.Ordinal));
                List<string> moved = r.GetRange(tables, end - tables + 1);
                r.RemoveRange(tables, moved.Count);
                r.InsertRange(Nth(r, "SECTION", 4), moved);
                return r;
            });

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.Equal(
                "The sections are not in AutoCAD's order; they were put as HEADER, CLASSES, TABLES, BLOCKS, ENTITIES, OBJECTS.",
                Only(report, DxfRepairKind.SectionOrder).Message
            );
            Intact(db);
            Rereads(db);
        }

        [Fact]
        public void ASectionWithNoEndsec_IsClosedWhereTheNextOpens()
        {
            string text = Damaged(r =>
            {
                r.RemoveAt(
                    r.FindIndex(Nth(r, "SECTION", 3), s => s.StartsWith("  0\nENDSEC\n", StringComparison.Ordinal))
                );
                return r;
            });
            Assert.Throws<FormatException>(() => Opened(Encoding.UTF8.GetBytes(text)));

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.StartsWith(
                "The section BLOCKS opened on line ",
                Only(report, DxfRepairKind.SectionStructure).Message
            );
            Intact(db);
            Rereads(db);
        }

        [Fact]
        public void AFileWithNoHeaderAndNoEntities_IsGivenBoth()
        {
            string text = Damaged(r =>
            {
                foreach (int nth in new[] { 4, 0 })
                {
                    int at = Nth(r, "SECTION", nth);
                    r.RemoveRange(
                        at,
                        r.FindIndex(at, s => s.StartsWith("  0\nENDSEC\n", StringComparison.Ordinal)) - at + 1
                    );
                }

                return r;
            });

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.Equal(
                new[] { DxfRepairKind.MissingSection, DxfRepairKind.MissingSection, DxfRepairKind.HandseedTooLow },
                report.Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.Equal("The file has no ENTITIES section; an empty one was added.", report.Repairs[0].Message);
            Assert.Equal(
                "The file has no HEADER, though its records are of R13 or later; one naming R2000 (AC1015) was added.",
                report.Repairs[1].Message
            );
            Assert.Equal("AC1015", db.Version);
            Assert.Empty(Model(db));
            Assert.Equal(2, db.BlockTable["DOOR"].Cast<object>().Count());
            Rereads(db);
        }

        [Fact]
        public void LinesEndedByACarriageReturnAlone_AreReadAsLines()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(Sound().Replace("\n", "\r"));
            Assert.Throws<FormatException>(() => Opened(bytes));

            DxfDatabase db = Recovered(bytes, out DxfRepairReport report);

            Only(report, DxfRepairKind.LineEnds);
            Intact(db);
            Rereads(db);
        }

        [Fact]
        public void AFileWithItsMarkOfUtf8AndCrLf_KeepsBothThroughARepair()
        {
            string text = Damaged(r =>
                {
                    int line = Nth(r, "LINE", 0);
                    r[line] = r[line].Replace("100\nAcDbLine\n", "100\nAcDbLine\n#\n");
                    return r;
                })
                .Replace("\n", "\r\n");
            byte[] bytes = new byte[] { 0xEF, 0xBB, 0xBF }
                .Concat(Encoding.UTF8.GetBytes(text))
                .ToArray();

            DxfDatabase db = Recovered(bytes, out DxfRepairReport report);

            Only(report, DxfRepairKind.BadGroupCode);
            byte[] saved = Saved(db);
            Assert.Equal(
                new byte[] { 0xEF, 0xBB, 0xBF, (byte)' ', (byte)' ', (byte)'0', (byte)'\r', (byte)'\n' },
                saved.Take(8).ToArray()
            );
            Assert.DoesNotContain("\r\n#\r\n", Encoding.UTF8.GetString(saved));
            Intact(db);
            Rereads(db);
        }

        [Fact]
        public void AFileOfR2018WrittenInLatin1_IsReadInItsCodePageAndWrittenAsUtf8()
        {
            string text = Damaged(r =>
            {
                int line = Nth(r, "LINE", 2);
                r[line] = Group(r[line], 8, "Außen");
                return r;
            });
            byte[] bytes = Encoding.GetEncoding(28591).GetBytes(text);
            Assert.False(Opened(bytes).LayerTable.Has("Außen"));

            DxfDatabase db = Recovered(bytes, out DxfRepairReport report);

            Assert.Equal(
                new[] { DxfRepairKind.Encoding, DxfRepairKind.MissingLayer },
                report.Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.Equal(
                "The file is of AC1032, whose text is UTF-8, but 1 of its values are not UTF-8; they were read in the code page ISO-8859-1 and written as UTF-8.",
                report.Repairs[0].Message
            );
            Assert.True(db.LayerTable.Has("Außen"));
            Assert.Equal("Außen", Model(db).OfType<DxfLine>().First().Layer);
            Intact(db);
            Rereads(db);
        }

        [Fact]
        public void AFileOfR2000WhoseHeaderNamesACodePageNoDxfCanBeIn_IsReadAsAnsi1252()
        {
            var made = new DxfDatabase("AC1015");
            made.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(500, 0, 0)) { Layer = "Außen" }
            );
            Encoding latin1 = Encoding.GetEncoding(28591);
            byte[] bytes = latin1.GetBytes(latin1.GetString(Saved(made)).Replace("ANSI_1252", "ANSI_1200"));
            Assert.Throws<FormatException>(() => Opened(bytes));

            DxfDatabase db = Recovered(bytes, out DxfRepairReport report);

            Assert.StartsWith(
                "The file could not be read in the code page its header names (",
                Only(report, DxfRepairKind.Encoding).Message
            );
            Assert.Equal("Außen", Assert.Single(Model(db).OfType<DxfLine>()).Layer);
            Rereads(db);
        }

        [Fact]
        public void AHandleTwoRecordsHave_IsGivenAgainToTheLater_ItsVerticesPointedAtIt()
        {
            string lwpolyline = null;
            string text = Damaged(r =>
            {
                lwpolyline = ValueOf(r[Nth(r, "LWPOLYLINE")], 5);
                int polyline = Nth(r, "POLYLINE");
                string old = ValueOf(r[polyline], 5);

                for (int i = polyline; i < polyline + 5; i++)
                {
                    r[i] = r[i]
                        .Replace("\n  5\n" + old + "\n", "\n  5\n" + lwpolyline + "\n")
                        .Replace("\n330\n" + old + "\n", "\n330\n" + lwpolyline + "\n");
                }

                return r;
            });

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.Equal(
                new[]
                {
                    DxfRepairKind.DuplicateHandle,
                    DxfRepairKind.BadOwner,
                    DxfRepairKind.BadOwner,
                    DxfRepairKind.BadOwner,
                    DxfRepairKind.BadOwner,
                },
                report.Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.StartsWith("The handle " + lwpolyline + " of the POLYLINE on line ", report.Repairs[0].Message);
            Assert.Equal(lwpolyline, Assert.Single(Model(db).OfType<DxfPolyline>()).Handle);
            Assert.NotEqual(lwpolyline, Assert.Single(Model(db).OfType<DxfPolyline2d>()).Handle);
            Intact(db);
            Rereads(db);
        }

        [Fact]
        public void ARecordWithNoHandle_AndAHandseedTooLow_AreGivenNewOnes()
        {
            string text = Damaged(r =>
            {
                int line = Nth(r, "LINE", 4);
                r[line] = Group(r[line], 5, null);
                int header = Nth(r, "SECTION", 0);
                r[header] = r[header]
                    .Replace(
                        "$HANDSEED\n  5\n"
                            + ValueOf(
                                r[header].Substring(r[header].IndexOf("$HANDSEED", StringComparison.Ordinal) - 1),
                                5
                            ),
                        "$HANDSEED\n  5\n1"
                    );
                return r;
            });

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.Equal(
                new[] { DxfRepairKind.MissingHandle, DxfRepairKind.HandseedTooLow },
                report.Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.StartsWith(
                "The $HANDSEED 1 of the header is not above every handle of the file",
                report.Repairs[1].Message
            );
            Assert.Equal(3, Model(db).OfType<DxfLine>().Select(l => l.Handle).Distinct().Count());
            Intact(db);
            Rereads(db);
        }

        [Fact]
        public void AnOwnerThatNamesNothing_IsSetToModelSpace()
        {
            string text = Damaged(r =>
            {
                int line = Nth(r, "LINE", 3);
                r[line] = Group(r[line], 330, "FFFFF");
                return r;
            });

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.EndsWith(
                "names as its owner the handle FFFFF, which no record has; it now names "
                    + db.ModelSpace.Handle
                    + ", its BLOCK_RECORD.",
                Only(report, DxfRepairKind.BadOwner).Message
            );
            Intact(db);
            Rereads(db);
        }

        [Fact]
        public void ABlockWithNoEndblk_AndAPolylineAndAnInsertWithNoSeqend_AreClosed()
        {
            string text = Damaged(r =>
            {
                r.RemoveAt(Nth(r, "SEQEND", 12));
                r.RemoveAt(Nth(r, "SEQEND", 0));
                r.RemoveAt(
                    r.FindIndex(Nth(r, "BLOCK", 2), s => s.StartsWith("  0\nENDBLK\n", StringComparison.Ordinal))
                );
                return r;
            });

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.Equal(
                new[] { DxfRepairKind.MissingEndBlk, DxfRepairKind.MissingSeqEnd, DxfRepairKind.MissingSeqEnd },
                report.Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.StartsWith("The BLOCK DOOR on line ", report.Repairs[0].Message);
            Assert.EndsWith("has no SEQEND after its attributes; one was written.", report.Repairs[1].Message);
            Assert.EndsWith("has no SEQEND after its vertices; one was written.", report.Repairs[2].Message);
            Intact(db);
            Rereads(db);
        }

        [Fact]
        public void CountsOfVerticesAndPathsThatAreWrong_AreSetToTheParts()
        {
            string text = Damaged(r =>
            {
                r[Nth(r, "LWPOLYLINE")] = Group(r[Nth(r, "LWPOLYLINE")], 90, "7");
                r[Nth(r, "HATCH")] = Group(r[Nth(r, "HATCH")], 91, "3");
                return r;
            });

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.Equal(
                new[] { DxfRepairKind.WrongLoopCount, DxfRepairKind.WrongVertexCount },
                report.Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.EndsWith(" counts 7 vertices and has 4; the count was set to 4.", report.Repairs[1].Message);
            Assert.Equal(1, Assert.Single(Model(db).OfType<DxfHatch>()).NumberOfLoops);
            Intact(db);
            Rereads(db);

            // An open drawing is audited the same: found, then fixed in place.
            DxfDatabase open = Opened(Encoding.UTF8.GetBytes(text));
            Assert.Equal(
                new[] { DxfRepairKind.WrongLoopCount, DxfRepairKind.WrongVertexCount },
                open.Audit(false).Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.Equal(2, open.Audit(true).FixedCount);
            Assert.Empty(open.Audit(false).Repairs);
        }

        [Fact]
        public void ANumberThatIsNoNumber_IsReadAsTheNumberItBeginsWith_AndANumberNotFinite_LeavesItsEntityOut()
        {
            string text = Damaged(r =>
            {
                r[Nth(r, "LINE", 2)] = Group(r[Nth(r, "LINE", 2)], 11, "500mm");
                r[Nth(r, "LINE", 3)] = Group(r[Nth(r, "LINE", 3)], 10, "NaN");
                r[Nth(r, "VERTEX", 1)] = Group(r[Nth(r, "VERTEX", 1)], 20, "1.#INF");
                return r;
            });

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.Equal(
                new[] { DxfRepairKind.NonFiniteNumber, DxfRepairKind.BadNumber, DxfRepairKind.NonFiniteNumber },
                report.Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.EndsWith("is no number, '500mm'; it was read as 500.", report.Repairs[1].Message);
            Assert.EndsWith(
                "has a number that is not finite, 'NaN' in its group 10; it was left out.",
                report.Repairs[2].Message
            );
            Intact(db, lines: 2, vertices2d: 2);
            Rereads(db);
        }

        [Fact]
        public void NamesTheTablesLack_AreAdded_AnInsertOfNoBlockAndAnUnknownTypeKept()
        {
            string text = Damaged(r =>
            {
                r[Nth(r, "LINE", 2)] = Group(r[Nth(r, "LINE", 2)], 8, "WALLS");
                r[Nth(r, "LINE", 3)] = Group(r[Nth(r, "LINE", 3)], 8, "WALLS")
                    .Replace("100\nAcDbEntity\n", "100\nAcDbEntity\n  6\nDASHED2\n");
                r[Nth(r, "TEXT")] = r[Nth(r, "TEXT")].Replace("100\nAcDbText\n", "100\nAcDbText\n  7\nROMANS\n");
                r[Nth(r, "DIMENSION")] = Group(r[Nth(r, "DIMENSION")], 3, "ARCH");
                r[Nth(r, "INSERT", 11)] = Group(r[Nth(r, "INSERT", 11)], 2, "WINDOW");
                r[Nth(r, "LINE", 4)] = r[Nth(r, "LINE", 4)].Replace("  0\nLINE\n", "  0\nFOOBAR\n");
                return r;
            });
            DxfDatabase open = Opened(Encoding.UTF8.GetBytes(text));
            string before = open.ToText();
            Assert.Equal(6, open.Audit(false).Count);
            Assert.Equal(before, open.ToText());

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            DxfRepairKind[] kinds =
            {
                DxfRepairKind.MissingBlock,
                DxfRepairKind.UnknownEntity,
                DxfRepairKind.MissingTextStyle,
                DxfRepairKind.MissingDimStyle,
                DxfRepairKind.MissingLayer,
                DxfRepairKind.MissingLinetype,
            };
            Assert.Equal(kinds, report.Repairs.Select(r => r.Kind).ToArray());
            Assert.Equal(4, report.FixedCount);
            Assert.StartsWith("The layer WALLS, named by 2 entities (the first: the LINE ", report.Repairs[4].Message);
            Assert.True(
                db.LayerTable.Has("WALLS")
                    && db.LinetypeTable.Has("DASHED2")
                    && db.TextStyleTable.Has("ROMANS")
                    && db.DimStyleTable.Has("ARCH")
            );
            Assert.Equal("FOOBAR", Assert.Single(Model(db).OfType<DxfUnknownEntity>()).DxfName);
            Intact(db, lines: 2);
            Rereads(db, DxfRepairKind.MissingBlock, DxfRepairKind.UnknownEntity);
        }

        [Fact]
        public void BrokenEntriesOfADictionary_AreTakenOut()
        {
            string text = Damaged(r =>
            {
                int root = Nth(r, "DICTIONARY", 0);
                r[root] = r[root]
                    .Replace(
                        "  3\nACAD_GROUP\n350\n" + ValueOf(r[root], 350) + "\n",
                        "  3\nACAD_GROUP\n350\nZZ\n  3\nLOST\n"
                    );
                return r;
            });

            DxfDatabase db = Recovered(text, out DxfRepairReport report);

            Assert.Equal(
                new[] { DxfRepairKind.BrokenDictionaryEntry, DxfRepairKind.BrokenDictionaryEntry },
                report.Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.StartsWith(
                "The entry LOST with no pointer after it stands in the dictionary ",
                report.Repairs[0].Message
            );
            Assert.StartsWith(
                "The entry ACAD_GROUP that points at 'ZZ', which is not a handle, stands in the dictionary ",
                report.Repairs[1].Message
            );
            Assert.DoesNotContain("ACAD_GROUP", db.ToText());
            Intact(db);
            Rereads(db);
        }

        [Fact]
        public void BytesThatAreNoDxf_GiveAnEmptyDrawing_AndABinaryDxfIsRefused()
        {
            DxfDatabase db = Recovered(
                Encoding.ASCII.GetBytes("This is a letter,\nnot a drawing.\n"),
                out DxfRepairReport report
            );

            Assert.Equal(
                new[]
                {
                    DxfRepairKind.BadGroupCode,
                    DxfRepairKind.Unreadable,
                    DxfRepairKind.Truncated,
                    DxfRepairKind.MissingSection,
                },
                report.Repairs.Select(r => r.Kind).ToArray()
            );
            Assert.Empty(Model(db));
            Rereads(db);

            Assert.Throws<FormatException>(() =>
                Recovered(Encoding.ASCII.GetBytes("AutoCAD Binary DXF\r\n\u001a\0"), out _)
            );
            Assert.Throws<ArgumentNullException>(() => DxfDatabase.Recover((string)null, out _));
        }
    }
}
