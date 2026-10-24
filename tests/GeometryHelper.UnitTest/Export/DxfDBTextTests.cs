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
    /// The TEXT as a <see cref="DxfDBText"/>: read as ezdxf 1.4.5 reads it, set and read back to the bit, made in code and
    /// appended, erased, and moved as ezdxf moves one; every other record of the file keeping its bytes.
    /// </summary>
    public class DxfDBTextTests
    {
        private static readonly string[] Drawings = { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 };

        #region Read as ezdxf reads it

        [Fact]
        public void TheTextsOfEzdxf_AreReadAsEzdxfReadsThem_ToTheBit()
        {
            // As ezdxf 1.4.5 reads the TEXT 64 of R2000 (67 of R12): 'Grid %%c20 A' at (7, 8, 0), aligned (7, 8, 0), centred
            // on its middle (72 = 1, 73 = 2), 2.5 high, turned by 15 degrees, the style Standard.
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfDBText grid = ByHandle<DxfDBText>(db, text == DxfFileEzdxfDrawings.R12 ? "67" : "64");

                Assert.Equal("Grid %%c20 A", grid.TextString);
                Assert.Equal(Bits(new GeoPoint3(7.0, 8.0, 0.0)), Bits(grid.Position));
                Assert.Equal(Bits(new GeoPoint3(7.0, 8.0, 0.0)), Bits(grid.AlignmentPoint));
                Assert.Equal(2.5, grid.Height);
                Assert.Equal(15.0 * (Math.PI / 180.0), grid.Rotation);
                Assert.Equal(1.0, grid.WidthFactor);
                Assert.Equal(0.0, grid.Oblique);
                Assert.Equal(DxfTextHorizontalMode.TextCenter, grid.HorizontalMode);
                Assert.Equal(DxfTextVerticalMode.TextVerticalMid, grid.VerticalMode);
                Assert.Equal(DxfAttachmentPoint.MiddleCenter, grid.Justify);
                Assert.False(grid.IsDefaultAlignment);
                Assert.False(grid.IsMirroredInX);
                Assert.False(grid.IsMirroredInY);
                Assert.Equal("Standard", grid.TextStyleName);
                Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(grid.Normal));
                Assert.Equal(0.0, grid.Thickness);
                Assert.Same(db.ModelSpace, grid.Owner);
            }
        }

        [Fact]
        public void ATextOnANormalDownwards_GivesItsPointsInTheWorld_AndItsShapeIsTheReaders()
        {
            // A text whose file says (3, 4, 5) on the normal (0, 0, -1) stands at (-3, 4, -5), as AutoCAD gives it.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            var text = new DxfDBText { TextString = "Fit under", Height = 1.5 };
            db.ModelSpace.AppendEntity(text);
            text.Normal = new GeoVector3(0, 0, -1);
            text.Position = new GeoPoint3(-3.0, 4.0, -5.0);

            string record = Records(db.ToText()).Single(r => HandleOf(r) == text.Handle);
            Assert.Contains(" 10\n3\n 20\n4\n 30\n5\n", record.Replace("\r", string.Empty), StringComparison.Ordinal);
            Assert.Equal(Bits(new GeoPoint3(-3.0, 4.0, -5.0)), Bits(text.Position));

            DxfDocument read = DxfReader.Parse(db.ToText(), new DxfReadOptions { Tolerance = Tolerance.Default });
            var theirs = (DxfText)read.Shapes.Single(s => s.Handle == text.Handle).Shape;
            var ours = (DxfText)Assert.Single(text.GetGeometry());
            Assert.Equal(Bits(theirs.Position), Bits(ours.Position));
            Assert.Equal(theirs.Value, ours.Value);
            Assert.Equal(Bits(theirs.Height), Bits(ours.Height));
        }

        #endregion

        #region Nothing changed

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            string writer = DxfReaderRoundTripTests.OneOfEach()
                .Add(new DxfText(new GeoPoint3(1, 2, 3), new GeoVector3(0, 0, 1), 2.5, 0.3, "writer text"))
                .Add(new DxfText(new GeoPoint3(4, 5, 6), new GeoVector3(0, 0.6, 0.8), 1.5, 0.2, "writer\nmtext", true, 1.0, 20.0))
                .ToString();

            foreach (string text in Drawings.Concat(new[] { writer }))
            {
                SettingWhatItHasChangesNothing<DxfDBText>(text, t =>
                {
                    t.TextString = t.TextString;
                    t.Position = t.Position;
                    t.AlignmentPoint = t.AlignmentPoint;
                    t.Height = t.Height;
                    t.Rotation = t.Rotation;
                    t.WidthFactor = t.WidthFactor;
                    t.Oblique = t.Oblique;
                    t.HorizontalMode = t.HorizontalMode;
                    t.VerticalMode = t.VerticalMode;
                    t.Justify = t.Justify;
                    t.IsMirroredInX = t.IsMirroredInX;
                    t.IsMirroredInY = t.IsMirroredInY;
                    t.Normal = t.Normal;
                    t.Thickness = t.Thickness;
                    t.Layer = t.Layer;
                    t.ColorIndex = t.ColorIndex;
                    t.XData = t.XData;
                    t.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        #endregion

        [Fact]
        public void TheWordsItReads_GivenBack_ChangeNothing_ThoughAnEscapeInThemIsALineBreak()
        {
            // A TEXT whose words hold the escape \U+000A reads a line break, which a TEXT is not given; given back what it reads,
            // nothing is checked or written.
            string text = DxfFileEzdxfDrawings.R2000.Replace("Grid %%c20 A", "Grid\\U+000Atwo");
            Assert.NotEqual(DxfFileEzdxfDrawings.R2000, text);
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");
            Assert.Equal("Grid\ntwo", grid.TextString);

            grid.TextString = grid.TextString;

            Assert.Equal(new UTF8Encoding(false).GetBytes(text), Saved(db));
            Assert.Throws<ArgumentException>(() => grid.TextString = "Grid\nthree");
        }

        #region Changed

        [Fact]
        public void ATextGivenNewWordsPlaceAndLook_IsThatRecordWrittenAnew_ReadBackToTheBit()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfDBText grid = ByHandle<DxfDBText>(db, text == DxfFileEzdxfDrawings.R12 ? "67" : "64");
                string handle = grid.Handle;

                grid.TextString = "Grid é 30";
                grid.Position = new GeoPoint3(4.0, 5.0, 0.0);
                grid.AlignmentPoint = new GeoPoint3(9.5, 8.25, 0.0);
                grid.Height = 4.0;
                grid.Rotation = 1.0;
                grid.WidthFactor = 1.2;
                grid.Oblique = 0.2;
                grid.Justify = DxfAttachmentPoint.TopRight;
                grid.IsMirroredInX = true;
                grid.Thickness = 0.5;

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, handle);

                DxfDBText again = ByHandle<DxfDBText>(DxfDatabase.Parse(saved), handle);
                Assert.Equal("Grid é 30", again.TextString);
                Assert.Equal(Bits(new GeoPoint3(4.0, 5.0, 0.0)), Bits(again.Position));
                Assert.Equal(Bits(new GeoPoint3(9.5, 8.25, 0.0)), Bits(again.AlignmentPoint));
                Assert.Equal(4.0, again.Height);
                Assert.Equal(Bits(1.0), Bits(again.Rotation));
                Assert.Equal(1.2, again.WidthFactor);
                Assert.Equal(Bits(0.2), Bits(again.Oblique));
                Assert.Equal(DxfTextHorizontalMode.TextRight, again.HorizontalMode);
                Assert.Equal(DxfTextVerticalMode.TextTop, again.VerticalMode);
                Assert.True(again.IsMirroredInX);
                Assert.Equal(0.5, again.Thickness);
            }
        }

        [Fact]
        public void TheWordsGivenALineBreakOrNull_AndAModeOutOfRange_AreRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");

            Assert.Throws<ArgumentException>(() => grid.TextString = "two\nlines");
            Assert.Throws<ArgumentNullException>(() => grid.TextString = null);
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.Height = 0.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.HorizontalMode = (DxfTextHorizontalMode)6);
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.Justify = (DxfAttachmentPoint)25);
        }

        #endregion

        #region Moved

        [Fact]
        public void ATextScaledUnlikeAlongItsAxes_IsMovedAsEzdxfMovesIt()
        {
            // ezdxf 1.4.5, Text.transform() of the TEXT 64 of R2000 by x stretched twice and moved by 1: insert and align point
            // (15, 8, 0), height 2.56527057434103, rotation 7.630740212430057, oblique -20.55604521958348, width 1.899519052838329.
            var stretch = new GeoTransform3(new double[,] { { 2, 0, 0, 1 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });

            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfDBText grid = ByHandle<DxfDBText>(db, text == DxfFileEzdxfDrawings.R12 ? "67" : "64");
                string handle = grid.Handle;

                grid.TransformBy(stretch);

                DxfDBText again = ByHandle<DxfDBText>(DxfDatabase.Parse(db.ToText()), handle);
                Near(new GeoPoint3(15.0, 8.0, 0.0), again.Position);
                Near(new GeoPoint3(15.0, 8.0, 0.0), again.AlignmentPoint);
                Near(2.56527057434103, again.Height);
                Near(7.630740212430057 * (Math.PI / 180.0), again.Rotation);
                Near(-20.55604521958348 * (Math.PI / 180.0), again.Oblique);
                Near(1.899519052838329, again.WidthFactor);
            }
        }

        [Fact]
        public void ATextTurnedOutOfItsPlane_IsMovedAsEzdxfMovesIt()
        {
            // ezdxf 1.4.5 by the turn and move M1: insert (7.485053854217978, -8.512214266717093, 6.5943981460456715) in the
            // new plane's system, rotation -32.121572440350405, extrusion (0.3947397981737998, -0.0713924994178759, 0.9160150668873175).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");
            var normal = new GeoVector3(0.3947397981737998, -0.0713924994178759, 0.9160150668873175);

            grid.TransformBy(Turn);

            Near(normal, grid.Normal);
            Near(Ocs(normal, new GeoPoint3(7.485053854217978, -8.512214266717093, 6.5943981460456715)), grid.Position);
            Near(-32.121572440350405 * (Math.PI / 180.0), grid.Rotation);
            Near(2.5, grid.Height);
            Near(1.0, grid.WidthFactor);
        }

        #endregion

        #region Made in code

        [Fact]
        public void ATextMadeInCode_IsAppendedToEachVersion_AndReadBack()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var made = new DxfDBText { TextString = "new text", Position = new GeoPoint3(3, 3, 0), Height = 2.0, Rotation = 0.3, Layer = "notes" };

                Assert.Null(made.Handle);
                db.ModelSpace.AppendEntity(made);
                Assert.Same(db.ModelSpace, made.Owner);
                Assert.NotNull(made.Handle);

                string saved = db.ToText();
                DxfDBText again = ByHandle<DxfDBText>(DxfDatabase.Parse(saved), made.Handle);
                Assert.Equal("new text", again.TextString);
                Assert.Equal(Bits(new GeoPoint3(3, 3, 0)), Bits(again.Position));
                Assert.Equal(2.0, again.Height);
                Assert.Equal(Bits(0.3), Bits(again.Rotation));
                Assert.Equal("notes", again.Layer);
                Assert.Equal(DxfAttachmentPoint.BaseLeft, again.Justify);
            }
        }

        [Fact]
        public void ATextErased_GoesFromTheFile_AndRefusesItsSetters()
        {
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfDBText grid = ByHandle<DxfDBText>(db, "64");

            grid.Erase();

            Assert.True(grid.IsErased);
            Assert.Equal("Grid %%c20 A", grid.TextString);
            Assert.Throws<InvalidOperationException>(() => grid.TextString = "x");
            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "64");
        }

        #endregion

        /// <summary>The turn and move M1 of the oracle: a rotation out of the xy plane, then a move by (10, -5, 2.5).</summary>
        internal static readonly GeoTransform3 Turn = new GeoTransform3(new double[,]
        {
            { 0.7816391739070251, -0.4829292842142122, 0.3947397981737998, 10.0 },
            { 0.5501172307043584, 0.8320301337746346, -0.07139249941787586, -5.0 },
            { -0.29395787843858057, 0.27295633888831433, 0.9160150668873173, 2.5 },
            { 0, 0, 0, 1 },
        });

        /// <summary>A point of the system of a normal's plane in the world, by the arbitrary axis algorithm.</summary>
        internal static GeoPoint3 Ocs(GeoVector3 normal, GeoPoint3 p)
        {
            bool nearZ = Math.Abs(normal.X) < 1.0 / 64.0 && Math.Abs(normal.Y) < 1.0 / 64.0;
            GeoVector3 u = (nearZ ? new GeoVector3(0, 1, 0) : new GeoVector3(0, 0, 1)).CrossProduct(normal).Normalize();
            GeoVector3 v = normal.CrossProduct(u).Normalize();
            return new GeoPoint3(u.X * p.X + v.X * p.Y + normal.X * p.Z, u.Y * p.X + v.Y * p.Y + normal.Y * p.Z, u.Z * p.X + v.Z * p.Y + normal.Z * p.Z);
        }

        internal static void Near(double expected, double actual)
            => Assert.True(Math.Abs(expected - actual) <= 1E-9 * Math.Max(1.0, Math.Abs(expected)), "expected " + expected.ToString("R") + ", was " + actual.ToString("R"));

        internal static void Near(GeoPoint3 expected, GeoPoint3 actual)
        {
            Near(expected.X, actual.X);
            Near(expected.Y, actual.Y);
            Near(expected.Z, actual.Z);
        }

        internal static void Near(GeoVector3 expected, GeoVector3 actual)
        {
            Near(expected.X, actual.X);
            Near(expected.Y, actual.Y);
            Near(expected.Z, actual.Z);
        }
    }
}
