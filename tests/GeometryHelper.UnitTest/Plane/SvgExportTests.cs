using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// What <see cref="SvgWriter"/> draws beyond points, lines, arcs and polygons: ellipses, triangles, meshes, rays and
    /// labels. Each picture is parsed back and measured.
    /// </summary>
    public class SvgExportTests
    {
        private static readonly XNamespace Ns = "http://www.w3.org/2000/svg";
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPolygon2 Rectangle(double width, double height)
            => new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(width, 0), new GeoPoint2(width, height), new GeoPoint2(0, height));

        private static double[] ViewBox(XDocument picture)
            => ((string)picture.Root.Attribute("viewBox")).Split(' ').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();

        /// <summary>The corners a path of M, L and Z commands goes through, in the order it goes.</summary>
        private static List<GeoPoint2> Corners(string d)
        {
            string[] tokens = d.Split(' ');
            var corners = new List<GeoPoint2>();

            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == "M" || tokens[i] == "L")
                {
                    corners.Add(new GeoPoint2(
                        double.Parse(tokens[i + 1], CultureInfo.InvariantCulture),
                        double.Parse(tokens[i + 2], CultureInfo.InvariantCulture)));
                }
            }

            return corners;
        }

        private static double SignedArea(IReadOnlyList<GeoPoint2> corners)
        {
            double twice = 0.0;

            for (int i = 0; i < corners.Count; i++)
            {
                GeoPoint2 a = corners[i];
                GeoPoint2 b = corners[(i + 1) % corners.Count];
                twice += a.X * b.Y - b.X * a.Y;
            }

            return twice / 2.0;
        }

        [Fact]
        public void AnEllipseThirtyByTenTurnedThirtyDegreesReadsBackWithItsRadiiAndTurn()
        {
            double turn = Math.PI / 6.0;
            var ellipse = new GeoEllipse2(new GeoPoint2(50, 20), new GeoVector2(Math.Cos(turn), Math.Sin(turn)), 30, 10);

            XDocument picture = XDocument.Parse(new SvgWriter().Add(ellipse, "red", "#eeeeee").ToString());
            XElement drawn = Assert.Single(picture.Descendants(Ns + "ellipse"));

            Assert.Equal(30.0, (double)drawn.Attribute("rx"), 6);
            Assert.Equal(10.0, (double)drawn.Attribute("ry"), 6);
            Assert.Equal(50.0, (double)drawn.Attribute("cx"), 6);
            Assert.Equal(20.0, (double)drawn.Attribute("cy"), 6);
            Assert.Equal("rotate(30 50 20)", (string)drawn.Attribute("transform"));
            Assert.Equal("red", (string)drawn.Attribute("stroke"));
            Assert.Equal("#eeeeee", (string)drawn.Attribute("fill"));
        }

        [Fact]
        public void AnEllipseGrowsThePictureByItsBoundingBoxNotByItsMajorRadiusSquared()
        {
            double turn = Math.PI / 6.0;
            var ellipse = new GeoEllipse2(new GeoPoint2(50, 20), new GeoVector2(Math.Cos(turn), Math.Sin(turn)), 30, 10);

            double halfWidth = Math.Sqrt(30 * 30 * Math.Cos(turn) * Math.Cos(turn) + 10 * 10 * Math.Sin(turn) * Math.Sin(turn));
            double halfHeight = Math.Sqrt(30 * 30 * Math.Sin(turn) * Math.Sin(turn) + 10 * 10 * Math.Cos(turn) * Math.Cos(turn));
            double margin = 0.05 * Math.Max(2 * halfWidth, 2 * halfHeight);

            double[] box = ViewBox(XDocument.Parse(new SvgWriter().Add(ellipse).ToString()));

            Assert.Equal(50 - halfWidth - margin, box[0], 4);
            Assert.Equal(-(20 + halfHeight + margin), box[1], 4);
            Assert.Equal(2 * halfWidth + 2 * margin, box[2], 4);
            Assert.Equal(2 * halfHeight + 2 * margin, box[3], 4);
        }

        [Fact]
        public void AnEllipseWhoseMajorAxisPointsDownIsTurnedANegativeQuarter()
        {
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), new GeoVector2(0, -1), 20, 5);

            XDocument picture = XDocument.Parse(new SvgWriter().Add(ellipse).ToString());
            XElement drawn = Assert.Single(picture.Descendants(Ns + "ellipse"));
            double[] box = ViewBox(picture);

            Assert.Equal("rotate(-90 0 0)", (string)drawn.Attribute("transform"));

            // Turned a quarter, the 20 sits along Y: the picture is 10 wide and 40 high, plus a margin of 5 % of 40 each side.
            Assert.Equal(10.0 + 4.0, box[2], 4);
            Assert.Equal(40.0 + 4.0, box[3], 4);
        }

        [Fact]
        public void AnArcOfAWholeTurn_IsDrawnAsACircleNotAPathThatEndsWhereItStarts()
        {
            // SVG leaves out an arc command whose end is its start, so "M 10 0 A 10 10 0 1 1 10 0" draws nothing at all.
            GeoArc2[] turns =
            {
                new GeoArc2(new GeoPoint2(0, 0), 10, 0, 0, false),
                new GeoArc2(new GeoPoint2(25, -15), 40, 1.0, 1.0, true),
                new GeoArc2(new GeoPoint2(25, -15), 40, 2.5, 2.5, false),
            };

            foreach (GeoArc2 turn in turns)
            {
                XDocument picture = XDocument.Parse(new SvgWriter().Add(turn, "red").ToString());
                XElement drawn = Assert.Single(picture.Descendants(Ns + "circle"));

                Assert.Empty(picture.Descendants(Ns + "path"));
                Assert.Equal(turn.Center.X, (double)drawn.Attribute("cx"), 6);
                Assert.Equal(turn.Center.Y, (double)drawn.Attribute("cy"), 6);
                Assert.Equal(turn.Radius, (double)drawn.Attribute("r"), 6);
                Assert.Equal("red", (string)drawn.Attribute("stroke"));
                Assert.Equal("none", (string)drawn.Attribute("fill"));

                // The picture is fitted to the whole circle, as it is for the circle itself.
                Assert.Equal(ViewBox(XDocument.Parse(new SvgWriter().Add(new GeoCircle2(turn.Center, turn.Radius)).ToString())), ViewBox(picture));
            }
        }

        [Fact]
        public void AnArcSoNearAWholeTurnThatItsEndPrintsAsItsStart_IsDrawnAsACircleToo()
        {
            // The picture has six places, so an arc of radius 10 that stops a billionth of a radian short ends 1E-8 from where it began, and the
            // path "M 10 0 A 10 10 0 1 1 10 0" would again draw nothing.
            GeoArc2[] nearly =
            {
                new GeoArc2(new GeoPoint2(0, 0), 10, 0.0, -1E-9),
                new GeoArc2(new GeoPoint2(0, 0), 10, 0.0, 1E-9, true),
                new GeoArc2(new GeoPoint2(5, 5), 10, 1.0, 1.0 - 1E-8),
            };

            foreach (GeoArc2 arc in nearly)
            {
                XDocument picture = XDocument.Parse(new SvgWriter().Add(arc).ToString());

                Assert.Single(picture.Descendants(Ns + "circle"));
                Assert.Empty(picture.Descendants(Ns + "path"));
            }
        }

        [Fact]
        public void AChainHoldingAnEdgeOfNearlyAWholeTurn_WritesItAsTwoArcsSoItDraws()
        {
            // An edge from (10, 0) to (10, 1E-8) of bulge 4E9 turns a billionth short of a whole turn about a radius of 10, and its end prints as
            // its start; made at a tolerance of 1E-9, since a chain made at the default one would merge the two points. As one arc command it
            // would draw nothing, so it is two, each half of it and through its middle.
            var edges = new[]
            {
                new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                new GeoEdge2(new GeoPoint2(10, 0), new GeoPoint2(10, 1E-8), 4E9),
            };
            GeoPolylineArc2 chain = GeoPolylineArc2.FromEdges(edges, new Tolerance(1E-9, 1E-9, 1E-9, 1E-9));

            XDocument picture = XDocument.Parse(new SvgWriter().Add(chain).ToString());
            string[] tokens = ((string)Assert.Single(picture.Descendants(Ns + "path")).Attribute("d")).Split(' ');

            Assert.Equal(2, tokens.Count(t => t == "A"));
            Assert.Equal("10", tokens[tokens.Length - 2]);
            Assert.Equal("0", tokens[tokens.Length - 1]);

            // The middle it goes through is across the circle from the start, 20 away in X.
            int firstArc = Array.IndexOf(tokens, "A");
            Assert.Equal(20.0, Math.Abs(double.Parse(tokens[firstArc + 6], CultureInfo.InvariantCulture) - 10.0), 3);
        }

        [Fact]
        public void AnArcOfAThousandthOfARadianLessThanAWholeTurn_StaysAPath_AndATinyArcToo()
        {
            // 1E-3 short of a turn at radius 10 ends 0.01 from its start, which six places show; a sweep of 1E-9 is a speck, not a circle.
            foreach (GeoArc2 arc in new[] { new GeoArc2(new GeoPoint2(0, 0), 10, 0.0, -1E-3), new GeoArc2(new GeoPoint2(0, 0), 10, 0.0, 1E-9) })
            {
                XDocument picture = XDocument.Parse(new SvgWriter().Add(arc).ToString());

                Assert.Single(picture.Descendants(Ns + "path"));
                Assert.Empty(picture.Descendants(Ns + "circle"));
            }
        }

        [Fact]
        public void AnArcOfNineTenthsOfATurn_StaysAPathThatEndsAwayFromItsStart()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 10, 0.0, 0.9 * 2.0 * Math.PI, false);

            XDocument picture = XDocument.Parse(new SvgWriter().Add(arc).ToString());
            XElement path = Assert.Single(picture.Descendants(Ns + "path"));

            Assert.Empty(picture.Descendants(Ns + "circle"));
            Assert.StartsWith("M 10 0 A 10 10 0 1 1 ", (string)path.Attribute("d"));
            Assert.DoesNotContain("A 10 10 0 1 1 10 0", (string)path.Attribute("d"));
        }

        [Fact]
        public void ATriangleIsOneClosedPathThroughItsCorners()
        {
            var triangle = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10));

            XDocument picture = XDocument.Parse(new SvgWriter().Add(triangle, "blue", "yellow").ToString());
            XElement path = Assert.Single(picture.Descendants(Ns + "path"));

            Assert.Equal("M 0 0 L 10 0 L 0 10 Z", (string)path.Attribute("d"));
            Assert.Equal("blue", (string)path.Attribute("stroke"));
            Assert.Equal("yellow", (string)path.Attribute("fill"));
            Assert.Equal(new[] { -0.5, -10.5, 11.0, 11.0 }, ViewBox(picture));
        }

        [Fact]
        public void ATriangleListIsOnePathEach()
        {
            GeoTriangle2[] triangles = Rectangle(30, 20).TriangulateSurface(Tolerance);

            XDocument picture = XDocument.Parse(new SvgWriter().Add(triangles, "black", "#cccccc").ToString());
            List<XElement> paths = picture.Descendants(Ns + "path").ToList();

            Assert.Equal(triangles.Length, paths.Count);
            Assert.Equal(30.0 * 20.0, paths.Sum(p => Math.Abs(SignedArea(Corners((string)p.Attribute("d"))))), 6);
        }

        [Fact]
        public void AMeshOfFourCellsIsFourPathsCoveringItsArea()
        {
            GeoMesh2 mesh = Rectangle(200, 200).ToMesh(MeshOptions.Grid(100, 100), Tolerance);

            XDocument picture = XDocument.Parse(new SvgWriter().Add(mesh, "black", "#eeeeee").ToString());
            List<XElement> paths = picture.Descendants(Ns + "path").ToList();

            Assert.Equal(4, mesh.FaceCount);
            Assert.Equal(4, paths.Count);
            Assert.All(paths, p => Assert.EndsWith(" Z", (string)p.Attribute("d")));
            Assert.All(paths, p => Assert.Equal("#eeeeee", (string)p.Attribute("fill")));
            Assert.Equal(mesh.Area, paths.Sum(p => SignedArea(Corners((string)p.Attribute("d")))), 6);
        }

        [Fact]
        public void ARayFromTheOriginAlongXEndsOnTheRightEdgeOfThePicture()
        {
            XDocument picture = XDocument.Parse(new SvgWriter()
                .Add(Rectangle(100, 100))
                .Add(new GeoRay2(new GeoPoint2(0, 0), new GeoVector2(1, 0)), "red")
                .ToString());

            double[] box = ViewBox(picture);
            XElement ray = picture.Descendants(Ns + "path").Single(p => (string)p.Attribute("stroke") == "red");
            List<GeoPoint2> ends = Corners((string)ray.Attribute("d"));

            Assert.Equal(2, ends.Count);
            Assert.Equal(0.0, ends[0].X, 6);
            Assert.Equal(0.0, ends[0].Y, 6);
            Assert.Equal(box[0] + box[2], ends[1].X, 5);
            Assert.Equal(105.0, ends[1].X, 5);
            Assert.Equal(0.0, ends[1].Y, 6);
        }

        [Fact]
        public void ARayLeavesThePictureByTheEdgeItMeetsFirst()
        {
            // The picture is 100 by 50, a margin of 5 on every side: up and to the right at 45 degrees the top comes first.
            XDocument up = XDocument.Parse(new SvgWriter()
                .Add(Rectangle(100, 50))
                .Add(new GeoRay2(new GeoPoint2(0, 0), new GeoVector2(1, 1)), "red")
                .ToString());
            XDocument back = XDocument.Parse(new SvgWriter()
                .Add(Rectangle(100, 50))
                .Add(new GeoRay2(new GeoPoint2(10, 10), new GeoVector2(-1, 0)), "red")
                .ToString());

            List<GeoPoint2> upEnds = Corners((string)up.Descendants(Ns + "path").Single(p => (string)p.Attribute("stroke") == "red").Attribute("d"));
            List<GeoPoint2> backEnds = Corners((string)back.Descendants(Ns + "path").Single(p => (string)p.Attribute("stroke") == "red").Attribute("d"));

            Assert.Equal(55.0, upEnds[1].X, 5);
            Assert.Equal(55.0, upEnds[1].Y, 5);
            Assert.Equal(-5.0, backEnds[1].X, 5);
            Assert.Equal(10.0, backEnds[1].Y, 5);
        }

        [Fact]
        public void TheOriginOfARayGrowsThePictureButItsLengthDoesNot()
        {
            double[] box = ViewBox(XDocument.Parse(new SvgWriter()
                .Add(Rectangle(100, 100))
                .Add(new GeoRay2(new GeoPoint2(200, 0), new GeoVector2(1, 0)))
                .ToString()));

            // 200 wide, 100 high: a margin of 10 each side, and no further.
            Assert.Equal(-10.0, box[0], 5);
            Assert.Equal(220.0, box[2], 5);
            Assert.Equal(120.0, box[3], 5);
        }

        [Fact]
        public void ALabelIsEscapedFlippedBackAndSizedByThePicture()
        {
            const string label = "a<b & \"c\"";

            string svg = new SvgWriter().Add(Rectangle(100, 100)).Text(new GeoPoint2(10, 20), label, "green").ToString();
            XDocument picture = XDocument.Parse(svg);
            XElement text = Assert.Single(picture.Descendants(Ns + "text"));

            Assert.Contains("a&lt;b &amp; ", svg);
            Assert.Equal(label, text.Value);
            Assert.Equal("green", (string)text.Attribute("fill"));
            Assert.Equal("translate(10 20) scale(1,-1)", (string)text.Attribute("transform"));
            Assert.Equal(3.0, (double)text.Attribute("font-size"), 6);
        }

        [Fact]
        public void ALabelsPlaceGrowsThePicture()
        {
            double[] box = ViewBox(XDocument.Parse(new SvgWriter().Add(Rectangle(100, 100)).Text(new GeoPoint2(300, 50), "far").ToString()));

            Assert.Equal(300.0 + 0.05 * 300.0, box[0] + box[2], 5);
        }

        [Fact]
        public void ACharacterXmlForbidsInALabelIsReplacedSoThePictureStillParses()
        {
            // A control character, an unpaired surrogate and the two non-characters; a tab, a line feed and a pair kept.
            string label = "a\u0001b\u0000c\uD800d￾e￿f\tg\nh😀i";

            string svg = new SvgWriter().Add(Rectangle(10, 10)).Text(new GeoPoint2(1, 1), label, "red").ToString();
            XElement text = Assert.Single(XDocument.Parse(svg).Descendants(Ns + "text"));

            Assert.Equal("a�b�c�d�e�f\tg\nh😀i", text.Value);
        }

        [Fact]
        public void ARayWithNoDirectionIsRefused()
        {
            Assert.Throws<ArgumentException>(() => new SvgWriter().Add(default(GeoRay2)));
        }

        [Fact]
        public void ANumberThatRoundsToMinusNoughtIsPrintedAsNought()
        {
            // A pin: its red proof is .NET 10 only, since these tests run on net48, which prints 0 whatever the code does.
            // The major axis (1, -1E-13) turns the ellipse by -5.7E-12 degrees, which .NET 10 prints as -0 where .NET Framework
            // prints 0; so does a centre a billionth to the left of the axis.
            var ellipse = new GeoEllipse2(new GeoPoint2(-1E-9, 5), new GeoVector2(1, -1E-13), 30, 10);

            string svg = new SvgWriter().Add(ellipse).Add(new GeoPoint2(-1E-9, 0)).ToString();
            XElement drawn = Assert.Single(XDocument.Parse(svg).Descendants(Ns + "ellipse"));

            Assert.Equal("rotate(0 0 5)", (string)drawn.Attribute("transform"));
            Assert.Equal("0", (string)drawn.Attribute("cx"));
            Assert.DoesNotContain("-0\"", svg);
            Assert.DoesNotContain("-0 ", svg);
        }

        [Fact]
        public void ALabelWithNoTextIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => new SvgWriter().Text(new GeoPoint2(0, 0), null));
            Assert.Throws<ArgumentNullException>(() => new SvgWriter().Add((IEnumerable<GeoTriangle2>)null));
            Assert.Throws<ArgumentNullException>(() => new SvgWriter().Add((GeoMesh2)null));
        }
    }
}
