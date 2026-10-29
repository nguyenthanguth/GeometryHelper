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
    /// The RAY as a <see cref="DxfRay"/>: read as ezdxf 1.4.5 reads it, set and read back, made in code and appended (refused in
    /// R12), erased, walked by the distance from its base point, split and moved as ezdxf moves one.
    /// </summary>
    public class DxfRayTests
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

        [Fact]
        public void TheRayOfEzdxf_IsReadAsEzdxfReadsIt_ToTheBit()
        {
            // ezdxf 1.4.5: RAY 71 from (1, 1, 0) along (0.7071067811865475, 0.7071067811865475, 0) (0x1.6a09e667f3bccp-1).
            foreach (string text in Drawings)
            {
                DxfRay ray = ByHandle<DxfRay>(DxfDatabase.Parse(text), "71");
                var unit = new GeoVector3(FromBits(0x3FE6A09E667F3BCC), FromBits(0x3FE6A09E667F3BCC), 0.0);

                Assert.Equal(Bits(new GeoPoint3(1, 1, 0)), Bits(ray.BasePoint));
                Assert.Equal(Bits(unit), Bits(ray.UnitDir));
                Assert.Equal(Bits(new GeoPoint3(1, 1, 0).Add(unit)), Bits(ray.SecondPoint));
                Assert.Equal(Bits(ray.BasePoint), Bits(ray.StartPoint));
                Assert.Equal(0.0, ray.StartParam);
                Assert.Equal(double.PositiveInfinity, ray.EndParam);
                Assert.False(ray.Closed);
                Assert.Equal(0.0, ray.Area);
                Assert.Throws<NotSupportedException>(() => ray.EndPoint);
            }
        }

        [Fact]
        public void TheShapeOfARay_IsTheReadersRay_ToTheBit()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tol });
            DxfRay ray = ByHandle<DxfRay>(db, "71");

            var shape = (GeoRay3)Assert.Single(ray.GetGeometry());
            var theirs = (GeoRay3)read.Shapes.Single(s => s.Handle == "71").Shape;
            Assert.Equal(Bits(theirs.Origin), Bits(shape.Origin));
            Assert.Equal(Bits(theirs.Direction), Bits(shape.Direction));
        }

        [Fact]
        public void ARay_IsWalkedByTheDistanceFromItsBasePoint_AndSplitIntoLinesAndARay()
        {
            DxfRay ray = new DxfRay { BasePoint = new GeoPoint3(1, 2, 3), UnitDir = new GeoVector3(0, 0, 2) };

            Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(ray.UnitDir));
            Assert.Equal(Bits(new GeoPoint3(1, 2, 43)), Bits(ray.GetPointAtParameter(40.0)));
            Assert.Equal(40.0, ray.GetParameterAtPoint(new GeoPoint3(1, 2, 43)));
            Assert.Equal(40.0, ray.GetDistAtPoint(new GeoPoint3(1, 2, 43)));
            Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(ray.GetFirstDerivative(2.5)));
            Assert.Equal(Bits(new GeoPoint3(1, 2, 3)), Bits(ray.GetClosestPointTo(new GeoPoint3(5, 5, -10), false)));
            Assert.Equal(Bits(new GeoPoint3(1, 2, -10)), Bits(ray.GetClosestPointTo(new GeoPoint3(5, 5, -10), true)));
            Assert.Throws<ArgumentException>(() => ray.GetParameterAtPoint(new GeoPoint3(1, 2, -10)));
            Assert.Throws<ArgumentOutOfRangeException>(() => ray.GetPointAtParameter(-1.0));
            Assert.Throws<NotSupportedException>(() => ray.ReverseCurve());

            IReadOnlyList<DxfCurve> pieces = ray.GetSplitCurves(new[] { 10.0, 5.0 });
            Assert.Equal(3, pieces.Count);
            Assert.Equal(Bits(new GeoPoint3(1, 2, 8)), Bits(((DxfLine)pieces[0]).EndPoint));
            Assert.Equal(Bits(new GeoPoint3(1, 2, 13)), Bits(((DxfLine)pieces[1]).EndPoint));
            Assert.Equal(Bits(new GeoPoint3(1, 2, 13)), Bits(((DxfRay)pieces[2]).BasePoint));

            // To the left of (0, 0, 1) seen from the tip of (1, 0, 0), as a line is (AutoCAD 2021's side, check 11): -y.
            var moved = (DxfRay)Assert.Single(ray.GetOffsetCurvesGivenPlaneNormal(new GeoVector3(1, 0, 0), 2.0));
            Assert.Equal(Bits(new GeoPoint3(1, 0, 3)), Bits(moved.BasePoint));

            ray.Extend(-3.0);
            Assert.Equal(Bits(new GeoPoint3(1, 2, 0)), Bits(ray.BasePoint));
            ray.SecondPoint = new GeoPoint3(1, 5, 0);
            Assert.Equal(Bits(new GeoVector3(0, 1, 0)), Bits(ray.UnitDir));
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach().ToString();

            foreach (string text in Drawings.Concat(new[] { writer }))
            {
                SettingWhatItHasChangesNothing<DxfRay>(text, ray =>
                {
                    ray.BasePoint = ray.BasePoint;
                    ray.UnitDir = ray.UnitDir;
                    ray.SecondPoint = ray.SecondPoint;
                    ray.StartPoint = ray.StartPoint;
                    ray.Layer = ray.Layer;
                    ray.XData = ray.XData;
                    ray.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void ARayGivenANewBaseAndDirection_IsThatRecordWrittenAnew_TheDirectionOfUnitLength()
        {
            string text = DxfFileEzdxfDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfRay ray = ByHandle<DxfRay>(db, "71");

            ray.BasePoint = new GeoPoint3(0.5, 0.5, 0.5);
            ray.UnitDir = new GeoVector3(0.0, 3.0, 4.0);

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "71");
            DxfRay again = ByHandle<DxfRay>(DxfDatabase.Parse(saved), "71");
            Assert.Equal(Bits(new GeoPoint3(0.5, 0.5, 0.5)), Bits(again.BasePoint));
            Assert.Equal(Bits(new GeoVector3(0.0, 3.0 / 5.0, 4.0 / 5.0)), Bits(again.UnitDir));
            Assert.Throws<ArgumentException>(() => again.UnitDir = new GeoVector3(0, 0, 0));
        }

        [Fact]
        public void ARayMadeInCode_IsAppendedFromR2000On_AndRefusedByR12()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var ray = new DxfRay { BasePoint = new GeoPoint3(0, 0, 0), UnitDir = new GeoVector3(0, 1, 0) };

                db.ModelSpace.AppendEntity(ray);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, ray.Handle);
                DxfRay again = ByHandle<DxfRay>(DxfDatabase.Parse(saved), ray.Handle);
                Assert.Equal(Bits(new GeoVector3(0, 1, 0)), Bits(again.UnitDir));
            }

            Assert.Throws<ArgumentException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).ModelSpace.AppendEntity(new DxfRay()));
        }

        [Fact]
        public void ARayErased_TakesItsRecordOut_AndRefusesASetter()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfRay ray = ByHandle<DxfRay>(db, "71");

            ray.Erase();

            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "71");
            Assert.Throws<InvalidOperationException>(() => ray.BasePoint = new GeoPoint3(1, 1, 1));
        }

        [Fact]
        public void ARayMovedOrSheared_IsWhereEzdxfMovesIt()
        {
            // ezdxf 1.4.5 Ray.transform: turned, from (10.298709889692812, -3.617852635521007, 2.4789984604497337) along
            // (0.21121978860927365, 0.9773257740222108, -0.014850331031350753); sheared, from (1.5316426516942345,
            // 4.620958672623072, 3) along (0.19879432835442837, 0.9800412312827006, 0).
            DxfRay turned = ByHandle<DxfRay>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "71");
            turned.TransformBy(Turn);
            Assert.True(turned.BasePoint.DistanceTo(new GeoPoint3(10.298709889692812, -3.617852635521007, 2.4789984604497337)) <= 1E-12);
            Assert.True(turned.UnitDir.Subtract(new GeoVector3(0.21121978860927365, 0.9773257740222108, -0.014850331031350753)).Length <= 1E-12);

            DxfRay sheared = ByHandle<DxfRay>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "71");
            sheared.TransformBy(Shear);
            Assert.True(sheared.BasePoint.DistanceTo(new GeoPoint3(1.5316426516942345, 4.620958672623072, 3.0)) <= 1E-12);
            Assert.True(sheared.UnitDir.Subtract(new GeoVector3(0.19879432835442837, 0.9800412312827006, 0.0)).Length <= 1E-12);
        }
    }
}
