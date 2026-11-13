using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The plot stamp parameter file, AutoCAD's .pss: a PIA file (PIAFILEVERSION_2.0, plotstampcompress) holding a zlib stream
    /// of the stamp's settings, written by Save As of the Plot Stamp dialog and read by its Load.
    /// </summary>
    public class PlotStampFileTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gh-pss-" + Guid.NewGuid().ToString("N"));

        public PlotStampFileTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException) { }
        }

        [Fact]
        public void AStampSavedAndLoaded_KeepsItsFieldsUserFieldsPlaceTextUnitsAndLogFile()
        {
            var stamp = new CadPlotStamp
            {
                LayoutName = true,
                DeviceName = false,
                PlotScale = true,
                UserFields = new[] { "Project 2611", "Checked: NT" },
                UserField1 = "Checked: NT",
                Location = CadPlotStampCorner.TopRight,
                Vertical = true,
                UpsideDown = true,
                OffsetX = 5,
                OffsetY = 7.5,
                OffsetFromPaperBorder = true,
                TextHeight = 3.5,
                SingleLine = true,
                Units = CadPlotStampUnits.Millimeters,
                WriteLog = false,
                LogFile = "stamps.log",
            };
            string path = Path.Combine(_folder, "site.pss");

            stamp.SaveParameterFile(path);
            var read = new CadPlotStamp();
            read.LoadParameterFile(path);

            Assert.True(read.DrawingName);
            Assert.True(read.LayoutName);
            Assert.False(read.DeviceName);
            Assert.True(read.PlotScale);
            Assert.Equal(new[] { "Project 2611", "Checked: NT" }, read.UserFields);
            Assert.Equal("Checked: NT", read.UserField1);
            Assert.Equal(string.Empty, read.UserField2);
            Assert.Equal(CadPlotStampCorner.TopRight, read.Location);
            Assert.True(read.Vertical);
            Assert.True(read.UpsideDown);
            Assert.Equal(5.0, read.OffsetX, 9);
            Assert.Equal(7.5, read.OffsetY, 9);
            Assert.True(read.OffsetFromPaperBorder);
            Assert.Equal(3.5, read.TextHeight, 9);
            Assert.True(read.SingleLine);
            Assert.Equal(CadPlotStampUnits.Millimeters, read.Units);
            Assert.False(read.WriteLog);
            Assert.Equal("stamps.log", read.LogFile);
        }

        [Fact]
        public void TheFileIsAPiaFileOfAZlibStream_ItsChecksumTheAdler32OfTheStream_AsAutoCadWritesIt()
        {
            string path = Path.Combine(_folder, "mm.pss");
            new CadPlotStamp { Units = CadPlotStampUnits.Millimeters }.SaveParameterFile(path);
            byte[] bytes = File.ReadAllBytes(path);
            string head = "PIAFILEVERSION_2.0,plotstampcompress\r\npmzlibcodec";

            Assert.Equal(head, Encoding.ASCII.GetString(bytes, 0, head.Length));
            uint checksum = BitConverter.ToUInt32(bytes, head.Length);
            int length = BitConverter.ToInt32(bytes, head.Length + 4);
            int packed = BitConverter.ToInt32(bytes, head.Length + 8);
            Assert.Equal(bytes.Length - head.Length - 12, packed);
            Assert.Equal(0x78, bytes[head.Length + 12]);

            var stream = new byte[packed];
            Array.Copy(bytes, head.Length + 12, stream, 0, packed);
            Assert.Equal(Adler32(stream), checksum);

            string text = Inflate(stream);
            Assert.Equal(length, Encoding.ASCII.GetByteCount(text));
            Assert.StartsWith("pstamp{\n fields{\n  drawing_name=1\n", text);
            Assert.Contains("\n pstamp_units{\n  units=1\n }\n", text);
            Assert.Contains("\n  logfile_name=\"plot.log\n", text);
        }

        [Fact]
        public void AFileThatIsNoPlotStampFile_IsRefusedSayingSo()
        {
            string path = Path.Combine(_folder, "other.pss");
            File.WriteAllText(path, "not a plot stamp");

            var stamp = new CadPlotStamp();
            InvalidDataException refused = Assert.Throws<InvalidDataException>(() => stamp.LoadParameterFile(path));

            Assert.Contains("plot stamp parameter file", refused.Message);
        }

        /// <summary>A file of AutoCAD's own form, as mm.pss: no user field, the stamp in millimetres.</summary>
        [Fact]
        public void AnAutoCadFileWithNoUserField_ReadsAsItsDefaults()
        {
            string text =
                "pstamp{\n fields{\n  drawing_name=1\n  layout_name=0\n  date_time=1\n  login_name=0\n  device_name=1\n"
                + "  papersize=0\n  plotscale=0\n }\n ud_fields{\n  comment{\n   str=\"\n   combo1{\n    selection=\"\n   }\n"
                + "   combo2{\n    selection=\"\n   }\n  }\n }\n preview{\n  location=0\n  orientation=1\n  upsidedown=0\n"
                + "  xoffset=2.54\n  yoffset=2.54\n  dimensional_xoffset=2.54\n  dimensional_yoffset=2.54\n"
                + "  non_dimensional_xoffset=10.0\n  non_dimensional_yoffset=10.0\n  relative_offset=0\n }\n text_prop{\n"
                + "  font_height=5.08\n  dimensional_font_height=5.08\n  non_dimensional_font_height=10.0\n  single_line=0\n }\n"
                + " pstamp_units{\n  units=1\n }\n logfile{\n  create_logfile=1\n  logfile_name=\"plot.log\n }\n}\n";
            string path = Path.Combine(_folder, "acad.pss");
            File.WriteAllBytes(path, Pia(text));

            var stamp = new CadPlotStamp { UserFields = new[] { "Old" }, UserField1 = "Old" };
            stamp.LoadParameterFile(path);

            Assert.Empty(stamp.UserFields);
            Assert.Equal(string.Empty, stamp.UserField1);
            Assert.Equal(CadPlotStampCorner.BottomLeft, stamp.Location);
            Assert.False(stamp.Vertical);
            Assert.Equal(2.54, stamp.OffsetX, 9);
            Assert.Equal(5.08, stamp.TextHeight, 9);
            Assert.Equal(CadPlotStampUnits.Millimeters, stamp.Units);
            Assert.True(stamp.WriteLog);
            Assert.Equal("plot.log", stamp.LogFile);
        }

        private static byte[] Pia(string text)
        {
            byte[] raw = Encoding.ASCII.GetBytes(text);
            byte[] stream;

            using (var packed = new MemoryStream())
            {
                packed.WriteByte(0x78);
                packed.WriteByte(0x9C);

                using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, true))
                {
                    deflate.Write(raw, 0, raw.Length);
                }

                uint sum = Adler32(raw);
                packed.WriteByte((byte)(sum >> 24));
                packed.WriteByte((byte)(sum >> 16));
                packed.WriteByte((byte)(sum >> 8));
                packed.WriteByte((byte)sum);
                stream = packed.ToArray();
            }

            using (var file = new MemoryStream())
            {
                byte[] head = Encoding.ASCII.GetBytes("PIAFILEVERSION_2.0,plotstampcompress\r\npmzlibcodec");
                file.Write(head, 0, head.Length);
                file.Write(BitConverter.GetBytes(Adler32(stream)), 0, 4);
                file.Write(BitConverter.GetBytes(raw.Length), 0, 4);
                file.Write(BitConverter.GetBytes(stream.Length), 0, 4);
                file.Write(stream, 0, stream.Length);
                return file.ToArray();
            }
        }

        private static string Inflate(byte[] stream)
        {
            using (var input = new MemoryStream(stream, 2, stream.Length - 6))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(deflate, Encoding.ASCII))
            {
                return reader.ReadToEnd();
            }
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1;
            uint b = 0;

            foreach (byte x in data)
            {
                a = (a + x) % 65521;
                b = (b + a) % 65521;
            }

            return (b << 16) | a;
        }
    }
}
