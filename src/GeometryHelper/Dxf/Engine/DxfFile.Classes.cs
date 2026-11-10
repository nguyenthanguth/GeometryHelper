using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfFile
    {
        #region Classes

        /// <summary>Whether the file has a CLASSES section, which <see cref="AddClass"/> lists a class in.</summary>
        internal bool ListsClasses()
        {
            foreach (Section section in _sections)
            {
                if (section.Name == "CLASSES")
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Lists the class DICTIONARYVAR as AutoCAD 2021 lists it (its proxy flags 0, of ObjectDBX Classes, read off its
        /// <c>RXClass</c>), counted once from R2004 on, where the file lists none, so that a variable kept out of the header can
        /// be added to a drawing the library made new. False where the file has no CLASSES section to list it in.
        /// </summary>
        internal bool ListDictionaryVariables()
        {
            if (HasClass("DICTIONARYVAR"))
            {
                return true;
            }

            if (!ListsClasses())
            {
                return false;
            }

            AddClass("DICTIONARYVAR", "AcDbDictionaryVar", 0, count: 1);
            return true;
        }

        /// <summary>
        /// Lists a class of objects in the CLASSES section, last, where the section lists none of its DXF name, as AutoCAD lists
        /// the class of an object not built in: its DXF name (1), its C++ name (2), its application (3), ObjectDBX Classes where
        /// none is given, SCENEOE for a SUN, its proxy flags (90), from R2004 on how many objects of it there are (91), the
        /// count given (0 where none is, which AutoCAD counts anew when it saves), and that it was no proxy (280) and whether it
        /// is an entity (281), as an IMAGE is. Nothing where the file has no CLASSES section.
        /// </summary>
        internal void AddClass(string dxfName, string cppName, int proxyFlags, string application = "ObjectDBX Classes", int count = 0, bool entity = false)
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
            b.Group(3, application);
            b.Group(90, proxyFlags.ToString(CultureInfo.InvariantCulture));

            if (Supports(1018))
            {
                b.Group(91, count.ToString(CultureInfo.InvariantCulture));
            }

            b.Group(280, "0");
            b.Group(281, entity ? "1" : "0");
            PutBefore(end, new Piece(new List<Rec> { b.Done("CLASS") }));
            Edited();
        }

        /// <summary>
        /// Counts a class of the CLASSES section once (its 91 made 1) where the section lists it counted 0, from R2004 on, as
        /// ezdxf 1.4.5 counts every class of its drawings of R2010 and R2018: AutoCAD 2021 passes over every object of a class
        /// it counts 0. Nothing where the class is counted otherwise, is not listed, or the file is older than R2004.
        /// </summary>
        internal void CountClass(string dxfName)
        {
            if (!Supports(1018))
            {
                return;
            }

            foreach (Section section in _sections)
            {
                if (section.Name != "CLASSES")
                {
                    continue;
                }

                VisitInside(section, (Rec record, out Rec? now) =>
                {
                    now = null;
                    var read = Record(record);

                    if (record.Type != "CLASS" || !string.Equals((read.Text(1) ?? string.Empty).Trim(), dxfName, StringComparison.OrdinalIgnoreCase)
                        || (read.Text(91) ?? string.Empty).Trim() != "0" || !WithGroup(record, 91, "1", false, out Rec counted))
                    {
                        now = record;
                        return false;
                    }

                    now = counted;
                    return true;
                });
            }

            Edited();
        }

        #endregion
    }
}
