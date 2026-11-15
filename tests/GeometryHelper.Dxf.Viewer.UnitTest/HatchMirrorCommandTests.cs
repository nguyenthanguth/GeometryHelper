using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MIRROR, ROTATE and -OBJECTSCALE on a hatch, as AutoCAD 2021's console does them (probed, mirror-6 GHM6, 1 800 cases of
    /// hatches of ANSI31, HONEY, ANSI37, AR-B816, DASH, ANGLE, a user pattern, SOLID and a gradient, with one scale and two, written
    /// by DxfOut): MIRROR keeps the normal and reflects the loops and the seed points; with MIRRHATCH 0, its default, the lines of
    /// the pattern stay as they were, with 1 they are reflected too; the direction of the pattern of a context is reflected either
    /// way; ROTATE takes its angle from nought up to a whole turn, so that -30 is 330 and 360 is none; -OBJECTSCALE Add writes the
    /// view context of a hatch from its default context as it stands. The hatch here is a user pattern at 30 degrees with its
    /// lines 2 apart over (42, 50) to (58, 58), its line at 30 degrees through (0, 0), offset (-1, 1.732), annotative at 1:1.
    /// </summary>
    public class HatchMirrorCommandTests
    {
        private const string Collection = "ACDB_ANNOTATIONSCALE_VIEW_COLLECTION";
        private static readonly double Root3 = Math.Sqrt(3.0);

        private static DxfAnnotationScale Scale(string name, int drawing) =>
            new DxfAnnotationScale
            {
                Name = name,
                PaperUnits = 1,
                DrawingUnits = drawing,
            };

        private static CadDocument Hatched(bool annotative, out DxfHatch hatch, int pattern = 0)
        {
            CadDocument document = Blank();
            DxfObjectContextCollection scales = document.Database.ObjectContextManager.GetContextCollection(
                "ACDB_ANNOTATIONSCALES"
            );
            scales.AddContext(Scale("1:1", 1));
            scales.AddContext(Scale("1:2", 2));
            hatch = new DxfHatch();

            if (pattern == 0)
            {
                hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
                hatch.PatternAngle = 30.0 * Math.PI / 180.0;
                hatch.PatternSpace = 2.0;
            }
            else if (pattern == 1)
            {
                hatch.SetGradient(DxfGradientPatternType.PreDefinedGradient, "LINEAR");
                hatch.GradientAngle = 30.0 * Math.PI / 180.0;
            }

            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            document.Database.ModelSpace.AppendEntity(hatch);

            if (annotative)
            {
                hatch.Annotative = DxfAnnotativeStates.True;
            }

            return document;
        }

        /// <summary>Gives the hatch the scale 1:2 too, as its context.</summary>
        private static void AddSecondScale(CadDocument document, DxfHatch hatch) =>
            hatch.AddContext(
                (DxfAnnotationScale)
                    document
                        .Database.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES")
                        .GetContext("1:2")
            );

        /// <summary>The groups of the record of a handle as "code=value", from its group 0 to the next.</summary>
        private static List<string> Groups(CadDocument document, string handle)
        {
            string[] lines = document.Database.ToText().Replace("\r\n", "\n").Split('\n');
            var groups = new List<string>();
            bool found = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1].Trim();

                if (code == 0 && found)
                {
                    break;
                }

                if (code == 0)
                {
                    groups.Clear();
                }

                groups.Add(code + "=" + value);

                if (code == 5 && value == handle)
                {
                    found = true;
                }
            }

            return found ? groups : null;
        }

        private static DxfDBDictionary Manager(DxfDBObject entity) =>
            (DxfDBDictionary)entity.ExtensionDictionary.GetAt("AcDbContextDataManager");

        private static List<string> Context(CadDocument document, DxfHatch hatch, string entry) =>
            Groups(document, ((DxfDBDictionary)Manager(hatch).GetAt("ACDB_ANNOTATIONSCALES")).GetAt(entry).Handle);

        private static List<string> View(CadDocument document, DxfHatch hatch) =>
            Groups(document, ((DxfDBDictionary)Manager(hatch).GetAt(Collection)).GetAt("*A1").Handle);

        /// <summary>The first pattern line a record holds, after its 78: angle, base x, base y, offset x, offset y.</summary>
        private static double[] FirstLine(List<string> groups)
        {
            var line = new double[5];
            int[] codes = { 53, 43, 44, 45, 46 };
            int from = groups.IndexOf(groups.First(g => g.StartsWith("78=", StringComparison.Ordinal)));

            for (int i = 0; i < codes.Length; i++)
            {
                string prefix = codes[i] + "=";
                string group = groups.Skip(from).First(g => g.StartsWith(prefix, StringComparison.Ordinal));
                line[i] = double.Parse(group.Substring(prefix.Length), CultureInfo.InvariantCulture);
            }

            return line;
        }

        /// <summary>The direction of the pattern a context holds (10, 20): the pair of groups after its scale (40).</summary>
        private static double[] Direction(List<string> groups)
        {
            int scale = groups.IndexOf(groups.Last(g => g.StartsWith("40=", StringComparison.Ordinal)));
            var direction = new double[2];

            for (int i = 0; i < 2; i++)
            {
                string prefix = (10 * (i + 1)) + "=";
                direction[i] = double.Parse(
                    groups.Skip(scale).First(g => g.StartsWith(prefix, StringComparison.Ordinal)).Substring(3),
                    CultureInfo.InvariantCulture
                );
            }

            return direction;
        }

        private static double Group(List<string> groups, int code) =>
            double.Parse(
                groups.First(g => g.StartsWith(code + "=", StringComparison.Ordinal)).Substring((code + "=").Length),
                CultureInfo.InvariantCulture
            );

        private static void AssertLine(double angle, double x, double y, double ox, double oy, double[] actual)
        {
            double[] expected = { angle, x, y, ox, oy };

            for (int i = 0; i < 5; i++)
            {
                Assert.True(Math.Abs(expected[i] - actual[i]) <= 1E-9, i + ": " + expected[i] + " but " + actual[i]);
            }
        }

        private static DxfHatch Mirrored(
            CadDocument document,
            DxfHatch hatch,
            GeoPoint2 first,
            GeoPoint2 second,
            CadCommandSession session = null
        )
        {
            var before = new HashSet<string>(All<DxfHatch>(document.ModelSpace).Select(e => e.Handle));
            Run(session ?? Session(document), "MIRROR", new[] { hatch.Handle }, Enter, first, second, Enter);
            return All<DxfHatch>(document.ModelSpace).Single(e => !before.Contains(e.Handle));
        }

        [Fact]
        public void AHatchMirrored_KeepsItsNormal_AndReflectsItsLoop()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);

            DxfHatch copy = Mirrored(document, hatch, P(0, 0), P(10, 10));

            // AutoCAD 2021: the normal still (0, 0, 1), the rectangle over (50, 42) to (58, 58) where it was over (42, 50) to (58, 58).
            Assert.Equal(1.0, copy.Normal.Z);
            List<string> groups = Groups(document, copy.Handle);
            int from = groups.FindIndex(g => g.StartsWith("93=", StringComparison.Ordinal));
            Assert.Equal(
                new[] { "10=50", "20=42", "10=50", "20=58", "10=58", "20=58", "10=58", "20=42" },
                groups
                    .Skip(from)
                    .Where(g =>
                        g.StartsWith("10=", StringComparison.Ordinal) || g.StartsWith("20=", StringComparison.Ordinal)
                    )
                    .Take(8)
                    .Select(g =>
                        g.Substring(0, 3)
                        + string.Format(
                            CultureInfo.InvariantCulture,
                            "{0:0.######}",
                            double.Parse(g.Substring(3), CultureInfo.InvariantCulture)
                        )
                    )
                    .ToArray()
            );
        }

        [Fact]
        public void AHatchMirrored_KeepsThePatternWhereMirrhatchIs0_AndMirrorsItWhereMirrhatchIs1()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);

            // The default of AutoCAD 2021, MIRRHATCH 0: the line of the pattern, its base and its offset as they were.
            DxfHatch kept = Mirrored(document, hatch, P(20, 5), P(30, 20));
            AssertLine(30, 0, 0, -1, Root3, FirstLine(Groups(document, kept.Handle)));
            Assert.Equal(30.0, Group(Groups(document, kept.Handle), 52), 9);

            // MIRRHATCH 1: across a line at 56.31 degrees, 82.6198649480404 (twice that less 30), the base (23.0769, -15.3846), the
            // offset reflected, the pattern angle (52) as it was.
            CadCommandSession session = Session(document);
            Run(session, "MIRRHATCH", "1");
            DxfHatch turned = Mirrored(document, hatch, P(20, 5), P(30, 20), session);
            double phi = 2.0 * Math.Atan2(15.0, 10.0);
            AssertLine(
                82.6198649480404,
                23.07692307692307,
                -15.38461538461539,
                -Math.Cos(phi) + Root3 * Math.Sin(phi),
                -Math.Sin(phi) - Root3 * Math.Cos(phi),
                FirstLine(Groups(document, turned.Handle))
            );
            Assert.Equal(30.0, Group(Groups(document, turned.Handle), 52), 9);
        }

        [Fact]
        public void TheVariableMirrhatch_IsKeptBetweenCommands_ZeroOrOne()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);
            CadCommandSession session = Session(document);

            // AutoCAD 2021's default is 0; the session keeps what it is given, and takes no 2.
            Run(session, "SETVAR", "MIRRHATCH");
            Assert.Equal("Enter new value for MIRRHATCH <0>:", session.PromptText);
            Run(session, "1");
            Run(session, "SETVAR", "MIRRHATCH", "2");
            Run(session, "SETVAR", "MIRRHATCH");
            Assert.Equal("Enter new value for MIRRHATCH <1>:", session.PromptText);
            session.Escape();
        }

        [Fact]
        public void TheContextsOfAMirroredHatch_ReflectTheirDirection_AndKeepTheirLinesWithMirrhatch0()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);
            AddSecondScale(document, hatch);

            DxfHatch copy = Mirrored(document, hatch, P(20, 5), P(30, 20));

            // AutoCAD 2021: the lines of both contexts as they were, the direction (0.866, 0.5) across a line at 56.31 degrees
            // (0.1284517677752159, 0.9917157573394819), the view context of the original's place with its direction nought.
            AssertLine(30, 0, 0, -1, Root3, FirstLine(Context(document, copy, "*A1")));
            AssertLine(30, 0, 0, -2, 2.0 * Root3, FirstLine(Context(document, copy, "*A2")));
            double[] direction = Direction(Context(document, copy, "*A1"));
            Assert.Equal(0.1284517677752159, direction[0], 9);
            Assert.Equal(0.9917157573394819, direction[1], 9);
            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, copy)));
            Assert.Equal(
                new[] { "*A1" },
                ((DxfDBDictionary)Manager(copy).GetAt(Collection)).Select(e => e.Key).ToArray()
            );
        }

        [Fact]
        public void TheContextsOfAMirroredHatch_AreReflectedWithMirrhatch1()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);
            AddSecondScale(document, hatch);
            CadCommandSession session = Session(document);
            Run(session, "MIRRHATCH", "1");

            DxfHatch copy = Mirrored(document, hatch, P(20, 5), P(30, 20), session);

            // The line, base and offset of each context as the record's are (82.6198649480404, (23.0769, -15.3846)), the offset
            // of the 1:2 context twice that of the 1:1.
            double phi = 2.0 * Math.Atan2(15.0, 10.0);
            double ox = -Math.Cos(phi) + Root3 * Math.Sin(phi);
            double oy = -Math.Sin(phi) - Root3 * Math.Cos(phi);
            AssertLine(
                82.6198649480404,
                23.07692307692307,
                -15.38461538461539,
                ox,
                oy,
                FirstLine(Context(document, copy, "*A1"))
            );
            AssertLine(
                82.6198649480404,
                23.07692307692307,
                -15.38461538461539,
                2.0 * ox,
                2.0 * oy,
                FirstLine(Context(document, copy, "*A2"))
            );
            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, copy)));
        }

        [Fact]
        public void AHatchTurnedMinus30Degrees_HoldsItsLineAt360_AsAutoCADWritesIt()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);

            Run(Session(document), "ROTATE", new[] { hatch.Handle }, Enter, P(0, 0), "-30");

            // AutoCAD 2021: 330 degrees as the turn, the line at 30 a hair below nought, written 360 (not 0), in the record and in the context.
            AssertLine(360, 0, 0, 0, 2, FirstLine(Groups(document, hatch.Handle)));
            AssertLine(360, 0, 0, 0, 2, FirstLine(Context(document, hatch, "*A1")));
        }

        [Fact]
        public void AHatchTurnedMinus180Degrees_HoldsItsPatternAngleAs180_NotMinus180()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);

            Run(Session(document), "ROTATE", new[] { hatch.Handle }, Enter, P(0, 0), "-180");

            // AutoCAD 2021: 52 of 30 + 180, the line at 210 (30 and 180), not at 30 + -180 = -150.
            Assert.Equal(210.0, Group(Groups(document, hatch.Handle), 52), 9);
            Assert.Equal(210.0, FirstLine(Groups(document, hatch.Handle))[0], 9);
        }

        [Fact]
        public void AHatchTurnedAWholeTurn_IsNotTouched()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);
            List<string> before = Groups(document, hatch.Handle);

            Run(Session(document), "ROTATE", new[] { hatch.Handle }, Enter, P(0, 0), "360");

            // AutoCAD 2021 takes 360 as nought: the line and its offset exactly as they were.
            double[] line = FirstLine(Groups(document, hatch.Handle));
            Assert.Equal(FirstLine(before), line);
        }

        [Fact]
        public void TheAngleOfAGradient_IsLeftAsItIs_ByRotateAndByMirror()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch, 1);
            double angle = hatch.GradientAngle;

            Run(Session(document), "ROTATE", new[] { hatch.Handle }, Enter, P(0, 0), "30");
            Assert.Equal(angle, hatch.GradientAngle);

            DxfHatch copy = Mirrored(document, hatch, P(20, 5), P(30, 20));
            Assert.Equal(angle, copy.GradientAngle);
        }

        [Fact]
        public void AnAnnotativeHatchGivenAScale_WritesItsViewContext_FromTheDefaultContextAsItStands()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);
            Assert.False(Manager(hatch).Contains(Collection));

            Run(Session(document), "-OBJECTSCALE", new[] { hatch.Handle }, Enter, "A", "1:2", Enter);

            // AutoCAD 2021's -OBJECTSCALE Add writes it on the original: the default context before the new one is made, its
            // direction nought, before the contexts of the scales.
            Assert.Equal(new[] { Collection, "ACDB_ANNOTATIONSCALES" }, Manager(hatch).Select(e => e.Key).ToArray());
            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, hatch)));
            Assert.Equal(new[] { 0.0, 0.0 }, Direction(View(document, hatch)));
            AssertLine(30, 0, 0, -2, 2.0 * Root3, FirstLine(Context(document, hatch, "*A2")));
        }

        [Fact]
        public void AMovedAnnotativeHatchGivenAScale_HoldsThePlaceItNowStandsAtInItsView_AndDerivesTheNewContextFromIt()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);
            Run(Session(document), "MOVE", new[] { hatch.Handle }, Enter, P(0, 0), P(100, 50));
            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, hatch)));

            Run(Session(document), "-OBJECTSCALE", new[] { hatch.Handle }, Enter, "A", "1:2", Enter);

            // AutoCAD 2021: the view replaced by the default context as it stands, its base (100, 50); the context of 1:2 twice that.
            AssertLine(30, 100, 50, -1, Root3, FirstLine(View(document, hatch)));
            AssertLine(30, 100, 50, -1, Root3, FirstLine(Context(document, hatch, "*A1")));
            AssertLine(30, 200, 100, -2, 2.0 * Root3, FirstLine(Context(document, hatch, "*A2")));
        }
    }
}
