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

        /// <summary>A polyline's vertex: stretches the vertex, as a click on the grip does ("** STRETCH VERTEX **").</summary>
        StretchVertex,

        /// <summary>A polyline's vertex or segment middle: asks for a new vertex, put after the vertex of the grip.</summary>
        AddVertex,

        /// <summary>A polyline's vertex: takes the vertex out at once.</summary>
        RemoveVertex,

        /// <summary>The middle of a straight segment: asks for the arc's middle and makes the segment an arc.</summary>
        ConvertToArc,

        /// <summary>The middle of an arc segment: makes it straight at once.</summary>
        ConvertToLine,
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
    /// Arc, in the middle of an arc segment Stretch, Add Vertex and Convert to Line. The other entities' grips have none.
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

            if (!(document.FindEntity(handle) is DxfPolyline polyline))
            {
                return items;
            }

            IReadOnlyList<CadGrip> grips = CadGrips.Of(polyline);

            if (gripIndex < 0 || gripIndex >= grips.Count)
            {
                return items;
            }

            switch (grips[gripIndex].Kind)
            {
                case CadGripKind.Vertex:
                    items.Add(new CadGripMenuItem(CadGripAction.StretchVertex, "Stretch Vertex", true));
                    items.Add(new CadGripMenuItem(CadGripAction.AddVertex, "Add Vertex", true));
                    items.Add(
                        new CadGripMenuItem(CadGripAction.RemoveVertex, "Remove Vertex", polyline.NumberOfVertices > 2)
                    );
                    break;
                case CadGripKind.SegmentMiddle:
                    bool arc = Math.Abs(polyline.GetBulgeAt(gripIndex - polyline.NumberOfVertices)) >= 1E-12;
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
