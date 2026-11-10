using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeometryHelper.Dwg;
using GeometryHelper.Enums;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dwg
{
    /// <summary>
    /// Broken files: cut short, of another version, not a DWG at all, with a byte changed. Each is refused with a
    /// <see cref="FormatException"/> or a <see cref="NotSupportedException"/>, or read without the broken object; none
    /// hangs and none throws anything else.
    /// </summary>
    public class DwgReaderRobustnessTests : IDisposable
    {
        private readonly List<(GeometryHelperLogLevel Level, string Message)> _written = new List<(GeometryHelperLogLevel Level, string Message)>();

        public DwgReaderRobustnessTests()
        {
            GeometryHelperLog.Enable = true;
            GeometryHelperLog.Writer = (level, message, exception) => _written.Add((level, message));
        }

        public void Dispose()
        {
            GeometryHelperLog.Writer = null;
            GeometryHelperLog.Enable = true;
        }

        private static DxfDatabase Open(byte[] file) => DwgReader.Open(new MemoryStream(file, false));

        /// <summary>
        /// Reads <paramref name="file"/> on another thread and waits for it: true where it was read, false where it was
        /// refused as a broken or unsupported file. Anything else, and a read that does not end, fails the test.
        /// </summary>
        private static bool ReadsOrRefuses(byte[] file, string what)
        {
            Task<bool> read = Task.Run(() =>
            {
                try
                {
                    DwgReader.Read(new MemoryStream(file, false));
                    return true;
                }
                catch (FormatException)
                {
                    return false;
                }
                catch (NotSupportedException)
                {
                    return false;
                }
            });

            bool ended;

            try
            {
                ended = read.Wait(TimeSpan.FromSeconds(60));
            }
            catch (AggregateException fault)
            {
                throw new Xunit.Sdk.XunitException(what + " threw " + fault.InnerException);
            }

            Assert.True(ended, what + " was still being read after 60 seconds.");
            return read.Result;
        }

        /// <summary>The place and the size of the object at <paramref name="offset"/>: its data start two bytes on, and its check sum follows them.</summary>
        private static int SizeOfObject(byte[] file, long offset)
        {
            // The size is a modular short; every object of the test drawings is shorter than 32 768 bytes, so it is one short.
            Assert.Equal(0, file[offset + 1] & 0x80);
            return file[offset] | (file[offset + 1] << 8);
        }

        private static void RepairCheckSum(byte[] file, long offset)
        {
            int size = SizeOfObject(file, offset);
            int crc = DwgCrc.Of(file, offset, size + 2, DwgCrc.Seed);
            file[offset + 2 + size] = (byte)crc;
            file[offset + 3 + size] = (byte)(crc >> 8);
        }

        [Fact]
        public void TheCheckSumOfEveryObject_IsTheOneTheTestRepairs()
        {
            byte[] file = DwgAnswerKey.Dwg("curves");
            byte[] same = (byte[])file.Clone();

            foreach (KeyValuePair<ulong, long> entry in DwgContainer.Read(file).ObjectMap)
            {
                RepairCheckSum(same, entry.Value);
            }

            Assert.Equal(file, same);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(20)]
        [InlineData(0x60)]
        [InlineData(0x200)]
        [InlineData(5000)]
        public void AFileCutShort_IsRefused(int length)
        {
            byte[] file = DwgAnswerKey.Dwg("empty").Take(length).ToArray();

            Assert.Throws<FormatException>(() => Open(file));
        }

        [Fact]
        public void AFileCutAnywhere_IsRefusedOrReadWithoutWhatIsMissing()
        {
            byte[] file = DwgAnswerKey.Dwg("curves");

            for (int length = file.Length - 1; length > 0; length -= 4999)
            {
                ReadsOrRefuses(file.Take(length).ToArray(), "The file cut to " + length + " bytes");
            }
        }

        [Theory]
        [InlineData("AC1012", "AutoCAD R13")]
        [InlineData("AC1018", "AutoCAD 2004")]
        [InlineData("AC1021", "AutoCAD 2007")]
        [InlineData("AC1024", "AutoCAD 2010")]
        [InlineData("AC1027", "AutoCAD 2013")]
        [InlineData("AC1032", "AutoCAD 2018")]
        public void ADwgOfAnotherVersion_IsNotSupported_AndTheMessageNamesIt(string version, string release)
        {
            byte[] file = DwgAnswerKey.Dwg("empty");
            Encoding.ASCII.GetBytes(version).CopyTo(file, 0);

            NotSupportedException fault = Assert.Throws<NotSupportedException>(() => Open(file));
            Assert.Contains(version, fault.Message);
            Assert.Contains(release, fault.Message);
        }

        [Fact]
        public void BytesThatAreNotADwg_AreRefused_AlsoADxf()
        {
            var noise = new byte[4096];
            new Random(7).NextBytes(noise);

            Assert.Throws<FormatException>(() => Open(noise));
            Assert.Throws<FormatException>(() => Open(new byte[4096]));
            Assert.Throws<FormatException>(() => Open(Encoding.ASCII.GetBytes("A drawing, or so it says")));

            FormatException fault = Assert.Throws<FormatException>(() => Open(DwgAnswerKey.Key("empty")));
            Assert.Contains("not a DWG file", fault.Message);
        }

        [Fact]
        public void AByteChangedInTheHeaderVariables_RefusesTheFile()
        {
            byte[] file = DwgAnswerKey.Dwg("curves");
            file[DwgContainerTests.SectionStart(file, 0) + 40] ^= 0x04;

            FormatException fault = Assert.Throws<FormatException>(() => Open(file));
            Assert.Contains("check sum", fault.Message);
        }

        [Fact]
        public void AByteChangedInOneEntity_LeavesThatOneOut_AndReadsTheOthers()
        {
            byte[] file = DwgAnswerKey.Dwg("curves");
            int before = Open(file).ModelSpace.Count();
            _written.Clear();
            file[OffsetOf(file, "CIRCLE") + 6] ^= 0x20;

            DxfDatabase database = Open(file);

            // The first of the two circles stands between the lines and the arcs in the chain of the model space, and the
            // chain is broken there: what comes after it is still read.
            Assert.Single(database.ModelSpace.OfType<DxfCircle>());
            Assert.Equal(3, database.ModelSpace.OfType<DxfLine>().Count());
            Assert.Equal(2, database.ModelSpace.OfType<DxfArc>().Count());
            Assert.Equal(before - 1, database.ModelSpace.Count());

            string warning = Assert.Single(_written, line => line.Level == GeometryHelperLogLevel.Warn && line.Message.StartsWith("DwgReader:", StringComparison.Ordinal)).Message;
            Assert.Contains("1 broken", warning);
            Assert.Contains("check sum", warning);
        }

        /// <summary>Where the first object of the DXF name <paramref name="name"/> stands in the file.</summary>
        private static long OffsetOf(byte[] file, string name)
        {
            DwgContainer container = DwgContainer.Read(file);
            var reader = new DwgObjectReader(file, DwgClass.ReadAll(container.Classes));

            foreach (KeyValuePair<ulong, long> entry in container.ObjectMap)
            {
                if (reader.Read(entry.Key, entry.Value).Name == name)
                {
                    return entry.Value;
                }
            }

            throw new InvalidOperationException("The drawing has no " + name + ".");
        }

        [Theory]
        [InlineData("curves", 1)]
        [InlineData("polys", 2)]
        [InlineData("text", 3)]
        [InlineData("blocks", 4)]
        [InlineData("props", 5)]
        [InlineData("tables", 6)]
        [InlineData("later", 7)]
        public void BytesChangedAnywhere_AreRefusedOrRead_NeverAnythingElse(string name, int seed)
        {
            byte[] file = DwgAnswerKey.Dwg(name);
            var random = new Random(seed);

            for (int round = 0; round < 40; round++)
            {
                byte[] changed = (byte[])file.Clone();
                int count = 1 + random.Next(4);
                var places = new List<int>();

                for (int i = 0; i < count; i++)
                {
                    int at = random.Next(changed.Length);
                    changed[at] ^= (byte)(1 << random.Next(8));
                    places.Add(at);
                }

                ReadsOrRefuses(changed, name + " with a bit changed at " + string.Join(", ", places));
            }
        }

        [Theory]
        [InlineData("curves", 11)]
        [InlineData("polys", 12)]
        [InlineData("text", 13)]
        [InlineData("blocks", 14)]
        [InlineData("props", 15)]
        [InlineData("tables", 16)]
        [InlineData("later", 17)]
        public void BytesChangedInsideObjects_UnderARightCheckSum_AreRefusedOrRead_NeverAnythingElse(string name, int seed)
        {
            // With its check sum repaired a changed object reaches its decoder, which has to stand whatever it is given.
            byte[] file = DwgAnswerKey.Dwg(name);
            List<KeyValuePair<ulong, long>> map = DwgContainer.Read(file).ObjectMap;
            var random = new Random(seed);

            for (int round = 0; round < 60; round++)
            {
                byte[] changed = (byte[])file.Clone();
                var places = new List<long>();

                for (int i = 1 + random.Next(3); i > 0; i--)
                {
                    long offset = map[random.Next(map.Count)].Value;
                    long at = offset + 2 + random.Next(SizeOfObject(file, offset));
                    changed[at] = random.Next(3) == 0 ? (byte)random.Next(256) : (byte)(changed[at] ^ (1 << random.Next(8)));
                    places.Add(at);
                    RepairCheckSum(changed, offset);
                }

                ReadsOrRefuses(changed, name + " with a byte changed at " + string.Join(", ", places));
            }
        }
    }
}
