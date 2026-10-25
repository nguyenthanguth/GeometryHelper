using System;
using System.Linq;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfDBTextTests;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The MINSERT as a <see cref="DxfMInsertBlock"/>: an INSERT of more than one column or row, read as ezdxf 1.4.5 reads it,
    /// its array set and read back, made in code, and moved with its spacing scaled.
    /// </summary>
    public class DxfMInsertBlockTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        [Fact]
        public void TheMInsertsOfEzdxf_AreReadAsEzdxfReadsThem_AndAnInsertOfOneCellIsABlockReference()
        {
            // As ezdxf 1.4.5 reads the INSERT 6A: BOLT at (300, 0, 0), 4 columns 30 apart by 3 rows 20 apart.
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMInsertBlock grid = ByHandle<DxfMInsertBlock>(db, "6A");

                Assert.Equal(4, grid.Columns);
                Assert.Equal(3, grid.Rows);
                Assert.Equal(30.0, grid.ColumnSpacing);
                Assert.Equal(20.0, grid.RowSpacing);
                Assert.Equal("BOLT", grid.Name);
                Near(299.0, grid.BlockTransform[0, 3]);
                Near(-2.0, grid.BlockTransform[1, 3]);
                Assert.IsNotType<DxfMInsertBlock>(ByHandle<DxfBlockReference>(db, "65"));
                Assert.Equal(db.BlockTable["BOLT"].Count(), grid.Explode().Count);
            }
        }

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in Drawings)
            {
                SettingWhatItHasChangesNothing<DxfMInsertBlock>(text, m =>
                {
                    m.Columns = m.Columns;
                    m.Rows = m.Rows;
                    m.ColumnSpacing = m.ColumnSpacing;
                    m.RowSpacing = m.RowSpacing;
                    m.Position = m.Position;
                    m.BlockTransform = m.BlockTransform;
                    m.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        [Fact]
        public void AnArrayGivenNewCountsAndSpacing_IsThatRecordWrittenAnew_AndACountBelowOneRefused()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMInsertBlock grid = ByHandle<DxfMInsertBlock>(db, "6A");

                grid.Columns = 5;
                grid.Rows = 2;
                grid.ColumnSpacing = 12.5;
                grid.RowSpacing = -7.25;

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "6A");
                DxfMInsertBlock again = ByHandle<DxfMInsertBlock>(DxfDatabase.Parse(saved), "6A");
                Assert.Equal(5, again.Columns);
                Assert.Equal(2, again.Rows);
                Assert.Equal(12.5, again.ColumnSpacing);
                Assert.Equal(-7.25, again.RowSpacing);
                Assert.Throws<ArgumentOutOfRangeException>(() => grid.Columns = 0);
            }
        }

        [Fact]
        public void AnArrayScaled_IsMovedAsEzdxfMovesIt_ItsSpacingScaledWithIt()
        {
            // ezdxf 1.4.5 by the uniform scale by 2 turned about z (M2), the spacing scaled as the oracle scales it: insert
            // (569.2018934753636, 183.31212399680373, 1), rotation 17.188733853924695, scale 2, spacing 60 and 40.
            var scale = new GeoTransform3(new double[,]
            {
                { 1.910672978251212, -0.5910404133226791, 0.0, -4.0 },
                { 0.5910404133226791, 1.910672978251212, 0.0, 6.0 },
                { 0.0, 0.0, 2.0, 1.0 },
                { 0, 0, 0, 1 },
            });
            DxfMInsertBlock grid = ByHandle<DxfMInsertBlock>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000), "6A");

            grid.TransformBy(scale);

            Near(new GeoPoint3(569.2018934753636, 183.31212399680373, 1.0), grid.Position);
            Near(17.188733853924695 * (Math.PI / 180.0), grid.Rotation);
            Near(new GeoVector3(2.0, 2.0, 2.0), grid.ScaleFactors);
            Near(60.0, grid.ColumnSpacing);
            Near(40.0, grid.RowSpacing);
        }

        [Fact]
        public void AnArrayMadeInCode_IsAppended_AndOneOfNoBlockRefused()
        {
            foreach (string text in Drawings.Concat(new[] { DxfFileEzdxfDrawings.R12 }))
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var made = new DxfMInsertBlock(new GeoPoint3(5, 6, 0), db.BlockTable["BOLT"], 2, 3, 10.0, 15.0);
                db.ModelSpace.AppendEntity(made);

                DxfMInsertBlock again = ByHandle<DxfMInsertBlock>(DxfDatabase.Parse(db.ToText()), made.Handle);
                Assert.Equal(2, again.Columns);
                Assert.Equal(3, again.Rows);
                Assert.Equal(15.0, again.RowSpacing);
                Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(new DxfMInsertBlock()));
            }
        }

        [Fact]
        public void AnArrayMirrored_PutsItsRowsOnTheOtherSide_AsTheReaderPlacesThem()
        {
            // The MINSERT 6A of 4 columns 30 apart by 3 rows 20 apart, mirrored in x, in z and turned and mirrored: its y axis
            // comes out turned over, and its rows, 20 apart along it, go to the other side with it (row spacing -20). Every LINE
            // of every cell the reader placed lands where the mirror takes it.
            var mirrors = new[]
            {
                new GeoTransform3(new double[,] { { -1, 0, 0, 10 }, { 0, 1, 0, 20 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } }),
                new GeoTransform3(new double[,] { { 1, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, -1, 3 }, { 0, 0, 0, 1 } }),
                GeoTransform3.RotationAxis(new GeoVector3(1, 1, 1), 0.7).Multiply(new GeoTransform3(new double[,] { { -1, 0, 0, 5 }, { 0, 1, 0, -3 }, { 0, 0, 1, 7 }, { 0, 0, 0, 1 } })),
            };

            foreach (string text in Drawings)
            {
                foreach (GeoTransform3 mirror in mirrors)
                {
                    DxfMInsertBlock grid = ByHandle<DxfMInsertBlock>(DxfDatabase.Parse(text), "6A");
                    GeoLine3[] wanted = grid.GetGeometry().OfType<GeoLine3>().Select(l => mirror.Transform(l)).ToArray();

                    grid.TransformBy(mirror);

                    Assert.Equal(12, wanted.Length);
                    DxfBlockReferenceTests.SameLines(wanted, grid.GetGeometry().OfType<GeoLine3>().ToArray());
                    Near(30.0, grid.ColumnSpacing);
                    Near(-20.0, grid.RowSpacing);
                }
            }
        }
    }
}
