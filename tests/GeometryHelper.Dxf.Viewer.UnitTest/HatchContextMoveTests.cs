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
    /// An annotative HATCH moved, turned, scaled, mirrored or copied carries the contexts of its scales along, and writes a view
    /// context of the place it stood, as AutoCAD 2021's TransformBy does (probed in its console, mirror-5 GHM5, written by
    /// DxfOut: hatches of ANSI31, HONEY, ANSI37, AR-B816 and a user pattern, one scale and two, moved by (100, 50), turned 30 and
    /// 90 degrees, scaled 2 about the origin and 3 about (20, 10), copied and mirrored). The hatch here is a user pattern at 30
    /// degrees with its lines 2 apart: its line is at 30 degrees through (0, 0), offset (-1, 1.732), its direction (0.866, 0.5).
    /// </summary>
    public class HatchContextMoveTests
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

        /// <summary>An annotative hatch of a user pattern at 30 degrees, lines 2 apart, over a rectangle, at 1:1 and, where asked, 1:2.</summary>
        private static CadDocument Hatched(bool second, out DxfHatch hatch)
        {
            CadDocument document = Blank();
            DxfObjectContextCollection scales = document.Database.ObjectContextManager.GetContextCollection(
                "ACDB_ANNOTATIONSCALES"
            );
            scales.AddContext(Scale("1:1", 1));
            scales.AddContext(Scale("1:2", 2));
            hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternAngle = 30.0 * Math.PI / 180.0;
            hatch.PatternSpace = 2.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            document.Database.ModelSpace.AppendEntity(hatch);
            hatch.Annotative = DxfAnnotativeStates.True;

            if (second)
            {
                hatch.AddContext((DxfAnnotationScale)scales.GetContext("1:2"));
            }

            return document;
        }

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

        /// <summary>The groups of the context of the scale entry given (*A1, the default, or *A2).</summary>
        private static List<string> Context(CadDocument document, DxfHatch hatch, string entry) =>
            Groups(document, ((DxfDBDictionary)Manager(hatch).GetAt("ACDB_ANNOTATIONSCALES")).GetAt(entry).Handle);

        private static List<string> View(CadDocument document, DxfHatch hatch) =>
            Groups(document, ((DxfDBDictionary)Manager(hatch).GetAt(Collection)).GetAt("*A1").Handle);

        /// <summary>The first pattern line a record holds, after its 78: angle, base x, base y, offset x, offset y.</summary>
        private static double[] FirstLine(List<string> groups)
        {
            var line = new double[5];
            int[] codes = { 53, 43, 44, 45, 46 };
            int from = groups.IndexOf(groups.First(g => g.StartsWith("78=")));

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
            int scale = groups.IndexOf(groups.Last(g => g.StartsWith("40=")));
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

        private static void AssertLine(double angle, double x, double y, double ox, double oy, double[] actual)
        {
            double[] expected = { angle, x, y, ox, oy };

            for (int i = 0; i < 5; i++)
            {
                Assert.True(Math.Abs(expected[i] - actual[i]) <= 1E-9, i + ": " + expected[i] + " but " + actual[i]);
            }
        }

        private static void AssertDirection(double x, double y, double[] actual)
        {
            Assert.True(Math.Abs(x - actual[0]) <= 1E-9, "x " + x + " but " + actual[0]);
            Assert.True(Math.Abs(y - actual[1]) <= 1E-9, "y " + y + " but " + actual[1]);
        }

        private static DxfHatch Moved(CadDocument document, DxfHatch hatch, params object[] inputs)
        {
            Run(Session(document), inputs);
            return hatch;
        }

        private static DxfHatch Copied(CadDocument document, DxfHatch hatch, params object[] inputs)
        {
            var before = new HashSet<string>(All<DxfHatch>(document.ModelSpace).Select(e => e.Handle));
            Run(Session(document), inputs);
            return All<DxfHatch>(document.ModelSpace).Single(e => !before.Contains(e.Handle));
        }

        [Fact]
        public void AHatchMoved_MovesTheBaseOfThePatternLineOfItsContext_AndKeepsTheViewOfThePlaceItStood()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);
            Assert.False(Manager(hatch).Contains(Collection));

            Moved(document, hatch, "MOVE", new[] { hatch.Handle }, Enter, P(0, 0), P(100, 50));

            // AutoCAD 2021: 43 of 100 and 44 of 50, the rest of the line and the direction as they were.
            AssertLine(30, 100, 50, -1, Root3, FirstLine(Context(document, hatch, "*A1")));
            AssertLine(30, 100, 50, -1, Root3, FirstLine(Groups(document, hatch.Handle)));
            AssertDirection(Math.Cos(Math.PI / 6.0), 0.5, Direction(Context(document, hatch, "*A1")));

            // The view context: the default context as it stood, its direction nought; before the contexts of the scales.
            Assert.Equal(new[] { Collection, "ACDB_ANNOTATIONSCALES" }, Manager(hatch).Select(e => e.Key).ToArray());
            List<string> view = View(document, hatch);
            Assert.Equal("0=ACDB_HATCHVIEWCONTEXTDATA_CLASS", view[0]);
            AssertLine(30, 0, 0, -1, Root3, FirstLine(view));
            AssertDirection(0, 0, Direction(view));
            Assert.Equal(
                new[] { "100=AcDbHatchViewContextData", "330=0", "10=0", "20=0", "30=1", "51=0", "290=0" },
                view.Skip(view.IndexOf("100=AcDbHatchViewContextData"))
            );
            AssertReopens(document);
        }

        [Fact]
        public void AHatchMovedAgain_HoldsTheViewOfTheLastPlaceItStood()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);

            Moved(document, hatch, "MOVE", new[] { hatch.Handle }, Enter, P(0, 0), P(100, 0));
            Moved(document, hatch, "MOVE", new[] { hatch.Handle }, Enter, P(0, 0), P(0, 50));

            AssertLine(30, 100, 50, -1, Root3, FirstLine(Context(document, hatch, "*A1")));
            AssertLine(30, 100, 0, -1, Root3, FirstLine(View(document, hatch)));
            Assert.Single((DxfDBDictionary)Manager(hatch).GetAt(Collection));
        }

        [Fact]
        public void AHatchTransformedByTheApi_WritesTheViewContext_ThoughMovedByNought()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);

            hatch.TransformBy(GeoTransform3.Identity);

            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, hatch)));
            AssertDirection(0, 0, Direction(View(document, hatch)));
            AssertLine(30, 0, 0, -1, Root3, FirstLine(Context(document, hatch, "*A1")));
        }

        [Fact]
        public void AHatchOfTwoScalesMoved_MovesTheContextOfEachScale_AndHoldsOneView()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);

            Moved(document, hatch, "MOVE", new[] { hatch.Handle }, Enter, P(0, 0), P(100, 50));

            AssertLine(30, 100, 50, -1, Root3, FirstLine(Context(document, hatch, "*A1")));
            AssertLine(30, 100, 50, -2, 2.0 * Root3, FirstLine(Context(document, hatch, "*A2")));
            Assert.Equal(
                new[] { "*A1" },
                ((DxfDBDictionary)Manager(hatch).GetAt(Collection)).Select(e => e.Key).ToArray()
            );
            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, hatch)));
        }

        [Fact]
        public void AHatchTurned30Degrees_TurnsTheAngleOffsetAndDirectionOfItsContexts()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);

            Moved(document, hatch, "ROTATE", new[] { hatch.Handle }, Enter, P(0, 0), "30");

            // AutoCAD 2021: 53 of 60, the offset (-1.732, 1) at 1:1 and (-3.464, 2) at 1:2, the direction (0.5, 0.866).
            AssertLine(60, 0, 0, -Root3, 1, FirstLine(Context(document, hatch, "*A1")));
            AssertLine(60, 0, 0, -2.0 * Root3, 2, FirstLine(Context(document, hatch, "*A2")));
            AssertDirection(0.5, Root3 / 2.0, Direction(Context(document, hatch, "*A1")));
            AssertDirection(0.5, Root3 / 2.0, Direction(Context(document, hatch, "*A2")));
            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, hatch)));
            AssertLine(60, 0, 0, -Root3, 1, FirstLine(Groups(document, hatch.Handle)));
        }

        [Fact]
        public void AHatchTurned90Degrees_FlipsTheSignOfTheSecondOffsetOfItsContext()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);

            Moved(document, hatch, "ROTATE", new[] { hatch.Handle }, Enter, P(0, 0), "90");

            // AutoCAD 2021: 53 of 120 (30 and 90), the offset (-1.732, -1), the direction (-0.5, 0.866).
            AssertLine(120, 0, 0, -Root3, -1, FirstLine(Context(document, hatch, "*A1")));
            AssertDirection(-0.5, Root3 / 2.0, Direction(Context(document, hatch, "*A1")));
        }

        [Fact]
        public void AHatchScaledTwice_DoublesTheOffsetsOfItsContexts_AndTheViewKeepsThemAsTheyWere()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);

            Moved(document, hatch, "SCALE", new[] { hatch.Handle }, Enter, P(0, 0), "2");

            // AutoCAD 2021: (-2, 3.464) at 1:1, (-4, 6.928) at 1:2, the line at 30 degrees through (0, 0) and the direction as it was.
            AssertLine(30, 0, 0, -2, 2.0 * Root3, FirstLine(Context(document, hatch, "*A1")));
            AssertLine(30, 0, 0, -4, 4.0 * Root3, FirstLine(Context(document, hatch, "*A2")));
            AssertDirection(Math.Cos(Math.PI / 6.0), 0.5, Direction(Context(document, hatch, "*A1")));
            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, hatch)));
        }

        [Fact]
        public void AHatchScaledAboutAPoint_MovesTheBaseOfItsContexts_AsThePointIsMoved()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);

            Moved(document, hatch, "SCALE", new[] { hatch.Handle }, Enter, P(20, 10), "3");

            // AutoCAD 2021: (0, 0) scaled 3 about (20, 10) is (-40, -20), the offsets multiplied by 3 and 6.
            AssertLine(30, -40, -20, -3, 3.0 * Root3, FirstLine(Context(document, hatch, "*A1")));
            AssertLine(30, -40, -20, -6, 6.0 * Root3, FirstLine(Context(document, hatch, "*A2")));
        }

        [Fact]
        public void TheCopyOfAHatch_HoldsAContextOfItsOwnPlace_AndAViewOfTheOriginalsPlace()
        {
            CadDocument document = Hatched(true, out DxfHatch hatch);

            DxfHatch copy = Copied(document, hatch, "COPY", new[] { hatch.Handle }, Enter, P(0, 0), P(100, 0), Enter);

            // AutoCAD 2021: 43 of 100 in both contexts of the copy, the original's untouched, 0 in the view.
            AssertLine(30, 100, 0, -1, Root3, FirstLine(Context(document, copy, "*A1")));
            AssertLine(30, 100, 0, -2, 2.0 * Root3, FirstLine(Context(document, copy, "*A2")));
            AssertLine(30, 0, 0, -1, Root3, FirstLine(Context(document, hatch, "*A1")));
            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, copy)));
            AssertReopens(document);
        }

        [Fact]
        public void TheCopyOfAMovedHatch_CarriesTheViewOfItsOriginal_NotTheOriginalsPlace()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);
            Moved(document, hatch, "MOVE", new[] { hatch.Handle }, Enter, P(0, 0), P(100, 0));

            DxfHatch copy = Copied(document, hatch, "COPY", new[] { hatch.Handle }, Enter, P(0, 0), P(0, 50), Enter);

            // AutoCAD 2021 clones the view context of the original: the place the original stood at before its move.
            AssertLine(30, 100, 50, -1, Root3, FirstLine(Context(document, copy, "*A1")));
            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, copy)));
            AssertLine(30, 100, 0, -1, Root3, FirstLine(Context(document, hatch, "*A1")));
        }

        [Fact]
        public void TheItemsOfAnArrayOfAHatch_HoldAContextOfTheirOwnPlace_AndAViewOfTheOriginalsPlace()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);

            Run(
                Session(document),
                "ARRAYRECT",
                new[] { hatch.Handle },
                Enter,
                "AS",
                "Y",
                "COU",
                "2",
                "2",
                "S",
                "70",
                "20",
                "X"
            );

            // AutoCAD 2021 (-ARRAY of 2 rows by 2 columns, 20 and 70 apart): the bases (0, 20), (70, 0) and (70, 20), a view of (0, 0) each.
            List<DxfHatch> items = All<DxfHatch>(document.ModelSpace).Where(e => e != hatch).ToList();
            Assert.Equal(3, items.Count);
            var bases = new List<(double, double)>();

            foreach (DxfHatch item in items)
            {
                double[] line = FirstLine(Context(document, item, "*A1"));
                bases.Add((Math.Round(line[1], 9), Math.Round(line[2], 9)));
                AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, item)));
            }

            Assert.Equal(new[] { (0.0, 20.0), (70.0, 0.0), (70.0, 20.0) }, bases.OrderBy(b => b).ToArray());
        }

        [Fact]
        public void TheCopyTurnedWithItsHatch_TurnsItsContexts_AndHoldsTheViewOfTheOriginal()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);

            DxfHatch copy = Copied(document, hatch, "ROTATE", new[] { hatch.Handle }, Enter, P(0, 0), "C", "30");

            AssertLine(60, 0, 0, -Root3, 1, FirstLine(Context(document, copy, "*A1")));
            AssertDirection(0.5, Root3 / 2.0, Direction(Context(document, copy, "*A1")));
            AssertLine(30, 0, 0, -1, Root3, FirstLine(Context(document, hatch, "*A1")));
            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, copy)));
        }

        [Fact]
        public void TheMirrorOfAHatch_CarriesTheDirectionOfItsContext_AsItCarriesTheRecord()
        {
            CadDocument document = Hatched(false, out DxfHatch hatch);

            DxfHatch copy = Copied(document, hatch, "MIRROR", new[] { hatch.Handle }, Enter, P(0, 0), P(10, 10), Enter);

            // The hatch is mirrored in a plane turned over (its normal (0, 0, -1), so its x is the drawing's -x), where AutoCAD
            // 2021 keeps the normal and writes (0.5, 0.866) for the direction (GHM5): the same direction, in the frame of the
            // hatch. Its context holds what its record holds.
            Assert.Equal(-1.0, copy.Normal.Z);
            AssertDirection(-0.5, Root3 / 2.0, Direction(Context(document, copy, "*A1")));
            double[] record = FirstLine(Groups(document, copy.Handle));
            AssertLine(
                record[0],
                record[1],
                record[2],
                record[3],
                record[4],
                FirstLine(Context(document, copy, "*A1"))
            );
            AssertLine(30, 0, 0, -1, Root3, FirstLine(View(document, copy)));
            AssertDirection(0, 0, Direction(View(document, copy)));
        }

        [Fact]
        public void ASolidHatchMoved_HoldsAViewContext()
        {
            CadDocument document = Blank();
            document
                .Database.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES")
                .AddContext(Scale("1:1", 1));
            var hatch = new DxfHatch();
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            document.Database.ModelSpace.AppendEntity(hatch);
            hatch.Annotative = DxfAnnotativeStates.True;

            Moved(document, hatch, "MOVE", new[] { hatch.Handle }, Enter, P(0, 0), P(100, 50));

            Assert.Equal(new[] { Collection, "ACDB_ANNOTATIONSCALES" }, Manager(hatch).Select(e => e.Key).ToArray());
        }

        [Fact]
        public void APlainHatchMoved_GainsNoExtensionDictionary()
        {
            CadDocument document = Blank();
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternSpace = 2.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[] { new GeoPoint2(42, 50), new GeoPoint2(58, 50), new GeoPoint2(58, 58), new GeoPoint2(42, 58) },
                null
            );
            document.Database.ModelSpace.AppendEntity(hatch);

            Moved(document, hatch, "MOVE", new[] { hatch.Handle }, Enter, P(0, 0), P(100, 50));

            Assert.Null(hatch.ExtensionDictionary);
        }
    }
}
