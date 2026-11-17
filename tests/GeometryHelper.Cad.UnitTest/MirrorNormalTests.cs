using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// MIRROR of a circle, an arc, a polyline, a SOLID and an MTEXT across the line x = 40, as AutoCAD 2021's MIRROR leaves
    /// them (probed in its console, mirror-8, MIRRTEXT 0 and 1): the normal the image of the old one, so that a circle in the
    /// drawing's plane keeps (0, 0, 1); an MTEXT readable under MIRRTEXT 0, held by the opposite side, and mirrored in its
    /// plane under 1, its normal turned over. The picture of each is the same as it was drawn before the entity's own
    /// <c>TransformBy</c> took the image of the normal.
    /// </summary>
    public class MirrorNormalTests
    {
        private static readonly Tolerance Tol = new Tolerance(1E-9, 1E-9);

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(actual.IsEqualTo(expected, Tol), actual + " is not " + expected);

        private static void Near(GeoVector3 expected, GeoVector3 actual) =>
            Assert.True(actual.IsEqualTo(expected, Tol), actual + " is not " + expected);

        /// <summary>Mirrors the entity across x = 40, keeping it; gives the copy.</summary>
        private static T Mirrored<T>(CadDocument document, DxfEntity source)
            where T : DxfEntity
        {
            var before = new HashSet<string>(All<T>(document.ModelSpace).Select(e => e.Handle));
            Run(Session(document), "MIRROR", new[] { source.Handle }, Enter, P(40, 0), P(40, 10), Enter);
            return All<T>(document.ModelSpace).Single(e => !before.Contains(e.Handle));
        }

        [Fact]
        public void ACircleMirroredAcrossAVerticalLine_KeepsItsNormalUp()
        {
            var db = new DxfDatabase("AC1027");
            var circle = new DxfCircle(new GeoPoint3(50, 50, 0), new GeoVector3(0, 0, 1), 5.0);
            db.ModelSpace.AppendEntity(circle);
            CadDocument document = CadDocument.From(db, "circle.dxf");

            DxfCircle copy = Mirrored<DxfCircle>(document, circle);

            // AutoCAD 2021: the centre (30, 50, 0) and no 210 group.
            Near(new GeoPoint3(30, 50, 0), copy.Center);
            Near(new GeoVector3(0, 0, 1), copy.Normal);
        }

        [Fact]
        public void AnArcMirroredAcrossAVerticalLine_KeepsItsNormalUp_AndRunsFrom60To150Degrees()
        {
            var db = new DxfDatabase("AC1027");
            var arc = new DxfArc(
                new GeoPoint3(60, 30, 0),
                new GeoVector3(0, 0, 1),
                8.0,
                30.0 * Math.PI / 180.0,
                120.0 * Math.PI / 180.0
            );
            db.ModelSpace.AppendEntity(arc);
            CadDocument document = CadDocument.From(db, "arc.dxf");

            DxfArc copy = Mirrored<DxfArc>(document, arc);

            // AutoCAD 2021: the centre (20, 30, 0), 50 of 1.0472 and 51 of 2.61799 radians, no 210 group.
            Near(new GeoPoint3(20, 30, 0), copy.Center);
            Near(new GeoVector3(0, 0, 1), copy.Normal);
            Assert.Equal(60.0 * Math.PI / 180.0, copy.StartAngle, 9);
            Assert.Equal(150.0 * Math.PI / 180.0, copy.EndAngle, 9);
        }

        [Fact]
        public void AnMTextMirroredAcrossAVerticalLine_IsHeldByItsTopRight_UnderMirrtextZero()
        {
            var db = new DxfDatabase("AC1027");
            var text = new DxfMText();
            db.ModelSpace.AppendEntity(text);
            text.Contents = "ABC DEF";
            text.TextHeight = 3.0;
            text.Width = 20.0;
            text.Location = new GeoPoint3(20, 40, 0);
            CadDocument document = CadDocument.From(db, "mtext.dxf");

            DxfMText copy = Mirrored<DxfMText>(document, text);

            // AutoCAD 2021 (MIRRTEXT 0): at (60, 40), reading along (1, 0, 0), held by 3, the top right; the normal (0, 0, 1).
            Near(new GeoPoint3(60, 40, 0), copy.Location);
            Near(new GeoVector3(1, 0, 0), copy.Direction);
            Near(new GeoVector3(0, 0, 1), copy.Normal);
            Assert.Equal(DxfAttachmentPoint.TopRight, copy.Attachment);
        }

        [Fact]
        public void AnMTextMirroredAcrossAVerticalLine_IsMirroredInItsPlane_UnderMirrtextOne()
        {
            var db = new DxfDatabase("AC1027");
            db.Mirrtext = true;
            var text = new DxfMText();
            db.ModelSpace.AppendEntity(text);
            text.Contents = "ABC DEF";
            text.TextHeight = 3.0;
            text.Width = 20.0;
            text.Location = new GeoPoint3(20, 40, 0);
            CadDocument document = CadDocument.From(db, "mtext.dxf");

            DxfMText copy = Mirrored<DxfMText>(document, text);

            // AutoCAD 2021 (MIRRTEXT 1): at (60, 40), 11 of (-1, 0, 0), 210 of (0, 0, -1), still held by 1, the top left.
            Near(new GeoPoint3(60, 40, 0), copy.Location);
            Near(new GeoVector3(-1, 0, 0), copy.Direction);
            Near(new GeoVector3(0, 0, -1), copy.Normal);
            Assert.Equal(DxfAttachmentPoint.TopLeft, copy.Attachment);
        }

        [Theory]
        [InlineData(false, 1.0, DxfAttachmentPoint.TopRight, 1.0)]
        [InlineData(true, -1.0, DxfAttachmentPoint.TopLeft, -1.0)]
        public void AnMTextOfNormalY_MirroredAcrossAVerticalLine_FollowsMirrtext(
            bool mirrtext,
            double directionX,
            DxfAttachmentPoint attachment,
            double normalSign
        )
        {
            var db = new DxfDatabase("AC1027");
            db.Mirrtext = mirrtext;
            var text = new DxfMText();
            db.ModelSpace.AppendEntity(text);
            text.Normal = new GeoVector3(0, 1, 0);
            text.Contents = "ABC DEF";
            text.TextHeight = 3.0;
            text.Width = 20.0;
            text.Location = new GeoPoint3(20, 7, 40);
            text.Direction = new GeoVector3(1, 0, 0);
            CadDocument document = CadDocument.From(db, "mtext.dxf");

            DxfMText copy = Mirrored<DxfMText>(document, text);

            // AutoCAD 2021 (MIRRTEXT 0 and 1): at (60, 7, 40); readable under 0, the normal (0, 1, 0) kept and the point it is
            // held by turned to the top right; under 1 the box reflected, reading along (-1, 0, 0) of the normal (0, -1, 0).
            Near(new GeoPoint3(60, 7, 40), copy.Location);
            Near(new GeoVector3(directionX, 0, 0), copy.Direction);
            Near(new GeoVector3(0, normalSign, 0), copy.Normal);
            Assert.Equal(attachment, copy.Attachment);
        }
    }
}
