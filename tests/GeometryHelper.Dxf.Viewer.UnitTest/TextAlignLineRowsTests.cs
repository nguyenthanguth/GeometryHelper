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
    /// TEXTALIGN along a line of two points against AutoCAD 2021's console run through standard input (16 November), 16 of
    /// the 144 runs compared (12 alignments and 4 spacing modes, four lines, four TEXT 2.5 high, A, BBB, CCCCCC and DDDD, the
    /// last at the origin because the console moves every typed point by the place of the first text it picks, the last
    /// made): every text the same to 1E-4. The width of each text is set so that its box is the 1.6667, 6.6667, 14.1667 and
    /// 9.1667 the console's font gives, where the presenter's fonts would give others.
    /// </summary>
    public class TextAlignLineRowsTests
    {
        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        private static GeoPoint2 Point(string text)
        {
            string[] p = text.Split(',');
            return new GeoPoint2(Number(p[0]), Number(p[1]));
        }

        /// <summary>The rows AutoCAD gave: the alignment, the mode (V, H, S with a spacing of 3, D), the line, the places of the texts, and where each was left.</summary>
        public static readonly IEnumerable<object[]> Rows = new[]
        {
            new object[]
            {
                "Left",
                "V",
                "20,10;90,70",
                "8.5,36;54,51;48.5,4;0,0",
                "53.25,36;70.75,51;15.916667,4;11.25,0",
            },
            new object[]
            {
                "TL",
                "V",
                "100,10;30,60",
                "48.5,49;0,44.5;28.5,17;0,0",
                "41.9,49;48.2,44.5;86.7,17;110.5,0",
            },
            new object[]
            {
                "ML",
                "V",
                "10,90;80,20",
                "46,1.5;33.5,14;48.5,28;0,0",
                "97.25,1.5;84.75,14;70.75,28;98.75,0",
            },
            new object[]
            {
                "BL",
                "V",
                "30,50;100,80",
                "6,11.5;40,46;55,18.5;0,0",
                "-59.833333,11.5;20.666667,46;-43.5,18.5;-86.666667,0",
            },
            new object[]
            {
                "Left",
                "H",
                "20,10;90,70",
                "31.5,54;32,25;37.5,54.5;0,0",
                "31.5,17.357143;32,17.785714;37.5,22.5;0,-9.642857",
            },
            new object[]
            {
                "TL",
                "H",
                "100,10;30,60",
                "28,42;32.5,6.5;49.5,10;0,0",
                "28,58.928571;32.5,55.714286;49.5,43.571429;0,78.928571",
            },
            new object[]
            {
                "ML",
                "H",
                "10,90;80,20",
                "10.5,32;14.5,0.5;49,12.5;0,0",
                "10.5,88.25;14.5,84.25;49,49.75;0,98.75",
            },
            new object[]
            {
                "BL",
                "H",
                "30,50;100,80",
                "0,24.5;50,54.5;52.5,56.5;0,0",
                "0,37.142857;50,58.571429;52.5,59.642857;0,37.142857",
            },
            new object[]
            {
                "Left",
                "S",
                "20,10;90,70",
                "31,52;22.5,26.5;22,0;0,0",
                "35.583309,20.857122;30.388873,16.404748;25.194436,11.952374;20,7.5",
            },
            new object[]
            {
                "TL",
                "S",
                "100,10;30,60",
                "51,35;51,54;52,59.5;0,0",
                "100,7.5;94.0588,11.743715;88.117599,15.987429;82.176399,20.231144",
            },
            new object[]
            {
                "ML",
                "S",
                "10,90;80,20",
                "11.5,22;18.5,4;10.5,10;0,0",
                "10,88.75;23.030628,75.719372;18.409307,80.340693;13.787987,84.962013",
            },
            new object[]
            {
                "BL",
                "S",
                "30,50;100,80",
                "26.5,50.5;12,16.5;6.5,16;0,0",
                "55.772305,61.045274;47.181537,57.363516;38.590768,53.681758;30,50",
            },
            new object[]
            {
                "Left",
                "D",
                "20,10;90,70",
                "43,27;34.5,53;14,40;0,0",
                "67.5,48.214286;90,67.5;43.75,27.857143;20,7.5",
            },
            new object[]
            {
                "TL",
                "D",
                "100,10;30,60",
                "19,8;13.5,56;3,19.5;0,0",
                "100,7.5;30,57.5;53.333333,40.833333;76.666667,24.166667",
            },
            new object[]
            {
                "ML",
                "D",
                "10,90;80,20",
                "52,13.5;57.5,36;29,10.5;0,0",
                "80,18.75;56.666667,42.083333;33.333333,65.416667;10,88.75",
            },
            new object[]
            {
                "BL",
                "D",
                "30,50;100,80",
                "12,31.5;6.5,42.5;24.5,18.5;0,0",
                "79.444444,71.190476;54.722222,60.595238;100,80;30,50",
            },
        };

        [Theory]
        [MemberData(nameof(Rows))]
        public void TextsAlignedToALine_StandWhereAutoCad2021PutsThem(
            string alignment,
            string mode,
            string line,
            string places,
            string expected
        )
        {
            string[] words = { "A", "BBB", "CCCCCC", "DDDD" };
            double[] widths =
            {
                1.6666666666667 / 1.5,
                6.6666666666667 / 4.5,
                14.1666666666667 / 9.0,
                9.1666666666667 / 6.0,
            };
            List<GeoPoint2> at = places.Split(';').Select(Point).ToList();
            var entities = new string[4];

            for (int i = 0; i < 4; i++)
            {
                entities[i] = TestDrawings.Entity(
                    "TEXT",
                    "0",
                    256,
                    "B" + (i + 1),
                    "10",
                    at[i].X,
                    "20",
                    at[i].Y,
                    "30",
                    0.0,
                    "40",
                    2.5,
                    "1",
                    words[i],
                    "41",
                    widths[i]
                );
            }

            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, null, entities), "four.dxf");
            string[] ends = line.Split(';');
            var inputs = new List<object> { "TEXTALIGN", "I", alignment, "O", mode };

            if (mode == "S")
            {
                inputs.Add("3");
            }

            inputs.AddRange(
                new object[] { new[] { "B1", "B2", "B3", "B4" }, Enter, "P", Point(ends[0]), Point(ends[1]) }
            );

            Run(Session(document), inputs.ToArray());

            List<GeoPoint2> want = expected.Split(';').Select(Point).ToList();

            for (int i = 0; i < 4; i++)
            {
                var text = (DxfDBText)document.FindEntity("B" + (i + 1));
                Near(want[i], new GeoPoint2(text.Position.X, text.Position.Y), 1E-4);
            }
        }
    }
}
