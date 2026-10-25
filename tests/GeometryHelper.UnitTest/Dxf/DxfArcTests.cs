using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The ARC as a <see cref="DxfArc"/>: its angles in radians from the degrees of the file, its centre in the world, set and
    /// read back, made in code and appended, erased, walked by its angle, and moved and mirrored as ezdxf moves one.
    /// </summary>
    public class DxfArcTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        private static readonly GeoTransform3 Turn = new GeoTransform3(new[,]
        {
            { 0.7816391739070251, -0.4829292842142122, 0.3947397981737998, 10.0 },
            { 0.5501172307043584, 0.8320301337746346, -0.07139249941787586, -5.0 },
            { -0.29395787843858057, 0.27295633888831433, 0.9160150668873173, 2.5 },
            { 0.0, 0.0, 0.0, 1.0 },
        });

        private static readonly GeoTransform3 Mirror = new GeoTransform3(new double[,] { { -1, 0, 0, 3 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });

        [Fact]
        public void AnArcOnATiltedNormal_IsReadAsEzdxfReadsIt_ItsAnglesInRadians()
        {
            // ezdxf 1.4.5: ARC 40 of R2000 (43 of R12), groups 10/20/30 (30, 30, 0) on the extrusion (1, 2, 3), 15 to 200
            // degrees: OCS.to_wcs (-37.589873214007014, -8.09770710302035, 17.928429140015904), normal (0.2672612419124244,
            // 0.5345224838248488, 0.8017837257372732), start 0.2617993877991494 and end 3.490658503988659 radians.
            foreach (var (text, handle) in new[] { (DxfFileEzdxfDrawings.R12, "43"), (DxfFileEzdxfDrawings.R2000, "40"), (DxfFileEzdxfDrawings.R2018, "40") })
            {
                DxfArc arc = ByHandle<DxfArc>(DxfDatabase.Parse(text), handle);

                Near(new GeoPoint3(-37.589873214007014, -8.09770710302035, 17.928429140015904), arc.Center, 1E-12);
                Near(new GeoVector3(0.2672612419124244, 0.5345224838248488, 0.8017837257372732), arc.Normal, 1E-15);
                Assert.Equal(12.0, arc.Radius);
                Assert.Equal(0.2617993877991494, arc.StartAngle, 15);
                Assert.Equal(3.490658503988659, arc.EndAngle, 15);
                Assert.Equal(3.490658503988659 - 0.2617993877991494, arc.TotalAngle, 14);
                Assert.Equal(12.0 * arc.TotalAngle, arc.Length);
                Assert.Equal(arc.StartAngle, arc.StartParam);
                Assert.Equal(arc.EndAngle, arc.EndParam);
                Assert.False(arc.Closed);
                Near(new GeoPoint3(-49.070929634720784, -5.141310238787746, 19.784516704098756), arc.StartPoint, 1E-12);
                Near(new GeoPoint3(-26.03238150247499, -10.197322616342964, 15.47567557838697), arc.EndPoint, 1E-12);
            }
        }

        [Fact]
        public void TheShapeOfAnArc_IsTheReadersArc_ToTheBit_AndItsEndsAreTheArcsEnds()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tol });
            DxfArc arc = ByHandle<DxfArc>(db, "40");

            var shape = (GeoArc3)Assert.Single(arc.GetGeometry());
            var theirs = (GeoArc3)read.Shapes.Single(s => s.Handle == "40").Shape;
            Assert.Equal(Bits(theirs.Center), Bits(shape.Center));
            Assert.Equal(Bits(theirs.StartPoint), Bits(shape.StartPoint));
            Assert.Equal(Bits(theirs.EndPoint), Bits(shape.EndPoint));
            Assert.True(arc.StartPoint.IsEqualTo(shape.StartPoint, new Tolerance(1E-12, 1E-12)));
            Assert.True(arc.EndPoint.IsEqualTo(shape.EndPoint, new Tolerance(1E-12, 1E-12)));
            Assert.Equal(shape.Length, arc.Length, 12);
        }

        [Fact]
        public void AnArc_IsWalkedByItsAngle_AndRunsOnPastATurnWhereItsEndIsBelowItsStart()
        {
            var near = new Tolerance(1E-12, 1E-12);
            var arc = new DxfArc(new GeoPoint3(0, 0, 0), 10, 1.5 * Math.PI, 0.5 * Math.PI);

            Assert.Equal(1.5 * Math.PI, arc.StartParam, 15);
            Assert.Equal(2.5 * Math.PI, arc.EndParam, 14);
            Assert.Equal(Math.PI, arc.TotalAngle, 14);
            Assert.True(arc.GetPointAtParameter(2.0 * Math.PI).IsEqualTo(new GeoPoint3(10, 0, 0), near));
            Assert.Equal(2.0 * Math.PI, arc.GetParameterAtPoint(new GeoPoint3(10, 0, 0)), 14);
            Assert.Equal(5.0 * Math.PI, arc.GetDistAtPoint(new GeoPoint3(10, 0, 0)), 12);
            Assert.True(arc.GetPointAtDist(5.0 * Math.PI).IsEqualTo(new GeoPoint3(10, 0, 0), near));
            Assert.Equal(0.5 * 100.0 * Math.PI, arc.Area, 11);
            Assert.True(arc.GetClosestPointTo(new GeoPoint3(-20, 1, 0), false).IsEqualTo(new GeoPoint3(0, 10, 0), near));
            Assert.True(arc.GetClosestPointTo(new GeoPoint3(-20, 0, 0), true).IsEqualTo(new GeoPoint3(-10, 0, 0), near));
            Assert.Throws<ArgumentException>(() => arc.GetParameterAtPoint(new GeoPoint3(-10, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => arc.GetPointAtParameter(Math.PI));

            IReadOnlyList<DxfCurve> pieces = arc.GetSplitCurves(new[] { 2.0 * Math.PI });
            Assert.Equal(2, pieces.Count);
            Assert.Equal(0.0, ((DxfArc)pieces[1]).StartAngle, 14);

            arc.Extend(2.75 * Math.PI);
            Assert.Equal(0.75 * Math.PI, arc.EndAngle, 14);
            Assert.Throws<ArgumentOutOfRangeException>(() => arc.Extend(2.0 * Math.PI));
            Assert.Throws<ArgumentOutOfRangeException>(() => arc.Extend(4.0 * Math.PI));

            GeoPoint3 start = arc.StartPoint;
            GeoPoint3 end = arc.EndPoint;
            arc.ReverseCurve();
            Assert.Equal(Bits(new GeoVector3(0, 0, -1)), Bits(arc.Normal));
            Assert.True(arc.StartPoint.IsEqualTo(end, near));
            Assert.True(arc.EndPoint.IsEqualTo(start, near));
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach().ToString();

            foreach (string text in Drawings.Concat(new[] { writer }))
            {
                SettingWhatItHasChangesNothing<DxfArc>(text, arc =>
                {
                    arc.Center = arc.Center;
                    arc.Radius = arc.Radius;
                    arc.Normal = arc.Normal;
                    arc.Thickness = arc.Thickness;
                    arc.StartAngle = arc.StartAngle;
                    arc.EndAngle = arc.EndAngle;
                    arc.Layer = arc.Layer;
                    arc.XData = arc.XData;
                    arc.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void AnArcGivenNewAngles_IsWrittenInDegrees_AndOnlyThatRecordChanges()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfArc arc = ByHandle<DxfArc>(db, "40");

            arc.StartAngle = 0.75;
            arc.EndAngle = 5.0;
            arc.Radius = 12.5;
            arc.Center = new GeoPoint3(1.0, 2.0, -3.0);

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "40");
            DxfArc again = ByHandle<DxfArc>(DxfDatabase.Parse(saved), "40");
            Assert.Equal(0.75, again.StartAngle, 15);
            Assert.Equal(5.0, again.EndAngle, 15);
            Assert.Equal(12.5, again.Radius);
            Assert.True(again.Center.IsEqualTo(new GeoPoint3(1.0, 2.0, -3.0), new Tolerance(1E-12, 1E-12)));

            // An angle set past a turn is written from nought up to 360 degrees.
            again.EndAngle = 2.0 * Math.PI + 1.0;
            Assert.Equal(1.0, again.EndAngle, 14);
        }

        [Fact]
        public void AnArcMadeInCode_IsAppendedToEachVersion_AndReadBack()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var arc = new DxfArc(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 3.0, 0.5, 2.5) { Layer = "rebar" };

                db.ModelSpace.AppendEntity(arc);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, arc.Handle, db.LayerTable["rebar"].Handle, LayerTableHandle(text));
                DxfArc again = ByHandle<DxfArc>(DxfDatabase.Parse(saved), arc.Handle);
                Assert.Equal(3.0, again.Radius);
                Assert.Equal(0.5, again.StartAngle, 15);
                Assert.Equal(2.5, again.EndAngle, 15);
            }
        }

        [Fact]
        public void AnArcErased_TakesItsRecordOut_AndRefusesASetter()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            DxfArc arc = ByHandle<DxfArc>(db, "43");

            arc.Erase();

            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "43");
            Assert.Throws<InvalidOperationException>(() => arc.StartAngle = 1.0);
        }

        [Fact]
        public void AnArcMovedOrMirrored_IsWhereEzdxfMovesIt()
        {
            // ezdxf 1.4.5 Arc.transform on the oracle's turn: centre (-8.39403305092514, -33.696328646153326, 27.762230114410592),
            // radius 12.000000000000002, from (-18.06313549824982, -37.68495506177682, 33.64434854060114) to
            // (0.6855215940005207, -28.91018848112107, 21.544951789413876).
            DxfArc arc = ByHandle<DxfArc>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "40");
            arc.TransformBy(Turn);
            Near(new GeoPoint3(-8.39403305092514, -33.696328646153326, 27.762230114410592), arc.Center, 1E-9);
            Near(new GeoVector3(0.26726124191242445, 0.5345224838248488, 0.8017837257372733), arc.Normal, 1E-9);
            Assert.Equal(12.000000000000002, arc.Radius, 12);
            Near(new GeoPoint3(-18.06313549824982, -37.68495506177682, 33.64434854060114), arc.StartPoint, 1E-9);
            Near(new GeoPoint3(0.6855215940005207, -28.91018848112107, 21.544951789413876), arc.EndPoint, 1E-9);

            // Mirrored in x the arc turns about: ezdxf swaps its ends, from (52.07092963472077, -5.141310238787757,
            // 19.784516704098756) to (29.03238150247498, -10.197322616342973, 15.475675578386973).
            DxfArc mirrored = ByHandle<DxfArc>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "40");
            mirrored.TransformBy(Mirror);
            Near(new GeoPoint3(40.58987321400701, -8.097707103020358, 17.928429140015904), mirrored.Center, 1E-9);
            Near(new GeoVector3(0.2672612419124244, -0.5345224838248488, -0.8017837257372733), mirrored.Normal, 1E-9);
            Near(new GeoPoint3(52.07092963472077, -5.141310238787757, 19.784516704098756), mirrored.StartPoint, 1E-9);
            Near(new GeoPoint3(29.03238150247498, -10.197322616342973, 15.475675578386973), mirrored.EndPoint, 1E-9);
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual, double relative)
            => Assert.True(actual.DistanceTo(expected) <= relative * Math.Max(1.0, Math.Abs(expected.X) + Math.Abs(expected.Y) + Math.Abs(expected.Z)), actual + " is not " + expected);

        private static void Near(GeoVector3 expected, GeoVector3 actual, double absolute)
            => Assert.True(actual.Subtract(expected).Length <= absolute, actual + " is not " + expected);

        private static string LayerTableHandle(string text)
            => HandleOf(Records(text).First(r => r.StartsWith("  0\nTABLE\n  2\nLAYER\n", StringComparison.Ordinal)));
    }
}
