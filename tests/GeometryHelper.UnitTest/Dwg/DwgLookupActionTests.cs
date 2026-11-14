using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The lookup action of a dynamic block (BLOCKLOOKUPACTION) in a DWG of AutoCAD 2000 or 2004, laid out by hand in code: the
    /// values of its table are texts of the data before R2007, which has no string stream. The reader looked for them in a
    /// string stream, refused the object as holding more values than it had room for and left it out, and with it the lookup
    /// parameter, the linear parameter, the grips, the stretch and the graph that lean on it (21 of the 391 objects of
    /// AutoCAD's Architectural - Metric when it saved the drawing as R2000 or R2004).
    /// </summary>
    public class DwgLookupActionTests
    {
        private const ulong Handle = 0x20;

        /// <summary>
        /// The object from its size to its check sum, as a file of R2000 or R2004 holds it: the type, the bit position of the
        /// handles, the data (the handle, no extended data, no reactors, a bit for no extension dictionary from R2004) and the
        /// handles (the owner, the entities the action moves).
        /// </summary>
        private static byte[] LookupAction(bool r2004, string[] values, int columns)
        {
            var data = new DwgBitWriter();
            var handles = new DwgBitWriter();
            data.Handle(5, Handle);
            data.BitShort(0);
            data.BitLong(0);

            if (r2004)
            {
                data.Bit(true);
            }

            handles.Handle(4, 0x1F);

            if (!r2004)
            {
                // Before R2004 the extension dictionary is always named: none.
                handles.Handle(3, 0);
            }

            // The block element: an expression with no value (-1, 33, 174, kind 0, id 6), its name and versions and a number.
            data.BitLong(-1);
            data.BitLong(33);
            data.BitLong(174);
            data.BitShort(0);
            data.BitLong(6);
            data.Text(Encoding.UTF8.GetBytes("Size Lookup"));
            data.BitLong(33);
            data.BitLong(174);
            data.BitLong(0);

            // The action: the label's place, no entities, one parameter (id 3).
            data.Point3(new[] { -50.8, 50.8, 0.0 });
            data.BitLong(0);
            data.BitLong(1);
            data.BitLong(3);

            // The table: two values in a row of one column or one in each of two rows, then each column's data.
            data.BitLong(values.Length / columns);
            data.BitLong(columns);

            foreach (string value in values)
            {
                data.Text(Encoding.UTF8.GetBytes(value));
            }

            for (int i = 0; i < columns; i++)
            {
                data.BitLong(0);
                data.BitLong(0);
                data.BitLong(0);
                data.Bit(false);
                data.Text(new byte[0]);
                data.Bit(false);
                data.Text(new byte[0]);
            }

            data.Bit(false);

            var type = new DwgBitWriter();
            type.BitShort(500);
            long handlesAt = type.Position + 32 + data.Position;
            var body = new DwgBitWriter();
            body.BitShort(500);
            body.RawLong((int)handlesAt);
            body.Append(data);
            body.Append(handles);
            body.PadToByte(true);
            byte[] bytes = body.ToArray();

            var head = new DwgBitWriter();
            head.ModularShort(bytes.Length);
            head.Bytes(bytes);
            byte[] withHead = head.ToArray();
            int crc = DwgCrc.Of(withHead, 0, withHead.Length, DwgCrc.Seed);
            return withHead.Concat(new[] { (byte)crc, (byte)(crc >> 8) }).ToArray();
        }

        private static DwgObject Read(byte[] bytes, string version)
        {
            var lookup = new DwgClass
            {
                Number = 500,
                Application = Encoding.ASCII.GetBytes("ObjectDBX Classes"),
                CppName = Encoding.ASCII.GetBytes("AcDbBlockLookupAction"),
                DxfName = Encoding.ASCII.GetBytes("BLOCKLOOKUPACTION"),
            };
            return new DwgObjectReader(bytes, new List<DwgClass> { lookup }, version).Read(Handle, 0);
        }

        private static string[] Values(DwgObject action, int code) =>
            action
                .Body.Where(g => g.Code == code)
                .Select(g =>
                    g.Value is byte[] text
                        ? Encoding.UTF8.GetString(text)
                        : Convert.ToString(g.Value, CultureInfo.InvariantCulture)
                )
                .ToArray();

        [Theory]
        [InlineData("AC1015", false)]
        [InlineData("AC1018", true)]
        public void ALookupTableOfTwoRowsAndAColumn_InAFileBeforeR2007_IsReadWithItsValues(string version, bool r2004)
        {
            DwgObject action = Read(LookupAction(r2004, new[] { "Small", "Large" }, 1), version);

            // The DXF AutoCAD gives: the rows (92), the columns (93) and each value as a group 302, row by row.
            Assert.True(action.Decoded);
            Assert.Equal(new[] { "2" }, Values(action, 92));
            Assert.Equal(new[] { "1" }, Values(action, 93));
            Assert.Equal(new[] { "Small", "Large" }, Values(action, 302));
            Assert.Equal(new[] { "Size Lookup" }, Values(action, 300));
        }

        [Fact]
        public void ALookupTableOfOneRowAndTwoColumns_InAFileOfR2004_IsReadWithItsValues()
        {
            DwgObject action = Read(LookupAction(true, new[] { "50 x 100", "50 x 150" }, 2), "AC1018");

            Assert.Equal(new[] { "1" }, Values(action, 92));
            Assert.Equal(new[] { "2" }, Values(action, 93));
            Assert.Equal(new[] { "50 x 100", "50 x 150" }, Values(action, 302));
        }
    }
}
