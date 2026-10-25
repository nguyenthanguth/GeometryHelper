using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The writers refuse a ray with no direction, and draw a picture of no size as one a unit across.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DxfWriter"/> took a <c>default</c> ray, whose direction is nought, and wrote a RAY with a direction of
    /// (0, 0, 0), which AutoCAD will not read. It is refused now, as a line without ends of no direction is.
    /// </para>
    /// <para>
    /// <see cref="SvgWriter"/> fitted its picture round what was drawn, and a picture of a point, a ray or a line without
    /// ends alone has no size: it was written with a view box of width and height nought, which nothing shows, and a segment
    /// 2E-6 long with one of height nought. An axis the view box would hold less than a millionth of, margin and all, the six
    /// places its numbers are written to, is given a unit now, centred on what is drawn, so that a ray along X from (5, 5)
    /// alone runs to the edge of the view box from (4.45, 4.45) to (5.55, 5.55) and stops at x = 5.55.
    /// </para>
    /// </remarks>
    public class ExportFaultLN1DegenerateTests
    {
        private static readonly XNamespace Ns = "http://www.w3.org/2000/svg";

        private static double[] ViewBox(string svg)
            => ((string)XDocument.Parse(svg).Root.Attribute("viewBox")).Split(' ').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();

        [Fact]
        public void ARayWithNoDirection_IsRefusedByTheDxfWriter()
        {
            var writer = new DxfWriter().Add(new GeoPoint2(1, 1));
            string before = writer.ToString();

            Assert.Throws<ArgumentException>(() => writer.Add(default(GeoRay2)));
            Assert.Throws<ArgumentException>(() => writer.Add(default(GeoRay3)));
            Assert.Equal(before, writer.ToString());
        }

        [Fact]
        public void ARayAlone_IsDrawnInAPictureAUnitAcross()
        {
            string svg = new SvgWriter().Add(new GeoRay2(new GeoPoint2(5, 5), GeoVector2.XAxis)).ToString();
            double[] box = ViewBox(svg);

            Assert.Equal(new[] { 4.45, -5.55, 1.1, 1.1 }, box);

            XElement path = Assert.Single(XDocument.Parse(svg).Descendants(Ns + "path"));
            Assert.Equal("M 5 5 L 5.55 5", (string)path.Attribute("d"));
        }

        [Fact]
        public void APointAlone_AndALineWithoutEndsAlone_AreDrawnInAPictureAUnitAcross()
        {
            Assert.Equal(new[] { -10.55, -20.55, 1.1, 1.1 }, ViewBox(new SvgWriter().Add(new GeoPoint2(-10, 20)).ToString()));
            Assert.Equal(new[] { -0.55, -0.55, 1.1, 1.1 }, ViewBox(new SvgWriter().Add(new GeoXLine2(new GeoPoint2(0, 0), GeoVector2.YAxis)).ToString()));

            // Two points a ten-millionth apart are a picture of no size to six places too, centred between them.
            Assert.Equal(new[] { -0.55, -0.55, 1.1, 1.1 }, ViewBox(new SvgWriter().Add(new GeoPoint2(-5E-8, 0)).Add(new GeoPoint2(5E-8, 0)).ToString()));
        }

        [Fact]
        public void ASegmentTwoMillionthsLong_IsGivenAUnitOfHeight()
        {
            // 2E-6 wide and nought high: the margin of 1E-7 would leave a view box 2E-7 high, nought at six places. The height
            // is given a unit, and the margin is 5 % of that.
            Assert.Equal(new[] { 2.95, -3.55, 0.100002, 1.1 }, ViewBox(new SvgWriter().Add(new GeoLine2(new GeoPoint2(3, 3), new GeoPoint2(3 + 2E-6, 3))).ToString()));
            Assert.Equal(new[] { 999999.95, -3.55, 0.100002, 1.1 }, ViewBox(new SvgWriter().Add(new GeoLine2(new GeoPoint2(1E6, 3), new GeoPoint2(1E6 + 2E-6, 3))).ToString()));
        }

        [Fact]
        public void APictureWithASize_IsFittedAsBefore()
        {
            // A segment 100 long along X: 100 wide and nought high, a margin of 5 all round.
            Assert.Equal(new[] { -5.0, -5.0, 110.0, 10.0 }, ViewBox(new SvgWriter().Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0))).ToString()));

            // Two points a thousandth apart: a margin of five hundred-thousandths.
            Assert.Equal(new[] { -0.00005, -0.00005, 0.0011, 0.0001 }, ViewBox(new SvgWriter().Add(new GeoPoint2(0, 0)).Add(new GeoPoint2(0.001, 0)).ToString()));
        }
    }
}
