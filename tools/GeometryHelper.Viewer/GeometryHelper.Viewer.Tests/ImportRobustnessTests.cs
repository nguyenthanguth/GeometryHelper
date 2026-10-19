using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// What the readers do with files that are cut short, damaged, or hostile: they read what is there or refuse the file with a <see cref="FormatException"/>,
    /// and do nothing else (no other exception, no endless loop, no stack overflow).
    /// </summary>
    public class ImportRobustnessTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static Dictionary<string, string> Samples()
        {
            GeoSolid3 box = SampleShapes.Box(30.0, 20.0, 10.0, true);
            var face = (GeoFace2)SampleShapes.Make("GeoFace2", 3);
            var loop = new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100) }, new[] { 0.0, 0.5, 0.0, 0.3 });
            var chain = new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(200, 40) }, new[] { 0.4, -0.2, 0.0 });

            return new Dictionary<string, string>
            {
                ["obj"] = new ObjWriter().Add(box, Tol, "box").Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 2, 3)), "line").Add(new GeoPoint3(1, 1, 1), "dot").ToString(),
                ["dxf"] = new DxfWriter().Add(box, Tol, "a").Add(face, "b").Add(loop, "c").Add(chain, "c").Add(new GeoCircle2(new GeoPoint2(1, 2), 3), "d")
                    .Add(new GeoArc2(new GeoPoint2(0, 0), 10, 0.5, 2.0), "d").Add(new GeoEllipse2(new GeoPoint2(0, 0), new GeoVector2(1, 0), 20, 10), "d")
                    .Add((GeoPolyline3)SampleShapes.Make("GeoPolyline3", 1), "e").ToString(),
                ["wkt"] = string.Join("\n", Wkt.Write(box), Wkt.Write(face), Wkt.Write(new GeoPoint3(1, 2, 3)), Wkt.Write(new[] { new GeoPoint2(1, 2), new GeoPoint2(3, 4) })),
                ["svg"] = new SvgWriter().Add(face, "black", "#cccccc").Add(loop).Add(chain).Add(new GeoCircle2(new GeoPoint2(1, 2), 3)).Add(new GeoEllipse2(new GeoPoint2(0, 0), new GeoVector2(1, 0), 20, 10))
                    .Text(new GeoPoint2(1, 1), "x").ToString(),
            };
        }

        /// <summary>
        /// Reads a text, and says what went wrong where it was anything but the refusal a reader gives.
        /// </summary>
        private static string Try(string text, string format)
        {
            try
            {
                SceneImport.ReadText(text, format);
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
            catch (Exception e)
            {
                return format + ": " + e.GetType().Name + ": " + e.Message + " on '" + (text.Length > 120 ? text.Substring(0, 120) : text).Replace("\n", "\\n") + "'";
            }
        }

        private static void Within(Func<string> work, int seconds = 180)
        {
            Task<string> task = Task.Run(work);
            Assert.True(task.Wait(TimeSpan.FromSeconds(seconds)), "the readers did not finish: an endless loop");
            Assert.Null(task.Result);
        }

        [Fact]
        public void EveryFileCutShort_IsReadAsFarAsItGoes_OrRefused()
        {
            Within(() =>
            {
                foreach (KeyValuePair<string, string> sample in Samples())
                {
                    string text = sample.Value;
                    int step = Math.Max(1, text.Length / 400);

                    for (int cut = 0; cut < text.Length; cut += step)
                    {
                        string said = Try(text.Substring(0, cut), sample.Key);

                        if (said != null)
                        {
                            return said;
                        }
                    }
                }

                return null;
            });
        }

        [Fact]
        public void EveryFileWithACharacterOrALineChanged_IsReadOrRefused()
        {
            Within(() =>
            {
                var random = new Random(20261019);
                string alphabet = "0123456789.-+eE()[]<>/\"' \n\t,;ABCXYZ#%\\é⌀";

                foreach (KeyValuePair<string, string> sample in Samples())
                {
                    for (int round = 0; round < 600; round++)
                    {
                        var text = new StringBuilder(sample.Value);

                        for (int k = 0; k < 1 + (round % 4); k++)
                        {
                            int at = random.Next(text.Length);

                            switch (random.Next(4))
                            {
                                case 0:
                                    text[at] = alphabet[random.Next(alphabet.Length)];
                                    break;

                                case 1:
                                    text.Remove(at, Math.Min(random.Next(1, 30), text.Length - at));
                                    break;

                                case 2:
                                    text.Insert(at, alphabet[random.Next(alphabet.Length)]);
                                    break;

                                default:
                                {
                                    // A line repeated, which a table or a ring does not expect.
                                    int start = text.ToString().LastIndexOf('\n', at);
                                    int end = text.ToString().IndexOf('\n', at);

                                    if (start >= 0 && end > start)
                                    {
                                        text.Insert(start, text.ToString(start, end - start));
                                    }

                                    break;
                                }
                            }
                        }

                        string said = Try(text.ToString(), sample.Key);

                        if (said != null)
                        {
                            return said;
                        }
                    }
                }

                return null;
            });
        }

        [Fact]
        public void HostileNumbers_AreRefusedOrKept_NeverCrashAReader()
        {
            string[] numbers = { "1E999", "-1E999", "1E300", "-1E300", "NaN", "Infinity", "2147483648", "-2147483648", "99999999999999999999", "1e", ".", "-", "0x10", "1,5", "" };
            var failures = new List<string>();

            foreach (string n in numbers)
            {
                failures.Add(Try("v " + n + " 0 0\nf 1 " + n + " 3\np " + n + "\n", "obj"));
                failures.Add(Try("POINT (" + n + " 1)\nPOLYGON ((0 0, 1 " + n + ", 2 2, 0 0))", "wkt"));
                failures.Add(Try("0\nSECTION\n2\nENTITIES\n0\nLWPOLYLINE\n10\n" + n + "\n20\n1\n42\n" + n + "\n10\n3\n20\n4\n0\nCIRCLE\n10\n" + n + "\n20\n1\n40\n" + n + "\n0\nPOLYLINE\n70\n64\n71\n" + n + "\n72\n" + n
                    + "\n0\nVERTEX\n70\n128\n71\n" + n + "\n0\nVERTEX\n70\n16\n71\n" + n + "\n72\n" + n + "\n0\nARC\n10\n1\n20\n1\n40\n1\n50\n" + n + "\n51\n" + n + "\n210\n" + n + "\n220\n0\n230\n1\n0\nLAYER\n62\n" + n + "\n420\n" + n + "\n0\nENDSEC\n0\nEOF\n", "dxf"));
                failures.Add(Try("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M " + n + " 0 A " + n + " " + n + " 0 1 1 5 5 L " + n + " 1 Z\" stroke=\"rgb(" + n + ",1,2)\"/><circle cx=\"" + n + "\" cy=\"1\" r=\"2\" stroke=\"red\"/></svg>", "svg"));
            }

            Assert.DoesNotContain(failures, f => f != null);
        }

        [Fact]
        public void CollectionsNestedAbsurdlyDeep_AreRefused_NotAStackOverflow()
        {
            var text = new StringBuilder();

            for (int i = 0; i < 20000; i++)
            {
                text.Append("GEOMETRYCOLLECTION (");
            }

            Assert.Throws<FormatException>(() => SceneImport.ReadText(text.ToString(), "wkt"));

            var svg = new StringBuilder("<svg xmlns=\"http://www.w3.org/2000/svg\">");

            for (int i = 0; i < 20000; i++)
            {
                svg.Append("<g>");
            }

            Assert.Null(Try(svg.ToString(), "svg"));
        }

        [Fact]
        public void AHugeDeclaredMesh_WithFewVertices_IsRefusedAtOnce()
        {
            string text = "0\nSECTION\n2\nENTITIES\n0\nPOLYLINE\n70\n16\n71\n100000\n72\n100000\n0\nVERTEX\n10\n0\n20\n0\n0\nSEQEND\n0\nENDSEC\n0\nEOF\n";
            Scene scene = SceneImport.ReadText(text, "dxf");

            Assert.Empty(scene.Items);
            Assert.Contains(scene.LoadWarnings, w => w.Contains("a polygon mesh of 100000 by 100000"));
        }
    }
}
