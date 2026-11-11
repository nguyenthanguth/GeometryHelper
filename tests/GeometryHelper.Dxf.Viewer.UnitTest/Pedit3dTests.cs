using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// PEDIT of a 3D polyline against AutoCAD 2021: the prompts and the vertices are those AutoCAD's own console asked and
    /// stored for the 3DPOLY (0, 0, 0), (100, 0, 10), (100, 100, 20), (0, 100, 30) and the same input, listed with entget.
    /// </summary>
    public class Pedit3dTests
    {
        private const string Options = "Enter an option [Close/Join/Edit vertex/Spline curve/Decurve/Reverse/Undo]:";
        private const string ClosedOptions = "Enter an option [Open/Join/Edit vertex/Spline curve/Decurve/Reverse/Undo]:";
        private const string VertexOptions = "Enter a vertex editing option [Next/Previous/Break/Insert/Move/Regen/Straighten/eXit] <N>:";

        private static readonly GeoPoint3[] Corners = { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 10), new GeoPoint3(100, 100, 20), new GeoPoint3(0, 100, 30) };

        private static CadDocument Drawn(out CadCommandSession session)
        {
            CadDocument document = Blank();
            session = Session(document);
            document.ApplyEdit("draw", null, s => s.Append(document.ModelSpace, new DxfPolyline3d(DxfPoly3dType.SimplePoly, Corners, false)));
            return document;
        }

        private static List<DxfPolyline3d> Polylines(CadDocument document) => All<DxfPolyline3d>(document.ModelSpace);

        /// <summary>The vertices in their order, each x, y, z and then its flags as AutoCAD writes them (32, 48 frame, 40 curve).</summary>
        private static void Vertices(DxfPolyline3d polyline, params double[] xyzf)
        {
            List<DxfPolylineVertex3d> vertices = polyline.ToList();
            Assert.Equal(xyzf.Length / 4, vertices.Count);

            for (int i = 0; i < vertices.Count; i++)
            {
                Near(new GeoPoint3(xyzf[4 * i], xyzf[4 * i + 1], xyzf[4 * i + 2]), vertices[i].Position, 1E-8);
                int flags = vertices[i].VertexType == DxfVertex3dType.ControlVertex ? 48 : vertices[i].VertexType == DxfVertex3dType.FitVertex ? 40 : 32;
                Assert.Equal((int)xyzf[4 * i + 3], flags);
            }
        }

        [Fact]
        public void CloseOpenAndUndo_AskAutoCadsPrompts_AndUndoSaysWhenNothingIsLeft()
        {
            CadDocument document = Drawn(out CadCommandSession session);

            Run(session, "PEDIT", P(50, 0));
            Assert.Equal(Options, session.PromptText);
            Run(session, "C");
            Assert.True(Assert.Single(Polylines(document)).Closed);
            Assert.Equal(ClosedOptions, session.PromptText);
            Run(session, "U");
            Assert.False(Assert.Single(Polylines(document)).Closed);
            Run(session, "U");
            Assert.Contains("Command has been completely undone.", session.History);
            Run(session, "C", "O", Enter);
            Assert.False(session.IsRunning);
            Vertices(Assert.Single(Polylines(document)), 0, 0, 0, 32, 100, 0, 10, 32, 100, 100, 20, 32, 0, 100, 30, 32);

            Run(session, "PEDIT", P(50, 0), "J");
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, "ALL", Enter);
            Assert.Contains("0 segments added to polyline", session.History);
            Assert.Equal(Options, session.PromptText);
        }

        [Fact]
        public void SplineCurveClosedReversedAndDecurved_IsWhatAutoCadMade()
        {
            CadDocument document = Drawn(out CadCommandSession session);

            // AutoCAD 2021 at SPLINETYPE 6 and SPLINESEGS 8: the first vertex of the frame, the 9 of the curve, the rest.
            Run(session, "PEDIT", P(50, 0), "S");
            DxfPolyline3d polyline = Assert.Single(Polylines(document));
            Assert.Equal(DxfPoly3dType.CubicSplinePoly, polyline.PolyType);
            Vertices(
                polyline,
                0, 0, 0, 48, 0, 0, 0, 40, 32.8125, 4.296875, 3.75, 40, 56.25, 15.625, 7.5, 40, 70.3125, 31.640625, 11.25, 40,
                75, 50, 15, 40, 70.3125, 68.359375, 18.75, 40, 56.25, 84.375, 22.5, 40, 32.8125, 95.703125, 26.25, 40, 0, 100, 30, 40,
                100, 0, 10, 48, 100, 100, 20, 48, 0, 100, 30, 48);

            // Closed, fitted again round the frame: 32 vertices of the curve, the first at (83.3333, 16.6667, 10).
            Run(session, "C");
            polyline = Assert.Single(Polylines(document));
            List<DxfPolylineVertex3d> vertices = polyline.ToList();
            Assert.Equal(36, vertices.Count);
            Near(new GeoPoint3(83.3333333333, 16.6666666667, 10), vertices[1].Position, 1E-8);
            Near(new GeoPoint3(50, 95.8333333333, 24.1666666667), vertices[13].Position, 1E-8);
            Near(new GeoPoint3(76.3671875, 11.1979166667, 8.7630208333), vertices[32].Position, 1E-8);

            // Reversed, the first vertex too: the frame (0, 100, 30), (100, 100, 20), (100, 0, 10), (0, 0, 0), the curve from
            // (83.3333, 83.3333, 20).
            Run(session, "R");
            vertices = Assert.Single(Polylines(document)).ToList();
            Assert.Equal(36, vertices.Count);
            Near(new GeoPoint3(0, 100, 30), vertices[0].Position);
            Near(new GeoPoint3(83.3333333333, 83.3333333333, 20), vertices[1].Position, 1E-8);
            Near(new GeoPoint3(76.3671875, 88.8020833333, 21.2369791667), vertices[32].Position, 1E-8);
            Near(new GeoPoint3(0, 0, 0), vertices[35].Position);

            Run(session, "D", Enter);
            polyline = Assert.Single(Polylines(document));
            Assert.True(polyline.Closed);
            Assert.Equal(DxfPoly3dType.SimplePoly, polyline.PolyType);
            Vertices(polyline, 0, 100, 30, 32, 100, 100, 20, 32, 100, 0, 10, 32, 0, 0, 0, 32);
            AssertReopens(document);
        }

        [Fact]
        public void EditVertex_InsertsMovesBreaksAndStraightens_AsAutoCad2021Did()
        {
            CadDocument document = Drawn(out CadCommandSession session);
            Run(session, "PEDIT", P(50, 0), "E");
            Assert.Equal(VertexOptions, session.PromptText);
            Near(P(0, 0), session.Prompt.Mark.Value);
            Run(session, "P", "R");
            Assert.Equal(VertexOptions.Replace("<N>", "<P>"), session.PromptText);
            Run(session, "N", "I");
            Assert.Equal("Specify location for new vertex:", session.PromptText);
            Run(session, P(150, 50), "X", Enter);
            Vertices(Assert.Single(Polylines(document)), 0, 0, 0, 32, 100, 0, 10, 32, 150, 50, 0, 32, 100, 100, 20, 32, 0, 100, 30, 32);
            Run(session, "U");

            Run(session, "PEDIT", P(50, 0), "E", "N", "M", P(150, 50), "X", Enter);
            Vertices(Assert.Single(Polylines(document)), 0, 0, 0, 32, 150, 50, 0, 32, 100, 100, 20, 32, 0, 100, 30, 32);
            Run(session, "U");

            // Break at the second vertex and the third: (0, 0, 0) to (100, 0, 10), and a new one from (100, 100, 20) on.
            Run(session, "PEDIT", P(50, 0), "E", "N", "B");
            Assert.Equal("Enter an option [Next/Previous/Go/eXit] <N>:", session.PromptText);
            Run(session, "N", "G", "X", Enter);
            List<DxfPolyline3d> pieces = Polylines(document);
            Assert.Equal(2, pieces.Count);
            Vertices(pieces[0], 0, 0, 0, 32, 100, 0, 10, 32);
            Vertices(pieces[1], 100, 100, 20, 32, 0, 100, 30, 32);
            Run(session, "U");

            Run(session, "PEDIT", P(50, 0), "E", "S", "N", "N", "G", "X", Enter);
            Vertices(Assert.Single(Polylines(document)), 0, 0, 0, 32, 100, 100, 20, 32, 0, 100, 30, 32);
            Run(session, "U");

            // Next stops at the end of a closed one too: Break there and Go is "*Invalid*", as AutoCAD said.
            Run(session, "PEDIT", P(50, 0), "C", "E", "N", "N", "N", "N", "B", "N", "G");
            Assert.Contains("*Invalid*", session.History);
            Run(session, "X", Enter);
            Assert.True(Assert.Single(Polylines(document)).Closed);
        }

        [Fact]
        public void EditVertexOfASpline_MovesAVertexOfTheFrame_AndFitsItAgain()
        {
            CadDocument document = Drawn(out CadCommandSession session);

            // AutoCAD 2021: S, then E N M 150,50: the frame (0, 0, 0), (150, 50, 0), ..., the curve through (47.168, 18.652, 0.879).
            Run(session, "PEDIT", P(50, 0), "S", "E", "N", "M", P(150, 50), "X", Enter);
            Vertices(
                Assert.Single(Polylines(document)),
                0, 0, 0, 48, 0, 0, 0, 40, 47.16796875, 18.65234375, 0.87890625, 40, 77.34375, 36.71875, 3.28125, 40,
                92.28515625, 53.61328125, 6.85546875, 40, 93.75, 68.75, 11.25, 40, 83.49609375, 81.54296875, 16.11328125, 40,
                63.28125, 91.40625, 21.09375, 40, 34.86328125, 97.75390625, 25.83984375, 40, 0, 100, 30, 40,
                150, 50, 0, 48, 100, 100, 20, 48, 0, 100, 30, 48);
        }
    }
}
