using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The parametric constraints of AutoCAD 2021's Parametric tab for the presenter: the geometric constraints (GCCOINCIDENT
    /// and the others) and the dimensional ones (DCLINEAR and the others) of the lines, circles, arcs and straight polyline
    /// segments of a space, kept in the drawing as AutoCAD keeps them (<see cref="CadConstraintStore"/>), and solved by
    /// <see cref="CadConstraintSolver"/> when a constraint is made, a parameter changed, or a constrained object edited: the
    /// edit then carries the other objects with it (<see cref="Follow"/>), or is taken back with "Constraints cannot be
    /// satisfied." where no place meets them all.
    /// </summary>
    internal static partial class CadConstraints
    {
        /// <summary>The property name of the records the constraints make, which <see cref="Follow"/> does not take for the user's edits.</summary>
        internal const string OwnProperty = "Constraints";

        /// <summary>The message of constraints no place can meet.</summary>
        internal const string CannotSatisfy = "Constraints cannot be satisfied.";

        /// <summary>The heaviest weight, that of a number an edit has just placed.</summary>
        private const double Pinned = 1E30;

        /// <summary>The weight of the object a constraint is made on first: the others move to it.</summary>
        private const double Anchored = 1E24;

        /// <summary>The weight of a line's start over its end, which AutoCAD 2021 keeps where it is as it turns or stretches the line.</summary>
        private const double Start = 1E12;

        /// <summary>The working state of one operation on the constraints of one space.</summary>
        internal sealed class Work
        {
            internal CadDocument Document;
            internal CadSpace Space;
            internal CadConstraintSet Set;
            internal readonly Dictionary<string, CadShape> Shapes = new Dictionary<string, CadShape>(
                StringComparer.OrdinalIgnoreCase
            );

            /// <summary>The object of a handle, as the document finds it.</summary>
            internal DxfEntity Entity(string handle) => Kept ? Document.FindKept(handle) : Document.FindEntity(handle);

            /// <summary>Whether an object an edit erased is still found, as it is while the edit is followed.</summary>
            internal bool Kept;

            /// <summary>Takes the shape of an object into the work where it has one; false where it is not an object constraints are tied to.</summary>
            internal bool Take(string handle)
            {
                if (Shapes.ContainsKey(handle))
                {
                    return true;
                }

                CadShape shape = CadConstraintShapes.Of(Entity(handle));

                if (shape == null)
                {
                    return false;
                }

                Shapes[handle] = shape;
                return true;
            }
        }

        /// <summary>
        /// Opens the constraints of a space: the set read from its network, and the shape of each object it ties.
        /// </summary>
        internal static Work Open(CadDocument document, CadSpace space, bool kept = false)
        {
            var work = new Work
            {
                Document = document,
                Space = space,
                Kept = kept,
            };
            work.Set = CadConstraintStore.Read(document.Database, space.Block, work.Entity);

            foreach (string handle in work.Set.Handles())
            {
                work.Take(handle);
            }

            return work;
        }

        /// <summary>Whether the drawing can hold constraints: AutoCAD 2010 brought the objects that keep them.</summary>
        internal static bool CanHold(DxfDatabase database) => CadCenterMarks.CanTie(database);

        /// <summary>The message of a drawing that cannot hold constraints.</summary>
        internal const string CannotHold =
            "A drawing of this version cannot hold constraints: save it as AutoCAD 2010 or later first.";

        /// <summary>
        /// Whether the space of a handle holds constraints at all: its network lists a constraint group. Quick enough to ask of
        /// every edit.
        /// </summary>
        internal static bool HoldsAny(DxfDatabase database)
        {
            try
            {
                return database.File.HoldsObjects
                    && database.NamedObjectsDictionary.Contains(CadCenterMarks.NetworkKey);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        #region Parameters

        /// <summary>
        /// Works the expression of every parameter out, each from the others it names, and puts the values in. A parameter with
        /// no expression keeps its value. False, and the values untouched, where an expression cannot be worked out or the
        /// parameters name one another in a ring.
        /// </summary>
        internal static bool Evaluate(CadConstraintSet set, out string error)
        {
            error = null;
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string failure = null;

            double? Resolve(string name)
            {
                if (values.TryGetValue(name, out double done))
                {
                    return done;
                }

                CadParameter parameter = set.Parameter(name);

                if (parameter == null)
                {
                    return null;
                }

                if (!visiting.Add(name))
                {
                    failure = "The parameters " + name + " and others name one another in a ring.";
                    throw new FormatException(failure);
                }

                double value = parameter.Value;

                if (!string.IsNullOrWhiteSpace(parameter.Expression))
                {
                    if (!CadConstraintExpression.TryEvaluate(parameter.Expression, Resolve, out value, out string why))
                    {
                        failure = parameter.Name + ": " + why;
                        throw new FormatException(failure);
                    }
                }

                visiting.Remove(name);
                values[name] = value;
                return value;
            }

            try
            {
                foreach (CadParameter parameter in set.Parameters)
                {
                    Resolve(parameter.Name);
                }
            }
            catch (FormatException exception)
            {
                error = failure ?? exception.Message;
                return false;
            }

            foreach (CadParameter parameter in set.Parameters)
            {
                parameter.Value = values[parameter.Name];
            }

            return true;
        }

        #endregion

        #region Solving

        /// <summary>
        /// Solves a set where the objects stand now. The weights keep what an edit placed (<paramref name="pinned"/>, by handle)
        /// where it is, and the object a constraint was made on first (<paramref name="anchor"/>) nearly so; every line keeps its
        /// start nearer than its end.
        /// </summary>
        internal static CadSolveResult Solve(Work work, ISet<string> pinned, string anchor)
        {
            Func<string, double?> parameter = name =>
            {
                CadParameter p = work.Set.Parameter(name);
                return p == null ? (double?)null : p.Value;
            };

            CadConstraintSolver.Weighter weight = (handle, index) =>
            {
                double w = 1.0;
                CadShape shape = work.Shapes[handle];

                if (shape.Kind == CadShapeKind.Line && index < 2)
                {
                    w *= Start;
                }

                if (pinned != null && pinned.Contains(handle))
                {
                    w *= Pinned;
                }
                else if (anchor != null && string.Equals(anchor, handle, StringComparison.OrdinalIgnoreCase))
                {
                    w *= Anchored;
                }

                return w;
            };

            return CadConstraintSolver.Solve(work.Set, work.Shapes, parameter, weight);
        }

        /// <summary>
        /// The records of the objects a solution moves, as planned changes for <see cref="CadDocument.ApplyEdit"/>; none where
        /// it moves nothing.
        /// </summary>
        internal static List<CadEditRecord> Moves(Work work, CadSolveResult result)
        {
            var records = new List<CadEditRecord>();

            foreach (KeyValuePair<string, double[]> pair in result.Values)
            {
                if (CadConstraintShapes.Differ(work.Shapes[pair.Key].Values, pair.Value))
                {
                    records.Add(new CadEditRecord(CadEditRecordKind.Reshaped, pair.Key, OwnProperty));
                }
            }

            records.AddRange(DimensionRecords(work));
            return records;
        }

        /// <summary>The records of the dimensions of the dimensional constraints, which are drawn again whenever a number changes.</summary>
        internal static List<CadEditRecord> DimensionRecords(Work work)
        {
            var records = new List<CadEditRecord>();

            foreach (CadDimConstraint constraint in work.Set.Dimensional)
            {
                if (constraint.DimensionHandle != null && work.Entity(constraint.DimensionHandle) != null)
                {
                    records.Add(new CadEditRecord(CadEditRecordKind.Reshaped, constraint.DimensionHandle, OwnProperty));
                }
            }

            return records;
        }

        /// <summary>
        /// Puts a solution into the drawing: the objects it moves written, the dimensions that show constraints drawn again, the
        /// set written into the network.
        /// </summary>
        internal static void Commit(Work work, CadSolveResult result)
        {
            if (result != null)
            {
                foreach (KeyValuePair<string, double[]> pair in result.Values)
                {
                    CadShape before = work.Shapes[pair.Key];

                    if (CadConstraintShapes.Differ(before.Values, pair.Value))
                    {
                        CadConstraintShapes.Apply(work.Entity(pair.Key), before, pair.Value);
                        work.Shapes[pair.Key] = new CadShape(pair.Key, before.Kind, pair.Value, before.Closed);
                    }
                }
            }

            RefreshDimensions(work);
            Write(work);
        }

        /// <summary>Writes the set into the drawing, with the shapes the work holds.</summary>
        internal static void Write(Work work)
        {
            CadConstraintStore.Write(work.Document.Database, work.Space.Block, work.Set, work.Shapes, h =>
                work.Document.FindEntity(h)
            );
        }

        /// <summary>Draws the dimension of each dimensional constraint again from the numbers the work holds now.</summary>
        internal static void RefreshDimensions(Work work)
        {
            foreach (CadDimConstraint constraint in work.Set.Dimensional)
            {
                if (constraint.DimensionHandle == null)
                {
                    continue;
                }

                if (work.Entity(constraint.DimensionHandle) is DxfDimension dimension)
                {
                    CadCommands.RefreshConstraintDimension(work, constraint, dimension);
                }
            }
        }

        #endregion

        #region Edits

        /// <summary>
        /// Follows an edit, as the other associations do (<see cref="CadDocument.ApplyEdit"/> calls it after the edit is made):
        /// each space whose constraints tie an object the edit changed is solved again with what the edit placed held, the
        /// other objects moved to meet the constraints; an object erased takes the constraints on it with it, the dimensions
        /// of the dimensional ones too. Where no place meets the constraints the edit is taken back, with
        /// <see cref="CannotSatisfy"/>. Returns the records of the objects moved, for the edit's step.
        /// </summary>
        internal static List<CadEditRecord> Follow(
            CadDocument document,
            IReadOnlyList<CadEditRecord> planned,
            IReadOnlyList<CadEditRecord> made
        )
        {
            var followed = new List<CadEditRecord>();

            if (!HoldsAny(document.Database))
            {
                return followed;
            }

            var edited = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            var erased = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IReadOnlyList<CadEditRecord> records in new[] { planned, made })
            {
                foreach (CadEditRecord record in records)
                {
                    if (record.Handle == null)
                    {
                        continue;
                    }

                    switch (record.Kind)
                    {
                        case CadEditRecordKind.Transformed:
                        case CadEditRecordKind.Reshaped:
                            if (record.Property != OwnProperty)
                            {
                                edited.Add(record.Handle);
                            }

                            break;
                        case CadEditRecordKind.PropertyChanged:
                            if (record.Property != OwnProperty && IsGeometry(record.Property))
                            {
                                edited.Add(record.Handle);
                            }

                            break;
                        case CadEditRecordKind.Erased:
                            erased.Add(record.Handle);
                            break;
                    }
                }
            }

            if (edited.Count == 0 && erased.Count == 0)
            {
                return followed;
            }

            DxfFile file = document.Database.File;
            var networks = new SortedSet<string>(StringComparer.Ordinal);

            foreach (string handle in new SortedSet<string>(edited, StringComparer.OrdinalIgnoreCase))
            {
                CollectNetworks(document, file, handle, networks);
            }

            foreach (string handle in erased)
            {
                CollectNetworks(document, file, handle, networks);
            }

            foreach (string network in networks)
            {
                foreach (CadSpace space in document.Spaces)
                {
                    DxfItem held = CadConstraintStore.NetworkOf(space.Block);

                    if (held == null || !string.Equals(held.Handle, network, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    FollowSpace(document, space, edited, erased, followed);
                }
            }

            return followed;
        }

        private static bool IsGeometry(string property)
        {
            switch (property)
            {
                case "Start X":
                case "Start Y":
                case "End X":
                case "End Y":
                case "Center X":
                case "Center Y":
                case "Radius":
                case "Diameter":
                case "Start angle":
                case "End angle":
                case "Vertex X":
                case "Vertex Y":
                case "Length":
                case "Angle":
                    return true;
                default:
                    return property != null
                        && (
                            property.IndexOf("X", StringComparison.Ordinal) >= 0
                            || property.IndexOf("Y", StringComparison.Ordinal) >= 0
                            || property.IndexOf("Radius", StringComparison.Ordinal) >= 0
                        );
            }
        }

        private static void CollectNetworks(
            CadDocument document,
            DxfFile file,
            string handle,
            SortedSet<string> networks
        )
        {
            DxfEntity entity = document.FindKept(handle);

            if (entity == null)
            {
                return;
            }

            foreach (string reactor in entity.GetPersistentReactorIds())
            {
                DxfItem dependency = file.ObjectItem(reactor);

                if (dependency == null || dependency.IsRemoved)
                {
                    continue;
                }

                DxfItem owner = file.ObjectItem(file.OwnerOf(dependency) ?? string.Empty);

                if (owner != null && !owner.IsRemoved && owner.Type == "ACDBASSOC2DCONSTRAINTGROUP")
                {
                    DxfItem network = file.ObjectItem(file.OwnerOf(owner) ?? string.Empty);

                    if (network != null)
                    {
                        networks.Add(network.Handle);
                    }
                }
            }
        }

        private static void FollowSpace(
            CadDocument document,
            CadSpace space,
            ISet<string> edited,
            ISet<string> erased,
            List<CadEditRecord> followed
        )
        {
            Work work = Open(document, space, true);

            if (work.Set.Refusal != null || work.Set.IsEmpty)
            {
                return;
            }

            // An object erased takes its constraints with it, and the dimensions of the dimensional ones.
            bool taken = false;

            foreach (string handle in erased)
            {
                taken |= RemoveOn(work, handle, followed);
            }

            var mine = new HashSet<string>(work.Set.Handles(), StringComparer.OrdinalIgnoreCase);
            var pinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string handle in edited)
            {
                if (mine.Contains(handle))
                {
                    pinned.Add(handle);
                }
            }

            if (pinned.Count == 0 && !taken)
            {
                return;
            }

            foreach (string handle in work.Set.Handles())
            {
                if (!work.Take(handle))
                {
                    // An object that is no longer one constraints can be tied to (it was made an arc segment): its constraints go.
                    taken |= RemoveOn(work, handle, followed);
                }
            }

            if (work.Set.IsEmpty)
            {
                Write(work);
                return;
            }

            CadSolveResult result = Solve(work, pinned, null);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(CannotSatisfy);
            }

            foreach (CadEditRecord record in Moves(work, result))
            {
                if (!pinned.Contains(record.Handle))
                {
                    followed.Add(
                        new CadEditRecord(CadEditRecordKind.Reshaped, record.Handle, OwnProperty) { Space = space.Name }
                    );
                }
            }

            Commit(work, result);
        }

        /// <summary>Takes the constraints on an object out of the work, and the dimensions of the dimensional ones; true where there were any.</summary>
        private static bool RemoveOn(Work work, string handle, List<CadEditRecord> followed)
        {
            bool any = false;

            for (int i = work.Set.Geometric.Count - 1; i >= 0; i--)
            {
                if (
                    work
                        .Set.Geometric[i]
                        .Refs.Exists(r => string.Equals(r.Handle, handle, StringComparison.OrdinalIgnoreCase))
                )
                {
                    work.Set.Geometric.RemoveAt(i);
                    any = true;
                }
            }

            for (int i = work.Set.Dimensional.Count - 1; i >= 0; i--)
            {
                CadDimConstraint constraint = work.Set.Dimensional[i];

                if (constraint.Refs.Exists(r => string.Equals(r.Handle, handle, StringComparison.OrdinalIgnoreCase)))
                {
                    work.Set.Dimensional.RemoveAt(i);
                    EraseDimension(work, constraint, followed);
                    DropParameter(work, constraint.Name);
                    any = true;
                }
            }

            return any;
        }

        /// <summary>Erases the dimension of a dimensional constraint that is taken away.</summary>
        internal static void EraseDimension(Work work, CadDimConstraint constraint, List<CadEditRecord> records)
        {
            if (constraint.DimensionHandle != null && work.Entity(constraint.DimensionHandle) is DxfEntity dimension)
            {
                string space = work.Space.Name;
                dimension.Erase();
                records?.Add(new CadEditRecord(CadEditRecordKind.Erased, constraint.DimensionHandle) { Space = space });
            }
        }

        /// <summary>Takes a parameter out of the set unless some other dimensional constraint still measures it or an expression names it.</summary>
        internal static void DropParameter(Work work, string name)
        {
            CadParameter parameter = work.Set.Parameter(name);

            if (parameter == null)
            {
                return;
            }

            if (work.Set.Dimensional.Exists(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            work.Set.Parameters.Remove(parameter);
        }

        #endregion

        /// <summary>Writes a length as the history of the command line writes one: to four places.</summary>
        internal static string Text(double value) =>
            (Math.Abs(value) < 5E-5 ? 0.0 : value).ToString("0.0000", CultureInfo.InvariantCulture);
    }
}
