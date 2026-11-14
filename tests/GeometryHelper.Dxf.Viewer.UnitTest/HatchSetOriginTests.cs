using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// HATCHSETORIGIN as AutoCAD 2021 runs it (probed in its console): "Select hatch objects:", "Select new hatch origin:", and
    /// the pattern lines of every hatch picked moved by what the origin moves, so that setting it again does not add up.
    /// </summary>
    public sealed class HatchSetOriginTests
    {
        private static CadDocument Square(CadHatchSettings settings = null)
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

            CadDocument document = CadDocument.From(database, "origin.dxf");

            if (settings != null)
            {
                document.HatchSettings = settings;
            }

            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "HATCH", P(5, 5), Enter);
            return document;
        }

        private static DxfHatch OnlyHatch(CadDocument document) =>
            Assert.Single(document.ModelSpace.Block.OfType<DxfHatch>());

        private static CadCommandSession SetOrigin(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "HATCHSETORIGIN");
            return Run(session, inputs);
        }

        [Fact]
        public void TheHatchPicked_AndThePoint_LayTheFirstLineFromThePoint()
        {
            CadDocument document = Square();
            DxfHatch hatch = OnlyHatch(document);
            DxfPatternDefinition before = hatch.GetPatternDefinitionAt(0);
            CadCommandSession session = Session(document);
            Run(session, "HATCHSETORIGIN");
            Assert.Equal("Select hatch objects:", session.PromptText);
            Run(session, new[] { hatch.Handle }, Enter);
            Assert.Equal("Select new hatch origin:", session.PromptText);
            Run(session, P(10, 5));

            Assert.False(session.IsRunning);
            DxfPatternDefinition after = OnlyHatch(document).GetPatternDefinitionAt(0);
            Assert.Equal(10.0, after.BaseX, 9);
            Assert.Equal(5.0, after.BaseY, 9);
            Assert.Equal(before.OffsetX, after.OffsetX, 12);
            Assert.Equal(before.OffsetY, after.OffsetY, 12);
            Assert.Equal(before.Angle, after.Angle, 12);
            Assert.Equal("HATCHSETORIGIN", document.NextUndo.Name);

            document.Undo();
            Assert.Equal(0.0, OnlyHatch(document).GetPatternDefinitionAt(0).BaseX, 12);
        }

        [Fact]
        public void ANSI32AtThirtyDegreesAndScaleTwo_KeepsItsSecondLineBesideTheFirst_OnEveryOrigin()
        {
            // AutoCAD 2021 (its imperial ANSI32): the first line's base is the origin, the second keeps its distance from it, and
            // setting (0, 0) again gives the first pair back, so that origins do not add up.
            CadDocument document = Square(
                new CadHatchSettings
                {
                    PatternName = "ANSI32",
                    Scale = 2.0,
                    Angle = 30.0,
                }
            );
            string handle = OnlyHatch(document).Handle;
            DxfPatternDefinition first = OnlyHatch(document).GetPatternDefinitionAt(0);
            DxfPatternDefinition second = OnlyHatch(document).GetPatternDefinitionAt(1);
            Assert.Equal(2, OnlyHatch(document).NumberOfPatternDefinitions);
            Assert.True(second.BaseX - first.BaseX > 1.0);

            foreach (GeoPoint2 origin in new[] { P(10, 5), P(20, 20), P(0, 0) })
            {
                SetOrigin(document, new[] { handle }, Enter, origin);
                DxfHatch hatch = OnlyHatch(document);
                Assert.Equal(origin.X, hatch.GetPatternDefinitionAt(0).BaseX, 6);
                Assert.Equal(origin.Y, hatch.GetPatternDefinitionAt(0).BaseY, 6);
                Assert.Equal(origin.X + second.BaseX - first.BaseX, hatch.GetPatternDefinitionAt(1).BaseX, 6);
                Assert.Equal(origin.Y + second.BaseY - first.BaseY, hatch.GetPatternDefinitionAt(1).BaseY, 6);
                Assert.Equal(first.OffsetX, hatch.GetPatternDefinitionAt(0).OffsetX, 9);
                Assert.Equal(first.OffsetY, hatch.GetPatternDefinitionAt(0).OffsetY, 9);
            }
        }

        [Fact]
        public void LinesMadeByTheUser_FollowTheirSpacingAlone_AndAreLeftAsTheyAre()
        {
            // AutoCAD moves their base points too; the library keeps a user's lines to the spacing, so nothing is moved and
            // nothing is said.
            CadDocument document = Square(
                new CadHatchSettings
                {
                    Type = CadHatchType.UserDefined,
                    Angle = 30.0,
                    Spacing = 4.0,
                }
            );
            DxfHatch hatch = OnlyHatch(document);

            CadCommandSession session = SetOrigin(document, new[] { hatch.Handle }, Enter, P(10, 5));

            Assert.False(session.IsRunning);
            Assert.DoesNotContain(session.History, line => line.Contains("pattern made by the user"));
            Assert.Equal(0.0, OnlyHatch(document).GetPatternDefinitionAt(0).BaseX, 12);
            Assert.Equal(4.0, OnlyHatch(document).PatternSpace, 12);
        }

        [Fact]
        public void ASolidFill_IsLeftAsItIs_AndOnlyTheHatchesAreTaken()
        {
            CadDocument document = Square(new CadHatchSettings { Type = CadHatchType.Solid });
            DxfHatch hatch = OnlyHatch(document);
            DxfLine line = document.ModelSpace.Block.OfType<DxfLine>().First();
            CadCommandSession session = SetOrigin(document, new[] { line.Handle, hatch.Handle }, Enter, P(10, 5));

            Assert.False(session.IsRunning);
            Assert.True(OnlyHatch(document).IsSolidFill);
            Assert.Equal(0, OnlyHatch(document).NumberOfPatternDefinitions);
        }

        [Fact]
        public void EnterWithNoHatchPicked_EndsTheCommand_AndALineAlone_IsNotAHatch()
        {
            CadDocument document = Square();
            DxfLine line = document.ModelSpace.Block.OfType<DxfLine>().First();
            CadCommandSession session = SetOrigin(document, new[] { line.Handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(0.0, OnlyHatch(document).GetPatternDefinitionAt(0).BaseX, 12);
        }

        [Fact]
        public void AHatchSelectedBeforeTheCommand_IsTaken_AndTheCommandAsksTheOriginAtOnce()
        {
            CadDocument document = Square();
            DxfHatch hatch = OnlyHatch(document);
            document.Selection.Set(new[] { hatch.Handle });
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "HATCHSETORIGIN");

            Assert.Equal("Select new hatch origin:", session.PromptText);
            Run(session, P(3, 4));
            Assert.Equal(3.0, OnlyHatch(document).GetPatternDefinitionAt(0).BaseX, 9);
            Assert.Equal(4.0, OnlyHatch(document).GetPatternDefinitionAt(0).BaseY, 9);
        }

        [Fact]
        public void AHatchOnALockedLayer_IsSaidSoAndNotMoved()
        {
            CadDocument document = Square();
            DxfHatch hatch = OnlyHatch(document);
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "LOCKED" });
            hatch.Layer = "LOCKED";
            document.Database.LayerTable["LOCKED"].IsLocked = true;

            CadCommandSession session = SetOrigin(document, new[] { hatch.Handle }, Enter);

            Assert.Contains("1 was on a locked layer.", session.History);
            Assert.False(session.IsRunning);
            Assert.Equal(0.0, OnlyHatch(document).GetPatternDefinitionAt(0).BaseX, 9);
        }
    }
}
