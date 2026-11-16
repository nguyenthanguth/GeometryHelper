using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The grips of a revision cloud while REVCLOUDGRIPS is on, as AutoCAD 2021's <c>GetGripPoints</c> gives them.</summary>
    public static partial class CadGrips
    {
        /// <summary>
        /// Gets the grips of an entity as a document shows them: those <see cref="Of"/> gives, but for a revision
        /// cloud while the document's <see cref="CadDocument.RevCloudGrips"/> is on, which has the few grips of the shape it
        /// goes round. A cloud (a closed polyline of six vertices or more with REVCLOUD's data) round a rectangle (0, 0) to
        /// (60, 40) with arcs 10 long has eight: (0, 0), (0, 20), (0, 40), (30, 40), (60, 40), (60, 20), (60, 0) and (30, 0), a
        /// vertex grip at each corner and a middle grip at each side in turn from the first vertex. A cloud round the polygon
        /// (100, 0), (160, 0), (160, 40), (130, 60), (100, 40) has ten, (130, 60) among them, a corner the arcs cut off. A cloud
        /// round a circle has five, its centre and its quadrants to the right, left, above and below. Dragging one draws the
        /// cloud again along the new shape (see <see cref="Drag"/>); the grips of its vertices come back with REVCLOUDGRIPS 0.
        /// </summary>
        /// <param name="document">The document the entity is in.</param>
        /// <param name="entity">The entity.</param>
        /// <returns>The grips in the order of their indices; empty for an entity with none.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the entity is null.</exception>
        public static IReadOnlyList<CadGrip> Shown(CadDocument document, DxfEntity entity)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            return (document.RevCloudGrips ? CadCommands.RevCloudFewGrips(entity) : null) ?? Of(entity);
        }

        /// <summary>A grip of a cloud with few grips dragged: the cloud drawn again along its shape as the grip changes it.</summary>
        private static CadEditResult DragRevCloud(
            CadDocument document,
            string handle,
            int gripIndex,
            GeoPoint2 to,
            string property,
            string was,
            string went
        )
        {
            if (!CadCommands.RevCloudRegrow((DxfPolyline)document.FindEntity(handle), gripIndex, to, false))
            {
                return CadEditResult.Refuse("The revision cloud would be too small for its arcs.");
            }

            return Reshape(document, handle, property, was, went, () =>
                CadCommands.RevCloudRegrow((DxfPolyline)document.FindEntity(handle), gripIndex, to, true)
            );
        }
    }
}
