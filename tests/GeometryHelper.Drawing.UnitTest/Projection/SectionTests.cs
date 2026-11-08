using System;
using System.Linq;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Drawing.UnitTest.Projection.ProjectionKit;

namespace GeometryHelper.Drawing.UnitTest.Projection
{
    public class SectionTests
    {
        [Fact]
        public void ABeamCutAcrossShowsItsISectionInFrontOfTheEndPlateBehindAndNothingOfTheOtherEnd()
        {
            var cut = new GeoPlane3(new GeoPoint3(500, 0, 0), GeoVector3.XAxis);
            var options = new ProjectionOptions(sectionPlane: cut);

            ProjectedView2 view = ViewProjector.Project(BeamWithEndPlates(), Right, options, Tol);

            ProjectedFace2 face = Assert.Single(view.SectionFaces);
            Assert.Equal("beam", face.ObjectId);
            Assert.Equal(2 * 100 * 10 + 180 * 6, face.Face.Area, 6);
            Assert.Equal(12, view.VisibleEdges.Count(e => e.Kind == EdgeKind.SectionOutline));
            Assert.Equal(16, view.VisibleEdges.Count);
            Assert.Empty(view.HiddenEdges);
            Assert.DoesNotContain(view.VisibleEdges, e => e.ObjectIds.Contains("plate-2"));
            HasLine(view.VisibleEdges, -60, -110, 60, -110);
            HasLine(view.VisibleEdges, -50, 90, -3, 90);

            // The section is one face: no line runs across the web where it meets a flange.
            Assert.DoesNotContain(view.VisibleEdges, e => !e.IsArc && Math.Abs(e.StartPoint.Y - 90) < 1E-3 && Math.Abs(e.EndPoint.Y - 90) < 1E-3 && Math.Min(e.StartPoint.X, e.EndPoint.X) < 2.9 && Math.Max(e.StartPoint.X, e.EndPoint.X) > -2.9);
        }

        [Fact]
        public void ThePlateOfTC1CutThroughTwoHolesHasTheSectionAreaOfTheBody()
        {
            TestBody tc1 = TestBodies.TC1();
            GeoSolid3 plate = tc1.Parts[0].Object.Solid;
            var cut = new GeoPlane3(new GeoPoint3(50, 0, 0), GeoVector3.XAxis);

            ProjectedView2 view = ViewProjector.Project(new[] { new ProjectionInput("plate", plate) }, Right, new ProjectionOptions(sectionPlane: cut), Tol);

            double expected = plate.Section(cut, Tol).Sum(f => f.Area);
            Assert.Equal(3, view.SectionFaces.Count);
            Assert.Equal(expected, view.SectionFaces.Sum(f => f.Face.Area), 6);
            Assert.Equal(20 * (200 - 2 * 22), expected, 6);
            Assert.Equal(12, view.VisibleEdges.Count(e => e.Kind == EdgeKind.SectionOutline));
            Assert.Empty(Overlaps(view.VisibleEdges, view.VisibleEdges, true));
            Assert.Empty(Overlaps(view.VisibleEdges, view.HiddenEdges, false));
        }

        [Fact]
        public void APlaneTurnedAwayFromTheViewerKeepsTheSameSide()
        {
            var towards = new GeoPlane3(new GeoPoint3(500, 0, 0), GeoVector3.XAxis);
            var away = new GeoPlane3(new GeoPoint3(500, 0, 0), GeoVector3.XAxis.Negate());

            string a = Signature(ViewProjector.Project(BeamWithEndPlates(), Right, new ProjectionOptions(sectionPlane: towards), Tol));
            string b = Signature(ViewProjector.Project(BeamWithEndPlates(), Right, new ProjectionOptions(sectionPlane: away), Tol));

            Assert.Equal(a, b);
        }
    }
}
