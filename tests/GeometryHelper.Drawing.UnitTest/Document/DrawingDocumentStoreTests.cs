using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Drawing.UnitTest
{
    public class DrawingDocumentStoreTests
    {
        private static KeyValuePair<string, string> Value(string name, string value) =>
            new KeyValuePair<string, string>(name, value);

        private static DxfDatabase SavedAndOpened(DxfDatabase database)
        {
            using (var stream = new MemoryStream())
            {
                database.Save(stream);
                stream.Position = 0;
                return DxfDatabase.Open(stream);
            }
        }

        private static DxfXrecord Records(DxfDatabase database) =>
            (DxfXrecord)
                ((DxfDBDictionary)database.NamedObjectsDictionary.GetAt(DrawingDocumentStore.DictionaryName)).GetAt(
                    "RECORDS"
                );

        /// <summary>A drawing B-101 of a beam B-12 3 000 long: a front and a top view, two dimensions and a mark, two hand edits and a plugin run.</summary>
        internal static DrawingDocumentRecords Beam()
        {
            var front = new DrawingElement(
                "V-1",
                "View",
                null,
                new DrawingLinks(new[] { "B-12", "P-1" }),
                new[] { Value("Scale", "20"), Value("Direction", "Front") }
            );
            var top = new DrawingElement(
                "V-2",
                "View",
                null,
                new DrawingLinks(new[] { "B-12" }),
                new[] { Value("Scale", "20"), Value("Crop", null) }
            );
            var overall = new DrawingElement(
                "D-7",
                "Dimension",
                "V-1",
                new DrawingLinks(
                    new[] { "B-12" },
                    new[]
                    {
                        new DrawingLinkPoint3("B-12", new GeoPoint3(0, 0, 0)),
                        new DrawingLinkPoint3("B-12", new GeoPoint3(3000, 0, 0)),
                    }
                )
            );
            var chain = new DrawingElement(
                "D-8",
                "Dimension",
                "V-2",
                new DrawingLinks(
                    null,
                    new[]
                    {
                        new DrawingLinkPoint3(null, new GeoPoint3(-150.25, 0.1, 1.0 / 3.0)),
                        new DrawingLinkPoint3("P-1", new GeoPoint3(2850.5, -0.0, 12)),
                    }
                ),
                new[] { Value("Rule", "Chain"), Value("Tier", "2") }
            );
            var mark = new DrawingElement("M-3", "Mark", "V-1", new DrawingLinks(new[] { "P-1" }));
            var moved = new DrawingEdit("M-3", CadEditKind.Move, newValue: "40,0");
            var overridden = new DrawingEdit("D-7", CadEditKind.Property, "Text override", "", "3000 (cut on site)");
            var run = new DrawingPluginRun(
                "R-1",
                "BoltDimensions",
                "1.2",
                "V-1",
                new DrawingLinks(new[] { "G-4" }),
                new[] { Value("Gap", "8") }
            );
            return new DrawingDocumentRecords(
                "B-101",
                new[] { front, top },
                new[] { overall, chain, mark },
                new[] { moved, overridden },
                new[] { run }
            );
        }

        [Fact]
        public void ADocumentSavedAndOpenedAgainIsTheSameDocument()
        {
            DrawingDocumentRecords document = Beam();
            var database = new DxfDatabase("AC1027");

            DrawingDocumentStore.Write(database, document);
            DrawingDocumentRecords back = DrawingDocumentStore.Read(SavedAndOpened(database));

            Assert.Equal(document, back);
            Assert.Equal("B-101", back.DrawingId);
            Assert.Equal(new[] { "V-1", "V-2" }, back.Views.Select(v => v.Id));
            Assert.Equal(new[] { "D-7", "D-8", "M-3" }, back.Annotations.Select(a => a.Id));
            Assert.Equal(3000.0, back.Annotations[0].Links.Points[1].Point.X);
            Assert.Null(back.Views[1].Values[1].Value);
            Assert.Equal(CadEditKind.Move, back.Edits[0].Kind);
            Assert.Null(back.Edits[0].Property);
            Assert.Equal("", back.Edits[1].OldValue);
            Assert.Equal("1.2", Assert.Single(back.PluginRuns).Version);
            Assert.Empty(back.UnknownRecords);
        }

        [Fact]
        public void ADocumentOfNothingComesBackAsNothing()
        {
            var document = new DrawingDocumentRecords(null, null, null, null, null);
            var database = new DxfDatabase("AC1027");

            DrawingDocumentStore.Write(database, document);
            DrawingDocumentRecords back = DrawingDocumentStore.Read(SavedAndOpened(database));

            Assert.Equal(document, back);
            Assert.Null(back.DrawingId);
            Assert.Empty(back.Views);
        }

        [Fact]
        public void ADrawingWithNoDocumentReadsAsNone()
        {
            Assert.Null(DrawingDocumentStore.Read(new DxfDatabase("AC1027")));
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1027")]
        public void TextsComeBackLetterForLetter(string version)
        {
            string longText = new string('x', 249) + "😀" + new string('y', 300) + "\n" + new string('%', 120);
            string[] texts =
            {
                null,
                "",
                "  two spaces either side  ",
                "line one\nline two\r\nline three\r",
                "Đường ống thép",
                "鋼材 S355",
                "😀",
                "\uD800 alone",
                @"a\U+0041b \M+18140 \P",
                "%",
                "%25",
                "\\",
                "\t tab",
                "{GH_VIEW",
                "}",
                longText,
            };
            var values = texts.Select((t, i) => Value("Text " + i, t)).ToList();
            values.Add(Value("Đ\nname", "value"));
            var element = new DrawingElement(
                "V-1\n2",
                "View",
                texts[4],
                new DrawingLinks(
                    new[] { longText, @"\U+0041" },
                    new[] { new DrawingLinkPoint3("\r\n", new GeoPoint3(1, 2, 3)) }
                ),
                values
            );
            var edit = new DrawingEdit(longText, CadEditKind.Property, "Text override", texts[3], texts[8]);
            var document = new DrawingDocumentRecords("Đ-101\n", new[] { element }, null, new[] { edit }, null);
            var database = new DxfDatabase(version);

            DrawingDocumentStore.Write(database, document);
            DxfDatabase opened = SavedAndOpened(database);
            DrawingDocumentRecords back = DrawingDocumentStore.Read(opened);

            Assert.Equal(document, back);
            Assert.Equal(texts, back.Views[0].Values.Take(texts.Length).Select(v => v.Value));
            Assert.Equal(longText, back.Edits[0].ObjectId);
            Assert.All(Records(opened).Data.Where(g => g.TypeCode == 1 || g.TypeCode == 3), g =>
                Assert.True(g.Value.Length <= 250)
            );

            // A surrogate pair is never split between two groups, which another reader would not join again.
            Assert.DoesNotContain(Records(opened).Data, g =>
                g.Value.Length > 0 && char.IsHighSurrogate(g.Value[g.Value.Length - 1])
            );
        }

        [Fact]
        public void CoordinatesComeBackToTheBit()
        {
            double[] numbers =
            {
                0.0,
                -0.0,
                0.1,
                1.0 / 3.0,
                -2.0 / 3.0,
                Math.PI * 1e5,
                123456789.123456789,
                1e-300,
                double.Epsilon,
                2.2250738585072009E-308,
                double.MaxValue,
                double.MinValue,
                4.35,
                0.3,
                1e23,
                5e-324,
                9007199254740993.0,
            };
            var points = new List<DrawingLinkPoint3>();

            for (int i = 0; i < numbers.Length; i++)
            {
                points.Add(
                    new DrawingLinkPoint3(
                        "B-12",
                        new GeoPoint3(numbers[i], numbers[(i + 1) % numbers.Length], numbers[(i + 2) % numbers.Length])
                    )
                );
            }

            var document = new DrawingDocumentRecords(
                "B-101",
                new[] { new DrawingElement("V-1", "View", links: new DrawingLinks(null, points)) },
                null,
                null,
                null
            );
            var database = new DxfDatabase("AC1027");

            DrawingDocumentStore.Write(database, document);
            DrawingDocumentRecords back = DrawingDocumentStore.Read(SavedAndOpened(database));

            IReadOnlyList<DrawingLinkPoint3> read = back.Views[0].Links.Points;

            for (int i = 0; i < numbers.Length; i++)
            {
                Assert.Equal(
                    BitConverter.DoubleToInt64Bits(numbers[i]),
                    BitConverter.DoubleToInt64Bits(read[i].Point.X)
                );
                Assert.Equal(
                    BitConverter.DoubleToInt64Bits(numbers[(i + 1) % numbers.Length]),
                    BitConverter.DoubleToInt64Bits(read[i].Point.Y)
                );
                Assert.Equal(
                    BitConverter.DoubleToInt64Bits(numbers[(i + 2) % numbers.Length]),
                    BitConverter.DoubleToInt64Bits(read[i].Point.Z)
                );
            }

            Assert.Equal(document, back);
        }

        [Fact]
        public void ARecordOfAKindItDoesNotKnowIsKeptThroughASave()
        {
            var database = new DxfDatabase("AC1027");
            DrawingDocumentStore.Write(database, Beam());
            DxfTypedValue[] future =
            {
                new DxfTypedValue(102, "{GH_SECTION"),
                new DxfTypedValue(1, "S-1"),
                new DxfTypedValue(40, "2.5"),
                new DxfTypedValue(102, "}"),
            };
            Records(database).Append(future);
            var notes = (DxfDBDictionary)database.NamedObjectsDictionary.GetAt(DrawingDocumentStore.DictionaryName);
            notes.SetAt("NOTES", new DxfXrecord { Data = new[] { new DxfTypedValue(1, "kept") } });

            DxfDatabase opened = SavedAndOpened(database);
            DrawingDocumentRecords read = DrawingDocumentStore.Read(opened);
            Assert.Equal(
                future.Select(g => g.ToString()),
                Assert.Single(read.UnknownRecords).Select(g => g.ToString())
            );
            Assert.Equal(Beam().Views, read.Views);

            // Written back as an update writes it: the unknown record and the other entry stay.
            var updated = new DrawingDocumentRecords(
                read.DrawingId,
                read.Views,
                read.Annotations,
                read.Edits.Concat(new[] { new DrawingEdit("D-8", CadEditKind.Erase) }),
                read.PluginRuns,
                read.UnknownRecords
            );
            DrawingDocumentStore.Write(opened, updated);
            DxfDatabase again = SavedAndOpened(opened);
            DrawingDocumentRecords twice = DrawingDocumentStore.Read(again);

            Assert.Equal(updated, twice);
            Assert.Equal(3, twice.Edits.Count);
            Assert.Equal(
                future.Select(g => g.ToString()),
                Records(again).Data.Skip(Records(again).Data.Count - 4).Select(g => g.ToString())
            );
            var root = (DxfDBDictionary)again.NamedObjectsDictionary.GetAt(DrawingDocumentStore.DictionaryName);
            Assert.Equal("kept", ((DxfXrecord)root.GetAt("NOTES")).Data[0].Value);
        }

        [Fact]
        public void ARecordThatFailsToReadIsSkippedSaidAndKeptAndTheOthersAreRead()
        {
            var database = new DxfDatabase("AC1027");
            var good = new DrawingElement("V-1", "View");
            DrawingDocumentStore.Write(database, new DrawingDocumentRecords("B-101", new[] { good }, null, null, null));
            DxfTypedValue[] badCount =
            {
                new DxfTypedValue(102, "{GH_VIEW"),
                new DxfTypedValue(1, "V-2"),
                new DxfTypedValue(1, "View"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(90, "x"),
                new DxfTypedValue(102, "}"),
            };
            DxfTypedValue[] badKind =
            {
                new DxfTypedValue(102, "{GH_EDIT"),
                new DxfTypedValue(1, "D-1"),
                new DxfTypedValue(1, "Teleport"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(102, "}"),
            };
            DxfTypedValue[] tooLong =
            {
                new DxfTypedValue(102, "{GH_EDIT"),
                new DxfTypedValue(1, "D-1"),
                new DxfTypedValue(1, "Erase"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(1, "more"),
                new DxfTypedValue(102, "}"),
            };
            DxfTypedValue[] numberEdit =
            {
                new DxfTypedValue(102, "{GH_EDIT"),
                new DxfTypedValue(1, "D-1"),
                new DxfTypedValue(1, "1"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(102, "}"),
            };
            DxfTypedValue[] fine =
            {
                new DxfTypedValue(102, "{GH_EDIT"),
                new DxfTypedValue(1, "D-1"),
                new DxfTypedValue(1, "Erase"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(1, "%"),
                new DxfTypedValue(102, "}"),
            };
            Records(database).Append(badCount.Concat(badKind).Concat(tooLong).Concat(numberEdit).Concat(fine));
            var lines = new List<(GeometryHelperLogLevel Level, string Text)>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            bool enabled = GeometryHelperLog.Enable;
            DrawingDocumentRecords read;

            try
            {
                GeometryHelperLog.Enable = true;
                GeometryHelperLog.Writer = (level, text, exception) =>
                {
                    lock (lines)
                    {
                        lines.Add((level, text));
                    }
                };
                read = DrawingDocumentStore.Read(SavedAndOpened(database));
            }
            finally
            {
                GeometryHelperLog.Writer = before;
                GeometryHelperLog.Enable = enabled;
            }

            Assert.Equal(good, Assert.Single(read.Views));
            Assert.Equal(new DrawingEdit("D-1", CadEditKind.Erase), Assert.Single(read.Edits));
            Assert.Equal(4, read.UnknownRecords.Count);
            Assert.Equal(badCount.Select(g => g.ToString()), read.UnknownRecords[0].Select(g => g.ToString()));
            Assert.Contains(lines, l =>
                l.Level == GeometryHelperLogLevel.Warn
                && l.Text.StartsWith(
                    "DrawingDocumentStore.Read: 4 records of GH_DRAWING failed to read",
                    StringComparison.Ordinal
                )
            );
        }

        [Fact]
        public void AListLeftOpenIsDroppedAndTheDocumentStillWritesBack()
        {
            var written = new DxfDatabase("AC1027");
            DrawingDocumentStore.Write(written, Beam());
            string text = written.ToText();

            // The plugin run's 102 } taken out by another program: the list cannot be written back as it is.
            int run = text.IndexOf("{GH_PLUGIN_RUN", StringComparison.Ordinal);
            var close = new System.Text.RegularExpressions.Regex(@"(?m)^ *102\r?\n\}\r?\n");
            System.Text.RegularExpressions.Match match = close.Match(text, run);
            Assert.True(match.Success);
            DxfDatabase database = DxfDatabase.Parse(text.Remove(match.Index, match.Length));

            DrawingDocumentRecords read = DrawingDocumentStore.Read(database);
            DrawingDocumentStore.Write(database, read);

            Assert.Empty(read.PluginRuns);
            Assert.Empty(read.UnknownRecords);
            Assert.Equal(Beam().Annotations, DrawingDocumentStore.Read(SavedAndOpened(database)).Annotations);
        }

        [Fact]
        public void WritingTheSameDocumentTwiceChangesNothing()
        {
            var database = new DxfDatabase("AC1027");
            DrawingDocumentStore.Write(database, Beam());
            string once = database.ToText();

            DrawingDocumentStore.Write(database, Beam());

            Assert.Equal(once, database.ToText());
        }

        [Fact]
        public void AnEntryOfTheNameThatIsNotADictionaryIsNotWrittenOver()
        {
            var database = new DxfDatabase("AC1027");
            database.NamedObjectsDictionary.SetAt(
                DrawingDocumentStore.DictionaryName,
                new DxfXrecord { Data = new[] { new DxfTypedValue(1, "someone else's") } }
            );

            Assert.Throws<InvalidOperationException>(() => DrawingDocumentStore.Write(database, Beam()));
            Assert.Null(DrawingDocumentStore.Read(database));
        }

        /// <summary>
        /// 10 000 records (1 000 views, 5 000 dimensions of two points, 4 000 edits) written, saved, opened and read back.
        /// On the maintainer's machine it takes 0.14 s under .NET Framework 4.8; the limit of 3 s guards against a fall into
        /// the quadratic, as one dictionary entry per record took 108 s.
        /// </summary>
        [Fact]
        public void TenThousandRecordsAreWrittenSavedAndReadBackFast()
        {
            var views = new List<DrawingElement>();
            var annotations = new List<DrawingElement>();
            var edits = new List<DrawingEdit>();

            for (int i = 0; i < 1000; i++)
            {
                views.Add(
                    new DrawingElement(
                        "V-" + i,
                        "View",
                        null,
                        new DrawingLinks(new[] { "B-" + i }),
                        new[] { Value("Scale", "20") }
                    )
                );
            }

            for (int i = 0; i < 5000; i++)
            {
                var points = new[]
                {
                    new DrawingLinkPoint3("B-" + (i % 1000), new GeoPoint3(i * 0.1, 0, 0)),
                    new DrawingLinkPoint3("B-" + (i % 1000), new GeoPoint3(i * 0.1 + 3000.0 / 7.0, 0, 0)),
                };
                annotations.Add(
                    new DrawingElement("D-" + i, "Dimension", "V-" + (i % 1000), new DrawingLinks(null, points))
                );
            }

            for (int i = 0; i < 4000; i++)
            {
                edits.Add(
                    new DrawingEdit(
                        "D-" + i,
                        CadEditKind.Move,
                        newValue: (i * 0.5).ToString(System.Globalization.CultureInfo.InvariantCulture) + ",0"
                    )
                );
            }

            var document = new DrawingDocumentRecords("B-101", views, annotations, edits, null);
            var watch = Stopwatch.StartNew();
            var database = new DxfDatabase("AC1027");
            DrawingDocumentStore.Write(database, document);
            DrawingDocumentStore.Write(database, document);
            DrawingDocumentRecords back = DrawingDocumentStore.Read(SavedAndOpened(database));
            watch.Stop();

            Assert.Equal(document, back);
            Assert.True(watch.ElapsedMilliseconds < 3000, "10 000 records took " + watch.ElapsedMilliseconds + " ms.");
        }

        [Fact]
        public void RecordsAreComparedFieldByField()
        {
            DrawingDocumentRecords beam = Beam();
            var other = new DrawingDocumentRecords(
                "B-101",
                beam.Views,
                beam.Annotations,
                beam.Edits.Take(1),
                beam.PluginRuns
            );
            var zero = new DrawingLinkPoint3("B-12", new GeoPoint3(0, 0, 0));
            var minusZero = new DrawingLinkPoint3("B-12", new GeoPoint3(-0.0, 0, 0));

            Assert.Equal(beam, Beam());
            Assert.NotEqual(beam, other);
            Assert.NotEqual(zero, minusZero);
            Assert.Throws<ArgumentException>(() => new DrawingLinkPoint3("B-12", new GeoPoint3(double.NaN, 0, 0)));
            Assert.Throws<ArgumentException>(() => new DrawingLinks(new[] { "" }));
            Assert.Throws<ArgumentNullException>(() => new DrawingElement(null, "View"));
            Assert.Throws<ArgumentException>(() =>
                new DrawingDocumentRecords(null, new DrawingElement[] { null }, null, null, null)
            );
        }
    }
}
