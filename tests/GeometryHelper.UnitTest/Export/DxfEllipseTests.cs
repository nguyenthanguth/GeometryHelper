using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The ELLIPSE as a <see cref="DxfEllipse"/>: its parameters as the file holds them and its angles as AutoCAD gives them,
    /// set and read back, made in code and appended (refused in R12), erased, walked along, and moved, sheared and mirrored as
    /// ezdxf moves one.
    /// </summary>
    public class DxfEllipseTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        private static readonly GeoTransform3 Turn = new GeoTransform3(new[,]
        {
            { 0.7816391739070251, -0.4829292842142122, 0.3947397981737998, 10.0 },
            { 0.5501172307043584, 0.8320301337746346, -0.07139249941787586, -5.0 },
            { -0.29395787843858057, 0.27295633888831433, 0.9160150668873173, 2.5 },
            { 0.0, 0.0, 0.0, 1.0 },
        });

        private static readonly GeoTransform3 Shear = new GeoTransform3(new[,]
        {
            { 0.9210609940028851, -0.3894183423086505, 0.0, 1.0 },
            { 0.778836684617301, 1.8421219880057702, 0.0, 2.0 },
            { 0.0, 0.0, 1.0, 3.0 },
            { 0.0, 0.0, 0.0, 1.0 },
        });

        private static readonly GeoTransform3 Mirror = new GeoTransform3(new double[,] { { -1, 0, 0, 3 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });

        [Fact]
        public void APartOfAnEllipse_IsReadAsEzdxfReadsIt_ItsAnglesNotItsParameters()
        {
            // ezdxf 1.4.5: ELLIPSE 6D of R2000, major axis (10, 0, 0), ratio 0.5, parameters 0.3 (0x1.3333333333333p-2) to 4.0;
            // minor axis (0, 5, 0), from (9.55336489125606, 1.4776010333066978, 0) to (-6.536436208636119, -3.7840124765396412, 0).
            DxfEllipse ellipse = ByHandle<DxfEllipse>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "6D");

            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(ellipse.Center));
            Assert.Equal(Bits(new GeoVector3(10, 0, 0)), Bits(ellipse.MajorAxis));
            Assert.Equal(Bits(new GeoVector3(0, 5, 0)), Bits(ellipse.MinorAxis));
            Assert.Equal(10.0, ellipse.MajorRadius);
            Assert.Equal(5.0, ellipse.MinorRadius);
            Assert.Equal(0.5, ellipse.RadiusRatio);
            Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(ellipse.Normal));
            Assert.Equal(Bits(FromBits(0x3FD3333333333333)), Bits(ellipse.StartParam));
            Assert.Equal(4.0, ellipse.EndParam);
            Assert.Equal(Math.Atan2(0.5 * Math.Sin(0.3), Math.Cos(0.3)), ellipse.StartAngle);
            Assert.Equal(Math.Atan2(0.5 * Math.Sin(4.0), Math.Cos(4.0)) + 2.0 * Math.PI, ellipse.EndAngle);
            Assert.False(ellipse.Closed);
            Assert.False(ellipse.IsNull);
            Near(new GeoPoint3(9.55336489125606, 1.4776010333066978, 0.0), ellipse.StartPoint, 1E-15);
            Near(new GeoPoint3(-6.536436208636119, -3.7840124765396412, 0.0), ellipse.EndPoint, 1E-15);
            Assert.Equal(0.5 * 10.0 * 5.0 * (3.7 - Math.Sin(3.7)), ellipse.Area, 12);

            // A whole one ends at the angle 2 pi, as AutoCAD's EndAngle gives it.
            DxfEllipse whole = ByHandle<DxfEllipse>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "6E");
            Assert.True(whole.Closed);
            Assert.True(whole.IsPeriodic);
            Assert.Equal(2.0 * Math.PI, whole.EndAngle);
            Assert.Equal(Math.PI * 8.0 * 2.0, whole.Area, 12);
        }

        [Fact]
        public void TheShapeOfAnEllipse_IsTheReadersShape_AndItsLengthTheShapesLength()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tol });

            DxfEllipse part = ByHandle<DxfEllipse>(db, "6D");
            var spline = (GeoSpline3)Assert.Single(part.GetGeometry());
            Assert.Same(read.Shapes.Single(s => s.Handle == "6D").Shape.GetType(), spline.GetType());
            Assert.Equal(spline.Length, part.GetDistanceAtParameter(part.EndParam), 9);

            DxfEllipse whole = ByHandle<DxfEllipse>(db, "6E");
            var ellipse = (GeoEllipse3)Assert.Single(whole.GetGeometry());
            var theirs = (GeoEllipse3)read.Shapes.Single(s => s.Handle == "6E").Shape;
            Assert.Equal(Bits(theirs.Center), Bits(ellipse.Center));
            Assert.Equal(Bits(theirs.MajorRadius), Bits(ellipse.MajorRadius));
            Assert.Equal(ellipse.Length, whole.GetDistanceAtParameter(whole.EndParam), 11);
        }

        [Fact]
        public void AnEllipse_IsWalkedByItsParameter_AndItsDistancesGoBackToTheirPoints()
        {
            var ellipse = new DxfEllipse(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), new GeoVector3(10, 0, 0), 0.5, 0.0, 2.0 * Math.PI);
            var near = new Tolerance(1E-12, 1E-12);

            Assert.True(ellipse.GetPointAtParameter(Math.PI / 2).IsEqualTo(new GeoPoint3(0, 5, 0), near));
            Assert.Equal(Math.PI / 4, ellipse.GetParameterAtPoint(new GeoPoint3(10 * Math.Cos(Math.PI / 4), 5 * Math.Sin(Math.PI / 4), 0)), 14);
            Assert.Equal(Math.Atan2(0.5, 1.0), ellipse.GetAngleAtParameter(Math.PI / 4), 15);
            Assert.Equal(Math.PI / 4, ellipse.GetParameterAtAngle(Math.Atan2(0.5, 1.0)), 15);

            foreach (double t in new[] { 0.2, 1.0, 2.0, 3.5, 5.9 })
            {
                double distance = ellipse.GetDistanceAtParameter(t);
                Assert.Equal(t, ellipse.GetParameterAtDistance(distance), 12);
                Assert.Equal(distance, ellipse.GetDistAtPoint(ellipse.GetPointAtParameter(t)), 9);
            }

            Assert.True(ellipse.GetFirstDerivative(0.0).IsEqualTo(new GeoVector3(0, 5, 0), near));
            Assert.True(ellipse.GetSecondDerivative(0.0).IsEqualTo(new GeoVector3(-10, 0, 0), near));
            Assert.True(ellipse.GetClosestPointTo(new GeoPoint3(20, 0, 3), false).IsEqualTo(new GeoPoint3(10, 0, 0), near));
            Assert.Throws<NotSupportedException>(() => ellipse.GetOffsetCurves(1.0));
            Assert.Throws<NotSupportedException>(() => ellipse.Extend(7.0));

            IReadOnlyList<DxfCurve> pieces = ellipse.GetSplitCurves(new[] { 1.0, 4.0 });
            Assert.Equal(2, pieces.Count);
            Assert.Equal(1.0, ((DxfEllipse)pieces[0]).StartParam);
            Assert.Equal(4.0, ((DxfEllipse)pieces[0]).EndParam);
            Assert.Equal(1.0 + 2.0 * Math.PI, ((DxfEllipse)pieces[1]).EndParam);

            var part = (DxfEllipse)pieces[0];
            Assert.True(part.GetClosestPointTo(new GeoPoint3(20, 0, 0), false).IsEqualTo(part.StartPoint, near));
            part.Extend(0.5);
            Assert.Equal(0.5, part.StartParam);

            GeoPoint3 start = part.StartPoint;
            GeoPoint3 end = part.EndPoint;
            part.ReverseCurve();
            Assert.True(part.StartPoint.IsEqualTo(end, near));
            Assert.True(part.EndPoint.IsEqualTo(start, near));
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach().ToString();

            foreach (string text in Drawings.Concat(new[] { writer }))
            {
                SettingWhatItHasChangesNothing<DxfEllipse>(text, ellipse =>
                {
                    ellipse.Center = ellipse.Center;
                    ellipse.RadiusRatio = ellipse.RadiusRatio;
                    ellipse.StartParam = ellipse.StartParam;
                    ellipse.EndParam = ellipse.EndParam;
                    ellipse.StartAngle = ellipse.StartAngle;
                    ellipse.EndAngle = ellipse.EndAngle;
                    ellipse.Layer = ellipse.Layer;
                    ellipse.XData = ellipse.XData;
                    ellipse.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void AnEllipseGivenAnAngle_IsWrittenTheParameterAtThatAngle_AndOnlyThatRecordChanges()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfEllipse ellipse = ByHandle<DxfEllipse>(db, "6D");

            ellipse.Center = new GeoPoint3(6.0, 5.0, 4.0);
            ellipse.RadiusRatio = 0.35;
            ellipse.StartAngle = 0.6;
            ellipse.EndAngle = 4.4;

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "6D");
            DxfEllipse again = ByHandle<DxfEllipse>(DxfDatabase.Parse(saved), "6D");
            Assert.Equal(Bits(new GeoPoint3(6.0, 5.0, 4.0)), Bits(again.Center));
            Assert.Equal(0.35, again.RadiusRatio);
            Assert.Equal(0.6, again.StartAngle, 14);
            Assert.Equal(4.4, again.EndAngle, 14);

            // The parameters are what ezdxf's oracle writes: atan2(sin a, ratio cos a), from nought up to 2 pi.
            Assert.Equal(Math.Atan2(Math.Sin(0.6), 0.35 * Math.Cos(0.6)), again.StartParam);

            again.Set(new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 1), new GeoVector3(0, 4, 0), 0.25, 0.0, 2.0 * Math.PI);
            Assert.Equal(Bits(new GeoVector3(0, 4, 0)), Bits(again.MajorAxis));
            Assert.True(again.Closed);
            Assert.Throws<ArgumentException>(() => again.Set(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), new GeoVector3(0, 0, 4), 0.5, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => again.RadiusRatio = 1.5);
        }

        [Fact]
        public void AnEllipseMadeInCode_IsAppendedFromR2000On_AndRefusedByR12()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var ellipse = new DxfEllipse(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), new GeoVector3(4, 0, 0), 0.5, 0.0, 2.0 * Math.PI);

                db.ModelSpace.AppendEntity(ellipse);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, ellipse.Handle);
                DxfEllipse again = ByHandle<DxfEllipse>(DxfDatabase.Parse(saved), ellipse.Handle);
                Assert.Equal(Bits(new GeoVector3(4, 0, 0)), Bits(again.MajorAxis));
                Assert.Equal(0.5, again.RadiusRatio);
                Assert.True(again.Closed);
            }

            DxfDatabase r12 = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            Assert.Throws<ArgumentException>(() => r12.ModelSpace.AppendEntity(new DxfEllipse()));
        }

        [Fact]
        public void AnEllipseErased_TakesItsRecordOut_AndRefusesASetter()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfEllipse ellipse = ByHandle<DxfEllipse>(db, "6E");

            ellipse.Erase();

            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "6E");
            Assert.Throws<InvalidOperationException>(() => ellipse.RadiusRatio = 0.5);
        }

        [Fact]
        public void AnEllipseMovedShearedOrMirrored_IsWhereEzdxfMovesIt()
        {
            // ezdxf 1.4.5 Ellipse.transform of 6D: turned, from (16.75370743226478, 1.4848792232937376, 0.09503369300573444) to
            // (6.718275838338634, -11.744218592830762, 3.388566728535829), ratio 0.5000000000000001; sheared, made square again
            // by Rytz's construction, ratio 0.5620713290497522, normal (0, 0, -1), from (-3.546892565673515, -10.061428891868477, 3)
            // to (9.22382681782873, 12.162432391799506, 3); mirrored, from (-6.553364891256059, 1.4776010333066978, 0) to
            // (9.536436208636118, -3.7840124765396412, 0).
            DxfEllipse turned = ByHandle<DxfEllipse>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "6D");
            turned.TransformBy(Turn);
            Near(new GeoPoint3(10.0, -5.0, 2.5), turned.Center, 1E-9);
            Near(new GeoVector3(0.3947397981737998, -0.07139249941787584, 0.9160150668873174), turned.Normal);
            Assert.Equal(0.5000000000000001, turned.RadiusRatio, 12);
            Near(new GeoPoint3(16.75370743226478, 1.4848792232937376, 0.09503369300573444), turned.StartPoint, 1E-9);
            Near(new GeoPoint3(6.718275838338634, -11.744218592830762, 3.388566728535829), turned.EndPoint, 1E-9);

            DxfEllipse sheared = ByHandle<DxfEllipse>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "6D");
            sheared.TransformBy(Shear);
            Near(new GeoPoint3(1.0, 2.0, 3.0), sheared.Center, 1E-9);
            Near(new GeoVector3(0.0, 0.0, -1.0), sheared.Normal);
            Assert.Equal(0.5620713290497522, sheared.RadiusRatio, 12);
            Assert.Equal(new GeoVector3(6.8841473354701685, 11.424617182076942, 0.0).Length, sheared.MajorRadius, 9);
            Near(new GeoPoint3(-3.546892565673515, -10.061428891868477, 3.0), sheared.StartPoint, 1E-9);
            Near(new GeoPoint3(9.22382681782873, 12.162432391799506, 3.0), sheared.EndPoint, 1E-9);

            DxfEllipse mirrored = ByHandle<DxfEllipse>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "6D");
            mirrored.TransformBy(Mirror);
            Near(new GeoVector3(0.0, 0.0, -1.0), mirrored.Normal);
            Near(new GeoPoint3(-6.553364891256059, 1.4776010333066978, 0.0), mirrored.StartPoint, 1E-9);
            Near(new GeoPoint3(9.536436208636118, -3.7840124765396412, 0.0), mirrored.EndPoint, 1E-9);
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual, double relative)
            => Assert.True(actual.DistanceTo(expected) <= relative * Math.Max(1.0, Math.Abs(expected.X) + Math.Abs(expected.Y) + Math.Abs(expected.Z)), actual + " is not " + expected);

        private static void Near(GeoVector3 expected, GeoVector3 actual)
            => Assert.True(actual.Subtract(expected).Length <= 1E-9, actual + " is not " + expected);
    }
}
