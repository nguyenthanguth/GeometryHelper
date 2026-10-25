using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What the tests of the editing model (<see cref="DxfDatabase"/> and the entity classes) share: saving, bits, records of a
    /// text and their differences, entities by handle, and the checks every setter is held to.
    /// </summary>
    internal static class DxfModel
    {
        /// <summary>The bytes a drawing is saved as.</summary>
        internal static byte[] Saved(DxfDatabase db)
        {
            using (var stream = new MemoryStream())
            {
                db.Save(stream);
                return stream.ToArray();
            }
        }

        /// <summary>A drawing opened from bytes.</summary>
        internal static DxfDatabase Opened(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                return DxfDatabase.Open(stream);
            }
        }

        /// <summary>The bits of a point, so that two equal strings are two equal points.</summary>
        internal static string Bits(GeoPoint3 p) => Bits(p.X) + "," + Bits(p.Y) + "," + Bits(p.Z);

        /// <summary>The bits of a vector.</summary>
        internal static string Bits(GeoVector3 v) => Bits(v.X) + "," + Bits(v.Y) + "," + Bits(v.Z);

        /// <summary>The bits of a number.</summary>
        internal static string Bits(double d) => BitConverter.DoubleToInt64Bits(d).ToString("x", CultureInfo.InvariantCulture);

        /// <summary>The number of the bits ezdxf prints with float.hex, as <c>0x4049bfffffffffff</c> for 51.49999999999999.</summary>
        internal static double FromBits(long bits) => BitConverter.Int64BitsToDouble(bits);

        /// <summary>Every entity of a drawing: of model space, paper space and every block.</summary>
        internal static IEnumerable<DxfEntity> Everything(DxfDatabase db)
            => db.BlockTable.SelectMany(b => b);

        /// <summary>The entity of a handle, of model space, paper space or any block.</summary>
        internal static T ByHandle<T>(DxfDatabase db, string handle) where T : DxfEntity
            => Everything(db).OfType<T>().First(e => e.Handle == handle);

        /// <summary>The records of a text, each its lines from its group 0 to the next, line ends made LF.</summary>
        internal static List<string> Records(string text)
        {
            var records = new List<string>();
            var current = new StringBuilder();
            string[] lines = text.Replace("\r\n", "\n").Split('\n');

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
        internal static void Differ(string before, string after, out List<string> gone, out List<string> come)
        {
            List<string> left = Records(before);
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

        /// <summary>The handle (group 5) of a record of <see cref="Records"/>; null where it has none.</summary>
        internal static string HandleOf(string record)
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

        /// <summary>
        /// The no-op rule: every setter given the value its getter gives leaves the drawing byte for byte. The action sets
        /// each property of an entity to its own value.
        /// </summary>
        internal static void SettingWhatItHasChangesNothing<T>(string text, Action<T> setEach) where T : DxfEntity
        {
            DxfDatabase db = DxfDatabase.Parse(text);
            byte[] before = Saved(db);
            int seen = 0;

            foreach (T entity in Everything(db).OfType<T>().ToList())
            {
                setEach(entity);
                seen++;
            }

            Assert.True(seen > 0, "The drawing has no " + typeof(T).Name + ".");
            Assert.Equal(before, Saved(db));
            Assert.Equal(new UTF8Encoding(false).GetBytes(text), before);
        }

        /// <summary>
        /// The untouched rule: after a change, the records that differ between the text before and the text saved are those of
        /// the handles given, changed, added or erased, the header aside; and each of those differs.
        /// </summary>
        internal static void OnlyTheseChanged(string before, string after, params string[] handles)
        {
            Differ(before, after, out List<string> gone, out List<string> come);
            string[] goneHandles = gone.Where(r => !r.Contains("\nHEADER\n")).Select(HandleOf).OrderBy(h => h, StringComparer.Ordinal).ToArray();
            string[] comeHandles = come.Where(r => !r.Contains("\nHEADER\n")).Select(HandleOf).OrderBy(h => h, StringComparer.Ordinal).ToArray();

            Assert.All(comeHandles, h => Assert.Contains(h, handles));
            Assert.All(goneHandles, h => Assert.Contains(h, handles));
            Assert.All(handles, h => Assert.True(comeHandles.Contains(h) || goneHandles.Contains(h), "The record " + h + " did not change."));
        }
    }
}
