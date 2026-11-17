using System;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// Arc length dimensions (DIMARC) of more than half a circle, laid out as AutoCAD 2021 does: the arc point of words
    /// outside, aligned or level, and the aligned words standing by a ray (DIMJUST 1 and 2) on a small arc. The numbers are
    /// those AutoCAD 2021's console gave for the same drawings, made with its own template (Standard in arial.ttf, text 0.18
    /// high, arrowheads 0.18, gap 0.09, DIMARCSYM 2, DIMDEC 4); positions are given from the arc's centre.
    /// </summary>
    public class DimensionAssociativeFollowUps12Tests
    {
        private static CadDocument Styled(Action<DxfDimStyleTableRecord> set)
        {
            CadDocument document = Blank();
            DxfTextStyleTableRecord standard = document.Database.TextStyleTable["Standard"];
            standard.FileName = "arial.ttf";
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["Standard"];
            style.Dimtxsty = standard;
            style.Dimdec = 4;
            style.Dimarcsym = 2;
            set(style);
            StandardCurrent(document.Database);
            return document;
        }

        private static CadDocument Aligned(int justify) =>
            Styled(s =>
            {
                s.Dimtih = false;
                s.Dimtoh = false;
                s.Dimjust = justify;
            });

        private static GeoPoint2 Polar(double radius, double degrees) =>
            P(100 + radius * Math.Cos(degrees * Math.PI / 180.0), 100 + radius * Math.Sin(degrees * Math.PI / 180.0));

        private static GeoPoint3 Offset(double radius, double degrees) =>
            new GeoPoint3(
                100 + radius * Math.Cos(degrees * Math.PI / 180.0),
                100 + radius * Math.Sin(degrees * Math.PI / 180.0),
                0
            );

        private static DxfArcDimension Dimension(CadDocument document, double from, double sweep, double distance)
        {
            CadCommandSession session = Session(document);
            Run(
                session,
                "ARC",
                "C",
                P(100, 100),
                Polar(1.0, from),
                "A",
                sweep.ToString("0.##########", CultureInfo.InvariantCulture)
            );
            double middle = from + sweep / 2.0;
            Run(session, "DIMARC", Polar(1.0, middle), Polar(distance, middle));
            return (DxfArcDimension)document.ModelSpace.Block.Last(e => !e.IsErased && e is DxfDimension);
        }

        [Theory]
        [InlineData(0, 181.0, 0.31, 60.3333)]
        [InlineData(0, 181.0, 0.32, 120.6667)]
        [InlineData(1, 181.0, 0.31, 120.6667)]
        [InlineData(1, 181.0, 0.32, 60.3333)]
        [InlineData(0, 200.0, 0.36, 66.6667)]
        [InlineData(0, 200.0, 0.37, 133.3333)]
        [InlineData(2, 200.0, 0.36, 66.6667)]
        [InlineData(2, 200.0, 0.37, 133.3333)]
        public void AlignedWordsOutsideAnArcPastAHalfCircle_HaveTheirArcPointWhereTheyStandNearerTheFirstEndOrTheSecond_AsAutoCad(
            int justify,
            double sweep,
            double distance,
            double degrees
        )
        {
            // AutoCAD 2021, the arc of 1 from 0 through 181 and 200 degrees, the distance put by 0.01: the words (3.1590 and so
            // on) stand outside past the first end (the second for DIMJUST 1), in the half turn left over, and the arc point is
            // two thirds of the way round where their middle is nearer the first end, round that half turn, than the second
            // (DIMJUST 0 and 2 from 0.32 out at 181 degrees, from 0.37 at 200), a third where it is nearer the second
            // (DIMJUST 1 the other way round); it was two thirds (a third for DIMJUST 1) whatever the distance before.
            DxfArcDimension dimension = Dimension(Aligned(justify), 0.0, sweep, distance);

            Near(Offset(distance, degrees), dimension.ArcPoint, 1E-3);
        }

        [Theory]
        [InlineData(60.0, -0.1515, 0.2589)]
        [InlineData(70.0, -0.2948, -0.0555)]
        public void LevelWordsOutsideAnArcPastAHalfCircle_HaveTheirArcPointTwoThirdsRoundWhereTheirMiddleStandsNearerTheFirstEnd_AsAutoCad(
            double from,
            double x,
            double y
        )
        {
            // AutoCAD 2021, the arc of 1 through 181 degrees put 0.3 out: the words stand level past the first end, in the
            // half turn left over, and the arc point is a third of the way round (from 60 degrees: 120.3 about the centre)
            // while their middle is nearer the second end, two thirds (from 70: 190.7) where the middle has come nearer the
            // first end, a hair inside the arc; it was always a third before.
            DxfArcDimension dimension = Dimension(Styled(_ => { }), from, 181.0, 0.3);

            Near(new GeoPoint3(100 + x, 100 + y, 0), dimension.ArcPoint, 1E-3);
        }

        [Theory]
        [InlineData(1, 0.1742, 0.2442, 5.6634)]
        [InlineData(2, -0.2959, 0.0495, 1.4051)]
        public void AlignedWordsByARayOnASmallArcPast220Degrees_StandAndTurnAsTheChordsOfTheirRoomCarryThem_AsAutoCad(
            int justify,
            double wordsX,
            double wordsY,
            double rotation
        )
        {
            // AutoCAD 2021, the arc of 1 from 0 through 225 degrees put 0.3 out, the words "3.9270" 0.75 wide next to the
            // second (DIMJUST 1) or first (2) ray: the arrowhead, a gap, half the words with a gap and another gap are laid
            // along the arc as chords, the cube of each over 24 radii squared (the first term) left the words 4 degrees short
            // round the arc and turned them as much too far.
            DxfArcDimension dimension = Dimension(Aligned(justify), 0.0, 225.0, 0.3);
            DxfMText words = dimension.DimBlock.OfType<DxfMText>().First();

            Near(new GeoPoint3(100 + wordsX, 100 + wordsY, 0), words.Location, 1E-3);
            Assert.Equal(rotation, words.Rotation, 3);
        }
    }
}
