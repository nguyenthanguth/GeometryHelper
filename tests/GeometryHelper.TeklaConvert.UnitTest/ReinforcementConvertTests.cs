using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.TeklaConvert;
using Xunit;
using TSG = Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;

namespace GeometryHelper.TeklaConvert.UnitTest
{
    /// <summary>
    /// Reading reinforcement, and the Tekla polygon every kind of it is laid out with.
    /// <para>
    /// A bar is read as Tekla works it out, which needs a running model, so what can be shown here is the
    /// polygon conversion underneath it and the guards around the read. Nothing here pretends to test
    /// <c>GetRebarGeometries</c>.
    /// </para>
    /// </summary>
    public class ReinforcementConvertTests
    {
        private static readonly Tolerance Loose = new Tolerance(1E-6, 1E-6);

        private static TSG.Point At(double x, double y, double z) => new TSG.Point(x, y, z);

        /// <summary>
        /// A Tekla polygon set out as an L: two legs of three hundred meeting at a right angle.
        /// </summary>
        private static TSM.Polygon Elbow()
        {
            var polygon = new TSM.Polygon();

            polygon.Points.Add(At(0, 0, 0));
            polygon.Points.Add(At(300, 0, 0));
            polygon.Points.Add(At(300, 300, 0));

            return polygon;
        }

        /// <summary>
        /// A closed rectangle, three hundred by two hundred, as a tie is set out.
        /// </summary>
        private static TSM.Polygon Tie()
        {
            var polygon = new TSM.Polygon();

            polygon.Points.Add(At(0, 0, 0));
            polygon.Points.Add(At(300, 0, 0));
            polygon.Points.Add(At(300, 200, 0));
            polygon.Points.Add(At(0, 200, 0));

            return polygon;
        }

        private static ArrayList Radii(params double[] values) => new ArrayList(values);

        [Fact]
        public void APolygonIsTheShapeEveryKindOfReinforcementIsSetOutBy()
        {
            TSM.Polygon elbow = Elbow();

            Assert.Equal(3, elbow.ToTeklaPoints().Count);
            Assert.Equal(600.0, elbow.ToGeoPolyline3().Length, 6);

            // Bent at its one turn, it is shorter than its set-out and misses its own corner.
            GeoPolylineArc3 bar = elbow.ToGeoPolylineArc3(new[] { 50.0 });

            Assert.Equal(1, bar.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(50.0, bar.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);
            Assert.True(bar.Length < 600.0);
            Assert.False(bar.IsPointOn(new GeoPoint3(300, 0, 0)));

            // A closed set-out reads as a loop, and a loop encloses an area.
            GeoPolygon3 tie = Tie().ToGeoPolygon3();

            Assert.Equal(4, tie.VertexCount);
            Assert.Equal(60000.0, tie.Area, 6);

            // And it rounds at every corner, the closing one included, which is what a tie is.
            GeoPolygonArc3 bentTie = Tie().ToGeoPolygonArc3(40.0);

            Assert.Equal(4, bentTie.GetEdges().Count(edge => edge.IsArc));
            Assert.True(bentTie.Length < tie.Length);
        }

        [Fact]
        public void APolygonGoesBackToTeklaAndComesRoundAgain()
        {
            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0), new GeoPoint3(300, 300, 0));

            TSM.Polygon there = chain.ToTeklaPolygon();

            Assert.Equal(3, there.Points.Count);
            Assert.True(there.ToGeoPolyline3().IsEqualTo(chain, Loose));

            var loop = new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(300, 0, 0), new GeoPoint3(300, 200, 0), new GeoPoint3(0, 200, 0));

            Assert.Equal(loop.Area, loop.ToTeklaPolygon().ToGeoPolygon3().Area, 6);
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToGeoPolylineArc3s((TSM.Reinforcement)null));
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToGeoPolylineArc3s((TSM.RebarSet)null));
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToGeoPolylineArc3s((IEnumerable<TSM.Reinforcement>)null));
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToGeoPolylineArc3s((IEnumerable<TSM.Reinforcement>)null, Tolerance.Global));
            Assert.Throws<ArgumentNullException>(() => PolygonConvert.ToGeoPolyline3((TSM.Polygon)null));
            Assert.Throws<ArgumentNullException>(() => PolygonConvert.ToTeklaPolygon((GeoPolyline3)null));
            Assert.Throws<ArgumentNullException>(() => PolygonConvert.ToTeklaPolygon((GeoPolygon3)null));
        }

        [Fact]
        public void ManyReinforcementsReadInOneGoAndTheAnswerLinesUpWithTheQuestion()
        {
            // The sequence form takes an array, a list, or anything else that walks, and an empty one gives
            // an empty answer rather than reaching for a model that is not there.
            Assert.Empty(new TSM.Reinforcement[0].ToGeoPolylineArc3s());
            Assert.Empty(new List<TSM.Reinforcement>().ToGeoPolylineArc3s(Tolerance.Global));

            // One entry per reinforcement asked about, in the same order, so the answer can be read back
            // against the question. A null gives an empty entry rather than shortening the list, which would
            // put every index after it against the wrong reinforcement.
            List<GeoPolylineArc3[]> read = new TSM.Reinforcement[] { null, null, null }.ToGeoPolylineArc3s();

            Assert.Equal(3, read.Count);
            Assert.All(read, bars => Assert.Empty(bars));

            // The grouping is the point of the shape: running the bars together is a SelectMany away, and
            // no way back. Nothing further can be shown here, because past this point the read asks Tekla
            // for the geometries it worked out and that needs the modeller.
            Assert.Empty(read.SelectMany(bars => bars));

            Assert.Empty(new TSM.RebarSet().ToGeoPolylineArc3s());
        }
    }
}
