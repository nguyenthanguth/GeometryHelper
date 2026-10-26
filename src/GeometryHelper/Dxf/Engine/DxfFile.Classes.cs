using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        #region Classes

        /// <summary>
        /// Lists a class of objects in the CLASSES section, last, where the section lists none of its DXF name, as AutoCAD lists
        /// the class of an object not built in: its DXF name (1), its C++ name (2), its application, ObjectDBX Classes (3), its
        /// proxy flags (90), from R2004 on how many objects of it there are (91), written 0, which AutoCAD counts anew when it
        /// saves, and that it was no proxy (280) and is no entity (281). Nothing where the file has no CLASSES section.
        /// </summary>
        internal void AddClass(string dxfName, string cppName, int proxyFlags)
        {
            if (HasClass(dxfName))
            {
                return;
            }

            int end = -1;

            foreach (Section section in _sections)
            {
                if (section.Name == "CLASSES")
                {
                    end = section.Last;
                }
            }

            if (end < 0)
            {
                return;
            }

            var b = new Builder(this);
            b.Group(0, "CLASS");
            b.Group(1, dxfName);
            b.Group(2, cppName);
            b.Group(3, "ObjectDBX Classes");
            b.Group(90, proxyFlags.ToString(CultureInfo.InvariantCulture));

            if (Supports(1018))
            {
                b.Group(91, "0");
            }

            b.Group(280, "0");
            b.Group(281, "0");
            PutBefore(end, new Piece(new List<Rec> { b.Done("CLASS") }));
            Edited();
        }

        #endregion
    }
}
