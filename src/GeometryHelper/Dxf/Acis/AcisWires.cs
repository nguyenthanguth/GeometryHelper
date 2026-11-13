using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// One use of an edge by a face, as AutoCAD writes a solid or a region for R12: the edge as the edge itself runs (a line
    /// from its start vertex to its end, an arc of an ellipse), or a vertex alone where an edge has no curve (the apex of a
    /// cone); with the outward normal of the face where that face is flat.
    /// </summary>
    internal sealed class AcisWire
    {
        internal AcisWire(AcisCurve curve, AcisVec point, bool planar, AcisVec faceNormal)
        {
            Curve = curve;
            Point = point;
            IsPlanar = planar;
            FaceNormal = faceNormal;
        }

        /// <summary>The edge; null where it is a vertex alone.</summary>
        internal AcisCurve Curve { get; }

        /// <summary>Where a vertex alone stands.</summary>
        internal AcisVec Point { get; }

        /// <summary>Whether the face lies on a plane.</summary>
        internal bool IsPlanar { get; }

        /// <summary>The unit normal of a flat face, pointing out of the body; nought for a face that is not flat.</summary>
        internal AcisVec FaceNormal { get; }
    }

    /// <summary>
    /// The wires of the ACIS data of a solid or a region: each coedge of each loop of each face, in the order the data lists
    /// them, which is the order AutoCAD writes their lines when it saves the drawing as R12.
    /// </summary>
    internal sealed class AcisWires
    {
        internal AcisWires(IReadOnlyList<AcisWire> items, int skipped)
        {
            Items = items;
            Skipped = skipped;
        }

        /// <summary>The wires, face by face and loop by loop.</summary>
        internal IReadOnlyList<AcisWire> Items { get; }

        /// <summary>How many uses of an edge were left out because the edge is a spline or another curve that is neither straight nor an ellipse.</summary>
        internal int Skipped { get; }

        /// <summary>The wires moved by a transformation that scales alike in every direction.</summary>
        /// <exception cref="NotSupportedException">Thrown when the transformation scales more one way than another, or shears.</exception>
        internal AcisWires Transformed(GeoTransform3 transform)
        {
            bool mirrors = AcisBody.Mirrors(transform);
            var moved = new List<AcisWire>(Items.Count);

            foreach (AcisWire wire in Items)
            {
                AcisVec normal = wire.IsPlanar ? AcisCurve.Turned(transform, wire.FaceNormal).Unit : wire.FaceNormal;
                moved.Add(
                    new AcisWire(
                        wire.Curve?.Transformed(transform, mirrors),
                        AcisCurve.Moved(transform, wire.Point),
                        wire.IsPlanar,
                        wire.IsPlanar && mirrors ? -normal : normal
                    )
                );
            }

            return new AcisWires(moved, Skipped);
        }
    }
}
