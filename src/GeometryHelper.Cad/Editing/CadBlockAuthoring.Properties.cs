using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The Properties of a parameter or an action picked in the Block Editor, as AutoCAD 2021's palette lists them: the name, the
    /// flags, the geometry read only, the labels and descriptions of the properties a parameter gives, their value sets; for an
    /// action its multiplier and angle offset or its spacings and the number of objects it works on. A row is set through
    /// <see cref="SetProperty"/>, one undo step, and the evaluation graph is written in place.
    /// </summary>
    internal static partial class CadBlockAuthoring
    {
        /// <summary>The rows of one group, each with how it is set where it is not read only.</summary>
        private sealed class RowSpec
        {
            public string Group;
            public string Label;
            public string Value;
            public bool IsReadOnly;
            public IReadOnlyList<string> Choices;
            public Action<DxfBlockElement, string> Apply;
        }

        private static readonly string[] SetTypes = { "None", "List", "Increment" };

        private static string Number(double value) =>
            (Math.Abs(value) < 5E-5 ? 0.0 : value).ToString("0.0000", CultureInfo.InvariantCulture);

        private static string Degrees(double radians) => Number(radians * 180.0 / Math.PI);

        private static bool TryNumber(string text, out double value) =>
            double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && Math.Abs(value) <= double.MaxValue;

        private static string YesNo(bool value) => value ? "Yes" : "No";

        /// <summary>The word the rows of a property's value set start with: Dist, Angle, Horz, Vert.</summary>
        private static string SetWord(DxfBlockElementKind kind, int which)
        {
            switch (kind)
            {
                case DxfBlockElementKind.XY:
                    return which == 0 ? "Horz" : "Vert";
                case DxfBlockElementKind.Rotation:
                    return "Angle";
                case DxfBlockElementKind.Polar:
                    return which == 0 ? "Dist" : "Angle";
                default:
                    return "Dist";
            }
        }

        /// <summary>The word the label and description rows of a property start with: Distance, Angle, Horizontal, Vertical, Position.</summary>
        private static string LabelWord(DxfBlockElementKind kind, int which)
        {
            switch (kind)
            {
                case DxfBlockElementKind.XY:
                    return which == 0 ? "Horizontal" : "Vertical";
                case DxfBlockElementKind.Rotation:
                    return "Angle";
                case DxfBlockElementKind.Polar:
                    return which == 0 ? "Distance" : "Angle";
                case DxfBlockElementKind.Point:
                    return "Position";
                case DxfBlockElementKind.Flip:
                    return "Flip";
                case DxfBlockElementKind.Visibility:
                    return "Visibility";
                case DxfBlockElementKind.Lookup:
                    return "Lookup";
                default:
                    return "Distance";
            }
        }

        private static bool IsAngle(DxfBlockElementKind kind, int which) =>
            kind == DxfBlockElementKind.Rotation || (kind == DxfBlockElementKind.Polar && which == 1);

        /// <summary>The number of grips the library gives a kind of parameter by default, as the palette's Number of grips row shows it.</summary>
        private static int GripsOf(DxfBlockElementKind kind)
        {
            switch (kind)
            {
                case DxfBlockElementKind.Polar:
                    return 2;
                case DxfBlockElementKind.XY:
                    return 3;
                case DxfBlockElementKind.BasePoint:
                    return 0;
                default:
                    return 1;
            }
        }

        private static List<RowSpec> RowsOf(DxfBlockElement element)
        {
            var rows = new List<RowSpec>();

            void Add(
                string group,
                string label,
                string value,
                Action<DxfBlockElement, string> apply = null,
                IReadOnlyList<string> choices = null
            ) =>
                rows.Add(
                    new RowSpec
                    {
                        Group = group,
                        Label = label,
                        Value = value,
                        IsReadOnly = apply == null,
                        Choices = choices,
                        Apply = apply,
                    }
                );

            Add("Misc", "Name", element.Name, (e, text) => DxfBlockAuthoring.Rename(e, text));

            if (element.IsParameter)
            {
                if (element.Kind != DxfBlockElementKind.Alignment && element.Kind != DxfBlockElementKind.BasePoint)
                {
                    Add(
                        "Misc",
                        "Show properties",
                        YesNo(element.ShowProperties),
                        (e, text) =>
                            DxfBlockAuthoring.SetShowProperties(
                                e,
                                text.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase)
                            ),
                        CadProperties.YesNoChoices
                    );
                }

                Add(
                    "Misc",
                    "Chain actions",
                    YesNo(element.ChainActions),
                    (e, text) =>
                        DxfBlockAuthoring.SetChainActions(
                            e,
                            text.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase)
                        ),
                    CadProperties.YesNoChoices
                );
                Add("Misc", "Number of grips", GripsOf(element.Kind).ToString(CultureInfo.InvariantCulture));
                Geometry(rows, element);

                for (int i = 0; i < element.Labels.Count; i++)
                {
                    int which = i;
                    string word = LabelWord(element.Kind, which);
                    Add("Property labels", word + " name", element.Labels[which], (e, text) =>
                        DxfBlockAuthoring.SetLabel(e, which, text)
                    );
                    Add("Property labels", word + " description", element.Descriptions[which], (e, text) =>
                        DxfBlockAuthoring.SetDescription(e, which, text)
                    );
                }

                for (int i = 0; i < element.ValueSets.Count; i++)
                {
                    int which = i;

                    if (
                        element.Kind != DxfBlockElementKind.Linear
                        && element.Kind != DxfBlockElementKind.Polar
                        && element.Kind != DxfBlockElementKind.XY
                        && element.Kind != DxfBlockElementKind.Rotation
                    )
                    {
                        continue;
                    }

                    ValueSetRows(rows, element, which);
                }
            }
            else
            {
                Add("Misc", "Number of objects", element.Entities.Count.ToString(CultureInfo.InvariantCulture));

                if (
                    element.Kind == DxfBlockElementKind.Move
                    || element.Kind == DxfBlockElementKind.Stretch
                    || element.Kind == DxfBlockElementKind.PolarStretch
                )
                {
                    Add("Action", "Distance multiplier", Number(element.Multiplier), (e, text) =>
                    {
                        if (!TryNumber(text, out double value) || value == 0.0)
                        {
                            throw new ArgumentException("Requires numeric nonzero value.");
                        }

                        DxfBlockAuthoring.SetMultiplier(e, value);
                    });
                    Add("Action", "Angle offset", Degrees(element.AngleOffset), (e, text) =>
                    {
                        if (!TryNumber(text, out double value))
                        {
                            throw new ArgumentException("Requires a number.");
                        }

                        DxfBlockAuthoring.SetAngleOffset(e, value * Math.PI / 180.0);
                    });
                }

                if (element.Kind == DxfBlockElementKind.Array)
                {
                    Add("Action", "Column spacing", Number(element.ColumnSpacing), (e, text) =>
                        DxfBlockAuthoring.SetSpacing(e, false, TryNumber(text, out double value) ? value : -1.0)
                    );

                    if (element.RowSpacing != 0.0 || element.ParameterNode >= 0)
                    {
                        Add("Action", "Row spacing", Number(element.RowSpacing), (e, text) =>
                            DxfBlockAuthoring.SetSpacing(e, true, TryNumber(text, out double value) ? value : -1.0)
                        );
                    }
                }
            }

            return rows;
        }

        private static void Geometry(List<RowSpec> rows, DxfBlockElement element)
        {
            void Read(string label, double value) =>
                rows.Add(
                    new RowSpec
                    {
                        Group = "Geometry",
                        Label = label,
                        Value = Number(value),
                        IsReadOnly = true,
                    }
                );

            GeoPoint3? b = element.Base;
            GeoPoint3? e = element.End;

            if (!b.HasValue)
            {
                return;
            }

            switch (element.Kind)
            {
                case DxfBlockElementKind.Point:
                case DxfBlockElementKind.Visibility:
                case DxfBlockElementKind.Lookup:
                case DxfBlockElementKind.BlockTable:
                    Read("Position X", b.Value.X);
                    Read("Position Y", b.Value.Y);
                    return;
                case DxfBlockElementKind.BasePoint:
                    Read("Base X", b.Value.X);
                    Read("Base Y", b.Value.Y);
                    return;
            }

            Read("Base X", b.Value.X);
            Read("Base Y", b.Value.Y);

            if (!e.HasValue)
            {
                return;
            }

            GeoVector3 along = b.Value.GetVectorTo(e.Value);

            if (element.Kind == DxfBlockElementKind.Rotation)
            {
                Read("Radius", along.Length);
                Read("Angle", Math.Atan2(along.Y, along.X) * 180.0 / Math.PI);
                return;
            }

            Read("End X", e.Value.X);
            Read("End Y", e.Value.Y);

            if (element.Kind == DxfBlockElementKind.Linear)
            {
                Read("Distance", along.Length);
            }
            else if (element.Kind == DxfBlockElementKind.Polar)
            {
                Read("Distance", along.Length);
                Read("Angle", Math.Atan2(along.Y, along.X) * 180.0 / Math.PI);
            }
            else if (element.Kind == DxfBlockElementKind.XY)
            {
                Read("Horizontal distance", Math.Abs(along.X));
                Read("Vertical distance", Math.Abs(along.Y));
            }
        }

        /// <summary>The Value set rows of a property: its type, and for increments the minimum, maximum and increment, for a list the values.</summary>
        private static void ValueSetRows(List<RowSpec> rows, DxfBlockElement element, int which)
        {
            string word = SetWord(element.Kind, which);
            bool angle = IsAngle(element.Kind, which);
            DxfBlockParameterValueSet set = element.ValueSets[which];
            string type =
                set == null ? "None"
                : set.IsList ? "List"
                : "Increment";

            string Show(double value) => angle ? Degrees(value) : Number(value);

            double Read(string text, string what)
            {
                if (!TryNumber(text, out double value) || value < 0.0)
                {
                    throw new ArgumentException("Requires numeric nonzero value.");
                }

                return angle ? value * Math.PI / 180.0 : value;
            }

            void Spec(
                string label,
                string value,
                Action<DxfBlockElement, string> apply,
                IReadOnlyList<string> choices = null
            ) =>
                rows.Add(
                    new RowSpec
                    {
                        Group = "Value set",
                        Label = label,
                        Value = value,
                        IsReadOnly = false,
                        Choices = choices,
                        Apply = apply,
                    }
                );

            Spec(
                word + " type",
                type,
                (e, text) =>
                {
                    string chosen = SetTypes.FirstOrDefault(t =>
                        string.Equals(t, text?.Trim(), StringComparison.OrdinalIgnoreCase)
                    );

                    if (chosen == null)
                    {
                        throw new ArgumentException("The type of a value set is None, List or Increment.");
                    }

                    DxfBlockParameterValueSet now = e.ValueSets[which];
                    double own = Distance(e, which);

                    if (chosen == "None")
                    {
                        DxfBlockAuthoring.SetValueSet(e, which, null);
                    }
                    else if (chosen == "List")
                    {
                        DxfBlockAuthoring.SetValueSet(
                            e,
                            which,
                            DxfBlockParameterValueSet.List(new[] { Math.Max(own, 0.0) })
                        );
                    }
                    else if (now == null || now.IsList)
                    {
                        // A first set of increments runs from nought to twice the parameter's own value, by a tenth of it; an angle's
                        // to a whole turn by 15 degrees.
                        double top =
                            angle ? 2.0 * Math.PI
                            : own > 0.0 ? 2.0 * own
                            : 1.0;
                        double step =
                            angle ? Math.PI / 12.0
                            : own > 0.0 ? own / 10.0
                            : 0.1;
                        DxfBlockAuthoring.SetValueSet(e, which, DxfBlockParameterValueSet.Increments(0.0, top, step));
                    }
                },
                SetTypes
            );

            if (set != null && !set.IsList)
            {
                Spec(word + " minimum", Show(set.Minimum), (e, text) =>
                {
                    DxfBlockParameterValueSet now = e.ValueSets[which];
                    DxfBlockAuthoring.SetValueSet(
                        e,
                        which,
                        DxfBlockParameterValueSet.Increments(Read(text, "minimum"), now.Maximum, now.Increment)
                    );
                });
                Spec(word + " maximum", Show(set.Maximum), (e, text) =>
                {
                    DxfBlockParameterValueSet now = e.ValueSets[which];
                    DxfBlockAuthoring.SetValueSet(
                        e,
                        which,
                        DxfBlockParameterValueSet.Increments(now.Minimum, Read(text, "maximum"), now.Increment)
                    );
                });
                Spec(word + " increment", Show(set.Increment), (e, text) =>
                {
                    DxfBlockParameterValueSet now = e.ValueSets[which];
                    DxfBlockAuthoring.SetValueSet(
                        e,
                        which,
                        DxfBlockParameterValueSet.Increments(now.Minimum, now.Maximum, Read(text, "increment"))
                    );
                });
            }
            else if (set != null)
            {
                Spec(word + " value list", string.Join(",", set.Values.Select(Show)), (e, text) =>
                {
                    var values = new List<double>();

                    foreach (
                        string part in (text ?? string.Empty).Split(
                            new[] { ',' },
                            StringSplitOptions.RemoveEmptyEntries
                        )
                    )
                    {
                        values.Add(Read(part, "value"));
                    }

                    DxfBlockAuthoring.SetValueSet(e, which, DxfBlockParameterValueSet.List(values));
                });
            }
        }

        /// <summary>The distance or the angle a parameter's property has now, in the unit the value set is in: the block's own value.</summary>
        private static double Distance(DxfBlockElement element, int which)
        {
            if (!element.Base.HasValue || !element.End.HasValue)
            {
                return 0.0;
            }

            GeoVector3 along = element.Base.Value.GetVectorTo(element.End.Value);

            switch (element.Kind)
            {
                case DxfBlockElementKind.Rotation:
                    return Math.Atan2(along.Y, along.X) < 0.0
                        ? Math.Atan2(along.Y, along.X) + 2.0 * Math.PI
                        : Math.Atan2(along.Y, along.X);
                case DxfBlockElementKind.Polar when which == 1:
                    double turn = Math.Atan2(along.Y, along.X);
                    return turn < 0.0 ? turn + 2.0 * Math.PI : turn;
                case DxfBlockElementKind.XY:
                    return Math.Abs(which == 0 ? along.X : along.Y);
                default:
                    return along.Length;
            }
        }

        /// <summary>The Properties of an element, as the palette shows them: its type as the selection box, then its groups.</summary>
        internal static CadSelectionInfo Describe(CadAuthoringElement element)
        {
            var groups = new List<CadPropertyGroup>();
            var titles = new List<string>();

            foreach (RowSpec row in RowsOf(element.Element))
            {
                if (!titles.Contains(row.Group))
                {
                    titles.Add(row.Group);
                }
            }

            foreach (string title in titles)
            {
                groups.Add(
                    new CadPropertyGroup(
                        title,
                        RowsOf(element.Element)
                            .Where(r => r.Group == title)
                            .Select(r => new CadPropertyRow(r.Label, r.Value, r.IsReadOnly, r.Choices))
                            .ToList()
                    )
                );
            }

            return new CadSelectionInfo(element.TypeName + " (1)", groups);
        }

        /// <summary>
        /// Sets a row of the Properties of a parameter or an action of the block open, one undo step: the name, a flag, a label or
        /// description, a value set or an action's multiplier, offset or spacing.
        /// </summary>
        /// <returns>What came of it: refused where the editor is not open, the element is gone, the row is read only or the value
        /// is not one the row takes; nothing is changed then.</returns>
        internal static CadEditResult SetProperty(CadDocument document, string handle, string label, string text)
        {
            CadBlockEditor editor = document?.BlockEditor;
            CadAuthoringElement owner = editor?.ElementOf(handle);

            if (owner == null)
            {
                return CadEditResult.Refuse("The block has no parameter or action " + handle + ".");
            }

            RowSpec row = RowsOf(owner.Element).FirstOrDefault(r => r.Label == label);

            if (row == null)
            {
                return CadEditResult.Refuse("The " + owner.TypeName.ToLowerInvariant() + " has no row " + label + ".");
            }

            if (row.IsReadOnly || row.Apply == null)
            {
                return CadEditResult.Refuse("The row " + label + " is read only.");
            }

            string blockName = editor.Name;

            return document.ApplyEdit(
                "Properties",
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, label, row.Value, text) },
                s =>
                {
                    DxfBlockElement element = Find(s.Database.BlockTable[blockName], handle);

                    if (element == null)
                    {
                        throw new InvalidOperationException("The parameter or action is no longer in the block.");
                    }

                    RowsOf(element).First(r => r.Label == label).Apply(element, text ?? string.Empty);
                }
            );
        }
    }
}
