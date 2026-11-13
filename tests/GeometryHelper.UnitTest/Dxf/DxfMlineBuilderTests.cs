using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// <see cref="DxfMlineBuilder"/> and the styles of <see cref="DxfDatabase.AddMlineStyle"/>: the groups of an MLINE and of an
    /// MLINESTYLE as AutoCAD 2021 wrote them (a Database made in accoreconsole with <c>new Mline()</c>, <c>AppendSegment</c> and
    /// <c>new MlineStyle()</c>, written by <c>DxfOut</c>), compared number for number.
    /// </summary>
    public class DxfMlineBuilderTests
    {
        private const double Close = 1E-9;
        private static readonly double Root = Math.Sqrt(0.5);

        [Fact]
        public void AStraightRunOfTheStyleStandardAtScaleOne_IsWrittenAsAutoCADWritesIt()
        {
            // AutoCAD 2021: AppendSegment (0, 0) and (100, 0), scale 1, justified at the top.
            DxfDatabase db = Blank();
            DxfUnknownEntity mline = Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(100, 0)).Build();

            Written written = Read(mline);

            Assert.Equal(db.MLStyleDictionary.GetAt("Standard").Handle, written.StyleHandle);
            Assert.Equal(1.0, written.Scale);
            Assert.Equal(0, written.Justification);
            Assert.Equal(1, written.Flags);
            Assert.Equal(2, written.Vertices.Count);
            Assert.Equal(2, written.ElementCount);
            Assert.Equal(new[] { 0.0, 0.0, 0.0 }, written.Start);
            Assert.Equal(new[] { 0.0, 0.0, 1.0 }, written.Normal);
            Vertex(written.Vertices[0], new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, 0.0, -1.0);
            Vertex(written.Vertices[1], new[] { 100.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, 0.0, -1.0);
            Assert.All(written.Vertices, v => Assert.All(v.Tails, t => Assert.Equal(new[] { 0.0 }, t)));
        }

        [Theory]
        [InlineData(DxfMlineJustification.Top, 0, 0.0, -20.0)]
        [InlineData(DxfMlineJustification.Zero, 1, 10.0, -10.0)]
        [InlineData(DxfMlineJustification.Bottom, 2, 20.0, 0.0)]
        public void AStraightRunAtScaleTwenty_PutsTheElementsOffTheLineTheJustificationNames(
            DxfMlineJustification justification,
            int written70,
            double first,
            double second
        )
        {
            // AutoCAD 2021: (0, 0) to (100, 0) at scale 20: the offsets 0.5 and -0.5 less that of the justified line, times 20.
            DxfDatabase db = Blank();

            Written written = Read(Builder(db, 20.0, justification, P(0, 0), P(100, 0)).Build());

            Assert.Equal(20.0, written.Scale);
            Assert.Equal(written70, written.Justification);
            Vertex(written.Vertices[0], new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, first, second);
            Vertex(written.Vertices[1], new[] { 100.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, first, second);
        }

        [Fact]
        public void ARightAngleCorner_HasAMiterOfTheBisectorAndOffsetsDividedByTheCosineOfTheHalfAngle()
        {
            // AutoCAD 2021: (0, 0), (100, 0), (100, 100): at the corner the miter (-0.7071, 0.7071) and the second element at -1.41421.
            DxfDatabase db = Blank();

            Written written = Read(
                Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(100, 0), P(100, 100)).Build()
            );

            Assert.Equal(3, written.Vertices.Count);
            Vertex(written.Vertices[0], new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 1, 0 }, 0.0, -1.0);
            Vertex(
                written.Vertices[1],
                new[] { 100.0, 0, 0 },
                new[] { 0.0, 1, 0 },
                new[] { -Root, Root, 0 },
                0.0,
                -1.414213562373094
            );
            Vertex(written.Vertices[2], new[] { 100.0, 100, 0 }, new[] { 0.0, 1, 0 }, new[] { -1.0, 0, 0 }, 0.0, -1.0);
        }

        [Fact]
        public void AClosedSquare_IsFlaggedThree_AndEveryCornerHasAMiter()
        {
            // AutoCAD 2021: (0, 0), (100, 0), (100, 100), (0, 100) with IsClosed: the flags are 3, the last vertex runs back to the first.
            DxfDatabase db = Blank();
            DxfMlineBuilder builder = Builder(
                db,
                1.0,
                DxfMlineJustification.Top,
                P(0, 0),
                P(100, 0),
                P(100, 100),
                P(0, 100)
            );
            builder.IsClosed = true;

            Written written = Read(builder.Build());

            Assert.Equal(3, written.Flags);
            Assert.Equal(4, written.Vertices.Count);
            double s = 1.414213562373094;
            Vertex(written.Vertices[0], new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { Root, Root, 0 }, 0.0, -s);
            Vertex(written.Vertices[1], new[] { 100.0, 0, 0 }, new[] { 0.0, 1, 0 }, new[] { -Root, Root, 0 }, 0.0, -s);
            Vertex(
                written.Vertices[2],
                new[] { 100.0, 100, 0 },
                new[] { -1.0, 0, 0 },
                new[] { -Root, -Root, 0 },
                0.0,
                -s
            );
            Vertex(written.Vertices[3], new[] { 0.0, 100, 0 }, new[] { 0.0, -1, 0 }, new[] { Root, -Root, 0 }, 0.0, -s);
        }

        [Fact]
        public void AClosedTriangleJustifiedAtZeroAtScaleTwo_DividesByTheCosineAtEachCorner()
        {
            // AutoCAD 2021: (0, 0), (100, 0), (50, 80), closed, zero, scale 2.
            DxfDatabase db = Blank();
            DxfMlineBuilder builder = Builder(db, 2.0, DxfMlineJustification.Zero, P(0, 0), P(100, 0), P(50, 80));
            builder.IsClosed = true;

            Written written = Read(builder.Build());

            Vertex(
                written.Vertices[0],
                new[] { 0.0, 0, 0 },
                new[] { 1.0, 0, 0 },
                new[] { 0.8746424812468179, 0.4847685323929451, 0 },
                2.062840166344413,
                -2.062840166344413
            );
            Vertex(
                written.Vertices[1],
                new[] { 100.0, 0, 0 },
                new[] { -0.52999894000318, 0.847998304005088, 0 },
                new[] { -0.8746424812468177, 0.4847685323929456, 0 },
                2.062840166344413,
                -2.062840166344413
            );
            Vertex(
                written.Vertices[2],
                new[] { 50.0, 80, 0 },
                new[] { -0.52999894000318, -0.847998304005088, 0 },
                new[] { 0.0, -1, 0 },
                1.886796226411322,
                -1.886796226411322
            );
        }

        [Fact]
        public void AnAngleThatIsNotARightAngle_GivesAMiterOffTheBisector()
        {
            // AutoCAD 2021: (0, 0), (100, 0), (186.60254, 50): a turn of 30 degrees, the half angle's cosine 0.9659.
            DxfDatabase db = Blank();

            Written written = Read(
                Builder(
                        db,
                        1.0,
                        DxfMlineJustification.Top,
                        P(0, 0),
                        P(100, 0),
                        P(100 + 100 * Math.Cos(Math.PI / 6), 100 * Math.Sin(Math.PI / 6))
                    )
                    .Build()
            );

            Vertex(
                written.Vertices[1],
                new[] { 100.0, 0, 0 },
                new[] { 0.8660254037844389, 0.4999999999999999, 0 },
                new[] { -0.2588190451025205, 0.9659258262890682, 0 },
                0.0,
                -1.035276180410083
            );
        }

        [Fact]
        public void ATurnBackOnItself_IsHeldToACosineOfOneTenThousandth()
        {
            // AutoCAD 2021: (0, 0), (100, 0), (0, 0.001): the cosine is 5E-6, and the offset is divided by 1E-4: -1 / 1E-4.
            DxfDatabase db = Blank();

            Written written = Read(
                Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(100, 0), P(0, 0.001)).Build()
            );

            Assert.Equal(-10000.0, written.Vertices[1].Offsets[1], 6);
        }

        [Fact]
        public void ARunInAnotherPlane_HasItsMitersAcrossItsSegmentsInThatPlane()
        {
            // AutoCAD 2021: (0, 0, 0), (100, 0, 0), (100, 0, 100) with the normal (0, -1, 0): the miters lie in the plane.
            DxfDatabase db = Blank();
            DxfMlineBuilder builder = Builder(
                db,
                1.0,
                DxfMlineJustification.Top,
                P(0, 0, 0),
                P(100, 0, 0),
                P(100, 0, 100)
            );
            builder.Normal = new GeoVector3(0, -1, 0);

            Written written = Read(builder.Build());

            Assert.Equal(new[] { 0.0, -1.0, 0.0 }, written.Normal);
            Vertex(written.Vertices[0], new[] { 0.0, 0, 0 }, new[] { 1.0, 0, 0 }, new[] { 0.0, 0, 1 }, 0.0, -1.0);
            Vertex(
                written.Vertices[1],
                new[] { 100.0, 0, 0 },
                new[] { 0.0, 0, 1 },
                new[] { -Root, 0, Root },
                0.0,
                -1.414213562373094
            );
        }

        [Fact]
        public void TheCapsLeftOffTheStartAndTheEnd_AreFlagsFourAndEight()
        {
            // AutoCAD 2021: SupressStartCaps and SupressEndCaps both set: 71 is 13.
            DxfDatabase db = Blank();
            DxfMlineBuilder builder = Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(100, 0), P(100, 100));
            builder.SuppressStartCaps = true;
            builder.SuppressEndCaps = true;

            Assert.Equal(13, Read(builder.Build()).Flags);
        }

        [Fact]
        public void AStyleWithAnAngleOfSixtyAndOneOfOneHundredAndTwenty_TurnsTheMitersOfTheEndsByThem()
        {
            // AutoCAD 2021: a style of elements 0.5, 0 and -0.5, start angle 60 and end angle 120 degrees, a run of (0, 0) to (100, 0).
            DxfDatabase db = Blank();
            DxfMlineStyleSettings settings = Settings(
                new DxfMlineStyleElement(0.5, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), "BYLAYER"),
                new DxfMlineStyleElement(0.0, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2), "BYLAYER"),
                new DxfMlineStyleElement(-0.5, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5), "BYBLOCK")
            );
            settings.StartAngle = Math.PI / 3.0;
            settings.EndAngle = 2.0 * Math.PI / 3.0;
            var builder = new DxfMlineBuilder(db.AddMlineStyle("GHSTY", settings));
            builder.Points.Add(P(0, 0));
            builder.Points.Add(P(100, 0));

            Written written = Read(builder.Build());

            Assert.Equal(3, written.ElementCount);
            Vertex(
                written.Vertices[0],
                new[] { 0.0, 0, 0 },
                new[] { 1.0, 0, 0 },
                new[] { 0.5, 0.8660254037844385, 0 },
                0.0,
                -0.5773502691896258,
                -1.154700538379251
            );
            Vertex(
                written.Vertices[1],
                new[] { 100.0, 0, 0 },
                new[] { 1.0, 0, 0 },
                new[] { -0.5, 0.8660254037844389, 0 },
                0.0,
                -0.5773502691896257,
                -1.154700538379251
            );
        }

        [Fact]
        public void ElementsOfARightAngleToTheLine_NeverDivideByLessThanOneTenThousandth_AndTheTopIsTheGreatestOffset()
        {
            // AutoCAD 2021: a style of elements 1, 0.25 and -1 (sorted from the greatest), angles 0: the miter of the start lies
            // along the line, its cosine is 0, and the offsets from the top, -0.75 and -2, are divided by 1E-4.
            DxfDatabase db = Blank();
            DxfMlineStyleSettings settings = Settings(
                new DxfMlineStyleElement(-1.0, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), "BYLAYER"),
                new DxfMlineStyleElement(1.0, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2), "BYLAYER"),
                new DxfMlineStyleElement(0.25, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2), "BYLAYER")
            );
            settings.StartAngle = 0.0;
            settings.EndAngle = 0.0;
            var builder = new DxfMlineBuilder(db.AddMlineStyle("GHUNS", settings));
            builder.Points.Add(P(0, 0));
            builder.Points.Add(P(100, 0));
            builder.Points.Add(P(100, 100));

            Written written = Read(builder.Build());

            Vertex(
                written.Vertices[0],
                new[] { 0.0, 0, 0 },
                new[] { 1.0, 0, 0 },
                new[] { 1.0, 0, 0 },
                0.0,
                -7500.0,
                -20000.0
            );
            Vertex(
                written.Vertices[1],
                new[] { 100.0, 0, 0 },
                new[] { 0.0, 1, 0 },
                new[] { -Root, Root, 0 },
                0.0,
                -1.060660171779821,
                -2.828427124746189
            );
        }

        [Fact]
        public void AMovedVertex_RebuiltIntoTheMline_GivesWhatAutoCADWritesAfterMoveVertexAt()
        {
            // AutoCAD 2021: (0, 0), (100, 0), (100, 100) with MoveVertexAt (1, (120, 10)).
            DxfDatabase db = Blank();
            DxfMlineBuilder builder = Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(100, 0), P(100, 100));
            DxfUnknownEntity mline = builder.Build();
            db.ModelSpace.AppendEntity(mline);
            mline.ColorIndex = 2;

            var again = new DxfMlineBuilder(mline);
            again.Points[1] = P(120, 10);
            again.Rebuild(mline);

            Written written = Read(mline);
            Assert.Equal(2, mline.ColorIndex);
            Vertex(
                written.Vertices[0],
                new[] { 0.0, 0, 0 },
                new[] { 0.9965457582448797, 0.08304547985373998, 0 },
                new[] { -0.08304547985374011, 0.9965457582448797, 0 },
                0.0,
                -1.0
            );
            Vertex(
                written.Vertices[1],
                new[] { 120.0, 10, 0 },
                new[] { -0.2169304578186562, 0.9761870601839529, 0 },
                new[] { -0.8053722898179728, 0.5927693267986758, 0 },
                0.0,
                -1.520671352844416
            );
            Vertex(
                written.Vertices[2],
                new[] { 100.0, 100, 0 },
                new[] { -0.2169304578186562, 0.9761870601839529, 0 },
                new[] { -0.9761870601839529, -0.216930457818656, 0 },
                0.0,
                -1.0
            );
        }

        [Fact]
        public void AVertexMovedAt_KeepsTheCutsOfASegmentThatKeepsItsWay_WhereTheyWere_AndMakesWholeOneThatTurns()
        {
            // AutoCAD 2021: (0, 0), (100, 0), (100, 100) at scale 20, cut all from 30 to 60 on both segments (MLEDIT CA), then
            // MoveVertexAt (1, (100, -10)): the first segment turns and is whole again; the second keeps its way and its cut where
            // it was, 40 to 70 from the vertex for the top element, 62.0998 to 92.0998 for the bottom one; the starts at nought stay.
            DxfDatabase db = Blank();
            DxfMlineBuilder builder = Builder(db, 20.0, DxfMlineJustification.Top, P(0, 0), P(100, 0), P(100, 100));
            var cut = new[]
            {
                new DxfMlineBuilder.Run(0.0, 30.0),
                new DxfMlineBuilder.Run(60.0, double.PositiveInfinity),
            };
            builder.SetRuns(0, 0, cut);
            builder.SetRuns(0, 1, cut);
            builder.SetRuns(1, 0, cut);
            builder.SetRuns(
                1,
                1,
                new[] { new DxfMlineBuilder.Run(0.0, 50.0), new DxfMlineBuilder.Run(80.0, double.PositiveInfinity) }
            );

            builder.MoveVertexAt(1, P(100, -10));

            Written written = Read(builder.Build());
            Assert.Equal(new[] { 0.0 }, written.Vertices[0].Tails[0]);
            Assert.Equal(new[] { 0.0 }, written.Vertices[0].Tails[1]);
            Same(new[] { 0.0, 40.0, 70.0 }, written.Vertices[1].Tails[0]);
            Same(new[] { 0.0, 62.09975124224175, 92.09975124224175 }, written.Vertices[1].Tails[1]);

            // AutoCAD 2021 then MoveVertexAt (2, (110, 100)): the second segment turns, its cut is gone.
            builder.MoveVertexAt(2, P(110, 100));

            written = Read(builder.Build());
            Assert.Equal(new[] { 0.0 }, written.Vertices[1].Tails[0]);
            Assert.Equal(new[] { 0.0 }, written.Vertices[1].Tails[1]);
        }

        [Fact]
        public void AVertexMovedAlongItsSegment_KeepsTheCutOfTheSegment_AndOneThatTurnsTheFirstSegmentLosesIt()
        {
            // AutoCAD 2021's STRETCH of the vertex (100, 0) by (20, 0), then of (0, 0) by (0, 15), the first segment cut from 30 to 60.
            DxfDatabase db = Blank();
            DxfMlineBuilder builder = Builder(db, 20.0, DxfMlineJustification.Top, P(0, 0), P(100, 0), P(100, 100));
            var cut = new[]
            {
                new DxfMlineBuilder.Run(0.0, 30.0),
                new DxfMlineBuilder.Run(60.0, double.PositiveInfinity),
            };
            builder.SetRuns(0, 0, cut);
            builder.SetRuns(0, 1, cut);

            builder.MoveVertexAt(1, P(120, 0));

            Written written = Read(builder.Build());
            Same(new[] { 0.0, 30.0, 60.0 }, written.Vertices[0].Tails[0]);
            Same(new[] { 0.0, 30.0, 60.0 }, written.Vertices[0].Tails[1]);

            builder.MoveVertexAt(0, P(0, 15));

            written = Read(builder.Build());
            Assert.Equal(new[] { 0.0 }, written.Vertices[0].Tails[0]);
            Assert.Equal(new[] { 0.0 }, written.Vertices[0].Tails[1]);
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.MoveVertexAt(3, P(0, 0)));
        }

        [Fact]
        public void AMlineReadBack_GivesItsPointsScaleJustificationAndFlagsAsItWasBuilt()
        {
            DxfDatabase db = Blank();
            DxfMlineBuilder builder = Builder(db, 20.0, DxfMlineJustification.Zero, P(0, 0), P(100, 0), P(100, 100));
            builder.SuppressEndCaps = true;
            db.ModelSpace.AppendEntity(builder.Build());

            DxfDatabase reread = DxfDatabase.Parse(db.ToText());
            var mline = (DxfUnknownEntity)reread.ModelSpace.Last(e => e is DxfUnknownEntity);
            var read = new DxfMlineBuilder(mline);

            Assert.Equal(20.0, read.Scale);
            Assert.Equal(DxfMlineJustification.Zero, read.Justification);
            Assert.True(read.SuppressEndCaps);
            Assert.False(read.SuppressStartCaps);
            Assert.False(read.IsClosed);
            Assert.Equal(3, read.Points.Count);
            Assert.Equal(P(100, 100), read.Points[2]);
            Assert.Equal("Standard", read.Style.Name, true);
        }

        [Fact]
        public void AMlineBuiltInTheDrawing_ExplodesIntoOneLinePerElementAlongEachSegment()
        {
            // The corner (0, 0), (100, 0), (100, 100) of STANDARD: its first element runs (0, 0) to (100, 0) to (100, 100), the second
            // (0, -1) to (101, -1) to (101, 100).
            DxfDatabase db = Blank();
            DxfUnknownEntity mline = Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(100, 0), P(100, 100))
                .Build();
            db.ModelSpace.AppendEntity(mline);

            DxfLine[] lines = mline.Explode().Cast<DxfLine>().ToArray();

            Assert.Equal(4, lines.Length);
            Near(P(0, 0), lines[0].StartPoint);
            Near(P(100, 0), lines[0].EndPoint);
            Near(P(0, -1), lines[1].StartPoint);
            Near(P(101, -1), lines[1].EndPoint);
            Near(P(100, 100), lines[2].EndPoint);
            Near(P(101, 100), lines[3].EndPoint);
        }

        [Fact]
        public void AStretchPastTheLastVertexAndOneBeforeTheFirst_AreDrawnWhereAMitreJoinLeavesThem()
        {
            // AutoCAD 2021's corner joint: the end vertex of one mline holds [offset, 0, 1.1547] (the element runs 1.1547 on past the
            // vertex) and the start vertex of the other [offset, -0.5774] (it starts 0.5774 before the vertex).
            DxfDatabase db = Blank();
            DxfMlineBuilder builder = Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(100, 0));
            builder.SetEndExtension(0, 1.1547);
            builder.SetRuns(0, 1, new[] { new DxfMlineBuilder.Run(-0.5774, double.PositiveInfinity) });
            DxfUnknownEntity mline = builder.Build();
            db.ModelSpace.AppendEntity(mline);

            DxfLine[] lines = mline.Explode().Cast<DxfLine>().ToArray();

            Assert.Equal(3, lines.Length);
            Near(P(0, 0), lines[0].StartPoint);
            Near(P(100, 0), lines[0].EndPoint);
            Near(P(-0.5774, -1), lines[1].StartPoint);
            Near(P(100, -1), lines[1].EndPoint);
            Near(P(100, 0), lines[2].StartPoint);
            Near(P(101.1547, 0), lines[2].EndPoint);
        }

        [Fact]
        public void TheCutsOfAnMlineReadFromAFile_AreWrittenBackAsTheyWere()
        {
            // AutoCAD 2021's MLEDIT Closed Cross over (50, 0): the first mline's two elements broken from 50 to 51, which their
            // vertex holds as [offset, 0, 50, 51].
            DxfDatabase db = Blank();
            DxfMlineBuilder builder = Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(100, 0));
            builder.SetRuns(
                0,
                0,
                new[] { new DxfMlineBuilder.Run(0.0, 50.0), new DxfMlineBuilder.Run(51.0, double.PositiveInfinity) }
            );
            builder.SetRuns(
                0,
                1,
                new[] { new DxfMlineBuilder.Run(0.0, 50.0), new DxfMlineBuilder.Run(51.0, double.PositiveInfinity) }
            );
            db.ModelSpace.AppendEntity(builder.Build());

            Written built = Read((DxfUnknownEntity)db.ModelSpace.Last(e => e is DxfUnknownEntity));
            DxfDatabase reread = DxfDatabase.Parse(db.ToText());
            var mline = (DxfUnknownEntity)reread.ModelSpace.Last(e => e is DxfUnknownEntity);
            new DxfMlineBuilder(mline).Rebuild(mline);

            Written written = Read(mline);
            Assert.Equal(new[] { 0.0, 50.0, 51.0 }, built.Vertices[0].Tails[0]);
            Assert.Equal(new[] { 0.0, 50.0, 51.0 }, written.Vertices[0].Tails[0]);
            Assert.Equal(new[] { 0.0, 50.0, 51.0 }, written.Vertices[0].Tails[1]);
            Assert.Equal(new[] { 0.0 }, written.Vertices[1].Tails[0]);
            Assert.Equal(2, mline.Explode().OfType<DxfLine>().Count(l => l.StartPoint.X < 1));
        }

        [Fact]
        public void APointAddedToACutSegment_DividesTheCutBetweenTheTwoSegmentsItMakes()
        {
            // The segment (0, 0) to (100, 0) drawn from 0 to 30 and from 60 on (cut from 30 to 60); a vertex at (50, 0) leaves the
            // first half drawn from 0 to 30, and the second, which starts at 50, drawn from 10 on.
            DxfDatabase db = Blank();
            DxfMlineBuilder builder = Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(100, 0));
            builder.SetRuns(
                0,
                0,
                new[] { new DxfMlineBuilder.Run(0.0, 30.0), new DxfMlineBuilder.Run(60.0, double.PositiveInfinity) }
            );

            builder.InsertVertex(1, P(50, 0));

            Assert.Equal(
                new[] { 0.0, 30.0 },
                builder.RunsOf(0, 0).SelectMany(r => new[] { r.From, r.To }).Where(v => !double.IsInfinity(v))
            );
            List<DxfMlineBuilder.Run> second = builder.RunsOf(1, 0);
            Assert.Single(second);
            Assert.Equal(10.0, second[0].From, 9);
            Assert.True(double.IsPositiveInfinity(second[0].To));
            Assert.Equal(3, builder.Points.Count);
        }

        [Fact]
        public void AnMlineMoved_ByAMoveATurnAMirrorAndAScale_DrawsTheMovedLinesOfTheOne()
        {
            // A corner of STANDARD at scale 3 with one element cut: moved by a translation, a quarter turn, a mirror in the x axis
            // and a scale of 2 about (10, 5), what it explodes into is what the original explodes into, moved the same way.
            foreach (
                GeoTransform3 transform in new[]
                {
                    GeoTransform3.Translation(new GeoVector3(10, -5, 0)),
                    GeoTransform3.RotationZ(Math.PI / 2.0),
                    GeoTransform3.Mirror(new GeoPlane3(P(0, 0, 0), new GeoVector3(0, 1, 0))),
                    GeoTransform3.Scaling(P(10, 5, 0), 2.0),
                }
            )
            {
                DxfDatabase db = Blank();
                DxfMlineBuilder builder = Builder(db, 3.0, DxfMlineJustification.Zero, P(0, 0), P(100, 0), P(100, 60));
                builder.SetRuns(
                    0,
                    1,
                    new[] { new DxfMlineBuilder.Run(0.0, 30.0), new DxfMlineBuilder.Run(50.0, double.PositiveInfinity) }
                );
                DxfUnknownEntity mline = builder.Build();
                db.ModelSpace.AppendEntity(mline);
                DxfLine[] before = mline.Explode().Cast<DxfLine>().ToArray();

                mline.TransformBy(transform);

                DxfLine[] after = mline.Explode().Cast<DxfLine>().ToArray();
                Assert.Equal(before.Length, after.Length);

                for (int i = 0; i < before.Length; i++)
                {
                    Near(before[i].StartPoint.TransformBy(transform), after[i].StartPoint);
                    Near(before[i].EndPoint.TransformBy(transform), after[i].EndPoint);
                }
            }
        }

        [Fact]
        public void AnMlineCopiedMoved_KeepsItsStyle_AndOneScaledUnevenlyIsRefused()
        {
            DxfDatabase db = Blank();
            DxfUnknownEntity mline = Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(100, 0)).Build();
            db.ModelSpace.AppendEntity(mline);

            var copy = (DxfUnknownEntity)mline.GetTransformedCopy(GeoTransform3.Translation(new GeoVector3(0, 50, 0)));
            db.ModelSpace.AppendEntity(copy);

            Assert.Equal(P(0, 50), new DxfMlineBuilder(copy).Points[0]);
            Assert.Equal(P(0, 0), new DxfMlineBuilder(mline).Points[0]);
            Assert.Throws<NotSupportedException>(() => mline.TransformBy(GeoTransform3.Scaling(2.0, 1.0, 1.0)));
        }

        [Fact]
        public void AStyleNamedInLowerCase_IsKeptAsGivenInItsEntry_AndWrittenInCapitals()
        {
            // AutoCAD 2021: MlineStyle.Name = "ghLow" set into the dictionary as "ghLow" writes its record's name (2) as GHLOW.
            DxfDatabase db = Blank();

            DxfMlineStyle style = db.AddMlineStyle("ghLow", Settings());

            Assert.Equal("GHLOW", style.Name);
            Assert.Equal("ghLow", db.MLStyleDictionary.NameAt(style));
        }

        [Fact]
        public void TwoPointsTooCloseOrTooFew_AreRefused()
        {
            DxfDatabase db = Blank();

            Assert.Throws<ArgumentException>(() => Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0)).Build());
            Assert.Throws<ArgumentException>(() =>
                Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(0, 0.0001)).Build()
            );
            DxfMlineBuilder closed = Builder(db, 1.0, DxfMlineJustification.Top, P(0, 0), P(10, 0));
            closed.IsClosed = true;
            Assert.Throws<ArgumentException>(() => closed.Build());
            Assert.Throws<ArgumentException>(() =>
                new DxfMlineBuilder(db.MLStyleDictionary.GetAt("Standard") as DxfMlineStyle).Scale = double.NaN
            );
        }

        [Fact]
        public void ANewStyleOfThreeElementsWithCapsAndAFill_IsWrittenAsAutoCADWritesIt()
        {
            // AutoCAD 2021: a style with the description "three lines", filled in colour 3, angles 60 and 120, a line across each
            // joint, a square start cap and a round end cap, elements 0.5 (colour 1), 0 (colour 2) and -0.5 (colour 5, by block):
            // 70 is 1555 = 1 + 2 + 16 + 1024 + 512 with end inner arcs.
            DxfDatabase db = Blank();
            DxfMlineStyleSettings settings = Settings(
                new DxfMlineStyleElement(0.5, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), "BYLAYER"),
                new DxfMlineStyleElement(0.0, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2), "BYLAYER"),
                new DxfMlineStyleElement(-0.5, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5), "BYBLOCK")
            );
            settings.Description = "three lines";
            settings.Filled = true;
            settings.FillColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
            settings.StartAngle = Math.PI / 3.0;
            settings.EndAngle = 2.0 * Math.PI / 3.0;
            settings.ShowMiters = true;
            settings.StartSquareCap = true;
            settings.EndRoundCap = true;
            settings.EndInnerArcs = true;

            DxfMlineStyle style = db.AddMlineStyle("GHSTY", settings);

            Assert.Same(style, db.MLStyleDictionary.GetAt("GHSTY"));
            Assert.Equal(
                new[]
                {
                    "100:AcDbMlineStyle",
                    "2:GHSTY",
                    "70:1555",
                    "3:three lines",
                    "62:3",
                    "51:60",
                    "52:120",
                    "71:3",
                    "49:0.5",
                    "62:1",
                    "6:BYLAYER",
                    "49:0",
                    "62:2",
                    "6:BYLAYER",
                    "49:-0.5",
                    "62:5",
                    "6:BYBLOCK",
                },
                Own(style)
            );
            Assert.Equal("three lines", style.Description);
            Assert.True(style.Filled);
            Assert.True(style.ShowMiters);
            Assert.True(style.StartSquareCap);
            Assert.True(style.EndRoundCap);
            Assert.True(style.EndInnerArcs);
            Assert.False(style.StartRoundCap);
            Assert.Equal(Math.PI / 3.0, style.StartAngle, 9);
            Assert.Equal(3, style.Elements.Count);
            Assert.Equal(-0.5, style.Elements[2].Offset);
            Assert.Equal("BYBLOCK", style.Elements[2].Linetype);
            Assert.Equal((short)5, style.Elements[2].Color.ColorIndex);
        }

        [Fact]
        public void AStyleWithItsElementsGivenOutOfOrder_IsWrittenFromTheGreatestOffsetToTheLeast()
        {
            // AutoCAD 2021 sorts a style's elements when they are added with sorting: -1, 1, 0.25 is kept as 1, 0.25, -1.
            DxfDatabase db = Blank();
            DxfMlineStyleSettings settings = Settings(
                new DxfMlineStyleElement(-1.0, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), "BYLAYER"),
                new DxfMlineStyleElement(1.0, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2), "BYLAYER"),
                new DxfMlineStyleElement(0.25, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2), "BYLAYER")
            );

            DxfMlineStyle style = db.AddMlineStyle("GHUNS", settings);

            Assert.Equal(new[] { 1.0, 0.25, -1.0 }, style.Elements.Select(e => e.Offset));
        }

        [Fact]
        public void AStyleApplied_KeepsItsNameAndHandle_AndTakesTheNewElementsAndCaps()
        {
            DxfDatabase db = Blank();
            DxfMlineStyle style = db.AddMlineStyle("GHEDIT", Settings());
            string handle = style.Handle;
            DxfMlineStyleSettings settings = style.GetSettings();
            settings.Description = "wall";
            settings.EndSquareCap = true;
            settings.Elements.Add(
                new DxfMlineStyleElement(0.0, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 4), "BYLAYER")
            );

            style.Apply(settings);

            DxfMlineStyle again = (DxfMlineStyle)DxfDatabase.Parse(db.ToText()).MLStyleDictionary.GetAt("GHEDIT");
            Assert.Equal(handle, again.Handle);
            Assert.Equal("wall", again.Description);
            Assert.True(again.EndSquareCap);
            Assert.Equal(new[] { 0.5, 0.0, -0.5 }, again.Elements.Select(e => e.Offset));
            Assert.Equal(256, again.Elements[0].Color.ColorIndex);
            Assert.Equal(4, again.Elements[1].Color.ColorIndex);
        }

        [Fact]
        public void AStyleNameTakenOrNotAName_OrALinetypeTheDrawingHasNot_IsRefused()
        {
            DxfDatabase db = Blank();
            db.AddMlineStyle("GHONE", Settings());

            Assert.Throws<ArgumentException>(() => db.AddMlineStyle("ghone", Settings()));
            Assert.Throws<ArgumentException>(() => db.AddMlineStyle("a/b", Settings()));
            Assert.Throws<ArgumentException>(() =>
                db.AddMlineStyle("GHTWO", Settings(new DxfMlineStyleElement(0.5, DxfColor.ByLayer, "NOSUCHLINETYPE")))
            );
            Assert.Throws<ArgumentException>(() =>
                db.AddMlineStyle("GHTHREE", new DxfMlineStyleSettings { Description = "a\nb" })
            );
        }

        private static DxfDatabase Blank() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

        private static GeoPoint3 P(double x, double y, double z = 0.0) => new GeoPoint3(x, y, z);

        private static DxfMlineStyleSettings Settings(params DxfMlineStyleElement[] elements)
        {
            var settings = new DxfMlineStyleSettings();

            if (elements.Length > 0)
            {
                settings.Elements.Clear();

                foreach (DxfMlineStyleElement element in elements)
                {
                    settings.Elements.Add(element);
                }
            }

            return settings;
        }

        private static DxfMlineBuilder Builder(
            DxfDatabase db,
            double scale,
            DxfMlineJustification justification,
            params GeoPoint3[] points
        )
        {
            var builder = new DxfMlineBuilder((DxfMlineStyle)db.MLStyleDictionary.GetAt("Standard"))
            {
                Scale = scale,
                Justification = justification,
            };

            foreach (GeoPoint3 point in points)
            {
                builder.Points.Add(point);
            }

            return builder;
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) =>
            Assert.True(expected.DistanceTo(actual) < 1E-6, expected + " was " + actual);

        /// <summary>Asserts a vertex as AutoCAD writes it: its point, direction and miter, and the first parameter of each element.</summary>
        private static void Vertex(
            Written.VertexData vertex,
            double[] at,
            double[] along,
            double[] miter,
            params double[] offsets
        )
        {
            Same(at, vertex.At);
            Same(along, vertex.Along);
            Same(miter, vertex.Miter);
            Assert.Equal(offsets.Length, vertex.Offsets.Count);

            for (int i = 0; i < offsets.Length; i++)
            {
                Assert.True(
                    Math.Abs(offsets[i] - vertex.Offsets[i]) < Close * Math.Max(1.0, Math.Abs(offsets[i])),
                    "element " + i + " was " + vertex.Offsets[i] + ", AutoCAD wrote " + offsets[i]
                );
            }
        }

        private static void Same(double[] expected, double[] actual)
        {
            for (int i = 0; i < 3; i++)
            {
                Assert.True(
                    Math.Abs(expected[i] - actual[i]) < Close,
                    string.Join(",", expected) + " was " + string.Join(",", actual)
                );
            }
        }

        /// <summary>The own groups of an object as "code:value" lines.</summary>
        private static string[] Own(DxfMlineStyle style) =>
            style
                .Item.File.ObjectGroupsOf(style.Item)
                .Select(g => g.TypeCode.ToString(CultureInfo.InvariantCulture) + ":" + g.Value.Trim())
                .ToArray();

        /// <summary>An MLINE's groups read the way AutoCAD's own are laid out.</summary>
        private static Written Read(DxfUnknownEntity mline)
        {
            var written = new Written();
            Written.VertexData vertex = null;
            Written.ElementData element = null;
            bool inHead = false;

            foreach (DxfTypedValue group in mline.Groups)
            {
                string value = group.Value.Trim();
                double Number() => double.Parse(value, CultureInfo.InvariantCulture);

                if (group.TypeCode == 100 && value == "AcDbMline")
                {
                    inHead = true;
                    continue;
                }

                if (!inHead)
                {
                    continue;
                }

                switch (group.TypeCode)
                {
                    case 2 when vertex == null:
                        written.StyleName = value;
                        break;
                    case 340:
                        written.StyleHandle = value;
                        break;
                    case 40 when vertex == null:
                        written.Scale = Number();
                        break;
                    case 70:
                        written.Justification = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case 71:
                        written.Flags = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case 72:
                        written.VertexCount = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case 73:
                        written.ElementCount = int.Parse(value, CultureInfo.InvariantCulture);
                        break;
                    case 10 when vertex == null:
                        written.Start[0] = Number();
                        break;
                    case 20 when vertex == null:
                        written.Start[1] = Number();
                        break;
                    case 30 when vertex == null:
                        written.Start[2] = Number();
                        break;
                    case 210:
                        written.Normal[0] = Number();
                        break;
                    case 220:
                        written.Normal[1] = Number();
                        break;
                    case 230:
                        written.Normal[2] = Number();
                        break;
                    case 11:
                        vertex = new Written.VertexData();
                        written.Vertices.Add(vertex);
                        vertex.At[0] = Number();
                        break;
                    case 21:
                        vertex.At[1] = Number();
                        break;
                    case 31:
                        vertex.At[2] = Number();
                        break;
                    case 12:
                        vertex.Along[0] = Number();
                        break;
                    case 22:
                        vertex.Along[1] = Number();
                        break;
                    case 32:
                        vertex.Along[2] = Number();
                        break;
                    case 13:
                        vertex.Miter[0] = Number();
                        break;
                    case 23:
                        vertex.Miter[1] = Number();
                        break;
                    case 33:
                        vertex.Miter[2] = Number();
                        break;
                    case 74:
                        element = new Written.ElementData();
                        vertex.Elements.Add(element);
                        break;
                    case 41:
                        element.Parameters.Add(Number());
                        break;
                }
            }

            foreach (Written.VertexData each in written.Vertices)
            {
                foreach (Written.ElementData e in each.Elements)
                {
                    each.Offsets.Add(e.Parameters[0]);
                    each.Tails.Add(e.Parameters.Skip(1).ToArray());
                }
            }

            Assert.Equal(written.VertexCount, written.Vertices.Count);
            return written;
        }

        private sealed class Written
        {
            internal string StyleName;
            internal string StyleHandle;
            internal double Scale;
            internal int Justification;
            internal int Flags;
            internal int VertexCount;
            internal int ElementCount;
            internal double[] Start = new double[3];
            internal double[] Normal = new double[3];
            internal List<VertexData> Vertices = new List<VertexData>();

            internal sealed class VertexData
            {
                internal double[] At = new double[3];
                internal double[] Along = new double[3];
                internal double[] Miter = new double[3];
                internal List<ElementData> Elements = new List<ElementData>();
                internal List<double> Offsets = new List<double>();
                internal List<double[]> Tails = new List<double[]>();
            }

            internal sealed class ElementData
            {
                internal List<double> Parameters = new List<double>();
            }
        }
    }
}
