using System.Globalization;
using System.Text;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// DXF files written by hand in the tests, in the style AutoCAD writes them: each group code padded to three places on
    /// its own line, its value on the next.
    /// </summary>
    internal static class DxfLines
    {
        /// <summary>The groups of a record: a code, a value, a code, a value, and so on.</summary>
        internal static string G(params object[] pairs)
        {
            var text = new StringBuilder();

            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text.Append(((int)pairs[i]).ToString(CultureInfo.InvariantCulture).PadLeft(3)).Append('\n');
                text.Append(pairs[i + 1] is double d ? d.ToString("R", CultureInfo.InvariantCulture) : pairs[i + 1].ToString()).Append('\n');
            }

            return text.ToString();
        }

        /// <summary>A file with a header of a version and a unit, the sections given between, and the entities.</summary>
        internal static string File(string version, int units, string tables, string entities)
            => G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, version, 9, "$INSUNITS", 70, units, 0, "ENDSEC")
            + tables
            + G(0, "SECTION", 2, "ENTITIES") + entities + G(0, "ENDSEC", 0, "EOF");

        /// <summary>A file of R2000 in millimetres with these entities and no tables.</summary>
        internal static string R2000(string entities) => File("AC1015", 4, string.Empty, entities);

        /// <summary>A file of R12, which has no units, with these entities and no tables.</summary>
        internal static string R12(string entities)
            => G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1009", 0, "ENDSEC", 0, "SECTION", 2, "ENTITIES") + entities + G(0, "ENDSEC", 0, "EOF");

        /// <summary>A layer table of these layers, each with its colour.</summary>
        internal static string Layers(params string[] names)
        {
            var text = new StringBuilder(G(0, "SECTION", 2, "TABLES", 0, "TABLE", 2, "LAYER", 70, names.Length));

            foreach (string name in names)
            {
                text.Append(G(0, "LAYER", 100, "AcDbSymbolTableRecord", 100, "AcDbLayerTableRecord", 2, name, 70, 0, 62, 7, 6, "Continuous"));
            }

            return text.Append(G(0, "ENDTAB", 0, "ENDSEC")).ToString();
        }
    }
}
