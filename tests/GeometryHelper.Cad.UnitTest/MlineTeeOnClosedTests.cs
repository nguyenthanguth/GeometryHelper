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
    /// MLEDIT's tees (CT, OT, MT) onto a closed second multiline against the records AutoCAD 2021 left (accoreconsole, read
    /// back through <c>DxfOut</c> of a <c>Wblock</c>), STANDARD at scale 20 justified at the top. Of the pairs of segments
    /// the stem can meet, AutoCAD takes the one nearest the picks, the distance along the stem from its pick and along the
    /// second from its pick added, with the closed second's distance along itself from its start the shorter way round (the
    /// last segment of a square 100 round lies near its start, not 300 from it). Each row is the option, the points of the
    /// stem, those of the second ("c:" before them for a closed one), where each is picked and what AutoCAD answered, as
    /// <see cref="MlineTeePairTests"/> writes them.
    /// </summary>
    public class MlineTeeOnClosedTests
    {
        private static readonly string[] Cases =
        {
            "OT|10,80;50,80;50,-10|c:30,20;70,20;70,80;30,80|23,80|58,20| || f9 (10,80){}{} (30,80){}{} | f3 (30,20){0}{0} (70,20){0}{0} (70,80){0}{0} (30,80){0}{0,20,40}",
            "CT|120,150;120,260;30,260|c:90,60;170,60;170,100;90,100|44,260|130,60| || f5 (120,100){20}{20} (120,260){0}{0} (30,260){0}{0} | f3 (90,60){0}{0} (170,60){0}{0} (170,100){0}{0} (90,100){0}{0}",
            "MT|140,50;60,50;60,100|c:20,50;80,50;80,90;20,90|60,64|20,62| || f5 (60,50){0}{0} (60,100){0}{0} | f3 (20,50){0,40}{0} (80,50){0}{0} (80,90){0}{0} (20,90){0}{0}",
            "OT|20,80;20,-20|c:10,20;70,20;70,100;10,100|20,34|10,76|Invalid operation on a self intersecting multiline. || f9 (20,80){0}{0} (20,20){}{0} | f3 (10,20){0}{0} (70,20){0}{0} (70,100){0}{0} (10,100){0}{0}",
            "MT|150,-30;70,-30;70,10|c:50,40;90,40;90,100;50,100|70,-19|50,82| || f9 (150,-30){0}{0} (70,-30){0,50}{0,30} (70,40){}{} | f3 (50,40){0}{0,40,60} (90,40){0}{0} (90,100){0}{0} (50,100){0}{0}",
            "MT|90,-40;90,60|c:80,90;160,90;160,150;80,150|90,-8|80,108| || f9 (90,-40){0,110}{0,110} (90,90){}{} | f3 (80,90){0}{0,30,50} (160,90){0}{0} (160,150){0}{0} (80,150){0}{0}",
            "MT|0,120;-100,120|c:30,70;110,70;110,130;30,130|-90,120|110,100| || f5 (30,120){20}{20} (-100,120){0}{0} | f3 (30,70){0}{0} (110,70){0}{0} (110,130){0}{0} (30,130){0}{0,10,30}",
            "CT|50,0;50,80;0,80|c:40,10;100,10;100,50;40,50|18,80|82,10| || f5 (50,50){20}{20} (50,80){0}{0} (0,80){0}{0} | f3 (40,10){0}{0} (100,10){0}{0} (100,50){0}{0} (40,50){0}{0}",
            "OT|60,50;20,50|c:50,50;90,50;90,110;50,110|43,50|62,50| || f5 (50,50){20}{20} (20,50){0}{0} | f3 (50,50){0}{0} (90,50){0}{0} (90,110){0}{0} (50,110){0}{0,60,80}",
            "CT|40,-20;40,-70;120,-70|c:30,80;90,80;90,120;30,120|79,-70|30,100| || f5 (40,80){20}{20} (40,-70){0}{0} (120,-70){0}{0} | f3 (30,80){0}{0} (90,80){0}{0} (90,120){0}{0} (30,120){0}{0}",
            "CT|70,30;70,-20|c:70,20;130,20;130,80;70,80|70,-6|100,80| || f5 (70,20){20}{20} (70,-20){0}{0} | f3 (70,20){0}{0} (130,20){0}{0} (130,80){0}{0} (70,80){0}{0}",
            "CT|20,70;20,-30|c:10,60;50,60;50,100;10,100|20,-20|22,100| || f5 (20,60){20}{20} (20,-30){0}{0} | f3 (10,60){0}{0} (50,60){0}{0} (50,100){0}{0} (10,100){0}{0}",
            "CT|10,-20;10,-140;90,-140|c:10,70;50,70;50,130;10,130|57,-140|22,130| || f5 (10,70){20}{20} (10,-140){0}{0} (90,-140){0}{0} | f3 (10,70){0}{0} (50,70){0}{0} (50,130){0}{0} (10,130){0}{0}",
            "CT|40,140;40,240|c:-20,70;60,70;60,110;-20,110|40,190|36,70| || f5 (40,110){20}{20} (40,240){0}{0} | f3 (-20,70){0}{0} (60,70){0}{0} (60,110){0}{0} (-20,110){0}{0}",
            "CT|40,-50;40,-120;130,-120|c:-10,50;70,50;70,90;-10,90|40,-85|30,90| || f5 (40,50){20}{20} (40,-120){0}{0} (130,-120){0}{0} | f3 (-10,50){0}{0} (70,50){0}{0} (70,90){0}{0} (-10,90){0}{0}",
            "OT|-40,70;-110,70|c:80,50;140,50;140,110;80,110|-75,70|98,50| || f5 (80,70){20}{20} (-110,70){0}{0} | f3 (80,50){0}{0} (140,50){0}{0} (140,110){0}{0} (80,110){0}{0,40,60}",
            "OT|40,100;40,40;150,40|c:90,30;130,30;130,110;90,110|40,89|118,30| || f9 (40,100){0}{0} (40,40){0,30}{0,50} (90,40){}{} | f3 (90,30){0}{0} (130,30){0}{0} (130,110){0}{0} (90,110){0}{0,90,110}",
            "OT|-40,40;20,40|c:20,20;80,20;80,100;20,100|4,40|62,20| || f9 (-40,40){0,40}{0,40} (20,40){}{} | f3 (20,20){0}{0} (80,20){0}{0} (80,100){0}{0} (20,100){0}{0,80,100}",
            "OT|60,70;-10,70|c:90,40;130,40;130,120;90,120|49,70|90,96| || f5 (90,70){20}{20} (-10,70){0}{0} | f3 (90,40){0}{0} (130,40){0}{0} (130,120){0}{0} (90,120){0}{0,50,70}",
            "MT|130,40;240,40|c:20,-10;100,-10;100,50;20,50|139,40|76,-10| || f5 (100,40){20}{20} (240,40){0}{0} | f3 (20,-10){0}{0} (100,-10){0}{0,50,70} (100,50){0}{0} (20,50){0}{0}",
            "OT|130,110;50,110|c:60,20;100,20;100,100;60,100|121,110|80,20|Invalid operation on a closed multiline. || f1 (130,110){0}{0} (50,110){0}{0} | f3 (60,20){0}{0} (100,20){0}{0} (100,100){0}{0} (60,100){0}{0}",
            "OT|0,70;100,70|c:80,70;140,70;140,150;80,150|88,70|122,150|Invalid operation on a closed multiline. || f1 (0,70){0}{0} (100,70){0}{0} | f3 (80,70){0}{0} (140,70){0}{0} (140,150){0}{0} (80,150){0}{0}",
            "CT|150,-50;150,-150;30,-150|c:0,90;60,90;60,150;0,150|150,-85|30,90|Invalid operation on a self intersecting multiline. || f5 (150,-50){0}{0} (150,-150){0}{0} (30,-150){0}{0} | f3 (0,90){0}{0} (60,90){0}{0} (60,150){0}{0} (0,150){0}{0}",
            "MT|80,80;80,-20;160,-20|c:0,10;60,10;60,70;0,70|80,5|42,70|Invalid operation on a closed multiline. || f1 (80,80){0}{0} (80,-20){0}{0} (160,-20){0}{0} | f3 (0,10){0}{0} (60,10){0}{0} (60,70){0}{0} (0,70){0}{0}",
            "CT|20,90;20,180;90,180|c:90,60;170,60;170,140;90,140|20,161|114,60|Invalid operation on a self intersecting multiline. || f5 (20,90){0}{0} (20,180){0}{0} (90,180){0}{0} | f3 (90,60){0}{0} (170,60){0}{0} (170,140){0}{0} (90,140){0}{0}",
            "MT|20,50;20,110;110,110|c:-20,90;60,90;60,150;-20,150|89,110|4,150| || f5 (60,110){20}{20} (110,110){0}{0} | f3 (-20,90){0}{0} (60,90){0}{0,20,40} (60,150){0}{0} (-20,150){0}{0}",
        };

        public static IEnumerable<object[]> Table() => Cases.Select(c => new object[] { c });

        private const int Elements = 2;

        private static readonly string[] Messages =
        {
            "Invalid operation on a self intersecting multiline.",
            "These multilines do not intersect.",
            "Invalid operation on a closed multiline.",
        };

        private static List<DxfUnknownEntity> Mlines(CadDocument document) =>
            All<DxfUnknownEntity>(document.ModelSpace).Where(m => m.DxfName == "MLINE").ToList();

        private static GeoPoint2 Point(string text)
        {
            string[] xy = text.Split(',');
            return P(
                double.Parse(xy[0], CultureInfo.InvariantCulture),
                double.Parse(xy[1], CultureInfo.InvariantCulture)
            );
        }

        private static object[] Points(string text) => text.Split(';').Select(p => (object)Point(p)).ToArray();

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
        public void TheEditOfTwoMultilines_LeavesWhatAutoCADLeaves(string row)
        {
            string[] parts = row.Split(new[] { '|' }, 6);
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            session.PickAperture = 3.0;

            foreach (string shape in new[] { parts[1], parts[2] })
            {
                bool closed = shape.StartsWith("c:", StringComparison.Ordinal);
                Run(session, "MLINE");
                Run(session, Points(closed ? shape.Substring(2) : shape));
                Run(session, closed ? (object)"C" : Enter);
            }

            Run(session, "MLEDIT", parts[0], Point(parts[3]), Point(parts[4]), Enter);

            List<DxfUnknownEntity> mlines = Mlines(document);
            string messages = string.Join(" / ", session.History.Where(h => Messages.Contains(h)));
            string actual = messages + " || " + string.Join(" | ", mlines.Select(Describe));

            Assert.Equal(parts[5], actual);
        }
    }
}
