using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Curves joined into a source as AutoCAD's <c>JoinEntity</c> and <c>JoinEntities</c> join them by the rules of its JOIN:
    /// lines along a line and arcs of a circle gaps and all, pieces end to end into the three polylines and a spline, within
    /// the point tolerance of <see cref="Tolerance.Default"/>, 0.001; the source written anew and the others left as they are.
    /// </summary>
    public class DxfJoinTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        private static readonly GeoVector3 Down = new GeoVector3(0, 0, -1);

        /// <summary>A LWPOLYLINE of points of the plane z = 0, each with no bulge or width.</summary>
        private static DxfPolyline Light(params double[] xy)
        {
            var polyline = new DxfPolyline();

            for (int i = 0; i < xy.Length; i += 2)
            {
                polyline.AddVertexAt(polyline.NumberOfVertices, new GeoPoint2(xy[i], xy[i + 1]), 0, 0, 0);
            }

            return polyline;
        }

        private static DxfLine Line(double x0, double y0, double x1, double y1) => new DxfLine(new GeoPoint3(x0, y0, 0), new GeoPoint3(x1, y1, 0));

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) => Assert.True(actual.IsEqualTo(expected, Tight), "Expected " + expected + ", was " + actual + ".");

        /// <summary>Asserts that a join is refused with an <see cref="ArgumentException"/> whose message says why, as the part given.</summary>
        private static void Refused(string why, Action join) => Assert.Contains(why, Assert.Throws<ArgumentException>(join).Message);

        /// <summary>A curve of each kind a source is given, made anew for each pair: each starts at (10, 0, 0).</summary>
        private static DxfCurve Piece(string kind)
        {
            switch (kind)
            {
                case "LINE": return Line(10, 0, 20, 0);
                case "ARC": return new DxfArc(new GeoPoint3(15, 0, 0), 5, Math.PI, 1.5 * Math.PI);
                case "ELLIPSE": return new DxfEllipse(new GeoPoint3(15, 0, 0), Up, new GeoVector3(5, 0, 0), 0.5, Math.PI, 1.5 * Math.PI);
                case "LWPOLYLINE": return Light(10, 0, 20, 5);
                case "POLYLINE2D": return new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(10, 0, 0), new GeoPoint3(20, 5, 0) }, 0, false, 0, 0, null);
                case "POLYLINE3D": return new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(10, 0, 0), new GeoPoint3(20, 0, 5) }, false);
                case "SPLINE": return new DxfSpline(2, false, false, false, new[] { new GeoPoint3(10, 0, 0), new GeoPoint3(15, 5, 0), new GeoPoint3(20, 0, 0) }, new[] { 0.0, 0, 0, 1, 1, 1 }, null, 1E-10, 1E-10);
                default: return new DxfCircle(new GeoPoint3(15, 0, 0), Up, 5);
            }
        }

        /// <summary>A source of each kind, made anew for each pair: each ends at (10, 0, 0), an arc on the circle of the arc piece and an ellipse on its ellipse.</summary>
        private static DxfCurve Source(string kind)
        {
            switch (kind)
            {
                case "LINE": return Line(0, 0, 10, 0);
                case "ARC": return new DxfArc(new GeoPoint3(15, 0, 0), 5, 0.5 * Math.PI, Math.PI);
                case "ELLIPSE": return new DxfEllipse(new GeoPoint3(15, 0, 0), Up, new GeoVector3(5, 0, 0), 0.5, 0.5 * Math.PI, Math.PI);
                case "LWPOLYLINE": return Light(0, 0, 10, 0);
                case "POLYLINE2D": return new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, 0, false, 0, 0, null);
                case "POLYLINE3D": return new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, false);
                default: return new DxfSpline(1, false, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, new[] { 0.0, 0, 1, 1 }, null, 1E-10, 1E-10);
            }
        }

        [Fact]
        public void EveryKindOfSource_TakesTheKindsAutoCADsJoinTakesIntoIt_AndRefusesTheOthers()
        {
            string[] kinds = { "LINE", "ARC", "ELLIPSE", "LWPOLYLINE", "POLYLINE2D", "POLYLINE3D", "SPLINE", "CIRCLE" };
            var takes = new Dictionary<string, string[]>
            {
                ["LINE"] = new[] { "LINE" },
                ["ARC"] = new[] { "ARC" },
                ["ELLIPSE"] = new[] { "ELLIPSE" },
                ["LWPOLYLINE"] = new[] { "LINE", "ARC", "LWPOLYLINE", "POLYLINE2D" },
                ["POLYLINE2D"] = new[] { "LINE", "ARC", "LWPOLYLINE", "POLYLINE2D" },
                ["POLYLINE3D"] = new[] { "LINE", "LWPOLYLINE", "POLYLINE2D", "POLYLINE3D" },
                ["SPLINE"] = new[] { "LINE", "ARC", "ELLIPSE", "LWPOLYLINE", "POLYLINE2D", "POLYLINE3D", "SPLINE" },
            };
            var ends = new Dictionary<string, GeoPoint3>
            {
                ["LINE"] = new GeoPoint3(20, 0, 0),
                ["ARC"] = new GeoPoint3(15, -5, 0),
                ["ELLIPSE"] = new GeoPoint3(15, -2.5, 0),
                ["LWPOLYLINE"] = new GeoPoint3(20, 5, 0),
                ["POLYLINE2D"] = new GeoPoint3(20, 5, 0),
                ["POLYLINE3D"] = new GeoPoint3(20, 0, 5),
                ["SPLINE"] = new GeoPoint3(20, 0, 0),
            };
            var why = new Dictionary<string, string>
            {
                ["LINE"] = "takes only lines",
                ["ARC"] = "takes only others of its kind",
                ["ELLIPSE"] = "takes only others of its kind",
                ["LWPOLYLINE"] = "A polyline of the plane takes",
                ["POLYLINE2D"] = "A polyline of the plane takes",
                ["POLYLINE3D"] = "A 3D polyline takes",
                ["SPLINE"] = "A SPLINE takes",
            };

            foreach (string source in takes.Keys)
            {
                foreach (string kind in kinds)
                {
                    DxfCurve curve = Source(source);
                    DxfCurve piece = Piece(kind);
                    Near(new GeoPoint3(10, 0, 0), curve.EndPoint);

                    if (takes[source].Contains(kind))
                    {
                        curve.JoinEntity(piece);
                        Assert.True(curve.EndPoint.IsEqualTo(ends[kind], new Tolerance(1E-9, 1E-9)), source + " joined to " + kind + " ends at " + curve.EndPoint + ".");
                        Assert.True(curve.StartPoint.IsEqualTo(new GeoPoint3(source == "ARC" || source == "ELLIPSE" ? 15 : 0, source == "ARC" ? 5 : source == "ELLIPSE" ? 2.5 : 0, 0), Tight), source + " joined to " + kind + " starts at " + curve.StartPoint + ".");
                    }
                    else
                    {
                        Refused(why[source], () => curve.JoinEntity(piece));
                        Assert.True(curve.EndPoint.IsEqualTo(new GeoPoint3(10, 0, 0), Tight), source + " refusing " + kind + " ends at " + curve.EndPoint + ".");
                    }
                }
            }
        }

        [Fact]
        public void A3DPolylineInADrawingOfR12_TakesALine_ItsVerticesWrittenAnewAndReadBack()
        {
            DxfDatabase made = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            var polyline = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, false);
            var line = new DxfLine(new GeoPoint3(10, 0, 5), new GeoPoint3(10.0006, 0, 0));
            made.ModelSpace.AppendEntity(polyline);
            made.ModelSpace.AppendEntity(line);
            string before = Encoding.UTF8.GetString(Saved(made));

            DxfDatabase db = DxfDatabase.Parse(before);
            var source = ByHandle<DxfPolyline3d>(db, polyline.Handle);
            var old = source.Select(v => v.Handle).ToList();
            source.JoinEntity(ByHandle<DxfLine>(db, line.Handle));

            string after = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(before, after, old.Concat(source.Select(v => v.Handle)).ToArray());
            var again = ByHandle<DxfPolyline3d>(DxfDatabase.Parse(after), polyline.Handle);
            Assert.Equal(new[] { Bits(new GeoPoint3(0, 0, 0)), Bits(new GeoPoint3(10, 0, 0)), Bits(new GeoPoint3(10, 0, 5)) }, again.Select(v => Bits(v.Position)));
            Assert.Equal(15.0, again.Length, 12);
        }

        [Fact]
        public void ALine_TakesLinesAlongIt_OverlappingOrWithGaps_EitherWay_AndRunsFromTheLowestEndToTheHighest()
        {
            DxfLine line = Line(0, 0, 10, 0);
            DxfLine other = Line(25, 0, 15, 0);

            line.JoinEntity(other);

            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(line.StartPoint));
            Assert.Equal(Bits(new GeoPoint3(25, 0, 0)), Bits(line.EndPoint));
            Assert.Equal(Bits(new GeoPoint3(25, 0, 0)), Bits(other.StartPoint));

            // An end 0.0009 off the line is on it, and is taken as it is; one 0.0011 off is not.
            line.JoinEntity(Line(-5, 0.0009, -1, 0));
            Assert.Equal(Bits(new GeoPoint3(-5, 0.0009, 0)), Bits(line.StartPoint));
            Refused("is not along the line", () => line.JoinEntity(Line(30, 0.0011, 40, 0)));

            // One lying within it changes nothing; a point of no length along it runs it on; an arc is no line.
            line.JoinEntity(Line(3, 0, 7, 0));
            Assert.Equal(Bits(new GeoPoint3(25, 0, 0)), Bits(line.EndPoint));
            line.JoinEntity(new DxfLine(new GeoPoint3(40, 0, 0), new GeoPoint3(40, 0, 0)));
            Assert.Equal(Bits(new GeoPoint3(40, 0, 0)), Bits(line.EndPoint));
            Refused("takes only lines", () => line.JoinEntity(new DxfArc(new GeoPoint3(0, 0, 0), 5, 0, 1)));
            Assert.Throws<InvalidOperationException>(() => new DxfLine().JoinEntity(Line(0, 0, 1, 0)));
        }

        [Fact]
        public void AnArc_TakesArcsOfItsCircle_CounterClockwiseFromItsStart_AndRefusesTheWholeCircle()
        {
            var center = new GeoPoint3(5, 5, 0);
            var arc = new DxfArc(center, 10, 0, Math.PI / 2);

            // The gap from pi / 2 to pi is filled, as AutoCAD's JOIN fills it counter-clockwise.
            arc.JoinEntity(new DxfArc(center, 10, Math.PI, 1.5 * Math.PI));
            Assert.Equal(0.0, arc.StartAngle);
            Assert.Equal(1.5 * Math.PI, arc.EndAngle, 12);

            // One reaching back over the start, from 7 pi / 4 to pi / 8, moves the start back to 7 pi / 4.
            arc.JoinEntity(new DxfArc(center, 10, 1.75 * Math.PI, Math.PI / 8));
            Assert.Equal(1.75 * Math.PI, arc.StartAngle, 12);
            Assert.Equal(1.5 * Math.PI, arc.EndAngle, 12);
            Assert.Equal(1.75 * Math.PI, arc.TotalAngle, 12);

            // The last quarter would make it the whole circle, a CIRCLE to AutoCAD's JOIN: refused, nothing written.
            Refused("whole curve", () => arc.JoinEntity(new DxfArc(center, 10, 1.5 * Math.PI, 1.75 * Math.PI)));
            Assert.Equal(1.75 * Math.PI, arc.TotalAngle, 12);
        }

        [Fact]
        public void AnArcOfTheNormalTurnedOver_IsTheSameArcRunTheOtherWay_AndARadiusOffByMoreThanTheToleranceIsAnotherCircle()
        {
            // Seen from above, the arc of the normal (0, 0, -1) from 0 to pi / 2 of its own system runs from (-10, 0) to (0, 10)
            // clockwise: the quarter from pi / 2 to pi of the arc of the normal (0, 0, 1).
            var arc = new DxfArc(new GeoPoint3(0, 0, 0), 10, 0, Math.PI / 2);
            var turned = new DxfArc(new GeoPoint3(0, 0, 0), Down, 10, 0, Math.PI / 2);
            Near(new GeoPoint3(-10, 0, 0), turned.StartPoint);

            arc.JoinEntity(turned);

            Assert.Equal(Math.PI, arc.EndAngle, 12);
            Near(new GeoPoint3(-10, 0, 0), arc.EndPoint);

            // A centre 0.0005 off and a radius 0.0009 more are the same circle; the end is read where the other's start is
            // seen from the centre, some 4E-5 off.
            arc.JoinEntity(new DxfArc(new GeoPoint3(0, 0.0005, 0), 10.0009, 1.25 * Math.PI, 1.5 * Math.PI));
            Assert.Equal(1.5 * Math.PI, arc.EndAngle, 3);
            Refused("is not of the same circle", () => arc.JoinEntity(new DxfArc(new GeoPoint3(0, 0, 0), 10.0011, 1.5 * Math.PI, 1.6 * Math.PI)));
            Refused("is not of the same circle", () => arc.JoinEntity(new DxfArc(new GeoPoint3(0, 0, 0), new GeoVector3(1, 0, 0), 10, 0, 1)));
            Refused("takes only others of its kind", () => arc.JoinEntity(Line(0, -10, 10, 0)));
        }

        [Fact]
        public void AnEllipticalArc_TakesArcsOfItsEllipse_ItsMajorAxisEitherWay_ByItsParameter()
        {
            // The ellipse of radii 20 and 10, its quarter from the parameter 0 to pi / 2; the one of the major axis (-20, 0, 0)
            // from 0 to pi / 2 runs from (-20, 0) to (0, -10), the source's from pi to 3 pi / 2.
            var ellipse = new DxfEllipse(new GeoPoint3(0, 0, 0), Up, new GeoVector3(20, 0, 0), 0.5, 0, Math.PI / 2);
            var other = new DxfEllipse(new GeoPoint3(0, 0, 0), Up, new GeoVector3(-20, 0, 0), 0.5, 0, Math.PI / 2);

            ellipse.JoinEntity(other);

            Assert.Equal(0.0, ellipse.StartParam);
            Assert.Equal(1.5 * Math.PI, ellipse.EndParam, 12);
            Near(new GeoPoint3(0, -10, 0), ellipse.EndPoint);
            Refused("is closed", () => ellipse.JoinEntity(new DxfEllipse(new GeoPoint3(0, 0, 0), Up, new GeoVector3(20, 0, 0), 0.5, 0, 2 * Math.PI)));
            Refused("is not of the same ellipse", () => ellipse.JoinEntity(new DxfEllipse(new GeoPoint3(0, 0, 0), Up, new GeoVector3(0, 20, 0), 0.5, 0, 1)));
            Refused("takes only others of its kind", () => ellipse.JoinEntity(new DxfArc(new GeoPoint3(0, 0, 0), 20, 0, 1)));
            Assert.Throws<InvalidOperationException>(() => new DxfEllipse().JoinEntity(other));
        }

        [Fact]
        public void APolyline_TakesLinesArcsAndPolylinesEndToEnd_AtEitherEnd_EachTurnedToRunOnAlongIt()
        {
            DxfPolyline polyline = Light(0, 0, 10, 0);

            // The half circle of radius 5 about (10, 5) from (10, 0) to (10, 10): a bulge of 1 on the second vertex.
            polyline.JoinEntity(new DxfArc(new GeoPoint3(10, 5, 0), 5, -Math.PI / 2, Math.PI / 2));
            Assert.Equal(3, polyline.NumberOfVertices);
            Assert.Equal(1.0, polyline.GetBulgeAt(1), 12);
            Near(new GeoPoint3(10, 10, 0), polyline.EndPoint);

            // A line running into the start is put before it, turned to run on along the polyline.
            polyline.JoinEntity(Line(0, 0, -10, 0));
            Assert.Equal(4, polyline.NumberOfVertices);
            Assert.Equal(Bits(new GeoPoint3(-10, 0, 0)), Bits(polyline.GetPoint3dAt(0)));

            // A polyline from (0, 20) to (10, 10), its arc bulged by 0.5 and its widths 2 to 4, runs on turned: the bulge -0.5,
            // the widths 4 to 2.
            DxfPolyline wide = Light(0, 20, 10, 10);
            wide.SetBulgeAt(0, 0.5);
            wide.SetStartWidthAt(0, 2);
            wide.SetEndWidthAt(0, 4);
            polyline.JoinEntity(wide);
            Assert.Equal(5, polyline.NumberOfVertices);
            Assert.Equal(-0.5, polyline.GetBulgeAt(3));
            Assert.Equal(4.0, polyline.GetStartWidthAt(3));
            Assert.Equal(2.0, polyline.GetEndWidthAt(3));
            Assert.Equal(0.0, polyline.GetStartWidthAt(0));
            Assert.Equal(Bits(new GeoPoint3(0, 20, 0)), Bits(polyline.EndPoint));
            Assert.False(polyline.Closed);
            Assert.Equal(2, wide.NumberOfVertices);
        }

        [Fact]
        public void APolylinesEnds_MeetWithinThePointTolerance_AndItsPlaneWithinItToo()
        {
            DxfPolyline polyline = Light(0, 0, 10, 0);

            // The gap is measured in space: 0.0007 along and 0.0007 up is 0.00099 away and meets; 0.0008 and 0.0008, 0.00113
            // away, does not, though each is within the tolerance alone.
            polyline.JoinEntity(new DxfLine(new GeoPoint3(10.0007, 0, 0.0007), new GeoPoint3(20, 0, 0)));
            Assert.Equal(3, polyline.NumberOfVertices);
            Assert.Equal(Bits(new GeoPoint3(10, 0, 0)), Bits(polyline.GetPoint3dAt(1)));
            Refused("meets neither end", () => polyline.JoinEntity(new DxfLine(new GeoPoint3(20.0008, 0, 0.0008), new GeoPoint3(30, 0, 0))));

            Refused("meets neither end", () => polyline.JoinEntity(Line(20.0011, 0, 30, 0)));
            Refused("is not in the plane", () => polyline.JoinEntity(new DxfLine(new GeoPoint3(20, 0, 0), new GeoPoint3(30, 0, 0.0011))));
            Refused("is not in the plane", () => polyline.JoinEntity(new DxfArc(new GeoPoint3(20, 0, 5), new GeoVector3(0, 1, 0), 5, 1.5 * Math.PI, 1.5 * Math.PI + 1)));
            Refused("A polyline of the plane takes", () => polyline.JoinEntity(new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(20, 0, 0), new GeoPoint3(30, 0, 0) }, false)));
            Refused("A polyline of the plane takes", () => polyline.JoinEntity(new DxfCircle(new GeoPoint3(25, 0, 0), Up, 5)));
            Assert.Equal(3, polyline.NumberOfVertices);
        }

        [Fact]
        public void AnArcOrAPolylineOfTheNormalTurnedOver_JoinsAPolylineWithItsBulgeTurnedOver()
        {
            // The arc of the normal (0, 0, -1) from 0 to pi of its own system runs from (-5, 0) through (0, 5) to (5, 0) seen
            // from above, clockwise: about (15, 0), from (10, 0) to (20, 0) it bulges by -1.
            DxfPolyline polyline = Light(0, 0, 10, 0);
            polyline.JoinEntity(new DxfArc(new GeoPoint3(15, 0, 0), Down, 5, 0, Math.PI));
            Assert.Equal(-1.0, polyline.GetBulgeAt(1), 12);
            Near(new GeoPoint3(20, 0, 0), polyline.EndPoint);
            Near(new GeoPoint3(15, 5, 0), polyline.GetPointAtParameter(1.5));

            // A polyline of the normal (0, 0, -1) has its x turned over: (-20, 0) is (20, 0) in the world.
            DxfPolyline under = Light(-20, 0, -30, 0);
            under.SetBulgeAt(0, 1);
            under.Normal = Down;
            polyline.JoinEntity(under);
            Assert.Equal(-1.0, polyline.GetBulgeAt(2), 12);
            Near(new GeoPoint3(30, 0, 0), polyline.EndPoint);
            Near(new GeoPoint3(25, 5, 0), polyline.GetPointAtParameter(2.5));
        }

        [Fact]
        public void TheSidesOfASquareGivenInAnyOrder_CloseAPolylineOfOneOfThem_AndTheOthersAreLeftOut()
        {
            DxfPolyline polyline = Light(0, 0, 10, 0);
            var others = new DxfEntity[]
            {
                Line(0, 10, 10, 10),
                Line(30, 30, 40, 40),
                Line(0, 0, 0, 10),
                Line(10, 10, 10, 0),
                Line(10, 0, 10, 10),
            };

            IReadOnlyList<int> joined = polyline.JoinEntities(others);

            Assert.Equal(new[] { 0, 2, 3 }, joined);
            Assert.True(polyline.Closed);
            Assert.Equal(4, polyline.NumberOfVertices);
            Assert.Equal(100.0, polyline.Area, 9);
            Assert.Equal(Bits(new GeoPoint3(10, 10, 0)), Bits(polyline.GetPoint3dAt(2)));
            Assert.Empty(Light(0, 0, 10, 0).JoinEntities(new DxfEntity[0]));
            Assert.Throws<InvalidOperationException>(() => polyline.JoinEntity(Line(0, 0, -5, 0)));
            Refused("is null", () => Light(0, 0, 10, 0).JoinEntities(new DxfEntity[] { Line(10, 0, 20, 0), null }));
            Assert.Throws<ArgumentNullException>(() => polyline.JoinEntities(null));
            Assert.Throws<ArgumentNullException>(() => polyline.JoinEntity(null));
        }

        [Fact]
        public void APieceOfNoLengthAtAnEnd_IsJoinedAndAddsNothing_AndOneGivenTwiceIsJoinedOnce()
        {
            DxfPolyline polyline = Light(0, 0, 10, 0);
            DxfLine next = Line(10, 0, 20, 0);
            var point = new DxfLine(new GeoPoint3(20, 0, 0), new GeoPoint3(20.0005, 0, 0));

            IReadOnlyList<int> joined = polyline.JoinEntities(new DxfEntity[] { next, point, next, Line(50, 0, 60, 0) });

            Assert.Equal(new[] { 0, 1 }, joined);
            Assert.Equal(3, polyline.NumberOfVertices);
            Assert.Equal(Bits(new GeoPoint3(20, 0, 0)), Bits(polyline.EndPoint));
            Refused("not joined to itself", () => polyline.JoinEntity(polyline));
        }

        [Fact]
        public void AConstantWidth_StaysOnTheSegmentsThatHadIt_AndALineJoinedHasNone()
        {
            DxfPolyline polyline = Light(0, 0, 10, 0);
            polyline.ConstantWidth = 2;

            polyline.JoinEntity(Line(10, 0, 20, 0));

            Assert.Equal(2.0, polyline.GetStartWidthAt(0));
            Assert.Equal(2.0, polyline.GetEndWidthAt(0));
            Assert.Equal(0.0, polyline.GetStartWidthAt(1));
            Assert.Equal(0.0, polyline.ConstantWidth);
        }

        [Fact]
        public void APolylineOfThePlaneWithDefaultWidths_GivesALineJoinedNone_AndAPieceTurnedHasItsBulgeAndWidthsTurned()
        {
            // Widths 1 by default on (0, 0), (10, 0); a line on to (20, 0) has none; a polyline of the plane from (30, 0) back to
            // (20, 0), bulged by 0.25 and 3 wide to 5, runs on turned, -0.25 and 5 to 3.
            var polyline = new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, 0, false, 1, 1, null);
            var back = new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(30, 0, 0), new GeoPoint3(20, 0, 0) }, 0, false, 3, 5, new[] { 0.25, 0 });

            Assert.Equal(new[] { 0, 1 }, polyline.JoinEntities(new DxfEntity[] { Line(10, 0, 20, 0), back }));

            var vertices = polyline.ToList();
            Assert.Equal(4, vertices.Count);
            Assert.Equal(1.0, vertices[0].StartWidth);
            Assert.Equal(1.0, vertices[0].EndWidth);
            Assert.Equal(0.0, vertices[1].StartWidth);
            Assert.Equal(0.0, vertices[1].EndWidth);
            Assert.Equal(-0.25, vertices[2].Bulge);
            Assert.Equal(5.0, vertices[2].StartWidth);
            Assert.Equal(3.0, vertices[2].EndWidth);
            Assert.Equal(Bits(new GeoPoint3(30, 0, 0)), Bits(vertices[3].Position));
            Assert.Equal(1.0, polyline.DefaultStartWidth);
        }

        [Fact]
        public void APolylineOfThePlane_TakesLinesAndArcs_InADrawingOfR12AsOfR2000_ItsVerticesWrittenAnew()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase made = DxfDatabase.Parse(text);
                var polyline = new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, 0, false, 0, 0, null) { Layer = "joined" };
                DxfLine line = Line(0, 0, 10, 10);
                var arc = new DxfArc(new GeoPoint3(10, 5, 0), 5, -Math.PI / 2, Math.PI / 2);
                made.ModelSpace.AppendEntity(polyline);
                made.ModelSpace.AppendEntity(line);
                made.ModelSpace.AppendEntity(arc);
                string before = Encoding.UTF8.GetString(Saved(made));

                DxfDatabase db = DxfDatabase.Parse(before);
                var source = ByHandle<DxfPolyline2d>(db, polyline.Handle);
                var old = source.Select(v => v.Handle).ToList();
                IReadOnlyList<int> joined = source.JoinEntities(new DxfEntity[] { ByHandle<DxfLine>(db, line.Handle), ByHandle<DxfArc>(db, arc.Handle) });

                Assert.Equal(new[] { 0, 1 }, joined);
                Assert.True(source.Closed);
                var vertices = source.ToList();
                Assert.Equal(3, vertices.Count);
                Assert.Equal(1.0, vertices[1].Bulge, 12);
                Near(new GeoPoint3(10, 10, 0), vertices[2].Position);

                // The triangle (0, 0), (10, 0), (10, 10) and the half circle of radius 5 outside its upright side.
                string after = Encoding.UTF8.GetString(Saved(db));
                OnlyTheseChanged(before, after, old.Concat(vertices.Select(v => v.Handle)).Append(source.Handle).ToArray());
                var again = ByHandle<DxfPolyline2d>(DxfDatabase.Parse(after), polyline.Handle);
                Assert.Equal(source.Area, again.Area, 12);
                Assert.Equal(50.0 + Math.PI * 12.5, again.Area, 9);
                Assert.Equal("joined", again.Layer);
            }
        }

        [Fact]
        public void ALightweightPolylineInADrawing_IsItsOneRecordWritten_ItsOwnVerticesAsTheyWere()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase made = DxfDatabase.Parse(text);
                DxfPolyline polyline = Light(0, 0, 10, 0, 10, 10);
                DxfPolyline other = Light(0, 10, 10, 10);
                made.ModelSpace.AppendEntity(polyline);
                made.ModelSpace.AppendEntity(other);
                string before = Encoding.UTF8.GetString(Saved(made));

                DxfDatabase db = DxfDatabase.Parse(before);
                DxfPolyline source = ByHandle<DxfPolyline>(db, polyline.Handle);
                source.JoinEntity(ByHandle<DxfPolyline>(db, other.Handle));

                string after = Encoding.UTF8.GetString(Saved(db));
                OnlyTheseChanged(before, after, polyline.Handle);
                DxfPolyline again = ByHandle<DxfPolyline>(DxfDatabase.Parse(after), polyline.Handle);
                Assert.Equal(4, again.NumberOfVertices);
                Assert.False(again.Closed);
                Assert.Equal(Bits(new GeoPoint3(0, 10, 0)), Bits(again.EndPoint));
            }
        }

        [Fact]
        public void A3DPolyline_TakesLinesAndPolylinesWithNoArcInAnyPlane_ButNoArc()
        {
            var polyline = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, false);
            DxfPolyline standing = Light(0, 0, 0, 10);
            standing.Normal = new GeoVector3(1, 0, 0);
            standing.Elevation = 10;
            Near(new GeoPoint3(10, 0, 0), standing.StartPoint);

            IReadOnlyList<int> joined = polyline.JoinEntities(new DxfEntity[] { new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, -5)), standing });

            Assert.Equal(new[] { 0, 1 }, joined);
            var points = polyline.Select(v => v.Position).ToList();
            Assert.Equal(4, points.Count);
            Assert.Equal(Bits(new GeoPoint3(0, 0, -5)), Bits(points[0]));
            Near(new GeoPoint3(10, 0, 10), points[3]);

            DxfPolyline bulged = Light(0, 0, -10, 0);
            bulged.SetBulgeAt(0, 0.5);
            Refused("A 3D polyline takes", () => polyline.JoinEntity(new DxfArc(new GeoPoint3(10, 5, 10), 5, 0, 1)));
            Refused("takes no arc", () => new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 1) }, false).JoinEntity(bulged));
        }

        [Fact]
        public void ASpline_TakesCurvesEndToEnd_RaisedToItsDegree_EachCurveKept()
        {
            // The cubic of ezdxf from (0, 0, 0) to (12, 0, 1); a line on from its end, an arc of the plane x = 20 back to it, and a
            // polyline with a half circle into its start; each piece's points stay on the spline joined.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfSpline spline = ByHandle<DxfSpline>(db, "70");
            GeoPoint3 end = spline.EndPoint;
            var line = new DxfLine(end, new GeoPoint3(20, 0, 1));
            var arc = new DxfArc(new GeoPoint3(20, 0, 6), new GeoVector3(1, 0, 0), 5, 1.5 * Math.PI, 0.5 * Math.PI);
            DxfPolyline polyline = Light(-10, 0, 0, 0);
            polyline.SetBulgeAt(0, 1);
            var pieces = new DxfCurve[] { arc, polyline, line };
            var samples = pieces.SelectMany(c => Enumerable.Range(0, 9).Select(i => c.GetPointAtParameter(c.StartParam + (c.EndParam - c.StartParam) * i / 8))).ToList();
            samples.AddRange(Enumerable.Range(0, 9).Select(i => spline.GetPointAtParameter(i / 8.0)));
            Near(new GeoPoint3(20, 0, 1), arc.StartPoint);

            IReadOnlyList<int> joined = spline.JoinEntities(pieces);

            Assert.Equal(new[] { 0, 1, 2 }, joined);
            Assert.Equal(3, spline.Degree);
            Assert.False(spline.HasFitData);
            Assert.True(spline.IsRational);
            Assert.False(spline.Closed);
            Assert.Equal(Bits(new GeoPoint3(-10, 0, 0)), Bits(spline.StartPoint));
            Near(new GeoPoint3(20, 0, 11), spline.EndPoint);
            Assert.All(samples, p => Assert.True(spline.GetClosestPointTo(p, false).IsEqualTo(p, new Tolerance(1E-6, 1E-6)), p.ToString()));

            DxfDatabase again = DxfDatabase.Parse(Encoding.UTF8.GetString(Saved(db)));
            DxfSpline read = ByHandle<DxfSpline>(again, "70");
            Assert.Equal(spline.ControlPoints.Select(Bits), read.ControlPoints.Select(Bits));
            Assert.Equal(spline.Knots, read.Knots);
        }

        [Fact]
        public void ASplineBroughtBackToItsStart_IsClosed_AndAPlanarOneTakenOutOfItsPlaneIsNoLongerSaidToBePlanar()
        {
            var points = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(5, 5, 0), new GeoPoint3(10, 0, 0) };
            var spline = new DxfSpline(2, false, false, false, points, new[] { 0.0, 0, 0, 1, 1, 1 }, null, 1E-10, 1E-10);

            IReadOnlyList<int> joined = spline.JoinEntities(new DxfEntity[] { Line(0, 0, 0, -10), Line(10, -10, 10, 0), Line(0, -10, 10, -10) });

            Assert.Equal(new[] { 0, 1, 2 }, joined);
            Assert.True(spline.Closed);
            Assert.Equal(Bits(spline.StartPoint), Bits(spline.EndPoint));
            Assert.Equal(2, spline.Degree);
            Assert.Throws<InvalidOperationException>(() => spline.JoinEntity(Line(0, 0, -5, 0)));

            // A spline the record says is planar, on the floor: a line on the floor keeps it so, one standing up does not.
            string text = string.Join("\n", new[]
            {
                "  0", "SECTION", "  2", "ENTITIES",
                "  0", "SPLINE", "  5", "2A", "100", "AcDbEntity", "  8", "0", "100", "AcDbSpline",
                "210", "0.0", "220", "0.0", "230", "1.0", " 70", "8", " 71", "1", " 72", "4", " 73", "2", " 74", "0",
                " 40", "0.0", " 40", "0.0", " 40", "1.0", " 40", "1.0",
                " 10", "0.0", " 20", "0.0", " 30", "0.0", " 10", "10.0", " 20", "0.0", " 30", "0.0",
                "  0", "ENDSEC", "  0", "EOF", string.Empty,
            });
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfSpline flat = ByHandle<DxfSpline>(db, "2A");
            flat.JoinEntity(Line(10, 0, 20, 0));
            Assert.True(flat.IsPlanar);
            Assert.Contains("\n230\n", Records(db.ToText()).Single(r => r.Contains("\nSPLINE\n")));
            flat.JoinEntity(new DxfLine(new GeoPoint3(20, 0, 0), new GeoPoint3(20, 0, 5)));
            Assert.False(flat.IsPlanar);
            Assert.DoesNotContain("\n230\n", Records(db.ToText()).Single(r => r.Contains("\nSPLINE\n")));
            Assert.Equal(4, flat.NumControlPoints);

            Refused("A SPLINE takes", () => flat.JoinEntity(new DxfCircle(flat.EndPoint, Up, 5)));
            Refused("A SPLINE takes", () => flat.JoinEntity(new DxfRay()));
            Refused("A SPLINE takes", () => flat.JoinEntity(new DxfDBText()));
        }

        [Fact]
        public void TheFitPointsOfASpline_GoWhenItIsJoined_ItsCurveKept()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfSpline fit = ByHandle<DxfSpline>(db, "6F");
            GeoPoint3 middle = fit.GetPointAtParameter(0.5 * (fit.StartParam + fit.EndParam));

            fit.JoinEntity(new DxfLine(fit.EndPoint, new GeoPoint3(20, 4, 0)));

            Assert.False(fit.HasFitData);
            Assert.True(fit.GetClosestPointTo(middle, false).IsEqualTo(middle, new Tolerance(1E-6, 1E-6)));
            Near(new GeoPoint3(20, 4, 0), fit.EndPoint);
        }

        [Fact]
        public void AnEntityOfAClassJoinTakesNothingInto_IsRefused_AndSoIsAClosedOrFittedSource()
        {
            Assert.Throws<NotSupportedException>(() => new DxfCircle(new GeoPoint3(0, 0, 0), Up, 5).JoinEntity(Line(5, 0, 10, 0)));
            Assert.Throws<NotSupportedException>(() => new DxfDBPoint(new GeoPoint3(0, 0, 0)).JoinEntities(new DxfEntity[0]));

            DxfPolyline closed = Light(0, 0, 10, 0, 10, 10);
            closed.Closed = true;
            Assert.Throws<InvalidOperationException>(() => closed.JoinEntity(Line(0, 0, -5, 0)));
            Refused("is closed", () => Light(0, 0, -5, 0).JoinEntity(closed));
            Assert.Throws<InvalidOperationException>(() => new DxfPolyline().JoinEntity(Line(0, 0, -5, 0)));

            var fitted = new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 5, 0), new GeoPoint3(20, 0, 0) }, 0, false, 0, 0, null);
            fitted.CurveFit();
            Assert.Throws<InvalidOperationException>(() => fitted.JoinEntity(Line(20, 0, 30, 0)));
            Refused("fitted polyline", () => Light(20, 0, 30, 0).JoinEntity(fitted));
        }

        [Fact]
        public void APolylineFarFromTheOrigin_JoinsAsOneAtIt()
        {
            const double Far = 1E7;
            DxfPolyline polyline = Light(Far, Far, Far + 10, Far);

            polyline.JoinEntity(new DxfLine(new GeoPoint3(Far + 20, Far + 0.5, 0), new GeoPoint3(Far + 10.0007, Far, 0)));
            polyline.JoinEntity(new DxfArc(new GeoPoint3(Far, Far + 5, 0), 5, 0.5 * Math.PI, 1.5 * Math.PI));

            // The half circle from (Far, Far + 10) round the left to the start, counter-clockwise: a bulge of 1.
            Assert.Equal(4, polyline.NumberOfVertices);
            Assert.True(polyline.GetPoint3dAt(0).IsEqualTo(new GeoPoint3(Far, Far + 10, 0), new Tolerance(1E-8, 1E-8)));
            Assert.Equal(1.0, polyline.GetBulgeAt(0), 12);
            Assert.Equal(Bits(new GeoPoint3(Far, Far, 0)), Bits(polyline.GetPoint3dAt(1)));
            Assert.Equal(Bits(new GeoPoint3(Far + 20, Far + 0.5, 0)), Bits(polyline.EndPoint));
        }

        [Fact]
        public void TenThousandSegmentsInAnyOrder_EitherWay_AreChainedIntoAPolyline_AndASpline()
        {
            const int Count = 10000;
            var random = new Random(20261027);
            var path = new List<GeoPoint3>(Count + 1);

            for (int i = 0; i <= Count; i++)
            {
                path.Add(new GeoPoint3(i, (i % 2) * 0.5 + 0.001 * random.NextDouble(), 0));
            }

            var segments = new List<DxfEntity>(Count - 1);

            for (int i = 1; i < Count; i++)
            {
                segments.Add(random.Next(2) == 0 ? new DxfLine(path[i], path[i + 1]) : new DxfLine(path[i + 1], path[i]));
            }

            List<DxfEntity> shuffled = segments.OrderBy(_ => random.Next()).ToList();
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(path[0].X, path[0].Y), 0, 0, 0);
            polyline.AddVertexAt(1, new GeoPoint2(path[1].X, path[1].Y), 0, 0, 0);
            var watch = Stopwatch.StartNew();

            IReadOnlyList<int> joined = polyline.JoinEntities(shuffled);

            watch.Stop();
            Assert.Equal(Count - 1, joined.Count);
            Assert.Equal(Count + 1, polyline.NumberOfVertices);
            Assert.All(Enumerable.Range(0, Count / 97 + 1).Select(k => k * 97).Append(Count), i => Assert.Equal(Bits(path[i]), Bits(polyline.GetPoint3dAt(i))));
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), watch.Elapsed.ToString());

            var spline = new DxfSpline(1, false, false, false, new[] { path[0], path[1] }, new[] { 0.0, 0, 1, 1 }, null, 1E-10, 1E-10);
            Assert.Equal(Count - 1, spline.JoinEntities(shuffled).Count);
            Assert.Equal(path.Select(Bits), spline.ControlPoints.Select(Bits));
            Assert.Equal(Bits(path[Count]), Bits(spline.EndPoint));
        }

        [Fact]
        public void APolylineOfNoVertices_IsLeftOut_AndRefusedSayingSo_AndOneOfOneVertexAtAnEndAddsNothing()
        {
            var sources = new Func<DxfCurve>[]
            {
                () => Light(0, 0, 10, 0),
                () => new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, 0, false, 0, 0, null),
                () => new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, false),
                () => new DxfSpline(1, false, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, new[] { 0.0, 0, 1, 1 }, null, 1E-10, 1E-10),
            };

            foreach (Func<DxfCurve> source in sources)
            {
                bool flat = source() is DxfPolyline || source() is DxfPolyline2d;
                var empties = flat ? new DxfEntity[] { new DxfPolyline(), new DxfPolyline2d() } : new DxfEntity[] { new DxfPolyline(), new DxfPolyline2d(), new DxfPolyline3d() };

                foreach (DxfEntity empty in empties)
                {
                    DxfCurve curve = source();
                    Assert.Empty(curve.JoinEntities(new[] { empty }));
                    Refused("has no vertices", () => curve.JoinEntity(empty));
                    Near(new GeoPoint3(10, 0, 0), curve.EndPoint);
                }

                DxfCurve joined = source();
                Assert.Equal(new[] { 0 }, joined.JoinEntities(new DxfEntity[] { Light(10, 0) }));
                Near(new GeoPoint3(10, 0, 0), joined.EndPoint);
                Near(new GeoPoint3(0, 0, 0), joined.StartPoint);
            }
        }

        [Fact]
        public void AnEllipseOfParametersOutsideATurn_IsWrittenWithBothWithinOne_ItsSweepKept()
        {
            // An ellipse the file gives the parameters -3 and -2, joined to the arc of it from 2 pi - 2 to 2 pi - 1.5: from
            // 2 pi - 3, 1.5 radians on, where the end alone moved and was wrapped to 4.78 from a start of -3, 7.78 radians.
            var ellipse = new DxfEllipse(new GeoPoint3(0, 0, 0), Up, new GeoVector3(20, 0, 0), 0.5, 0, 1);
            ellipse.StartParam = -3;
            ellipse.EndParam = -2;
            var other = new DxfEllipse(new GeoPoint3(0, 0, 0), Up, new GeoVector3(20, 0, 0), 0.5, 0, 1);
            other.StartParam = 2 * Math.PI - 2;
            other.EndParam = 2 * Math.PI - 1.5;

            ellipse.JoinEntity(other);

            Assert.Equal(2 * Math.PI - 3, ellipse.StartParam, 12);
            Assert.Equal(1.5, ellipse.EndParam - ellipse.StartParam, 12);
            Near(new GeoPoint3(20 * Math.Cos(-1.5), 10 * Math.Sin(-1.5), 0), ellipse.EndPoint);

            // From 13 to 13.5 joined to 13.5 to 14: from 13 - 4 pi, a radian on.
            var far = new DxfEllipse(new GeoPoint3(0, 0, 0), Up, new GeoVector3(20, 0, 0), 0.5, 0, 1);
            far.StartParam = 13;
            far.EndParam = 13.5;
            var next = new DxfEllipse(new GeoPoint3(0, 0, 0), Up, new GeoVector3(20, 0, 0), 0.5, 0, 1);
            next.StartParam = 13.5;
            next.EndParam = 14;

            far.JoinEntity(next);

            Assert.Equal(13 - 4 * Math.PI, far.StartParam, 12);
            Assert.Equal(1.0, far.EndParam - far.StartParam, 12);
            Near(new GeoPoint3(20 * Math.Cos(14), 10 * Math.Sin(14), 0), far.EndPoint);
        }

        [Fact]
        public void ASplineSaidToBeLinear_IsSoNoLonger_WhenAPieceOffItsLineIsJoined()
        {
            // Flags 24, planar and linear, on the floor along the x axis: a line on along it keeps both, one turning off it in
            // the floor keeps planar alone, 8.
            string text = string.Join("\n", new[]
            {
                "  0", "SECTION", "  2", "ENTITIES",
                "  0", "SPLINE", "  5", "2A", "100", "AcDbEntity", "  8", "0", "100", "AcDbSpline",
                "210", "0.0", "220", "0.0", "230", "1.0", " 70", "24", " 71", "1", " 72", "4", " 73", "2", " 74", "0",
                " 40", "0.0", " 40", "0.0", " 40", "1.0", " 40", "1.0",
                " 10", "0.0", " 20", "0.0", " 30", "0.0", " 10", "10.0", " 20", "0.0", " 30", "0.0",
                "  0", "ENDSEC", "  0", "EOF", string.Empty,
            });
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfSpline spline = ByHandle<DxfSpline>(db, "2A");

            spline.JoinEntity(Line(10, 0, 20, 0));
            Assert.Contains("\n 70\n24\n", Records(db.ToText()).Single(r => r.Contains("\nSPLINE\n")));

            spline.JoinEntity(Line(20, 0, 20, 10));
            Assert.Contains("\n 70\n8\n", Records(db.ToText()).Single(r => r.Contains("\nSPLINE\n")));
            Assert.True(spline.IsPlanar);
        }

        [Fact]
        public void ANearlyWholeArcTiltedOutOfThePlane_IsRefused_ThoughItsEndsAndMiddleAreInIt()
        {
            // An arc of radius 10 sweeping 2 pi - 0.17 on a normal tilted 0.0011 from the floor's: its ends 0.00094 off the
            // floor, its middle on it, its sides 0.011 off.
            double s = Math.Sin(0.0011);
            double c = Math.Cos(0.0011);
            var arc = new DxfArc(new GeoPoint3(0, 0, 0), new GeoVector3(s, 0, c), 10, Math.PI / 2 + 0.085, Math.PI / 2 - 0.085);
            GeoPoint3 at = arc.StartPoint;
            Assert.True(Math.Abs(at.Z) < 0.001 && Math.Abs(arc.EndPoint.Z) < 0.001 && Math.Abs(arc.GetPointAtParameter(arc.StartParam + 0.5 * arc.TotalAngle).Z) < 0.001);
            Refused("is not in the plane", () => Light(at.X - 5, at.Y, at.X, at.Y).JoinEntity(arc));

            // The same of a segment of a polyline so tilted: its vertices 0.00033 off, the middle of its arc 0.00017, its
            // sides 0.0021.
            DxfPolyline tilted = Light(0, 0, 0.3, 0);
            tilted.SetBulgeAt(0, Math.Tan((2 * Math.PI - 0.17) / 4));
            tilted.Normal = new GeoVector3(s, 0, c);
            Refused("is not in the plane", () => Light(-5, 0, 0, 0).JoinEntity(tilted));
            Refused("is not in the plane", () => Light(-5, 0, 0, 0).JoinEntity(Assert.Single(tilted.Explode())));

            // Tilted ten times less, it is within the tolerance and joins.
            var flatter = new DxfArc(new GeoPoint3(0, 0, 0), new GeoVector3(Math.Sin(0.00009), 0, Math.Cos(0.00009)), 10, Math.PI / 2 + 0.085, Math.PI / 2 - 0.085);
            GeoPoint3 near = flatter.StartPoint;
            DxfPolyline joined = Light(near.X - 5, near.Y, near.X, near.Y);
            joined.JoinEntity(flatter);
            Assert.Equal(3, joined.NumberOfVertices);
        }

        [Fact]
        public void ArcsOfOneCircle_JoinAlikeWhateverTheirOrder_TakenCounterClockwiseFromTheEnd()
        {
            // From 0 to pi / 2, with the arcs from 3 pi / 2 to 7 pi / 4 and from 7 pi / 4 round to pi / 8: taken from the end on,
            // the first runs the source to 7 pi / 4, and the second would then make it whole, in either order.
            var center = new GeoPoint3(0, 0, 0);
            var a = new DxfArc(center, 10, 1.5 * Math.PI, 1.75 * Math.PI);
            var b = new DxfArc(center, 10, 1.75 * Math.PI, Math.PI / 8);
            var first = new DxfArc(center, 10, 0, Math.PI / 2);
            var second = new DxfArc(center, 10, 0, Math.PI / 2);

            Assert.Equal(new[] { 0 }, first.JoinEntities(new DxfEntity[] { a, b }));
            Assert.Equal(new[] { 1 }, second.JoinEntities(new DxfEntity[] { b, a }));

            Assert.Equal(Bits(first.StartAngle), Bits(second.StartAngle));
            Assert.Equal(Bits(first.EndAngle), Bits(second.EndAngle));
            Assert.Equal(0.0, first.StartAngle);
            Assert.Equal(1.75 * Math.PI, first.EndAngle, 12);

            var ellipses = new Func<double, double, DxfEllipse>((from, to) =>
            {
                var e = new DxfEllipse(center, Up, new GeoVector3(20, 0, 0), 0.5, 0, 1);
                e.StartParam = from;
                e.EndParam = to;
                return e;
            });
            DxfEllipse one = ellipses(0, Math.PI / 2);
            DxfEllipse other = ellipses(0, Math.PI / 2);
            one.JoinEntities(new DxfEntity[] { ellipses(1.5 * Math.PI, 1.75 * Math.PI), ellipses(1.75 * Math.PI, 2 * Math.PI + Math.PI / 8) });
            other.JoinEntities(new DxfEntity[] { ellipses(1.75 * Math.PI, 2 * Math.PI + Math.PI / 8), ellipses(1.5 * Math.PI, 1.75 * Math.PI) });
            Assert.Equal(Bits(one.StartParam), Bits(other.StartParam));
            Assert.Equal(Bits(one.EndParam), Bits(other.EndParam));
        }

        [Fact]
        public void APieceRunningBackOverTheOneAtTheEnd_IsLeftOut_AsALineDrawnTwiceIs()
        {
            // The source's own segment run back would close it into a loop of no area.
            Refused("runs back over", () => Light(0, 0, 10, 0).JoinEntity(Line(10, 0, 0, 0)));

            // A line drawn twice: the second, run back from (20, 0), is left out, and the chain goes on past it.
            DxfPolyline polyline = Light(0, 0, 10, 0);
            Assert.Equal(new[] { 0, 2 }, polyline.JoinEntities(new DxfEntity[] { Line(10, 0, 20, 0), Line(20, 0, 10, 0), Line(20, 0, 20, 10) }));
            Assert.Equal(4, polyline.NumberOfVertices);
            Assert.Equal(Bits(new GeoPoint3(20, 10, 0)), Bits(polyline.EndPoint));

            // The half circle back over a half circle is refused; the other half, between the same ends, closes it.
            DxfPolyline bulged = Light(0, 0, 10, 0);
            bulged.SetBulgeAt(0, 1);
            Refused("runs back over", () => bulged.JoinEntity(new DxfArc(new GeoPoint3(5, 0, 0), 5, Math.PI, 0)));
            bulged.JoinEntity(new DxfArc(new GeoPoint3(5, 0, 0), 5, 0, Math.PI));
            Assert.True(bulged.Closed);
            Assert.Equal(25 * Math.PI, Math.Abs(bulged.Area), 9);

            Refused("runs back over", () => new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, false).JoinEntity(Line(10, 0, 0, 0)));
            Refused("runs back over", () => new DxfSpline(1, false, false, false, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }, new[] { 0.0, 0, 1, 1 }, null, 1E-10, 1E-10).JoinEntity(Line(10, 0, 0, 0)));
        }
    }
}
