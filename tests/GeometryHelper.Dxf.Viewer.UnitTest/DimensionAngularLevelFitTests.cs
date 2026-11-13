using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The level words of an angular dimension of three points, as AutoCAD 2021 lays them out: whether they fit between the
    /// rays (what their box takes of the chord across the arc's turn, as for an arc length dimension), and which way they run
    /// out when they do not. The numbers are those AutoCAD 2021's console gave for the same drawings, made with its own
    /// template (Standard in arial.ttf, text 0.18 high, words kept level by DIMTIH and DIMTOH): rays 10 long from the vertex,
    /// the arc 12 out, chords of 0.2 to 1.6; positions are given from the vertex.
    /// </summary>
    public class DimensionAngularLevelFitTests
    {
        private static CadDocument Arial()
        {
            CadDocument document = Blank();
            DxfTextStyleTableRecord standard = document.Database.TextStyleTable["Standard"];
            standard.FileName = "arial.ttf";
            document.Database.DimStyleTable["Standard"].Dimtxsty = standard;
            StandardCurrent(document.Database);
            return document;
        }

        private static GeoPoint2 Polar(double radius, double degrees) =>
            P(100 + radius * Math.Cos(degrees * Math.PI / 180.0), 100 + radius * Math.Sin(degrees * Math.PI / 180.0));

        private static GeoPoint3 Words(CadDocument document, double from, double chord)
        {
            double sweep = Math.Round(2.0 * Math.Asin(chord / 20.0) * 180.0 / Math.PI, 4);
            CadCommandSession session = Session(document);
            Run(
                session,
                "DAN",
                Enter,
                P(100, 100),
                Polar(10, from),
                Polar(10, from + sweep),
                Polar(12, from + sweep / 2.0)
            );
            DxfDimension dimension = document.ModelSpace.Block.OfType<DxfDimension>().Last();
            GeoPoint3 at = dimension.DimBlock.OfType<DxfMText>().First().Location;
            return new GeoPoint3(at.X - 100, at.Y - 100, at.Z);
        }

        [Theory]
        [InlineData(60.0, 0.48, 6.5318, 10.3011)]
        [InlineData(60.0, 0.56, 6.5318, 10.3011)]
        [InlineData(60.0, 0.6, 5.6855, 10.5676)]
        [InlineData(120.0, 0.56, -5.4669, 10.4811)]
        [InlineData(120.0, 0.6, -6.3091, 10.2076)]
        public void LevelWordsOfAnAngle_FitWhereTheirBoxAcrossTheTurnTakesTheChord_AsAutoCad(
            double from,
            double chord,
            double wordsX,
            double wordsY
        )
        {
            // The angle's words stand level, so what they take of the chord across the turn is their width times the sine of the
            // direction of the arc's middle and their height times its cosine, joined at 45 degrees: between 60 and 63 degrees
            // from the vertex the chords of 0.48 to 0.56 leave them outside, past the first ray, and the chord of 0.6 is the
            // first to hold them; from 120 degrees likewise.
            Near(P3(wordsX, wordsY), Words(Arial(), from, chord), 1E-3);
        }

        [Theory]
        [InlineData(0.0, 0.32, 12.3768, -0.18)]
        [InlineData(0.0, 0.2, 12.3628, -0.36)]
        public void LevelWordsOutsideAnAngleWhoseFirstRayLiesOnTheHorizontal_RunOutToTheRight_AsAutoCad(
            double from,
            double chord,
            double wordsX,
            double wordsY
        )
        {
            // AutoCAD 2021: the words run out to the right where the arc point (two thirds round) stands more than 0.05 above the
            // vertex, and to the left where it does not: a first ray along 0 degrees puts it 0.25 up, so they run right.
            Near(P3(wordsX, wordsY), Words(Arial(), from, chord), 1E-3);
        }

        private static GeoPoint3 P3(double x, double y) => new GeoPoint3(x, y, 0);
    }
}
