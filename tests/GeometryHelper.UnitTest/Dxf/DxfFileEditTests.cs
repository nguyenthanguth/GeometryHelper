using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfLines;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A DXF file opened with <see cref="DxfFile"/>, edited and saved: the entities changed, added and removed are written as
    /// the file writes them, and every other record keeps its bytes.
    /// </summary>
    public class DxfFileEditTests
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

        /// <summary>The records of a text, each its lines from its group 0 to the next, as the file has them.</summary>
        private static List<string> Records(string text)
        {
            var records = new List<string>();
            var current = new StringBuilder();
            string[] lines = text.Split('\n');

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == "0" && current.Length > 0)
                {
                    records.Add(current.ToString());
                    current.Clear();
                }

                current.Append(lines[i]).Append('\n').Append(lines[i + 1]).Append('\n');
            }

            records.Add(current.ToString());
            return records;
        }

        /// <summary>The records of the second text not among those of the first, and of the first not in the second, each once.</summary>
        private static void Differ(string before, string after, out List<string> gone, out List<string> come)
        {
            var left = Records(before);
            come = new List<string>();

            foreach (string record in Records(after))
            {
                if (!left.Remove(record))
                {
                    come.Add(record);
                }
            }

            gone = left;
        }

        private static string HandleOf(string record)
        {
            string[] lines = record.Split('\n');

            for (int i = 2; i + 1 < lines.Length; i += 2)
            {
                if (lines[i].Trim() == "5")
                {
                    return lines[i + 1].Trim();
                }
            }

            return null;
        }

        private static DxfItem ByHandle(DxfFile file, string handle) => file.ModelSpace.Single(i => i.Handle == handle);

        private static ulong Hex(string handle) => ulong.Parse(handle.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

        /// <summary>Every handle of a text, the header's left out.</summary>
        private static List<ulong> Handles(string text)
            => Records(text).Where(r => !r.Contains("\nHEADER\n")).Select(HandleOf).Where(h => h != null).Select(Hex).ToList();

        private static string Bits(GeoPoint3 p)
            => BitConverter.DoubleToInt64Bits(p.X).ToString("x", CultureInfo.InvariantCulture) + "," + BitConverter.DoubleToInt64Bits(p.Y).ToString("x", CultureInfo.InvariantCulture) + "," + BitConverter.DoubleToInt64Bits(p.Z).ToString("x", CultureInfo.InvariantCulture);

        private static readonly string Hand = G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1015", 9, "$HANDSEED", 5, "40", 9, "$EXTMIN", 10, "0", 20, "0", 30, "0", 9, "$EXTMAX", 10, "100", 20, "50", 30, "0", 0, "ENDSEC")
            + G(0, "SECTION", 2, "TABLES", 0, "TABLE", 2, "LTYPE", 5, "5", 70, "1", 0, "LTYPE", 5, "16", 2, "Continuous", 70, "0", 0, "LTYPE", 5, "17", 2, "DASHED", 70, "0", 0, "ENDTAB")
            + G(0, "TABLE", 2, "LAYER", 5, "2", 330, "0", 100, "AcDbSymbolTable", 70, "1", 0, "LAYER", 5, "10", 330, "2", 100, "AcDbSymbolTableRecord", 100, "AcDbLayerTableRecord", 2, "0", 70, "0", 62, "7", 6, "Continuous", 0, "ENDTAB")
            + G(0, "TABLE", 2, "BLOCK_RECORD", 5, "1", 70, "1", 0, "BLOCK_RECORD", 5, "1F", 330, "1", 100, "AcDbSymbolTableRecord", 100, "AcDbBlockTableRecord", 2, "*Model_Space", 0, "ENDTAB", 0, "ENDSEC")
            + G(0, "SECTION", 2, "ENTITIES")
            + G(0, "LINE", 5, "30", 330, "1F", 100, "AcDbEntity", 8, "0", 62, "     1", 100, "AcDbLine", 39, "5.000", 10, "0", 20, "0", 30, "0", 11, "100", 21, "50", 31, "0", 1001, "ACAD", 1000, "a note")
            + G(0, "ELLIPSE", 5, "31", 330, "1F", 100, "AcDbEntity", 8, "0", 100, "AcDbEllipse", 10, "10", 20, "20", 30, "0", 11, "30", 21, "0", 31, "0", 210, "0", 220, "0", 230, "1", 40, "0.5", 41, "0", 42, "6.283185307179586")
            + G(0, "HATCH", 5, "32", 330, "1F", 100, "AcDbEntity", 8, "0", 100, "AcDbHatch", 10, "0", 20, "0", 30, "0", 210, "0", 220, "0", 230, "1", 2, "SOLID", 70, "1", 71, "0", 91, "0", 75, "0", 76, "1", 98, "0")
            + G(0, "ENDSEC", 0, "EOF");

        #endregion

        #region Nothing changed

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string[] texts =
            {
                DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018, Hand, Hand.Replace("\n", "\r\n"),
                DxfReaderRoundTripTests.OneOfEach().Add(new DxfText(new GeoPoint3(1, 2, 0), new GeoVector3(0, 0, 1), 2.5, 0.3, "Grid A")).ToString(),
            };

            foreach (string text in texts)
            {
                DxfFile file = DxfFile.Parse(text);
                byte[] before = Saved(file);
                int shaped = 0;

                foreach (DxfItem item in file.ModelSpace.Concat(file.PaperSpace).ToList())
                {
                    item.Layer = item.Layer;
                    item.Colour = item.Colour;
                    item.TrueColour = item.TrueColour;
                    item.Linetype = item.Linetype;
                    item.Lineweight = item.Lineweight;
                    item.Thickness = item.Thickness;

                    if (item.Shape != null && !(item.Shape is DxfInsert) && item.Type != "HATCH")
                    {
                        item.Shape = item.Shape;
                        shaped++;
                    }

                    // The same segment in a box of its own is the same value.
                    if (item.Shape is GeoLine3 line)
                    {
                        item.Shape = new GeoLine3(line.StartPoint, line.EndPoint);
                    }
                }

                Assert.True(shaped >= 2);
                Assert.Equal(before, Saved(file));
                Assert.Equal(Encoding.UTF8.GetBytes(text), before);
            }
        }

        #endregion

        #region Changing an entity

        [Fact]
        public void ALineGivenANewSegment_IsWrittenAnew_ItsHandleReactorsAndExtendedDataKept_AndNoOtherRecordChanges()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfFile file = DxfFile.Parse(text);
            DxfItem line = ByHandle(file, "3E");
            var segment = new GeoLine3(new GeoPoint3(1.25, -2.5, 3.0000000000000004), new GeoPoint3(100.1, 0.2, 0.3));
            List<DxfTypedValue> was = line.Groups.ToList();

            line.Shape = segment;

            Assert.Equal("LINE", line.Type);
            Assert.Equal("3E", line.Handle);
            Assert.Equal(Bits(segment.StartPoint), Bits(((GeoLine3)line.Shape).StartPoint));
            Assert.Equal(Bits(segment.EndPoint), Bits(((GeoLine3)line.Shape).EndPoint));

            // Every group but those of the geometry is kept, in its order: the handle, the extension dictionary, the reactors,
            // the owner, the marker, the layer, and the extended data after.
            int[] geometry = { 10, 20, 30, 11, 21, 31 };
            Assert.Equal(was.Where(g => !geometry.Contains(g.TypeCode)).Select(g => g.ToString()), line.Groups.Where(g => !geometry.Contains(g.TypeCode)).Select(g => g.ToString()));
            Assert.Contains(line.Groups, g => g.TypeCode == 1001);

            // One record differs besides the header, the line's; in the header, no handle given, ezdxf's extents, unset (1E+20
            // above -1E+20), become the line's box and nothing else changes.
            string saved = Encoding.UTF8.GetString(Saved(file));
            Differ(text, saved, out List<string> gone, out List<string> come);
            Assert.Equal("3E", HandleOf(Assert.Single(gone, r => !r.Contains("\nHEADER\n"))));
            Assert.Equal("3E", HandleOf(Assert.Single(come, r => !r.Contains("\nHEADER\n"))));
            Func<string, string[]> others = t => DxfFile.Parse(t).Header.Where(v => v.Key != "$EXTMIN" && v.Key != "$EXTMAX")
                .OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => v.Key + " " + string.Join(" ", v.Value.Select(g => g.ToString()))).ToArray();
            Assert.Equal(others(text), others(saved));
            Assert.Equal(Bits(new GeoPoint3(1.25, -2.5, 0.3)), Bits(DxfDatabase.Parse(saved).Extmin));
            Assert.Equal(Bits(new GeoPoint3(100.1, 0.2, 3.0000000000000004)), Bits(DxfDatabase.Parse(saved).Extmax));

            DxfDocument read = DxfReader.Parse(saved, new DxfReadOptions { Tolerance = Tol });
            GeoLine3 again = (GeoLine3)read.Shapes.Single(e => e.Handle == "3E").Shape;
            Assert.Equal(Bits(segment.StartPoint), Bits(again.StartPoint));
            Assert.Equal(Bits(segment.EndPoint), Bits(again.EndPoint));
        }

        [Fact]
        public void AShapeWrittenAsAnotherType_ChangesTheType_AndKeepsTheHandleAndLook()
        {
            DxfFile file = DxfFile.Parse(Hand);
            DxfItem line = ByHandle(file, "30");
            var circle = new GeoCircle3(new GeoPoint3(5, 6, 0), new GeoVector3(0, 0, 1), 4.5);

            line.Shape = circle;

            Assert.Equal("CIRCLE", line.Type);
            Assert.Equal("30", line.Handle);
            Assert.Equal(1, line.Colour);
            Assert.Equal(5.0, line.Thickness);

            DxfFile again = Opened(Saved(file));
            DxfItem read = ByHandle(again, "30");
            var shape = (GeoCircle3)read.Shape;
            Assert.Equal("CIRCLE", read.Type);
            Assert.Equal(Bits(circle.Center), Bits(shape.Center));
            Assert.Equal(4.5, shape.Radius);

            // The thickness is written where a CIRCLE has it, spelled as the file spelled it; the colour as it was, padded.
            string record = Records(again.ToText()).Single(r => HandleOf(r) == "30");
            Assert.Contains(" 39\n5.000\n", record, StringComparison.Ordinal);
            Assert.Contains(" 62\n     1\n", record, StringComparison.Ordinal);
            Assert.Contains("100\nAcDbCircle\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("AcDbLine", record, StringComparison.Ordinal);
            Assert.EndsWith("1001\nACAD\n1000\na note\n", record, StringComparison.Ordinal);
        }

        [Fact]
        public void AChainSetInAFileOfR12_IsAPolylineWithItsVertices_ReadBackAsTheLightweightOneIs()
        {
            var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(60, 0), new GeoPoint2(60, 20), new GeoPoint2(0, 20) }, new[] { 0.0, 1.0, 0.0, 1.0 });
            DxfFile r12 = DxfFile.Parse(DxfFileEzdxfDrawings.R12);
            DxfItem line = ByHandle(r12, "41");

            line.Shape = loop;

            Assert.Equal("POLYLINE", line.Type);
            Assert.Equal("41", line.Handle);

            // The same loop added to a file of R2000 is a LWPOLYLINE, which the reader reads as the same shape, to the bit.
            DxfFile r2000 = DxfFile.Parse(DxfFileEzdxfDrawings.R2000);
            DxfItem added = r2000.ModelSpace.Add(loop);
            Assert.Equal("LWPOLYLINE", added.Type);

            string saved = r12.ToText();
            DxfDocument read = DxfReader.Parse(saved, new DxfReadOptions { Tolerance = Tol, PlanAs2D = true });
            DxfDocument other = DxfReader.Parse(r2000.ToText(), new DxfReadOptions { Tolerance = Tol, PlanAs2D = true });
            var mine = (GeoPolygonArc2)read.Shapes.Single(e => e.Handle == "41").Shape;
            var theirs = (GeoPolygonArc2)other.Shapes.Single(e => e.Handle == added.Handle).Shape;
            Assert.Equal(theirs.Vertices.Select(p => Bits(new GeoPoint3(p.X, p.Y, 0))), mine.Vertices.Select(p => Bits(new GeoPoint3(p.X, p.Y, 0))));
            Assert.Equal(theirs.Bulges.Select(BitConverter.DoubleToInt64Bits), mine.Bulges.Select(BitConverter.DoubleToInt64Bits));

            // Its records have no subclass markers, the vertices handles of their own, as $HANDLING is 1, and its end.
            Differ(DxfFileEzdxfDrawings.R12, saved, out _, out List<string> come);
            List<string> mineRecords = come.Where(r => !r.Contains("\nHEADER\n")).ToList();
            Assert.Equal(new[] { "POLYLINE", "VERTEX", "VERTEX", "VERTEX", "VERTEX", "SEQEND" }, mineRecords.Select(r => r.Split('\n')[1]));
            Assert.All(mineRecords, r => Assert.DoesNotContain("\n100\n", r, StringComparison.Ordinal));
            Assert.All(mineRecords, r => Assert.NotNull(HandleOf(r)));
        }

        [Fact]
        public void AChainAddedToAFileOfR13_IsAPolylineWithSubclassMarkers_AndAnEntityOfPaperSpaceStaysThere()
        {
            DxfFile r13 = DxfFile.Parse(Hand.Replace("AC1015", "AC1012"));
            var triangle = new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10));

            DxfItem added = r13.ModelSpace.Add(triangle, "0");

            Assert.Equal("POLYLINE", added.Type);
            Assert.Contains(added.Groups, g => g.TypeCode == 100 && g.Value == "AcDb2dPolyline");
            Assert.Equal(3, added.Groups.Count(g => g.TypeCode == 100 && g.Value == "AcDb2dVertex"));
            var read = (GeoPolygon3)DxfReader.Parse(r13.ToText(), new DxfReadOptions { Tolerance = Tol }).Shapes.Single(e => e.Handle == added.Handle).Shape;
            Assert.Equal(new[] { "0,0,0", "10,0,0", "0,10,0" }, read.Vertices.Select(p => p.X.ToString(CultureInfo.InvariantCulture) + "," + p.Y.ToString(CultureInfo.InvariantCulture) + "," + p.Z.ToString(CultureInfo.InvariantCulture)));

            // A circle of paper space made a 3D polyline: its vertices are of paper space too, and it stays in PaperSpace.
            DxfFile paper = DxfFile.Parse(DxfFileEzdxfDrawings.R2000);
            DxfItem circle = paper.PaperSpace.Single(i => i.Handle == "9C");
            circle.Shape = new GeoPolyline3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(5, 5, 5) });

            Assert.Equal(new[] { "1", "1", "1", "1" }, circle.Groups.Where(g => g.TypeCode == 67).Select(g => g.Value.Trim()));
            DxfFile again = Opened(Saved(paper));
            Assert.Equal("POLYLINE", again.PaperSpace.Single(i => i.Handle == "9C").Type);
            Assert.DoesNotContain(again.ModelSpace, i => i.Handle == "9C");
        }

        [Fact]
        public void AShapeTheFileCannotHold_OrOfAnEntityWithNoShape_IsRefused()
        {
            DxfFile r12 = DxfFile.Parse(DxfFileEzdxfDrawings.R12);
            DxfItem line = ByHandle(r12, "41");
            byte[] before = Saved(r12);

            Assert.Throws<ArgumentException>(() => line.Shape = new GeoEllipse3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 10, 5));
            Assert.Throws<ArgumentException>(() => line.Shape = GeoSpline3.FromCircle(new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 10)));
            Assert.Throws<ArgumentException>(() => line.Shape = new GeoXLine3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0)));
            Assert.Throws<ArgumentException>(() => line.Shape = new DxfText(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 2.5, 0, "two\nlines", true));
            Assert.Throws<ArgumentException>(() => r12.ModelSpace.Add(GeoSpline3.FromCircle(new GeoCircle3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 10))));

            // A box is six faces, and an entity is one.
            Assert.Throws<ArgumentException>(() => line.Shape = new GeoAabb3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 1)));
            Assert.Throws<ArgumentException>(() => line.Shape = "not a shape");
            Assert.Throws<ArgumentNullException>(() => line.Shape = null);
            Assert.Equal(before, Saved(r12));

            DxfFile r2000 = DxfFile.Parse(DxfFileEzdxfDrawings.R2000);
            Assert.Throws<InvalidOperationException>(() => r2000.ModelSpace.Single(i => i.Type == "HATCH").Shape = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            Assert.Throws<InvalidOperationException>(() => r2000.ModelSpace.First(i => i.Type == "INSERT").Shape = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));

            // The entities of a block are changed as those of the drawing are, since A1 made the blocks editable.
            DxfItem inBlock = r2000.Blocks.Values.First(b => b.Items.Count > 0).Items[0];
            inBlock.Colour = 3;
            Assert.Equal(3, inBlock.Colour);
        }

        [Fact]
        public void TheLookSetters_SetTheRawValue_NullTakesTheGroupOut_AndWhatTheFileCannotHoldIsRefused()
        {
            DxfFile file = DxfFile.Parse(Hand);
            DxfItem line = ByHandle(file, "30");
            DxfItem ellipse = ByHandle(file, "31");

            line.Colour = 256;
            line.Linetype = "dashed";
            line.Lineweight = 35;
            line.Thickness = 0.0;
            ellipse.Colour = 0;
            Assert.Throws<ArgumentNullException>(() => ellipse.Layer = null);

            Assert.Equal(256, line.Colour);
            Assert.Equal("dashed", line.Linetype);
            Assert.Equal(35, line.Lineweight);
            Assert.Equal(0.0, line.Thickness);
            Assert.Equal("0", ellipse.Layer);

            string record = Records(file.ToText()).Single(r => HandleOf(r) == "30");
            Assert.Contains("  8\n0\n  6\ndashed\n 62\n   256\n370\n35\n100\nAcDbLine\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain(" 39\n", record, StringComparison.Ordinal);
            Assert.Contains("100\nAcDbEntity\n  8\n0\n 62\n0\n100\nAcDbEllipse\n", Records(file.ToText()).Single(r => HandleOf(r) == "31"), StringComparison.Ordinal);

            Assert.Throws<ArgumentException>(() => line.Linetype = "CENTER");
            Assert.Throws<ArgumentOutOfRangeException>(() => line.Colour = 257);
            Assert.Throws<ArgumentOutOfRangeException>(() => line.Lineweight = 33);
            Assert.Throws<InvalidOperationException>(() => line.TrueColour = 0xFF8000);
            Assert.Throws<InvalidOperationException>(() => ellipse.Thickness = 2.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => line.Thickness = double.NaN);
            Assert.Throws<ArgumentException>(() => line.Layer = "a:b");
            Assert.Throws<ArgumentException>(() => line.Layer = " steel");

            DxfItem r12 = ByHandle(DxfFile.Parse(DxfFileEzdxfDrawings.R12), "41");
            Assert.Throws<InvalidOperationException>(() => r12.Lineweight = 25);
        }

        [Fact]
        public void ALayerSetThatTheTableDoesNotList_IsAdded_AndTheVerticesOfAPolylineAreMovedToIt()
        {
            DxfFile file = DxfFile.Parse(DxfFileEzdxfDrawings.R2000);
            DxfItem polyline = ByHandle(file, "44");

            polyline.Layer = "rebar";
            polyline.Layer = "REBAR";

            DxfLayer layer = Assert.Single(file.LayerTable, l => l.Name == "rebar");
            Assert.Equal(7, layer.Color);
            Assert.Equal("Continuous", layer.Linetype);
            Assert.Equal(new[] { "REBAR", "REBAR", "REBAR", "REBAR" }, polyline.Groups.Where(g => g.TypeCode == 8).Skip(1).Select(g => g.Value));

            DxfFile again = Opened(Saved(file));
            Assert.Single(again.LayerTable, l => string.Equals(l.Name, "rebar", StringComparison.OrdinalIgnoreCase));
            Assert.Equal("REBAR", ByHandle(again, "44").Layer);
        }

        #endregion

        #region Adding and removing

        [Fact]
        public void AShapeAdded_TakesAHandleAboveEveryOther_TheModelSpaceAsOwner_AndHandseedAboveIt()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfFile file = DxfFile.Parse(text);
            int count = file.ModelSpace.Count;
            ulong most = Handles(text).Max();

            DxfItem circle = file.ModelSpace.Add(new GeoCircle2(new GeoPoint2(1000, -500), 12.5), "holes");
            DxfItem chain = file.ModelSpace.Add(new GeoPolyline3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 1), new GeoPoint3(2, 0, 5) }));
            DxfItem look = file.ModelSpace.Add(new DxfShape(new GeoLine3(new GeoPoint3(1, 2, 3), new GeoPoint3(4, 5, 6)), "holes", color: 1, linetype: "Continuous", lineWeight: 50, thickness: 3.0));

            Assert.Equal(count + 3, file.ModelSpace.Count);
            Assert.Same(look, file.ModelSpace[count + 2]);
            Assert.Equal(most + 2, Hex(circle.Handle));
            Assert.Equal("holes", circle.Layer);
            Assert.Equal(new[] { "CIRCLE", "POLYLINE", "LINE" }, new[] { circle.Type, chain.Type, look.Type });
            Assert.Equal(1, look.Colour);
            Assert.Equal(50, look.Lineweight);
            Assert.Equal(3.0, look.Thickness);

            string saved = Encoding.UTF8.GetString(Saved(file));
            List<ulong> handles = Handles(saved);
            Assert.Equal(handles.Count, handles.Distinct().Count());
            Assert.True(Hex(file.Header["$HANDSEED"][0].Value) > handles.Max());

            // The model space owns what is added, the polyline its vertices, and the new ones stand before the ENDSEC of the entities.
            string owner = Records(text).Single(r => r.Contains("\n*Model_Space\n") && r.StartsWith("  0\nBLOCK_RECORD\n", StringComparison.Ordinal)).Split('\n')[3].Trim();
            Assert.Contains(new DxfTypedValue(330, owner).ToString(), circle.Groups.Select(g => g.ToString()));
            Assert.Equal(4, chain.Groups.Count(g => g.TypeCode == 330 && g.Value == chain.Handle));
            List<string> records = Records(saved);
            int end = records.FindIndex(r => r.StartsWith("  0\nVIEWPORT\n", StringComparison.Ordinal)) + 1;
            Assert.Equal(circle.Handle, HandleOf(records[end]));
            Assert.Equal("  0\nENDSEC\n", records[end + 1 + 5 + 1]);

            // Read back as written, the new layer in the table.
            DxfFile again = Opened(Saved(file));
            Assert.Equal(Bits(new GeoPoint3(1000, -500, 0)), Bits(((GeoCircle3)ByHandle(again, circle.Handle).Shape).Center));
            Assert.Single(again.LayerTable, l => l.Name == "holes");
            Assert.Equal(count + 3, again.ModelSpace.Count);
        }

        [Fact]
        public void ARemovedPolyline_LeavesNoVertexBehind_AndAGroupNoLongerListsWhatWasRemoved()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfFile file = DxfFile.Parse(text);
            DxfItem polyline = ByHandle(file, "44");
            DxfItem line = ByHandle(file, "3E");
            int count = file.ModelSpace.Count;

            Assert.True(file.ModelSpace.Remove(polyline));
            Assert.True(file.ModelSpace.Remove(line));
            Assert.False(file.ModelSpace.Remove(line));
            Assert.Equal(count - 2, file.ModelSpace.Count);
            Assert.DoesNotContain(polyline, file.ModelSpace);

            string saved = file.ToText();
            List<string> records = Records(saved);
            Assert.DoesNotContain(records, r => new[] { "44", "45", "46", "47", "48", "3E" }.Contains(HandleOf(r)));

            // The GROUP lists the INSERT alone, and the line's extension dictionary went with it; the rest is as it was.
            string group = records.Single(r => r.StartsWith("  0\nGROUP\n", StringComparison.Ordinal));
            Assert.Contains("340\n65\n", group, StringComparison.Ordinal);
            Assert.DoesNotContain("340\n3E\n", group, StringComparison.Ordinal);
            Assert.DoesNotContain(records, r => HandleOf(r) == "92");
            Differ(text, saved, out List<string> gone, out List<string> come);
            Assert.Equal(new[] { "3E", "44", "45", "46", "47", "48", "90", "92" }, gone.Select(HandleOf).OrderBy(h => h, StringComparer.Ordinal));
            Assert.Equal("90", HandleOf(Assert.Single(come)));

            // The removed is read still, not changed, and its handle is not given again.
            Assert.Equal("LINE", line.Type);
            Assert.Throws<InvalidOperationException>(() => line.Colour = 3);
            DxfItem added = file.ModelSpace.Add(new GeoPoint3(1, 2, 3));
            Assert.True(Hex(added.Handle) > Handles(text).Max());
        }

        [Fact]
        public void ANamedGroupLeftWithNoMember_IsKeptWithItsEntryInTheDictionary_AndAnImageGoesWithTheReactorItOwned()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);

            DxfModel.ByHandle<DxfEntity>(db, "3E").Erase();
            DxfModel.ByHandle<DxfEntity>(db, "65").Erase();
            DxfModel.ByHandle<DxfEntity>(db, "8E").Erase();

            // The group PAIR (90) is kept with no member, as AutoCAD 2021 keeps a named group emptied.
            List<string> records = Records(db.ToText());
            Assert.DoesNotContain(records, r => new[] { "8F", "65", "66", "67" }.Contains(HandleOf(r)));
            Assert.DoesNotContain("\n340\n", records.Single(r => HandleOf(r) == "90"), StringComparison.Ordinal);
            Assert.Contains("350\n90\n", records.Single(r => HandleOf(r) == "C"), StringComparison.Ordinal);
            Assert.DoesNotContain("330\n8F\n", records.Single(r => HandleOf(r) == "8D"), StringComparison.Ordinal);
            Assert.Contains("330\n8C\n", records.Single(r => HandleOf(r) == "8D"), StringComparison.Ordinal);
        }

        [Fact]
        public void AFileWrittenWithNoHandles_GetsNoneForWhatIsAdded_SoThatItsReaderCanGiveItsOwn()
        {
            // A file of R2000 with no handle anywhere, as many writers write one: a handle 1 for the new entity is the one
            // ezdxf gives the first LINE, and it then throws the new one out.
            DxfFile file = DxfFile.Parse(R2000(G(0, "LINE", 8, "0", 10, "0", 20, "0", 30, "0", 11, "1", 21, "1", 31, "0")));

            DxfItem circle = file.ModelSpace.Add(new GeoCircle2(new GeoPoint2(5, 5), 2));

            Assert.Null(circle.Handle);
            Assert.DoesNotContain(circle.Groups, g => g.TypeCode == 5 || g.TypeCode == 330);
            Assert.StartsWith("  0\nCIRCLE\n100\nAcDbEntity\n  8\n0\n100\nAcDbCircle\n", Records(file.ToText()).Single(r => r.StartsWith("  0\nCIRCLE\n", StringComparison.Ordinal)), StringComparison.Ordinal);
        }

        [Fact]
        public void AnInsertGivenAnotherLayer_PlacesTheEntitiesOfLayer0OnIt_ItsShapeReadOnceMore()
        {
            DxfFile file = DxfFile.Parse(DxfFileEzdxfDrawings.R2000);
            DxfItem insert = ByHandle(file, "65");
            var before = (DxfInsert)insert.Shape;
            int onSteel = before.GetPlacedEntities().Count(e => e.Layer == "steel");

            insert.Layer = "rebar";

            Assert.NotSame(before, insert.Shape);
            Assert.Equal(onSteel, ((DxfInsert)insert.Shape).GetPlacedEntities().Count(e => e.Layer == "rebar"));
            Assert.Equal(onSteel, before.GetPlacedEntities().Count(e => e.Layer == "rebar"));
            Assert.True(onSteel > 0);
        }

        [Fact]
        public void TheSpace_IsWalkedAsItStoodWhenTheWalkBegan_WhatIsAppendedOnTheWayNotReached()
        {
            DxfDatabase db = DxfDatabase.Parse(Hand);
            int before = db.ModelSpace.Count;
            int walked = 0;

            foreach (DxfEntity entity in db.ModelSpace)
            {
                db.ModelSpace.AppendEntity(new DxfLine());
                walked++;
            }

            Assert.Equal(before, walked);
            Assert.Equal(2 * before, db.ModelSpace.Count);
        }

        #endregion

        #region The file's own style

        [Fact]
        public void WhatIsWritten_FollowsTheFilesLineEndsPaddingAndCodePage()
        {
            // A file of R2000 in the page 1252 with CRLF and padded codes: the layer café as the byte 0xE9.
            byte[] crlf = Encoding.ASCII.GetBytes(Hand.Replace("AC1015\n", "AC1015\n  9\n$DWGCODEPAGE\n  3\nANSI_1252\n").Replace("\n", "\r\n"));
            DxfFile page = Opened(crlf);
            page.ModelSpace.Add(new GeoPoint3(1, 2, 3), "café");
            byte[] saved = Saved(page);
            string latin = Encoding.GetEncoding("iso-8859-1").GetString(saved);
            Assert.Contains("\r\n  8\r\ncafé\r\n", latin, StringComparison.Ordinal);
            Assert.Equal(0, latin.Replace("\r\n", string.Empty).Count(c => c == '\n' || c == '\r'));
            Assert.Equal("café", Opened(saved).LayerTable.Last().Name);

            // The same in the page 932, which has no e acute: its escape.
            DxfFile japanese = Opened(Encoding.ASCII.GetBytes(Hand.Replace("AC1015\n", "AC1015\n  9\n$DWGCODEPAGE\n  3\nANSI_932\n")));
            DxfItem point = japanese.ModelSpace.Add(new GeoPoint3(1, 2, 3), "café");
            Assert.Equal("caf\\U+00E9", point.Layer);
            Assert.Equal("café", japanese.LayerTable.Last().Name);

            // R2018 in UTF-8, the codes not padded, lines ended by LF.
            string plain = Hand.Replace("AC1015", "AC1032").Replace("  0\n", "0\n").Replace("  2\n", "2\n").Replace("  5\n", "5\n").Replace("  8\n", "8\n").Replace(" 10\n", "10\n");
            DxfFile utf8 = DxfFile.Parse(plain);
            utf8.ModelSpace.Add(new GeoPoint3(1, 2, 3), "café");
            byte[] written = Saved(utf8);
            string after = Encoding.UTF8.GetString(written);
            Assert.Contains("\n0\nPOINT\n5\n", after, StringComparison.Ordinal);
            Assert.Contains("\n8\ncafé\n", after, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", after, StringComparison.Ordinal);
        }

        [Fact]
        public void TheExtents_GrowToHoldWhatIsAddedOrMoved_AndAreNotShrunk()
        {
            DxfFile file = DxfFile.Parse(Hand);

            file.ModelSpace.Add(new GeoCircle2(new GeoPoint2(200, 10), 5));
            file.ModelSpace.Remove(ByHandle(file, "30"));

            Assert.Equal(new[] { "0", "0", "0" }, file.Header["$EXTMIN"].Select(g => g.Value));
            Assert.Equal(new[] { "205", "50", "0" }, file.Header["$EXTMAX"].Select(g => g.Value));

            // ezdxf's extents of a drawing it has not measured, 1E+20 and -1E+20, are empty: they become the circle's box.
            DxfFile ezdxf = DxfFile.Parse(DxfFileEzdxfDrawings.R2000);
            ezdxf.ModelSpace.Add(new GeoCircle2(new GeoPoint2(200, 10), 5));
            Assert.Equal(new[] { "195", "5", "0" }, ezdxf.Header["$EXTMIN"].Select(g => g.Value));
            Assert.Equal(new[] { "205", "15", "0" }, ezdxf.Header["$EXTMAX"].Select(g => g.Value));
        }

        #endregion
    }
}
