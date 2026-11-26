using System;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// The auxiliary header of the files of R2000, R2004, R2010 and R2013, against the one AutoCAD 2021 wrote for the same
    /// drawing. AutoCAD 2021 names itself, 33 and 174, before and after the version of the file (23 and 15 in R2000, 25
    /// and 104 in R2004, 29 and 226 in R2010, 31 and 125 in R2013); the writer named the file's version in all three
    /// places. The handseed is the one number that differs, as the drawing it counts is written again.
    /// </summary>
    public class DwgAuxiliaryHeaderTests
    {
        /// <summary>The bytes of the handseed in the auxiliary header of the files before R2018 (a long, its place measured in AutoCAD's files).</summary>
        private const int HandseedAt = 79;

        private static byte[] AuxiliaryOf(byte[] file)
        {
            if (DwgContainer.VersionOf(file) == "AC1015")
            {
                // The sixth locator of the file header: the section's number, its place and its size.
                int at = BitConverter.ToInt32(file, 0x19 + (5 * 9) + 1);
                int size = BitConverter.ToInt32(file, 0x19 + (5 * 9) + 5);
                return file.Skip(at).Take(size).ToArray();
            }

            return DwgContainer.SectionsOf(file, out _).Single(s => s.Key == "AcDb:AuxHeader").Value;
        }

        private static byte[] OursOf(string answer, string version)
        {
            string text = Encoding.UTF8.GetString(DwgAnswerKey.Key(answer));
            return DwgFileWriter.Write(DwgDrawing.Parse(text, null), version, out _);
        }

        [Theory]
        [InlineData("hatch_AC1015", "AC1015", 23, 15)]
        [InlineData("curves_AC1018", "AC1018", 25, 104)]
        [InlineData("curves_AC1024", "AC1024", 29, 226)]
        [InlineData("curves_AC1027", "AC1027", 31, 125)]
        public void TheAuxiliaryHeader_NamesAutoCad33And174AroundTheVersionOfTheFile_AsAutoCadDoes(
            string answer,
            string version,
            int fileVersion,
            int fileMaintenance
        )
        {
            byte[] was = AuxiliaryOf(DwgAnswerKey.Dwg(answer));
            byte[] now = AuxiliaryOf(OursOf(answer, version));

            // The shorts at 3 and 5 (the application), 23 and 25 (the file), 27 and 29 (the application again), as in AutoCAD's own files.
            Assert.Equal(new[] { 33, 174, fileVersion, fileMaintenance, 33, 174 }, Shorts(was, 3, 5, 23, 25, 27, 29));
            Assert.Equal(was.Length, now.Length);
            Assert.Equal(Shorts(was, 3, 5, 23, 25, 27, 29), Shorts(now, 3, 5, 23, 25, 27, 29));
        }

        [Theory]
        [InlineData("hatch_AC1015", "AC1015")]
        [InlineData("curves_AC1018", "AC1018")]
        [InlineData("curves_AC1024", "AC1024")]
        [InlineData("curves_AC1027", "AC1027")]
        [InlineData("curves_AC1032", "AC1032")]
        public void TheAuxiliaryHeader_IsAutoCadsByteForByte_ButTheHandseed(string answer, string version)
        {
            byte[] was = AuxiliaryOf(DwgAnswerKey.Dwg(answer));
            byte[] now = AuxiliaryOf(OursOf(answer, version));
            int seedAt = version == "AC1032" ? HandseedAt + 6 : HandseedAt;

            Assert.Equal(was.Length, now.Length);

            for (int i = 0; i < was.Length; i++)
            {
                if (i < seedAt || i >= seedAt + 4)
                {
                    Assert.True(was[i] == now[i], "byte " + i + " of the auxiliary header of " + version);
                }
            }
        }

        private static int[] Shorts(byte[] data, params int[] at) =>
            at.Select(i => data[i] | (data[i + 1] << 8)).ToArray();
    }
}
