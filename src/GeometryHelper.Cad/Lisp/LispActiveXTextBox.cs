using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>textbox</c>, which AutoCAD has before <c>vl-load-com</c> and the ActiveX bounding box of a text shares its measure
    /// with: the box of a text's words in the document's fonts, as the DXF layer measures them for <c>GeometricExtents</c>
    /// (check F1a of the DXF layer: the ink of the line for an SHX font, the pen's run for a TrueType one).
    /// </summary>
    internal sealed partial class LispActiveX
    {
        private void RegisterTextBox() => Lisp.Define("TEXTBOX", 1, 1, a => Lisp.OnOwner(() => TextBox(a[0])));

        /// <summary>
        /// <c>(textbox elist)</c> of a TEXT or MTEXT (ours; the measured row is AutoCAD 2021, GUI g2: <c>abc</c>, 2.5 high, in txt
        /// gives <c>((0.0 0.0 0.0) (6.66667 2.5 0.0))</c>): the lower left and upper right corners of its words as though it
        /// stood unturned at its insertion point, relative to that point. Nil for a list that names no text of the drawing (the
        /// groups of a text that is not drawn are not read yet), and for words whose font is not found, which are not measured
        /// (the box would be the insertion point alone). A list that is no list is <c>bad argument type: consp</c>.
        /// </summary>
        private object TextBox(object list)
        {
            if (!(list is LispCons) || !LispEntityData.TryRead(list, out List<KeyValuePair<int, object>> groups))
            {
                throw LispInterpreter.TypeError("consp", list);
            }

            if (!(groups.Where(g => g.Key == -1).Select(g => g.Value).FirstOrDefault() is LispEntityName name))
            {
                return null;
            }

            DxfEntity entity = Document.FindEntity(State.Resolve(name.Handle));
            GeoPoint3 at;
            DxfEntity copy;

            // The words are measured on a copy turned back to nought, as the entity is not touched.
            try
            {
                copy = entity?.Clone();

                if (copy is DxfDBText text)
                {
                    text.Rotation = 0.0;
                    at = text.Position;
                }
                else if (copy is DxfMText mtext)
                {
                    mtext.Rotation = 0.0;
                    at = mtext.Location;
                }
                else
                {
                    return null;
                }
            }
            catch (System.InvalidOperationException)
            {
                return null;
            }

            GeoAabb3 box;

            try
            {
                using (Measuring())
                {
                    box = copy.GeometricExtents;
                }
            }
            catch (System.InvalidOperationException)
            {
                return null;
            }

            if (box.IsEmpty || (box.Max - box.Min).Length <= Tolerance.Default.EqualPoint)
            {
                return null;
            }

            GeoPoint3 low = box.Min;
            GeoPoint3 high = box.Max;
            return LispInterpreter.ListOf(
                new object[]
                {
                    LispInterpreter.ListOf(new object[] { low.X - at.X + 0.0, low.Y - at.Y + 0.0, low.Z - at.Z + 0.0 }),
                    LispInterpreter.ListOf(
                        new object[] { high.X - at.X + 0.0, high.Y - at.Y + 0.0, high.Z - at.Z + 0.0 }
                    ),
                }
            );
        }
    }
}
