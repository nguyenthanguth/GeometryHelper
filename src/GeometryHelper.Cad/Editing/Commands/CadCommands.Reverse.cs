using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// REVERSE, as AutoCAD 2021 asks it: "Select line, polyline, spline or helix to reverse direction:", the objects, then
    /// "Object direction has been reversed." once for the whole run. Whatever else is selected is passed over without a word
    /// ("0 found" where nothing is of the four kinds). A line is turned from its end to its start, a polyline has its
    /// vertices in the other order (a closed one too, its first vertex last: the square (0, 0), (10, 0), (10, 10) is
    /// (10, 10), (10, 0), (0, 0)), each bulge turned over and each segment's widths swapped, a spline its control points and
    /// fit points. A linetype drawn along the object follows, since the object starts from its other end. A helix is not read
    /// by the presenter, so one is passed over.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Reverse(CadCommandContext c)
        {
            c.Write("Select line, polyline, spline or helix to reverse direction:");

            foreach (CadPrompt prompt in c.SelectObjectsOf("Select objects", Reversible))
            {
                yield return prompt;
            }

            var handles = new List<string>(c.Selected);

            if (handles.Count == 0)
            {
                yield break;
            }

            var planned = handles.Select(h => new CadEditRecord(CadEditRecordKind.Reshaped, h, "Reverse")).ToList();
            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (string handle in handles)
                {
                    DxfEntity entity = s.Document.FindEntity(handle);

                    if (entity is DxfCurve curve && !curve.IsErased)
                    {
                        ReverseOneWay(curve);
                    }
                }
            });

            if (result.Succeeded)
            {
                c.Write("Object direction has been reversed.");
            }
        }

        /// <summary>Whether REVERSE turns the entity: a line, a polyline of any of the three kinds, or a spline.</summary>
        private static bool Reversible(DxfEntity entity) =>
            entity is DxfLine
            || entity is DxfPolyline
            || entity is DxfPolyline2d
            || entity is DxfPolyline3d
            || entity is DxfSpline;

        /// <summary>
        /// Turns a curve to run the other way as the REVERSE and PEDIT commands do: the library's reversal, which keeps the first
        /// vertex of a closed polyline in its place, then that vertex put last, as AutoCAD's commands put it: the vertices of a
        /// closed polyline in the other order throughout. A 3D polyline is turned by its frame and fitted again as it was, a
        /// polyline fitted to a spline by its frame, its curve's vertices written anew.
        /// </summary>
        private static void ReverseOneWay(DxfCurve curve)
        {
            // The frame of a 3D polyline turned round, the first vertex of a closed one too, and fitted again as it was.
            if (curve is DxfPolyline3d three)
            {
                List<GeoPoint3> frame = Pedit3dFrame(three);
                frame.Reverse();
                Pedit3dWrite(three, frame, three.Closed, three.PolyType);
                return;
            }

            // A spline is fitted again on its frame turned round, its curve's vertices written anew as AutoCAD writes them.
            if (
                curve is DxfPolyline2d heavy
                && (heavy.PolyType == DxfPoly2dType.QuadSplinePoly || heavy.PolyType == DxfPoly2dType.CubicSplinePoly)
            )
            {
                DxfPoly2dType type = heavy.PolyType;
                heavy.Straighten();
                heavy.ReverseCurve();
                heavy.ConvertToPolyType(type);
                return;
            }

            curve.ReverseCurve();

            // A closed polyline: the library's reversal leaves its first vertex first, AutoCAD's commands put it last.
            if (curve is DxfPolyline light && light.Closed && light.NumberOfVertices > 2)
            {
                GeoPoint2 at = light.GetPoint2dAt(0);
                double bulge = light.GetBulgeAt(0);
                double start = light.GetStartWidthAt(0);
                double end = light.GetEndWidthAt(0);
                light.RemoveVertexAt(0);
                light.AddVertexAt(light.NumberOfVertices, at, bulge, start, end);
            }
        }
    }
}
