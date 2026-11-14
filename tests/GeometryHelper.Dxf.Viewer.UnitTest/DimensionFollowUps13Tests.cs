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
    /// Where AutoCAD 2021 stands the level words of a radius or a diameter put out past the rim under DIMATFIT 3 and DIMTAD 1:
    /// on a landing an arrowhead long ("on the line") or above a landing of their own. Every figure is a picture made by
    /// accoreconsole: the metric template with a style of arrowheads and words 2.5 high, DIMGAP 0.625, level words and Arial, a
    /// circle about the origin, its dimension put 1.3 radii out (the figures are relative to the centre). It fits the words and
    /// the arrowheads to the line as DIMATFIT 3 does: they stand on the landing where they fit the line, the arrowheads do not
    /// take their place, and both do not fit together.
    /// </summary>
    public class DimensionFollowUps13Tests
    {
        private static CadCommandSession Probe()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            CadDocument document = CadDocument.From(database, "probe.dxf");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Probe", FileName = "arial.ttf" });
            string start = CadDimStyleEditor.Current(database);
            var values = new SortedDictionary<string, object>(CadDimStyleEditor.Read(database.DimStyleTable[start]))
            {
                ["Dimasz"] = 2.5,
                ["Dimtxt"] = 2.5,
                ["Dimgap"] = 0.625,
                ["Dimexo"] = 0.625,
                ["Dimexe"] = 1.25,
                ["Dimscale"] = 1.0,
                ["Dimdec"] = 0,
                ["Dimtad"] = 1,
                ["Dimtih"] = true,
                ["Dimtoh"] = true,
                ["Dimtofl"] = true,
                ["Dimatfit"] = 3,
                ["Dimcen"] = 2.5,
                ["Dimjust"] = 0,
                ["Dimtix"] = false,
                ["Dimsoxd"] = false,
                ["Dimtmove"] = 0,
                ["Dimtvp"] = 0.0,
                ["Dimadec"] = 0,
                ["Dimazin"] = 0,
                ["Dimtxsty"] = "Probe",
            };
            CadDimStyleEditor.Add(document, "Probe", start, values);
            CadDimStyleEditor.SetCurrent(document, "Probe");
            return Session(document);
        }

        private static GeoPoint2 Polar(double degrees, double distance) =>
            new GeoPoint2(
                distance * Math.Cos(degrees * Math.PI / 180.0),
                distance * Math.Sin(degrees * Math.PI / 180.0)
            );

        /// <summary>A circle about the origin, then its radius or diameter at the angle, put 1.3 radii out.</summary>
        private static DxfDimension Round(string command, double radius, double degrees)
        {
            CadCommandSession session = Probe();
            Run(session, "C", P(0, 0), radius.ToString(System.Globalization.CultureInfo.InvariantCulture));
            string circle = session.Document.ModelSpace.Block.Cast<DxfEntity>().Last().Handle;
            Run(session, command, new[] { circle }, Polar(degrees, radius * 1.3));
            return session.Document.ModelSpace.Block.OfType<DxfDimension>().Last();
        }

        private static DxfMText Words(DxfDimension dimension) => dimension.DimBlock.OfType<DxfMText>().Single();

        private static List<DxfLine> Lines(DxfDimension dimension) =>
            dimension.DimBlock.OfType<DxfLine>().Where(l => !l.IsErased).ToList();

        private static void Near(double x, double y, GeoPoint3 actual)
        {
            Assert.True(
                Math.Abs(actual.X - x) < 1E-3 && Math.Abs(actual.Y - y) < 1E-3,
                $"expected ({x}, {y}), got ({actual.X}, {actual.Y})"
            );
        }

        private static bool Joins(DxfLine l, double x1, double y1, double x2, double y2)
        {
            const double tolerance = 1E-3;
            bool forward =
                Math.Abs(l.StartPoint.X - x1) < tolerance
                && Math.Abs(l.StartPoint.Y - y1) < tolerance
                && Math.Abs(l.EndPoint.X - x2) < tolerance
                && Math.Abs(l.EndPoint.Y - y2) < tolerance;
            bool backward =
                Math.Abs(l.StartPoint.X - x2) < tolerance
                && Math.Abs(l.StartPoint.Y - y2) < tolerance
                && Math.Abs(l.EndPoint.X - x1) < tolerance
                && Math.Abs(l.EndPoint.Y - y1) < tolerance;
            return forward || backward;
        }

        private static void HasLine(DxfDimension dimension, double x1, double y1, double x2, double y2) =>
            Assert.Contains(Lines(dimension), l => Joins(l, x1, y1, x2, y2));

        [Fact]
        public void ADiameterOf6PutOutAt90Degrees_HasItsWordsOnTheLanding()
        {
            // AutoCAD 2021: "%%c6" (4.357 wide) put 3.9 out straight up stands level with a landing 2.5 long from 8, as a radius's
            // words do; before, a diameter's words always stood above a landing of their own.
            DxfDimension dimension = Round("DDI", 3, 90);

            Near(5.3035, 8.0, Words(dimension).Location);
            HasLine(dimension, 0, 8, 2.5, 8);
        }

        [Fact]
        public void ADiameterOf7PutOutAt90Degrees_HasItsWordsAboveALandingOfTheirOwn()
        {
            // AutoCAD 2021: "%%c7" put 4.55 out straight up: the arrowheads and their gaps (6.25) fit the 7 and are the larger of
            // the two (the words reach 3.75 along it), so the words stand above a landing 4.984 long.
            DxfDimension dimension = Round("DDI", 3.5, 90);

            Near(2.8044, 10.4714, Words(dimension).Location);
            HasLine(dimension, 0, 8.5, 4.9838, 8.5);
        }

        [Fact]
        public void ADiameterOf10PutOutAt45Degrees_HasItsWordsOnTheLanding()
        {
            // AutoCAD 2021: "%%c10" (6.293 wide) put 6.5 out at 45 degrees stands on a landing 2.5 long from (9.571, 7.071);
            // 12 does not (a landing 6.9 long, the words above it).
            DxfDimension dimension = Round("DDI", 5, 45);

            Near(13.3424, 7.0711, Words(dimension).Location);
            HasLine(dimension, 9.5711, 7.0711, 7.0711, 7.0711);
        }

        [Fact]
        public void ADiameterOf12PutOutAt45Degrees_HasItsWordsAboveALandingOfTheirOwn()
        {
            DxfDimension dimension = Round("DDI", 6, 45);

            Near(11.541, 9.7495, Words(dimension).Location);
            HasLine(dimension, 14.6788, 7.7782, 7.7782, 7.7782);
        }

        [Fact]
        public void ARadiusOf8PutOutAt60Degrees_HasItsWordsOnTheLanding()
        {
            // AutoCAD 2021: "R8" (4.037 wide) put 10.4 out at 60 degrees: a landing 2.5 long from (9, 11.258).
            DxfDimension dimension = Round("DRA", 8, 60);

            Near(11.6432, 11.2583, Words(dimension).Location);
            HasLine(dimension, 9, 11.2583, 6.5, 11.2583);
        }

        [Fact]
        public void ARadiusOf10PutOutAt45Degrees_HasItsWordsAboveALandingOfTheirOwn()
        {
            // AutoCAD 2021: "R10" put 13 out at 45 degrees: the words fit the radius and so do the arrowheads and both together
            // (the chord through the words' box 5.3 and the arrowhead and its gaps 3.75 are 9.05, under 10), so they stand above
            // a landing 6.59 long; before, on a short one.
            DxfDimension dimension = Round("DRA", 10, 45);

            Near(14.2142, 12.508, Words(dimension).Location);
            HasLine(dimension, 17.1968, 10.6066, 10.6066, 10.6066);
        }

        [Fact]
        public void ARadiusOf5PutOutAt85Degrees_HasItsWordsOnTheLandingNearUpright()
        {
            // AutoCAD 2021: "R5" put 6.5 out at 85 degrees, 5 degrees off upright, stands on a landing 2.5 long; before, the
            // viewer put the words above a landing of their own on any line within 15 degrees of upright.
            DxfDimension dimension = Round("DRA", 5, 85);

            Near(6.0216, 9.9619, Words(dimension).Location);
            HasLine(dimension, 3.3716, 9.9619, 0.8716, 9.9619);
        }

        [Fact]
        public void ARadiusOf7PutOutAt90Degrees_HasItsWordsAboveALandingOfTheirOwn()
        {
            // AutoCAD 2021: "R7" put 9.1 out straight up: the words and the arrowhead with its two gaps each take 3.75 of the
            // radius, a tie the arrowheads win in Arial (with the SHX font ties of the same figures came out either way): the
            // words stand above a landing 4.656 long.
            DxfDimension dimension = Round("DRA", 7, 90);

            Near(2.6407, 13.875, Words(dimension).Location);
            HasLine(dimension, 0, 12, 4.6564, 12);
        }
    }
}
