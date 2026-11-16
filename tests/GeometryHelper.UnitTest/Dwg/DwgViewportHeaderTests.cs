using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// AutoCAD 2021's own file of R2000 keeps a viewport entity header for each viewport it has numbered, in a table of its
    /// own (the control object 0xB, type 0x46, listing the headers, type 0x47), names the header in the viewport and writes
    /// the viewport's frozen layers as hard pointers (handle code 5); the writer gave the viewport no header and the layers as
    /// soft pointers (code 4). The drawing <c>vports</c> has five viewports, four of them numbered.
    /// </summary>
    public class DwgViewportHeaderTests
    {
        private static byte[] OursOf(string answer) =>
            DwgWriter.ToDwg(Encoding.UTF8.GetString(DwgReader.ToDxf(DwgAnswerKey.Dwg(answer))), "AC1015", out _);

        /// <summary>The type of each object of a file of R2000, by handle.</summary>
        private static Dictionary<ulong, int> Types(byte[] file)
        {
            DwgContainer container = DwgContainer.Read(file);
            var types = new Dictionary<ulong, int>();

            foreach (KeyValuePair<ulong, long> entry in container.ObjectMap)
            {
                var reader = new DwgBitReader(container.Objects, entry.Value * 8, container.Objects.LongLength * 8);
                reader.ModularShort();
                types[entry.Key] = reader.BitShort();
            }

            return types;
        }

        /// <summary>
        /// The handle references of an object of a file of R2000, each as its code and whether it names an object ("5:x") or
        /// none ("5:0"); a handle given by its distance from the object's own (codes 6 to 12) as the code alone.
        /// </summary>
        private static List<string> References(byte[] file, ulong handle)
        {
            DwgContainer container = DwgContainer.Read(file);
            long at = container.ObjectMap.Single(pair => pair.Key == handle).Value;
            byte[] bytes = container.Objects;
            var head = new DwgBitReader(bytes, at * 8, bytes.LongLength * 8);
            long size = head.ModularShort();
            long start = head.Position / 8;
            long end = (start + size) * 8;
            var data = new DwgBitReader(bytes, start * 8, end);
            data.BitShort();
            var references = new DwgBitReader(bytes, (start * 8) + (uint)data.RawLong(), end);
            var found = new List<string>();

            while (references.Remaining >= 8)
            {
                uint code = references.Bits(4);
                uint count = references.Bits(4);
                ulong value = 0;

                for (uint i = 0; i < count; i++)
                {
                    value = (value << 8) | references.Bits(8);
                }

                found.Add(code >= 6 ? code.ToString("X") : code + ":" + (value == 0 ? "0" : "x"));
            }

            return found;
        }

        [Fact]
        public void TheViewportsOfADrawing_HaveATableOfHeadersAndOneEachForTheNumberedOnes_AsAutoCADWritesThem()
        {
            Dictionary<ulong, int> acad = Types(DwgAnswerKey.Dwg("vports_AC1015"));
            Dictionary<ulong, int> ours = Types(OursOf("vports_AC1015"));

            // The table, and a header for each of the four numbered viewports and the empty one AutoCAD lists first.
            Assert.Equal(1, acad.Values.Count(t => t == 0x46));
            Assert.Equal(5, acad.Values.Count(t => t == 0x47));
            Assert.Equal(1, ours.Values.Count(t => t == 0x46));
            Assert.Equal(5, ours.Values.Count(t => t == 0x47));
            Assert.Equal(0x46, ours[0xB]);
        }

        [Fact]
        public void TheTableOfHeaders_ListsTheHeaders_AsAutoCADsDoes()
        {
            byte[] acad = DwgAnswerKey.Dwg("vports_AC1015");
            byte[] ours = OursOf("vports_AC1015");

            Assert.Equal(References(acad, 0xB), References(ours, 0xB));
        }

        [Theory]
        [InlineData(0x7BUL)]
        [InlineData(0x7DUL)]
        [InlineData(0x81UL)]
        [InlineData(0x83UL)]
        [InlineData(0x85UL)]
        public void AViewport_NamesItsHeader_AndItsFrozenLayersAreHardPointers_AsInAutoCADsFile(ulong viewport)
        {
            // The viewport 0x7B has a frozen layer, 0x85 is not numbered and has no header.
            Assert.Equal(
                References(DwgAnswerKey.Dwg("vports_AC1015"), viewport),
                References(OursOf("vports_AC1015"), viewport)
            );
        }

        [Fact]
        public void TheHeaders_TakeNoHandleOfARecordTheWriterLeavesOut()
        {
            // The last record of the drawing of R2018, the section 8C, is not written in R2000. A header given its handle would
            // make a dictionary entry that names the section find an object of another class (AUDIT: eNotThatKindOfClass,
            // as AutoCAD 2021 said of 13 drawings written so).
            string text = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("vports_AC1032"), false)).ToText();
            var source = new HashSet<ulong>();

            foreach (DwgAnswerKey.Record record in DwgAnswerKey.Parse(Encoding.UTF8.GetBytes(text)))
            {
                KeyValuePair<int, string> group = record.Groups.FirstOrDefault(g => g.Key == 5 || g.Key == 105);

                if (
                    group.Value != null
                    && ulong.TryParse(
                        group.Value.Trim(),
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out ulong handle
                    )
                )
                {
                    source.Add(handle);
                }
            }

            Dictionary<ulong, int> types = Types(DwgWriter.ToDwg(text, "AC1015", out _));
            List<ulong> headers = types.Where(p => p.Value == 0x47).Select(p => p.Key).ToList();

            Assert.Equal(5, headers.Count);
            Assert.DoesNotContain(headers, source.Contains);
        }

        [Fact]
        public void TheHeaderOfAViewport_IsOwnedByTheTable_AndPointsBackAtTheViewport()
        {
            byte[] acad = DwgAnswerKey.Dwg("vports_AC1015");
            byte[] ours = OursOf("vports_AC1015");
            ulong header = Types(ours).Where(p => p.Value == 0x47).Select(p => p.Key).Max();
            ulong acadHeader = Types(acad).Where(p => p.Value == 0x47).Select(p => p.Key).Max();

            Assert.Equal(References(acad, acadHeader), References(ours, header));
        }
    }
}
