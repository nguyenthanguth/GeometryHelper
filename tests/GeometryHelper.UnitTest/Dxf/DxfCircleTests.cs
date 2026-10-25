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
    /// The CIRCLE as a <see cref="DxfCircle"/>: read as ezdxf 1.4.5 reads it, its centre taken from the system of its normal into
    /// the world, set and read back, made in code and appended, erased, walked by its angle, and moved as ezdxf moves one.
    /// </summary>
    public class DxfCircleTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        // The transformation the oracle moves entities by: a turn about a tilted axis and a shift.
        private static readonly GeoTransform3 Turn = new GeoTransform3(new[,]
        {
            { 0.7816391739070251, -0.4829292842142122, 0.3947397981737998, 10.0 },
            { 0.5501172307043584, 0.8320301337746346, -0.07139249941787586, -5.0 },
            { -0.29395787843858057, 0.27295633888831433, 0.9160150668873173, 2.5 },
            { 0.0, 0.0, 0.0, 1.0 },
        });

        // A shear, which scales a plane more one way than the other.
        private static readonly GeoTransform3 Shear = new GeoTransform3(new[,]
        {
            { 0.9210609940028851, -0.3894183423086505, 0.0, 1.0 },
            { 0.778836684617301, 1.8421219880057702, 0.0, 2.0 },
            { 0.0, 0.0, 1.0, 3.0 },
            { 0.0, 0.0, 0.0, 1.0 },
        });

        [Fact]
        public void ACircleOnTheNormalDown_IsReadAsEzdxfReadsIt_ItsCentreInTheWorld()
        {
            // ezdxf 1.4.5: CIRCLE 3F of R2000 (42 of R12), groups 10/20/30 (10, 20, 5) on the extrusion (0, 0, -1), is about
            // OCS.to_wcs = (-10, 20, -5), radius 7.5 (0x1.e000000000000p+2).
            foreach (var (text, handle) in new[] { (DxfFileEzdxfDrawings.R12, "42"), (DxfFileEzdxfDrawings.R2000, "3F"), (DxfFileEzdxfDrawings.R2018, "3F") })
            {
                DxfCircle circle = ByHandle<DxfCircle>(DxfDatabase.Parse(text), handle);

                Assert.Equal(Bits(new GeoPoint3(-10.0, 20.0, -5.0)), Bits(circle.Center));
                Assert.Equal(Bits(new GeoVector3(0.0, 0.0, -1.0)), Bits(circle.Normal));
                Assert.Equal(7.5, circle.Radius);
                Assert.Equal(15.0, circle.Diameter);
                Assert.Equal(2.0 * Math.PI * 7.5, circle.Circumference);
                Assert.Equal(Math.PI * 7.5 * 7.5, circle.Area);
                Assert.Equal(0.0, circle.Thickness);
                Assert.True(circle.Closed);
                Assert.True(circle.IsPeriodic);
                Assert.Equal(0.0, circle.StartParam);
                Assert.Equal(2.0 * Math.PI, circle.EndParam);

                // The x axis of the system of (0, 0, -1) is the world's -x: the circle starts at (-17.5, 20, -5).
                Assert.True(circle.StartPoint.IsEqualTo(new GeoPoint3(-17.5, 20.0, -5.0), new Tolerance(1E-12, 1E-12)));
            }
        }

        [Fact]
        public void TheShapeOfACircle_IsTheReadersCircle_ToTheBit()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tol });

            foreach (DxfCircle circle in db.ModelSpace.OfType<DxfCircle>())
            {
                var shape = (GeoCircle3)Assert.Single(circle.GetGeometry());
                var theirs = (GeoCircle3)read.Shapes.Single(s => s.Handle == circle.Handle).Shape;
                Assert.Equal(Bits(theirs.Center), Bits(shape.Center));
                Assert.Equal(Bits(theirs.Normal), Bits(shape.Normal));
                Assert.Equal(Bits(theirs.Radius), Bits(shape.Radius));
                Assert.Equal(Bits(theirs.Center), Bits(circle.Center));
                Assert.Equal(shape, circle.GetGeCurve());
            }
        }

        [Fact]
        public void ACircle_IsWalkedByItsAngle_AsAutoCADWalksOne()
        {
            var circle = new DxfCircle(new GeoPoint3(10, 20, 0), new GeoVector3(0, 0, 1), 5);
            var near = new Tolerance(1E-12, 1E-12);

            Assert.True(circle.GetPointAtParameter(Math.PI / 2).IsEqualTo(new GeoPoint3(10, 25, 0), near));
            Assert.Equal(Math.PI / 2, circle.GetParameterAtPoint(new GeoPoint3(10, 25, 0)), 15);
            Assert.Equal(5 * Math.PI / 2, circle.GetDistanceAtParameter(Math.PI / 2), 12);
            Assert.Equal(Math.PI, circle.GetParameterAtDistance(5 * Math.PI), 15);
            Assert.True(circle.GetPointAtDist(5 * Math.PI).IsEqualTo(new GeoPoint3(5, 20, 0), near));
            Assert.Equal(5 * Math.PI / 2, circle.GetDistAtPoint(new GeoPoint3(10, 25, 0)), 12);
            Assert.True(circle.GetFirstDerivative(0.0).IsEqualTo(new GeoVector3(0, 5, 0), near));
            Assert.True(circle.GetSecondDerivative(0.0).IsEqualTo(new GeoVector3(-5, 0, 0), near));
            Assert.True(circle.GetClosestPointTo(new GeoPoint3(30, 20, 7), false).IsEqualTo(new GeoPoint3(15, 20, 0), near));
            Assert.Throws<ArgumentException>(() => circle.GetParameterAtPoint(new GeoPoint3(10, 20, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => circle.GetPointAtParameter(7.0));
            Assert.Throws<NotSupportedException>(() => circle.Extend(1.0));

            var outer = (DxfCircle)Assert.Single(circle.GetOffsetCurves(2.5));
            Assert.Equal(7.5, outer.Radius);
            Assert.Throws<ArgumentOutOfRangeException>(() => circle.GetOffsetCurves(-5.0));

            IReadOnlyList<DxfCurve> pieces = circle.GetSplitCurves(new[] { Math.PI, 0.5 });
            Assert.Equal(2, pieces.Count);
            var first = (DxfArc)pieces[0];
            Assert.Equal(0.5, first.StartAngle, 12);
            Assert.Equal(Math.PI, first.EndAngle, 12);
            Assert.Equal(0.5, ((DxfArc)pieces[1]).EndAngle, 12);
            Assert.IsType<DxfCircle>(Assert.Single(circle.GetSplitCurves(new[] { 1.0 })));
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach().ToString();

            foreach (string text in Drawings.Concat(new[] { writer }))
            {
                SettingWhatItHasChangesNothing<DxfCircle>(text, circle =>
                {
                    circle.Center = circle.Center;
                    circle.Radius = circle.Radius;
                    circle.Normal = circle.Normal;
                    circle.Thickness = circle.Thickness;
                    circle.Diameter = circle.Diameter;
                    circle.Circumference = circle.Circumference;
                    circle.Layer = circle.Layer;
                    circle.ColorIndex = circle.ColorIndex;
                    circle.Linetype = circle.Linetype;
                    circle.Visible = circle.Visible;
                    circle.XData = circle.XData;
                    circle.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void ACircleGivenANewCentreRadiusAndNormal_IsThatRecordWrittenAnew_TheCentreKeptInTheWorld()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfCircle circle = ByHandle<DxfCircle>(db, "3F");
            var centre = new GeoPoint3(12.5, -7.25, 3.0);
            var normal = new GeoVector3(0.6, 0.0, 0.8);

            circle.Center = centre;
            circle.Radius = 9.75;
            circle.Normal = normal;
            circle.Thickness = 0.5;

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "3F");
            DxfCircle again = ByHandle<DxfCircle>(DxfDatabase.Parse(saved), "3F");
            Assert.True(again.Center.IsEqualTo(centre, new Tolerance(1E-12, 1E-12)));
            Assert.Equal(9.75, again.Radius);
            Assert.Equal(Bits(normal), Bits(again.Normal));
            Assert.Equal(0.5, again.Thickness);

            again.Diameter = 20.5;
            Assert.Equal(10.25, again.Radius);
            again.Circumference = 2.0 * Math.PI;
            Assert.Equal(1.0, again.Radius);
            Assert.Throws<ArgumentOutOfRangeException>(() => again.Radius = -1.0);
            Assert.Throws<ArgumentException>(() => again.Normal = new GeoVector3(0, 0, 0));
        }

        [Fact]
        public void ACircleMadeInCode_IsAppendedToEachVersion_AndReadBack()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var circle = new DxfCircle(new GeoPoint3(1, 1, 1), new GeoVector3(0.6, 0, 0.8), 1.25) { Layer = "rebar", ColorIndex = 3 };

                Assert.Null(circle.Handle);
                db.ModelSpace.AppendEntity(circle);
                Assert.Same(db.ModelSpace, circle.Owner);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, circle.Handle, db.LayerTable["rebar"].Handle, LayerTableHandle(text));
                DxfCircle again = ByHandle<DxfCircle>(DxfDatabase.Parse(saved), circle.Handle);
                Assert.True(again.Center.IsEqualTo(new GeoPoint3(1, 1, 1), new Tolerance(1E-12, 1E-12)));
                Assert.Equal(Bits(new GeoVector3(0.6, 0, 0.8)), Bits(again.Normal));
                Assert.Equal(1.25, again.Radius);
                Assert.Equal("rebar", again.Layer);
                Assert.Equal(3, again.ColorIndex);
            }
        }

        [Fact]
        public void ACircleErased_TakesItsRecordOut_AndRefusesASetter()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfCircle circle = ByHandle<DxfCircle>(db, "3F");

            circle.Erase();

            Assert.True(circle.IsErased);
            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "3F");
            Assert.Throws<InvalidOperationException>(() => circle.Radius = 2.0);
        }

        [Fact]
        public void ACircleMoved_IsWhereEzdxfMovesIt_AndAShearIsRefused()
        {
            // ezdxf 1.4.5 Circle.transform on the oracle's turn: centre (-9.448676414223495, 6.496392865538491,
            // 6.318630227715507), extrusion (-0.3947397981737998, 0.0713924994178759, -0.9160150668873175), radius 7.500000000000001.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfCircle circle = ByHandle<DxfCircle>(db, "3F");

            circle.TransformBy(Turn);

            Near(new GeoPoint3(-9.448676414223495, 6.496392865538491, 6.318630227715507), circle.Center);
            Near(new GeoVector3(-0.3947397981737998, 0.0713924994178759, -0.9160150668873175), circle.Normal);
            Assert.Equal(7.500000000000001, circle.Radius, 12);
            Assert.Throws<ArgumentException>(() => circle.TransformBy(Shear));

            // A mirror in x: the normal comes up, the centre (13, 20, -5).
            DxfCircle mirrored = ByHandle<DxfCircle>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "3F");
            mirrored.TransformBy(new GeoTransform3(new double[,] { { -1, 0, 0, 3 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }));
            Near(new GeoPoint3(13.0, 20.0, -5.0), mirrored.Center);
            Near(new GeoVector3(0.0, 0.0, 1.0), mirrored.Normal);
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
            => Assert.True(actual.DistanceTo(expected) <= 1E-9 * Math.Max(1.0, Math.Abs(expected.X) + Math.Abs(expected.Y) + Math.Abs(expected.Z)), actual + " is not " + expected);

        private static void Near(GeoVector3 expected, GeoVector3 actual)
            => Assert.True(actual.Subtract(expected).Length <= 1E-9, actual + " is not " + expected);

        private static string LayerTableHandle(string text)
            => HandleOf(Records(text).First(r => r.StartsWith("  0\nTABLE\n  2\nLAYER\n", StringComparison.Ordinal)));
    }
}
