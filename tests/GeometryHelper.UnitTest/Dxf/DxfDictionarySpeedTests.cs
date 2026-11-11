using System.Diagnostics;
using System.Globalization;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Entries added to one dictionary one by one, as a loop that stores data under many names does. Each SetAt read and
    /// wrote every entry of the dictionary again, so that 2 000 XRECORDs took 1.5 s and 10 000 took 84 s under .NET
    /// Framework 4.8; now each costs one copy of the dictionary's record, and they take 0.02 s and 0.9 s.
    /// </summary>
    public class DxfDictionarySpeedTests
    {
        [Fact]
        public void TwoThousandRecordsAddedOneByOneToOneDictionary_TakeUnderHalfASecond()
        {
            AddedOneByOne(2000, 0.5);
        }

        [Fact]
        public void TenThousandRecordsAddedOneByOneToOneDictionary_TakeUnderFiveSeconds()
        {
            AddedOneByOne(10000, 5.0);
        }

        private static void AddedOneByOne(int count, double seconds)
        {
            var database = new DxfDatabase("AC1027");
            var dictionary = new DxfDBDictionary();
            database.NamedObjectsDictionary.SetAt("GH_PROBE", dictionary);
            var watch = Stopwatch.StartNew();

            for (int i = 0; i < count; i++)
            {
                dictionary.SetAt(Name(i), new DxfXrecord { Data = new[] { new DxfTypedValue(1, "x") } });
            }

            watch.Stop();
            Assert.True(
                watch.Elapsed.TotalSeconds < seconds,
                count.ToString(CultureInfo.InvariantCulture)
                    + " entries took "
                    + watch.Elapsed.TotalSeconds.ToString(CultureInfo.InvariantCulture)
                    + " s."
            );
            Assert.Equal(count, dictionary.Count);
            Assert.True(dictionary.Contains(Name(count - 1).ToLowerInvariant()));
            Assert.IsType<DxfXrecord>(dictionary.GetAt(Name(count / 2)));

            // The entries are read back in the order they were added.
            DxfDBDictionary read = Assert.IsType<DxfDBDictionary>(
                DxfDatabase.Parse(database.ToText()).NamedObjectsDictionary["GH_PROBE"]
            );
            int at = 0;

            foreach (DxfDBDictionaryEntry entry in read)
            {
                Assert.Equal(Name(at++), entry.Key);
            }

            Assert.Equal(count, at);
        }

        private static string Name(int i) => "E" + i.ToString("D5", CultureInfo.InvariantCulture);
    }
}
