using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A scale of the drawing's list of scales, as AutoCAD's <c>AnnotationScale</c>: a SCALE of the dictionary ACAD_SCALELIST,
    /// its name (group 300) and so many units of the paper (140) to so many of the drawing (141). The scale <c>1:50</c> of a
    /// plan in millimetres is 1 of the paper to 50 of the drawing, <see cref="Scale"/> 0.02: 1 mm of the sheet stands for 50 mm
    /// of the drawing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A scale is read from <see cref="DxfDatabase.ObjectContextManager"/>, in the collection <c>ACDB_ANNOTATIONSCALES</c>, a
    /// new object at each reading that stands for its SCALE: a property set writes the record at once, every other byte of the
    /// file kept. A scale made in code, <c>new DxfAnnotationScale { Name = "1:20", PaperUnits = 1, DrawingUnits = 20 }</c>, is in no
    /// file until <see cref="DxfObjectContextCollection.AddContext"/> adds it.
    /// </para>
    /// </remarks>
    public sealed class DxfAnnotationScale : DxfObjectContext
    {
        // The groups of AcDbScale in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 70, 300, 140, 141, 290 });

        private DxfItem _item;
        private string _name = string.Empty;
        private double _paperUnits = 1.0;
        private double _drawingUnits = 1.0;

        /// <summary>
        /// Initializes a scale made in code, as AutoCAD's <c>new AnnotationScale()</c>: of no name, 1 unit of the paper to 1 of
        /// the drawing; give it its name and units before it is added to a drawing's list.
        /// </summary>
        public DxfAnnotationScale()
        {
        }

        /// <summary>A scale that stands for a SCALE of a file.</summary>
        internal DxfAnnotationScale(DxfItem item)
        {
            _item = item;
        }

        /// <summary>The SCALE the scale stands for; null for one made in code and not added yet.</summary>
        internal DxfItem Item => _item;

        /// <summary>Gets the handle of the SCALE the scale stands for, as the file writes it; null for a scale in no file.</summary>
        internal string Handle => _item?.Handle;

        /// <summary>Gets the name of the collection of scales, as AutoCAD's <c>CollectionName</c>: <c>ACDB_ANNOTATIONSCALES</c>.</summary>
        public override string CollectionName => DxfObjectContextManager.AnnotationScales;

        /// <summary>
        /// Gets or sets the name of the scale (group 300), as AutoCAD's <c>Name</c>, such as <c>1:50</c>; the spaces around it
        /// taken off.
        /// </summary>
        /// <value>Set, a name no other scale of the drawing's list has, letters of either case the same; the annotation scale of
        /// model space (<see cref="DxfDatabase.Cannoscale"/>) that named the scale names it by its new name.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set is empty, has a control character in it, or is another
        /// scale's of the list.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the scale's SCALE is erased, or the scale is the current one and
        /// its drawing could not write CANNOSCALE anew, as one whose CLASSES section lists no DICTIONARYVAR; nothing is written
        /// then.</exception>
        public override string Name
        {
            get => _item == null ? _name : (Value(300) ?? string.Empty).Trim();

            set
            {
                CheckName(value, nameof(value));

                if (_item == null)
                {
                    _name = value.Trim();
                    return;
                }

                string now = Name;

                if (string.Equals(value.Trim(), now, StringComparison.Ordinal))
                {
                    return;
                }

                DxfFile file = Resident();
                DxfAnnotationScale other = DxfObjectContextCollection.Find(file.Database, value);

                if (other != null && !string.Equals(other.Handle, Handle, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("The drawing's list of scales has a scale named " + value.Trim() + " already, letters of either case the same.", nameof(value));
                }

                // The annotation scale that names it is to name it anew: refused before the name is written where it cannot be.
                bool current = string.Equals(file.Database.Cannoscale, now, StringComparison.OrdinalIgnoreCase);

                if (current)
                {
                    file.Database.CheckVariableWritable("CANNOSCALE");
                }

                file.SetValues(_item, new[] { new KeyValuePair<int, string>(300, value.Trim()) }, Order, false);

                if (current)
                {
                    file.Database.Cannoscale = value.Trim();
                }
            }
        }

        /// <summary>Gets or sets how many units of the paper the scale gives (group 140), as AutoCAD's <c>PaperUnits</c>: 1 for <c>1:50</c>; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the units set are not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the scale's SCALE is erased.</exception>
        public double PaperUnits
        {
            get => _item == null ? _paperUnits : Real(140);
            set => SetUnits(140, value, PaperUnits, ref _paperUnits);
        }

        /// <summary>Gets or sets how many units of the drawing the scale gives (group 141), as AutoCAD's <c>DrawingUnits</c>: 50 for <c>1:50</c>; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the units set are not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the scale's SCALE is erased.</exception>
        public double DrawingUnits
        {
            get => _item == null ? _drawingUnits : Real(141);
            set => SetUnits(141, value, DrawingUnits, ref _drawingUnits);
        }

        /// <summary>Gets the scale as a number, as AutoCAD's <c>Scale</c>: <see cref="PaperUnits"/> over <see cref="DrawingUnits"/>, 0.02 for <c>1:50</c>.</summary>
        public double Scale => PaperUnits / DrawingUnits;

        /// <summary>Gets whether the scale is the drawing's unit scale (group 290 of 1), the <c>1:1</c> AutoCAD keeps in every list and never takes out.</summary>
        internal bool IsUnitScale => _item != null && Value(290)?.Trim() == "1";

        /// <summary>Gives the scale as a line for debugging: its name and its units.</summary>
        /// <returns>The line, such as <c>1:50 (1 to 50)</c>.</returns>
        public override string ToString()
            => Name + " (" + PaperUnits.ToString("R", CultureInfo.InvariantCulture) + " to " + DrawingUnits.ToString("R", CultureInfo.InvariantCulture) + ")";

        /// <summary>Makes the scale stand for the SCALE written for it when it was added to a drawing's list.</summary>
        internal void Bind(DxfItem item) => _item = item;

        /// <summary>Refuses a name a scale cannot have: none, or one with a control character in it.</summary>
        internal static void CheckName(string name, string parameter)
        {
            if (name == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (name.Trim().Length == 0)
            {
                throw new ArgumentException("A scale has a name, such as 1:50.", parameter);
            }

            foreach (char c in name)
            {
                if (c < ' ')
                {
                    throw new ArgumentException("The name of a scale has no control character in it: '" + name + "' has one.", parameter);
                }
            }
        }

        private void SetUnits(int code, double value, double now, ref double made)
        {
            Guard.Positive(value, nameof(value), "The units of a scale are a finite number above nought.");

            if (BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(now))
            {
                return;
            }

            if (_item == null)
            {
                made = value;
                return;
            }

            Resident().SetValues(_item, new[] { new KeyValuePair<int, string>(code, Numbers.Exact(value)) }, Order, false);
        }

        /// <summary>The file of the scale's SCALE, which is not erased.</summary>
        private DxfFile Resident()
        {
            if (_item.IsRemoved)
            {
                throw new InvalidOperationException("The SCALE " + (Handle ?? string.Empty) + " of the scale is erased, and is not changed.");
            }

            return _item.File;
        }

        /// <summary>The first group of a code of the SCALE; null where it has none.</summary>
        private string Value(int code)
        {
            foreach (DxfTypedValue group in _item.File.ObjectGroupsOf(_item))
            {
                if (group.TypeCode == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>A number of the SCALE above nought; 1 where it has none such.</summary>
        private double Real(int code) => Numbers.TryRead(Value(code), out double value) && Guard.IsFinite(value) && value > 0.0 ? value : 1.0;
    }
}
