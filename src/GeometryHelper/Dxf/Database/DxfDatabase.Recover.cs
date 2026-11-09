using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Repairing a drawing, as AutoCAD's RECOVER opens a damaged file and its AUDIT checks an open drawing.
    /// </summary>
    public sealed partial class DxfDatabase
    {
        /// <summary>
        /// Opens a damaged DXF file from a path, as AutoCAD's RECOVER opens one: what can be read of it, repaired, with a report
        /// of what was found and done. A sound file is opened as <see cref="Open(string)"/> opens it, every byte kept, with an
        /// empty report.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="report">What was found wrong and what was done, in the order it was found.</param>
        /// <returns>The drawing; never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="FormatException">Thrown for a binary DXF, which is not damaged but not read: save it as an ASCII DXF.</exception>
        /// <remarks>
        /// <para>
        /// The file is read as pairs of lines whatever is wrong with them, then put right where it can be, in the order of
        /// <see cref="DxfRepairKind"/>: a line that is not a group code left out; a file cut short closed with its ENDSEC and
        /// EOF; the sections closed and put in order, ENTITIES added where there is none; the text of a file of R2007 or later
        /// that is not UTF-8 read in its code page; a number that is no number given the number it begins with, or nought; an
        /// entity with a number that is not finite left out; the counts of a LWPOLYLINE and a HATCH set to their parts; a handle
        /// two records have, or one missing, given a new one; an owner that names nothing pointed at the block, table or
        /// polyline the record stands in; an ENDBLK, a SEQEND and <c>$HANDSEED</c> written where missing. The file so written
        /// is then opened, and audited as <see cref="Audit"/> audits it, fixing: layers, linetypes, text styles and dimension
        /// styles the entities name and the tables lack are added; a broken entry of a dictionary is taken out. An
        /// INSERT of a block not defined, an entity of a type unknown, a missing TABLES, BLOCKS or OBJECTS section are reported
        /// and left as they are.
        /// </para>
        /// <para>
        /// A drawing of R2018 of 12 INSERTs, a hatch, two polylines, a text, a dimension and three lines, cut off after the code
        /// of the first point of its last line, opens with every entity but that line whole, the line read with no point, and a
        /// report of 3 errors found, 2 fixed: the code with no value left out, the file closed with its ENDSEC and EOF, and its
        /// OBJECTS section, lost with the cut, reported. It saves as a file <see cref="Open(Stream)"/> opens. Where what was
        /// repaired still cannot be read, its entities alone are kept, and failing that the drawing is empty; both are said in
        /// the report (<see cref="DxfRepairKind.Unreadable"/>).
        /// </para>
        /// </remarks>
        public static DxfDatabase Recover(string path, out DxfRepairReport report)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            byte[] bytes = System.IO.File.ReadAllBytes(path);
            DxfDatabase database = Recover(bytes, bytes.Length, "'" + Path.GetFileName(path) + "'", out report);
            database._filename = database._originalFileName = Path.GetFullPath(path);
            return database;
        }

        /// <summary>
        /// Opens a damaged DXF file from a stream, from where the stream stands to its end, as
        /// <see cref="Recover(string, out DxfRepairReport)"/> opens one from a path.
        /// </summary>
        /// <param name="stream">The stream; it is read to its end and left open.</param>
        /// <param name="report">What was found wrong and what was done, in the order it was found.</param>
        /// <returns>The drawing; never null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        /// <exception cref="FormatException">Thrown for a binary DXF.</exception>
        public static DxfDatabase Recover(Stream stream, out DxfRepairReport report)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var copy = new MemoryStream();
            stream.CopyTo(copy);
            return Recover(copy.GetBuffer(), (int)copy.Length, "a stream", out report);
        }

        /// <summary>
        /// Checks the drawing for errors, as AutoCAD's AUDIT does, and fixes those it can where asked; the report says what was
        /// found and what was fixed. A sound drawing gives an empty report.
        /// </summary>
        /// <param name="fix">Whether to fix what can be fixed in the drawing as it stands, as AUDIT's answer Yes.</param>
        /// <returns>What was found, each fixed or not.</returns>
        /// <remarks>
        /// <para>
        /// Fixed where asked: a layer, linetype, text style or dimension style an entity names that its table lacks is added
        /// with the defaults (a linetype drawn solid); a number of an entity's own record that is no number is given the number
        /// it begins with, or nought, and the count of a LWPOLYLINE's vertices or a HATCH's boundary paths set to their number;
        /// an entity with a number that is not finite is erased; a broken entry of a dictionary (a name with no pointer, a
        /// pointer with no name or one that is not a handle) is taken out. A drawing whose line 0 to 100 stands on the layer
        /// WALLS, which its layer table does not list, gives one error, and fixed, the layer WALLS of colour 7 and the linetype
        /// Continuous.
        /// </para>
        /// <para>
        /// Found and not fixed here: an INSERT of a block not defined and an entity of a type neither AutoCAD's own nor named in
        /// the CLASSES section, both kept; and what the file itself would need written again, a handle two records have, an
        /// owner that names nothing, a <c>$HANDSEED</c> too low, a number of a vertex or an attribute that is no number, which
        /// <see cref="Recover(string, out DxfRepairReport)"/> fixes as it reads the saved drawing again.
        /// </para>
        /// </remarks>
        public DxfRepairReport Audit(bool fix)
        {
            var repairs = new List<DxfRepair>();

            using (var saved = new MemoryStream())
            {
                Save(saved);
                DxfRecovery.Run(saved.GetBuffer(), (int)saved.Length, false, false, repairs);
            }

            AuditObjects(fix, repairs);
            var report = new DxfRepairReport(repairs);
            Said("audited" + (fix ? " and fixed" : string.Empty), report);
            return report;
        }

        private static DxfDatabase Recover(byte[] bytes, int length, string source, out DxfRepairReport report)
        {
            const string Binary = "AutoCAD Binary DXF";

            if (length >= Binary.Length && System.Text.Encoding.ASCII.GetString(bytes, 0, Binary.Length) == Binary)
            {
                throw new FormatException("This is a binary DXF, which is not read: save the drawing as an ASCII DXF and read that.");
            }

            var repairs = new List<DxfRepair>();
            DxfRecovery recovery = DxfRecovery.Run(bytes, length, true, true, repairs);
            DxfDatabase database = Repaired(recovery, source, repairs);

            try
            {
                database.AuditObjects(true, repairs);
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("DxfDatabase: the audit of " + source + " stopped.", e);
                repairs.Add(new DxfRepair(DxfRepairKind.Unreadable, "The audit of the recovered drawing stopped: " + e.Message, 0, null, false));
            }

            report = new DxfRepairReport(repairs);
            Said("recovered " + source, report);
            return database;
        }

        /// <summary>
        /// The drawing of the repaired bytes; where they still cannot be read, the code page of the header set to ANSI_1252,
        /// then the entities alone, then an empty drawing, each said in the report.
        /// </summary>
        private static DxfDatabase Repaired(DxfRecovery recovery, string source, List<DxfRepair> repairs)
        {
            Exception first;

            try
            {
                return new DxfDatabase(DxfFile.Recovered(recovery.Bytes, recovery.Length, source));
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                first = e;
            }

            if (recovery.WithoutCodePage())
            {
                try
                {
                    var database = new DxfDatabase(DxfFile.Recovered(recovery.Bytes, recovery.Length, source));
                    repairs.Add(new DxfRepair(DxfRepairKind.Encoding, "The file could not be read in the code page its header names (" + first.Message + "); it was read as ANSI_1252.", 0, null, true));
                    return database;
                }
                catch (Exception e) when (!(e is OutOfMemoryException))
                {
                }
            }

            try
            {
                byte[] entities = recovery.EntitiesOnly(out int length);
                var database = new DxfDatabase(DxfFile.Recovered(entities, length, source));
                repairs.Add(new DxfRepair(DxfRepairKind.Unreadable, "The repaired file could still not be read (" + first.Message + "); only its entities were kept.", 0, null, true));
                return database;
            }
            catch (Exception e) when (!(e is OutOfMemoryException))
            {
                repairs.Add(new DxfRepair(DxfRepairKind.Unreadable, "Nothing of the file could be read (" + e.Message + "); the drawing is empty.", 0, null, false));
                return new DxfDatabase("AC1015");
            }
        }

        /// <summary>
        /// Audits the objects of the drawing: the numbers of each entity, the names its tables should list, the block of an
        /// INSERT and the type; then the entries of the dictionaries, against the handles of the file. What is fixed is fixed
        /// after every entity was read.
        /// </summary>
        private void AuditObjects(bool fix, List<DxfRepair> repairs)
        {
            HashSet<string> classes = File.ClassNames();
            var tables = new Dictionary<DxfRepairKind, HashSet<string>>
            {
                [DxfRepairKind.MissingLayer] = Names(LayerTable),
                [DxfRepairKind.MissingLinetype] = Names(LinetypeTable),
                [DxfRepairKind.MissingTextStyle] = Names(TextStyleTable),
                [DxfRepairKind.MissingDimStyle] = Names(DimStyleTable),
            };
            HashSet<string> blocks = Names(BlockTable);
            var missing = new List<MissingName>();
            var later = new List<Action>();

            foreach (DxfBlockTableRecord block in BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    DxfItem item = entity.Item;
                    string named = item.Type + (item.Handle != null ? " " + item.Handle : string.Empty) + (block.IsLayout ? string.Empty : " in the block " + block.Name);
                    string name = "The " + named;
                    DxfFile.Audited audited = File.Audit(item);

                    foreach (Tuple<int, int, int, string, DxfRepairKind, string> wrong in audited.Wrong)
                    {
                        string where = (wrong.Item1 > 0 ? " has in its " + Ordinal(wrong.Item1) + " vertex or attribute" : " has") + " in its group " + wrong.Item3.ToString(CultureInfo.InvariantCulture);

                        if (wrong.Item5 == DxfRepairKind.NonFiniteNumber)
                        {
                            repairs.Add(new DxfRepair(wrong.Item5, name + where + " a number that is not finite, '" + wrong.Item4.Trim() + "'" + (fix ? "; it was erased." : "."), 0, item.Handle, fix));
                            later.Add(entity.Erase);
                            break;
                        }

                        bool own = wrong.Item1 == 0 && fix;
                        string said = wrong.Item5 == DxfRepairKind.BadNumber ? " no number, '" + wrong.Item4.Trim() + "'" : " the count " + wrong.Item4.Trim() + " of " + wrong.Item6 + (wrong.Item5 == DxfRepairKind.WrongLoopCount ? " boundary paths" : " vertices");
                        repairs.Add(new DxfRepair(wrong.Item5, name + where + said + (own ? "; it was set to " + wrong.Item6 + "." : "."), 0, item.Handle, own));

                        if (own)
                        {
                            int index = wrong.Item2;
                            string value = wrong.Item6;
                            later.Add(() => File.SetValueAt(item, index, value));
                        }
                    }

                    Need(missing, tables, DxfRepairKind.MissingLayer, audited.Layer, named);
                    Need(missing, tables, DxfRepairKind.MissingLinetype, audited.Linetype, named);
                    Need(missing, tables, DxfRepairKind.MissingTextStyle, audited.TextStyle, named);
                    Need(missing, tables, DxfRepairKind.MissingDimStyle, audited.DimStyle, named);

                    if (audited.Block != null && !blocks.Contains(audited.Block) && !BlockTable.Has(audited.Block))
                    {
                        repairs.Add(new DxfRepair(DxfRepairKind.MissingBlock, name + " places the block " + audited.Block + ", which the drawing does not define; it was kept, drawing nothing.", 0, item.Handle, false));
                    }

                    if (!DxfRecovery.IsEntity(item.Type) && !classes.Contains(item.Type))
                    {
                        repairs.Add(new DxfRepair(DxfRepairKind.UnknownEntity, name + " is of a type neither AutoCAD's own nor named in the CLASSES section; it was kept as it is.", 0, item.Handle, false));
                    }
                }
            }

            foreach (MissingName need in missing)
            {
                string fixedBy = fix ? Add(need) : null;
                repairs.Add(new DxfRepair(need.Kind, "The " + Table(need.Kind) + " " + need.Name + ", named by " + need.Count.ToString(CultureInfo.InvariantCulture) + (need.Count == 1 ? " entity" : " entities") + " (the first: the " + need.First + "), is not in its table" + (fixedBy == string.Empty ? "; it was added." : fixedBy != null ? "; it could not be added: " + fixedBy : "."), 0, null, fixedBy == string.Empty));
            }

            later.ForEach(a => a());

            foreach (Tuple<DxfItem, int, int, string, string> entry in File.BrokenEntries())
            {
                string said = entry.Item4 == null ? "A pointer " + entry.Item5 + " with no name before it" : entry.Item5 == null ? "The entry " + entry.Item4 + " with no pointer after it" : "The entry " + entry.Item4 + " that points at '" + entry.Item5 + "', which is not a handle,";
                repairs.Add(new DxfRepair(DxfRepairKind.BrokenDictionaryEntry, said + " stands in the dictionary " + entry.Item1.Handle + (fix ? "; it was taken out." : "."), 0, entry.Item1.Handle, fix));

                for (int i = entry.Item3 - 1; i >= 0 && fix; i--)
                {
                    File.RemoveGroupAt(entry.Item1, entry.Item2 + i);
                }
            }
        }

        /// <summary>A name an entity gives that its table lacks: the kind, the name as first written, how many name it, and the first.</summary>
        private sealed class MissingName
        {
            internal DxfRepairKind Kind { get; set; }

            internal string Name { get; set; }

            internal int Count { get; set; }

            internal string First { get; set; }
        }

        private static HashSet<string> Names(DxfSymbolTable table)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfSymbolTableRecord record in table)
            {
                names.Add(record.Name);
            }

            return names;
        }

        private static void Need(List<MissingName> missing, Dictionary<DxfRepairKind, HashSet<string>> tables, DxfRepairKind kind, string name, string entity)
        {
            if (string.IsNullOrEmpty(name) || tables[kind].Contains(name)
                || (kind == DxfRepairKind.MissingLinetype && (string.Equals(name, "BYLAYER", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "BYBLOCK", StringComparison.OrdinalIgnoreCase))))
            {
                return;
            }

            MissingName found = missing.Find(m => m.Kind == kind && string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                missing.Add(new MissingName { Kind = kind, Name = name, Count = 1, First = entity });
            }
            else
            {
                found.Count++;
            }
        }

        /// <summary>Adds a record of a name to its table with the defaults; empty where it was added, else why not.</summary>
        private string Add(MissingName need)
        {
            try
            {
                switch (need.Kind)
                {
                    case DxfRepairKind.MissingLayer:
                        LayerTable.Add(new DxfLayerTableRecord { Name = need.Name });
                        break;
                    case DxfRepairKind.MissingLinetype:
                        LinetypeTable.Add(new DxfLinetypeTableRecord { Name = need.Name });
                        break;
                    case DxfRepairKind.MissingTextStyle:
                        TextStyleTable.Add(new DxfTextStyleTableRecord { Name = need.Name });
                        break;
                    default:
                        DimStyleTable.Add(new DxfDimStyleTableRecord { Name = need.Name });
                        break;
                }

                return string.Empty;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                return e.Message;
            }
        }

        private static string Table(DxfRepairKind kind)
            => kind == DxfRepairKind.MissingLayer ? "layer" : kind == DxfRepairKind.MissingLinetype ? "linetype" : kind == DxfRepairKind.MissingTextStyle ? "text style" : "dimension style";

        private static string Ordinal(int number) => number.ToString(CultureInfo.InvariantCulture) + (number % 100 >= 11 && number % 100 <= 13 ? "th" : number % 10 == 1 ? "st" : number % 10 == 2 ? "nd" : number % 10 == 3 ? "rd" : "th");

        /// <summary>One Info line for the call, and one Debug line with every message where there are any.</summary>
        private static void Said(string what, DxfRepairReport report)
        {
            GeometryHelperLog.Info("DxfDatabase: " + what + ": " + report.Summary);

            if (report.Count > 0)
            {
                var lines = new List<string>(report.Count);

                foreach (DxfRepair repair in report.Repairs)
                {
                    lines.Add(repair.Message);
                }

                GeometryHelperLog.Debug("DxfDatabase: " + what + ": " + string.Join(" ", lines));
            }
        }
    }
}
