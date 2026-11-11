using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A table style of a DXF file, as AutoCAD's <c>TableStyle</c>: a TABLESTYLE of the dictionary ACAD_TABLESTYLE
    /// (<see cref="DxfDatabase.TableStyleDictionary"/>), the look a <see cref="DxfTable"/> of the style takes where it and its
    /// cells give none of their own: the margins of a cell, whether the title and the header are shown, and for each kind of
    /// row, the title, the header and the data, its text style, text height, alignment, colours, fill and grid lines.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The record holds its own groups after <c>100 AcDbTableStyle</c>, then the cells of the data, the title and the header in
    /// that order, each opening with its text style (7): its height (140), alignment (170), text colour (62), fill colour (63)
    /// and whether the fill is on (283), from R2007 on the kind of its value (90, 91, 1), then for each of its six lines, top,
    /// inside and bottom across and left, inside and right down, its lineweight (274 to 279), whether it is drawn (284 to 289)
    /// and its colour (64 to 69). A group the record leaves out reads as AutoCAD's Standard has it: the data top centre and
    /// the rest middle centre, the text by block in the style Standard, no fill, every line drawn by block. The heights of
    /// AutoCAD's Standard are those of the drawing's units: the title 6 and the header and the data 4.5 high, margins of 1.5,
    /// in a drawing of millimetres, and 0.25, 0.18 and 0.06 in one of inches.
    /// </para>
    /// <para>
    /// A style made by <c>new DxfTableStyle()</c> has the values of AutoCAD's Standard in millimetres, and is added to a
    /// drawing by <see cref="DxfDBDictionary.SetAt"/> of its dictionary of table styles:
    /// <c>db.TableStyleDictionary.SetAt("GH_SCHEDULE", style)</c>. The record written is the form AutoCAD reads from R2000 on,
    /// with no map of cell styles: the title, the header and the data are its three cell styles.
    /// </para>
    /// </remarks>
    public sealed class DxfTableStyle : DxfDBObject
    {
        // The groups of a kind of cell, in the order AutoCAD writes them: its text, then its six lines.
        private static readonly int[] CellCodes = { 7, 140, 170, 62, 63, 283, 90, 91, 1 };

        // The place of each kind of row among the three cells of the record: the data's first, then the title's and the header's.
        private static readonly DxfRowType[] Written =
        {
            DxfRowType.DataRow,
            DxfRowType.TitleRow,
            DxfRowType.HeaderRow,
        };

        private Model _made;

        /// <summary>
        /// Initializes a table style in no file, of the values of AutoCAD's Standard in a drawing of millimetres: margins of 1.5,
        /// the title 6 and the header and the data 4.5 high in the text style Standard, the data top centre and the rest middle
        /// centre, no fill, every line drawn by block; to be added to a drawing by <see cref="DxfDBDictionary.SetAt"/>.
        /// </summary>
        public DxfTableStyle()
        {
            _made = Model.Standard(true, true);
        }

        internal DxfTableStyle(DxfItem item)
            : base(item) { }

        /// <summary>
        /// Gets the name of the style, as AutoCAD's <c>Name</c>: the name of its entry in the dictionary that owns it, as
        /// <c>Standard</c>; null for a style in no file, or one no dictionary names.
        /// </summary>
        public string Name
        {
            get
            {
                string owner = Item == null ? null : OwnerHandle;

                if (
                    owner != null
                    && IsResident
                    && Item.File.Database.TryGetObject(owner, out DxfDBObject found)
                    && found is DxfDBDictionary dictionary
                )
                {
                    return dictionary.NameOf(this);
                }

                return null;
            }
        }

        /// <summary>Gets or sets the description of the style (group 3), as AutoCAD's <c>Description</c>; empty where it has none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the value set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public string Description
        {
            get => Read().Head(3) ?? string.Empty;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                Change(m => m.SetHead(3, value));
            }
        }

        /// <summary>
        /// Gets or sets which way the rows of a table of the style follow one another (group 70: 0 down, 1 up), as AutoCAD's
        /// <c>FlowDirection</c>: <see cref="DxfFlowDirection.TopToBottom"/> or <see cref="DxfFlowDirection.BottomToTop"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is neither of the two.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public DxfFlowDirection FlowDirection
        {
            get => Whole(Read().Head(70), 0) == 1 ? DxfFlowDirection.BottomToTop : DxfFlowDirection.TopToBottom;
            set
            {
                if (value != DxfFlowDirection.TopToBottom && value != DxfFlowDirection.BottomToTop)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "The rows of a table run TopToBottom or BottomToTop."
                    );
                }

                Change(m => m.SetHead(70, value == DxfFlowDirection.BottomToTop ? "1" : "0"));
            }
        }

        /// <summary>Gets or sets the margin left and right of a cell's content (group 40), as AutoCAD's <c>HorizontalCellMargin</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double HorizontalCellMargin
        {
            get => Real(Read().Head(40), Read().Metric ? 1.5 : 0.06);
            set
            {
                Guard.NonNegative(value, nameof(value), "A cell's margin has to be a finite number of nought or more.");
                Change(m => m.SetHead(40, DxfTable.AsReal(value)));
            }
        }

        /// <summary>Gets or sets the margin above and below a cell's content (group 41), as AutoCAD's <c>VerticalCellMargin</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public double VerticalCellMargin
        {
            get => Real(Read().Head(41), Read().Metric ? 1.5 : 0.06);
            set
            {
                Guard.NonNegative(value, nameof(value), "A cell's margin has to be a finite number of nought or more.");
                Change(m => m.SetHead(41, DxfTable.AsReal(value)));
            }
        }

        /// <summary>Gets or sets whether a table of the style leaves its title out (the second 280), as AutoCAD's <c>IsTitleSuppressed</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool IsTitleSuppressed
        {
            get => Whole(Read().Head(-280), 0) != 0;
            set => Change(m => m.SetHead(-280, value ? "1" : "0"));
        }

        /// <summary>Gets or sets whether a table of the style leaves its header out (group 281), as AutoCAD's <c>IsHeaderSuppressed</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public bool IsHeaderSuppressed
        {
            get => Whole(Read().Head(281), 0) != 0;
            set => Change(m => m.SetHead(281, value ? "1" : "0"));
        }

        /// <summary>
        /// Gets the name of the text style of a kind of row (group 7), as AutoCAD's <c>TextStyle(rowType)</c> gives its id:
        /// <c>Standard</c> where the record gives none.
        /// </summary>
        /// <param name="rowType">The kind: <see cref="DxfRowType.TitleRow"/>, <see cref="DxfRowType.HeaderRow"/> or <see cref="DxfRowType.DataRow"/>.</param>
        /// <returns>The name.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not one of the three.</exception>
        public string TextStyleName(DxfRowType rowType)
        {
            string name = Read().Cell(Kind(rowType, nameof(rowType)), 7);
            return string.IsNullOrWhiteSpace(name) ? "Standard" : DxfReader.NameOf(name).Trim();
        }

        /// <summary>Sets the text style of kinds of row, as AutoCAD's <c>SetTextStyle(id, rowTypes)</c>, by its name.</summary>
        /// <param name="name">The name of a text style of the drawing.</param>
        /// <param name="rowTypes">The kinds, one or more of the title, the header and the data.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty, or the style is in a drawing that has no text style of the name.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kinds are none, or hold a flag none of the three has.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public void SetTextStyle(string name, DxfRowType rowTypes)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Guard.Argument(name.Trim().Length > 0, nameof(name), "A text style is named.");

            if (Item != null && IsResident && !Item.File.Database.TextStyleTable.Has(name.Trim()))
            {
                throw new ArgumentException("The drawing has no text style named " + name.Trim() + ".", nameof(name));
            }

            SetCells(rowTypes, 7, name.Trim());
        }

        /// <summary>Gets the height of the text of a kind of row (group 140), as AutoCAD's <c>TextHeight(rowType)</c>.</summary>
        /// <param name="rowType">The kind: one of the three.</param>
        /// <returns>The height.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not one of the three.</exception>
        public double TextHeight(DxfRowType rowType)
        {
            Model model = Read();
            int kind = Kind(rowType, nameof(rowType));
            return Real(model.Cell(kind, 140), Model.StandardHeight(kind, model.Metric));
        }

        /// <summary>Sets the height of the text of kinds of row, as AutoCAD's <c>SetTextHeight(height, rowTypes)</c>.</summary>
        /// <param name="height">The height, a finite number above nought.</param>
        /// <param name="rowTypes">The kinds, one or more of the three.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height is not a finite number above nought, or the kinds are none or hold a flag none of the three has.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public void SetTextHeight(double height, DxfRowType rowTypes)
        {
            Guard.Positive(height, nameof(height), "A text height has to be a finite number above nought.");
            SetCells(rowTypes, 140, DxfTable.AsReal(height));
        }

        /// <summary>Gets where the content of a cell of a kind of row stands (group 170), as AutoCAD's <c>Alignment(rowType)</c>.</summary>
        /// <param name="rowType">The kind: one of the three.</param>
        /// <returns>The alignment.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not one of the three.</exception>
        public DxfCellAlignment Alignment(DxfRowType rowType)
        {
            int kind = Kind(rowType, nameof(rowType));
            int value = Whole(Read().Cell(kind, 170), kind == 0 ? 2 : 5);
            return value >= 1 && value <= 9 ? (DxfCellAlignment)value
                : kind == 0 ? DxfCellAlignment.TopCenter
                : DxfCellAlignment.MiddleCenter;
        }

        /// <summary>Sets where the content of the cells of kinds of row stands, as AutoCAD's <c>SetAlignment(alignment, rowTypes)</c>.</summary>
        /// <param name="alignment">The alignment, one of the nine.</param>
        /// <param name="rowTypes">The kinds, one or more of the three.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the alignment is not one of the nine, or the kinds are none or hold a flag none of the three has.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public void SetAlignment(DxfCellAlignment alignment, DxfRowType rowTypes)
        {
            if ((int)alignment < 1 || (int)alignment > 9)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(alignment),
                    alignment,
                    "A cell's content is aligned at one of nine places, TopLeft to BottomRight."
                );
            }

            SetCells(rowTypes, 170, ((int)alignment).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Gets the colour of the text of a kind of row (group 62), as AutoCAD's <c>Color(rowType)</c>; by block where the record gives none.</summary>
        /// <param name="rowType">The kind: one of the three.</param>
        /// <returns>The colour.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not one of the three.</exception>
        public DxfColor Color(DxfRowType rowType) =>
            DxfColor.OfIndex(Whole(Read().Cell(Kind(rowType, nameof(rowType)), 62), 0));

        /// <summary>Sets the colour of the text of kinds of row, as AutoCAD's <c>SetColor(color, rowTypes)</c>.</summary>
        /// <param name="color">The colour: of the index, by layer or by block.</param>
        /// <param name="rowTypes">The kinds, one or more of the three.</param>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is a true colour or none.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kinds are none or hold a flag none of the three has.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public void SetColor(DxfColor color, DxfRowType rowTypes) =>
            SetCells(rowTypes, 62, IndexOf(color, nameof(color)));

        /// <summary>Gets the colour the cells of a kind of row are filled with (group 63), as AutoCAD's <c>BackgroundColor(rowType)</c>; 7 where the record gives none.</summary>
        /// <param name="rowType">The kind: one of the three.</param>
        /// <returns>The colour.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not one of the three.</exception>
        public DxfColor BackgroundColor(DxfRowType rowType) =>
            DxfColor.OfIndex(Whole(Read().Cell(Kind(rowType, nameof(rowType)), 63), 7));

        /// <summary>Sets the colour the cells of kinds of row are filled with and turns the fill on, as AutoCAD's <c>SetBackgroundColor(color, rowTypes)</c>.</summary>
        /// <param name="color">The colour: of the index, by layer or by block.</param>
        /// <param name="rowTypes">The kinds, one or more of the three.</param>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is a true colour or none.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kinds are none or hold a flag none of the three has.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public void SetBackgroundColor(DxfColor color, DxfRowType rowTypes)
        {
            string index = IndexOf(color, nameof(color));
            int[] kinds = Kinds(rowTypes, nameof(rowTypes));
            Change(m =>
            {
                foreach (int kind in kinds)
                {
                    m.SetCell(kind, 63, index);
                    m.SetCell(kind, 283, "1");
                }
            });
        }

        /// <summary>Gets whether the cells of a kind of row are left unfilled (group 283 of 0), as AutoCAD's <c>IsBackgroundColorNone(rowType)</c>; true where the record gives none.</summary>
        /// <param name="rowType">The kind: one of the three.</param>
        /// <returns>Whether they are unfilled.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kind is not one of the three.</exception>
        public bool IsBackgroundColorNone(DxfRowType rowType) =>
            Whole(Read().Cell(Kind(rowType, nameof(rowType)), 283), 0) == 0;

        /// <summary>Sets whether the cells of kinds of row are left unfilled, as AutoCAD's <c>SetBackgroundColorNone(value, rowTypes)</c>.</summary>
        /// <param name="value">Whether they are unfilled.</param>
        /// <param name="rowTypes">The kinds, one or more of the three.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the kinds are none or hold a flag none of the three has.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public void SetBackgroundColorNone(bool value, DxfRowType rowTypes) =>
            SetCells(rowTypes, 283, value ? "0" : "1");

        /// <summary>Gets the lineweight of a line of a kind of row (groups 274 to 279), as AutoCAD's <c>GridLineWeight(gridLineType, rowType)</c>; by block where the record gives none.</summary>
        /// <param name="gridLineType">The line: one of the six.</param>
        /// <param name="rowType">The kind: one of the three.</param>
        /// <returns>The lineweight.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the line is not one of the six, or the kind not one of the three.</exception>
        public DxfLineWeight GridLineWeight(DxfGridLineType gridLineType, DxfRowType rowType) =>
            (DxfLineWeight)Whole(
                Read().Cell(Kind(rowType, nameof(rowType)), 274 + Line(gridLineType, nameof(gridLineType))),
                (int)DxfLineWeight.ByBlock
            );

        /// <summary>Sets the lineweight of lines of kinds of row, as AutoCAD's <c>SetGridLineWeight(lineWeight, borders, rowTypes)</c>.</summary>
        /// <param name="lineWeight">The lineweight.</param>
        /// <param name="borders">The lines, one or more of the six.</param>
        /// <param name="rowTypes">The kinds, one or more of the three.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the lines or the kinds are none or hold a flag none of their own has.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public void SetGridLineWeight(DxfLineWeight lineWeight, DxfGridLineType borders, DxfRowType rowTypes) =>
            SetLines(borders, rowTypes, 274, ((int)lineWeight).ToString(CultureInfo.InvariantCulture));

        /// <summary>Gets whether a line of a kind of row is drawn (groups 284 to 289), as AutoCAD's <c>GridVisibility(gridLineType, rowType)</c>; drawn where the record gives none.</summary>
        /// <param name="gridLineType">The line: one of the six.</param>
        /// <param name="rowType">The kind: one of the three.</param>
        /// <returns>Whether it is drawn.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the line is not one of the six, or the kind not one of the three.</exception>
        public bool GridVisibility(DxfGridLineType gridLineType, DxfRowType rowType) =>
            Whole(Read().Cell(Kind(rowType, nameof(rowType)), 284 + Line(gridLineType, nameof(gridLineType))), 1) != 0;

        /// <summary>Sets whether lines of kinds of row are drawn, as AutoCAD's <c>SetGridVisibility(visible, borders, rowTypes)</c>.</summary>
        /// <param name="visible">Whether they are drawn.</param>
        /// <param name="borders">The lines, one or more of the six.</param>
        /// <param name="rowTypes">The kinds, one or more of the three.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the lines or the kinds are none or hold a flag none of their own has.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public void SetGridVisibility(bool visible, DxfGridLineType borders, DxfRowType rowTypes) =>
            SetLines(borders, rowTypes, 284, visible ? "1" : "0");

        /// <summary>Gets the colour of a line of a kind of row (groups 64 to 69), as AutoCAD's <c>GridColor(gridLineType, rowType)</c>; by block where the record gives none.</summary>
        /// <param name="gridLineType">The line: one of the six.</param>
        /// <param name="rowType">The kind: one of the three.</param>
        /// <returns>The colour.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the line is not one of the six, or the kind not one of the three.</exception>
        public DxfColor GridColor(DxfGridLineType gridLineType, DxfRowType rowType) =>
            DxfColor.OfIndex(
                Whole(Read().Cell(Kind(rowType, nameof(rowType)), 64 + Line(gridLineType, nameof(gridLineType))), 0)
            );

        /// <summary>Sets the colour of lines of kinds of row, as AutoCAD's <c>SetGridColor(color, borders, rowTypes)</c>.</summary>
        /// <param name="color">The colour: of the index, by layer or by block.</param>
        /// <param name="borders">The lines, one or more of the six.</param>
        /// <param name="rowTypes">The kinds, one or more of the three.</param>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is a true colour or none.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the lines or the kinds are none or hold a flag none of their own has.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the style is erased.</exception>
        public void SetGridColor(DxfColor color, DxfGridLineType borders, DxfRowType rowTypes) =>
            SetLines(borders, rowTypes, 64, IndexOf(color, nameof(color)));

        /// <summary>Gives the style as a line for debugging: its type and its handle.</summary>
        /// <returns>The line, such as <c>TABLESTYLE 7E</c>.</returns>
        public override string ToString() => "TABLESTYLE " + (Handle ?? string.Empty);

        /// <summary>The groups a style made in code is written with in a file of a version: those after its subclass marker.</summary>
        internal List<KeyValuePair<int, string>> MadeGroups(bool valued) => _made.Groups(valued);

        /// <summary>The style's groups, as the record holds them or as made in code.</summary>
        private Model Read()
        {
            if (Item == null)
            {
                return _made;
            }

            DxfFile file = Item.File;
            bool metric = file.Database == null || file.Database.Measurement == DxfMeasurementValue.Metric;
            IReadOnlyList<DxfTypedValue> groups = file.GroupsOf(Item);
            int from = Start(groups);
            var tail = new List<KeyValuePair<int, string>>();

            for (int i = from; i < groups.Count && groups[i].TypeCode != 1001; i++)
            {
                tail.Add(new KeyValuePair<int, string>(groups[i].TypeCode, groups[i].Value));
            }

            return Model.Parse(tail, metric);
        }

        /// <summary>Changes the style's groups: those made in code, or the record's written anew from its subclass marker on.</summary>
        private void Change(Action<Model> change)
        {
            if (Item == null)
            {
                change(_made);
                return;
            }

            DxfFile file = InFile("be changed");
            Model model = Read();
            change(model);
            file.ReplaceTail(Item, Start(file.GroupsOf(Item)), model.Groups(file.Supports(1021)));
        }

        private void SetCells(DxfRowType rowTypes, int code, string value)
        {
            int[] kinds = Kinds(rowTypes, nameof(rowTypes));
            Change(m =>
            {
                foreach (int kind in kinds)
                {
                    m.SetCell(kind, code, value);
                }
            });
        }

        private void SetLines(DxfGridLineType borders, DxfRowType rowTypes, int first, string value)
        {
            int lines = (int)borders;

            if (lines <= 0 || (lines & ~63) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(borders),
                    borders,
                    "The lines are one or more of the six of DxfGridLineType."
                );
            }

            int[] kinds = Kinds(rowTypes, nameof(rowTypes));
            Change(m =>
            {
                foreach (int kind in kinds)
                {
                    for (int line = 0; line < 6; line++)
                    {
                        if ((lines & (1 << line)) != 0)
                        {
                            m.SetCell(kind, first + line, value);
                        }
                    }
                }
            });
        }

        /// <summary>The place of the first group after the style's subclass marker among the record's groups, outside the groups of the applications.</summary>
        private static int Start(IReadOnlyList<DxfTypedValue> groups)
        {
            bool braced = false;

            for (int i = 0; i < groups.Count; i++)
            {
                DxfTypedValue group = groups[i];

                if (group.TypeCode == 102)
                {
                    braced = group.Value.Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && group.TypeCode == 100 && group.Value.Trim() == "AcDbTableStyle")
                {
                    return i + 1;
                }
            }

            return groups.Count;
        }

        /// <summary>The place of a kind of row among the cells of the record: the data's 0, the title's 1, the header's 2.</summary>
        private static int Kind(DxfRowType rowType, string parameter)
        {
            int at = Array.IndexOf(Written, rowType);

            if (at < 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameter,
                    rowType,
                    "The rows are of one kind: TitleRow, HeaderRow or DataRow."
                );
            }

            return at;
        }

        /// <summary>The places of the kinds of row a set of flags names, in the order of the record.</summary>
        private static int[] Kinds(DxfRowType rowTypes, string parameter)
        {
            int flags = (int)rowTypes;

            if (flags <= 0 || (flags & ~7) != 0)
            {
                throw new ArgumentOutOfRangeException(
                    parameter,
                    rowTypes,
                    "The rows are one or more of TitleRow, HeaderRow and DataRow."
                );
            }

            var kinds = new List<int>(3);

            for (int kind = 0; kind < 3; kind++)
            {
                if ((flags & (int)Written[kind]) != 0)
                {
                    kinds.Add(kind);
                }
            }

            return kinds.ToArray();
        }

        /// <summary>The place of a line among the six: top, inside and bottom across, left, inside and right down.</summary>
        private static int Line(DxfGridLineType line, string parameter)
        {
            for (int k = 0; k < 6; k++)
            {
                if ((int)line == 1 << k)
                {
                    return k;
                }
            }

            throw new ArgumentOutOfRangeException(
                parameter,
                line,
                "A line of the rows of a kind is one of the six of DxfGridLineType alone."
            );
        }

        private static string IndexOf(DxfColor color, string parameter)
        {
            if (color == null)
            {
                throw new ArgumentNullException(parameter);
            }

            switch (color.ColorMethod)
            {
                case DxfColorMethod.ByAci:
                case DxfColorMethod.ByLayer:
                case DxfColorMethod.ByBlock:
                case DxfColorMethod.Foreground:
                    return color.ColorIndex.ToString(CultureInfo.InvariantCulture);
                default:
                    throw new ArgumentException(
                        "A table style writes a colour of AutoCAD's index: " + color + " is not one.",
                        parameter
                    );
            }
        }

        private static int Whole(string text, int otherwise) =>
            int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : otherwise;

        private static double Real(string text, double otherwise) =>
            Numbers.TryRead(text, out double value) && Guard.IsFinite(value) ? value : otherwise;

        /// <summary>
        /// The groups of a TABLESTYLE after its subclass marker: its own (the head), the cells of the data, the title and the
        /// header, and what follows them, kept as read.
        /// </summary>
        private sealed class Model
        {
            private readonly List<KeyValuePair<int, string>> _head = new List<KeyValuePair<int, string>>();
            private readonly List<KeyValuePair<int, string>>[] _cells =
            {
                new List<KeyValuePair<int, string>>(),
                new List<KeyValuePair<int, string>>(),
                new List<KeyValuePair<int, string>>(),
            };
            private readonly List<KeyValuePair<int, string>> _rest = new List<KeyValuePair<int, string>>();

            private Model(bool metric)
            {
                Metric = metric;
            }

            /// <summary>Whether the style is read in a drawing of millimetres, whose Standard is of millimetres.</summary>
            internal bool Metric { get; }

            /// <summary>The height AutoCAD's Standard gives the text of a kind of row: the data's and the header's 4.5 and the title's 6 in millimetres, 0.18 and 0.25 in inches.</summary>
            internal static double StandardHeight(int kind, bool metric) =>
                kind == 1 ? (metric ? 6.0 : 0.25) : (metric ? 4.5 : 0.18);

            /// <summary>AutoCAD's Standard, every group written.</summary>
            internal static Model Standard(bool metric, bool valued)
            {
                var model = new Model(metric);
                string margin = DxfTable.AsReal(metric ? 1.5 : 0.06);
                model._head.Add(new KeyValuePair<int, string>(3, string.Empty));
                model._head.Add(new KeyValuePair<int, string>(70, "0"));
                model._head.Add(new KeyValuePair<int, string>(71, "0"));
                model._head.Add(new KeyValuePair<int, string>(40, margin));
                model._head.Add(new KeyValuePair<int, string>(41, margin));
                model._head.Add(new KeyValuePair<int, string>(-280, "0"));
                model._head.Add(new KeyValuePair<int, string>(281, "0"));

                for (int kind = 0; kind < 3; kind++)
                {
                    model._cells[kind].Add(new KeyValuePair<int, string>(7, "Standard"));
                    model
                        ._cells[kind]
                        .Add(new KeyValuePair<int, string>(140, DxfTable.AsReal(StandardHeight(kind, metric))));
                    model._cells[kind].Add(new KeyValuePair<int, string>(170, kind == 0 ? "2" : "5"));
                }

                return model;
            }

            /// <summary>
            /// Reads the groups after the marker: the head up to the first 7, whose first 280 before its description is the
            /// version and the 280 after it whether the title is left out (kept as -280); each cell from its 7 up to the next
            /// 7; what follows the third cell's groups of a cell as read.
            /// </summary>
            internal static Model Parse(List<KeyValuePair<int, string>> tail, bool metric)
            {
                var model = new Model(metric);
                int cell = -1;
                bool described = false;

                foreach (KeyValuePair<int, string> group in tail)
                {
                    if (group.Key == 7 && cell < 2 && (cell < 0 || model._rest.Count == 0))
                    {
                        cell++;
                    }

                    if (cell < 0)
                    {
                        if (group.Key == 280 && !described)
                        {
                            continue;
                        }

                        described |= group.Key == 3 || group.Key == 70 || group.Key == 40;
                        model._head.Add(
                            new KeyValuePair<int, string>(group.Key == 280 ? -280 : group.Key, group.Value)
                        );
                    }
                    else if (
                        model._rest.Count == 0
                        && (Array.IndexOf(CellCodes, group.Key) >= 0 || IsLineCode(group.Key))
                    )
                    {
                        model._cells[cell].Add(group);
                    }
                    else
                    {
                        model._rest.Add(group);
                    }
                }

                return model;
            }

            internal string Head(int code) => Find(_head, code);

            internal string Cell(int kind, int code) => Find(_cells[kind], code);

            internal void SetHead(int code, string value) => Put(_head, code, value);

            internal void SetCell(int kind, int code, string value) => Put(_cells[kind], code, value);

            /// <summary>
            /// The groups as AutoCAD writes them: from R2007 on its version (280 0); its description, flow, flags, margins and
            /// whether the title and the header are left out; then each cell, the data's, the title's and the header's, its text
            /// style, height, alignment, colours and fill, from R2007 on the kind of its value, and its six lines, each its
            /// lineweight, whether it is drawn and its colour; what followed them as read.
            /// </summary>
            internal List<KeyValuePair<int, string>> Groups(bool valued)
            {
                var groups = new List<KeyValuePair<int, string>>();

                if (valued)
                {
                    groups.Add(new KeyValuePair<int, string>(280, "0"));
                }

                string margin = DxfTable.AsReal(Metric ? 1.5 : 0.06);
                groups.Add(new KeyValuePair<int, string>(3, Head(3) ?? string.Empty));
                groups.Add(new KeyValuePair<int, string>(70, Head(70) ?? "0"));
                groups.Add(new KeyValuePair<int, string>(71, Head(71) ?? "0"));
                groups.Add(new KeyValuePair<int, string>(40, Head(40) ?? margin));
                groups.Add(new KeyValuePair<int, string>(41, Head(41) ?? margin));
                groups.Add(new KeyValuePair<int, string>(280, Head(-280) ?? "0"));
                groups.Add(new KeyValuePair<int, string>(281, Head(281) ?? "0"));

                foreach (KeyValuePair<int, string> group in _head)
                {
                    if (
                        group.Key != 3
                        && group.Key != 70
                        && group.Key != 71
                        && group.Key != 40
                        && group.Key != 41
                        && group.Key != -280
                        && group.Key != 281
                    )
                    {
                        groups.Add(group);
                    }
                }

                for (int kind = 0; kind < 3; kind++)
                {
                    groups.Add(new KeyValuePair<int, string>(7, Cell(kind, 7) ?? "Standard"));
                    groups.Add(
                        new KeyValuePair<int, string>(
                            140,
                            Cell(kind, 140) ?? DxfTable.AsReal(StandardHeight(kind, Metric))
                        )
                    );
                    groups.Add(new KeyValuePair<int, string>(170, Cell(kind, 170) ?? (kind == 0 ? "2" : "5")));
                    groups.Add(new KeyValuePair<int, string>(62, Cell(kind, 62) ?? "0"));
                    groups.Add(new KeyValuePair<int, string>(63, Cell(kind, 63) ?? "7"));
                    groups.Add(new KeyValuePair<int, string>(283, Cell(kind, 283) ?? "0"));

                    if (valued)
                    {
                        groups.Add(new KeyValuePair<int, string>(90, Cell(kind, 90) ?? "512"));
                        groups.Add(new KeyValuePair<int, string>(91, Cell(kind, 91) ?? "0"));
                        groups.Add(new KeyValuePair<int, string>(1, Cell(kind, 1) ?? string.Empty));
                    }

                    for (int line = 0; line < 6; line++)
                    {
                        groups.Add(new KeyValuePair<int, string>(274 + line, Cell(kind, 274 + line) ?? "-2"));
                        groups.Add(new KeyValuePair<int, string>(284 + line, Cell(kind, 284 + line) ?? "1"));
                        groups.Add(new KeyValuePair<int, string>(64 + line, Cell(kind, 64 + line) ?? "0"));
                    }
                }

                groups.AddRange(_rest);
                return groups;
            }

            private static bool IsLineCode(int code) =>
                (code >= 274 && code <= 279) || (code >= 284 && code <= 289) || (code >= 64 && code <= 69);

            private static string Find(List<KeyValuePair<int, string>> groups, int code)
            {
                foreach (KeyValuePair<int, string> group in groups)
                {
                    if (group.Key == code)
                    {
                        return group.Value;
                    }
                }

                return null;
            }

            private static void Put(List<KeyValuePair<int, string>> groups, int code, string value)
            {
                int at = groups.FindIndex(g => g.Key == code);

                if (at >= 0)
                {
                    groups[at] = new KeyValuePair<int, string>(code, value);
                }
                else
                {
                    groups.Add(new KeyValuePair<int, string>(code, value));
                }
            }
        }
    }
}
