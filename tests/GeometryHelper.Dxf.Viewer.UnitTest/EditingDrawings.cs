using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>The drawings and the driver the editing tests share.</summary>
    internal static class EditingDrawings
    {
        /// <summary>Stands for Esc among the inputs of <see cref="Run"/>.</summary>
        public static readonly object Esc = new object();

        /// <summary>Stands for Enter among the inputs of <see cref="Run"/>.</summary>
        public static readonly object Enter = new object();

        public static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        public static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        /// <summary>
        /// A plan of R2000: on layer Steel a line A1 from (0, 0) to (100, 0); on 0 a line A2 from (0, 0) to (0, 50), a circle A3
        /// of radius 10 about (200, 0), an arc A4 of radius 20 about (300, 0) from 0 to 90 degrees, a polyline A5 from (0, 100)
        /// to (100, 100) to (100, 150), a text A6 "NOTE" 5 high at (0, 200), and A7 an INSERT of the block BOX (a square of 10)
        /// at (500, 0); on layer Locked a line A8 from (0, -50) to (100, -50).
        /// </summary>
        public static DxfDatabase PlanDatabase()
        {
            string box = TestDrawings.Block(
                "BOX",
                TestDrawings.Line(0, 0, 10, 0),
                TestDrawings.Line(10, 0, 10, 10),
                TestDrawings.Line(10, 10, 0, 10),
                TestDrawings.Line(0, 10, 0, 0)
            );
            DxfDatabase database = TestDrawings.Drawing(
                TestDrawings.Layer("Steel", 1) + TestDrawings.Layer("Locked", 3),
                box,
                TestDrawings.Line(0, 0, 100, 0, "Steel", handle: "A1"),
                TestDrawings.Line(0, 0, 0, 50, handle: "A2"),
                TestDrawings.Circle(200, 0, 10, handle: "A3"),
                TestDrawings.Arc(300, 0, 20, 0, 90, handle: "A4"),
                TestDrawings.Polyline("0", "A5", false, 0, 100, 0, 100, 100, 0, 100, 150, 0),
                TestDrawings.Text(0, 200, 5, "NOTE", handle: "A6"),
                TestDrawings.Insert("BOX", 500, 0, handle: "A7"),
                TestDrawings.Line(0, -50, 100, -50, "Locked", handle: "A8")
            );
            database.LayerTable["Locked"].IsLocked = true;
            return database;
        }

        public static CadDocument Plan() => CadDocument.From(PlanDatabase(), "plan.dxf");

        /// <summary>A new drawing of R2013 in millimetres, with its text style, dimension style and two layouts.</summary>
        public static CadDocument Blank() => CadDocument.From(new DxfDatabase("AC1027"), "blank.dxf");

        /// <summary>
        /// <see cref="Blank"/> with the multileader style Standard, as ezdxf 1.4.5 writes its default one, which a MULTILEADER
        /// takes: the library reads a multileader style but does not write one.
        /// </summary>
        public static CadDocument BlankWithMLeaderStyle()
        {
            string text = new DxfDatabase("AC1027").ToText();
            string entry = "  3\nACAD_PLOTSTYLENAME\n350\n31\n";
            Assert.Contains(entry, text);
            text = text.Replace(entry, "  3\nACAD_MLEADERSTYLE\n350\nF0\n" + entry);
            string style =
                string.Join(
                    "\n",
                    new[]
                    {
                        "  0",
                        "DICTIONARY",
                        "  5",
                        "F0",
                        "330",
                        "C",
                        "100",
                        "AcDbDictionary",
                        "281",
                        "1",
                        "  3",
                        "Standard",
                        "350",
                        "F1",
                        "  0",
                        "MLEADERSTYLE",
                        "  5",
                        "F1",
                        "102",
                        "{ACAD_REACTORS",
                        "330",
                        "F0",
                        "102",
                        "}",
                        "330",
                        "F0",
                        "100",
                        "AcDbMLeaderStyle",
                        "179",
                        "2",
                        "170",
                        "2",
                        "171",
                        "1",
                        "172",
                        "0",
                        " 90",
                        "2",
                        " 40",
                        "0.0",
                        " 41",
                        "0.0",
                        "173",
                        "1",
                        " 91",
                        "-1056964608",
                        " 92",
                        "-2",
                        "290",
                        "1",
                        " 42",
                        "2.0",
                        "291",
                        "1",
                        " 43",
                        "8.0",
                        "  3",
                        "Standard",
                        " 44",
                        "4.0",
                        "300",
                        string.Empty,
                        "342",
                        "21",
                        "174",
                        "1",
                        "175",
                        "1",
                        "176",
                        "0",
                        "178",
                        "1",
                        " 93",
                        "-1056964608",
                        " 45",
                        "4.0",
                        "292",
                        "0",
                        "297",
                        "0",
                        " 46",
                        "4.0",
                        " 94",
                        "-1056964608",
                        " 47",
                        "1.0",
                        " 49",
                        "1.0",
                        "140",
                        "1.0",
                        "294",
                        "1",
                        "141",
                        "0.0",
                        "177",
                        "0",
                        "142",
                        "1.0",
                        "295",
                        "0",
                        "296",
                        "0",
                        "143",
                        "3.75",
                        "271",
                        "0",
                        "272",
                        "9",
                        "273",
                        "9",
                    }
                ) + "\n";
            int objects = text.IndexOf("OBJECTS\n", StringComparison.Ordinal);
            int end = text.IndexOf("  0\nENDSEC\n", objects, StringComparison.Ordinal);
            text = text.Substring(0, end) + style + text.Substring(end);
            return CadDocument.From(DxfDatabase.Parse(text), "mleader.dxf");
        }

        public static CadCommandSession Session(CadDocument document) =>
            new CadCommandSession(document) { PickAperture = 1.0 };

        /// <summary>
        /// Feeds inputs in turn: a text starts a command when none runs and answers the prompt when one does, a point is
        /// picked, a string array is a selection, <see cref="Enter"/> and <see cref="Esc"/> are the keys.
        /// </summary>
        public static CadCommandSession Run(CadCommandSession session, params object[] inputs)
        {
            foreach (object input in inputs)
            {
                switch (input)
                {
                    case string text:
                        session.Feed(text);
                        break;
                    case GeoPoint2 point:
                        session.Feed(point);
                        break;
                    case string[] handles:
                        session.FeedSelection(handles);
                        break;
                    default:
                        if (ReferenceEquals(input, Enter))
                        {
                            session.Enter();
                        }
                        else if (ReferenceEquals(input, Esc))
                        {
                            session.Escape();
                        }
                        else
                        {
                            throw new ArgumentException("An input of no known kind: " + input);
                        }

                        break;
                }
            }

            return session;
        }

        /// <summary>Checks that the drawing reads back from its text as it is: the same text again, every entity's shapes read.</summary>
        public static void AssertReopens(CadDocument document)
        {
            string text = document.Database.ToText();
            DxfDatabase again = DxfDatabase.Parse(text);
            Assert.Equal(text, again.ToText());

            foreach (DxfEntity entity in again.ModelSpace)
            {
                Assert.NotNull(entity.GetGeometry());
            }
        }

        public static T Entity<T>(CadDocument document, string handle)
            where T : DxfEntity
        {
            DxfEntity entity = document.FindEntity(handle);
            Assert.NotNull(entity);
            return Assert.IsAssignableFrom<T>(entity);
        }

        /// <summary>The entities of a space of a kind, in the order of the space.</summary>
        public static List<T> All<T>(CadSpace space)
            where T : DxfEntity
        {
            var found = new List<T>();

            foreach (DxfEntity entity in space.Block)
            {
                if (entity is T kind)
                {
                    found.Add(kind);
                }
            }

            return found;
        }

        public static void Near(GeoPoint3 expected, GeoPoint3 actual, double tolerance = 1E-9)
        {
            Assert.True(expected.DistanceTo(actual) <= tolerance, "Expected " + expected + " but was " + actual + ".");
        }

        public static void Near(GeoPoint2 expected, GeoPoint2 actual, double tolerance = 1E-9)
        {
            Assert.True((expected - actual).Length <= tolerance, "Expected " + expected + " but was " + actual + ".");
        }
    }
}
