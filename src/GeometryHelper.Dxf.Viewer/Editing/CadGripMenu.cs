using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What an item of a multi-functional grip's hover menu does.</summary>
    public enum CadGripAction
    {
        /// <summary>The middle of a segment: stretches it, as a click on the grip does ("** STRETCH **").</summary>
        Stretch,

        /// <summary>A polyline's vertex: stretches the vertex, as a click on the grip does ("** STRETCH **").</summary>
        StretchVertex,

        /// <summary>A polyline's vertex or segment middle: asks for a new vertex, put after the vertex of the grip.</summary>
        AddVertex,

        /// <summary>A polyline's vertex: takes the vertex out at once.</summary>
        RemoveVertex,

        /// <summary>The middle of a straight segment: asks for the arc's middle and makes the segment an arc.</summary>
        ConvertToArc,

        /// <summary>The middle of an arc segment: asks for a pick ("Pick to convert to line segment:") and makes it straight.</summary>
        ConvertToLine,

        /// <summary>An end of a line or an arc: asks for the new end, which keeps the line's way or the arc's centre and radius.</summary>
        Lengthen,

        /// <summary>The middle of an arc: asks for a radius, the centre and the angles kept.</summary>
        Radius,

        /// <summary>A hatch's grip: asks for the scale of its pattern.</summary>
        HatchScale,

        /// <summary>A hatch's grip: asks for the angle of its pattern.</summary>
        HatchAngle,

        /// <summary>A hatch's grip: asks for the point its pattern is laid from.</summary>
        HatchOrigin,
    }

    /// <summary>One item of a multi-functional grip's hover menu.</summary>
    public sealed class CadGripMenuItem
    {
        internal CadGripMenuItem(CadGripAction action, string text, bool enabled)
        {
            Action = action;
            Text = text;
            Enabled = enabled;
        }

        /// <summary>Gets what choosing the item does.</summary>
        public CadGripAction Action { get; }

        /// <summary>Gets the words of the item, as AutoCAD 2021 writes them: "Stretch Vertex", "Convert to Arc".</summary>
        public string Text { get; }

        /// <summary>Gets whether the item can be chosen; false for it drawn grey, as Remove Vertex on a polyline of two vertices.</summary>
        public bool Enabled { get; }

        /// <inheritdoc/>
        public override string ToString() => Text + (Enabled ? string.Empty : " (grey)");
    }

    /// <summary>
    /// The hover menus of the multi-functional grips, as AutoCAD 2021 gives a polyline's: the grip on a vertex offers Stretch
    /// Vertex, Add Vertex and Remove Vertex; the grip in the middle of a straight segment Stretch, Add Vertex and Convert to
    /// Arc, in the middle of an arc segment Stretch, Add Vertex and Convert to Line. An end of a line or of an arc offers
    /// Stretch and Lengthen, the middle of an arc Stretch and Radius, the grip of a hatch Stretch, Hatch Scale, Hatch Angle and
    /// Origin Point. The other entities' grips have none.
    /// </summary>
    public static class CadGripMenus
    {
        /// <summary>Gets the items of the menu a grip shows when the cursor rests on it.</summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the entity.</param>
        /// <param name="gripIndex">The index of the grip, as <see cref="CadGrips.Of"/> gives it.</param>
        /// <returns>The items in the order they are listed; empty for a grip with no menu, or no such entity or grip.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static IReadOnlyList<CadGripMenuItem> Of(CadDocument document, string handle, int gripIndex)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            var items = new List<CadGripMenuItem>();
            DxfEntity entity = document.FindEntity(handle);

            if (entity is DxfLine || entity is DxfArc)
            {
                IReadOnlyList<CadGrip> own = CadGrips.Of(entity);

                if (gripIndex < 0 || gripIndex >= own.Count)
                {
                    return items;
                }

                // An end offers Stretch and Lengthen, the middle of an arc Stretch and Radius; a line's middle, which moves it, and an arc's centre have none.
                if (own[gripIndex].Kind == CadGripKind.End)
                {
                    items.Add(new CadGripMenuItem(CadGripAction.Stretch, "Stretch", true));
                    items.Add(new CadGripMenuItem(CadGripAction.Lengthen, "Lengthen", true));
                }
                else if (own[gripIndex].Kind == CadGripKind.Middle && entity is DxfArc)
                {
                    items.Add(new CadGripMenuItem(CadGripAction.Stretch, "Stretch", true));
                    items.Add(new CadGripMenuItem(CadGripAction.Radius, "Radius", true));
                }

                return items;
            }

            if (entity is DxfSpline spline)
            {
                if (gripIndex < 0 || gripIndex >= CadGrips.Of(spline).Count)
                {
                    return items;
                }

                // A point of a spline: stretch it, add one after it, take it out (not below the least a spline can keep).
                bool fit = spline.HasFitData;
                items.Add(
                    new CadGripMenuItem(CadGripAction.StretchVertex, fit ? "Stretch Fit Point" : "Stretch Vertex", true)
                );
                items.Add(new CadGripMenuItem(CadGripAction.AddVertex, fit ? "Add Fit Point" : "Add Vertex", true));
                items.Add(
                    new CadGripMenuItem(
                        CadGripAction.RemoveVertex,
                        fit ? "Remove Fit Point" : "Remove Vertex",
                        CadGrips.SplineCanLoseAPoint(spline)
                    )
                );
                return items;
            }

            if (entity is DxfHatch hatch)
            {
                IReadOnlyList<CadGrip> own = CadGrips.Of(hatch);

                if (gripIndex != 0 || own.Count == 0)
                {
                    return items;
                }

                // Stretch, then what the pattern lets change; a solid fill has Stretch alone, which is no menu.
                CadGrips.HatchMenuAllows(hatch, out bool scale, out bool angle, out bool origin);

                if (scale || angle || origin)
                {
                    items.Add(new CadGripMenuItem(CadGripAction.Stretch, "Stretch", true));
                }

                if (scale)
                {
                    items.Add(new CadGripMenuItem(CadGripAction.HatchScale, "Hatch Scale", true));
                }

                if (angle)
                {
                    items.Add(new CadGripMenuItem(CadGripAction.HatchAngle, "Hatch Angle", true));
                }

                if (origin)
                {
                    items.Add(new CadGripMenuItem(CadGripAction.HatchOrigin, "Origin Point", true));
                }

                return items;
            }

            // The grips of a cloud with few grips are those of its shape, which have no menu.
            if (
                !(entity is DxfPolyline || entity is DxfPolyline2d)
                || (entity is DxfPolyline cloud && CadCommands.RevCloudHasFewGrips(document, cloud))
            )
            {
                return items;
            }

            IReadOnlyList<CadGrip> grips = CadGrips.Of(entity);

            // The vertices of the polyline of either kind: how many, and the bulge of each segment.
            int vertices = entity is DxfPolyline polyline ? polyline.NumberOfVertices : CadGrips.VertexCount(entity);
            double Bulge(int segment) =>
                entity is DxfPolyline light ? light.GetBulgeAt(segment) : CadGrips.BulgeOf(entity, segment);

            if (gripIndex < 0 || gripIndex >= grips.Count)
            {
                return items;
            }

            switch (grips[gripIndex].Kind)
            {
                case CadGripKind.Vertex:
                    items.Add(new CadGripMenuItem(CadGripAction.StretchVertex, "Stretch Vertex", true));
                    items.Add(new CadGripMenuItem(CadGripAction.AddVertex, "Add Vertex", true));
                    items.Add(new CadGripMenuItem(CadGripAction.RemoveVertex, "Remove Vertex", vertices > 2));
                    break;
                case CadGripKind.SegmentMiddle:
                    bool arc = Math.Abs(Bulge(gripIndex - vertices)) >= 1E-12;
                    items.Add(new CadGripMenuItem(CadGripAction.Stretch, "Stretch", true));
                    items.Add(new CadGripMenuItem(CadGripAction.AddVertex, "Add Vertex", true));
                    items.Add(
                        arc
                            ? new CadGripMenuItem(CadGripAction.ConvertToLine, "Convert to Line", true)
                            : new CadGripMenuItem(CadGripAction.ConvertToArc, "Convert to Arc", true)
                    );
                    break;
            }

            return items;
        }
    }
}
