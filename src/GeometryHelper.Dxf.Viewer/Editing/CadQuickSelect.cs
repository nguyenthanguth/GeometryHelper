using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The operators of Quick Select, as AutoCAD's dialog offers them.</summary>
    public enum CadQuickSelectOperator
    {
        /// <summary>= Equals: the value the same; numbers within the tolerance of a point, texts of either case.</summary>
        EqualTo,

        /// <summary>&lt;&gt; Not Equal: the value not the same.</summary>
        NotEqualTo,

        /// <summary>&gt; Greater than: a number above the value.</summary>
        GreaterThan,

        /// <summary>&lt; Less than: a number below the value.</summary>
        LessThan,

        /// <summary>* Wildcard Match: the value matched as AutoCAD's wild-card patterns match, <c>WALL*</c> any text that begins WALL.</summary>
        Wildcard,

        /// <summary>Select All: every object of the type, whatever its value.</summary>
        SelectAll,
    }

    /// <summary>
    /// Quick Select (QSELECT), as AutoCAD's: the objects of a space, or of the selection, of one type or of every type
    /// (Multiple), whose property compares with a value as the operator says, included in a new selection or excluded from
    /// it, appended to the selection or in its place. The properties are the rows of the Properties panel
    /// (<see cref="CadProperties"/>), their values compared as the panel writes them: a line 500 long has the Length
    /// 500.0000, and Length &gt; 400 takes it.
    /// </summary>
    /// <remarks>
    /// Of a plan of 312 lines on layer WALLS among others, Line, Layer, = Equals, WALLS selects the 312: "312 item(s) selected."
    /// </remarks>
    public sealed class CadQuickSelect
    {
        /// <summary>The object type that stands for every type, as AutoCAD's Multiple.</summary>
        public const string Multiple = "Multiple";

        private static readonly string[] GeneralLabels =
        {
            "Color",
            "Layer",
            "Linetype",
            "Linetype scale",
            "Lineweight",
        };

        private readonly CadDocument _document;
        private readonly List<DxfEntity> _scope = new List<DxfEntity>();
        private readonly Func<DxfEntity, bool, List<CadPropertyRow>> _rows;

        /// <summary>Makes a Quick Select over the objects of a space, or of some of them.</summary>
        /// <param name="document">The document.</param>
        /// <param name="space">The space, Apply to: Entire drawing taking its objects; null for the current space.</param>
        /// <param name="current">The handles of Apply to: Current selection; null for the whole space.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public CadQuickSelect(CadDocument document, CadSpace space = null, IEnumerable<string> current = null)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            Space = space ?? document.CurrentSpace;
            _rows = CadProperties.RowReader(document.Database);

            if (current == null)
            {
                foreach (DxfEntity entity in Space.Block)
                {
                    if (entity.Handle != null && !(entity is DxfViewport viewport && viewport.Number == 1))
                    {
                        _scope.Add(entity);
                    }
                }
            }
            else
            {
                var handles = new List<string>(current);
                handles.Sort(StringComparer.OrdinalIgnoreCase);

                foreach (string handle in handles)
                {
                    DxfEntity entity = document.FindEntity(handle);

                    if (entity != null && document.FindSpace(handle) == Space)
                    {
                        _scope.Add(entity);
                    }
                }
            }
        }

        /// <summary>Gets the space the objects are of.</summary>
        public CadSpace Space { get; }

        /// <summary>Gets how many objects are in scope.</summary>
        public int Count => _scope.Count;

        /// <summary>Gets the types of the objects in scope as the Properties panel names them (Line, Circle, MText), in ordinal order, after <see cref="Multiple"/> where there are any.</summary>
        public IReadOnlyList<string> ObjectTypes
        {
            get
            {
                var kinds = new SortedSet<string>(StringComparer.Ordinal);

                foreach (DxfEntity entity in _scope)
                {
                    kinds.Add(CadProperties.KindOf(entity));
                }

                var list = new List<string>();

                if (kinds.Count > 0)
                {
                    list.Add(Multiple);
                    list.AddRange(kinds);
                }

                return list;
            }
        }

        /// <summary>
        /// Gets the properties of a type, as the Properties panel lists them for an object of it: Color, Layer, Linetype,
        /// Linetype scale and Lineweight, then those of its kind (Start X to Angle for a line); the General ones alone for
        /// <see cref="Multiple"/>.
        /// </summary>
        /// <param name="objectType">The type.</param>
        /// <returns>The labels; empty for a type no object in scope is of.</returns>
        public IReadOnlyList<string> PropertiesOf(string objectType)
        {
            if (objectType == Multiple)
            {
                return _scope.Count > 0 ? GeneralLabels : Array.Empty<string>();
            }

            foreach (DxfEntity entity in _scope)
            {
                if (CadProperties.KindOf(entity) == objectType)
                {
                    return _rows(entity, true).ConvertAll(r => r.Label);
                }
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Gets the values in use of a property that Value offers as a list (Color, Layer, Linetype, Lineweight) among the
        /// objects of a type in scope, in ordinal order, letters of either case together; empty for one whose value is typed.
        /// </summary>
        /// <param name="objectType">The type, or <see cref="Multiple"/>.</param>
        /// <param name="property">The property.</param>
        /// <returns>The values.</returns>
        public IReadOnlyList<string> ValuesOf(string objectType, string property)
        {
            if (!IsListed(property))
            {
                return Array.Empty<string>();
            }

            var values = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfEntity entity in OfType(objectType))
            {
                string value = ValueOf(entity, property);

                if (value != null)
                {
                    values.Add(value);
                }
            }

            var sorted = new List<string>(values);
            return sorted;
        }

        /// <summary>Gets whether Value lists the values of a property rather than taking one typed: Color, Layer, Linetype and Lineweight.</summary>
        /// <param name="property">The property.</param>
        /// <returns>Whether it is listed.</returns>
        public static bool IsListed(string property) =>
            property == "Color" || property == "Layer" || property == "Linetype" || property == "Lineweight";

        /// <summary>
        /// Gets the objects that match: those of the type whose property compares with the value as the operator says, or with
        /// <paramref name="include"/> false those of the type that do not; in ordinal order of their handles.
        /// </summary>
        /// <param name="objectType">The type, or <see cref="Multiple"/>.</param>
        /// <param name="property">The property; any for <see cref="CadQuickSelectOperator.SelectAll"/>.</param>
        /// <param name="op">The operator.</param>
        /// <param name="value">The value, as the Properties panel writes it.</param>
        /// <param name="include">True for Include in new selection set, false for Exclude.</param>
        /// <param name="tolerance">The tolerance numbers are compared within: its <see cref="Tolerance.EqualPoint"/>.</param>
        /// <returns>The handles.</returns>
        public IReadOnlyList<string> Match(
            string objectType,
            string property,
            CadQuickSelectOperator op,
            string value,
            bool include,
            Tolerance tolerance
        )
        {
            var found = new List<string>();
            double within = tolerance.EqualPoint;
            bool hasNumber = TryNumber(value, out double wanted);
            System.Text.RegularExpressions.Regex pattern =
                op == CadQuickSelectOperator.Wildcard ? CadWildcard.ToRegex(value ?? string.Empty, true, true) : null;

            foreach (DxfEntity entity in OfType(objectType))
            {
                bool matches;

                if (op == CadQuickSelectOperator.SelectAll)
                {
                    matches = true;
                }
                else
                {
                    string own = ValueOf(entity, property);
                    matches =
                        own != null && Compare(own, op, value ?? string.Empty, hasNumber, wanted, within, pattern);
                }

                if (matches == include)
                {
                    found.Add(entity.Handle);
                }
            }

            found.Sort(StringComparer.OrdinalIgnoreCase);
            return found;
        }

        /// <summary>
        /// Applies a match to the document's selection, as OK does: appended to it, or in its place; and says
        /// "N item(s) selected." of the selection made.
        /// </summary>
        /// <param name="matched">The handles <see cref="Match"/> gave.</param>
        /// <param name="append">Whether Append to current selection set is ticked.</param>
        /// <returns>The line for the command line.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the handles are null.</exception>
        public string Apply(IReadOnlyList<string> matched, bool append)
        {
            if (matched == null)
            {
                throw new ArgumentNullException(nameof(matched));
            }

            if (append)
            {
                _document.Selection.Add(matched);
            }
            else
            {
                _document.Selection.Set(matched);
            }

            return matched.Count.ToString(CultureInfo.InvariantCulture) + " item(s) selected.";
        }

        private IEnumerable<DxfEntity> OfType(string objectType)
        {
            foreach (DxfEntity entity in _scope)
            {
                if (objectType == Multiple || CadProperties.KindOf(entity) == objectType)
                {
                    yield return entity;
                }
            }
        }

        /// <summary>The value of a property of an entity as the panel writes it; null for a property it has not.</summary>
        private string ValueOf(DxfEntity entity, string property)
        {
            bool general = Array.IndexOf(GeneralLabels, property) >= 0;

            foreach (CadPropertyRow row in _rows(entity, !general))
            {
                if (row.Label == property)
                {
                    return row.Value;
                }
            }

            return null;
        }

        private static bool Compare(
            string own,
            CadQuickSelectOperator op,
            string value,
            bool hasNumber,
            double wanted,
            double within,
            System.Text.RegularExpressions.Regex pattern
        )
        {
            bool isNumber = TryNumber(own, out double number);

            switch (op)
            {
                case CadQuickSelectOperator.EqualTo:
                case CadQuickSelectOperator.NotEqualTo:
                    bool same =
                        isNumber && hasNumber
                            ? Math.Abs(number - wanted) <= within
                            : string.Equals(own.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase);
                    return same == (op == CadQuickSelectOperator.EqualTo);
                case CadQuickSelectOperator.GreaterThan:
                    return isNumber && hasNumber && number - wanted > within;
                case CadQuickSelectOperator.LessThan:
                    return isNumber && hasNumber && wanted - number > within;
                case CadQuickSelectOperator.Wildcard:
                    return pattern.IsMatch(own);
                default:
                    return true;
            }
        }

        private static bool TryNumber(string text, out double value) =>
            double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);
    }
}
