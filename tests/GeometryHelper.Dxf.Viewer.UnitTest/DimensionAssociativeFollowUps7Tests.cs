using System;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Arc length dimensions (DIMARC) and angles of three points laid out as AutoCAD 2021 does where the last follow-ups left
    /// them: the sweep that counts as 90 degrees, level words off the arc (DIMTAD 2 and 3), and the arc point after a scale.
    /// The numbers are those AutoCAD 2021's console gave for the same drawings, made with its own template (Standard in
    /// arial.ttf, text 0.18 high, arrowheads 0.18, gap 0.09, words kept level by DIMTIH and DIMTOH); positions are given from
    /// the arc's centre.
    /// </summary>
    public class DimensionAssociativeFollowUps7Tests
    {
        private static CadDocument Styled(Action<DxfDimStyleTableRecord> set)
        {
            CadDocument document = Blank();
            DxfTextStyleTableRecord standard = document.Database.TextStyleTable["Standard"];
            standard.FileName = "arial.ttf";
            document.Database.DimStyleTable["Standard"].Dimtxsty = standard;
            set(document.Database.DimStyleTable["Standard"]);
            StandardCurrent(document.Database);
            return document;
        }

        private static GeoPoint2 Polar(double radius, double degrees) =>
            P(100 + radius * Math.Cos(degrees * Math.PI / 180.0), 100 + radius * Math.Sin(degrees * Math.PI / 180.0));

        private static GeoPoint3 Offset(double dx, double dy) => new GeoPoint3(100 + dx, 100 + dy, 0);

        private static DxfArcDimension Dimension(
            CadDocument document,
            double radius,
            double from,
            double sweep,
            double distance
        )
        {
            CadCommandSession session = Session(document);
            Run(
                session,
                "ARC",
                "C",
                P(100, 100),
                Polar(radius, from),
                "A",
                sweep.ToString("0.##########", CultureInfo.InvariantCulture)
            );
            double middle = from + sweep / 2.0;
            Run(session, "DIMARC", Polar(radius, middle), Polar(distance, middle));
            return (DxfArcDimension)document.ModelSpace.Block.Last(e => !e.IsErased && e is DxfDimension);
        }

        private static GeoPoint3 Words(DxfDimension dimension) =>
            dimension.DimBlock.OfType<DxfMText>().First().Location;

        private static double Sweep(double chord) => Math.Round(2.0 * Math.Asin(chord / 20.0) * 180.0 / Math.PI, 4);

        [Theory]
        [InlineData(1.570796, 0.6928, 0.4)]
        [InlineData(1.5707953268, 0.6928, 0.4)]
        [InlineData(1.5707952268, 0.7246, 0.3586)]
        [InlineData(1.570794, 0.7246, 0.3586)]
        public void AnArcOfOneSweepingAMillionthOfARadianShortOfNinetyDegrees_RunsItsExtensionLinesFromTheCentre_AsAutoCad(
            double endRadians,
            double arcX,
            double arcY
        )
        {
            // AutoCAD 2021, the arc of 1 from 0 to the angle given (radians) put 0.8 out, no arc symbol: through 1.570796 (0.33
            // millionths short of 90 degrees) and 1.5707953268 (1.0 short) it takes the arc point on the circle about the
            // centre, a third round (30 degrees: 0.6928, 0.4); from 1.5707952268 (1.1 short) it moves the arc in along the
            // middle, as for any sweep under 90 degrees, and the arc point stands at (0.7246, 0.3586).
            CadDocument document = Styled(s => s.Dimarcsym = 2);
            DxfArcDimension dimension = Dimension(document, 1.0, 0.0, endRadians * 180.0 / Math.PI, 0.8);

            Near(Offset(arcX, arcY), dimension.ArcPoint, 1E-3);
        }

        [Theory]
        [InlineData(0.6, 12.2478, 3.1717, 11.4635, 3.5478)]
        [InlineData(0.64, 11.945, 3.6141, 11.5162, 3.3729)]
        public void LevelWordsOffTheArcUnderDimtad2_FitWhereTheirBoxTakesTheChordWithGapsRoundIt_AsAutoCad(
            double chord,
            double wordsX,
            double wordsY,
            double arcX,
            double arcY
        )
        {
            // AutoCAD 2021, arcs of 10 from 15 degrees put 12 out, DIMTAD 2, no arc symbol: the words (0.73 wide, 0.18 high, a
            // gap round them) take the chord as their width and gaps times the sine of the arc's middle direction plus their
            // height and gaps times its cosine, with none of the gap at either end of the arc that words on it keep. The
            // chord of 0.6 leaves them outside past the first end, 0.64 holds them between the extension lines.
            CadDocument document = Styled(s =>
            {
                s.Dimtad = 2;
                s.Dimarcsym = 2;
            });
            DxfArcDimension dimension = Dimension(document, 10.0, 15.0, Sweep(chord), 12.0);

            Near(Offset(wordsX, wordsY), Words(dimension), 2E-3);
            Near(Offset(arcX, arcY), dimension.ArcPoint, 2E-3);
        }

        [Theory]
        [InlineData(2, 0, 0.0, 12.5967, 0.9601)]
        [InlineData(3, 0, 45.0, 8.1693, 9.5174)]
        [InlineData(2, 1, 90.0, -0.9305, 12.2081)]
        public void LevelWordsOffTheArcWithTheArcSymbol_StandWithItClearOfTheArcToo_AsAutoCad(
            int above,
            int symbol,
            double from,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021, arcs of 10 through a chord of 1.52 put 12 out: the symbol before the words (DIMARCSYM 0) counts
            // in the box the arc is cleared by, half of its room (0.162) further out along the sine, and the symbol over
            // them (1) half of its 0.054 along the cosine: words at 4 degrees stand 0.165 further out than without it.
            CadDocument document = Styled(s =>
            {
                s.Dimtad = above;
                s.Dimarcsym = symbol;
            });
            DxfArcDimension dimension = Dimension(document, 10.0, from, Sweep(1.52), 12.0);

            Near(Offset(wordsX, wordsY), Words(dimension), 2E-3);
        }

        [Theory]
        [InlineData(0.36, 12.0146, 3.112)]
        [InlineData(0.4, 11.7577, 3.4039)]
        public void LevelWordsOffTheArcOfAnAngleUnderDimtad2_FitAsOffTheArcOfALength_AsAutoCad(
            double chord,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021, an angle of three points from 15 degrees, rays 10 long, the arc 12 out, DIMTAD 2: the chord of 0.36
            // leaves the words outside, 0.4 takes them between the rays.
            CadDocument document = Styled(s => s.Dimtad = 2);
            double sweep = Sweep(chord);
            CadCommandSession session = Session(document);
            Run(
                session,
                "DAN",
                Enter,
                P(100, 100),
                Polar(10, 15.0),
                Polar(10, 15.0 + sweep),
                Polar(12, 15.0 + sweep / 2.0)
            );
            DxfDimension dimension = document.ModelSpace.Block.OfType<DxfDimension>().Last();

            Near(Offset(wordsX, wordsY), Words(dimension), 2E-3);
        }

        [Theory]
        [InlineData(1.5, 0.7474, 0.3525)]
        [InlineData(0.1, 0.5019, 0.6315)]
        public void AnArcLengthDimensionMovedInAlongTheMiddle_KeepsItsArcPointOnItsFirstRadiusAfterAScale_AsAutoCad(
            double factor,
            double arcX,
            double arcY
        )
        {
            // AutoCAD 2021, the arc of 1 through 80 degrees from 10, put 0.8 out (moved in along the middle), scaled about its
            // centre: by 1.5 its arc point stands 0.8264 from the centre, worked out from the arc point it held and the new
            // radius (0.8346 from the old radius); by 0.1 it is the point the arc held, 0.8067 out, not 0.8.
            CadDocument document = Styled(s => s.Dimarcsym = 2);
            DxfArcDimension made = Dimension(document, 1.0, 10.0, 80.0, 0.8);
            string handle = made.Handle;
            string arc = document.ModelSpace.Block.OfType<DxfArc>().Last().Handle;

            document.Selection.Set(new[] { arc });
            Run(Session(document), "SCALE", P(100, 100), factor.ToString(CultureInfo.InvariantCulture));
            DxfArcDimension dimension = Entity<DxfArcDimension>(document, handle);

            Near(Offset(arcX, arcY), dimension.ArcPoint, 2E-3);
        }
    }
}
