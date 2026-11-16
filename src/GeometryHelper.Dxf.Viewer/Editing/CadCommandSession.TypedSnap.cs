using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>What a point typed after an override came to.</summary>
        private enum TypedSnapResult
        {
            /// <summary>The point is the answer, snapped or as typed.</summary>
            Point,

            /// <summary>The snap found nothing: the words are said and the prompt is asked again.</summary>
            Missing,

            /// <summary>INT or APP found one object and asks " and" for the second.</summary>
            Asking,
        }

        /// <summary>
        /// Gets or sets the object snap taken for the next point alone (the window sets it with its own override): a point typed
        /// at a prompt for a point, at "Select objects:", at a prompt for one object, at a corner of a window and at a point of
        /// a fence or a polygon is snapped to the snap point of the object under it, within <see cref="SnapAperture"/>, as
        /// AutoCAD 2021 snaps it, the point of an object found however far from the typed point it lies: with END, an aperture
        /// of 1.3 and a line from (0, 0) to (100, 0), 30,1 gives (0, 0) and 70,1 gives (100, 0), but 50,3 gives "No Endpoint
        /// found for specified point." and then the words of the prompt it was typed at ("Invalid point.", "*Invalid
        /// selection*" at "Select objects:", "Invalid window specification." at a corner, "Invalid response." at a fence), and
        /// the prompt is asked again. The override is for one point: it is cleared once a point is typed. ENDpoint, MIDpoint,
        /// CENter, QUAdrant, NODe, INSert, NEArest, PERpendicular, TANgent and GCEnter are taken; INTersection and APPint take
        /// the crossing of two objects under the point, or where one lies there ask " and" for the second, answered by a point
        /// on it, and with the objects not crossing say "No Intersection found for specified point."; EXTension and PARallel,
        /// which need the cursor to rest on an object, find nothing at a point typed. Where a snap has no name in AutoCAD's
        /// words (NEArest, TANgent, GCEnter, APPint, EXTension, PARallel) it says "No found for specified point." NONe leaves
        /// the point as typed. Null for none.
        /// </summary>
        public CadSnapModes? SnapOverride { get; set; }

        /// <summary>
        /// Gets or sets the aperture of the object snap in drawing units, 10 pixels over the zoom as the canvas sets it with the
        /// cursor: how near an object must lie to a point typed after an override (<see cref="SnapOverride"/>) to be snapped.
        /// </summary>
        public double SnapAperture { get; set; } = 6.0;

        /// <summary>
        /// Snaps a point typed after an object snap override. Where none is set, or it is NONe, the point stays.
        /// </summary>
        /// <param name="point">The point typed; the point snapped.</param>
        /// <param name="echo">The prompt and the point typed, written first where " and" is asked; null where it was written.</param>
        /// <param name="missing">The words said where the snap finds nothing ("No Endpoint found for specified point."); null otherwise.</param>
        /// <returns>Whether the point goes on, nothing was found, or the second object is asked for.</returns>
        private TypedSnapResult SnapTyped(ref GeoPoint2 point, string echo, out string missing)
        {
            missing = null;
            CadSnapModes? modes = SnapOverride;

            if (!modes.HasValue)
            {
                return TypedSnapResult.Point;
            }

            SnapOverride = null;

            if (!TryTypedKind(modes.Value, out CadSnapKind kind, out string name))
            {
                return TypedSnapResult.Point;
            }

            missing = NotFound(name);

            // Extension and Parallel run from a point the cursor rested on: a point typed has none.
            if (kind == CadSnapKind.Extension || kind == CadSnapKind.Parallel)
            {
                return TypedSnapResult.Missing;
            }

            GeoPoint2? from = null;

            if (kind == CadSnapKind.Perpendicular || kind == CadSnapKind.Tangent)
            {
                from = Prompt.BasePoint;

                if (!from.HasValue)
                {
                    missing = null;
                    return TypedSnapResult.Point;
                }
            }

            IReadOnlyList<string> near = Document
                .GetPickIndex(Document.WorkingSpace)
                .Pick(point.X, point.Y, Math.Max(SnapAperture, 1E-9));
            GeoPoint2? best = null;
            double nearest = double.MaxValue;

            if (kind == CadSnapKind.Intersection || kind == CadSnapKind.ApparentIntersection)
            {
                // One object under the point asks for the second; two or more cross, the crossing nearest the point.
                if (near.Count == 1)
                {
                    AskSecondObject(kind, near[0], echo);
                    missing = null;
                    return TypedSnapResult.Asking;
                }

                string[] objects = near.Take(8).ToArray();

                for (int i = 0; i < objects.Length; i++)
                {
                    for (int j = i + 1; j < objects.Length; j++)
                    {
                        Consider(
                            CadCalcSnap.Between(this, kind, objects[i], objects[j], point),
                            point,
                            ref best,
                            ref nearest
                        );
                    }
                }
            }
            else
            {
                foreach (string handle in near)
                {
                    Consider(CadCalcSnap.Snap(this, kind, handle, point, from), point, ref best, ref nearest);
                }
            }

            if (best.HasValue)
            {
                point = best.Value;
                missing = null;
                return TypedSnapResult.Point;
            }

            return TypedSnapResult.Missing;
        }

        /// <summary>Keeps a snap point where it lies nearer the point typed than the one kept; the first of two as near stays.</summary>
        private static void Consider(GeoPoint2? found, GeoPoint2 point, ref GeoPoint2? best, ref double nearest)
        {
            if (found.HasValue && (found.Value - point).Length < nearest)
            {
                best = found;
                nearest = (found.Value - point).Length;
            }
        }

        /// <summary>The words AutoCAD says where a snap finds nothing under a point typed: "No Endpoint found for specified point."; "No found for specified point." for a snap it gives no name.</summary>
        private static string NotFound(string name) =>
            "No " + (name.Length > 0 ? name + " " : string.Empty) + "found for specified point.";

        /// <summary>
        /// INT or APP typed over one object: " and" asks for a second, whose point (typed or clicked) gives the crossing of the
        /// two; the prompt waiting is answered by that.
        /// </summary>
        private void AskSecondObject(CadSnapKind kind, string first, string echo)
        {
            Echo(echo);
            // The last point is kept in First: a point given at " and" that finds no crossing leaves it as it was, as AutoCAD does.
            _modifiers.Add(
                new PointModifier(ModifierKind.Intersect, null, Prompt)
                {
                    Snap = kind,
                    Object = first,
                    First = LastPoint,
                }
            );
            Prompt = CadPrompt.ForPoint(" and").WithoutColon();
            Changed();
        }

        /// <summary>
        /// The point typed or clicked at " and": the crossing of the object under it with the first, the one nearest the point
        /// where several lie there; none, and "No Intersection found for specified point." (or the words with no name for
        /// APP) is said and the prompt INT was typed at is refused as it refuses a point that is none.
        /// </summary>
        private bool TakeSecondObject(PointModifier asking, GeoPoint2 point)
        {
            PointModifier outermost = _modifiers[0];
            _modifiers.RemoveAt(_modifiers.Count - 1);
            Prompt = asking.Outer;
            GeoPoint2? best = null;
            double nearest = double.MaxValue;

            foreach (
                string handle in Document
                    .GetPickIndex(Document.WorkingSpace)
                    .Pick(point.X, point.Y, Math.Max(SnapAperture, 1E-9))
            )
            {
                if (!string.Equals(handle, asking.Object, StringComparison.Ordinal))
                {
                    Consider(
                        CadCalcSnap.Between(this, asking.Snap, asking.Object, handle, point),
                        point,
                        ref best,
                        ref nearest
                    );
                }
            }

            if (!best.HasValue)
            {
                LastPoint = asking.First.Value;
                Write(NotFound(asking.Snap == CadSnapKind.Intersection ? "Intersection" : string.Empty));
                return RefuseAs(outermost.Outer, null);
            }

            if (_modifiers.Count > 0)
            {
                LastPoint = best.Value;
                return TakeModifierInput(new CadInput(CadInputKind.Point, best.Value, 0.0, null, null));
            }

            return GivePoint(best.Value);
        }

        /// <summary>The kind of snap a typed point takes, and the name AutoCAD says it by in "No Endpoint found for specified point.": none for NEArest, TANgent, GCEnter, APPint, EXTension and PARallel.</summary>
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
                case CadSnapModes.Intersection:
                    kind = CadSnapKind.Intersection;
                    name = "Intersection";
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
                case CadSnapModes.GeometricCenter:
                    kind = CadSnapKind.GeometricCenter;
                    break;
                case CadSnapModes.ApparentIntersection:
                    kind = CadSnapKind.ApparentIntersection;
                    break;
                case CadSnapModes.Extension:
                    kind = CadSnapKind.Extension;
                    break;
                case CadSnapModes.Parallel:
                    kind = CadSnapKind.Parallel;
                    break;
                default:
                    return false;
            }

            return true;
        }
    }
}
