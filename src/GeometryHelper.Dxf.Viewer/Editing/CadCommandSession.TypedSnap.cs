using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A point typed after an object snap override (END, MID, CEN typed, or chosen from the Shift+right-click menu), which AutoCAD
    /// 2021 takes as a click: the objects within the aperture of the point are looked at, and the snap point of any of them
    /// nearest the point is the answer, or "No Endpoint found for specified point." and the prompt is asked again.
    /// </summary>
    public sealed partial class CadCommandSession
    {
        /// <summary>
        /// Gets or sets the object snap taken for the next point alone (the window sets it with its own override): a point typed
        /// at a prompt for a point, at "Select objects:" or at a prompt for one object is snapped to the snap point of the
        /// object under it, within <see cref="SnapAperture"/>, as AutoCAD 2021 snaps it, the point of an object found however far
        /// from the typed point it lies: with END, an aperture of 1.3 and a line from (0, 0) to (100, 0), 30,1 gives (0, 0) and
        /// 70,1 gives (100, 0), but 50,3 gives "No Endpoint found for specified point." and then the words of the prompt it was
        /// typed at ("Invalid point.", "*Invalid selection*" at "Select objects:"), and the prompt is asked again. The
        /// override is for one point: it is cleared once a point is typed. ENDpoint, MIDpoint, CENter, QUAdrant, NODe, INSert,
        /// NEArest, PERpendicular and TANgent are taken; the others and NONe leave the point as typed. Null for none.
        /// </summary>
        public CadSnapModes? SnapOverride { get; set; }

        /// <summary>
        /// Gets or sets the aperture of the object snap in drawing units, 10 pixels over the zoom as the canvas sets it with the
        /// cursor: how near an object must lie to a point typed after an override (<see cref="SnapOverride"/>) to be snapped.
        /// </summary>
        public double SnapAperture { get; set; } = 6.0;

        /// <summary>
        /// Snaps a point typed after an object snap override. Where none is set, or it is one not taken, the point stays.
        /// </summary>
        /// <param name="point">The point typed; the point snapped.</param>
        /// <param name="missing">The words said where the snap finds nothing under the point ("No Endpoint found for specified point."); null otherwise.</param>
        /// <returns>Whether the point goes on: false where the snap found nothing.</returns>
        private bool TrySnapTyped(ref GeoPoint2 point, out string missing)
        {
            missing = null;
            CadSnapModes? modes = SnapOverride;

            if (!modes.HasValue)
            {
                return true;
            }

            SnapOverride = null;

            if (!TryTypedKind(modes.Value, out CadSnapKind kind, out string name))
            {
                return true;
            }

            GeoPoint2? from = null;

            if (kind == CadSnapKind.Perpendicular || kind == CadSnapKind.Tangent)
            {
                from = Prompt.BasePoint;

                if (!from.HasValue)
                {
                    return true;
                }
            }

            GeoPoint2? best = null;
            double nearest = double.MaxValue;

            foreach (
                string handle in Document
                    .GetPickIndex(Document.WorkingSpace)
                    .Pick(point.X, point.Y, Math.Max(SnapAperture, 1E-9))
            )
            {
                GeoPoint2? found = CadCalcSnap.Snap(this, kind, handle, point, from);

                if (found.HasValue && (found.Value - point).Length < nearest)
                {
                    best = found;
                    nearest = (found.Value - point).Length;
                }
            }

            if (best.HasValue)
            {
                point = best.Value;
                return true;
            }

            missing = "No " + (name.Length > 0 ? name + " " : string.Empty) + "found for specified point.";
            return false;
        }

        /// <summary>The kind of snap a typed point takes, and the name AutoCAD says it by in "No Endpoint found for specified point.": none for NEArest and TANgent.</summary>
        private static bool TryTypedKind(CadSnapModes mode, out CadSnapKind kind, out string name)
        {
            kind = CadSnapKind.None;
            name = string.Empty;

            switch (mode)
            {
                case CadSnapModes.Endpoint:
                    kind = CadSnapKind.Endpoint;
                    name = "Endpoint";
                    break;
                case CadSnapModes.Midpoint:
                    kind = CadSnapKind.Midpoint;
                    name = "Midpoint";
                    break;
                case CadSnapModes.Center:
                    kind = CadSnapKind.Center;
                    name = "Center";
                    break;
                case CadSnapModes.Quadrant:
                    kind = CadSnapKind.Quadrant;
                    name = "Quadrant";
                    break;
                case CadSnapModes.Node:
                    kind = CadSnapKind.Node;
                    name = "Node";
                    break;
                case CadSnapModes.Insertion:
                    kind = CadSnapKind.Insertion;
                    name = "Insert";
                    break;
                case CadSnapModes.Perpendicular:
                    kind = CadSnapKind.Perpendicular;
                    name = "Perpendicular";
                    break;
                case CadSnapModes.Nearest:
                    kind = CadSnapKind.Nearest;
                    break;
                case CadSnapModes.Tangent:
                    kind = CadSnapKind.Tangent;
                    break;
                default:
                    return false;
            }

            return true;
        }
    }
}
