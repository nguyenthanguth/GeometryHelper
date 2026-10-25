using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
{
    /// <summary>
    /// The dimension style table of a DXF file, as AutoCAD's <c>DimStyleTable</c>: its styles in the order of the file, each a
    /// <see cref="DxfDimStyleTableRecord"/>, found by name and added to.
    /// </summary>
    /// <remarks>
    /// <c>db.DimStyleTable.Add(new DxfDimStyleTableRecord { Name = "GH_25", Dimasz = 2.5, Dimtxt = 2.5 })</c> adds the style
    /// GH_25, its arrowheads and its texts 2.5 long and high, every other variable AutoCAD's own. The handles a file of R2000
    /// or later lists in the table's head (71 and 340, as AutoCAD writes them) are kept in step with the styles added and
    /// erased; a table whose head lists none, as ezdxf writes one, is left so.
    /// </remarks>
    public sealed class DxfDimStyleTable : DxfSymbolTable, IEnumerable<DxfDimStyleTableRecord>
    {
        private readonly DxfFile _file;

        internal DxfDimStyleTable(DxfFile file, DxfItem item)
            : base(item)
        {
            _file = file;
        }

        /// <summary>
        /// Gets the style of a name, letters of either case the same.
        /// </summary>
        /// <param name="key">The name.</param>
        /// <returns>The style.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the table has no style of the name.</exception>
        public new DxfDimStyleTableRecord this[string key] => (DxfDimStyleTableRecord)base[key];

        /// <summary>Gets an enumerator over the styles, in the order of the file, those added after them.</summary>
        /// <returns>The enumerator.</returns>
        public new IEnumerator<DxfDimStyleTableRecord> GetEnumerator()
        {
            foreach (DxfItem item in _file.TableItems("DIMSTYLE").ToArray())
            {
                yield return DxfDimStyleTableRecord.Of(item);
            }
        }

        IEnumerator<DxfDimStyleTableRecord> IEnumerable<DxfDimStyleTableRecord>.GetEnumerator() => GetEnumerator();

        private protected override IEnumerable<DxfSymbolTableRecord> Records()
        {
            foreach (DxfDimStyleTableRecord record in this)
            {
                yield return record;
            }
        }

        private protected override void AddRecord(DxfSymbolTableRecord record)
        {
            if (!(record is DxfDimStyleTableRecord style))
            {
                throw new ArgumentException("The dimension style table takes a DxfDimStyleTableRecord, and was given a " + record.GetType().Name + ".", "value");
            }

            style.AddTo(_file);
        }

        /// <summary>
        /// Lists a handle in the head of the DIMSTYLE table, or takes one out of it, its count (71) kept as many as it lists
        /// (340), as AutoCAD writes the head from R2000 on; nothing where the head lists none.
        /// </summary>
        internal static void ListHandle(DxfFile file, string handle, bool listed)
        {
            DxfItem table = file.TableItem("DIMSTYLE");

            if (table == null || handle == null)
            {
                return;
            }

            var groups = new DxfDimGroups(table);
            int count = groups.Find(71, groups.All);

            if (count < 0)
            {
                return;
            }

            if (listed)
            {
                int last = count;

                for (int i = count + 1; i < groups.List.Count && groups.List[i].Key == 340; i++)
                {
                    last = i;
                }

                groups.Insert(last + 1, new KeyValuePair<int, string>(340, handle));
            }
            else
            {
                for (int i = groups.List.Count - 1; i > count; i--)
                {
                    if (groups.List[i].Key == 340 && string.Equals(groups.List[i].Value.Trim(), handle, StringComparison.OrdinalIgnoreCase))
                    {
                        groups.RemoveAt(i);
                    }
                }
            }

            int listedCount = 0;

            foreach (KeyValuePair<int, string> group in groups.List)
            {
                listedCount += group.Key == 340 ? 1 : 0;
            }

            groups.List[count] = new KeyValuePair<int, string>(71, listedCount.ToString(CultureInfo.InvariantCulture));
            groups.Write(new[] { 70, 100, 71, 340 });
        }
    }
}
