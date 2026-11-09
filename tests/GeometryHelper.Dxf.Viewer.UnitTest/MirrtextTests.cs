using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MIRROR with the drawing's MIRRTEXT, as AutoCAD 2021 runs it (probed in its console): at 0, its default, a TEXT, an
    /// MTEXT and the attributes of an INSERT come out readable, the box of their words mirrored, the text turned within the
    /// half of the circle it read in before; at 1 they are mirrored with the rest.
    /// </summary>
    public class MirrtextTests
    {
        private const double Degree = Math.PI / 180.0;

        private static CadDocument WithText(bool mirrtext, Action<DxfDatabase> add)
        {
            var db = new DxfDatabase("AC1027");
            db.Mirrtext = mirrtext;
            add(db);
            return CadDocument.From(db, "mirrtext.dxf");
        }

        private static DxfDBText Text(DxfDatabase db, double x, double y, double degrees, string words = "ABC")
        {
            var text = new DxfDBText();
            db.ModelSpace.AppendEntity(text);
            text.TextString = words;
            text.Height = 2.5;
            text.Position = new GeoPoint3(x, y, 0);
            text.Rotation = degrees * Degree;
            return text;
        }

        /// <summary>Mirrors the one object of the document across the line through two points, keeping the source; gives the copy.</summary>
        private static T MirroredCopy<T>(CadDocument document, GeoPoint2 first, GeoPoint2 second)
            where T : DxfEntity
        {
            string source = All<T>(document.ModelSpace)[0].Handle;
            Run(Session(document), "MIRROR", new[] { source }, Enter, first, second, Enter);
            return All<T>(document.ModelSpace).Single(e => e.Handle != source);
        }

        private static void Readable(DxfDBText text, double degrees)
        {
            Assert.True(text.Normal.Z > 0.0, "The normal points down: " + text.Normal + ".");
            Assert.False(text.IsMirroredInX);
            Assert.False(text.IsMirroredInY);
            double turned = (text.Rotation / Degree % 360.0 + 360.0) % 360.0;
            Assert.True(Math.Abs(turned - degrees) < 1E-6, "Turned " + turned + " degrees, not " + degrees + ".");
        }

        [Fact]
        public void ATextTurnedThirtyDegrees_MirroredAcrossAnUpright_ReadsAtThreeHundredAndThirty()
        {
            CadDocument document = WithText(false, db => Text(db, 0, 0, 30));

            DxfDBText copy = MirroredCopy<DxfDBText>(document, P(10, 0), P(10, 10));

            Readable(copy, 330);
        }

        [Fact]
        public void ATextTurnedThirtyDegrees_MirroredAcrossALevelLine_ReadsAtThreeHundredAndThirty_ItsBaselineMirroredAboutItsMiddle()
        {
            CadDocument document = WithText(false, db => Text(db, 0, 0, 30));

            DxfDBText copy = MirroredCopy<DxfDBText>(document, P(0, 10), P(10, 10));

            // AutoCAD 2021 put it at (-1.25, 17.8349): the text's own height up its upright, mirrored.
            Readable(copy, 330);
            Near(new GeoPoint3(-1.25, 20.0 - 2.5 * Math.Sin(60 * Degree), 0), copy.Position, 1E-9);
        }

        [Fact]
        public void ATextReadingRightToLeft_StaysInThatHalf_AsAutoCadTurnsOneOfAHundredAndTwentyDegreesToTwoHundredAndForty()
        {
            CadDocument document = WithText(false, db => Text(db, 0, 0, 120));

            Readable(MirroredCopy<DxfDBText>(document, P(10, 0), P(10, 10)), 240);
        }

        [Fact]
        public void AnAlignedText_HasItsTwoPointsMirroredAndSwapped()
        {
            CadDocument document = WithText(false, db =>
            {
                DxfDBText text = Text(db, 0, 0, 0);
                text.HorizontalMode = DxfTextHorizontalMode.TextAlign;
                text.AlignmentPoint = new GeoPoint3(20, 0, 0);
            });

            DxfDBText copy = MirroredCopy<DxfDBText>(document, P(30, 0), P(30, 10));

            // AutoCAD 2021: from (0, 0) and (20, 0) across x = 30 to (40, 0) and (60, 0).
            Readable(copy, 0);
            Near(new GeoPoint3(40, 0, 0), copy.Position, 1E-9);
            Near(new GeoPoint3(60, 0, 0), copy.AlignmentPoint, 1E-9);
        }

        [Fact]
        public void AnMText_IsPutAtItsPointMirrored_ItsAttachmentMirroredLeftForRightOrTopForBottom()
        {
            Action<DxfDatabase> add = db =>
            {
                var text = new DxfMText();
                db.ModelSpace.AppendEntity(text);
                text.Contents = "ABC";
                text.TextHeight = 2.5;
                text.Width = 30;
                text.Location = new GeoPoint3(0, 0, 0);
                text.Attachment = DxfAttachmentPoint.TopLeft;
            };

            DxfMText across = MirroredCopy<DxfMText>(WithText(false, add), P(40, 0), P(40, 10));
            DxfMText down = MirroredCopy<DxfMText>(WithText(false, add), P(0, 10), P(10, 10));

            // AutoCAD 2021: top left at the origin across x = 40 to top right at (80, 0); across y = 10 to bottom left at (0, 20).
            Near(new GeoPoint3(80, 0, 0), across.Location, 1E-9);
            Assert.Equal(DxfAttachmentPoint.TopRight, across.Attachment);
            Assert.True(across.Direction.X > 1.0 - 1E-9 && across.Normal.Z > 0.0);
            Near(new GeoPoint3(0, 20, 0), down.Location, 1E-9);
            Assert.Equal(DxfAttachmentPoint.BottomLeft, down.Attachment);
            Assert.True(down.Direction.X > 1.0 - 1E-9 && down.Normal.Z > 0.0);
        }

        [Fact]
        public void TheAttributeOfAnInsertMirrored_StaysReadable()
        {
            CadDocument document = WithText(false, db =>
            {
                var block = new DxfBlockTableRecord { Name = "B1" };
                db.BlockTable.Add(block);
                block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1.0));
                var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
                db.ModelSpace.AppendEntity(insert);
                var value = new DxfAttributeReference(new GeoPoint3(0, 0, 0), "VALUE", "TAG", null);
                insert.AttributeCollection.AppendAttribute(value);
                value.Height = 2.5;
            });

            DxfBlockReference copy = MirroredCopy<DxfBlockReference>(document, P(10, 0), P(10, 10));
            DxfAttributeReference attribute = copy.AttributeCollection.Cast<DxfAttributeReference>().Single();

            // AutoCAD 2021 put "VALUE" of txt.shx at (8.7193, 0): the box of its words mirrored, its start left of x = 20.
            Readable(attribute, 0);
            Assert.InRange(attribute.Position.X, 0.0, 20.0 - 1.0);
            Assert.Equal(0.0, attribute.Position.Y, 9);
        }

        [Fact]
        public void WithMirrtextOn_ATextIsMirroredWithTheRest()
        {
            CadDocument document = WithText(true, db => Text(db, 0, 0, 0));

            DxfDBText copy = MirroredCopy<DxfDBText>(document, P(10, 0), P(10, 10));

            Assert.True(copy.Normal.Z < 0.0 || copy.IsMirroredInX, "The text was not mirrored.");
        }
    }
}
