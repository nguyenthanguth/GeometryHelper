using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// XCLIP generate Polyline on a boundary that does not lie in the plane of the world, as AutoCAD 2021 draws it (probes of
    /// accoreconsole, the polyline read back with entget): the polyline lies where the boundary was made, in its own plane, so
    /// that a boundary made in a system lifted 15 gives a polyline of elevation 15, and one made in a system tilted 30 degrees
    /// about X at (100, 50, 20) a polyline of the normal (0, -0.5, 0.866) whose vertices are the corners of the rectangle as the
    /// boundary's own system gives them.
    /// </summary>
    public class XClipGenerateTests
    {
        private static readonly double Cos30 = Math.Cos(Math.PI / 6.0);

        /// <summary>A block GHG of a square 300 by 300 inserted as it is given and clipped to the rectangle (10, 10) to (90, 60) of a system.</summary>
        private static CadDocument Clipped(
            CadUcs system,
            out CadCommandSession session,
            out string handle,
            GeoPoint3 at = default,
            double scale = 1.0,
            double degrees = 0.0
        )
        {
            var database = new DxfDatabase("AC1027");
            var block = new DxfBlockTableRecord { Name = "GHG" };
            database.BlockTable.Add(block);
            var square = new DxfPolyline();
            square.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
            square.AddVertexAt(1, new GeoPoint2(300, 0), 0, 0, 0);
            square.AddVertexAt(2, new GeoPoint2(300, 300), 0, 0, 0);
            square.AddVertexAt(3, new GeoPoint2(0, 300), 0, 0, 0);
            square.Closed = true;
            block.AppendEntity(square);
            var insert = new DxfBlockReference(at, block)
            {
                ScaleFactors = new GeoVector3(scale, scale, scale),
                Rotation = degrees * Math.PI / 180.0,
            };
            database.ModelSpace.AppendEntity(insert);
            insert.ClipTo(new[] { new GeoPoint2(10, 10), new GeoPoint2(90, 60) }, CadXClips.WorldToClip(system));
            handle = insert.Handle;
            CadDocument document = CadDocument.From(database, "xclipgenerate.dxf");
            session = Session(document);
            return document;
        }

        private static DxfPolyline Generated(CadDocument document, CadCommandSession session, string handle)
        {
            int before = All<DxfPolyline>(document.ModelSpace).Count;
            Run(session, "XCLIP", new[] { handle }, Enter, "generate Polyline");
            List<DxfPolyline> polylines = All<DxfPolyline>(document.ModelSpace);
            Assert.Equal(before + 1, polylines.Count);
            return polylines.Last();
        }

        private static void AssertWorld(double x, double y, double z, GeoPoint3 actual)
        {
            Assert.Equal(x, actual.X, 4);
            Assert.Equal(y, actual.Y, 4);
            Assert.Equal(z, actual.Z, 4);
        }

        [Fact]
        public void ABoundaryMadeInASystemLifted_GivesAPolylineAtThatHeight()
        {
            // AutoCAD 2021: UCS Origin 0,0,15, the rectangle 10,10 to 90,60: elevation 15, the normal (0, 0, 1), the vertices as typed.
            var lifted = new CadUcs(new GeoPoint3(0, 0, 15), GeoVector3.XAxis, GeoVector3.YAxis);
            CadDocument document = Clipped(lifted, out CadCommandSession session, out string handle);

            DxfPolyline made = Generated(document, session, handle);

            Assert.True(made.Closed);
            Assert.Equal(15.0, made.Elevation, 6);
            Assert.Equal(0.0, made.Normal.X, 6);
            Assert.Equal(0.0, made.Normal.Y, 6);
            Assert.Equal(1.0, made.Normal.Z, 6);
            Assert.Equal(4, made.NumberOfVertices);
            AssertWorld(10, 10, 15, made.GetPoint3dAt(0));
            AssertWorld(90, 10, 15, made.GetPoint3dAt(1));
            AssertWorld(90, 60, 15, made.GetPoint3dAt(2));
            AssertWorld(10, 60, 15, made.GetPoint3dAt(3));
        }

        [Fact]
        public void ABoundaryMadeInASystemTilted_GivesAPolylineInItsPlane()
        {
            // AutoCAD 2021: UCS Origin 100,50,20 then X 30, the rectangle 10,10 to 90,60: normal (0, -0.5, 0.866025), elevation
            // -7.67949, vertices (110, 63.3013), (190, 63.3013), (190, 113.301), (110, 113.301) in the polyline's own system;
            // in the world (110, 50 + 10 cos 30, 25) ... (110, 50 + 60 cos 30, 50).
            var tilted = new CadUcs(new GeoPoint3(100, 50, 20), GeoVector3.XAxis, new GeoVector3(0.0, Cos30, 0.5));
            CadDocument document = Clipped(tilted, out CadCommandSession session, out string handle);

            DxfPolyline made = Generated(document, session, handle);

            Assert.Equal(0.0, made.Normal.X, 5);
            Assert.Equal(-0.5, made.Normal.Y, 5);
            Assert.Equal(Cos30, made.Normal.Z, 5);
            Assert.Equal(-7.67949, made.Elevation, 4);
            Assert.Equal(110.0, made.GetPoint2dAt(0).X, 4);
            Assert.Equal(63.3013, made.GetPoint2dAt(0).Y, 4);
            Assert.Equal(190.0, made.GetPoint2dAt(2).X, 4);
            Assert.Equal(113.301, made.GetPoint2dAt(2).Y, 3);
            AssertWorld(110, 50 + 10 * Cos30, 25, made.GetPoint3dAt(0));
            AssertWorld(190, 50 + 10 * Cos30, 25, made.GetPoint3dAt(1));
            AssertWorld(190, 50 + 60 * Cos30, 50, made.GetPoint3dAt(2));
            AssertWorld(110, 50 + 60 * Cos30, 50, made.GetPoint3dAt(3));
        }

        [Fact]
        public void ABoundaryOfAReferenceScaledTurnedAndLifted_GivesTheSamePolyline_AsAutoCadDoes()
        {
            // AutoCAD 2021 gave the very same polyline for the reference inserted at (30, 10, 5) scaled by 2 and turned 20
            // degrees: the boundary stands where it was picked, whatever the reference is.
            var tilted = new CadUcs(new GeoPoint3(100, 50, 20), GeoVector3.XAxis, new GeoVector3(0.0, Cos30, 0.5));
            CadDocument document = Clipped(
                tilted,
                out CadCommandSession session,
                out string handle,
                new GeoPoint3(30, 10, 5),
                2.0,
                20.0
            );

            DxfPolyline made = Generated(document, session, handle);

            Assert.Equal(-0.5, made.Normal.Y, 5);
            Assert.Equal(-7.67949, made.Elevation, 4);
            AssertWorld(110, 50 + 10 * Cos30, 25, made.GetPoint3dAt(0));
            AssertWorld(190, 50 + 60 * Cos30, 50, made.GetPoint3dAt(2));
        }
    }
}
