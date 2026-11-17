using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The one grip of a hatch, as AutoCAD 2021's GetGripPoints gives it (probed in its console: the middle of the box the
    /// loops fill, ahead of the grips of a non-associative boundary) and its hover menu of Stretch, Hatch Scale, Hatch Angle
    /// and Origin Point, the names of AutoCAD's grip table.
    /// </summary>
    public sealed class GripMenuHatchTests
    {
        // A square 100 by 60 hatched at its inside with the pattern given: the grip stands at (50, 30).
        private static CadDocument Hatched(string pattern = "ANSI32", double scale = 2.0, double angle = 30.0)
        {
            var database = new DxfDatabase("AC1027");
            GeoPoint3[] corners =
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(100, 0, 0),
                new GeoPoint3(100, 60, 0),
                new GeoPoint3(0, 60, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                database.ModelSpace.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            CadDocument document = CadDocument.From(database, "hatch.dxf");
            document.HatchSettings = new CadHatchSettings
            {
                PatternName = pattern,
                Scale = scale,
                Angle = angle,
            };
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "HATCH", P(5, 5), Enter);
            return document;
        }

        private static DxfHatch OnlyHatch(CadDocument document) =>
            Assert.Single(document.ModelSpace.Block.OfType<DxfHatch>());

        private static string[] Texts(CadDocument document, string handle, int grip) =>
            CadGripMenus.Of(document, handle, grip).Select(i => i.Text).ToArray();

        [Fact]
        public void AHatchHasOneGrip_AtTheMiddleOfTheBoxItsLoopsFill()
        {
            CadDocument document = Hatched();

            CadGrip grip = Assert.Single(CadGrips.Of(OnlyHatch(document)));

            Assert.Equal(CadGripKind.Center, grip.Kind);
            Near(P(50, 30), grip.Point);
        }

        [Fact]
        public void TheGripOfAHatchMovesIt_OneUndoStep()
        {
            CadDocument document = Hatched();
            string handle = OnlyHatch(document).Handle;

            Assert.True(CadGrips.Drag(document, handle, 0, P(60, 40)).Succeeded);

            DxfHatch hatch = OnlyHatch(document);
            Near(P(60, 40), CadGrips.Of(hatch)[0].Point);
            Assert.Equal("GRIP", document.NextUndo.Name);
        }

        [Fact]
        public void TheMenuOfAPatternedHatch_IsStretchHatchScaleHatchAngleOriginPoint()
        {
            CadDocument document = Hatched();
            string handle = OnlyHatch(document).Handle;

            Assert.Equal(new[] { "Stretch", "Hatch Scale", "Hatch Angle", "Origin Point" }, Texts(document, handle, 0));
            Assert.Equal(
                new[]
                {
                    CadGripAction.Stretch,
                    CadGripAction.HatchScale,
                    CadGripAction.HatchAngle,
                    CadGripAction.HatchOrigin,
                },
                CadGripMenus.Of(document, handle, 0).Select(i => i.Action)
            );
            Assert.Empty(CadGripMenus.Of(document, handle, 1));
        }

        [Fact]
        public void ASolidFillHasNoMenu_ItsGripIsStretchAlone()
        {
            CadDocument document = Hatched("SOLID", 1.0, 0.0);
            DxfHatch hatch = OnlyHatch(document);

            Assert.True(hatch.IsSolidFill);
            Assert.Single(CadGrips.Of(hatch));
            Assert.Empty(CadGripMenus.Of(document, hatch.Handle, 0));
            Assert.False(CadGrips.ChangeHatchScale(document, hatch.Handle, 3.0).Succeeded);
            Assert.False(CadGrips.SetHatchOrigin(document, hatch.Handle, P(5, 5)).Succeeded);
        }

        [Fact]
        public void HatchScale_SetsThePatternScale_TheOriginKept_AsOneUndoStep()
        {
            CadDocument document = Hatched();
            string handle = OnlyHatch(document).Handle;

            Assert.True(CadGrips.SetHatchOrigin(document, handle, P(10, 5)).Succeeded);
            Assert.True(CadGrips.ChangeHatchScale(document, handle, 4.0).Succeeded);

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(4.0, hatch.PatternScale, 9);
            Assert.Equal(10.0, hatch.GetPatternDefinitionAt(0).BaseX, 6);
            Assert.Equal(5.0, hatch.GetPatternDefinitionAt(0).BaseY, 6);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(2.0, OnlyHatch(document).PatternScale, 9);
            Assert.False(CadGrips.ChangeHatchScale(document, handle, 0.0).Succeeded);
            Assert.False(CadGrips.ChangeHatchScale(document, handle, double.NaN).Succeeded);
        }

        [Fact]
        public void HatchAngle_SetsThePatternAngle_TheOriginKept()
        {
            CadDocument document = Hatched();
            string handle = OnlyHatch(document).Handle;
            Assert.True(CadGrips.SetHatchOrigin(document, handle, P(10, 5)).Succeeded);

            Assert.True(CadGrips.ChangeHatchAngle(document, handle, Math.PI / 2.0).Succeeded);

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(Math.PI / 2.0, hatch.PatternAngle, 9);
            Assert.Equal(10.0, hatch.GetPatternDefinitionAt(0).BaseX, 6);
            Assert.Equal(5.0, hatch.GetPatternDefinitionAt(0).BaseY, 6);
            Assert.False(CadGrips.ChangeHatchAngle(document, handle, double.PositiveInfinity).Succeeded);
        }

        [Fact]
        public void OriginPoint_LaysTheFirstLineFromThePoint_AsHatchSetOriginDoes()
        {
            CadDocument document = Hatched();
            string handle = OnlyHatch(document).Handle;

            Assert.True(CadGrips.SetHatchOrigin(document, handle, P(20, 20)).Succeeded);

            DxfPatternDefinition first = OnlyHatch(document).GetPatternDefinitionAt(0);
            Assert.Equal(20.0, first.BaseX, 6);
            Assert.Equal(20.0, first.BaseY, 6);
            Assert.True(document.Undo());
            Assert.Equal(0.0, OnlyHatch(document).GetPatternDefinitionAt(0).BaseX, 6);
        }

        [Fact]
        public void HatchScaleFromTheMenu_AsksForTheScale_AnumberOrADistanceFromTheGrip_ThenSetsIt()
        {
            CadDocument document = Hatched();
            string handle = OnlyHatch(document).Handle;
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction(handle, 0, CadGripAction.HatchScale));

            Assert.Equal("** HATCH SCALE **", session.History.Last());
            Assert.Equal("Specify hatch scale:", session.PromptText);
            Assert.NotNull(session.Prompt.Preview);

            // 5 along from the grip (50, 30) is a scale of 5.
            Run(session, P(55, 30));

            Assert.False(session.IsRunning);
            Assert.Equal(5.0, OnlyHatch(document).PatternScale, 9);
            Assert.Equal("GRIP", document.NextUndo.Name);
        }

        [Fact]
        public void HatchAngleFromTheMenu_TakesTheDirectionFromTheGrip_OrATypedAngle()
        {
            CadDocument document = Hatched();
            string handle = OnlyHatch(document).Handle;
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction(handle, 0, CadGripAction.HatchAngle));
            Assert.Equal("** HATCH ANGLE **", session.History.Last());
            Assert.Equal("Specify hatch angle:", session.PromptText);
            Run(session, P(50, 80));

            Assert.False(session.IsRunning);
            Assert.Equal(Math.PI / 2.0, OnlyHatch(document).PatternAngle, 9);

            Assert.True(session.StartGripAction(handle, 0, CadGripAction.HatchAngle));
            Run(session, "45");
            Assert.Equal(Math.PI / 4.0, OnlyHatch(document).PatternAngle, 9);
        }

        [Fact]
        public void OriginPointFromTheMenu_AsksForAPoint_AndEscLeavesTheHatchAlone()
        {
            CadDocument document = Hatched();
            string handle = OnlyHatch(document).Handle;
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction(handle, 0, CadGripAction.HatchOrigin));
            Assert.Equal("** ORIGIN POINT **", session.History.Last());
            Assert.Equal("Specify origin point:", session.PromptText);
            Run(session, Esc);
            Assert.False(session.IsRunning);
            Assert.Equal(0.0, OnlyHatch(document).GetPatternDefinitionAt(0).BaseX, 6);

            Assert.True(session.StartGripAction(handle, 0, CadGripAction.HatchOrigin));
            Run(session, P(12, 7));
            Assert.Equal(12.0, OnlyHatch(document).GetPatternDefinitionAt(0).BaseX, 6);
            Assert.Equal(7.0, OnlyHatch(document).GetPatternDefinitionAt(0).BaseY, 6);
        }
    }
}
