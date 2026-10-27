using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfDatabase
    {
        /// <summary>
        /// Gets the contexts of the drawing, as AutoCAD's <c>ObjectContextManager</c>: its collection <c>ACDB_ANNOTATIONSCALES</c>
        /// is the drawing's list of scales, the SCALE objects of ACAD_SCALELIST. <c>db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES")</c>
        /// gives it, empty for a drawing that has no list, as one of R12.
        /// </summary>
        public DxfObjectContextManager ObjectContextManager => new DxfObjectContextManager(this);

        /// <summary>
        /// Refuses a variable kept out of the header that the drawing could not write, as setting it would refuse it, before
        /// anything is written: one the header does not hold, in a file with no OBJECTS section, no named object dictionary or
        /// no class DICTIONARYVAR.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the variable could not be written.</exception>
        internal void CheckVariableWritable(string name)
        {
            Holdable(name);
            IReadOnlyList<DxfTypedValue> header = File.HeaderOf("$" + name);

            if (header != null && header.Count == 1)
            {
                return;
            }

            if (NamedObjectsDictionary == null)
            {
                throw new InvalidOperationException("A DXF " + (Version ?? "with no version") + " has no named object dictionary to hold a variable kept out of the header: R13 (AC1012) brought it.");
            }

            if (!File.HasClass("DICTIONARYVAR"))
            {
                throw new InvalidOperationException("The file's CLASSES section lists no DICTIONARYVAR, which a variable kept out of the header is.");
            }
        }
    }
}
