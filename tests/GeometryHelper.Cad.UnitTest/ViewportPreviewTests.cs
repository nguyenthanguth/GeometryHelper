using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// A viewport of a layout as the edit that drags it will leave it, which the ghost and the previews of STRETCH and a grip draw
    /// under the cursor: the frame and the model shown through it. A viewport 200 by 100 about (110, 60) on Layout1 at 1:50, showing
    /// model space about (1 000, 500), the model a rectangle 2 000 by 1 000. Each case is checked against what the edit then
    /// makes, so that the preview shows the result and not a guess at it.
    /// </summary>
    public class ViewportPreviewTests
    {
        private static readonly GeoPoint2 ModelPoint = new GeoPoint2(1234.0, 567.0);

        private static CadDocument WithViewport(double twistDegrees, out CadCommandSession session, out string handle)
        {
            CadDocument document = Blank();
            session = Session(document);
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            DxfViewport viewport = All<DxfViewport>(document.CurrentSpace).Last();
            viewport.CustomScale = 0.02;
            viewport.TwistAngle = twistDegrees * Math.PI / 180.0;
            handle = viewport.Handle;
            return document;
        }

        private static DisplayViewport2 Shown(CadDocument document, string handle) =>
            document.GetDisplay(document.CurrentSpace).Items.OfType<DisplayViewport2>().Single(v => v.Handle == handle);

        /// <summary>The viewport as the preview shows it once an edit does <paramref name="m"/> to the sheet, its view's target at the origin.</summary>
        private static DisplayViewport2 AfterEdit(DisplayViewport2 viewport, Affine2 m) =>
            CadViewportPreview.After(viewport, m, new GeoPoint2(0, 0));

        private static GeoPoint2 Paper(DisplayViewport2 viewport)
        {
            Affine2 m = viewport.ModelToPaper;
            return new GeoPoint2(m.X(ModelPoint.X, ModelPoint.Y), m.Y(ModelPoint.X, ModelPoint.Y));
        }

        /// <summary>The viewport the preview makes, and the one the edit leaves, agree on the frame and on where the model stands.</summary>
        private static void AssertSame(DisplayViewport2 expected, DisplayViewport2 actual)
        {
            Assert.Equal(expected.Bounds.MinX, actual.Bounds.MinX, 6);
            Assert.Equal(expected.Bounds.MinY, actual.Bounds.MinY, 6);
            Assert.Equal(expected.Bounds.MaxX, actual.Bounds.MaxX, 6);
            Assert.Equal(expected.Bounds.MaxY, actual.Bounds.MaxY, 6);
            Assert.Equal(expected.Scale, actual.Scale, 9);
            GeoPoint2 was = Paper(expected);
            GeoPoint2 now = Paper(actual);
            Assert.Equal(was.X, now.X, 6);
            Assert.Equal(was.Y, now.Y, 6);

            // The model's orientation: the point a model unit to the right of the first stands where the edit puts it.
            Affine2 e = expected.ModelToPaper;
            Affine2 a = actual.ModelToPaper;
            Assert.Equal(e.X(ModelPoint.X + 100, ModelPoint.Y), a.X(ModelPoint.X + 100, ModelPoint.Y), 6);
            Assert.Equal(e.Y(ModelPoint.X + 100, ModelPoint.Y), a.Y(ModelPoint.X + 100, ModelPoint.Y), 6);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void AMove_IsPreviewedAsTheFrameAndTheModelMovedTogether(double twist)
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            DisplayViewport2 before = Shown(document, handle);
            DisplayViewport2 preview = AfterEdit(before, Affine2.Translation(30, -20));

            Run(session, "MOVE", new[] { handle }, Enter, P(0, 0), P(30, -20));

            AssertSame(Shown(document, handle), preview);
            Assert.Equal(before.Bounds.MinX + 30.0, preview.Bounds.MinX, 6);
            GeoPoint2 was = Paper(before);
            Assert.Equal(was.X + 30.0, Paper(preview).X, 6);
            Assert.Equal(was.Y - 20.0, Paper(preview).Y, 6);
        }

        [Theory]
        [InlineData(0.0, 10.0, 10.0)]
        [InlineData(30.0, 10.0, 10.0)]
        [InlineData(0.0, 110.0, 60.0)]
        public void AScale_IsPreviewedAsTheFrameScaledAndTheModelStillOnTheSheet(double twist, double x, double y)
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            DisplayViewport2 before = Shown(document, handle);
            DisplayViewport2 preview = AfterEdit(before, new Affine2(2, 0, 0, 2, -x, -y));

            Run(session, "SCALE", new[] { handle }, Enter, P(x, y), "2");

            AssertSame(Shown(document, handle), preview);
            Assert.Equal(400.0, preview.Width, 6);
            GeoPoint2 was = Paper(before);
            Assert.Equal(was.X, Paper(preview).X, 6);
            Assert.Equal(was.Y, Paper(preview).Y, 6);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void AMirror_IsPreviewedAsTheFrameCentreMirroredAndTheModelCarriedWithItNotMirrored(double twist)
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            DisplayViewport2 preview = AfterEdit(Shown(document, handle), new Affine2(-1, 0, 0, 1, 600, 0));

            // MIRROR keeps the source by default: the mirrored viewport is a copy.
            Run(session, "MIRROR", new[] { handle }, Enter, P(300, 0), P(300, 100), Enter);

            DisplayViewport2 made = document
                .GetDisplay(document.CurrentSpace)
                .Items.OfType<DisplayViewport2>()
                .Single(v => v.Handle != handle);
            AssertSame(made, preview);
            Assert.Equal(490.0, preview.CenterX, 6);
        }

        [Theory]
        [InlineData(0.0, 90.0)]
        [InlineData(30.0, 90.0)]
        [InlineData(0.0, 180.0)]
        [InlineData(0.0, 30.0)]
        [InlineData(30.0, 50.0)]
        public void ARotation_IsPreviewedAsTheFrameTurnedAndTheModelAsTheEditLeavesIt(double twist, double degrees)
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            double angle = degrees * Math.PI / 180.0;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            var about = new GeoPoint2(20, 30);
            var turn = new Affine2(
                cos,
                sin,
                -sin,
                cos,
                about.X - (cos * about.X - sin * about.Y),
                about.Y - (sin * about.X + cos * about.Y)
            );
            DisplayViewport2 preview = AfterEdit(Shown(document, handle), turn);

            Run(
                session,
                "ROTATE",
                new[] { handle },
                Enter,
                about,
                degrees.ToString(System.Globalization.CultureInfo.InvariantCulture)
            );

            AssertSame(Shown(document, handle), preview);
        }

        [Fact]
        public void ARotationByAnEighthOfATurn_IsPreviewedAsTheFrameTurned_AndItsOutlineIsThePlainFrameTurned()
        {
            CadDocument document = WithViewport(0.0, out _, out string handle);
            double cos = Math.Cos(Math.PI / 4.0);
            var turn = new Affine2(cos, cos, -cos, cos, 0, 0);

            DisplayViewport2 preview = AfterEdit(Shown(document, handle), turn);

            // The frame's corners, turned an eighth of a turn about the origin, make the outline the viewport is clipped to.
            Assert.NotNull(preview.Clip);
            Assert.Equal(4, preview.Clip.Count);
            Assert.Equal(turn.X(10, 10), preview.Clip.X(0), 6);
            Assert.Equal(turn.Y(10, 10), preview.Clip.Y(0), 6);
        }

        [Fact]
        public void AStretchOfOneCorner_IsPreviewedAsTheFrameResizedAndTheModelStillOnTheSheet()
        {
            CadDocument document = WithViewport(30.0, out CadCommandSession session, out string handle);
            DisplayViewport2 before = Shown(document, handle);
            Run(session, "STRETCH", "C", P(200, 0), P(220, 20), Enter, P(0, 0));

            DisplayViewport2 preview = Assert.IsType<DisplayViewport2>(
                Assert.Single(session.Prompt.Preview.Items(P(30, -30)))
            );

            // AutoCAD 2021: the frame 230 by 130 about (125, 45), the model where it was on the sheet.
            Assert.Equal(230.0, preview.Width, 6);
            Assert.Equal(130.0, preview.Height, 6);
            Assert.Equal(125.0, preview.CenterX, 6);
            Assert.Equal(45.0, preview.CenterY, 6);
            Assert.Equal(Paper(before).X, Paper(preview).X, 6);
            Assert.Equal(Paper(before).Y, Paper(preview).Y, 6);
            Assert.Equal(before.Model.Items.Count, preview.Model.Items.Count);
        }

        /// <summary>The prompt a click on a grip of the viewport asks its point at, STRETCH being the first mode of a grip edit.</summary>
        private static CadPreview GripPreview(CadDocument document, CadCommandSession session, string handle, int grip)
        {
            Assert.True(
                session.StartGripEdit(new[] { new System.Collections.Generic.KeyValuePair<string, int>(handle, grip) })
            );
            Assert.StartsWith("Specify stretch point", session.PromptText);
            return session.Prompt.Preview;
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(30.0)]
        public void ACornerGripDragged_IsPreviewedAsTheFrameResizedAndTheModelStillOnTheSheet(double twist)
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            DisplayViewport2 before = Shown(document, handle);
            CadPreview preview = GripPreview(document, session, handle, 1);

            Assert.NotNull(preview);
            DisplayViewport2 shown = Assert.IsType<DisplayViewport2>(Assert.Single(preview.Items(P(240, -20))));

            // As the drag itself leaves it (ViewportStretchTests): 230 by 130 about (125, 45).
            Assert.Equal(230.0, shown.Width, 6);
            Assert.Equal(130.0, shown.Height, 6);
            Assert.Equal(125.0, shown.CenterX, 6);
            Assert.Equal(45.0, shown.CenterY, 6);
            Assert.Equal(Paper(before).X, Paper(shown).X, 6);
            Assert.Equal(Paper(before).Y, Paper(shown).Y, 6);
            Assert.True(document.GripDrag(handle, 1, P(240, -20)).Succeeded);
            AssertSame(Shown(document, handle), shown);
        }

        [Fact]
        public void TheMiddleGripDragged_IsPreviewedAsTheFrameMovedWithItsModel()
        {
            CadDocument document = WithViewport(30.0, out CadCommandSession session, out string handle);
            DisplayViewport2 before = Shown(document, handle);
            CadPreview preview = GripPreview(document, session, handle, 4);

            DisplayViewport2 shown = Assert.IsType<DisplayViewport2>(Assert.Single(preview.Items(P(150, 90))));

            Assert.Equal(150.0, shown.CenterX, 6);
            Assert.Equal(90.0, shown.CenterY, 6);
            Assert.Equal(Paper(before).X + 40.0, Paper(shown).X, 6);
            Assert.Equal(Paper(before).Y + 30.0, Paper(shown).Y, 6);
        }

        [Theory]
        [InlineData("MOVE", 0.0)]
        [InlineData("MOVE", 30.0)]
        [InlineData("SCALE", 0.0)]
        [InlineData("SCALE", 30.0)]
        [InlineData("ROTATE", 0.0)]
        [InlineData("ROTATE", 30.0)]
        public void AClippedViewportEditedWithItsBoundary_IsPreviewedAsTheEditLeavesIt(string command, double twist)
        {
            CadDocument document = WithViewport(twist, out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            Run(session, P(30, 30), P(190, 30), P(190, 90), P(30, 90), Enter);
            string border = Entity<DxfViewport>(document, handle).NonRectClipEntity.Handle;
            DisplayViewport2 before = Shown(document, handle);
            GeoPoint3 target = Entity<DxfViewport>(document, handle).ViewTarget;
            var handles = new[] { handle, border };
            Affine2 m;

            switch (command)
            {
                case "MOVE":
                    m = Affine2.Translation(30, -20);
                    Run(session, "MOVE", handles, Enter, P(0, 0), P(30, -20));
                    break;
                case "SCALE":
                    m = new Affine2(2, 0, 0, 2, -10, -10);
                    Run(session, "SCALE", handles, Enter, P(10, 10), "2");
                    break;
                default:
                    double cos = Math.Cos(Math.PI / 6.0);
                    double sin = Math.Sin(Math.PI / 6.0);
                    m = new Affine2(cos, sin, -sin, cos, 20 - (cos * 20 - sin * 30), 30 - (sin * 20 + cos * 30));
                    Run(session, "ROTATE", handles, Enter, P(20, 30), "30");
                    break;
            }

            DisplayViewport2 preview = CadViewportPreview.After(before, m, new GeoPoint2(target.X, target.Y));
            DisplayViewport2 made = Shown(document, handle);

            AssertSame(made, preview);
            Assert.NotNull(preview.Clip);

            // The outline is the boundary as the edit leaves it, vertex for vertex.
            for (int i = 0; i < made.Clip.Count; i++)
            {
                Assert.Equal(made.Clip.X(i), preview.Clip.X(i), 6);
                Assert.Equal(made.Clip.Y(i), preview.Clip.Y(i), 6);
            }
        }

        [Fact]
        public void APastedViewport_IsPreviewedAtTheCursor_AsThePasteMakesIt()
        {
            CadDocument source = WithViewport(30.0, out CadCommandSession copy, out string handle);
            var clipboard = new CadMemoryClipboard();
            copy.Clipboard = clipboard;
            Run(copy, "COPYCLIP", new[] { handle }, Enter);

            // Another drawing's layout, with a model of its own.
            CadDocument target = Blank();
            CadCommandSession session = Session(target);
            session.Clipboard = clipboard;
            Run(session, "REC", P(0, 0), P(500, 500));
            target.CurrentSpace = target.Spaces[1];
            Run(session, "PASTECLIP");

            DisplayViewport2 preview = Assert.IsType<DisplayViewport2>(
                Assert.Single(session.Prompt.Preview.Items(P(300, 200)))
            );
            session.Cursor = P(300, 200);
            Run(session, P(300, 200));

            DisplayViewport2 made = target
                .GetDisplay(target.CurrentSpace)
                .Items.OfType<DisplayViewport2>()
                .Single(v => v.Handle != null && v.Width > 0 && v.CenterX > 150);
            Assert.Equal(made.CenterX, preview.CenterX, 6);
            Assert.Equal(made.CenterY, preview.CenterY, 6);
            Assert.Equal(made.Width, preview.Width, 6);
            Assert.Equal(Paper(made).X, Paper(preview).X, 6);
            Assert.Equal(Paper(made).Y, Paper(preview).Y, 6);
        }

        [Fact]
        public void ACornerGripPulledToTheOppositeCorner_PreviewsNothing()
        {
            CadDocument document = WithViewport(0.0, out CadCommandSession session, out string handle);
            CadPreview preview = GripPreview(document, session, handle, 1);

            Assert.Empty(preview.Items(P(10, 40)));
        }

        [Fact]
        public void TheMiddleGripOfAClippedViewportDragged_PreviewsTheViewportAndItsBoundaryMovedTogether()
        {
            CadDocument document = WithViewport(0.0, out CadCommandSession session, out string handle);
            Run(session, "VPCLIP", new[] { handle }, Enter);
            Run(session, P(30, 30), P(190, 30), P(190, 90), P(30, 90), Enter);
            string border = Entity<DxfViewport>(document, handle).NonRectClipEntity.Handle;
            CadPreview preview = GripPreview(document, session, handle, 0);

            Assert.NotNull(preview);
            Assert.Equal(new[] { handle, border }.OrderBy(h => h, StringComparer.OrdinalIgnoreCase), preview.Handles);
            Affine2 move = Assert.Single(preview.Moves(P(140, 70)));
            Assert.Equal(30.0, move.E, 6);
            Assert.Equal(10.0, move.F, 6);
        }
    }
}
