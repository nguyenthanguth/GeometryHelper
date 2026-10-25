using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A DXF file opened to be edited and saved again, as AutoCAD's <c>Database</c> is a drawing: its model space, paper space
    /// and blocks, its layers and linetypes, and its header; every byte of what is not changed kept as it was read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Opened and saved with nothing changed, a file is written back byte for byte, whatever its version (R12 to R2018), line
    /// ends, padding, code page or the sections and entities the library does not read. A change writes the records it is
    /// about anew in the file's own style, and every other record keeps its bytes.
    /// </para>
    /// <para>
    /// The objects are used directly, as AutoCAD's are inside a transaction; there is no <c>ObjectId</c> and no transaction:
    /// </para>
    /// <code>
    /// // AutoCAD: btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
    /// //          btr.AppendEntity(line); tr.AddNewlyCreatedDBObject(line, true);
    /// DxfDatabase db = DxfDatabase.Open("plan.dxf");
    /// db.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "steel", ColorIndex = 1 });
    /// foreach (DxfEntity entity in db.ModelSpace) { ... }   // DxfLine, ..., DxfUnknownEntity
    /// db.Save("copy.dxf");
    /// </code>
    /// <para>
    /// A file is read as <see cref="DxfReader"/> reads it, a binary DXF and one cut short refused with its messages; its
    /// coordinates are the file's own, no unit scaled. Editing is for one thread at a time.
    /// </para>
    /// </remarks>
    public sealed partial class DxfDatabase
    {
        private readonly DxfBlockTableRecord _model;
        private readonly DxfBlockTableRecord _paper;
        private readonly DxfBlockTable _blockTable;
        private DxfLayerTable _layerTable;
        private DxfLinetypeTable _linetypeTable;
        private DxfTextStyleTable _textStyleTable;
        private DxfDimStyleTable _dimStyleTable;

        private DxfDatabase(DxfFile file)
        {
            File = file;
            file.Database = this;

            // Model space and paper space stand for their records from the first: their handles, owners and extended data are
            // there whether or not the block table is read, and their entities are read when first asked for.
            _model = new DxfBlockTableRecord(file, DxfSpaceKind.Model);
            _paper = new DxfBlockTableRecord(file, DxfSpaceKind.Paper);
            _blockTable = new DxfBlockTable(this, file.TableItem("BLOCK_RECORD"));
            _blockTable.Load();
        }

        /// <summary>The file the drawing is kept in.</summary>
        internal DxfFile File { get; }

        #region Opening and saving

        /// <summary>
        /// Opens a DXF file from a path.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>The drawing, every byte of the file kept.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="FormatException">Thrown when the file is not a DXF, is binary, or is cut short, the message naming
        /// the line; or when its header names a code page that does not read each byte of ASCII as itself; as
        /// <see cref="DxfReader"/> refuses it.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static DxfDatabase Open(string path) => new DxfDatabase(DxfFile.Open(path));

        /// <summary>
        /// Opens a DXF file from a stream, from where the stream stands to its end.
        /// </summary>
        /// <param name="stream">The stream; it is read to its end and left open.</param>
        /// <returns>The drawing, every byte of it kept.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        /// <exception cref="FormatException">Thrown when the file is not a DXF, is binary, or is cut short, as <see cref="Open(string)"/> refuses it.</exception>
        public static DxfDatabase Open(Stream stream) => new DxfDatabase(DxfFile.Open(stream));

        /// <summary>
        /// Opens a DXF file from its text, as <see cref="ToText"/> or <see cref="DxfWriter.ToString"/> gives it.
        /// </summary>
        /// <param name="text">The text of the file.</param>
        /// <returns>The drawing, its text kept as the bytes of UTF-8.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="FormatException">Thrown when the text is not a DXF, is binary, or is cut short; the message names the line.</exception>
        /// <remarks>
        /// A file of R2007 or later is UTF-8, and one of an earlier version all ASCII is the same in any code page; one of an
        /// earlier version with letters beyond ASCII is best opened from its bytes, which are kept as they are.
        /// </remarks>
        public static DxfDatabase Parse(string text) => new DxfDatabase(DxfFile.Parse(text));

        /// <summary>
        /// Saves the drawing to a path, replacing whatever is there: the bytes read where nothing was changed, and each record
        /// not changed, added or erased with its own bytes where something was.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        public void Save(string path) => File.Save(path);

        /// <summary>
        /// Saves the drawing to a stream, where it stands, as <see cref="Save(string)"/> saves it.
        /// </summary>
        /// <param name="stream">The stream; it is written to and left open.</param>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        public void Save(Stream stream) => File.Save(stream);

        /// <summary>
        /// Gets the text of the drawing as it would be saved, read as <see cref="DxfReader"/> reads it from its bytes: UTF-8 from
        /// R2007 on, the code page of its header before, the mark of UTF-8 at its start left out.
        /// </summary>
        /// <returns>The text, its line ends as the file has them.</returns>
        public string ToText() => File.ToText();

        #endregion

        #region What the drawing is

        /// <summary>
        /// Gets the version of AutoCAD the file is written for (<c>$ACADVER</c>), such as <c>AC1015</c> for R2000; null where the
        /// file does not say.
        /// </summary>
        public string Version => File.Version;

        /// <summary>
        /// Gets the code page the text of the file is read in: <c>UTF-8</c> for a file of R2007 or later, else the page its header
        /// names (<c>$DWGCODEPAGE</c>) as <c>ANSI_</c> and its number, <c>ANSI_1252</c> where it names none.
        /// </summary>
        public string CodePage => File.CodePage;

        /// <summary>
        /// Gets what was said of reading the text of the file, each a sentence: a file before R2007 whose text is UTF-8, or one
        /// in a code page the runtime has not. Empty for most files.
        /// </summary>
        public IReadOnlyList<string> Warnings => File.Warnings;

        /// <summary>
        /// Gets model space, as AutoCAD's block <c>*Model_Space</c>: the entities of the ENTITIES section not in paper space,
        /// those appended after them. It stands for its BLOCK_RECORD (its BLOCK <c>$MODEL_SPACE</c> in a file of R12) from the
        /// moment the drawing is opened, with its handle, owner and extended data.
        /// </summary>
        public DxfBlockTableRecord ModelSpace => _model;

        /// <summary>
        /// Gets paper space, as AutoCAD's block <c>*Paper_Space</c>: the entities of the ENTITIES section in paper space (group
        /// 67 of 1), those of the layout last shown. Those of the other layouts are in the blocks <c>*Paper_Space0</c> and on.
        /// It stands for its records from the moment the drawing is opened, as <see cref="ModelSpace"/> does.
        /// </summary>
        public DxfBlockTableRecord PaperSpace => _paper;

        /// <summary>Gets the block table: model space, paper space and every block, as <see cref="DxfBlockTable"/> says.</summary>
        public DxfBlockTable BlockTable => _blockTable;

        /// <summary>Gets the layer table, as <see cref="DxfLayerTable"/> says.</summary>
        public DxfLayerTable LayerTable => _layerTable ?? (_layerTable = new DxfLayerTable(File, File.TableItem("LAYER")));

        /// <summary>Gets the text style table, read, as <see cref="DxfTextStyleTable"/> says.</summary>
        public DxfTextStyleTable TextStyleTable => _textStyleTable ?? (_textStyleTable = new DxfTextStyleTable(File, File.TableItem("STYLE")));

        /// <summary>
        /// Gets the dimension style table of the drawing, as AutoCAD's <c>DimStyleTableId</c> gives its id: the styles of its
        /// DIMSTYLE records, as <see cref="DxfDimStyleTable"/> says.
        /// </summary>
        public DxfDimStyleTable DimStyleTable => _dimStyleTable ?? (_dimStyleTable = new DxfDimStyleTable(File, File.TableItem("DIMSTYLE")));

        /// <summary>Gets the linetype table, as <see cref="DxfLinetypeTable"/> says.</summary>
        public DxfLinetypeTable LinetypeTable => _linetypeTable ?? (_linetypeTable = new DxfLinetypeTable(File, File.TableItem("LTYPE")));

        #endregion

        #region The header

        /// <summary>
        /// Gets a variable of the header by its name, as the file writes it: the groups after its group 9, <c>$INSUNITS</c> of
        /// millimetres the one group 70 of value <c>4</c>, <c>$EXTMIN</c> its groups 10, 20 and 30; as last given where it is
        /// given twice. After a change, as the drawing would be saved, <c>$HANDSEED</c> and the extents set as the change needs.
        /// </summary>
        /// <param name="name">The name of the variable, its $ in front: <c>$INSUNITS</c>.</param>
        /// <returns>The groups, a new list; null where the header has no such variable.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public IReadOnlyList<DxfTypedValue> GetHeader(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return File.HeaderOf(name.Trim());
        }

        /// <summary>
        /// Sets a variable of the header, as the system variables of AutoCAD are set: its groups given in the place of its own,
        /// each value in place where the codes are as they were, every other byte of the header kept; added at the end of the
        /// header where it has no such variable. Groups the same as the variable's change nothing.
        /// </summary>
        /// <param name="name">The name of the variable, its $ in front: <c>$LTSCALE</c>.</param>
        /// <param name="groups">The groups after its group 9: <c>new DxfTypedValue(40, "2")</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name or the groups are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name does not begin with $, no group is given, or a group is of
        /// code 0 or 9, which would end the variable.</exception>
        /// <exception cref="InvalidOperationException">Thrown for <c>$ACADVER</c>, <c>$DWGCODEPAGE</c> and <c>$HANDSEED</c>, which
        /// the file keeps itself, and for a file with no HEADER section.</exception>
        public void SetHeader(string name, params DxfTypedValue[] groups)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (groups == null)
            {
                throw new ArgumentNullException(nameof(groups));
            }

            name = name.Trim();

            if (!name.StartsWith("$", StringComparison.Ordinal) || name.Length < 2)
            {
                throw new ArgumentException("The name of a variable of the header begins with $: '" + name + "' does not.", nameof(name));
            }

            if (groups.Length == 0)
            {
                throw new ArgumentException("A variable of the header is given one group or more.", nameof(groups));
            }

            foreach (DxfTypedValue group in groups)
            {
                if (group.Value == null || group.TypeCode == 0 || group.TypeCode == 9)
                {
                    throw new ArgumentException("A variable of the header has no group 0 or 9, which would end it.", nameof(groups));
                }
            }

            string upper = name.ToUpperInvariant();

            if (upper == "$ACADVER" || upper == "$DWGCODEPAGE" || upper == "$HANDSEED")
            {
                throw new InvalidOperationException("The variable " + name + " is the file's to keep: it is set as the file is saved.");
            }

            File.SetHeader(name, groups);
        }

        /// <summary>
        /// Gets or sets the layer new entities are drawn on (<c>$CLAYER</c>), as AutoCAD's <c>Clayer</c> names it: <c>0</c> where
        /// the header gives none.
        /// </summary>
        /// <value>Set, the name of a layer: one the layer table does not list is added to it, as <see cref="DxfEntity.Layer"/> adds one.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set is one AutoCAD does not take.</exception>
        public string Clayer
        {
            get
            {
                string name = DxfReader.NameOf(First("$CLAYER", 8) ?? string.Empty);
                return name.Length == 0 ? "0" : name;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!string.Equals(value, Clayer, StringComparison.Ordinal))
                {
                    DxfSymbolTable.CheckName(value, nameof(value));
                    File.LayerNeeded(value);
                    File.SetHeader("$CLAYER", new[] { new DxfTypedValue(8, value) });
                }
            }
        }

        /// <summary>
        /// Gets or sets the unit the drawing is in (<c>$INSUNITS</c>), as AutoCAD's <c>Insunits</c>: <see cref="DxfUnits.Millimeters"/>
        /// for a drawing in millimetres; <see cref="DxfUnits.Undefined"/> where the header gives none. The coordinates are not
        /// scaled by it.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the unit set is not one of <see cref="DxfUnits"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no <c>$INSUNITS</c>.</exception>
        public DxfUnits Insunits
        {
            get => int.TryParse(First("$INSUNITS", 70)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int units) && units >= 0 && units <= 24 ? (DxfUnits)units : DxfUnits.Undefined;

            set
            {
                if ((int)value < 0 || (int)value > 24)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A unit is one of DxfUnits, 0 to 24.");
                }

                if (value != Insunits)
                {
                    File.Requires(1015, "$INSUNITS", "R2000 (AC1015)");
                    File.SetHeader("$INSUNITS", new[] { new DxfTypedValue(70, ((int)value).ToString(CultureInfo.InvariantCulture)) });
                }
            }
        }

        /// <summary>
        /// Gets or sets the least corner of the box the drawing's model space fills (<c>$EXTMIN</c>), as AutoCAD's <c>Extmin</c>;
        /// the origin where the header gives none. It grows to hold what is appended or moved, and is never shrunk, as AutoCAD
        /// leaves it until it zooms to the extents.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        public GeoPoint3 Extmin
        {
            get => Point("$EXTMIN");
            set => SetPoint("$EXTMIN", value);
        }

        /// <summary>
        /// Gets or sets the greatest corner of the box the drawing's model space fills (<c>$EXTMAX</c>), as AutoCAD's
        /// <c>Extmax</c>; the origin where the header gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        public GeoPoint3 Extmax
        {
            get => Point("$EXTMAX");
            set => SetPoint("$EXTMAX", value);
        }

        /// <summary>
        /// Gets the handle the next object added to the drawing is given at least (<c>$HANDSEED</c>), as AutoCAD's
        /// <c>Handseed</c>, in hexadecimal; null where the header gives none. The file sets it above every handle it gives.
        /// </summary>
        public string Handseed => First("$HANDSEED", 5)?.Trim();

        /// <summary>The value of the first group of a code of a variable of the header; null where there is none.</summary>
        private string First(string name, int code)
        {
            IReadOnlyList<DxfTypedValue> groups = File.HeaderOf(name);

            if (groups != null)
            {
                foreach (DxfTypedValue group in groups)
                {
                    if (group.TypeCode == code)
                    {
                        return group.Value;
                    }
                }
            }

            return null;
        }

        private GeoPoint3 Point(string name)
        {
            double[] values = new double[3];

            for (int i = 0; i < 3; i++)
            {
                values[i] = Numbers.TryRead(First(name, 10 + 10 * i), out double value) && Guard.IsFinite(value) ? value : 0.0;
            }

            return new GeoPoint3(values[0], values[1], values[2]);
        }

        private void SetPoint(string name, GeoPoint3 value)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", nameof(value));
            }

            GeoPoint3 now = Point(name);

            if (BitConverter.DoubleToInt64Bits(value.X) == BitConverter.DoubleToInt64Bits(now.X) && BitConverter.DoubleToInt64Bits(value.Y) == BitConverter.DoubleToInt64Bits(now.Y)
                && BitConverter.DoubleToInt64Bits(value.Z) == BitConverter.DoubleToInt64Bits(now.Z))
            {
                return;
            }

            File.SetHeader(name, new[]
            {
                new DxfTypedValue(10, Numbers.Exact(value.X + 0.0)),
                new DxfTypedValue(20, Numbers.Exact(value.Y + 0.0)),
                new DxfTypedValue(30, Numbers.Exact(value.Z + 0.0)),
            });
        }

        #endregion
    }
}
