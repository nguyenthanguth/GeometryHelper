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
    /// The XLINE as a <see cref="DxfXline"/>: read as ezdxf 1.4.5 reads it, set and read back, made in code and appended
    /// (refused in R12), erased, walked both ways from its base point, split into rays and lines, and moved as ezdxf moves one.
    /// </summary>
    public class DxfXlineTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        private static readonly GeoTransform3 Shear = new GeoTransform3(new[,]
        {
            { 0.9210609940028851, -0.3894183423086505, 0.0, 1.0 },
            { 0.778836684617301, 1.8421219880057702, 0.0, 2.0 },
            { 0.0, 0.0, 1.0, 3.0 },
            { 0.0, 0.0, 0.0, 1.0 },
        });

        [Fact]
        public void TheXlineOfEzdxf_IsReadAsEzdxfReadsIt_ToTheBit()
        {
            // ezdxf 1.4.5: XLINE 72 through (0, 5, 0) along (0, 1, 0).
            foreach (string text in Drawings)
            {
                DxfXline xline = ByHandle<DxfXline>(DxfDatabase.Parse(text), "72");

                Assert.Equal(Bits(new GeoPoint3(0, 5, 0)), Bits(xline.BasePoint));
                Assert.Equal(Bits(new GeoVector3(0, 1, 0)), Bits(xline.UnitDir));
                Assert.Equal(Bits(new GeoPoint3(0, 6, 0)), Bits(xline.SecondPoint));
                Assert.Equal(double.NegativeInfinity, xline.StartParam);
                Assert.Equal(double.PositiveInfinity, xline.EndParam);
                Assert.False(xline.Closed);
                Assert.Throws<NotSupportedException>(() => xline.StartPoint);
                Assert.Throws<NotSupportedException>(() => xline.EndPoint);
            }
        }

        [Fact]
        public void TheShapeOfAnXline_IsTheReadersLine_ToTheBit()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tol });
            DxfXline xline = ByHandle<DxfXline>(db, "72");

            var shape = (GeoXLine3)Assert.Single(xline.GetGeometry());
            var theirs = (GeoXLine3)read.Shapes.Single(s => s.Handle == "72").Shape;
            Assert.Equal(Bits(theirs.Origin), Bits(shape.Origin));
            Assert.Equal(Bits(theirs.Direction), Bits(shape.Direction));
        }

        [Fact]
        public void AnXline_IsWalkedBothWaysFromItsBasePoint_AndSplitIntoRaysAndLines()
        {
            DxfXline xline = new DxfXline { BasePoint = new GeoPoint3(0, 5, 0), UnitDir = new GeoVector3(0, 1, 0) };

            Assert.Equal(Bits(new GeoPoint3(0, -5, 0)), Bits(xline.GetPointAtParameter(-10.0)));
            Assert.Equal(-10.0, xline.GetParameterAtPoint(new GeoPoint3(0, -5, 0)));
            Assert.Equal(-10.0, xline.GetDistAtPoint(new GeoPoint3(0, -5, 0)));
            Assert.Equal(Bits(new GeoPoint3(0, -100, 0)), Bits(xline.GetClosestPointTo(new GeoPoint3(7, -100, 0), false)));
            Assert.Throws<ArgumentException>(() => xline.GetParameterAtPoint(new GeoPoint3(1, 5, 0)));
            Assert.Throws<NotSupportedException>(() => xline.Extend(1.0));

            IReadOnlyList<DxfCurve> pieces = xline.GetSplitCurves(new[] { 4.0, -2.0 });
            Assert.Equal(3, pieces.Count);
            var back = (DxfRay)pieces[0];
            Assert.Equal(Bits(new GeoPoint3(0, 3, 0)), Bits(back.BasePoint));
            Assert.Equal(Bits(new GeoVector3(0, -1, 0)), Bits(back.UnitDir));
            Assert.Equal(Bits(new GeoPoint3(0, 9, 0)), Bits(((DxfLine)pieces[1]).EndPoint));
            Assert.Equal(Bits(new GeoPoint3(0, 9, 0)), Bits(((DxfRay)pieces[2]).BasePoint));
            Assert.IsType<DxfXline>(Assert.Single(xline.GetSplitCurves(new double[0])));

            xline.ReverseCurve();
            Assert.Equal(Bits(new GeoVector3(0, -1, 0)), Bits(xline.UnitDir));
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach().ToString();

            foreach (string text in Drawings.Concat(new[] { writer }))
            {
                SettingWhatItHasChangesNothing<DxfXline>(text, xline =>
                {
                    xline.BasePoint = xline.BasePoint;
                    xline.UnitDir = xline.UnitDir;
                    xline.SecondPoint = xline.SecondPoint;
                    xline.Layer = xline.Layer;
                    xline.XData = xline.XData;
                    xline.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void AnXlineGivenASecondPoint_HasItsDirectionTheWayThere_AndOnlyThatRecordChanges()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfXline xline = ByHandle<DxfXline>(db, "72");

            xline.SecondPoint = new GeoPoint3(3.0, 9.0, 0.0);

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "72");
            DxfXline again = ByHandle<DxfXline>(DxfDatabase.Parse(saved), "72");
            Assert.Equal(Bits(new GeoVector3(3.0 / 5.0, 4.0 / 5.0, 0.0)), Bits(again.UnitDir));
            Assert.Equal(Bits(new GeoPoint3(0, 5, 0)), Bits(again.BasePoint));
            Assert.Throws<ArgumentException>(() => again.SecondPoint = again.BasePoint);
        }

        [Fact]
        public void AnXlineMadeInCode_IsAppendedFromR2000On_AndRefusedByR12()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var xline = new DxfXline { BasePoint = new GeoPoint3(1, 0, 0) };

                db.ModelSpace.AppendEntity(xline);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, xline.Handle);
                DxfXline again = ByHandle<DxfXline>(DxfDatabase.Parse(saved), xline.Handle);
                Assert.Equal(Bits(new GeoPoint3(1, 0, 0)), Bits(again.BasePoint));
                Assert.Equal(Bits(new GeoVector3(1, 0, 0)), Bits(again.UnitDir));
            }

            Assert.Throws<ArgumentException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).ModelSpace.AppendEntity(new DxfXline()));
        }

        [Fact]
        public void AnXlineErased_TakesItsRecordOut_AndRefusesASetter()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfXline xline = ByHandle<DxfXline>(db, "72");

            xline.Erase();

            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "72");
            Assert.Throws<InvalidOperationException>(() => xline.UnitDir = new GeoVector3(1, 0, 0));
        }

        [Fact]
        public void AnXlineSheared_IsWhereEzdxfMovesIt()
        {
            // ezdxf 1.4.5 XLine.transform on the shear: through (-0.9470917115432527, 11.210609940028851, 3) along
            // (-0.20682574854393662, 0.9783777949949806, 0).
            DxfXline xline = ByHandle<DxfXline>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "72");

            xline.TransformBy(Shear);

            Assert.True(xline.BasePoint.DistanceTo(new GeoPoint3(-0.9470917115432527, 11.210609940028851, 3.0)) <= 1E-12);
            Assert.True(xline.UnitDir.Subtract(new GeoVector3(-0.20682574854393662, 0.9783777949949806, 0.0)).Length <= 1E-12);
        }
    }
}
