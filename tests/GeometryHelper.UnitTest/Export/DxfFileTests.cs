using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GeometryHelper.Enums;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfLines;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// A DXF file opened with <see cref="DxfFile"/> and saved again: the same bytes whatever the file is made of, and views of
    /// its entities whose shapes are those <see cref="DxfReader"/> reads.
    /// </summary>
    public class DxfFileTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        #region Helpers

        private static byte[] Saved(DxfFile file)
        {
            using (var stream = new MemoryStream())
            {
                file.Save(stream);
                return stream.ToArray();
            }
        }

        private static DxfFile Opened(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DxfFile.Open(stream);
            }
        }

        private static DxfDocument Read(byte[] bytes, bool expand)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DxfReader.Read(stream, new DxfReadOptions { Tolerance = Tol, ExpandBlocks = expand });
            }
        }

        /// <summary>A shape as the numbers it holds, each double by its bits, so that two equal strings are two equal shapes.</summary>
        private static string Bits(object shape)
        {
            var text = new StringBuilder(shape?.GetType().Name).Append('|');
            Bits(text, shape, 0);
            return text.ToString();
        }

        private static void Bits(StringBuilder text, object value, int depth)
        {
            if (value == null || depth > 8)
            {
                text.Append("null;");
                return;
            }

            Type type = value.GetType();

            if (value is double number)
            {
                text.Append(BitConverter.DoubleToInt64Bits(number).ToString("x")).Append(';');
                return;
            }

            if (type.IsPrimitive || value is string || type.IsEnum)
            {
                text.Append(value).Append(';');
                return;
            }

            if (value is IEnumerable items)
            {
                text.Append('[');

                foreach (object item in items)
                {
                    Bits(text, item, depth + 1);
                }

                text.Append(']');
                return;
            }

            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (field.FieldType == typeof(IntPtr) || field.Name.IndexOf("cache", StringComparison.OrdinalIgnoreCase) >= 0 || field.Name.IndexOf("lazy", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                text.Append(field.Name).Append('=');
                Bits(text, field.GetValue(value), depth + 1);
            }
        }

        /// <summary>The shapes of items as the reader reads them, those it reads more than one of each, INSERTs left out.</summary>
        private static List<string> Shapes(IEnumerable<DxfItem> items)
        {
            var shapes = new List<string>();

            foreach (DxfItem item in items)
            {
                if (!(item.Shape is DxfInsert))
                {
                    shapes.AddRange(item.Shapes.Select(Bits));
                }
            }

            return shapes;
        }

        private static string Placed(DxfShape e)
            => e.Type + "|" + e.Handle + "|" + e.Layer + "|" + e.Color + "|" + e.TrueColor + "|" + e.Linetype + "|" + e.LineWeight + "|" + e.Thickness.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            + "|" + string.Join(">", e.BlockPath) + "|" + string.Join(">", e.InsertHandles.Select(h => h ?? "null")) + "|" + Bits(e.Shape);

        /// <summary>
        /// The entities the reader reads of items, an INSERT's placed: each placed one in full, each of the items themselves by
        /// its shape alone.
        /// </summary>
        private static List<string> Expanded(IEnumerable<DxfItem> items)
        {
            var entities = new List<string>();

            foreach (DxfItem item in items)
            {
                if (item.Shape is DxfInsert insert)
                {
                    entities.AddRange(insert.GetPlacedEntities().Select(Placed));
                }
                else
                {
                    entities.AddRange(Shapes(new[] { item }));
                }
            }

            return entities;
        }

        private static List<string> Expanded(IEnumerable<DxfShape> entities)
            => entities.Select(e => e.BlockPath.Count > 0 ? Placed(e) : Bits(e.Shape)).ToList();

        private static string Drawing(string version, string entities, string blocks = "")
            => G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, version, 9, "$INSUNITS", 70, "4", 0, "ENDSEC")
            + (blocks.Length > 0 ? G(0, "SECTION", 2, "BLOCKS") + blocks + G(0, "ENDSEC") : string.Empty)
            + G(0, "SECTION", 2, "ENTITIES") + entities + G(0, "ENDSEC", 0, "EOF");

        private static readonly string Mixed = string.Empty
            + G(0, "LINE", 5, "1A", 8, "steel", 62, "1", 10, "0", 20, "0", 30, "0", 11, "100.125", 21, "50.5", 31, "3")
            + G(0, "CIRCLE", 5, "1B", 8, "0", 10, "10", 20, "20", 30, "5", 40, "7.5", 210, "0", 220, "0", 230, "-1")
            + G(0, "LWPOLYLINE", 5, "1C", 8, "0", 90, "3", 70, "1", 10, "0", 20, "0", 42, "0.5", 10, "10", 20, "0", 10, "10", 20, "10")
            + G(0, "HATCH", 5, "1D", 8, "hatch", 62, "2", 2, "SOLID", 70, "1", 71, "0", 91, "0", 75, "0", 76, "1", 98, "0", 1001, "GHPROBE", 1000, "kept")
            + G(0, "3DFACE", 5, "1E", 8, "0", 10, "0", 20, "0", 30, "0", 11, "1", 21, "0", 31, "0", 12, "1", 22, "1", 32, "0.5", 13, "0", 23, "1", 33, "0")
            + G(0, "TEXT", 5, "1F", 8, "0", 10, "1", 20, "2", 30, "0", 40, "2.5", 1, "Grid %%c20")
            + G(0, "POLYLINE", 5, "20", 8, "0", 66, "1", 70, "8", 10, "0", 20, "0", 30, "0")
            + G(0, "VERTEX", 5, "21", 8, "0", 70, "32", 10, "0", 20, "0", 30, "0")
            + G(0, "VERTEX", 5, "22", 8, "0", 70, "32", 10, "1", 20, "2", 30, "3")
            + G(0, "SEQEND", 5, "23", 8, "0")
            + G(0, "INSERT", 5, "24", 8, "parts", 66, "1", 2, "BOLT", 10, "50", 20, "60", 30, "0", 41, "1.5", 42, "-1.5", 50, "45")
            + G(0, "ATTRIB", 5, "25", 8, "0", 10, "50", 20, "59", 30, "0", 40, "0.5", 1, "B-12", 2, "MARK", 70, "0")
            + G(0, "SEQEND", 5, "26", 8, "0")
            + G(0, "CIRCLE", 5, "27", 8, "0", 67, "1", 10, "50", 20, "50", 30, "0", 40, "20");

        private static readonly string Bolt = G(0, "BLOCK", 8, "0", 2, "BOLT", 70, "0", 10, "1", 20, "2", 30, "0", 3, "BOLT")
            + G(0, "CIRCLE", 5, "30", 8, "0", 10, "1", 20, "2", 30, "0", 40, "3")
            + G(0, "LINE", 5, "31", 8, "0", 62, "0", 10, "0", 20, "0", 30, "0", 11, "4", 21, "4", 31, "0")
            + G(0, "ENDBLK", 5, "32", 8, "0");

        private static string MixedDrawing() => Drawing("AC1015", Mixed, Bolt);

        /// <summary>The drawing of ezdxf as its bytes: its text in its encoding, each line ended as ezdxf ends it on Windows.</summary>
        private static byte[] Ezdxf(string text, Encoding encoding) => encoding.GetBytes(text.Replace("\n", "\r\n"));

        #endregion

        #region Saved without loss

        [Fact]
        public void AFileTheWriterWrote_IsSavedByteForByte_ItsShapesThoseTheReaderReads()
        {
            DxfWriter writer = DxfReaderRoundTripTests.OneOfEach()
                .Add(new DxfText(new GeoPoint3(1, 2, 0), new GeoVector3(0, 0, 1), 2.5, 0.3, "Grid A"), "notes")
                .Add(new DxfText(new GeoPoint3(1, 2, 0), new GeoVector3(0, 0, 1), 2.5, 0, "two\nlines", true, 1.0, 40.0))
                .Add(new DxfShape(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 2, 3)), "steel", 1, 0xFF8000, "DASHED", 35, 2.0));
            string text = writer.ToString();

            DxfFile file = DxfFile.Parse(text);
            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol });

            Assert.Equal(new UTF8Encoding(false).GetBytes(text), Saved(file));
            Assert.Equal(text, file.ToText());
            Assert.Equal(read.Shapes.Select(e => Bits(e.Shape)), Shapes(file.ModelSpace));
            Assert.Equal(DxfReaderRoundTripTests.EntitiesWritten(text), file.ModelSpace.Count);
            Assert.Equal("AC1015", file.Version);

            DxfItem line = file.ModelSpace.Last();
            DxfShape entity = read.Shapes.Last();

            Assert.Equal(entity.Handle, line.Handle);
            Assert.Equal(entity.Layer, line.Layer);
            Assert.Equal(1, line.Colour);
            Assert.Equal(0xFF8000, line.TrueColour);
            Assert.Equal("DASHED", line.Linetype);
            Assert.Equal(35, line.Lineweight);
            Assert.Equal(2.0, line.Thickness);
        }

        [Fact]
        public void AFileOpenedAndSaved_IsTheSameBytes_WhateverItsLineEndsPaddingCommentsAndMark()
        {
            string lf = MixedDrawing();
            var mixed = new StringBuilder();
            int line = 0;

            foreach (char c in lf)
            {
                mixed.Append(c == '\n' && line++ % 3 == 0 ? "\r\n" : c.ToString());
            }

            string[] texts =
            {
                lf,
                lf.Replace("\n", "\r\n"),
                mixed.ToString(),
                lf.Replace("  0\n", "0\n").Replace(" 10\n", "10\n"),
                lf.Replace("  8\n", "\t8 \n").Replace(" 62\n", "62   \n"),
                lf.Replace("\nsteel\n", "\nsteel  \n"),
                lf.TrimEnd('\n'),
                G(999, "written by hand") + lf.Replace("  0\nCIRCLE\n", "999\na comment\n  0\nCIRCLE\n 999\nanother\n"),
                lf + "after the end\n\n",
            };

            foreach (string text in texts)
            {
                foreach (byte[] bytes in new[] { Encoding.ASCII.GetBytes(text), new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.ASCII.GetBytes(text)).ToArray() })
                {
                    DxfFile file = Opened(bytes);

                    Assert.Equal(bytes, Saved(file));
                    Assert.Equal(text, file.ToText());
                    Assert.Equal(Read(bytes, false).Shapes.Select(e => Bits(e.Shape)), Shapes(file.ModelSpace));
                    Assert.Equal(8, file.ModelSpace.Count);
                }
            }
        }

        [Fact]
        public void TheBytesOfACodePageOrOfUtf8_AreKept_TheNamesReadAsTheReaderReadsThem()
        {
            // St€l in Windows-1252 (0x80 the euro), Grün in UTF-8, and gr#n of R2018 with a byte that is not UTF-8.
            byte[] page = Encoding.ASCII.GetBytes(Drawing("AC1015", G(0, "LINE", 8, "St#l", 10, "0", 20, "0", 11, "1", 21, "0")));
            page[Array.IndexOf(page, (byte)'#')] = 0x80;
            byte[] utf8 = new UTF8Encoding(false).GetBytes(Drawing("AC1032", G(0, "LINE", 8, "Grün", 10, "0", 20, "0", 11, "1", 21, "0")));
            byte[] broken = Encoding.ASCII.GetBytes(Drawing("AC1032", G(0, "LINE", 8, "gr#n", 10, "0", 20, "0", 11, "1", 21, "0")));
            broken[Array.IndexOf(broken, (byte)'#')] = 0xFC;

            foreach ((byte[] bytes, string layer) in new[] { (page, "St€l"), (utf8, "Grün"), (broken, "gr�n") })
            {
                DxfFile file = Opened(bytes);

                Assert.Equal(bytes, Saved(file));
                Assert.Equal(layer, Assert.Single(file.ModelSpace).Layer);
                Assert.Equal(Assert.Single(Read(bytes, false).Shapes).Layer, Assert.Single(file.ModelSpace).Layer);
            }
        }

        [Fact]
        public void AText_IsKeptAsUtf8_ItsByteOrderMarkAsTheMarkOfUtf8()
        {
            string text = Drawing("AC1032", G(0, "LINE", 8, "Grün", 10, "0", 20, "0", 11, "1", 21, "0"));
            DxfFile marked = DxfFile.Parse("﻿" + text);

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }.Concat(new UTF8Encoding(false).GetBytes(text)), Saved(marked));
            Assert.Equal(text, marked.ToText());
            Assert.Equal("Grün", Assert.Single(marked.ModelSpace).Layer);
        }

        [Fact]
        public void AFileSavedToAPath_IsTheFileItWasOpenedFrom()
        {
            string from = Path.Combine(Path.GetTempPath(), "dxffile-" + Guid.NewGuid().ToString("N") + ".dxf");
            string to = Path.Combine(Path.GetTempPath(), "dxffile-" + Guid.NewGuid().ToString("N") + ".dxf");
            byte[] bytes = Ezdxf(DxfFileEzdxfDrawings.R2000, Encoding.GetEncoding(1252));

            try
            {
                System.IO.File.WriteAllBytes(from, bytes);
                DxfDatabase.Open(from).Save(to);

                Assert.Equal(bytes, System.IO.File.ReadAllBytes(to));
            }
            finally
            {
                System.IO.File.Delete(from);
                System.IO.File.Delete(to);
            }
        }

        [Fact]
        public void AStream_IsOpenedFromWhereItStands_AndSavedWhereTheOtherStands()
        {
            byte[] junk = Encoding.ASCII.GetBytes("not the drawing\n");
            byte[] drawing = Encoding.ASCII.GetBytes(MixedDrawing());

            using (var from = new MemoryStream(junk.Concat(drawing).ToArray()))
            using (var to = new MemoryStream())
            {
                from.Position = junk.Length;
                to.Write(junk, 0, junk.Length);
                DxfDatabase.Open(from).Save(to);

                Assert.Equal(junk.Concat(drawing).ToArray(), to.ToArray());
            }
        }

        [Fact]
        public void EmptySectionsAndSectionsAndObjectsTheLibraryDoesNotRead_AreSavedAsTheyWere()
        {
            string text = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1032", 0, "ENDSEC")
                + G(0, "SECTION", 2, "CLASSES", 0, "ENDSEC")
                + G(0, "SECTION", 2, "BLOCKS", 0, "ENDSEC")
                + G(0, "SECTION", 2, "ENTITIES", 0, "ENDSEC")
                + G(0, "SECTION", 2, "OBJECTS", 0, "DICTIONARY", 5, "C", 330, "0", 100, "AcDbDictionary", 3, "GH", 350, "D")
                + G(0, "XRECORD", 5, "D", 102, "{ACAD_REACTORS", 330, "C", 102, "}", 330, "C", 100, "AcDbXrecord", 1, "kept", 40, "1.5")
                + G(0, "ENDSEC")
                + G(0, "SECTION", 2, "ACDSDATA", 70, "2", 0, "ACDSSCHEMA", 90, "0", 1, "AcDb3DSolid_ASM_Data", 0, "ENDSEC")
                + G(0, "EOF");

            DxfFile file = DxfFile.Parse(text);

            Assert.Equal(text, file.ToText());
            Assert.Empty(file.ModelSpace);
            Assert.Empty(file.PaperSpace);
            Assert.Empty(file.Blocks);
            Assert.Empty(file.LayerTable);
        }

        [Fact]
        public void AFileTheReaderRefuses_IsRefusedWithTheReadersMessage_NoEofAmongThem()
        {
            string whole = MixedDrawing();
            string[] refused =
            {
                whole.Substring(0, whole.LastIndexOf("  0\nEOF", StringComparison.Ordinal)),
                whole.Substring(0, whole.LastIndexOf("  0\nEOF", StringComparison.Ordinal)) + " 10\n",
                "  0\nSECTION\nabc\nENTITIES\n  0\nENDSEC\n  0\nEOF\n",
                G(0, "SECTION", 2, "HEADER", 0, "SECTION", 2, "ENTITIES", 0, "ENDSEC", 0, "EOF"),
                G(0, "SECTION", 999, "a comment", 2, "ENTITIES", 0, "ENDSEC", 0, "EOF"),
                G(0, "SECTION", 2, "HEADER", 0, "ENDSEC", 0, "EOF"),
                G(0, "LINE", 0, "EOF"),
                G(0, "ENDSEC", 0, "EOF"),
                G(0, "SECTION", 2, "ENTITIES", 0, "EOF"),
                "AutoCAD Binary DXF\r\n\u001a\0\0\0SECTION",
                string.Empty,
            };

            foreach (string text in refused)
            {
                string expected = Assert.Throws<FormatException>(() => DxfReader.Parse(text)).Message;

                Assert.Equal(expected, Assert.Throws<FormatException>(() => DxfDatabase.Parse(text)).Message);
                Assert.Equal(expected, Assert.Throws<FormatException>(() => DxfModel.Opened(Encoding.ASCII.GetBytes(text))).Message);
            }
        }

        /// <summary>A file of R2000 in the code page 932 with a TEXT whose words end in a byte, then the records after it.</summary>
        private static byte[] Japanese(byte last, string after, string lineEnd)
        {
            string head = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1015", 9, "$DWGCODEPAGE", 3, "ANSI_932", 0, "ENDSEC", 0, "SECTION", 2, "ENTITIES")
                + G(0, "TEXT", 5, "1A", 8, "0", 10, "0", 20, "0", 30, "0", 40, "2.5") + "  1\nAB#\n";
            string tail = after + G(0, "LINE", 5, "1B", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0", 0, "ENDSEC", 0, "EOF");
            byte[] bytes = Encoding.ASCII.GetBytes((head + tail).Replace("\n", lineEnd));
            bytes[Array.IndexOf(bytes, (byte)'#')] = last;
            return bytes;
        }

        [Fact]
        public void ALeadByteOfACodePageAtTheEndOfALine_EndsWithItsLine_InTheReaderAsInTheFile()
        {
            // 0x82 opens a letter of two bytes in the code page 932; a line feed after it still ends the line, as AutoCAD
            // reads a file line by line, so the line "CD" after the words is no group code, and without it the LINE is read.
            foreach (string lineEnd in new[] { "\n", "\r\n" })
            {
                byte[] refused = Japanese(0x82, "CD\n", lineEnd);
                string expected = Assert.Throws<FormatException>(() => DxfReader.Read(new MemoryStream(refused))).Message;

                Assert.Contains("line 35 should be a group code", expected);
                Assert.Equal(expected, Assert.Throws<FormatException>(() => Opened(refused)).Message);

                byte[] read = Japanese(0x82, string.Empty, lineEnd);
                DxfDocument document = Read(read, false);
                DxfFile file = Opened(read);

                Assert.Equal(new[] { "TEXT", "LINE" }, document.Shapes.Select(e => e.Type));
                Assert.StartsWith("AB", Assert.IsType<DxfText>(document.Shapes[0].Shape).Value);
                Assert.Equal(Assert.IsType<DxfText>(document.Shapes[0].Shape).Value, Assert.IsType<DxfText>(file.ModelSpace[0].Shape).Value);
                Assert.Equal(read, Saved(file));
            }
        }

        [Fact]
        public void NoArgument_IsTakenForAFileOrAStream()
        {
            DxfDatabase file = DxfDatabase.Parse(MixedDrawing());

            Assert.Throws<ArgumentNullException>(() => DxfDatabase.Open((string)null));
            Assert.Throws<ArgumentNullException>(() => DxfDatabase.Open((Stream)null));
            Assert.Throws<ArgumentNullException>(() => DxfDatabase.Parse(null));
            Assert.Throws<ArgumentNullException>(() => file.Save((string)null));
            Assert.Throws<ArgumentNullException>(() => file.Save((Stream)null));
        }

        #endregion

        #region The views

        [Fact]
        public void ModelSpace_IsTheEntitiesInTheOrderOfTheFile_APolylineWithItsVertices_AnInsertWithItsAttributes()
        {
            DxfFile file = DxfFile.Parse(MixedDrawing());

            Assert.Equal(new[] { "LINE", "CIRCLE", "LWPOLYLINE", "HATCH", "3DFACE", "TEXT", "POLYLINE", "INSERT" }, file.ModelSpace.Select(i => i.Type));
            Assert.Equal(new[] { "1A", "1B", "1C", "1D", "1E", "1F", "20", "24" }, file.ModelSpace.Select(i => i.Handle));

            IReadOnlyList<DxfTypedValue> polyline = file.ModelSpace[6].Groups;
            IReadOnlyList<DxfTypedValue> insert = file.ModelSpace[7].Groups;

            Assert.Equal(new[] { "VERTEX", "VERTEX", "SEQEND" }, polyline.Where(g => g.TypeCode == 0).Select(g => g.Value));
            Assert.Equal(new[] { "ATTRIB", "SEQEND" }, insert.Where(g => g.TypeCode == 0).Select(g => g.Value));
            Assert.Equal("B-12", insert.First(g => g.TypeCode == 1).Value);
            Assert.IsType<GeoPolyline3>(file.ModelSpace[6].Shape);
        }

        [Fact]
        public void TheShapes_AreThoseTheReaderReads_EntityForEntity_InsertsLeftUnplaced()
        {
            byte[] bytes = Encoding.ASCII.GetBytes(MixedDrawing());
            DxfFile file = Opened(bytes);
            DxfDocument read = Read(bytes, false);

            Assert.Equal(read.Shapes.Select(e => Bits(e.Shape)), Shapes(file.ModelSpace));
            Assert.Equal(read.Shapes.Select(e => e.Handle), file.ModelSpace.Where(i => !(i.Shape is DxfInsert)).SelectMany(i => i.Shapes.Select(_ => i.Handle)));
            Assert.Equal(1, read.Skipped["HATCH"]);
            Assert.Equal(1, read.Skipped["INSERT"]);
        }

        [Fact]
        public void A3dFaceOutOfFlat_IsTheTwoTrianglesTheReaderReads_AndHasNoOneShape()
        {
            DxfItem face = DxfFile.Parse(MixedDrawing()).ModelSpace[4];

            Assert.Equal(2, face.Shapes.Count);
            Assert.All(face.Shapes, t => Assert.IsType<GeoTriangle3>(t));
            Assert.Null(face.Shape);
        }

        [Fact]
        public void AnEntityTheReaderDoesNotRead_HasNoShape_ButItsLookAndEveryGroup()
        {
            DxfItem hatch = DxfFile.Parse(MixedDrawing()).ModelSpace[3];

            Assert.Empty(hatch.Shapes);
            Assert.Null(hatch.Shape);
            Assert.Equal("1D", hatch.Handle);
            Assert.Equal("hatch", hatch.Layer);
            Assert.Equal(2, hatch.Colour);
            Assert.Equal(new[] { 5, 8, 62, 2, 70, 71, 91, 75, 76, 98, 1001, 1000 }, hatch.Groups.Select(g => (int)g.TypeCode));
            Assert.Equal("kept", hatch.Groups.Last().Value);
        }

        [Fact]
        public void TheShape_IsReadOnce_TheSameObjectEachTimeItIsAskedFor()
        {
            DxfItem line = DxfFile.Parse(MixedDrawing()).ModelSpace[0];

            Assert.Same(line.Shape, line.Shape);
            Assert.Same(line.Shapes, line.Shapes);
            Assert.Same(line.Shape, Assert.Single(line.Shapes));
            Assert.NotSame(line.Groups, line.Groups);
        }

        [Fact]
        public void TheShapes_AskedForFromManyThreadsAtOnce_AreOneList_TheReadersShapes()
        {
            byte[] bytes = Encoding.ASCII.GetBytes(MixedDrawing());

            for (int round = 0; round < 20; round++)
            {
                DxfFile file = Opened(bytes);
                var seen = new System.Collections.Concurrent.ConcurrentBag<(int, IReadOnlyList<object>, int)>();

                System.Threading.Tasks.Parallel.For(0, 64, i =>
                {
                    DxfItem item = file.ModelSpace[i % file.ModelSpace.Count];
                    IReadOnlyList<DxfShape> placed = item.Shape is DxfInsert insert ? insert.GetPlacedEntities() : null;
                    seen.Add((i % file.ModelSpace.Count, item.Shapes, placed?.Count ?? -1));
                });

                foreach (IGrouping<int, (int Item, IReadOnlyList<object> Shapes, int Placed)> item in seen.GroupBy(s => s.Item1))
                {
                    Assert.Single(item.Select(s => s.Shapes).Distinct());
                    Assert.Single(item.Select(s => s.Placed).Distinct());
                }

                Assert.Equal(Read(bytes, false).Shapes.Select(e => Bits(e.Shape)), Shapes(file.ModelSpace));
            }
        }

        [Fact]
        public void PaperSpace_IsListedApart_ItsShapesReadAsThoseOfModelSpace()
        {
            DxfFile file = DxfFile.Parse(MixedDrawing());
            DxfItem circle = Assert.Single(file.PaperSpace);

            Assert.Equal("27", circle.Handle);
            Assert.Equal(20.0, Assert.IsType<GeoCircle3>(circle.Shape).Radius);
            Assert.DoesNotContain(file.ModelSpace, i => i.Handle == "27");
        }

        [Fact]
        public void TheLook_IsTheEntitysOwnAsWritten_NothingTakenFromABlock()
        {
            DxfFile file = DxfFile.Parse(MixedDrawing());
            DxfItem line = file.Blocks["BOLT"].Items[1];

            Assert.Equal("0", line.Layer);
            Assert.Equal(0, line.Colour);
            Assert.Equal("parts", file.ModelSpace[7].Layer);
        }

        [Fact]
        public void TheLook_IsAsTheRecordWritesIt_ByBlockByLayerAndSpacesKept_WhereTheReaderReadsItsMeaning()
        {
            string text = Drawing("AC1015", G(0, "LINE", 5, " 2A ", 8, " Steel\\U+00E9 ", 6, "BYLAYER", 62, "256", 370, "-1", 420, "33554432", 39, "2.5", 10, "0", 20, "0", 11, "1", 21, "0")
                + G(0, "LINE", 62, "0", 370, "-2", 6, "ByBlock", 10, "0", 20, "0", 11, "1", 21, "0"));
            DxfFile file = DxfFile.Parse(text);
            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol });
            DxfItem first = file.ModelSpace[0];
            DxfItem second = file.ModelSpace[1];

            Assert.Equal("2A", first.Handle);
            Assert.Equal(" Steel\\U+00E9 ", first.Layer);
            Assert.Equal("BYLAYER", first.Linetype);
            Assert.Equal(256, first.Colour);
            Assert.Equal(-1, first.Lineweight);
            Assert.Equal(33554432, first.TrueColour);
            Assert.Equal(2.5, first.Thickness);
            Assert.Null(second.Layer);
            Assert.Equal(0, second.Colour);
            Assert.Equal(-2, second.Lineweight);
            Assert.Equal("ByBlock", second.Linetype);
            Assert.Null(second.TrueColour);

            // The reader reads their meanings: layer Steelé, no colour, no lineweight, the true colour's 24 bits.
            Assert.Equal("Steelé", read.Shapes[0].Layer);
            Assert.Null(read.Shapes[0].Color);
            Assert.Null(read.Shapes[0].LineWeight);
            Assert.Equal(0, read.Shapes[0].TrueColor);
            Assert.Equal("0", read.Shapes[1].Layer);
        }

        [Fact]
        public void TheBlocks_AreByNameInEitherCase_TheFirstOfTwoKept_TheirEntitiesInTheirOwnCoordinates()
        {
            string blocks = Bolt + G(0, "BLOCK", 8, "0", 2, "bolt", 70, "0", 10, "0", 20, "0", 30, "0") + G(0, "POINT", 8, "0", 10, "9", 20, "9") + G(0, "ENDBLK", 8, "0")
                + G(0, "BLOCK", 8, "0", 2, "*Paper_Space0", 70, "0", 10, "0", 20, "0", 30, "0") + G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "420", 21, "297") + G(0, "ENDBLK", 8, "0");
            string text = Drawing("AC1015", G(0, "LINE", 8, "0", 10, "0", 20, "0", 11, "1", 21, "0"), blocks);
            DxfFile file = DxfFile.Parse(text);

            Assert.Equal(new[] { "*Paper_Space0", "BOLT" }, file.Blocks.Keys);
            Assert.Same(file.Blocks["bolt"], file.Blocks["BOLT"]);
            Assert.Equal(new[] { "CIRCLE", "LINE" }, file.Blocks["BOLT"].Items.Select(i => i.Type));
            DxfReaderRoundTripTests.Same(new GeoPoint3(1, 2, 0), Assert.IsType<GeoCircle3>(file.Blocks["BOLT"].Items[0].Shape).Center);
            Assert.Equal(text, file.ToText());
        }

        [Fact]
        public void TheHeader_GivesEachVariableItsGroups_AndTheVersionAndCodePage()
        {
            string text = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1015", 9, "$DWGCODEPAGE", 3, "ANSI_1252", 9, "$INSBASE", 10, "1.5", 20, "2", 30, "0", 9, "$HANDSEED", 5, "2F", 0, "ENDSEC")
                + G(0, "SECTION", 2, "ENTITIES", 0, "ENDSEC", 0, "EOF");
            DxfFile file = DxfFile.Parse(text);
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Equal("AC1015", db.Version);
            Assert.Equal("ANSI_1252", db.CodePage);
            Assert.Equal(new[] { "10 1.5", "20 2", "30 0" }, db.GetHeader("$INSBASE").Select(g => g.ToString()));
            Assert.Equal("2F", db.Handseed);
            Assert.Equal(new[] { "$ACADVER", "$DWGCODEPAGE", "$HANDSEED", "$INSBASE" }, file.Header.Keys);
            Assert.Equal(new[] { "10 1.5", "20 2", "30 0" }, file.Header["$INSBASE"].Select(g => g.ToString()));
            Assert.Equal("2F", Assert.Single(file.Header["$HANDSEED"]).Value);
            Assert.Equal("UTF-8", DxfFile.Parse(Drawing("AC1032", string.Empty)).CodePage);
            Assert.Equal("ANSI_1252", DxfFile.Parse(Drawing("AC1015", string.Empty)).CodePage);
        }

        [Fact]
        public void AHeaderVariableNamedTwice_IsAsLastGiven_AsTheReaderReadsTheVersion()
        {
            string text = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1009", 9, "$INSUNITS", 70, "1", 9, "$ACADVER", 1, "AC1015", 9, "$INSUNITS", 70, "4", 0, "ENDSEC")
                + G(0, "SECTION", 2, "ENTITIES", 0, "ENDSEC", 0, "EOF");
            DxfFile file = DxfFile.Parse(text);

            Assert.Equal(DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol }).Version, file.Version);
            Assert.Equal("AC1015", file.Version);
            Assert.Equal("4", Assert.Single(file.Header["$INSUNITS"]).Value);
            Assert.Equal("AC1015", Assert.Single(file.Header["$ACADVER"]).Value);
        }

        [Fact]
        public void TheCodePage_IsThatTheTextIsReadIn_AndWhatWasSaidOfItIsInTheWarnings()
        {
            byte[] page = Encoding.ASCII.GetBytes(Drawing("AC1015", G(0, "LINE", 8, "St#l", 10, "0", 20, "0", 11, "1", 21, "0")).Replace("$INSUNITS", "$DWGCODEPAGE").Replace(" 70\n4\n", "  3\nANSI_1250\n"));
            page[Array.IndexOf(page, (byte)'#')] = 0x8A;
            byte[] utf8 = new UTF8Encoding(false).GetBytes(Drawing("AC1015", G(0, "LINE", 8, "Grün", 10, "0", 20, "0", 11, "1", 21, "0")));

            DxfFile inPage = Opened(page);
            DxfFile inUtf8 = Opened(utf8);

            Assert.Equal("ANSI_1250", inPage.CodePage);
            Assert.Equal("Š", inPage.ModelSpace[0].Layer.Substring(2, 1));
            Assert.Empty(inPage.Warnings);
            Assert.Equal("UTF-8", inUtf8.CodePage);
            Assert.Equal("The file is of a version before R2007, whose text is in a code page, but its text is UTF-8 and was read as such.", Assert.Single(inUtf8.Warnings));
        }

        [Fact]
        public void ACodePageThatDoesNotWriteAsciiAsAscii_IsRefusedByTheReaderAndTheFile_WithOneMessage()
        {
            foreach (string page in new[] { "ANSI_1200", "ANSI_1201", "ANSI_37" })
            {
                byte[] bytes = Encoding.ASCII.GetBytes(Drawing("AC1015", G(0, "LINE", 8, "St#l", 10, "0", 20, "0", 11, "1", 21, "0")).Replace("$INSUNITS", "$DWGCODEPAGE").Replace(" 70\n4\n", "  3\n" + page + "\n"));
                bytes[Array.IndexOf(bytes, (byte)'#')] = 0xE9;
                string expected = Assert.Throws<FormatException>(() => DxfReader.Read(new MemoryStream(bytes))).Message;

                Assert.Contains("names the code page " + page, expected);
                Assert.Equal(expected, Assert.Throws<FormatException>(() => Opened(bytes)).Message);
            }
        }

        [Fact]
        public void ACodePageThatShiftsItsState_IsRefusedByTheReaderAndTheFile_WithOneMessage()
        {
            // ISO-2022 (50220, 50225) reads an escape or a shift as a change of state and leaves it out of the text, so the
            // code line " 10" with ESC ( B or SO after it would read as 10 where AutoCAD and the file read no group code.
            foreach ((string page, string shift) in new[] { ("ANSI_50220", "\u001b(B"), ("ANSI_50225", "\u000e"), ("ANSI_50222", "\u001b(B") })
            {
                string text = Drawing("AC1015", G(0, "LINE", 8, "St#l", 10, "0", 20, "0", 11, "1", 21, "0")).Replace("$INSUNITS", "$DWGCODEPAGE").Replace(" 70\n4\n", "  3\n" + page + "\n");
                byte[] bytes = Encoding.ASCII.GetBytes(text.Replace("\n 10\n", "\n 10" + shift + "\n"));
                bytes[Array.IndexOf(bytes, (byte)'#')] = 0xE9;
                string expected = Assert.Throws<FormatException>(() => DxfReader.Read(new MemoryStream(bytes))).Message;

                Assert.Contains("names the code page " + page, expected);
                Assert.Equal(expected, Assert.Throws<FormatException>(() => Opened(bytes)).Message);
            }
        }

        [Fact]
        public void APlacedEntity_IsOnTheLayerSpelledAsTheTableThenTheDrawingSpellIt_WhicheverInsertIsPlacedFirst()
        {
            string blocks = G(0, "BLOCK", 8, "0", 2, "A", 70, "0", 10, "0", 20, "0", 30, "0") + G(0, "LINE", 8, "steel", 10, "0", 20, "0", 11, "1", 21, "0") + G(0, "ENDBLK", 8, "0")
                + G(0, "BLOCK", 8, "0", 2, "B", 70, "0", 10, "0", 20, "0", 30, "0") + G(0, "LINE", 8, "STEEL", 10, "0", 20, "0", 11, "2", 21, "0") + G(0, "ENDBLK", 8, "0");
            string inserts = G(0, "INSERT", 5, "10", 8, "0", 2, "A", 10, "0", 20, "0") + G(0, "INSERT", 5, "11", 8, "0", 2, "B", 10, "0", 20, "0");

            string[] firstA = PlacedLayers(Drawing("AC1015", inserts, blocks), false);
            string[] firstB = PlacedLayers(Drawing("AC1015", inserts, blocks), true);

            Assert.Equal(new[] { "steel", "STEEL" }, firstA);
            Assert.Equal(firstA, firstB);

            // A LINE of the drawing on Steel before the INSERT: the reader spells the placed line so, and so does the file.
            string text = Drawing("AC1015", G(0, "LINE", 8, "Steel", 10, "0", 20, "0", 11, "1", 21, "0") + inserts, blocks);

            Assert.Equal(new[] { "Steel", "Steel" }, PlacedLayers(text, true));
            Assert.Equal(DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol }).Shapes.Skip(1).Select(e => e.Layer), PlacedLayers(text, false));
        }

        /// <summary>The layers of the entities the two INSERTs of a drawing place, the second placed first where asked.</summary>
        private static string[] PlacedLayers(string text, bool secondFirst)
        {
            DxfInsert[] inserts = DxfFile.Parse(text).ModelSpace.Select(i => i.Shape).OfType<DxfInsert>().ToArray();
            string second = secondFirst ? Assert.Single(inserts[1].GetPlacedEntities()).Layer : null;
            string first = Assert.Single(inserts[0].GetPlacedEntities()).Layer;
            second = second ?? Assert.Single(inserts[1].GetPlacedEntities()).Layer;
            return new[] { first, second };
        }

        [Fact]
        public void AnInsertOnTheExtrusionDown_StandsMirrored_AndPlacesWhatTheReaderPlaces()
        {
            // The INSERT at (10, 20, 5) on the extrusion (0, 0, -1), whose x axis is the world's -x, turned 30 degrees.
            string text = Drawing("AC1015", G(0, "INSERT", 5, "40", 8, "0", 2, "BOLT", 10, "10", 20, "20", 30, "5", 50, "30", 210, "0", 220, "0", 230, "-1"), Bolt);
            DxfFile file = DxfFile.Parse(text);
            DxfInsert insert = Assert.IsType<DxfInsert>(Assert.Single(file.ModelSpace).Shape);

            DxfReaderRoundTripTests.Same(new GeoPoint3(-10, 20, -5), insert.Position);
            DxfReaderRoundTripTests.Same(new GeoPoint3(0, 0, -1), new GeoPoint3(insert.Normal.X, insert.Normal.Y, insert.Normal.Z));
            Assert.Equal(30.0 * Math.PI / 180.0, insert.Rotation);
            Assert.Equal(Expanded(DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol }).Shapes), Expanded(file.ModelSpace));
        }

        [Fact]
        public void AnInsertThatPlacesNothing_SaysWhy()
        {
            string text = Drawing("AC1015", G(0, "INSERT", 5, "40", 8, "0", 2, "NOPE", 10, "0", 20, "0") + G(0, "INSERT", 5, "41", 8, "0", 2, "BOLT", 10, "0", 20, "0"),
                Bolt.Replace(G(0, "ENDBLK"), G(0, "HATCH", 8, "0", 0, "ENDBLK")));
            DxfInsert[] inserts = DxfFile.Parse(text).ModelSpace.Select(i => i.Shape).OfType<DxfInsert>().ToArray();

            Assert.Empty(inserts[0].GetPlacedEntities(out IReadOnlyList<string> missing));
            Assert.Contains(missing, w => w.Contains("NOPE"));
            Assert.Equal(2, inserts[1].GetPlacedEntities(out IReadOnlyList<string> hatch).Count);
            Assert.Equal("1 HATCH not placed: 1 of a type that is not read.", Assert.Single(hatch));
        }

        [Fact]
        public void APlacingThatLeavesSomethingOut_LogsOneDebugLineOfItsOwn_EachCall()
        {
            string text = Drawing("AC1015", G(0, "INSERT", 5, "41", 8, "0", 2, "BOLT", 10, "0", 20, "0"), Bolt.Replace(G(0, "ENDBLK"), G(0, "HATCH", 8, "0", 0, "ENDBLK")));
            DxfInsert insert = Assert.IsType<DxfInsert>(Assert.Single(DxfFile.Parse(text).ModelSpace).Shape);
            var written = new List<(GeometryHelperLogLevel Level, string Message)>();

            try
            {
                GeometryHelperLog.Enable = true;
                GeometryHelperLog.Writer = (level, message, exception) => written.Add((level, message));
                insert.GetPlacedEntities();
                insert.GetPlacedEntities();
            }
            finally
            {
                GeometryHelperLog.Writer = null;
            }

            Assert.Equal(2, written.Count);
            Assert.All(written, w => Assert.Equal(GeometryHelperLogLevel.Debug, w.Level));
            Assert.All(written, w => Assert.Equal("DxfFile: the INSERT 41 placed 2 entities: 1 HATCH not placed: 1 of a type that is not read.", w.Message));
        }

        [Fact]
        public void ABlock_HasItsNameBasePointAndFlags_AsTheReaderReadsThem()
        {
            string text = MixedDrawing();
            DxfFileBlock bolt = DxfFile.Parse(text).Blocks["BOLT"];
            DxfBlock read = Assert.Single(DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol }).Blocks);

            Assert.Equal(read.Name, bolt.Name);
            DxfReaderRoundTripTests.Same(read.BasePoint, bolt.BasePoint);
            DxfReaderRoundTripTests.Same(new GeoPoint3(1, 2, 0), bolt.BasePoint);
            Assert.Equal(read.Flags, bolt.Flags);
            Assert.Equal(2, bolt.Items.Count);
        }

        [Fact]
        public void AnInsert_IsItsBlockPositionTurnScalesAndArray_AndPlacesWhatTheReaderPlaces()
        {
            byte[] bytes = Encoding.ASCII.GetBytes(MixedDrawing());
            DxfFile file = Opened(bytes);
            DxfInsert insert = Assert.IsType<DxfInsert>(file.ModelSpace[7].Shape);

            Assert.Equal("BOLT", insert.BlockName);
            DxfReaderRoundTripTests.Same(new GeoPoint3(50, 60, 0), insert.Position);
            Assert.Equal(45.0 * Math.PI / 180.0, insert.Rotation);
            Assert.Equal(new[] { 1.5, -1.5, 1.0 }, new[] { insert.ScaleX, insert.ScaleY, insert.ScaleZ });
            Assert.Equal(new[] { 1, 1 }, new[] { insert.Columns, insert.Rows });
            Assert.Equal(Expanded(Read(bytes, true).Shapes), Expanded(file.ModelSpace));
            Assert.Equal(new[] { "parts", "parts" }, insert.GetPlacedEntities().Select(e => e.Layer));
        }

        [Fact]
        public void TheLayers_AreThoseOfTheLayerTable_AsTheReaderReadsThem()
        {
            string text = Layers("Steel", "STEEL", "Wand") + G(0, "SECTION", 2, "ENTITIES", 0, "ENDSEC", 0, "EOF");
            DxfFile file = DxfFile.Parse(text);
            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol });

            Assert.Equal(new[] { "Steel", "Wand" }, file.LayerTable.Select(l => l.Name));
            Assert.Equal(read.LayerTable.Select(l => l.Name + l.Color + l.Linetype), file.LayerTable.Select(l => l.Name + l.Color + l.Linetype));
        }

        #endregion

        #region The drawings of ezdxf

        [Fact]
        public void TheDrawingsOfEzdxf_FromR12ToR2018_AreSavedByteForByte_InTheirOwnEncodingAndLineEnds()
        {
            foreach ((string text, Encoding encoding) in new[] { (DxfFileEzdxfDrawings.R12, Encoding.ASCII), (DxfFileEzdxfDrawings.R2000, Encoding.GetEncoding(1252)), (DxfFileEzdxfDrawings.R2018, new UTF8Encoding(false)) })
            {
                foreach (byte[] bytes in new[] { Ezdxf(text, encoding), encoding.GetBytes(text) })
                {
                    DxfDatabase file = DxfModel.Opened(bytes);

                    Assert.Equal(bytes, DxfModel.Saved(file));
                    Assert.Equal(text, file.ToText().Replace("\r\n", "\n"));
                }
            }
        }

        [Fact]
        public void TheDrawingsOfEzdxf_ListTheEntitiesEzdxfLists_TheirShapesAndPlacedInsertsThoseOfTheReader()
        {
            foreach ((string text, Encoding encoding, string version) in new[] { (DxfFileEzdxfDrawings.R12, Encoding.ASCII, "AC1009"), (DxfFileEzdxfDrawings.R2000, Encoding.GetEncoding(1252), "AC1015"), (DxfFileEzdxfDrawings.R2018, new UTF8Encoding(false), "AC1032") })
            {
                byte[] bytes = Ezdxf(text, encoding);
                DxfFile file = Opened(bytes);
                DxfDocument flat = Read(bytes, false);
                DxfDocument placed = Read(bytes, true);

                Assert.Equal(version, file.Version);
                Assert.Equal(version == "AC1009" ? 12 : 27, file.ModelSpace.Count);
                Assert.Equal(flat.Shapes.Select(e => Bits(e.Shape)), Shapes(file.ModelSpace));
                Assert.Equal(Expanded(placed.Shapes), Expanded(file.ModelSpace));
                Assert.Equal(placed.LayerTable.Select(l => l.Name), file.LayerTable.Select(l => l.Name));

                foreach (DxfBlock block in placed.Blocks)
                {
                    Assert.Equal(Expanded(block.Shapes), Expanded(file.Blocks[block.Name].Items));
                }
            }
        }

        [Fact]
        public void TheDrawingsOfEzdxf_KeepWhatTheLibraryDoesNotRead_HatchDimensionImageAndTheRest()
        {
            DxfFile file = Opened(Ezdxf(DxfFileEzdxfDrawings.R2018, new UTF8Encoding(false)));
            string[] types = file.ModelSpace.Select(i => i.Type).ToArray();

            foreach (string type in new[] { "HATCH", "DIMENSION", "LEADER", "MULTILEADER", "IMAGE", "MESH" })
            {
                Assert.Contains(type, types);
                Assert.Null(file.ModelSpace.First(i => i.Type == type).Shape);
            }

            Assert.Equal("Bold\nline two é", Assert.IsType<DxfText>(file.ModelSpace.First(i => i.Type == "MTEXT").Shape).Value);
            Assert.Contains(file.LayerTable, l => l.Name == "Wand€Außen");
            Assert.Contains(file.ModelSpace[0].Groups, g => g.TypeCode == 1001 && g.Value == "GHPROBE");
            Assert.Contains(file.ModelSpace[0].Groups, g => g.TypeCode == 1000 && g.Value == "note é");
            Assert.Contains("*Paper_Space0", file.Blocks.Keys);
            Assert.Equal(new[] { "CIRCLE", "VIEWPORT" }, file.PaperSpace.Select(i => i.Type));
        }

        [Fact]
        public void TheInsertsOfEzdxf_StandWhereEzdxfReadsThem()
        {
            DxfFile file = Opened(Ezdxf(DxfFileEzdxfDrawings.R2000, Encoding.GetEncoding(1252)));
            DxfInsert[] inserts = file.ModelSpace.Select(i => i.Shape).OfType<DxfInsert>().ToArray();

            // As ezdxf 1.4.5 reads them: BOLT turned 45 degrees and scaled 1.5, -1.5 and 2; FRAME at (200, 0) on the extrusion
            // (0, 0.6, 0.8), which is (-200, 0, 0) in the world, turned -90 degrees; BOLT as a MINSERT of 4 columns 30 apart by
            // 3 rows 20 apart.
            Assert.Equal(3, inserts.Length);
            DxfReaderRoundTripTests.Same(new GeoPoint3(50, 60, 0), inserts[0].Position);
            Assert.Equal(new[] { 1.5, -1.5, 2.0 }, new[] { inserts[0].ScaleX, inserts[0].ScaleY, inserts[0].ScaleZ });
            Assert.Equal(45.0 * Math.PI / 180.0, inserts[0].Rotation);
            Assert.Equal("FRAME", inserts[1].BlockName);
            Assert.True(inserts[1].Position.IsEqualTo(new GeoPoint3(-200, 0, 0), new Tolerance(1E-12, 1E-12, 1E-12)));
            Assert.True(inserts[1].Normal.IsEqualTo(new GeoVector3(0, 0.6, 0.8), Tol));
            Assert.Equal(1.5 * Math.PI, inserts[1].Rotation, 15);
            Assert.Equal(new[] { 4, 3 }, new[] { inserts[2].Columns, inserts[2].Rows });
            Assert.Equal(new[] { 30.0, 20.0 }, new[] { inserts[2].ColumnSpacing, inserts[2].RowSpacing });
            Assert.Equal(24, inserts[2].GetPlacedEntities().Count);
        }

        #endregion
    }
}
