using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The snap functions of CAL (<c>end</c>, <c>mid</c>, <c>cen</c>, <c>qua</c>, <c>nea</c>, <c>ins</c>, <c>nod</c>, <c>int</c>,
    /// <c>per</c>, <c>tan</c>) and <c>rad</c>: what the object picked gives, as AutoCAD's "Select entity for END snap:" takes
    /// it. The snap is made on the object picked alone, wherever the pick lay on it: the end nearest the pick, the quadrant
    /// nearest the pick; <c>per</c> and <c>tan</c> from the last point.
    /// </summary>
    internal static class CadCalcSnap
    {
        /// <summary>The snap an object snap function of CAL makes, as the snaps of the cursor name it.</summary>
        private static CadSnapKind KindOf(string mode)
        {
            switch (mode)
            {
                case "END":
                    return CadSnapKind.Endpoint;
                case "INS":
                    return CadSnapKind.Insertion;
                case "INT":
                    return CadSnapKind.Intersection;
                case "MID":
                    return CadSnapKind.Midpoint;
                case "CEN":
                    return CadSnapKind.Center;
                case "NEA":
                    return CadSnapKind.Nearest;
                case "NOD":
                    return CadSnapKind.Node;
                case "QUA":
                    return CadSnapKind.Quadrant;
                case "PER":
                    return CadSnapKind.Perpendicular;
                default:
                    return CadSnapKind.Tangent;
            }
        }

        private static CadSnapModes ModeOf(CadSnapKind kind)
        {
            switch (kind)
            {
                case CadSnapKind.Endpoint:
                    return CadSnapModes.Endpoint;
                case CadSnapKind.Insertion:
                    return CadSnapModes.Insertion;
                case CadSnapKind.Intersection:
                    return CadSnapModes.Intersection;
                case CadSnapKind.Midpoint:
                    return CadSnapModes.Midpoint;
                case CadSnapKind.Center:
                    return CadSnapModes.Center;
                case CadSnapKind.Nearest:
                    return CadSnapModes.Nearest;
                case CadSnapKind.Node:
                    return CadSnapModes.Node;
                case CadSnapKind.Quadrant:
                    return CadSnapModes.Quadrant;
                case CadSnapKind.Perpendicular:
                    return CadSnapModes.Perpendicular;
                default:
                    return CadSnapModes.Tangent;
            }
        }

        /// <summary>
        /// The point an object snap of an object gives, near a pick: null where the object has no such point (a circle has no
        /// end, a line no centre), which AutoCAD answers "Improper type of object picked.".
        /// </summary>
        /// <param name="session">The session, whose drawing and last point (for per and tan) are used.</param>
        /// <param name="mode">END, INS, INT, MID, CEN, NEA, NOD, QUA, PER or TAN.</param>
        /// <param name="handle">The object picked.</param>
        /// <param name="pick">Where it was picked.</param>
        /// <returns>The point, or null.</returns>
        internal static GeoPoint2? Snap(CadCommandSession session, string mode, string handle, GeoPoint2 pick)
        {
            CadDocument document = session.Document;
            PickIndex whole = document.GetPickIndex(document.WorkingSpace);
            CadSnapKind kind = KindOf(mode);
            PickIndex index;
            double aperture;

            if (kind == CadSnapKind.Intersection)
            {
                // Two objects meet at an intersection: the ones under the pick.
                index = whole;
                aperture = Math.Max(session.PickAperture, 1E-9);
            }
            else
            {
                // The object alone, however far from the pick its points lie.
                var list = new DisplayList2();

                foreach (DisplayItem2 item in whole.List.Items)
                {
                    if (item.Handle == handle)
                    {
                        list.Add(item);
                    }
                }

                foreach (GeoPoint2 point in whole.List.InsertionPointsOf(handle))
                {
                    list.AddInsertionPoint(handle, point.X, point.Y);
                }

                if (list.Count == 0 && whole.List.InsertionPointsOf(handle).Count == 0)
                {
                    return null;
                }

                index = new PickIndex(list);
                DisplayBox bounds = list.Bounds;
                double reach = bounds.IsEmpty
                    ? 1.0
                    : Math.Max(bounds.Width, bounds.Height)
                        + Math.Max(Math.Abs(pick.X - bounds.MinX), Math.Abs(pick.Y - bounds.MinY))
                        + Math.Max(Math.Abs(pick.X - bounds.MaxX), Math.Abs(pick.Y - bounds.MaxY));
                aperture = reach + 1.0;
            }

            var snap = new ObjectSnap(session.Tolerance) { IsOn = true, Override = ModeOf(kind) };
            GeoPoint2? from =
                kind == CadSnapKind.Perpendicular || kind == CadSnapKind.Tangent ? session.LastPoint : (GeoPoint2?)null;
            CadSnapResult found = snap.Snap(index, pick, aperture, from);

            if (found == null || found.Kind != kind)
            {
                return null;
            }

            return found.Point;
        }

        /// <summary>The radius of a circle or an arc, for CAL's <c>rad</c>; null for any other object.</summary>
        internal static double? Radius(CadDocument document, string handle)
        {
            DxfEntity entity = document.FindEntity(handle);
            return entity is DxfCircle circle ? circle.Radius
                : entity is DxfArc arc ? arc.Radius
                : (double?)null;
        }
    }
}
