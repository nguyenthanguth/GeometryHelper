using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>One icon of a constraint bar: a geometric constraint on the object, and where it stands in the space's constraints.</summary>
    public sealed class CadConstraintIcon
    {
        internal CadConstraintIcon(CadConstraintType type, string label, int index)
        {
            Type = type;
            Label = label;
            Index = index;
        }

        /// <summary>Gets the type of the constraint, which picks its glyph.</summary>
        public CadConstraintType Type { get; }

        /// <summary>Gets the name AutoCAD gives the constraint: Horizontal, Parallel.</summary>
        public string Label { get; }

        /// <summary>Gets the place of the constraint in the space's geometric constraints, which <see cref="CadConstraintBars.Delete"/> takes.</summary>
        public int Index { get; }
    }

    /// <summary>
    /// The constraint bar of one object or polyline segment: the icons of the geometric constraints on it, and where the object's
    /// middle lies in the drawing, the bar standing near it.
    /// </summary>
    public sealed class CadConstraintBar
    {
        internal CadConstraintBar(string handle, double x, double y, IReadOnlyList<CadConstraintIcon> icons)
        {
            Handle = handle;
            X = x;
            Y = y;
            Icons = icons;
        }

        /// <summary>Gets the handle of the object.</summary>
        public string Handle { get; }

        /// <summary>Gets the x of the place the bar stands by, in the drawing.</summary>
        public double X { get; }

        /// <summary>Gets the y of the place the bar stands by, in the drawing.</summary>
        public double Y { get; }

        /// <summary>Gets the icons, in the order the constraints were made.</summary>
        public IReadOnlyList<CadConstraintIcon> Icons { get; }
    }

    /// <summary>
    /// The constraint bars of a document, as AutoCAD 2021 shows them: for each constrained object a bar of small icons, the
    /// bars of the objects selected and those of the objects a constraint was just made on shown (CONSTRAINTBARDISPLAY), any
    /// object's bar hidden or shown for good by CONSTRAINTBAR. What is hidden is kept with the document for the session, not
    /// in the file, as AutoCAD keeps it.
    /// </summary>
    public sealed class CadConstraintBars
    {
        private enum Show
        {
            Default,
            Shown,
            Hidden,
        }

        private readonly CadDocument _document;
        private readonly Dictionary<string, Show> _state = new Dictionary<string, Show>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly HashSet<string> _fresh = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CadConstraints.Work> _works = new Dictionary<string, CadConstraints.Work>(
            StringComparer.OrdinalIgnoreCase
        );

        internal CadConstraintBars(CadDocument document)
        {
            _document = document;
            document.Changed += (sender, e) => _works.Clear();
        }

        /// <summary>Notes the objects a constraint was just made on, whose bars show until the next command.</summary>
        internal void Added(CadGeoConstraint constraint)
        {
            foreach (CadRef r in constraint.Refs)
            {
                _fresh.Add(r.Handle);
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Raised when what the bars show changes without the drawing changing: a bar shown or hidden, a constraint just made.</summary>
        public event EventHandler Changed;

        /// <summary>Shows the bars of every constrained object of a space for good (the ribbon's Show All).</summary>
        /// <param name="space">The space.</param>
        public void ShowAll(CadSpace space) => ShowBars(HandlesOf(space));

        /// <summary>Hides the bars of every constrained object of a space for good (the ribbon's Hide All).</summary>
        /// <param name="space">The space.</param>
        public void HideAll(CadSpace space) => HideBars(HandlesOf(space));

        private List<string> HandlesOf(CadSpace space)
        {
            if (space == null || !CadConstraints.HoldsAny(_document.Database))
            {
                return new List<string>();
            }

            return new List<string>(CadConstraints.Open(_document, space).Set.Handles());
        }

        /// <summary>Forgets the objects constraints were just made on: the bars of those not selected go.</summary>
        public void EndCommand() => _fresh.Clear();

        /// <summary>Shows the bars of objects, selected or not, as CONSTRAINTBAR's Show.</summary>
        /// <param name="handles">The objects.</param>
        public void ShowBars(IEnumerable<string> handles)
        {
            foreach (string handle in handles)
            {
                _state[handle] = Show.Shown;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Hides the bars of objects, selected or not, as CONSTRAINTBAR's Hide.</summary>
        /// <param name="handles">The objects.</param>
        public void HideBars(IEnumerable<string> handles)
        {
            foreach (string handle in handles)
            {
                _state[handle] = Show.Hidden;
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Puts the bars of objects back as they were before they were shown or hidden, as CONSTRAINTBAR's Reset.</summary>
        /// <param name="handles">The objects.</param>
        public void ResetBars(IEnumerable<string> handles)
        {
            foreach (string handle in handles)
            {
                _state.Remove(handle);
            }

            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Whether the bar of an object is hidden for good.</summary>
        /// <param name="handle">The object.</param>
        /// <returns>True where CONSTRAINTBAR Hide hid it.</returns>
        public bool IsHidden(string handle) => _state.TryGetValue(handle, out Show s) && s == Show.Hidden;

        /// <summary>
        /// The bars of a space to draw now: those of every object whose bar is shown for good, of the objects selected where the
        /// settings show the bars of the selection, and of the objects a constraint was just made on where they show those; an
        /// object hidden has none. The icons are those of the types the settings show.
        /// </summary>
        /// <param name="space">The space.</param>
        /// <param name="selected">The handles of the objects selected.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>The bars, in the order of their objects' handles.</returns>
        public IReadOnlyList<CadConstraintBar> BarsFor(
            CadSpace space,
            IReadOnlyCollection<string> selected,
            CadConstraintSettings settings
        )
        {
            var bars = new List<CadConstraintBar>();

            if (
                space == null
                || settings == null
                || settings.BarDisplay == 0
                || !CadConstraints.HoldsAny(_document.Database)
            )
            {
                return bars;
            }

            if (!_works.TryGetValue(space.Name, out CadConstraints.Work work))
            {
                work = CadConstraints.Open(_document, space);
                _works[space.Name] = work;
            }

            if (work.Set.Geometric.Count == 0)
            {
                return bars;
            }

            var chosen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, Show> pair in _state)
            {
                if (pair.Value == Show.Shown)
                {
                    chosen.Add(pair.Key);
                }
            }

            if ((settings.BarDisplay & 1) != 0 && selected != null)
            {
                foreach (string handle in selected)
                {
                    chosen.Add(handle);
                }
            }

            if ((settings.BarDisplay & 2) != 0)
            {
                chosen.UnionWith(_fresh);
            }

            var byKey = new SortedDictionary<string, List<CadConstraintIcon>>(StringComparer.Ordinal);
            var refs = new Dictionary<string, CadRef>(StringComparer.Ordinal);

            for (int i = 0; i < work.Set.Geometric.Count; i++)
            {
                CadGeoConstraint constraint = work.Set.Geometric[i];
                CadConstraintType type = CadConstraintSettings.TypeOf(constraint.Kind);

                if (!settings.ShowsBarOf(type))
                {
                    continue;
                }

                var shown = new HashSet<string>(StringComparer.Ordinal);

                foreach (CadRef r in constraint.Refs)
                {
                    string key = r.Handle.ToUpperInvariant() + "#" + r.Segment.ToString(CultureInfo.InvariantCulture);

                    if (!chosen.Contains(r.Handle) || IsHidden(r.Handle) || !shown.Add(key))
                    {
                        continue;
                    }

                    if (!byKey.TryGetValue(key, out List<CadConstraintIcon> icons))
                    {
                        byKey[key] = icons = new List<CadConstraintIcon>();
                        refs[key] = r;
                    }

                    icons.Add(new CadConstraintIcon(type, constraint.Label, i));
                }
            }

            foreach (KeyValuePair<string, List<CadConstraintIcon>> pair in byKey)
            {
                CadRef r = refs[pair.Key];

                if (!work.Shapes.TryGetValue(r.Handle, out CadShape shape))
                {
                    continue;
                }

                GeoPoint2? at = Anchor(shape, r.Segment);

                if (at.HasValue)
                {
                    bars.Add(new CadConstraintBar(r.Handle, at.Value.X, at.Value.Y, pair.Value));
                }
            }

            return bars;
        }

        /// <summary>The place a bar stands by: the middle of a line or segment, the point of a circle's rim at 45 degrees, the middle of an arc's chord's far side.</summary>
        private static GeoPoint2? Anchor(CadShape shape, int segment)
        {
            double[] v = shape.Values;

            switch (shape.Kind)
            {
                case CadShapeKind.Circle:
                    return new GeoPoint2(v[0] + v[2] * Math.Cos(Math.PI / 4.0), v[1] + v[2] * Math.Sin(Math.PI / 4.0));
                case CadShapeKind.Arc:
                {
                    double a = Math.Atan2(v[4] - v[1], v[3] - v[0]);
                    double b = Math.Atan2(v[6] - v[1], v[5] - v[0]);

                    if (b < a)
                    {
                        b += 2.0 * Math.PI;
                    }

                    double mid = (a + b) / 2.0;
                    return new GeoPoint2(v[0] + v[2] * Math.Cos(mid), v[1] + v[2] * Math.Sin(mid));
                }

                default:
                    return CadConstraintShapes.PointOf(shape, new CadRef(shape.Handle, segment, CadPart.Mid));
            }
        }

        /// <summary>Takes one constraint away, as an icon of a bar is deleted; one undo step.</summary>
        /// <param name="space">The space.</param>
        /// <param name="index">The place of the constraint in the space's geometric constraints (<see cref="CadConstraintIcon.Index"/>).</param>
        /// <returns>What came of it.</returns>
        public CadEditResult Delete(CadSpace space, int index)
        {
            CadConstraints.Work work = CadConstraints.Open(_document, space);

            if (work.Set.Refusal != null)
            {
                return CadEditResult.Refuse(work.Set.Refusal);
            }

            if (index < 0 || index >= work.Set.Geometric.Count)
            {
                return CadEditResult.Refuse("The constraint is no longer there.");
            }

            CadGeoConstraint constraint = work.Set.Geometric[index];
            work.Set.Geometric.RemoveAt(index);
            var planned = new List<CadEditRecord>
            {
                new CadEditRecord(
                    CadEditRecordKind.PropertyChanged,
                    constraint.Refs[0].Handle,
                    CadConstraints.OwnProperty
                ),
            };
            return _document.ApplyEdit("DELCONSTRAINT", planned, s => CadConstraints.Write(work));
        }
    }

    public sealed partial class CadDocument
    {
        private CadConstraintBars _constraintBars;

        /// <summary>Gets the constraint bars of the document: which are shown, and the icons of each.</summary>
        public CadConstraintBars ConstraintBars => _constraintBars ?? (_constraintBars = new CadConstraintBars(this));
    }

    public sealed partial class CadCommandSession
    {
        /// <summary>Gets or sets the settings of the Constraint Settings dialog and of the constraint variables, which stay with the session.</summary>
        public CadConstraintSettings Constraints { get; set; } = new CadConstraintSettings();

        /// <summary>Gets or sets whether the Parameters Manager palette is shown, which PARAMETERSSTATUS reads; the window sets it as it shows or closes the palette.</summary>
        public bool ParametersShown { get; set; }
    }
}
