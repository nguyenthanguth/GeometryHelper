using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using GeometryHelper;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A line without ends in the writers (XL-6): a segment of a length centred on its origin where the format has no
    /// line without ends (SVG, OBJ, WKT), and the XLINE entity where it has (DXF).
    /// </summary>
    public class XLineExportTests
    {
        private static readonly XNamespace Ns = "http://www.w3.org/2000/svg";

        private static double Parse(string text) => double.Parse(text, CultureInfo.InvariantCulture);

        // ----- A DXF file is pairs of lines, a group code and its value -----------------------------------------------

        private sealed class Entity
        {
            public string Type { get; set; }

            public List<KeyValuePair<int, string>> Pairs { get; } = new List<KeyValuePair<int, string>>();

            public string Text(int code) => Pairs.First(p => p.Key == code).Value;

            public double Number(int code) => Parse(Text(code));

            public GeoPoint3 Point(int n) => new GeoPoint3(Number(10 + n), Number(20 + n), Number(30 + n));

            public IEnumerable<string> Markers => Pairs.Where(p => p.Key == 100).Select(p => p.Value);
        }

        private static List<Entity> EntitiesOf(DxfWriter writer)
        {
            string[] lines = writer.ToString().TrimEnd('\n').Split('\n');
            var entities = new List<Entity>();
            bool inside = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1];

                if (code == 0 && value == "ENDSEC")
                {
                    inside = false;
                }
                else if (code == 2 && i > 0 && lines[i - 2].Trim() == "0" && lines[i - 1] == "SECTION")
                {
                    inside = value == "ENTITIES";
                }
                else if (inside && code == 0)
                {
                    entities.Add(new Entity { Type = value });
                }
                else if (inside)
                {
                    entities[entities.Count - 1].Pairs.Add(new KeyValuePair<int, string>(code, value));
                }
            }

            return entities;
        }

        private static string Extents(DxfWriter writer)
        {
            string text = writer.ToString();
            int at = text.IndexOf("$EXTMIN", StringComparison.Ordinal);

            return text.Substring(at, text.IndexOf("ENDSEC", at, StringComparison.Ordinal) - at);
        }

        // ----- DXF -----------------------------------------------------------------------------------------------------

        [Fact]
        public void ALineInThePlaneIsAnXLineThroughItsOriginAlongItsUnitDirection()
        {
            Entity entity = Assert.Single(EntitiesOf(new DxfWriter().Add(new GeoXLine2(new GeoPoint2(3, 4), new GeoVector2(0, 2)))));

            Assert.Equal("XLINE", entity.Type);
            Assert.Equal(new[] { "AcDbEntity", "AcDbXline" }, entity.Markers);
            Assert.Equal("0", entity.Text(8));
            Assert.Equal(new GeoPoint3(3, 4, 0), entity.Point(0));
            Assert.Equal(new GeoPoint3(0, 1, 0), entity.Point(1));
        }

        [Fact]
        public void ALineInSpaceIsAnXLineThroughItsOriginAlongItsUnitDirection()
        {
            Entity entity = Assert.Single(EntitiesOf(new DxfWriter().Add(new GeoXLine3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 5)), "axes")));

            Assert.Equal("XLINE", entity.Type);
            Assert.Equal(new[] { "AcDbEntity", "AcDbXline" }, entity.Markers);
            Assert.Equal("axes", entity.Text(8));
            Assert.Equal(new GeoPoint3(1, 2, 3), entity.Point(0));
            Assert.Equal(new GeoPoint3(0, 0, 1), entity.Point(1));
        }

        [Fact]
        public void ASlantedLineKeepsItsUnitDirectionToTheLastDigit()
        {
            var xline = new GeoXLine3(new GeoPoint3(10.25, -3.5, 7.125), new GeoVector3(1, 2, 2));
            Entity entity = Assert.Single(EntitiesOf(new DxfWriter().Add(xline)));

            Assert.Equal(xline.Origin.X, entity.Number(10));
            Assert.Equal(xline.Direction.X, entity.Number(11));
            Assert.Equal(xline.Direction.Y, entity.Number(21));
            Assert.Equal(xline.Direction.Z, entity.Number(31));
        }

        [Fact]
        public void ALineWithoutEndsGrowsTheExtentsOfTheDrawingToItsOriginOnly_AsARayDoes()
        {
            // Nothing is known of how far it runs, so the header is what the points at its origins would make it.
            var line = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 5));
            DxfWriter withLines = new DxfWriter().Add(line)
                .Add(new GeoXLine2(new GeoPoint2(5000, 5000), GeoVector2.XAxis))
                .Add(new GeoXLine3(new GeoPoint3(-900, 0, 40), GeoVector3.ZAxis));
            DxfWriter withPoints = new DxfWriter().Add(line)
                .Add(new GeoPoint2(5000, 5000))
                .Add(new GeoPoint3(-900, 0, 40));
            DxfWriter withRays = new DxfWriter().Add(line)
                .Add(new GeoRay2(new GeoPoint2(5000, 5000), GeoVector2.XAxis))
                .Add(new GeoRay3(new GeoPoint3(-900, 0, 40), GeoVector3.ZAxis));

            Assert.Equal(Extents(withPoints), Extents(withLines));
            Assert.Equal(Extents(withRays), Extents(withLines));
        }

        [Fact]
        public void TheOldShapesAreWrittenAsTheyWereWhenALineIsAddedAfterThem()
        {
            // The text up to the first XLINE is the text of the writer without it: only what comes after is new.
            Func<DxfWriter> old = () => new DxfWriter()
                .Add(new GeoRay2(new GeoPoint2(3, 4), new GeoVector2(0, 2)), "rays")
                .Add(new GeoLine3(GeoPoint3.Origin, new GeoPoint3(1, 2, 3)))
                .Add(new GeoRay3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 5)));

            List<Entity> before = EntitiesOf(old());
            List<Entity> after = EntitiesOf(old().Add(new GeoXLine2(GeoPoint2.Origin, GeoVector2.YAxis)));

            Assert.Equal(before.Count + 1, after.Count);

            for (int i = 0; i < before.Count; i++)
            {
                Assert.Equal(before[i].Type, after[i].Type);
                Assert.Equal(before[i].Pairs, after[i].Pairs);
            }

            Assert.Equal("XLINE", after[after.Count - 1].Type);
        }

        [Fact]
        public void ALineWithNoDirectionOrANumberThatIsNotFiniteIsRefusedBeforeAnythingIsWritten()
        {
            var writer = new DxfWriter().Add(new GeoPoint2(1, 1));
            string before = writer.ToString();
            double nan = double.NaN;

            Assert.Throws<ArgumentException>(() => writer.Add(default(GeoXLine2)));
            Assert.Throws<ArgumentException>(() => writer.Add(default(GeoXLine3)));
            Assert.ThrowsAny<ArgumentException>(() => writer.Add(new GeoXLine2(new GeoPoint2(nan, 0), GeoVector2.XAxis)));
            Assert.ThrowsAny<ArgumentException>(() => writer.Add(new GeoXLine3(new GeoPoint3(0, nan, 0), GeoVector3.XAxis), "new"));
            Assert.Equal(before, writer.ToString());
        }

        // ----- SVG -----------------------------------------------------------------------------------------------------

        private static GeoPolygon2 Square() => new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100), new GeoPoint2(0, 100));

        private static GeoPoint2[] EndsOf(string svg, string colour)
        {
            XElement path = XDocument.Parse(svg).Descendants(Ns + "path").Single(p => (string)p.Attribute("stroke") == colour);
            string[] words = ((string)path.Attribute("d")).Split(' ');

            Assert.Equal(new[] { "M", "L" }, new[] { words[0], words[3] });
            return new[] { new GeoPoint2(Parse(words[1]), Parse(words[2])), new GeoPoint2(Parse(words[4]), Parse(words[5])) };
        }

        [Fact]
        public void ALineInASvgPictureRunsFromEdgeToEdgeBothWays()
        {
            // The picture is the square 0 to 100 with a margin of 5: the line along X through (50, 50) runs from x = -5 to 105.
            GeoPoint2[] ends = EndsOf(new SvgWriter().Add(Square()).Add(new GeoXLine2(new GeoPoint2(50, 50), GeoVector2.XAxis), "red").ToString(), "red");

            Assert.Equal(-5.0, ends[0].X, 6);
            Assert.Equal(50.0, ends[0].Y, 6);
            Assert.Equal(105.0, ends[1].X, 6);
            Assert.Equal(50.0, ends[1].Y, 6);
        }

        [Fact]
        public void ASvgLineLeavesByTheEdgeItMeetsFirstEachWay_AndTheWayItIsMadeDoesNotMatter()
        {
            // A slant through (10, 10) at 45 degrees leaves by the corner (-5, -5) and (105, 105); one of slope 1/2 leaves by the sides.
            GeoPoint2[] diagonal = EndsOf(new SvgWriter().Add(Square()).Add(new GeoXLine2(new GeoPoint2(10, 10), new GeoVector2(1, 1)), "red").ToString(), "red");
            GeoPoint2[] turned = EndsOf(new SvgWriter().Add(Square()).Add(new GeoXLine2(new GeoPoint2(10, 10), new GeoVector2(-1, -1)), "red").ToString(), "red");
            GeoPoint2[] gentle = EndsOf(new SvgWriter().Add(Square()).Add(new GeoXLine2(new GeoPoint2(10, 10), new GeoVector2(2, 1)), "red").ToString(), "red");

            Assert.Equal(-5.0, diagonal[0].X, 5);
            Assert.Equal(-5.0, diagonal[0].Y, 5);
            Assert.Equal(105.0, diagonal[1].X, 5);
            Assert.Equal(105.0, diagonal[1].Y, 5);

            // Turned round, the same segment is drawn from the other end.
            Assert.Equal(diagonal[0].X, turned[1].X, 5);
            Assert.Equal(diagonal[1].X, turned[0].X, 5);

            Assert.Equal(-5.0, gentle[0].X, 5);
            Assert.Equal(10.0 - 15.0 / 2.0, gentle[0].Y, 5);
            Assert.Equal(105.0, gentle[1].X, 5);
            Assert.Equal(10.0 + 95.0 / 2.0, gentle[1].Y, 5);
        }

        [Fact]
        public void TheOriginOfASvgLineGrowsThePictureAsARaysDoes_ButItsDirectionDoesNot()
        {
            double[] ViewBoxOf(SvgWriter writer) => ((string)XDocument.Parse(writer.ToString()).Root.Attribute("viewBox")).Split(' ').Select(Parse).ToArray();

            double[] withLine = ViewBoxOf(new SvgWriter().Add(Square()).Add(new GeoXLine2(new GeoPoint2(200, 0), GeoVector2.XAxis)));
            double[] withRay = ViewBoxOf(new SvgWriter().Add(Square()).Add(new GeoRay2(new GeoPoint2(200, 0), GeoVector2.XAxis)));
            double[] along = ViewBoxOf(new SvgWriter().Add(Square()).Add(new GeoXLine2(new GeoPoint2(50, 50), GeoVector2.XAxis)));

            Assert.Equal(withRay, withLine);
            Assert.Equal(new[] { -5.0, -105.0, 110.0, 110.0 }, ViewBoxOf(new SvgWriter().Add(Square())));
            Assert.Equal(ViewBoxOf(new SvgWriter().Add(Square())), along);
        }

        [Fact]
        public void ASvgLineAddedToNothingElse_IsDrawnAHairLong_AndADefaultLineIsRefused()
        {
            Assert.NotEmpty(new SvgWriter().Add(new GeoXLine2(new GeoPoint2(3, 4), GeoVector2.XAxis)).ToString());
            Assert.Throws<ArgumentException>(() => new SvgWriter().Add(default(GeoXLine2)));
        }

        // ----- OBJ -----------------------------------------------------------------------------------------------------

        private static List<GeoPoint3> Vertices(string obj)
        {
            return obj.Split('\n').Where(l => l.StartsWith("v ", StringComparison.Ordinal))
                .Select(l => l.Split(' ')).Select(w => new GeoPoint3(Parse(w[1]), Parse(w[2]), Parse(w[3]))).ToList();
        }

        [Fact]
        public void ALineInAnObjFileIsALineOfItsLengthCentredOnItsOrigin()
        {
            var xline = new GeoXLine3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 3, 4));
            string obj = new ObjWriter().Add(xline, 50, "axis").ToString();
            List<GeoPoint3> points = Vertices(obj);

            Assert.Equal(2, points.Count);
            Assert.True(points[0].DistanceTo(new GeoPoint3(1, -13, -17)) < 1E-9);
            Assert.True(points[1].DistanceTo(new GeoPoint3(1, 17, 23)) < 1E-9);
            Assert.Contains("l 1 2", obj);
            Assert.Contains("axis", obj);
        }

        [Fact]
        public void AnObjLineOfTheRaysLengthDoublesTheRayAcrossItsOrigin()
        {
            // The ray of length 25 is the half of the line of length 50 ahead of the origin.
            var xline = new GeoXLine3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 3, 4));
            List<GeoPoint3> line = Vertices(new ObjWriter().Add(xline, 50).ToString());
            List<GeoPoint3> ray = Vertices(new ObjWriter().Add(xline.ToRay(), 25).ToString());

            Assert.True(line[1].DistanceTo(ray[1]) < 1E-9);
            Assert.True(line[0].DistanceTo(xline.Reverse().ToRay().Origin.Add(xline.Reverse().Direction.Multiply(25))) < 1E-9);
        }

        [Fact]
        public void AnObjLineWithNoDirectionOrALengthThatIsNoNumberIsRefused()
        {
            Assert.Throws<ArgumentException>(() => new ObjWriter().Add(default(GeoXLine3), 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(new GeoXLine3(GeoPoint3.Origin, GeoVector3.XAxis), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ObjWriter().Add(new GeoXLine3(GeoPoint3.Origin, GeoVector3.XAxis), double.NaN));
        }

        // ----- WKT -----------------------------------------------------------------------------------------------------

        [Fact]
        public void ALineInWktIsALineStringOfItsLengthCentredOnItsOrigin()
        {
            Assert.Equal("LINESTRING (-40 20, 60 20)", Wkt.Write(new GeoXLine2(new GeoPoint2(10, 20), GeoVector2.XAxis), 100));
            Assert.Equal("LINESTRING Z (1 -13 -17, 1 17 23)", Wkt.Write(new GeoXLine3(new GeoPoint3(1, 2, 3), new GeoVector3(0, 3, 4)), 50));
        }

        [Fact]
        public void AWktLineWithNoDirectionOrALengthThatIsNoNumberIsRefused()
        {
            Assert.Throws<ArgumentException>(() => Wkt.Write(default(GeoXLine2), 10));
            Assert.Throws<ArgumentException>(() => Wkt.Write(default(GeoXLine3), 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(new GeoXLine2(GeoPoint2.Origin, GeoVector2.XAxis), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Wkt.Write(new GeoXLine3(GeoPoint3.Origin, GeoVector3.XAxis), double.PositiveInfinity));
        }
    }
}
