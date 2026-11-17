using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// A vertex moved of a closed multiline MLEDIT has cut (<c>DxfMlineBuilder.MoveVertexAt</c>, which a grip and STRETCH use)
    /// against the records AutoCAD 2021 left, as <see cref="MlineMoveVertexClosedEndsTests"/> does it. Where the cut went
    /// across the last vertex (its list empty, the cut going on from the segment before) and a move turns the segment before
    /// it, AutoCAD writes a nought there for the element: the square of 100 cut from (15, 0) to (15, 100) across (100, 100) and
    /// (0, 100) holds {} for both elements at (0, 100), and with (100, 100) moved to (108, 91) it holds {0}. A stop that
    /// now lies at or past the end of a segment of a closed multiline is left out whether the list is odd or even: the
    /// staircase 0,0 100,0 100,50 50,50 50,100 0,100 cut from (75, 50) to (50, 92), with (50, 100) moved to (50, 90),
    /// has {} at (50, 50) for the first element where it held {42}.
    /// </summary>
    public class MlineMoveVertexLastSegmentTests
    {
        private static readonly string[] Cases =
        {
            "0,0;120,0;120,60;70,60;70,120;0,120|CS|10,120|60,0|5|0,110|f3 (0,0){60}{0} (120,0){0}{0} (120,60){0}{0} (70,60){0}{0} (70,120){0,60}{0} (0,120){}{0}|f3 (0,0){60}{0} (120,0){0}{0} (120,60){0}{0} (70,60){0}{0} (70,120){0}{0} (0,110){0}{0}",
            "0,0;120,0;120,60;70,60;70,120;0,120|CA|120,9|95,60|3|80,60|f3 (0,0){0}{0} (120,0){0,9}{0,29} (120,60){25}{45} (70,60){0}{0} (70,120){0}{0} (0,120){0}{0}|f3 (0,0){0}{0} (120,0){0,9}{0,29} (120,60){25}{} (80,60){0}{0} (70,120){0}{0} (0,120){0}{0}",
            "0,0;100,0;100,50;50,50;50,100;0,100|CA|70,0|50,92|4|35,85|f3 (0,0){0,70}{0,90} (100,0){}{} (100,50){}{} (50,50){42}{22} (50,100){0}{0} (0,100){0}{0}|f3 (0,0){0,70}{0,90} (100,0){}{} (100,50){}{} (50,50){}{22} (35,85){0}{0} (0,100){0}{0}",
            "0,0;100,0;100,100;50,60;0,100|CS|70,0|58,66|2|85,85|f3 (0,0){0,70}{0} (100,0){}{0} (100,100){54.0361}{0} (50,60){0}{0} (0,100){0}{0}|f3 (0,0){0,70}{0} (100,0){}{0} (85,85){}{0} (50,60){0}{0} (0,100){0}{0}",
            "0,0;100,0;100,50;50,50;50,100;0,100|CA|75,50|50,92|4|50,90|f3 (0,0){0}{0} (100,0){0}{0} (100,50){0,25}{0,45} (50,50){42}{22} (50,100){0}{0} (0,100){0}{0}|f3 (0,0){0}{0} (100,0){0}{0} (100,50){0,25}{0,45} (50,50){}{22} (50,90){0}{0} (0,100){0}{0}",
            "0,0;120,0;120,60;70,60;70,120;0,120|CA|112,60|10,120|5|15,135|f3 (0,0){0}{0} (120,0){0}{0} (120,60){0,8}{0,28} (70,60){}{} (70,120){60}{80} (0,120){0}{0}|f3 (0,0){0}{0} (120,0){0}{0} (120,60){0,8}{0,28} (70,60){}{} (70,120){}{80} (15,135){0}{0}",
            "0,0;120,0;120,60;70,60;70,120;0,120|CA|10,120|102,0|5|15,135|f3 (0,0){102}{0,20,122} (120,0){0}{0} (120,60){0}{0} (70,60){0}{0} (70,120){0,60}{0,80} (0,120){}{}|f3 (0,0){102}{0,22.3453,124.3453} (120,0){0}{0} (120,60){0}{0} (70,60){0}{0} (70,120){0}{0} (15,135){0}{0}",
            "0,0;100,0;100,50;50,50;50,100;0,100|CA|35,100|15,0|5|-15,85|f3 (0,0){15}{0,20,35} (100,0){0}{0} (100,50){0}{0} (50,50){0}{0} (50,100){0,15}{0,35} (0,100){}{}|f3 (0,0){15}{0,16.7796,31.7796} (100,0){0}{0} (100,50){0}{0} (50,50){0}{0} (50,100){0}{0} (-15,85){0}{0}",
            "0,0;100,0;100,50;50,50;50,100;0,100|CA|100,25|75,50|3|60,50|f3 (0,0){0}{0} (100,0){0,25}{0,45} (100,50){25}{45} (50,50){0}{0} (50,100){0}{0} (0,100){0}{0}|f3 (0,0){0}{0} (100,0){0,25}{0,45} (100,50){25}{} (60,50){0}{0} (50,100){0}{0} (0,100){0}{0}",
            "0,0;100,0;100,100;0,100|CA|30,100|50,0|3|-15,85|f3 (0,0){50}{0,20,70} (100,0){0}{0} (100,100){0,70}{0,90} (0,100){}{}|f3 (0,0){50}{0,16.7796,66.7796} (100,0){0}{0} (100,100){0}{0} (-15,85){0}{0}",
            "0,0;100,0;100,50;50,50;50,100;0,100|CA|100,15|50,92|3|65,65|f3 (0,0){0}{0} (100,0){0,15}{0,35} (100,50){}{} (50,50){42}{22} (50,100){0}{0} (0,100){0}{0}|f3 (0,0){0}{0} (100,0){0,15}{0,35} (100,50){}{} (65,65){}{22} (50,100){0}{0} (0,100){0}{0}",
            "0,0;100,0;100,100;0,100|CA|15,0|100,30|0|10,0|f3 (0,0){0,15}{0,35} (100,0){30}{50} (100,100){0}{0} (0,100){0}{0}|f3 (10,0){0,5}{0,23.0998} (100,0){30}{50} (100,100){0}{0} (0,100){0}{0}",
            "0,0;100,0;50,80|CS|50,0|75,40|2|50,90|f3 (0,0){0,50}{0} (100,0){47.1699}{0} (50,80){0}{0}|f3 (0,0){0,50}{0} (100,0){47.1699}{0} (50,90){0}{0}",
            "0,0;120,0;120,60;70,60;70,120;0,120|CA|112,60|105,60|3|70,70|f3 (0,0){0}{0} (120,0){0}{0} (120,60){0,8,15}{0,28,35} (70,60){0}{0} (70,120){0}{0} (0,120){0}{0}|f3 (0,0){0}{0} (120,0){0}{0} (120,60){0}{0} (70,70){0}{0} (70,120){0}{0} (0,120){0}{0}",
            "0,0;100,0;50,80|CA|30,0|60,0|0|8,-9|f3 (0,0){0,30,60}{0,66.085,96.085} (100,0){0}{0} (50,80){0}{0}|f3 (8,-9){0}{0} (100,0){0}{0} (50,80){0}{0}",
            "0,0;100,0;100,50;50,50;50,100;0,100|CA|50,0|100,25|2|100,40|f3 (0,0){0,50}{0,70} (100,0){25}{45} (100,50){0}{0} (50,50){0}{0} (50,100){0}{0} (0,100){0}{0}|f3 (0,0){0,50}{0,70} (100,0){25}{45} (100,40){0}{0} (50,50){0}{0} (50,100){0}{0} (0,100){0}{0}",
            "0,0;100,0;50,80|CA|25,40|50,0|0|10,0|f3 (0,0){50}{0,36.085,86.085} (100,0){0}{0} (50,80){0,47.1699}{0,79.1699}|f3 (10,0){40}{0,22.3607,72.3607} (100,0){0}{0} (50,80){0}{0}",
            "0,0;100,0;100,50;50,50;50,100;0,100|CA|50,75|25,100|2|108,41|f3 (0,0){0}{0} (100,0){0}{0} (100,50){0}{0} (50,50){0,25}{0,5} (50,100){25}{45} (0,100){0}{0}|f3 (0,0){0}{0} (100,0){0}{0} (108,41){0}{0} (50,50){0,25}{0,7.8641} (50,100){25}{45} (0,100){0}{0}",
            "0,0;100,0;100,50;50,50;50,100;0,100|CA|50,0|100,25|2|90,50|f3 (0,0){0,50}{0,70} (100,0){25}{45} (100,50){0}{0} (50,50){0}{0} (50,100){0}{0} (0,100){0}{0}|f3 (0,0){0,50}{0,70} (100,0){25}{45} (90,50){0}{0} (50,50){0}{0} (50,100){0}{0} (0,100){0}{0}",
        };

        private const int Elements = 2;

        public static IEnumerable<object[]> Table() => Cases.Select(c => new object[] { c });

        private static GeoPoint2 Point(string text)
        {
            string[] xy = text.Split(',');
            return P(
                double.Parse(xy[0], CultureInfo.InvariantCulture),
                double.Parse(xy[1], CultureInfo.InvariantCulture)
            );
        }

        /// <summary>The parameters after the offset that an element holds at a vertex, as the record writes them.</summary>
        private static double[] Tail(DxfUnknownEntity mline, int vertex, int element)
        {
            int at = -1;
            int each = -1;
            var found = new List<double>();

            foreach (DxfTypedValue group in mline.Groups)
            {
                if (group.TypeCode == 11)
                {
                    at++;
                    each = -1;
                }
                else if (group.TypeCode == 74)
                {
                    each++;
                }
                else if (group.TypeCode == 41 && at == vertex && each == element)
                {
                    found.Add(double.Parse(group.Value.Trim(), CultureInfo.InvariantCulture));
                }
            }

            return found.Skip(1).ToArray();
        }

        private static string Number(double value) =>
            (Math.Round(value, 4) + 0.0).ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>The multiline as the rows write it: its flags, then each vertex with its elements' stretches.</summary>
        private static string Describe(DxfUnknownEntity mline)
        {
            var builder = new DxfMlineBuilder(mline);
            int flags =
                1
                | (builder.IsClosed ? 2 : 0)
                | (builder.SuppressStartCaps ? 4 : 0)
                | (builder.SuppressEndCaps ? 8 : 0);
            var vertices = new List<string>();

            for (int i = 0; i < builder.Points.Count; i++)
            {
                string text = "(" + Number(builder.Points[i].X) + "," + Number(builder.Points[i].Y) + ")";

                for (int e = 0; e < Elements; e++)
                {
                    text += "{" + string.Join(",", Tail(mline, i, e).Select(Number)) + "}";
                }

                vertices.Add(text);
            }

            return "f" + flags + " " + string.Join(" ", vertices);
        }

        [Theory]
        [MemberData(nameof(Table))]
        public void AVertexMovedOfACutClosedMultiline_PutsTheNoughtWhereAutoCADDoes(string row)
        {
            string[] parts = row.Split('|');
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            session.PickAperture = 3.0;
            Run(session, "MLINE");
            var points = parts[0].Split(';').Select(p => (object)Point(p)).ToList();
            points.Add("C");
            Run(session, points.ToArray());
            Run(session, "MLEDIT", parts[1], Point(parts[2]), Point(parts[3]), Enter);
            DxfUnknownEntity mline = All<DxfUnknownEntity>(document.ModelSpace).Single(m => m.DxfName == "MLINE");

            Assert.Equal(parts[6], Describe(mline));

            var builder = new DxfMlineBuilder(mline);
            GeoPoint2 target = Point(parts[5]);
            builder.MoveVertexAt(
                int.Parse(parts[4], CultureInfo.InvariantCulture),
                new GeoPoint3(target.X, target.Y, 0.0)
            );
            builder.Rebuild(mline);

            Assert.Equal(parts[7], Describe(mline));
        }
    }
}
