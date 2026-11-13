using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The two solids of splines_AC1024 (a closed spline extruded 7 up, and a solid lofted through a circle, an ellipse and a
    /// circle) as SAT turned into the SAB of R2018, against the SAB AutoCAD 2021 writes of the same drawing saved as R2018
    /// (splines_AC1032, the DXF of that save): byte for byte, and the other way round. The drawing rewritten with the SAB we
    /// used to write was refused (a modeling error, ErrorStatus 53): the parameter curves ended in two whole numbers where
    /// AutoCAD has two reals, and the lofted surface lacked the whole number of 0 the binary form carries after its last flag.
    /// </summary>
    public class DwgSplineBodiesTests
    {
        private static List<DwgAnswerKey.Record> Solids(string key) =>
            DwgAnswerKey
                .Parse(DwgAnswerKey.Key(key))
                .Where(r => r.Section == "ENTITIES" && r.Type == "3DSOLID")
                .ToList();

        /// <summary>The lines of the SAT of an entity, as the groups 3 and 1 after its AcDbModelerGeometry hold them, deciphered.</summary>
        private static List<string> Sat(DwgAnswerKey.Record entity)
        {
            var lines = new List<string>();
            var line = new StringBuilder();
            bool inside = false;

            foreach (KeyValuePair<int, string> group in entity.Groups)
            {
                if (group.Key == 100)
                {
                    inside = group.Value == "AcDbModelerGeometry";
                }
                else if (inside && (group.Key == 3 || group.Key == 1))
                {
                    line.Append(group.Value);

                    if (group.Key == 1)
                    {
                        lines.Add(DxfUnknownEntity.Plain(line.ToString()));
                        line.Clear();
                    }
                }
            }

            return lines;
        }

        /// <summary>The SAB bytes the ACDSDATA section of a DXF holds for an entity.</summary>
        private static byte[] Sab(string key, DwgAnswerKey.Record entity)
        {
            string handle = entity.Groups.First(g => g.Key == 5).Value.Trim();
            DwgAnswerKey.Record data = DwgAnswerKey
                .Parse(DwgAnswerKey.Key(key))
                .Single(r =>
                    r.Section == "ACDSDATA"
                    && r.Type == "ACDSRECORD"
                    && r.Groups.Any(g => g.Key == 320 && g.Value.Trim() == handle)
                );
            string hex = string.Concat(data.Groups.Where(g => g.Key == 310).Select(g => g.Value.Trim()));
            int size = int.Parse(data.Groups.First(g => g.Key == 94).Value.Trim(), CultureInfo.InvariantCulture);
            var bytes = new byte[size];

            for (int i = 0; i < size; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(2 * i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        /// <summary>The date in the head of a SAB (its third text, after the 31 bytes of the signature and numbers).</summary>
        private static string DateOf(byte[] sab)
        {
            int at = 31;

            for (int i = 0; i < 2; i++)
            {
                at += 2 + sab[at + 1];
            }

            return Encoding.ASCII.GetString(sab, at + 2, sab[at + 1]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void ASolidOfSplinesAsSat_IsTurnedIntoTheSabAutoCadWritesOfIt_ByteForByte(int solid)
        {
            byte[] autocad = Sab("splines_AC1032", Solids("splines_AC1032")[solid]);
            List<string> lines = Sat(Solids("splines_AC1024")[solid]);

            byte[] ours = DwgSat.ToSab(lines, AcisFormat.For("AC1032"), DateOf(autocad));

            Assert.NotNull(ours);
            Assert.Equal(autocad, ours);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void TheSabAutoCadWritesOfASolidOfSplines_IsReadBackInto_TheSatOfR2010(int solid)
        {
            byte[] autocad = Sab("splines_AC1032", Solids("splines_AC1032")[solid]);
            List<string> lines = Sat(Solids("splines_AC1024")[solid]);

            string sat = DwgSat.ToSat(autocad, AcisFormat.For("AC1024"));

            Assert.NotNull(sat);
            List<string> back = sat.Split('\n').Take(lines.Count).ToList();

            // The head's second line names the date of the data, which AutoCAD left empty in the DXF and stamped in the SAB.
            Assert.Equal(lines.Where((l, i) => i != 1), back.Where((l, i) => i != 1));
        }

        [Fact]
        public void ASharedParameterCurveOfR2004_EndsInTwoReals_AsAutoCadsSabDoes()
        {
            // The lofted solid of R2004 holds parameter curves that point at another one (pcurve $-1 -1 $-1 -1 $39 -1 0 #),
            // which AutoCAD 2021's SAB of it ends with the pointer 39 and two reals, 0 and 0, as the tag 0x06 and eight bytes.
            List<string> lines = Sat(Solids("splines_AC1018")[1]);

            byte[] sab = DwgSat.ToSab(lines, AcisFormat.For("AC1018"), string.Empty);

            var tail = new byte[]
            {
                0x0C,
                39,
                0,
                0,
                0,
                0x06,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0x06,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0x11,
            };
            Assert.NotNull(sab);
            Assert.Contains(Enumerable.Range(0, sab.Length - tail.Length), at =>
                sab.Skip(at).Take(tail.Length).SequenceEqual(tail)
            );
        }
    }
}
