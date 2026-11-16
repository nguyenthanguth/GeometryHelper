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
    /// DIMBREAK Auto against AutoCAD 2021's console run through standard input (16 November), 8 of the 96 runs compared (a
    /// DIMLINEAR of 40 to 90 with its line 15 to 25 above or below, two lines and sometimes a circle across it, the dimension
    /// style of its blank drawing: DIMEXO 0.0625, DIMEXE 0.18, DIMTAD 1): the pieces of the picture are the same lines to 1E-4,
    /// each extension line and the dimension line broken 0.0625 each side of every shape that crosses it.
    /// </summary>
    public class DimBreakAutoRowsTests
    {
        private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        private static double[] Numbers(string text) => text.Split(',').Select(Number).ToArray();

        /// <summary>A line as its ends in order, the lesser x first (the lesser y where they are level), so that two are compared as sets.</summary>
        private static double[] Ordered(double[] s) =>
            s[0] > s[2] + 1E-9 || (Math.Abs(s[0] - s[2]) <= 1E-9 && s[1] > s[3]) ? new[] { s[2], s[3], s[0], s[1] } : s;

        /// <summary>The rows AutoCAD gave: the points of the dimension, the shapes across it (L: a line, C: a circle), and the pieces of its picture.</summary>
        public static readonly IEnumerable<object[]> Rows = new[]
        {
            new object[]
            {
                "8,36;96,36;52,56",
                "L:81,6,81,66;L:-2,48,106,48;C:37,46,8",
                "8,36.0625,8,47.9375;8,48.0625,8,56.18;96,36.0625,96,47.9375;96,48.0625,96,56.18;8.18,56,80.9375,56;81.0625,56,95.82,56",
            },
            new object[]
            {
                "18,7;105,12;61,-18",
                "L:92,-23,92,37;L:8,-4,115,-4;C:47,-6,8",
                "18,6.9375,18,-3.9375;18,-4.0625,18,-18.18;105,11.9375,105,-3.9375;105,-4.0625,105,-18.18;18.18,-18,91.9375,-18;92.0625,-18,104.82,-18",
            },
            new object[]
            {
                "22,36;84,31;53,56",
                "L:56,6,56,66;L:12,52,94,52;C:42,46,8",
                "22,36.0625,22,51.9375;22,52.0625,22,56.18;84,31.0625,84,51.9375;84,52.0625,84,56.18;22.18,56,55.9375,56;56.0625,56,83.82,56",
            },
            new object[]
            {
                "7,39;58,44;32,59",
                "L:22,9,22,69;L:-3,46,68,46;C:24,49,8",
                "7,39.0625,7,45.9375;7,46.0625,7,59.18;58,44.0625,58,45.9375;58,46.0625,58,59.18;7.18,59,21.9375,59;22.0625,59,57.82,59",
            },
            new object[]
            {
                "3,5;48,10;25,-10",
                "L:21,-25,21,35;L:-7,-4,58,-4;C:18,-3,8",
                "3,4.9375,3,-3.9375;3,-4.0625,3,-10.18;48,9.9375,48,-3.9375;48,-4.0625,48,-10.18;3.18,-10,14.064517,-10;14.189517,-10,20.9375,-10;21.0625,-10,21.810483,-10;21.935483,-10,47.82,-10",
            },
            new object[]
            {
                "23,28;73,23;48,3",
                "L:53,-2,53,58;L:13,7,83,7;C:39,15,8",
                "23,27.9375,23,7.0625;23,6.9375,23,2.82;73,22.9375,73,7.0625;73,6.9375,73,2.82;23.18,3,52.9375,3;53.0625,3,72.82,3",
            },
            new object[]
            {
                "3,36;84,36;43,56",
                "L:56,6,56,66;L:-7,42,94,42;C:30,46,8",
                "3,36.0625,3,41.9375;3,42.0625,3,56.18;84,36.0625,84,41.9375;84,42.0625,84,56.18;3.18,56,55.9375,56;56.0625,56,83.82,56",
            },
            new object[]
            {
                "31,1;90,-4;60,26",
                "L:57,-29,57,31;L:21,18,100,18;C:50,13,8",
                "31,1.0625,31,17.9375;31,18.0625,31,26.18;90,-3.9375,90,17.9375;90,18.0625,90,26.18;31.18,26,56.9375,26;57.0625,26,89.82,26",
            },
        };

        [Theory]
        [MemberData(nameof(Rows))]
        public void ADimensionBrokenAtShapesAcrossIt_HasTheSamePiecesAsAutoCad2021(
            string points,
            string shapes,
            string expected
        )
        {
            string[] p = points.Split(';');
            double[] first = Numbers(p[0]);
            double[] second = Numbers(p[1]);
            double[] where = Numbers(p[2]);
            CadDocument document = Blank();
            DxfDimStyleTableRecord standard = document.Database.DimStyleTable["Standard"];
            standard.Dimtad = 1;
            standard.Dimexo = 0.0625;
            standard.Dimexe = 0.18;
            standard.Dimasz = 0.18;
            standard.Dimtxt = 0.18;
            standard.Dimgap = 0.09;
            StandardCurrent(document.Database);
            CadCommandSession session = Session(document);
            Run(session, "DLI", P(first[0], first[1]), P(second[0], second[1]), P(where[0], where[1]));

            foreach (string shape in shapes.Split(';'))
            {
                double[] v = Numbers(shape.Substring(2));

                if (shape.StartsWith("L:", StringComparison.Ordinal))
                {
                    document.Database.ModelSpace.AppendEntity(
                        new DxfLine
                        {
                            StartPoint = new GeoPoint3(v[0], v[1], 0.0),
                            EndPoint = new GeoPoint3(v[2], v[3], 0.0),
                        }
                    );
                }
                else
                {
                    document.Database.ModelSpace.AppendEntity(
                        new DxfCircle(new GeoPoint3(v[0], v[1], 0.0), new GeoVector3(0.0, 0.0, 1.0), v[2])
                    );
                }
            }

            DxfRotatedDimension dimension = Assert.Single(All<DxfRotatedDimension>(document.ModelSpace));
            Run(session, "DIMBREAK", new[] { dimension.Handle }, Enter);

            List<double[]> got = Assert
                .Single(All<DxfRotatedDimension>(document.ModelSpace))
                .DimBlock.OfType<DxfLine>()
                .Where(l => !l.IsErased && l.Layer != "Defpoints")
                .Select(l => Ordered(new[] { l.StartPoint.X, l.StartPoint.Y, l.EndPoint.X, l.EndPoint.Y }))
                .OrderBy(s => Math.Round(s[0], 3))
                .ThenBy(s => Math.Round(s[1], 3))
                .ThenBy(s => Math.Round(s[2], 3))
                .ThenBy(s => Math.Round(s[3], 3))
                .ToList();
            List<double[]> want = expected
                .Split(';')
                .Select(t => Ordered(Numbers(t)))
                .OrderBy(s => Math.Round(s[0], 3))
                .ThenBy(s => Math.Round(s[1], 3))
                .ThenBy(s => Math.Round(s[2], 3))
                .ThenBy(s => Math.Round(s[3], 3))
                .ToList();

            Assert.Equal(want.Count, got.Count);

            for (int i = 0; i < want.Count; i++)
            {
                for (int k = 0; k < 4; k++)
                {
                    Assert.Equal(want[i][k], got[i][k], 4);
                }
            }
        }
    }
}
