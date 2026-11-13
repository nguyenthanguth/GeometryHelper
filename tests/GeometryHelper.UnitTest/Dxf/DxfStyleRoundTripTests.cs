using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.UnitTest.Dwg;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The round trip AutoCAD 2021 keeps of a visual style when it saves a version that has no place for all of it: the 30
    /// properties of R2013 (<c>RTVSPost2010Prop28</c> to <c>57</c>) in an xrecord of the style's extension dictionary from R2010
    /// down, and from R2007 down, whose style says nothing of how a property is set, the flags of all 63 properties
    /// (<c>RTVSPropertyOp0</c> to <c>62</c>). The answers are AutoCAD's own files of one drawing in each version (matvis_AC10xx,
    /// acis_AC10xx): the handles differ, the groups of the xrecord must not.
    /// </summary>
    public class DxfStyleRoundTripTests
    {
        private static byte[] Saved(byte[] file, string version)
        {
            DxfDatabase db = Opened(file);

            using (var stream = new MemoryStream())
            {
                db.Save(stream, version);
                return stream.ToArray();
            }
        }

        /// <summary>The groups of the xrecord each style keeps under its extension dictionary, by the name of the style, but for the handles; empty for a style with none.</summary>
        private static Dictionary<string, string> RoundTrips(byte[] dxf)
        {
            List<DwgAnswerKey.Record> all = DwgAnswerKey.Parse(dxf);
            var byHandle = new Dictionary<string, DwgAnswerKey.Record>(StringComparer.OrdinalIgnoreCase);

            foreach (DwgAnswerKey.Record record in all.Where(r => r.Section == "OBJECTS"))
            {
                KeyValuePair<int, string> handle = record.Groups.FirstOrDefault(g => g.Key == 5);

                if (handle.Value != null)
                {
                    byHandle[handle.Value.Trim()] = record;
                }
            }

            var trips = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (DwgAnswerKey.Record style in all.Where(r => r.Section == "OBJECTS" && r.Type == "VISUALSTYLE"))
            {
                string name = style.Groups.First(g => g.Key == 2).Value;
                int at = style.Groups.FindIndex(g => g.Key == 102 && g.Value == "{ACAD_XDICTIONARY");
                string text = string.Empty;

                if (at >= 0)
                {
                    DwgAnswerKey.Record dictionary = byHandle[style.Groups[at + 1].Value.Trim()];
                    DwgAnswerKey.Record xrecord = byHandle[dictionary.Groups.First(g => g.Key == 360).Value.Trim()];
                    int own = xrecord.Groups.FindIndex(g => g.Key == 100 && g.Value == "AcDbXrecord");
                    text = string.Join(" ", xrecord.Groups.Skip(own).Select(g => g.Key + "=" + g.Value.Trim()));

                    // The dictionary names the style as its owner, the xrecord the dictionary.
                    Assert.Equal(HandleOf(style), dictionary.Groups.First(g => g.Key == 330).Value.Trim());
                    Assert.Equal(HandleOf(dictionary), xrecord.Groups.First(g => g.Key == 330).Value.Trim());
                }

                trips[name] = text;
            }

            return trips;
        }

        private static string HandleOf(DwgAnswerKey.Record record) => record.Groups.First(g => g.Key == 5).Value.Trim();

        [Theory]
        [InlineData("acis_AC1027", "AC1024", "acis_AC1024")]
        [InlineData("acis_AC1032", "AC1024", "acis_AC1024")]
        [InlineData("matvis_AC1027", "AC1024", "matvis_AC1024")]
        [InlineData("matvis_AC1027", "AC1021", "matvis_AC1021")]
        [InlineData("matvis_AC1032", "AC1018", "matvis_AC1018")]
        [InlineData("matvis_AC1024", "AC1021", "matvis_AC1021")]
        [InlineData("matvis_AC1024", "AC1018", "matvis_AC1018")]
        [InlineData("matvis_AC1021", "AC1024", "matvis_AC1024")]
        public void TheRoundTripOfEachStyle_GoingToAnotherVersion_IsTheOneAutoCadWritesThere(
            string from,
            string version,
            string answer
        )
        {
            Dictionary<string, string> key = RoundTrips(DwgAnswerKey.Key(answer));
            Dictionary<string, string> saved = RoundTrips(Saved(DwgAnswerKey.Key(from), version));

            Assert.Equal(
                key.Keys.OrderBy(k => k, StringComparer.Ordinal),
                saved.Keys.OrderBy(k => k, StringComparer.Ordinal)
            );

            foreach (string name in key.Keys)
            {
                Assert.Equal(key[name], saved[name]);
            }
        }

        [Fact]
        public void TheStylesOfR2013_GoingDownAndUpAgain_AreTheStylesTheyWere()
        {
            byte[] r2013 = DwgAnswerKey.Key("matvis_AC1027");

            foreach (string version in new[] { "AC1024", "AC1021", "AC1018" })
            {
                byte[] again = Saved(Saved(r2013, version), "AC1027");

                Assert.Equal(StyleTexts(r2013), StyleTexts(again));
            }
        }

        private static List<string> StyleTexts(byte[] dxf) =>
            DwgAnswerKey
                .Parse(dxf)
                .Where(r => r.Section == "OBJECTS" && r.Type == "VISUALSTYLE")
                .Select(r =>
                {
                    int at = r.Groups.FindIndex(g => g.Key == 100 && g.Value == "AcDbVisualStyle");
                    int stop = r.Groups.FindIndex(at, g => g.Key == 1001);
                    return string.Join(
                        " ",
                        r.Groups.Skip(at + 1)
                            .Take((stop < 0 ? r.Groups.Count : stop) - at - 1)
                            .Select(g => g.Key + "=" + g.Value.Trim())
                    );
                })
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

        [Fact]
        public void TheNewObjectsOfTheRoundTrip_TakeTheirHandlesFromTheSeedOfTheDrawing()
        {
            byte[] saved = Saved(DwgAnswerKey.Key("matvis_AC1027"), "AC1024");
            List<DwgAnswerKey.Record> all = DwgAnswerKey.Parse(saved);
            string seed = all.First(r => r.Type == "$HANDSEED").Groups.First(g => g.Key == 5).Value.Trim();
            long next = Convert.ToInt64(seed, 16);
            var handles = all.Where(r => r.Section == "OBJECTS" && r.Groups.Any(g => g.Key == 5))
                .Select(r => Convert.ToInt64(HandleOf(r), 16))
                .ToList();

            Assert.Equal(handles.Count, handles.Distinct().Count());
            Assert.All(handles, h => Assert.True(h < next, "a handle at or past the seed"));
        }
    }
}
