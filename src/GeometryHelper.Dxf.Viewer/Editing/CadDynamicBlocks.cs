using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Dynamic blocks in use, as AutoCAD 2021 drives them: the custom grips of a dynamic INSERT, a grip dragged or clicked, a
    /// property of the Custom group of Properties typed or chosen, and RESETBLOCK. Each change is one undo step, and the block
    /// is worked out anew by the library (<see cref="DxfDynamicBlockReferenceProperty.Value"/>) into an anonymous block that keeps
    /// its tie to the dynamic block, so that Properties still names it by the dynamic block's name. Parameters and actions are
    /// read, not added: the app drives the dynamic data a drawing holds.
    /// </summary>
    public static class CadDynamicBlocks
    {
        /// <summary>
        /// Gets the rows of the Custom group of Properties of a dynamic INSERT, as AutoCAD's palette lists them: each property
        /// it shows (<see cref="DxfDynamicBlockReferenceProperty.Show"/>) of a parameter the visibility state shown holds
        /// (<see cref="DxfDynamicBlockReferenceProperty.VisibleInCurrentVisibilityState"/>), by its name; a visibility state,
        /// a lookup's label and a flip's state (by the names its parameter gives them, Not flipped and Flipped) a drop-down of
        /// the values allowed; a distance to 4 places, an angle in degrees, a text and a table's row typed. A property AutoCAD
        /// keeps read only is read only.
        /// </summary>
        /// <param name="insert">The INSERT.</param>
        /// <param name="angle">How an angle in radians is written, as the rest of the palette writes one.</param>
        /// <returns>The rows; none where the INSERT places no dynamic block or its properties cannot be read.</returns>
        internal static List<CadPropertyRow> CustomRows(DxfBlockReference insert, Func<double, string> angle)
        {
            var rows = new List<CadPropertyRow>();

            foreach (DxfDynamicBlockReferenceProperty property in PropertiesOf(insert))
            {
                if (
                    string.IsNullOrEmpty(property.PropertyName)
                    || !property.Show
                    || !property.VisibleInCurrentVisibilityState
                    || rows.Exists(r => r.Label == property.PropertyName)
                )
                {
                    continue;
                }

                rows.Add(
                    new CadPropertyRow(
                        property.PropertyName,
                        TextOf(property, angle),
                        property.ReadOnly,
                        ChoicesOf(property)
                    )
                );
            }

            return rows;
        }

        /// <summary>
        /// Sets a property of the Custom group of the dynamic INSERTs among entities, as AutoCAD's palette sets it: a distance
        /// typed as a number, an angle in degrees, a flip's state by its name or as 0 or 1, a visibility state or a lookup's
        /// label by its name, a table's row by its index, a text as typed. The value is taken to the parameter's value set by
        /// the library, as AutoCAD takes it: a Width of a list of 800, 900 and 1 000 typed 870 is 900. One undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The handles of the entities selected; those that are not INSERTs of a dynamic block with the
        /// property are passed over.</param>
        /// <param name="name">The name of the property, as the row's label: Width.</param>
        /// <param name="text">The text typed or chosen.</param>
        /// <returns>What came of it: refused, nothing written, where no INSERT has the property, an INSERT is on a locked layer
        /// or the text is not a value of the property; failed, the drawing as it was, where the library does not work the block
        /// out (its message said).</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document, the handles, the name or the text is null.</exception>
        public static CadEditResult SetProperty(
            CadDocument document,
            IReadOnlyCollection<string> handles,
            string name,
            string text
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var planned = new List<CadEditRecord>();
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var sorted = new List<string>(handles);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (string handle in sorted)
            {
                if (
                    !(document.FindEntity(handle) is DxfBlockReference insert)
                    || !(Find(insert, name) is DxfDynamicBlockReferenceProperty property)
                    || values.ContainsKey(handle)
                )
                {
                    continue;
                }

                if (CadLayerEditor.IsLocked(document.Database, insert.Layer))
                {
                    return CadEditResult.Refuse("The block reference " + handle + " is on a locked layer.");
                }

                string error = Parse(property, text.Trim(), out object value);

                if (error != null)
                {
                    return CadEditResult.Refuse(error);
                }

                values.Add(handle, value);
                planned.Add(
                    new CadEditRecord(CadEditRecordKind.Reshaped, handle, name, TextOf(property, Degrees), text.Trim())
                );
            }

            if (planned.Count == 0)
            {
                return CadEditResult.Refuse("No dynamic block selected has the property " + name + ".");
            }

            return document.ApplyEdit("Properties", planned, s =>
            {
                foreach (CadEditRecord record in planned)
                {
                    Set((DxfBlockReference)s.Document.FindEntity(record.Handle), name, values[record.Handle]);
                }
            });
        }

        /// <summary>
        /// Puts every dynamic property of the dynamic INSERTs among entities back to the block's own, as AutoCAD's RESETBLOCK:
        /// each INSERT places its dynamic block again (<see cref="DxfBlockReference.ResetBlock"/>), keeping its place, scale and
        /// attributes. One undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The handles of the entities; those that are not INSERTs of a dynamic block are passed over.</param>
        /// <returns>What came of it: done with a record of each INSERT reset, none where none was; refused for an INSERT on a
        /// locked layer.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the handles are null.</exception>
        public static CadEditResult ResetBlock(CadDocument document, IReadOnlyCollection<string> handles)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            var planned = new List<CadEditRecord>();
            var sorted = new List<string>(handles);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (string handle in sorted)
            {
                if (
                    !(document.FindEntity(handle) is DxfBlockReference insert)
                    || !IsDynamic(insert)
                    || planned.Exists(r => string.Equals(r.Handle, handle, StringComparison.OrdinalIgnoreCase))
                )
                {
                    continue;
                }

                if (CadLayerEditor.IsLocked(document.Database, insert.Layer))
                {
                    return CadEditResult.Refuse("The block reference " + handle + " is on a locked layer.");
                }

                planned.Add(
                    new CadEditRecord(
                        CadEditRecordKind.Reshaped,
                        handle,
                        "Reset block",
                        insert.Name,
                        insert.DynamicBlockTableRecord.Name
                    )
                );
            }

            if (planned.Count == 0)
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            return document.ApplyEdit("RESETBLOCK", planned, s =>
            {
                foreach (CadEditRecord record in planned)
                {
                    ((DxfBlockReference)s.Document.FindEntity(record.Handle)).ResetBlock();
                }
            });
        }

        /// <summary>
        /// Clicks a custom grip of a dynamic INSERT, as AutoCAD does: a flip grip flips the block (its state 0 to 1, 1 to 0), one
        /// undo step. A grip of another kind is refused: a visibility or a lookup grip lists its values
        /// (<see cref="ValuesOf"/>), the others are dragged.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the INSERT.</param>
        /// <param name="gripIndex">The index of the grip, as <see cref="CadGrips.Of"/> gives it.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult Click(CadDocument document, string handle, int gripIndex)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!(GripOf(document, handle, gripIndex, out string refused) is CadGrip grip))
            {
                return CadEditResult.Refuse(refused);
            }

            if (grip.Kind != CadGripKind.CustomFlip)
            {
                return CadEditResult.Refuse("The grip is not a flip grip.");
            }

            var insert = (DxfBlockReference)document.FindEntity(handle);
            string name = grip.Custom.PropertyNames[0];
            object now = Find(insert, name)?.Value;
            short flipped = now is short state && state == 1 ? (short)0 : (short)1;
            return Apply(
                document,
                handle,
                "Grip " + gripIndex,
                Convert.ToString(now, CultureInfo.InvariantCulture),
                flipped.ToString(CultureInfo.InvariantCulture),
                i => Set(i, name, flipped)
            );
        }

        /// <summary>
        /// Gets what a visibility or a lookup grip lists when clicked, as AutoCAD's list at the grip: the values allowed, in
        /// the parameter's order, and the one the INSERT shows, ticked.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the INSERT.</param>
        /// <param name="gripIndex">The index of the grip, as <see cref="CadGrips.Of"/> gives it.</param>
        /// <param name="current">The value the INSERT shows; null where the grip lists nothing.</param>
        /// <returns>The values; empty for a grip of another kind, or one of no INSERT.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static IReadOnlyList<string> ValuesOf(
            CadDocument document,
            string handle,
            int gripIndex,
            out string current
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            current = null;

            if (
                !(GripOf(document, handle, gripIndex, out _) is CadGrip grip)
                || grip.Kind != CadGripKind.CustomList
                || !(
                    Find((DxfBlockReference)document.FindEntity(handle), grip.Custom.PropertyNames[0])
                    is DxfDynamicBlockReferenceProperty property
                )
            )
            {
                return Array.Empty<string>();
            }

            current = property.Value as string;
            var values = new List<string>();

            foreach (object allowed in property.GetAllowedValues())
            {
                if (allowed is string text && !values.Contains(text))
                {
                    values.Add(text);
                }
            }

            return values;
        }

        /// <summary>
        /// Chooses a value of the list of a visibility or a lookup grip, as AutoCAD's list at the grip: the state shown, or the
        /// lookup's row, every value it looks up set with it. One undo step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handle">The handle of the INSERT.</param>
        /// <param name="gripIndex">The index of the grip, as <see cref="CadGrips.Of"/> gives it.</param>
        /// <param name="value">The value chosen, one of <see cref="ValuesOf"/>.</param>
        /// <returns>What came of it: refused for a grip of another kind or a value not listed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the value is null.</exception>
        public static CadEditResult Choose(CadDocument document, string handle, int gripIndex, string value)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            IReadOnlyList<string> values = ValuesOf(document, handle, gripIndex, out string current);

            if (!((IList<string>)values).Contains(value))
            {
                return CadEditResult.Refuse("The grip lists no value " + value + ".");
            }

            string name = CadGrips.Of(document.FindEntity(handle))[gripIndex].Custom.PropertyNames[0];
            return Apply(document, handle, "Grip " + gripIndex, current, value, i => Set(i, name, value));
        }

        /// <summary>
        /// Drags a custom grip of a dynamic INSERT to a point, as AutoCAD's: a linear grip sets the distance from the base point
        /// along its parameter, a rotation grip turns its angle by the turn about the base point from the grip to the point, a
        /// point grip sets its position, a polar grip its distance and angle from the base point, an XY grip its distances along
        /// x and y; worked in the block's own coordinates and taken to the value set by the library. A flip grip dragged flips
        /// the block, as a click does; a visibility or a lookup grip is refused. One undo step.
        /// </summary>
        internal static CadEditResult Drag(CadDocument document, string handle, CadGrip grip, GeoPoint2 to)
        {
            var insert = (DxfBlockReference)document.FindEntity(handle);
            DxfDynamicBlockGrip custom = grip.Custom;
            string property = "Grip " + grip.Index;
            string was = CadCommands.Values(grip.Point.X, grip.Point.Y);
            string went = CadCommands.Values(to.X, to.Y);

            switch (grip.Kind)
            {
                case CadGripKind.CustomFlip:
                    return Click(document, handle, grip.Index);
                case CadGripKind.CustomList:
                    return CadEditResult.Refuse("Click the grip to choose from its list.");
                case CadGripKind.CustomLinear:
                {
                    double distance =
                        (to.X - custom.BasePoint.X) * custom.Direction.X
                        + (to.Y - custom.BasePoint.Y) * custom.Direction.Y;

                    if (!(distance > Tolerance.Default.EqualPoint))
                    {
                        return CadEditResult.Refuse("The distance must be above nought.");
                    }

                    return Apply(document, handle, property, was, went, i => Set(i, custom.PropertyNames[0], distance));
                }
            }

            // The rest are worked in the block's own coordinates: the INSERT may be turned, mirrored or scaled.
            if (!insert.BlockTransform.TryGetInverse(out GeoTransform3 inverse, Tolerance.Default))
            {
                return CadEditResult.Refuse("The block reference cannot be worked back to its block's coordinates.");
            }

            GeoVector3 factors = insert.ScaleFactors;
            double scale = Math.Max(Math.Abs(factors.X), Math.Max(Math.Abs(factors.Y), Math.Abs(factors.Z)));
            GeoPoint3 local = inverse.Transform(new GeoPoint3(to.X, to.Y, custom.Position.Z));
            GeoPoint3 origin = inverse.Transform(custom.BasePoint);

            switch (custom.GripType)
            {
                case DxfDynamicBlockGripType.Point:
                    return Apply(document, handle, property, was, went, i =>
                    {
                        Set(i, custom.PropertyNames[0], local.X * scale);
                        Set(i, custom.PropertyNames[1], local.Y * scale);
                    });
                case DxfDynamicBlockGripType.XY:
                {
                    double x = Math.Abs(local.X - origin.X) * scale,
                        y = Math.Abs(local.Y - origin.Y) * scale;

                    if (!(x > Tolerance.Default.EqualPoint) || !(y > Tolerance.Default.EqualPoint))
                    {
                        return CadEditResult.Refuse("The distances must be above nought.");
                    }

                    return Apply(document, handle, property, was, went, i =>
                    {
                        Set(i, custom.PropertyNames[0], x);
                        Set(i, custom.PropertyNames[1], y);
                    });
                }

                case DxfDynamicBlockGripType.Polar:
                {
                    double dx = local.X - origin.X,
                        dy = local.Y - origin.Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy) * scale;

                    if (!(distance > Tolerance.Default.EqualPoint))
                    {
                        return CadEditResult.Refuse("The distance must be above nought.");
                    }

                    double angle = Turn(Math.Atan2(dy, dx));
                    return Apply(document, handle, property, was, went, i =>
                    {
                        Set(i, custom.PropertyNames[0], distance);
                        Set(i, custom.PropertyNames[1], angle);
                    });
                }

                default:
                {
                    // A rotation: the angle it has, turned by the turn from the grip to the point about the base point.
                    GeoPoint3 from = inverse.Transform(custom.Position);

                    if (
                        !(local.DistanceTo(origin) > Tolerance.Default.EqualPoint)
                        || !(Find(insert, custom.PropertyNames[0])?.Value is double now)
                    )
                    {
                        return CadEditResult.Refuse("The point must be off the base point of the rotation.");
                    }

                    double turned =
                        Math.Atan2(local.Y - origin.Y, local.X - origin.X)
                        - Math.Atan2(from.Y - origin.Y, from.X - origin.X);
                    double angle = Turn(now + turned);
                    return Apply(document, handle, property, was, went, i => Set(i, custom.PropertyNames[0], angle));
                }
            }
        }

        /// <summary>The custom grips of an INSERT, as <see cref="DxfBlockReference.GetDynamicGrips"/> gives them; none where they cannot be read.</summary>
        internal static IReadOnlyList<DxfDynamicBlockGrip> GripsOf(DxfBlockReference insert)
        {
            try
            {
                return insert.GetDynamicGrips();
            }
            catch (Exception exception)
                when (exception is NotSupportedException
                    || exception is InvalidOperationException
                    || exception is ArgumentException
                    || exception is FormatException
                )
            {
                GeometryHelperLog.Debug(
                    "CadDynamicBlocks: the grips of the dynamic block reference "
                        + insert.Handle
                        + " are not shown: "
                        + exception.Message
                );
                return Array.Empty<DxfDynamicBlockGrip>();
            }
        }

        /// <summary>The kind of grip a custom grip is drawn and worked as.</summary>
        internal static CadGripKind KindOf(DxfDynamicBlockGrip grip)
        {
            switch (grip.GripType)
            {
                case DxfDynamicBlockGripType.Linear:
                    return CadGripKind.CustomLinear;
                case DxfDynamicBlockGripType.Rotation:
                    return CadGripKind.CustomRotation;
                case DxfDynamicBlockGripType.Flip:
                    return CadGripKind.CustomFlip;
                case DxfDynamicBlockGripType.Visibility:
                case DxfDynamicBlockGripType.Lookup:
                    return CadGripKind.CustomList;
                default:
                    return CadGripKind.CustomPoint;
            }
        }

        /// <summary>The name a block reference is shown by: its dynamic block's, as AutoCAD's palette names <c>*U3</c> of DOOR_900; else its own.</summary>
        internal static string NameOf(DxfBlockReference insert)
        {
            try
            {
                return IsDynamic(insert) ? insert.DynamicBlockTableRecord.Name : insert.Name;
            }
            catch (Exception exception)
                when (exception is NotSupportedException || exception is InvalidOperationException)
            {
                return insert.Name;
            }
        }

        /// <summary>Whether an INSERT places a dynamic block; false where that cannot be read.</summary>
        internal static bool IsDynamic(DxfBlockReference insert)
        {
            try
            {
                return insert.IsDynamicBlock;
            }
            catch (Exception exception)
                when (exception is NotSupportedException
                    || exception is InvalidOperationException
                    || exception is FormatException
                )
            {
                return false;
            }
        }

        /// <summary>One edit of one INSERT, its record a reshape of the grip or the property named.</summary>
        private static CadEditResult Apply(
            CadDocument document,
            string handle,
            string property,
            string was,
            string went,
            Action<DxfBlockReference> write
        )
        {
            if (CadLayerEditor.IsLocked(document.Database, document.FindEntity(handle).Layer))
            {
                return CadEditResult.Refuse("The object is on a locked layer.");
            }

            var record = new CadEditRecord(CadEditRecordKind.Reshaped, handle, property, was, went);
            return document.ApplyEdit("GRIP", new[] { record }, s =>
                write((DxfBlockReference)s.Document.FindEntity(handle))
            );
        }

        /// <summary>The custom grip of an index of an INSERT; null, with the reason, where it has none.</summary>
        private static CadGrip GripOf(CadDocument document, string handle, int gripIndex, out string refused)
        {
            DxfEntity entity = handle == null ? null : document.FindEntity(handle);
            refused = null;

            if (!(entity is DxfBlockReference))
            {
                refused = "The drawing has no block reference " + handle + ".";
                return null;
            }

            IReadOnlyList<CadGrip> grips = CadGrips.Of(entity);

            if (gripIndex < 0 || gripIndex >= grips.Count || grips[gripIndex].Custom == null)
            {
                refused = "The block reference has no custom grip " + gripIndex + ".";
                return null;
            }

            return grips[gripIndex];
        }

        /// <summary>The properties of an INSERT of a dynamic block; none where it places none or they cannot be read.</summary>
        private static IReadOnlyList<DxfDynamicBlockReferenceProperty> PropertiesOf(DxfBlockReference insert)
        {
            try
            {
                return IsDynamic(insert)
                    ? insert.DynamicBlockReferencePropertyCollection
                    : Array.Empty<DxfDynamicBlockReferenceProperty>();
            }
            catch (Exception exception)
                when (exception is NotSupportedException
                    || exception is InvalidOperationException
                    || exception is FormatException
                )
            {
                return Array.Empty<DxfDynamicBlockReferenceProperty>();
            }
        }

        /// <summary>The property of a name of an INSERT, the first of it; null for none.</summary>
        private static DxfDynamicBlockReferenceProperty Find(DxfBlockReference insert, string name)
        {
            foreach (DxfDynamicBlockReferenceProperty property in PropertiesOf(insert))
            {
                if (property.PropertyName == name)
                {
                    return property;
                }
            }

            return null;
        }

        /// <summary>Sets a property of a name of an INSERT, read anew, as the library works the block out.</summary>
        private static void Set(DxfBlockReference insert, string name, object value)
        {
            DxfDynamicBlockReferenceProperty property = Find(insert, name)
                ?? throw new InvalidOperationException(
                    "The block reference " + insert.Handle + " has no property " + name + "."
                );
            property.Value = value;
        }

        /// <summary>The value of a property as the palette writes it.</summary>
        private static string TextOf(DxfDynamicBlockReferenceProperty property, Func<double, string> angle)
        {
            switch (property.Value)
            {
                case null:
                    return string.Empty;
                case double number when property.UnitsType == DxfDynamicBlockReferencePropertyUnitsType.Angular:
                    return angle(number);
                case double number:
                    return (Math.Abs(number) < 5E-5 ? 0.0 : number).ToString("0.0000", CultureInfo.InvariantCulture);
                case short state when property.StateNames.Count == 2 && (state == 0 || state == 1):
                    return property.StateNames[state];
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return property.Value.ToString();
            }
        }

        /// <summary>The drop-down of a property: a flip's state names, else the texts allowed (of a visibility or a lookup); null for a value typed.</summary>
        private static IReadOnlyList<string> ChoicesOf(DxfDynamicBlockReferenceProperty property)
        {
            if (property.Value is short && property.StateNames.Count == 2)
            {
                return property.StateNames;
            }

            if (!(property.Value is string))
            {
                return null;
            }

            var choices = new List<string>();

            foreach (object allowed in property.GetAllowedValues())
            {
                if (allowed is string text && !choices.Contains(text))
                {
                    choices.Add(text);
                }
            }

            return choices.Count > 0 ? choices : null;
        }

        /// <summary>The value of a text typed for a property; null and the value, or the reason it is not one.</summary>
        private static string Parse(DxfDynamicBlockReferenceProperty property, string text, out object value)
        {
            value = null;

            switch (property.Value)
            {
                case double _:
                    if (
                        !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                        || double.IsNaN(number)
                        || double.IsInfinity(number)
                    )
                    {
                        return "The value of " + property.PropertyName + " must be a number.";
                    }

                    value =
                        property.UnitsType == DxfDynamicBlockReferencePropertyUnitsType.Angular
                            ? number * Math.PI / 180.0
                            : number;
                    return null;
                case short _:
                    for (int i = 0; i < property.StateNames.Count; i++)
                    {
                        if (string.Equals(property.StateNames[i], text, StringComparison.OrdinalIgnoreCase))
                        {
                            value = (short)i;
                            return null;
                        }
                    }

                    if (!short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out short whole))
                    {
                        return "The value of "
                            + property.PropertyName
                            + " must be "
                            + (
                                property.StateNames.Count == 2
                                    ? property.StateNames[0] + " or " + property.StateNames[1]
                                    : "a whole number"
                            )
                            + ".";
                    }

                    value = whole;
                    return null;
                default:
                    value = text;
                    return null;
            }
        }

        /// <summary>An angle in radians written in degrees to 4 places, for a record's old value.</summary>
        private static string Degrees(double radians) =>
            (radians * 180.0 / Math.PI).ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>An angle taken into a turn, from nought up to 2 pi.</summary>
        private static double Turn(double angle)
        {
            double turned = angle % (2.0 * Math.PI);
            return turned < 0.0 ? turned + 2.0 * Math.PI : turned;
        }
    }
}
