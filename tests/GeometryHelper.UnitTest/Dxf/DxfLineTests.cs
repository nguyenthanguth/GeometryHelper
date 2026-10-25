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
    /// The LINE as a <see cref="DxfLine"/>: read as ezdxf 1.4.5 reads it, set and read back to the bit, made in code and
    /// appended, erased, and moved as ezdxf moves one; every other record of the file keeping its bytes.
    /// </summary>
    public class DxfLineTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        #region Read as ezdxf reads it

        [Fact]
        public void TheLinesOfEzdxf_AreReadAsEzdxfReadsThem_ToTheBit()
        {
            // As ezdxf 1.4.5 reads them: the LINE 3E of model space and the LINE 36 of the block BOLT of the drawing of R2000,
            // and the LINE 84 of the dimension's block *D1, whose start is 51.49999999999999 (0x1.9bfffffffffffp+5).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            DxfLine bolt = ByHandle<DxfLine>(db, "36");
            DxfLine dimension = ByHandle<DxfLine>(db, "84");

            Assert.Equal(Bits(new GeoPoint3(0.0, 0.0, 0.0)), Bits(line.StartPoint));
            Assert.Equal(Bits(new GeoPoint3(100.125, 50.5, 3.0)), Bits(line.EndPoint));
            Assert.Equal("steel", line.Layer);
            Assert.Equal("17", line.OwnerHandle);
            Assert.Same(db.ModelSpace, line.Owner);
            Assert.Equal(0, bolt.ColorIndex);
            Assert.Equal("BOLT", bolt.BlockName);
            Assert.Equal(Bits(new GeoPoint3(4.0, 4.0, 0.0)), Bits(bolt.EndPoint));
            Assert.Equal(Bits(FromBits(0x4049bfffffffffff)), Bits(dimension.StartPoint.X));
            Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(line.Normal));
            Assert.Equal(0.0, line.Thickness);
            Assert.Equal(256, line.ColorIndex);
            Assert.Equal("ByLayer", line.Linetype);
            Assert.Equal(DxfLineWeight.ByLayer, line.LineWeight);
            Assert.Equal(1.0, line.LinetypeScale);
            Assert.True(line.Visible);
        }

        [Fact]
        public void ALine_IsItsDeltaLengthAndAngle_AndItsShapeIsTheReadersSegment()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            DxfDocument read = DxfReader.Parse(DxfFileEzdxfDrawings.R2000, new DxfReadOptions { Tolerance = Tol });

            Assert.Equal(Bits(new GeoVector3(100.125, 50.5, 3.0)), Bits(line.Delta));
            Assert.Equal(new GeoVector3(100.125, 50.5, 3.0).Length, line.Length);
            Assert.Equal(Math.Atan2(50.5, 100.125), line.Angle);
            var shape = (GeoLine3)Assert.Single(line.GetGeometry());
            var theirs = (GeoLine3)read.Shapes.Single(s => s.Handle == "3E").Shape;
            Assert.Equal(Bits(theirs.StartPoint), Bits(shape.StartPoint));
            Assert.Equal(Bits(theirs.EndPoint), Bits(shape.EndPoint));
            Assert.Equal(shape, line.GetGeCurve());
            Assert.Equal(0.0, line.StartParam);
            Assert.Equal(line.Length, line.EndParam);
        }

        #endregion

        #region Nothing changed

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach().ToString();

            foreach (string text in Drawings.Concat(new[] { writer }))
            {
                SettingWhatItHasChangesNothing<DxfLine>(text, line =>
                {
                    line.StartPoint = line.StartPoint;
                    line.EndPoint = line.EndPoint;
                    line.Normal = line.Normal;
                    line.Thickness = line.Thickness;
                    line.Layer = line.Layer;
                    line.ColorIndex = line.ColorIndex;
                    line.TrueColor = line.TrueColor;
                    line.Linetype = line.Linetype;
                    line.LineWeight = line.LineWeight;
                    line.LinetypeScale = line.LinetypeScale;
                    line.Visible = line.Visible;
                    line.XData = line.XData;
                    line.SetFromGeCurve(new GeoLine3(line.StartPoint, line.EndPoint));
                    line.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        #endregion

        #region Changed

        [Fact]
        public void ALineGivenNewEndsNormalAndThickness_IsThatRecordWrittenAnew_ReadBackToTheBit()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            var start = new GeoPoint3(1.25, -2.5, 3.0000000000000004);
            var end = new GeoPoint3(100.1, 0.2, 0.3);
            var normal = new GeoVector3(0.6, 0.0, 0.8);

            line.StartPoint = start;
            line.EndPoint = end;
            line.Normal = normal;
            line.Thickness = 12.5;

            string saved = Encoding.UTF8.GetString(Saved(db));
            OnlyTheseChanged(text, saved, "3E");

            DxfLine again = ByHandle<DxfLine>(DxfDatabase.Parse(saved), "3E");
            Assert.Equal(Bits(start), Bits(again.StartPoint));
            Assert.Equal(Bits(end), Bits(again.EndPoint));
            Assert.Equal(Bits(normal), Bits(again.Normal));
            Assert.Equal(12.5, again.Thickness);
            Assert.Contains("1001\nGHPROBE\n", Records(saved).Single(r => HandleOf(r) == "3E"), StringComparison.Ordinal);

            // AutoCAD's order of AcDbLine: the thickness first, then the points, then the extrusion.
            string record = Records(saved).Single(r => HandleOf(r) == "3E");
            Assert.True(record.IndexOf("100\nAcDbLine\n 39\n12.5\n 10\n1.25\n", StringComparison.Ordinal) >= 0, record);
            Assert.Contains(" 31\n0.3\n210\n0.6\n220\n0\n230\n0.8\n1001\n", record, StringComparison.Ordinal);
        }

        #endregion

        [Fact]
        public void ALineMoved_IsWhereEzdxfMovesIt_ItsNormalAndThicknessCarriedAsEzdxfCarriesThem()
        {
            // ezdxf 1.4.5: the LINE 3E given the thickness 2.5, then transform() by scale 1.5, x_rotate(0.3), translate(10, -5, 2):
            // start (10, -5, 2), end (160.1875, 66.03689812128863, 28.684669855661696), extrusion (0, -0.2955202066613395,
            // 0.955336489125606), thickness 3.75.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            line.Thickness = 2.5;
            var matrix = new GeoTransform3(new[,]
            {
                { 1.5, 0.0, 0.0, 10.0 },
                { 0.0, 1.433004733688409, -0.4432803099920093, -5.0 },
                { 0.0, 0.4432803099920093, 1.433004733688409, 2.0 },
                { 0.0, 0.0, 0.0, 1.0 },
            });

            line.TransformBy(matrix);

            DxfLine again = ByHandle<DxfLine>(DxfDatabase.Parse(db.ToText()), "3E");
            Near(new GeoPoint3(10, -5, 2), again.StartPoint);
            Near(new GeoPoint3(160.1875, 66.03689812128863, 28.684669855661696), again.EndPoint);
            Near(new GeoPoint3(0, -0.2955202066613395, 0.955336489125606), new GeoPoint3(again.Normal.X, again.Normal.Y, again.Normal.Z));
            Assert.Equal(3.75, again.Thickness, 12);

            DxfEntity copy = again.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(1, 0, 0)));
            Assert.Null(copy.Handle);
            Near(new GeoPoint3(11, -5, 2), ((DxfLine)copy).StartPoint);
            Near(new GeoPoint3(10, -5, 2), again.StartPoint);
        }

        [Fact]
        public void ALineIsWalked_AsAutoCADWalksOne_ItsParameterTheDistanceFromItsStart()
        {
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 40, 0));

            Assert.Equal(50.0, line.EndParam);
            Assert.Equal(new GeoPoint3(6, 8, 0), line.GetPointAtParameter(10));
            Assert.Equal(new GeoPoint3(6, 8, 0), line.GetPointAtDist(10));
            Assert.Equal(25.0, line.GetParameterAtPoint(new GeoPoint3(15, 20, 0)), 12);
            Assert.Equal(25.0, line.GetDistAtPoint(new GeoPoint3(15, 20, 0)), 12);
            Assert.Equal(new GeoVector3(0.6, 0.8, 0), line.GetFirstDerivative(3.0));
            Assert.Equal(new GeoVector3(0, 0, 0), line.GetSecondDerivative(3.0));
            Assert.Equal(new GeoPoint3(30, 40, 0), line.GetClosestPointTo(new GeoPoint3(60, 80, 0), false));
            Assert.Equal(new GeoPoint3(60, 80, 0), line.GetClosestPointTo(new GeoPoint3(60, 80, 0), true));
            Assert.Equal(Math.Atan2(40, 30), line.Angle);
            Assert.False(line.Closed);
            Assert.Throws<ArgumentOutOfRangeException>(() => line.GetPointAtParameter(51));
            Assert.Throws<ArgumentException>(() => line.GetParameterAtPoint(new GeoPoint3(1, 0, 0)));

            // The offset to the right seen from the normal's tip, the side an arc's radius grows to, as every curve offsets;
            // to the left about the normal turned over. The pieces of a split, an extension and a reversal.
            var offset = (DxfLine)Assert.Single(line.GetOffsetCurves(5.0));
            Near(new GeoPoint3(4, -3, 0), offset.StartPoint);
            Near(new GeoPoint3(34, 37, 0), offset.EndPoint);
            Near(new GeoPoint3(-4, 3, 0), ((DxfLine)Assert.Single(line.GetOffsetCurvesGivenPlaneNormal(new GeoVector3(0, 0, -1), 5.0))).StartPoint);
            Assert.Equal(new[] { 20.0, 30.0 }, line.GetSplitCurves(new[] { 20.0 }).Select(c => ((DxfLine)c).Length));
            line.Extend(60.0);
            Assert.Equal(new GeoPoint3(36, 48, 0), line.EndPoint);
            line.ReverseCurve();
            Assert.Equal(new GeoPoint3(36, 48, 0), line.StartPoint);
        }

        [Fact]
        public void ALine_TakesTheDrawingsDefaults_AndFillsTheBoxOfItsEnds()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            db.Clayer = "steel";
            db.Cecolor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
            db.Celweight = DxfLineWeight.LineWeight050;
            var line = new DxfLine(new GeoPoint3(3, -1, 2), new GeoPoint3(-4, 6, 0));

            line.SetDatabaseDefaults(db);

            Assert.Equal("steel", line.Layer);
            Assert.Equal(3, line.ColorIndex);
            Assert.Equal(DxfLineWeight.LineWeight050, line.LineWeight);
            Assert.Equal(new GeoAabb3(new GeoPoint3(-4, -1, 0), new GeoPoint3(3, 6, 2)), line.GeometricExtents);
            Assert.Throws<InvalidOperationException>(() => new DxfLine().GeometricExtents);
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            double scale = Math.Max(1.0, Math.Max(Math.Abs(expected.X), Math.Max(Math.Abs(expected.Y), Math.Abs(expected.Z))));
            Assert.True(expected.DistanceTo(actual) <= 1E-9 * scale, expected + " against " + actual);
        }

        #region Made in code

        [Fact]
        public void ALineMadeInCode_IsAppendedToEachVersion_WithAHandleAboveEveryOther_AndReadBack()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                int count = db.ModelSpace.Count;
                var line = new DxfLine(new GeoPoint3(1, 2, 3), new GeoPoint3(4.5, 6.25, -7)) { Layer = "rebar", ColorIndex = 1, Thickness = 2.5 };

                Assert.Null(line.Handle);
                Assert.Null(line.Owner);
                db.ModelSpace.AppendEntity(line);

                Assert.Equal(count + 1, db.ModelSpace.Count);
                Assert.Same(line, db.ModelSpace.Last());
                Assert.Same(db.ModelSpace, line.Owner);
                Assert.NotNull(line.Handle);
                Assert.True(db.LayerTable.Has("rebar"));

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, line.Handle, db.LayerTable["rebar"].Handle, LayerTableHandle(text));
                DxfLine again = ByHandle<DxfLine>(DxfDatabase.Parse(saved), line.Handle);
                Assert.Equal(Bits(new GeoPoint3(1, 2, 3)), Bits(again.StartPoint));
                Assert.Equal(Bits(new GeoPoint3(4.5, 6.25, -7)), Bits(again.EndPoint));
                Assert.Equal("rebar", again.Layer);
                Assert.Equal(1, again.ColorIndex);
                Assert.Equal(2.5, again.Thickness);
                Assert.Throws<InvalidOperationException>(() => db.ModelSpace.AppendEntity(line));
            }
        }

        [Fact]
        public void ALineErased_TakesItsRecordAndItsExtensionDictionaryOut_AndIsReadStill()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLine line = ByHandle<DxfLine>(db, "3E");
            int count = db.ModelSpace.Count;

            line.Erase();

            Assert.True(line.IsErased);
            Assert.Equal(count - 1, db.ModelSpace.Count);
            Assert.DoesNotContain(line, db.ModelSpace);
            Assert.Equal(Bits(new GeoPoint3(100.125, 50.5, 3.0)), Bits(line.EndPoint));
            Assert.Throws<InvalidOperationException>(() => line.Erase());
            Assert.Throws<InvalidOperationException>(() => line.StartPoint = new GeoPoint3(1, 1, 1));
            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "3E" || HandleOf(r) == "92");
            Assert.Throws<InvalidOperationException>(() => new DxfLine().Erase());
        }

        #endregion

        /// <summary>The handle of the TABLE record of the layer table of a text, whose count a new layer raises.</summary>
        private static string LayerTableHandle(string text)
            => HandleOf(Records(text).First(r => r.StartsWith("  0\nTABLE\n  2\nLAYER\n", StringComparison.Ordinal)));
    }
}
