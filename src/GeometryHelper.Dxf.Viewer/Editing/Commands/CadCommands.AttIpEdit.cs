using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// ATTIPEDIT (ATI), as AutoCAD 2021's: "Select an attribute:", the attribute clicked (the one of the INSERT clicked that is
    /// drawn nearest the click, as -ATTEDIT takes it; the INSERT's first where it was given by its handle), its value edited in
    /// place in the box over it on the canvas (<see cref="CadPrompt.InPlace"/>, as TEXTEDIT edits a TEXT), written as one undo
    /// step. An attribute of several lines is not edited here.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> AttributeInPlace(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select an attribute");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            string handle = c.Input.Handles[0];
            DxfAttributeReference attribute = null;

            if (c.Document.FindEntity(handle) is DxfBlockReference insert && insert.AttributeCollection.Count > 0)
            {
                if (c.Input.IsPickedAt)
                {
                    HashSet<string> clicked = ClickedAttributes(c, insert, new[] { c.Input.Point });
                    attribute = insert.AttributeCollection.FirstOrDefault(a => clicked.Contains(a.Handle));
                }
                else
                {
                    attribute = insert.AttributeCollection.FirstOrDefault(a => !a.IsErased && !a.Invisible);
                }

                if (attribute != null && CadLayerEditor.IsLocked(c.Database, insert.Layer))
                {
                    c.Write("The block is on a locked layer.");
                    yield break;
                }
            }

            if (attribute == null)
            {
                c.Write("Selected object is not an attribute.");
                yield break;
            }

            GeoPoint3 at =
                attribute.Justify == DxfAttachmentPoint.BaseLeft ? attribute.Position : attribute.AlignmentPoint;
            string insertHandle = handle;
            string attributeHandle = attribute.Handle;
            string was = attribute.TextString ?? string.Empty;
            yield return CadPrompt.ForInPlaceEdit(
                "Edit attribute text",
                new CadInPlaceEdit(
                    CadInPlaceKind.Text,
                    attributeHandle,
                    was,
                    new GeoPoint2(at.X, at.Y),
                    attribute.Height,
                    attribute.Rotation,
                    0.0,
                    attribute.TextStyleName,
                    1
                )
            );

            if (
                c.Input.Kind != CadInputKind.Text
                || c.Input.Text.Length == 0
                || string.Equals(c.Input.Text, was, StringComparison.Ordinal)
            )
            {
                yield break;
            }

            CadAttributeRow row = Rows(c.Document, insertHandle)
                .Rows.First(r => string.Equals(r.Handle, attributeHandle, StringComparison.OrdinalIgnoreCase));
            CadAttributeRow old = row.Clone();
            row.Value = c.Input.Text;
            WriteAttributes(
                c,
                new CadAttributeEditRequest(c.Document, insertHandle, null, new[] { row }),
                new List<CadAttributeRow> { old }
            );
        }
    }
}
