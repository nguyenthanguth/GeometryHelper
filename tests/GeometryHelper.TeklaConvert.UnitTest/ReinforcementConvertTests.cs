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
    /// Reading reinforcement as it was set out.
    /// <para>
    /// There are two ways to read reinforcement and they answer different questions: what Tekla worked out,
    /// which needs a running model, and what was typed in, which does not. The types that carry the set-out —
    /// <c>Polygon</c>, <c>SingleRebar</c>, <c>RebarGroup</c> and the rest — turn out to be plain holders that
    /// can be built without Tekla, so all of that is tested here. Only <c>GetRebarGeometries</c> needs the
    /// modeller, and nothing here pretends otherwise.
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
        public void ASingleBarReadsItsOwnSetOut()
        {
            var rebar = new TSM.SingleRebar { Polygon = Elbow() };

            rebar.RadiusValues = Radii(50.0);

            GeoPolylineArc3 bar = rebar.ToSetOutPolylineArc3();

            Assert.Equal(1, bar.GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(50.0, bar.GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);
            Assert.Equal(250.0 + Math.PI * 50.0 / 2.0 + 250.0, bar.Length, 6);

            // The tolerance form answers the same.
            Assert.True(rebar.ToSetOutPolylineArc3(Tolerance.Global).IsEqualTo(bar, Loose));

            // No radii at all is a bar of straight runs through every point it was set out by.
            var sharp = new TSM.SingleRebar { Polygon = Elbow() };

            Assert.Equal(0, sharp.ToSetOutPolylineArc3().GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(600.0, sharp.ToSetOutPolylineArc3().Length, 6);
        }

        [Fact]
        public void AGroupReadsTheShapesItWasSetOutByAndNotEveryBarOfIt()
        {
            var group = new TSM.RebarGroup();

            group.Polygons.Add(Elbow());
            group.Polygons.Add(Tie());
            group.RadiusValues = Radii(40.0);

            GeoPolylineArc3[] shapes = group.ToSetOutPolylineArc3s();

            // One for each polygon it was set out by: the shape at each end of the run, not the forty bars
            // Tekla spreads between them.
            Assert.Equal(2, shapes.Length);
            Assert.Equal(1, shapes[0].GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(40.0, shapes[0].GetEdges().Single(edge => edge.IsArc).ToArc().Radius, 6);

            // A group with nothing set out gives nothing rather than throwing.
            Assert.Empty(new TSM.RebarGroup().ToSetOutPolylineArc3s());
        }

        [Fact]
        public void TheOtherKindsOfGroupReadTheirOwnSetOutToo()
        {
            var curved = new TSM.CurvedRebarGroup { Polygon = Elbow() };
            var circle = new TSM.CircleRebarGroup { Polygon = Elbow() };

            curved.RadiusValues = Radii(50.0);
            circle.RadiusValues = Radii(50.0);

            Assert.Equal(1, curved.ToSetOutPolylineArc3().GetEdges().Count(edge => edge.IsArc));
            Assert.Equal(1, circle.ToSetOutPolylineArc3().GetEdges().Count(edge => edge.IsArc));
            Assert.True(curved.ToSetOutPolylineArc3(Tolerance.Global).IsEqualTo(curved.ToSetOutPolylineArc3(), Loose));

            // A strand runs between two points and does not bend.
            var strand = new TSM.RebarStrand { StartPoint = At(0, 0, 0), EndPoint = At(0, 0, 3000) };

            Assert.Equal(3000.0, strand.ToSetOutLine3().Length, 6);
            Assert.Equal(3000.0, strand.ToSetOutLine3(Tolerance.Global).Length, 6);

            // A mesh is set out by an outline, which is a loop and not a bar, and is named for that.
            var mesh = new TSM.RebarMesh { Polygon = Tie() };

            Assert.Equal(60000.0, mesh.ToSetOutPolygon3().Area, 6);
        }

        [Fact]
        public void TheSetOutIsNamedApartFromWhatTeklaWorksOut()
        {
            // The two readings answer different questions, so they are named apart. Had they shared a name,
            // a call on a variable typed RebarGroup would have taken the more specific overload and silently
            // read the set-out, while the same call on one typed Reinforcement read the model.
            var group = new TSM.RebarGroup();

            group.Polygons.Add(Elbow());

            TSM.Reinforcement asReinforcement = group;

            // Both names exist, on both static types, and neither hides the other.
            Assert.Single(group.ToSetOutPolylineArc3s());
            Assert.NotNull((Func<GeoPolylineArc3[]>)group.ToGeoPolylineArc3s);
            Assert.NotNull((Func<GeoPolylineArc3[]>)asReinforcement.ToGeoPolylineArc3s);
        }

        [Fact]
        public void NothingIsAskedOfNothing()
        {
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToGeoPolylineArc3s((TSM.Reinforcement)null));
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToGeoPolylineArc3s((TSM.RebarSet)null));
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToSetOutPolylineArc3((TSM.SingleRebar)null));
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToSetOutPolylineArc3s((TSM.RebarGroup)null));
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToSetOutPolylineArc3((TSM.CurvedRebarGroup)null));
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToSetOutPolylineArc3((TSM.CircleRebarGroup)null));
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToSetOutLine3((TSM.RebarStrand)null));
            Assert.Throws<ArgumentNullException>(() => ReinforcementConvert.ToSetOutPolygon3((TSM.RebarMesh)null));
            Assert.Throws<ArgumentNullException>(() => PolygonConvert.ToGeoPolyline3((TSM.Polygon)null));
            Assert.Throws<ArgumentNullException>(() => PolygonConvert.ToTeklaPolygon((GeoPolyline3)null));

            // A bar with no polygon has nothing to be set out by, and says which argument is at fault.
            Assert.Throws<ArgumentException>(() => new TSM.SingleRebar().ToSetOutPolylineArc3());
            Assert.Throws<ArgumentException>(() => new TSM.RebarMesh().ToSetOutPolygon3());
            Assert.Throws<ArgumentException>(() => new TSM.RebarStrand().ToSetOutLine3());
        }
    }
}
