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
    /// Arc length dimensions (DIMARC) where the arrowheads come inside with their words, as AutoCAD 2021 lays them out:
    /// words along the arc of more than half a circle, words by a ray (DIMJUST 1 and 2) on arcs of 115 to 140 degrees, and
    /// the arrowheads that go outside with level words past a half circle. The numbers are those AutoCAD 2021's console
    /// gave for the same drawings, made with its own template (Standard in arial.ttf, text 0.18 high, arrowheads 0.18, gap
    /// 0.09, DIMDEC 4); the arcs are of radius 1 about the same centre, and the distance is where the dimension arc is put.
    /// </summary>
    public class DimensionAssociativeFollowUps13Tests
    {
        private static CadDocument Styled(Action<DxfDimStyleTableRecord> set)
        {
            CadDocument document = Blank();
            DxfTextStyleTableRecord standard = document.Database.TextStyleTable["Standard"];
            standard.FileName = "arial.ttf";
            DxfDimStyleTableRecord style = document.Database.DimStyleTable["Standard"];
            style.Dimtxsty = standard;
            style.Dimdec = 4;
            set(style);
            StandardCurrent(document.Database);
            return document;
        }

        private static CadDocument Aligned(int symbol, int justify) =>
            Styled(s =>
            {
                s.Dimarcsym = symbol;
                s.Dimtih = false;
                s.Dimtoh = false;
                s.Dimjust = justify;
            });

        private static GeoPoint2 Polar(double radius, double degrees) =>
            P(100 + radius * Math.Cos(degrees * Math.PI / 180.0), 100 + radius * Math.Sin(degrees * Math.PI / 180.0));

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

        private static double AngleAbout(double x, double y) => Math.Atan2(y - 100.0, x - 100.0) * 180.0 / Math.PI;

        /// <summary>How many arrowheads have their backs on the arc, between its first end and its second, going round.</summary>
        private static int ArrowheadsOnTheArc(DxfDimension dimension, double from, double sweep)
        {
            int inside = 0;

            foreach (DxfSolid solid in dimension.DimBlock.OfType<DxfSolid>().Where(s => !s.IsErased))
            {
                GeoPoint3[] c = { solid.GetPointAt(0), solid.GetPointAt(1), solid.GetPointAt(2) };
                int tip = Enumerable.Range(0, 3).OrderBy(k => c[(k + 1) % 3].DistanceTo(c[(k + 2) % 3])).First();
                GeoPoint3 a = c[(tip + 1) % 3];
                GeoPoint3 b = c[(tip + 2) % 3];
                double back = AngleAbout((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
                double along = back - from - Math.Floor((back - from) / 360.0) * 360.0;

                if (along > 0.0 && along < sweep)
                {
                    inside++;
                }
            }

            return inside;
        }

        [Theory]
        [InlineData(181.0, 0.67, 0)]
        [InlineData(181.0, 0.68, 2)]
        [InlineData(200.0, 0.64, 0)]
        [InlineData(200.0, 0.65, 2)]
        [InlineData(225.0, 0.62, 0)]
        [InlineData(225.0, 0.63, 2)]
        public void AlignedWordsWithTheArcSymbolOnAnArcPastAHalfCircle_BringTheArrowheadsInOneStepOfTheDistanceSooner_AsAutoCad(
            double sweep,
            double distance,
            int arrowheadsInside
        )
        {
            // AutoCAD 2021, the arc of 1 from 0, DIMARCSYM 0, the distance put by 0.01: the words (3.1590 and so on) stand in
            // past the half circle with the arrowheads outside until the room between the arrowheads takes them as well, at
            // 0.68 for 181 degrees, 0.65 for 200 and 0.63 for 225, though the room asked for 0.043 to 0.071 more than it has
            // there; the arrowheads came in 0.02 further out before.
            DxfArcDimension dimension = Dimension(Aligned(0, 0), 0.0, sweep, distance);

            Assert.Equal(arrowheadsInside, ArrowheadsOnTheArc(dimension, 0.0, sweep));
        }

        [Theory]
        [InlineData(200.0, 0.46, 2)]
        [InlineData(200.0, 0.47, 0)]
        [InlineData(200.0, 0.49, 0)]
        [InlineData(200.0, 0.50, 2)]
        public void AlignedWordsWithoutTheArcSymbolOnAnArcPastAHalfCircle_TakeTheWordsInAndThenTheArrowheadsWhereAutoCadDoes(
            double sweep,
            double distance,
            int arrowheadsInside
        )
        {
            // AutoCAD 2021, the arc of 1 from 0 through 200 degrees, DIMARCSYM 2, the distance put by 0.01: the arrowheads stand
            // in with the words outside up to 0.46, the words come in with the arrowheads outside from 0.47, and the arrowheads
            // follow at 0.50; the room asked for the words to come in at 0.45.
            DxfArcDimension dimension = Dimension(Aligned(2, 0), 0.0, sweep, distance);

            Assert.Equal(arrowheadsInside, ArrowheadsOnTheArc(dimension, 0.0, sweep));
        }

        [Theory]
        [InlineData(0, 120.0, 0.92, 0)]
        [InlineData(0, 120.0, 0.93, 2)]
        [InlineData(1, 120.0, 0.94, 0)]
        [InlineData(1, 120.0, 0.95, 2)]
        public void WordsByARayOnAnArcOf120Degrees_BringTheArrowheadsInOneStepOfTheDistanceSooner_AsAutoCad(
            int level,
            double sweep,
            double distance,
            int arrowheadsInside
        )
        {
            // AutoCAD 2021, the arc of 1 from 10 degrees through 130, DIMARCSYM 0, DIMJUST 1, the distance put by 0.01: aligned
            // words (0) take their arrowheads in from 0.93, level ones (1) from 0.95; the room asked for 0.011 and 0.02 more,
            // and the arrowheads came in a step (0.01) later.
            CadDocument document = Styled(s =>
            {
                s.Dimarcsym = 0;
                s.Dimtih = level == 1;
                s.Dimtoh = level == 1;
                s.Dimjust = 1;
            });
            DxfArcDimension dimension = Dimension(document, 10.0, sweep, distance);

            Assert.Equal(arrowheadsInside, ArrowheadsOnTheArc(dimension, 10.0, sweep));
        }
    }
}
