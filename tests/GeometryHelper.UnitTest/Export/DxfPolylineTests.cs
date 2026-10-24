using System;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The LWPOLYLINE as a <see cref="DxfPolyline"/>: read as ezdxf 1.4.5 reads it, its vertices, bulges and widths set and read
    /// back to the bit, walked by AutoCAD's parameter, made in code and appended, erased, and moved as ezdxf moves one; every
    /// other record of the file keeping its bytes.
    /// </summary>
    public class DxfPolylineTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        #region Read as ezdxf reads it

        [Fact]
        public void TheLightweightPolylinesOfEzdxf_AreReadAsEzdxfReadsThem_ToTheBit()
        {
            // As ezdxf 1.4.5 reads them: 6C of model space, closed, lineweight 35, (0, 0) (10, 0) bulge -0.25, (10, 10), (0, 10);
            // 3B of the block FRAME with a bulge of 0.5 on its second vertex.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "6C");
            DxfPolyline frame = ByHandle<DxfPolyline>(db, "3B");

            Assert.Equal(4, polyline.NumberOfVertices);
            Assert.Equal(Bits(new GeoPoint3(10.0, 0.0, 0.0)), Bits(polyline.GetPoint3dAt(1)));
            Assert.Equal(new GeoPoint2(10.0, 10.0), polyline.GetPoint2dAt(2));
            Assert.Equal(-0.25, polyline.GetBulgeAt(1));
            Assert.Equal(0.0, polyline.GetBulgeAt(0));
            Assert.Equal(0.0, polyline.GetStartWidthAt(1));
            Assert.Equal(0.0, polyline.ConstantWidth);
            Assert.True(polyline.Closed);
            Assert.False(polyline.Plinegen);
            Assert.Equal(0.0, polyline.Elevation);
            Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(polyline.Normal));
            Assert.Equal(DxfLineWeight.LineWeight035, polyline.LineWeight);
            Assert.True(polyline.HasBulges);
            Assert.False(polyline.HasWidth);
            Assert.False(polyline.IsOnlyLines);
            Assert.Equal(DxfSegmentType.Line, polyline.GetSegmentType(0));
            Assert.Equal(DxfSegmentType.Arc, polyline.GetSegmentType(1));
            Assert.Equal(0.5, frame.GetBulgeAt(1));
            Assert.Equal("FRAME", frame.BlockName);
            Assert.Throws<ArgumentOutOfRangeException>(() => polyline.GetPoint2dAt(4));
        }

        [Fact]
        public void APolylinesShape_IsTheReadersLoop_AndItsSegmentsAreThoseOfItsVertices()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "6C");
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tolerance.Default });

            object shape = Assert.Single(polyline.GetGeometry());
            Assert.Equal(read.Shapes.Single(s => s.Handle == "6C").Shape, shape);
            Assert.Equal(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)), polyline.GetLineSegmentAt(0));
            Assert.Equal(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)), polyline.GetLineSegment2dAt(0));
            Assert.Equal(GeoArc2.FromBulge(new GeoPoint2(10, 0), new GeoPoint2(10, 10), -0.25), polyline.GetArcSegment2dAt(1));
            GeoArc3 arc = polyline.GetArcSegmentAt(1);
            Assert.True(arc.StartPoint.IsEqualTo(new GeoPoint3(10, 0, 0), Tolerance.Default));
            Assert.True(arc.EndPoint.IsEqualTo(new GeoPoint3(10, 10, 0), Tolerance.Default));
            Assert.Throws<InvalidOperationException>(() => polyline.GetArcSegmentAt(0));
            Assert.True(polyline.OnSegmentAt(0, new GeoPoint2(4, 0), 0.0));
            Assert.False(polyline.OnSegmentAt(0, new GeoPoint2(4, 1), 0.0));
        }

        #endregion

        #region Walked as AutoCAD walks one

        [Fact]
        public void APolylineOfALineAndAHalfCircle_IsWalkedByVertexIndexAndAngle()
        {
            // (0, 0) to (10, 0), then a half circle of radius 5 counter-clockwise to (10, 10): 10 + 5 pi long, its far side
            // (15, 5) at the parameter 1.5, the end at 2.
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new GeoPoint2(10, 0), 1.0, 0.0, 0.0);
            polyline.AddVertexAt(2, new GeoPoint2(10, 10), 0.0, 0.0, 0.0);

            Assert.Equal(3, polyline.NumberOfVertices);
            Assert.Equal(10.0 + 5.0 * Math.PI, polyline.Length, 12);
            Assert.Equal(0.0, polyline.StartParam);
            Assert.Equal(2.0, polyline.EndParam);
            Assert.True(polyline.GetPointAtParameter(1.5).IsEqualTo(new GeoPoint3(15, 5, 0), Tolerance.Default));
            Assert.True(polyline.EndPoint.IsEqualTo(new GeoPoint3(10, 10, 0), Tolerance.Default));
            Assert.Equal(10.0 + 2.5 * Math.PI, polyline.GetDistanceAtParameter(1.5), 12);
            Assert.Equal(1.5, polyline.GetParameterAtDistance(10.0 + 2.5 * Math.PI), 12);
            Assert.Equal(0.5, polyline.GetParameterAtPoint(new GeoPoint3(5, 0, 0)), 12);
            Assert.Equal(5.0, polyline.GetDistAtPoint(new GeoPoint3(5, 0, 0)), 12);
            Assert.True(polyline.GetClosestPointTo(new GeoPoint3(20, 5, 3), false).IsEqualTo(new GeoPoint3(15, 5, 0), Tolerance.Default));
            Assert.Equal(Bits(new GeoVector3(10, 0, 0)), Bits(polyline.GetFirstDerivative(0.5)));
            Assert.True(polyline.GetFirstDerivative(1.5).IsEqualTo(new GeoVector3(0, 5 * Math.PI, 0), Tolerance.Default));
            Assert.True(polyline.GetSecondDerivative(1.5).IsEqualTo(new GeoVector3(-5 * Math.PI * Math.PI, 0, 0), Tolerance.Default));
            Assert.Throws<ArgumentException>(() => polyline.GetParameterAtPoint(new GeoPoint3(5, 1, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => polyline.GetPointAtParameter(2.5));

            // Taken as closed by the chord from (10, 10) to (0, 0): the triangle of 50 and the half circle of 12.5 pi.
            Assert.Equal(50.0 + 12.5 * Math.PI, polyline.Area, 12);
        }

        [Fact]
        public void APolylineReversed_RunsBackOverTheSameArcs()
        {
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0.0, 1.0, 2.0);
            polyline.AddVertexAt(1, new GeoPoint2(10, 0), 1.0, 0.0, 0.0);
            polyline.AddVertexAt(2, new GeoPoint2(10, 10), 0.0, 0.0, 0.0);
            double length = polyline.Length;

            polyline.ReverseCurve();

            Assert.Equal(new GeoPoint2(10, 10), polyline.GetPoint2dAt(0));
            Assert.Equal(-1.0, polyline.GetBulgeAt(0));
            Assert.Equal(new GeoPoint2(0, 0), polyline.GetPoint2dAt(2));
            Assert.Equal(2.0, polyline.GetStartWidthAt(1));
            Assert.Equal(1.0, polyline.GetEndWidthAt(1));
            Assert.Equal(length, polyline.Length, 12);
            Assert.True(polyline.GetPointAtParameter(0.5).IsEqualTo(new GeoPoint3(15, 5, 0), Tolerance.Default));
        }

        #endregion

        #region Nothing changed

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach().ToString();

            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018, writer })
            {
                SettingWhatItHasChangesNothing<DxfPolyline>(text, polyline =>
                {
                    for (int i = 0; i < polyline.NumberOfVertices; i++)
                    {
                        polyline.SetPointAt(i, polyline.GetPoint2dAt(i));
                        polyline.SetBulgeAt(i, polyline.GetBulgeAt(i));
                        polyline.SetStartWidthAt(i, polyline.GetStartWidthAt(i));
                        polyline.SetEndWidthAt(i, polyline.GetEndWidthAt(i));
                    }

                    polyline.Closed = polyline.Closed;
                    polyline.Plinegen = polyline.Plinegen;
                    polyline.ConstantWidth = polyline.ConstantWidth;
                    polyline.Elevation = polyline.Elevation;
                    polyline.Normal = polyline.Normal;
                    polyline.Thickness = polyline.Thickness;
                    polyline.Layer = polyline.Layer;
                    polyline.ColorIndex = polyline.ColorIndex;
                    polyline.LineWeight = polyline.LineWeight;
                    polyline.Reset(true, polyline.NumberOfVertices);
                    polyline.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        #endregion

        #region Changed

        [Fact]
        public void APolylineGivenVerticesBulgesAndWidths_IsThatRecordWrittenAnew_ReadBackToTheBit()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "6C");

            polyline.SetPointAt(1, new GeoPoint2(10.000000000000002, -0.1));
            polyline.SetBulgeAt(1, 0.0);
            polyline.SetBulgeAt(2, 0.375);
            polyline.SetStartWidthAt(3, 0.5);
            polyline.AddVertexAt(4, new GeoPoint2(-5, 5), 0.0, 0.25, 0.25);
            polyline.RemoveVertexAt(0);
            polyline.Elevation = 2.5;
            polyline.Closed = false;

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "6C");
            DxfPolyline again = ByHandle<DxfPolyline>(DxfDatabase.Parse(saved), "6C");

            Assert.Equal(4, again.NumberOfVertices);
            Assert.Equal(new GeoPoint2(10.000000000000002, -0.1), again.GetPoint2dAt(0));
            Assert.Equal(0.0, again.GetBulgeAt(0));
            Assert.Equal(0.375, again.GetBulgeAt(1));
            Assert.Equal(0.5, again.GetStartWidthAt(2));
            Assert.Equal(0.0, again.GetEndWidthAt(2));
            Assert.Equal(new GeoPoint2(-5, 5), again.GetPoint2dAt(3));
            Assert.Equal(0.25, again.GetEndWidthAt(3));
            Assert.Equal(2.5, again.Elevation);
            Assert.False(again.Closed);
            Assert.True(again.HasWidth);
            string record = Records(saved).Single(r => HandleOf(r) == "6C");
            Assert.Contains(" 90\n4\n 70\n0\n 38\n2.5\n 10\n10.000000000000002\n 20\n-0.1\n 10\n10.0\n 20\n10.0\n 42\n0.375\n", record, StringComparison.Ordinal);
        }

        [Fact]
        public void AConstantWidth_IsGroup43_EveryVertexWidthTakenOut()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "6C");
            polyline.SetStartWidthAt(0, 1.0);

            polyline.ConstantWidth = 0.75;

            DxfPolyline again = ByHandle<DxfPolyline>(DxfDatabase.Parse(db.ToText()), "6C");
            Assert.Equal(0.75, again.ConstantWidth);
            Assert.Equal(0.75, again.GetStartWidthAt(0));
            Assert.Equal(0.75, again.GetEndWidthAt(3));
            Assert.DoesNotContain("\n 40\n", Records(db.ToText()).Single(r => HandleOf(r) == "6C"), StringComparison.Ordinal);
        }

        [Fact]
        public void APolylineKeepsTwoVerticesAtLeast_RemovingOrResettingBelowIsRefused()
        {
            // AutoCAD refuses a polyline of fewer than two vertices on save and audit.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "6C");

            polyline.RemoveVertexAt(0);
            polyline.RemoveVertexAt(0);
            string text = db.ToText();

            Assert.Throws<InvalidOperationException>(() => polyline.RemoveVertexAt(0));
            Assert.Throws<InvalidOperationException>(() => polyline.Reset(false, 0));
            Assert.Throws<InvalidOperationException>(() => polyline.Reset(true, 1));
            Assert.Equal(2, polyline.NumberOfVertices);
            Assert.Equal(text, db.ToText());
            polyline.Reset(true, 2);
        }

        #endregion

        #region Made in code

        [Fact]
        public void APolylineMadeInCode_IsAppendedFromR2000On_AndRefusedByR12()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var polyline = new DxfPolyline { Layer = "rebar", ColorIndex = 1 };
                polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0.0, 0.0, 0.0);
                polyline.AddVertexAt(1, new GeoPoint2(5, 0), 0.5, 0.0, 0.0);
                polyline.AddVertexAt(2, new GeoPoint2(5, 5), 0.0, 0.0, 0.0);
                polyline.Closed = true;

                if (text == DxfFileEzdxfDrawings.R12)
                {
                    Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(polyline));
                    continue;
                }

                db.ModelSpace.AppendEntity(polyline);
                Assert.Same(db.ModelSpace, polyline.Owner);
                DxfPolyline again = ByHandle<DxfPolyline>(DxfDatabase.Parse(db.ToText()), polyline.Handle);
                Assert.Equal(3, again.NumberOfVertices);
                Assert.Equal(0.5, again.GetBulgeAt(1));
                Assert.True(again.Closed);
                Assert.Equal("rebar", again.Layer);
                Assert.Equal(1, again.ColorIndex);
            }
        }

        [Fact]
        public void APolylineErased_IsGone_AndRefusesItsSetters()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "6C");

            polyline.Erase();

            Assert.True(polyline.IsErased);
            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "6C");
            Assert.Equal(4, polyline.NumberOfVertices);
            Assert.Throws<InvalidOperationException>(() => polyline.SetBulgeAt(0, 1.0));
        }

        #endregion

        #region Moved as ezdxf moves one

        [Fact]
        public void APolylineTurned_IsWhereEzdxfPutsIt()
        {
            // ezdxf 1.4.5's transform() of 6C by the same matrix: the new extrusion (0.3947397981737998, -0.0713924994178759,
            // 0.9160150668873175), the elevation 6.5943981460456715 (0x1.a60a9e85846dap+2), the first vertex
            // (-3.1404543833265546, -8.826180337930698), the bulge -0.25 kept.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfPolyline polyline = ByHandle<DxfPolyline>(db, "6C");

            polyline.TransformBy(Turn());

            DxfPolyline again = ByHandle<DxfPolyline>(DxfDatabase.Parse(db.ToText()), "6C");
            Near(new GeoVector3(0.3947397981737998, -0.0713924994178759, 0.9160150668873175), again.Normal);
            Assert.Equal(6.5943981460456715, again.Elevation, 9);
            Near(new GeoPoint2(-3.1404543833265546, -8.826180337930698), again.GetPoint2dAt(0));
            Near(new GeoPoint2(3.663995725746065, -16.154171789422826), again.GetPoint2dAt(1));
            Near(new GeoPoint2(10.991987177238189, -9.349721680350203), again.GetPoint2dAt(2));
            Near(new GeoPoint2(4.187537068165571, -2.0217302288580807), again.GetPoint2dAt(3));
            Assert.Equal(-0.25, again.GetBulgeAt(1));
            Assert.True(again.GetPoint3dAt(2).IsEqualTo(Turn().Transform(new GeoPoint3(10, 10, 0)), Tolerance.Default));
        }

        #endregion

        /// <summary>The rotation and move of the oracle's edits, as a matrix of columns.</summary>
        internal static GeoTransform3 Turn() => new GeoTransform3(new double[,]
        {
            { 0.7816391739070251, -0.4829292842142122, 0.3947397981737998, 10.0 },
            { 0.5501172307043584, 0.8320301337746346, -0.07139249941787586, -5.0 },
            { -0.29395787843858057, 0.27295633888831433, 0.9160150668873173, 2.5 },
            { 0.0, 0.0, 0.0, 1.0 },
        });

        internal static void Near(GeoPoint2 expected, GeoPoint2 actual)
        {
            Assert.True(Math.Abs(expected.X - actual.X) <= 1E-9 * Math.Max(1.0, Math.Abs(expected.X)), expected + " " + actual);
            Assert.True(Math.Abs(expected.Y - actual.Y) <= 1E-9 * Math.Max(1.0, Math.Abs(expected.Y)), expected + " " + actual);
        }

        internal static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            double scale = Math.Max(1.0, Math.Max(Math.Abs(expected.X), Math.Max(Math.Abs(expected.Y), Math.Abs(expected.Z))));
            Assert.True(expected.GetVectorTo(actual).Length <= 1E-9 * scale, expected + " " + actual);
        }

        internal static void Near(GeoVector3 expected, GeoVector3 actual) => Assert.True(expected.Subtract(actual).Length <= 1E-9, expected + " " + actual);
    }
}
