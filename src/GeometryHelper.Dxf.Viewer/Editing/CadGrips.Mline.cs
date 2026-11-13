using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The grips of a multiline: one at each vertex, which drags the vertex and builds the multiline anew.</summary>
    public static partial class CadGrips
    {
        /// <summary>
        /// Moves a vertex of a multiline to a point, as AutoCAD 2021's <c>MoveVertexAt</c> does: a segment that keeps its way
        /// keeps its cuts, one that turns is whole again. Refused where the vertex would fall on the one beside it.
        /// </summary>
        private static CadEditResult DragMline(
            CadDocument document,
            DxfUnknownEntity mline,
            string handle,
            int gripIndex,
            GeoPoint2 from,
            GeoPoint2 to
        )
        {
            var builder = new DxfMlineBuilder(mline);
            GeoPoint3 at = builder.Points[gripIndex];
            builder.MoveVertexAt(gripIndex, new GeoPoint3(to.X, to.Y, at.Z));

            try
            {
                builder.Build();
            }
            catch (ArgumentException)
            {
                return CadEditResult.Refuse("A vertex of a multiline cannot fall on the one beside it.");
            }

            return Reshape(
                document,
                handle,
                "Grip " + gripIndex,
                CadCommands.Values(from.X, from.Y),
                CadCommands.Values(to.X, to.Y),
                () =>
                {
                    var again = new DxfMlineBuilder((DxfUnknownEntity)document.FindEntity(handle));
                    again.MoveVertexAt(gripIndex, new GeoPoint3(to.X, to.Y, at.Z));
                    again.Rebuild((DxfUnknownEntity)document.FindEntity(handle));
                    return true;
                }
            );
        }
    }
}
