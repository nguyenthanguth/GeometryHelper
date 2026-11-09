using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A HATCH given the lines of a pattern of the caller's own (<see cref="DxfHatch.SetPatternDefinitions"/>), and one made to
    /// follow the objects its loops were made of (<see cref="DxfHatch.SetAssociatedObjects"/>), as the presenter's HATCH makes them.
    /// </summary>
    public class DxfHatchOwnLinesTests
    {
        private static readonly GeoPoint2[] Square = { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) };

        private static DxfDatabase Reopen(DxfDatabase db)
        {
            var bytes = new MemoryStream();
            db.Save(bytes);
            bytes.Position = 0;
            return DxfDatabase.Open(bytes);
        }

        private static DxfHatch Ansi31(DxfDatabase db)
        {
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
            hatch.AppendLoop(DxfHatchLoopTypes.External, Square, null);
            db.ModelSpace.AppendEntity(hatch);
            return hatch;
        }

        [Fact]
        public void TheLinesGiven_AreWrittenAsAutoCadWritesThem_ReadBack_AndExplodeToElevenLines()
        {
            var db = new DxfDatabase("AC1032");
            DxfHatch hatch = Ansi31(db);
            Assert.Equal(0, hatch.NumberOfPatternDefinitions);

            hatch.SetPatternDefinitions(new[] { new DxfPatternDefinition(Math.PI / 4.0, 0.0, 0.0, -2.2450640303, 2.2450640303, null) });

            DxfPatternDefinition line = hatch.GetPatternDefinitionAt(0);
            Assert.Equal(Math.PI / 4.0, line.Angle, 15);
            Assert.Equal(-2.2450640303, line.OffsetX, 15);
            Assert.Empty(line.GetDashes());
            Assert.Equal(11, hatch.Explode().Count);

            string text = db.ToText();
            Assert.Contains("\n 78\n1\n 53\n45\n 43\n0\n 44\n0\n 45\n-2.2450640303\n 46\n2.2450640303\n 79\n0\n", text.Replace("\r\n", "\n"));
            DxfHatch read = Reopen(db).ModelSpace.OfType<DxfHatch>().Single();
            Assert.Equal("ANSI31", read.PatternName);
            Assert.Equal(1, read.NumberOfPatternDefinitions);
        }

        [Fact]
        public void TheLinesGiven_AreScaledAndTurnedWithThePattern_AndRefusedOnASolidOrAUserPattern()
        {
            var db = new DxfDatabase("AC1032");
            DxfHatch hatch = Ansi31(db);
            hatch.SetPatternDefinitions(new[] { new DxfPatternDefinition(0.0, 0.0, 0.0, 0.0, 2.0, new[] { 1.0, -0.5 }) });

            hatch.PatternScale = 3.0;
            DxfPatternDefinition line = hatch.GetPatternDefinitionAt(0);
            Assert.Equal(6.0, line.OffsetY, 12);
            Assert.Equal(new[] { 3.0, -1.5 }, line.GetDashes().ToArray());

            Assert.Throws<ArgumentException>(() => hatch.SetPatternDefinitions(Array.Empty<DxfPatternDefinition>()));
            Assert.Throws<ArgumentNullException>(() => hatch.SetPatternDefinitions(null));

            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            Assert.Throws<InvalidOperationException>(() => hatch.SetPatternDefinitions(new[] { new DxfPatternDefinition(0, 0, 0, 0, 1, null) }));
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            Assert.Throws<InvalidOperationException>(() => hatch.SetPatternDefinitions(new[] { new DxfPatternDefinition(0, 0, 0, 0, 1, null) }));
        }

        [Fact]
        public void AHatchMadeToFollowItsFourLines_WritesTheirHandles_AndIsAmongTheirReactors_AndLetsGoOnRemove()
        {
            var db = new DxfDatabase("AC1032");
            var lines = new List<DxfEntity>();

            for (int i = 0; i < 4; i++)
            {
                GeoPoint2 a = Square[i];
                GeoPoint2 b = Square[(i + 1) % 4];
                var line = new DxfLine(new GeoPoint3(a.X, a.Y, 0), new GeoPoint3(b.X, b.Y, 0));
                db.ModelSpace.AppendEntity(line);
                lines.Add(line);
            }

            DxfHatch hatch = Ansi31(db);
            hatch.SetAssociatedObjects(new IReadOnlyList<DxfEntity>[] { lines });

            Assert.True(hatch.Associative);
            Assert.Equal(lines.Select(l => l.Handle), hatch.GetAssociatedObjectsAt(0).Select(e => e.Handle));
            string text = db.ToText().Replace("\r\n", "\n");
            Assert.Contains("\n 97\n4\n330\n" + lines[0].Handle + "\n330\n" + lines[1].Handle + "\n", text);
            Assert.Contains("{ACAD_REACTORS\n330\n" + hatch.Handle + "\n102\n}", text);

            DxfDatabase read = Reopen(db);
            Assert.Equal(4, read.ModelSpace.OfType<DxfHatch>().Single().GetAssociatedObjects().Count);

            hatch.RemoveAssociatedObjects();
            Assert.False(hatch.Associative);
            Assert.DoesNotContain("{ACAD_REACTORS\n330\n" + hatch.Handle + "\n102\n}", db.ToText().Replace("\r\n", "\n"));
        }

        [Fact]
        public void FollowingObjects_IsRefusedForTheWrongCount_AnotherDrawing_OrAHatchInNone()
        {
            var db = new DxfDatabase("AC1032");
            var other = new DxfDatabase("AC1032");
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0));
            other.ModelSpace.AppendEntity(line);
            DxfHatch hatch = Ansi31(db);

            Assert.Throws<ArgumentException>(() => hatch.SetAssociatedObjects(new IReadOnlyList<DxfEntity>[0]));
            Assert.Throws<ArgumentException>(() => hatch.SetAssociatedObjects(new IReadOnlyList<DxfEntity>[] { new DxfEntity[] { line } }));
            Assert.Throws<ArgumentNullException>(() => hatch.SetAssociatedObjects(new IReadOnlyList<DxfEntity>[] { null }));
            Assert.Throws<InvalidOperationException>(() => new DxfHatch().SetAssociatedObjects(new IReadOnlyList<DxfEntity>[0]));
            Assert.False(hatch.Associative);
        }
    }
}
