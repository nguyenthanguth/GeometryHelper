using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// AUTOCONSTRAIN (the constraints a selection already nearly meets, within the distance and angle of the AutoConstrain tab,
    /// applied in the order of its list), CONSTRAINTBAR (Show, Hide, Reset), DELCONSTRAINT, CONSTRAINTSETTINGS (the dialog's
    /// three tabs) and the variables behind them: CONSTRAINTBARMODE, CONSTRAINTBARDISPLAY, CONSTRAINTINFER,
    /// CONSTRAINTNAMEFORMAT, CCONSTRAINTFORM, DYNCONSTRAINTMODE, CONSTRAINTSOLVEMODE, PARAMETERCOPYMODE, PARAMETERSSTATUS
    /// and CONSTRAINTRELAX.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddConstraintToolsTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("AUTOCONSTRAIN", AutoConstrain));
            registry.Add(new CadCommand("CONSTRAINTBAR", ConstraintBarCommand));
            registry.Add(new CadCommand("DELCONSTRAINT", DelConstraint));
            registry.Add(new CadCommand("CONSTRAINTSETTINGS", ConstraintSettingsCommand));
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "CONSTRAINTBARMODE",
                    false,
                    0,
                    4095,
                    s => s.Constraints.BarMode,
                    (c, v) => c.Session.Constraints.BarMode = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "CONSTRAINTBARDISPLAY",
                    false,
                    0,
                    3,
                    s => s.Constraints.BarDisplay,
                    (c, v) => c.Session.Constraints.BarDisplay = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "CONSTRAINTINFER",
                    false,
                    0,
                    1,
                    s => s.Constraints.Infer ? 1 : 0,
                    (c, v) => c.Session.Constraints.Infer = v != 0
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "CONSTRAINTNAMEFORMAT",
                    false,
                    0,
                    2,
                    s => s.Constraints.NameFormat,
                    (c, v) => c.Session.Constraints.NameFormat = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "CCONSTRAINTFORM",
                    false,
                    0,
                    1,
                    s => s.Constraints.Form,
                    (c, v) => c.Session.Constraints.Form = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "DYNCONSTRAINTMODE",
                    false,
                    0,
                    1,
                    s => s.Constraints.DynamicMode,
                    (c, v) => c.Session.Constraints.DynamicMode = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "CONSTRAINTSOLVEMODE",
                    false,
                    0,
                    3,
                    s => s.Constraints.SolveMode,
                    (c, v) => c.Session.Constraints.SolveMode = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "PARAMETERCOPYMODE",
                    false,
                    0,
                    3,
                    s => s.Constraints.CopyMode,
                    (c, v) => c.Session.Constraints.CopyMode = v
                )
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly(
                    "PARAMETERSSTATUS",
                    CadVariableKind.Integer,
                    s => s.ParametersShown ? "1" : "0",
                    s => new[] { s.ParametersShown ? 1.0 : 0.0 }
                )
            );
            registry.AddVariable(
                CadSystemVariable.ReadOnly("CONSTRAINTRELAX", CadVariableKind.Integer, s => "0", s => new[] { 0.0 })
            );
        }

        #region AUTOCONSTRAIN

        private static IEnumerable<CadPrompt> AutoConstrain(CadCommandContext c)
        {
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            foreach (CadPrompt p in AutoConstrainBody(c, work, null))
            {
                yield return p;
            }
        }

        /// <summary>
        /// Asks for objects (or Settings, which opens the dialog on its AutoConstrain tab and asks again) and applies the
        /// constraints they nearly meet, those of the types given only where some are.
        /// </summary>
        internal static IEnumerable<CadPrompt> AutoConstrainBody(
            CadCommandContext c,
            CadConstraints.Work work,
            CadConstraintType[] only
        )
        {
            while (true)
            {
                yield return only == null
                    ? CadPrompt.ForSelection("Select objects", new[] { "Settings" })
                    : CadPrompt.ForSelection("Select objects");

                if (c.Input.Is("Settings"))
                {
                    var request = new CadConstraintSettingsRequest(c.Session.Constraints.Clone(), 2);

                    if (
                        c.Session.Request("CONSTRAINTSETTINGS", c.Document, request)
                        && request.Action == CadDialogAction.Ok
                    )
                    {
                        c.Session.Constraints = request.Settings;
                    }

                    continue;
                }

                break;
            }

            if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
            {
                yield break;
            }

            var handles = new List<string>();

            foreach (string handle in c.Input.Handles)
            {
                if (work.Take(handle))
                {
                    handles.Add(handle);
                }
            }

            if (handles.Count == 0)
            {
                c.Write("No object that constraints can be applied to was selected.");
                yield break;
            }

            AutoConstrainRun(c, work, handles, only);
        }

        private static void AutoConstrainRun(
            CadCommandContext c,
            CadConstraints.Work work,
            List<string> handles,
            CadConstraintType[] only
        )
        {
            CadConstraintSettings settings = c.Session.Constraints;
            var types = new List<CadConstraintType>();

            foreach (CadAutoConstrainRule rule in settings.AutoRules)
            {
                if (rule.Enabled && (only == null || Array.IndexOf(only, rule.Type) >= 0))
                {
                    types.Add(rule.Type);
                }
            }

            handles.Sort(StringComparer.OrdinalIgnoreCase);
            var units = new List<CadRef>();

            foreach (string handle in handles)
            {
                CadShape shape = work.Shapes[handle];

                if (shape.Kind == CadShapeKind.Polyline)
                {
                    for (int i = 0; i < shape.Segments; i++)
                    {
                        units.Add(new CadRef(handle, i, CadPart.Whole));
                    }
                }
                else
                {
                    units.Add(new CadRef(handle, -1, CadPart.Whole));
                }
            }

            double distance = settings.AutoDistance;
            double angle = settings.AutoAngle * Math.PI / 180.0;
            CadSolveResult last = null;
            int applied = 0;

            foreach (CadConstraintType type in types)
            {
                foreach (CadGeoConstraint candidate in Candidates(work, units, type, distance, angle, settings))
                {
                    if (Exists(work.Set, candidate))
                    {
                        continue;
                    }

                    CadSolveResult before = CadConstraints.Solve(work, null, candidate.Refs[0].Handle);
                    ShapeSide(work, candidate);
                    work.Set.Geometric.Add(candidate);
                    CadSolveResult result = CadConstraints.Solve(work, null, candidate.Refs[0].Handle);

                    if (
                        !result.Succeeded
                        || !before.Succeeded
                        || result.Equations - result.Rank > before.Equations - before.Rank
                    )
                    {
                        work.Set.Geometric.Remove(candidate);
                        continue;
                    }

                    // The objects stand where the solution puts them, for the constraints that come next.
                    foreach (KeyValuePair<string, double[]> pair in result.Values)
                    {
                        CadShape shape = work.Shapes[pair.Key];
                        work.Shapes[pair.Key] = new CadShape(pair.Key, shape.Kind, pair.Value, shape.Closed);
                    }

                    last = result;
                    applied++;
                    c.Document.ConstraintBars.Added(candidate);
                }
            }

            if (applied == 0)
            {
                c.Write("0 constraint(s) applied.");
                return;
            }

            // The original numbers decide what moved; the shapes were advanced while the constraints were tried.
            CadConstraints.Work fresh = CadConstraints.Open(c.Document, c.Space);
            var planned = new List<CadEditRecord>();

            foreach (string handle in work.Set.Handles())
            {
                CadShape now = work.Shapes[handle];
                CadShape was = CadConstraintShapes.Of(c.Document.FindEntity(handle));

                if (was != null && CadConstraintShapes.Differ(was.Values, now.Values))
                {
                    planned.Add(new CadEditRecord(CadEditRecordKind.Reshaped, handle, CadConstraints.OwnProperty));
                }
            }

            if (planned.Count == 0)
            {
                planned.Add(
                    new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        work.Set.Geometric[work.Set.Geometric.Count - 1].Refs[0].Handle,
                        CadConstraints.OwnProperty
                    )
                );
            }

            var solved = new CadSolveResult { Succeeded = true };

            foreach (string handle in work.Set.Handles())
            {
                solved.Values[handle] = work.Shapes[handle].Values;
            }

            // Written from the shapes the solution gave: the document's own numbers are put over them as they stand.
            foreach (string handle in work.Set.Handles())
            {
                CadShape was = CadConstraintShapes.Of(c.Document.FindEntity(handle));

                if (was != null)
                {
                    fresh.Shapes[handle] = was;
                }
            }

            fresh.Set.Geometric.Clear();
            fresh.Set.Geometric.AddRange(work.Set.Geometric);
            fresh.Set.Dimensional.Clear();
            fresh.Set.Dimensional.AddRange(work.Set.Dimensional);
            fresh.Set.Parameters.Clear();
            fresh.Set.Parameters.AddRange(work.Set.Parameters);
            c.Apply(planned, s => CadConstraints.Commit(fresh, solved));
            c.Write(applied.ToString(CultureInfo.InvariantCulture) + " constraint(s) applied.");
        }

        private static bool Exists(CadConstraintSet set, CadGeoConstraint candidate)
        {
            foreach (CadGeoConstraint have in set.Geometric)
            {
                if (have.Kind != candidate.Kind || have.Refs.Count != candidate.Refs.Count)
                {
                    continue;
                }

                bool same = true;
                bool swapped = candidate.Refs.Count == 2;

                for (int i = 0; i < have.Refs.Count; i++)
                {
                    same &= have.Refs[i].Equals(candidate.Refs[i]);
                    swapped &= have.Refs[i].Equals(candidate.Refs[candidate.Refs.Count - 1 - i]);
                }

                if (same || swapped)
                {
                    return true;
                }
            }

            return false;
        }

        private static GeoPoint2 AtOf(CadConstraints.Work work, CadRef r) =>
            CadConstraintShapes.PointOf(work.Shapes[r.Handle], r).Value;

        private static double Direction(CadConstraints.Work work, CadRef line)
        {
            GeoPoint2 p = AtOf(work, line.Of(CadPart.Start));
            GeoPoint2 q = AtOf(work, line.Of(CadPart.End));
            return Math.Atan2(q.Y - p.Y, q.X - p.X);
        }

        /// <summary>The angle between two directions as a line has them, from 0 to half a turn.</summary>
        private static double LineAngle(double a, double b)
        {
            double d = Math.Abs(a - b) % Math.PI;
            return d;
        }

        private static IEnumerable<CadGeoConstraint> Candidates(
            CadConstraints.Work work,
            List<CadRef> units,
            CadConstraintType type,
            double distance,
            double angle,
            CadConstraintSettings settings
        )
        {
            var lines = units.Where(u => IsLine(work, u)).ToList();
            var rounds = units.Where(u => IsRound(work, u)).ToList();

            switch (type)
            {
                case CadConstraintType.Horizontal:
                    foreach (CadRef line in lines)
                    {
                        double a = Direction(work, line);
                        double off = Math.Min(LineAngle(a, 0.0), Math.PI - LineAngle(a, 0.0));

                        if (off <= angle)
                        {
                            yield return new CadGeoConstraint(CadGeoKind.Horizontal, new[] { line });
                        }
                    }

                    break;
                case CadConstraintType.Vertical:
                    foreach (CadRef line in lines)
                    {
                        double a = Direction(work, line);
                        double off = Math.Min(LineAngle(a, Math.PI / 2.0), Math.PI - LineAngle(a, Math.PI / 2.0));

                        if (off <= angle)
                        {
                            yield return new CadGeoConstraint(CadGeoKind.Vertical, new[] { line });
                        }
                    }

                    break;
                case CadConstraintType.Parallel:
                case CadConstraintType.Perpendicular:
                case CadConstraintType.Collinear:
                    for (int i = 0; i < lines.Count; i++)
                    {
                        for (int j = i + 1; j < lines.Count; j++)
                        {
                            if (SameSegmentNeighbour(lines[i], lines[j]) && type != CadConstraintType.Collinear)
                            {
                                // Neighbours of one polyline meet at a corner: parallel or perpendicular there is the user's to choose.
                            }

                            double between = LineAngle(Direction(work, lines[i]), Direction(work, lines[j]));
                            double toParallel = Math.Min(between, Math.PI - between);
                            double toSquare = Math.Abs(between - Math.PI / 2.0);

                            if (type == CadConstraintType.Parallel && toParallel <= angle)
                            {
                                yield return new CadGeoConstraint(CadGeoKind.Parallel, new[] { lines[i], lines[j] });
                            }
                            else if (type == CadConstraintType.Perpendicular && toSquare <= angle)
                            {
                                if (!settings.PerpendicularAtIntersection || Meet(work, lines[i], lines[j], distance))
                                {
                                    yield return new CadGeoConstraint(
                                        CadGeoKind.Perpendicular,
                                        new[] { lines[i], lines[j] }
                                    );
                                }
                            }
                            else if (
                                type == CadConstraintType.Collinear
                                && toParallel <= angle
                                && FromLine(work, lines[i], AtOf(work, lines[j].Of(CadPart.Start))) <= distance
                                && FromLine(work, lines[i], AtOf(work, lines[j].Of(CadPart.End))) <= distance
                            )
                            {
                                yield return new CadGeoConstraint(CadGeoKind.Collinear, new[] { lines[i], lines[j] });
                            }
                        }
                    }

                    break;
                case CadConstraintType.Coincident:
                    for (int i = 0; i < units.Count; i++)
                    {
                        for (int j = i + 1; j < units.Count; j++)
                        {
                            foreach (CadPart a in EndsOf(work, units[i]))
                            {
                                foreach (CadPart b in EndsOf(work, units[j]))
                                {
                                    CadRef p = units[i].Of(a);
                                    CadRef q = units[j].Of(b);

                                    if (
                                        SameSegmentNeighbour(units[i], units[j])
                                        && JoinOf(work, units[i], a, units[j], b)
                                    )
                                    {
                                        continue;
                                    }

                                    if ((AtOf(work, p) - AtOf(work, q)).Length <= distance)
                                    {
                                        yield return new CadGeoConstraint(CadGeoKind.Coincident, new[] { p, q });
                                    }
                                }
                            }
                        }
                    }

                    break;
                case CadConstraintType.Concentric:
                    for (int i = 0; i < rounds.Count; i++)
                    {
                        for (int j = i + 1; j < rounds.Count; j++)
                        {
                            GeoPoint2 a = AtOf(work, rounds[i].Of(CadPart.Center));
                            GeoPoint2 b = AtOf(work, rounds[j].Of(CadPart.Center));

                            if ((a - b).Length <= distance)
                            {
                                yield return new CadGeoConstraint(
                                    CadGeoKind.Concentric,
                                    new[] { rounds[i], rounds[j] }
                                );
                            }
                        }
                    }

                    break;
                case CadConstraintType.Equal:
                    for (int i = 0; i < lines.Count; i++)
                    {
                        for (int j = i + 1; j < lines.Count; j++)
                        {
                            if (Math.Abs(Length(work, lines[i]) - Length(work, lines[j])) <= distance)
                            {
                                yield return new CadGeoConstraint(CadGeoKind.EqualLength, new[] { lines[i], lines[j] });
                            }
                        }
                    }

                    for (int i = 0; i < rounds.Count; i++)
                    {
                        for (int j = i + 1; j < rounds.Count; j++)
                        {
                            if (
                                Math.Abs(
                                    work.Shapes[rounds[i].Handle].Values[2] - work.Shapes[rounds[j].Handle].Values[2]
                                ) <= distance
                            )
                            {
                                yield return new CadGeoConstraint(
                                    CadGeoKind.EqualRadius,
                                    new[] { rounds[i], rounds[j] }
                                );
                            }
                        }
                    }

                    break;
                case CadConstraintType.Tangent:
                    foreach (CadRef line in lines)
                    {
                        foreach (CadRef round in rounds)
                        {
                            GeoPoint2 centre = AtOf(work, round.Of(CadPart.Center));
                            double r = work.Shapes[round.Handle].Values[2];
                            GeoPoint2 p = AtOf(work, line.Of(CadPart.Start));
                            GeoPoint2 q = AtOf(work, line.Of(CadPart.End));
                            double across = FromLine(work, line, centre);
                            double along = Along(p, q, centre);

                            if (
                                Math.Abs(across - r) <= distance
                                && along >= -distance
                                && along <= (q - p).Length + distance
                            )
                            {
                                yield return new CadGeoConstraint(CadGeoKind.Tangent, new[] { line, round });
                            }
                        }
                    }

                    for (int i = 0; i < rounds.Count; i++)
                    {
                        for (int j = i + 1; j < rounds.Count; j++)
                        {
                            double[] a = work.Shapes[rounds[i].Handle].Values;
                            double[] b = work.Shapes[rounds[j].Handle].Values;
                            double d = Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1]));

                            if (
                                d > distance
                                && (
                                    Math.Abs(d - (a[2] + b[2])) <= distance
                                    || Math.Abs(d - Math.Abs(a[2] - b[2])) <= distance
                                )
                            )
                            {
                                yield return new CadGeoConstraint(CadGeoKind.Tangent, new[] { rounds[i], rounds[j] });
                            }
                        }
                    }

                    break;
            }
        }

        private static IEnumerable<CadPart> EndsOf(CadConstraints.Work work, CadRef unit)
        {
            CadShape shape = work.Shapes[unit.Handle];

            switch (shape.Kind)
            {
                case CadShapeKind.Arc:
                    return new[] { CadPart.Start, CadPart.End };
                case CadShapeKind.Circle:
                    return Array.Empty<CadPart>();
                default:
                    return new[] { CadPart.Start, CadPart.End };
            }
        }

        private static bool SameSegmentNeighbour(CadRef a, CadRef b) =>
            a.Segment >= 0 && string.Equals(a.Handle, b.Handle, StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether two ends are the joint of two neighbouring segments of one polyline, which the polyline already holds together.</summary>
        private static bool JoinOf(CadConstraints.Work work, CadRef a, CadPart pa, CadRef b, CadPart pb)
        {
            CadShape shape = work.Shapes[a.Handle];
            int n = shape.Segments;
            bool following = pa == CadPart.End && pb == CadPart.Start && (a.Segment + 1) % n == b.Segment;
            bool preceding = pa == CadPart.Start && pb == CadPart.End && (b.Segment + 1) % n == a.Segment;
            return following || preceding;
        }

        private static double Length(CadConstraints.Work work, CadRef line) =>
            (AtOf(work, line.Of(CadPart.End)) - AtOf(work, line.Of(CadPart.Start))).Length;

        private static double FromLine(CadConstraints.Work work, CadRef line, GeoPoint2 point)
        {
            GeoPoint2 p = AtOf(work, line.Of(CadPart.Start));
            GeoPoint2 q = AtOf(work, line.Of(CadPart.End));
            GeoVector2 d = q - p;
            double len = d.Length;
            return len <= 0.0 ? (point - p).Length : Math.Abs(d.X * (point - p).Y - d.Y * (point - p).X) / len;
        }

        private static double Along(GeoPoint2 p, GeoPoint2 q, GeoPoint2 point)
        {
            GeoVector2 d = q - p;
            double len = d.Length;
            return len <= 0.0 ? 0.0 : (d.X * (point - p).X + d.Y * (point - p).Y) / len;
        }

        private static bool Meet(CadConstraints.Work work, CadRef a, CadRef b, double tolerance)
        {
            GeoPoint2 a0 = AtOf(work, a.Of(CadPart.Start));
            GeoPoint2 a1 = AtOf(work, a.Of(CadPart.End));
            GeoPoint2 b0 = AtOf(work, b.Of(CadPart.Start));
            GeoPoint2 b1 = AtOf(work, b.Of(CadPart.End));
            double best = Math.Min(
                Math.Min(GcDistanceToSegment(a0, a1, b0), GcDistanceToSegment(a0, a1, b1)),
                Math.Min(GcDistanceToSegment(b0, b1, a0), GcDistanceToSegment(b0, b1, a1))
            );

            if (best <= tolerance)
            {
                return true;
            }

            GeoVector2 u = a1 - a0;
            GeoVector2 v = b1 - b0;
            double cross = u.X * v.Y - u.Y * v.X;

            if (Math.Abs(cross) < 1E-12)
            {
                return false;
            }

            double s = ((b0 - a0).X * v.Y - (b0 - a0).Y * v.X) / cross;
            double t = ((b0 - a0).X * u.Y - (b0 - a0).Y * u.X) / cross;
            return s >= 0.0 && s <= 1.0 && t >= 0.0 && t <= 1.0;
        }

        #endregion

        #region CONSTRAINTBAR and DELCONSTRAINT

        private static IEnumerable<CadPrompt> ConstraintBarCommand(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection("Select objects");

            if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
            {
                yield break;
            }

            IReadOnlyList<string> handles = c.Input.Handles;
            yield return CadPrompt.ForKeyword("Enter an option", new[] { "Show", "Hide", "Reset" }, "Show");

            if (c.Input.Is("Hide"))
            {
                c.Document.ConstraintBars.HideBars(handles);
            }
            else if (c.Input.Is("Reset"))
            {
                c.Document.ConstraintBars.ResetBars(handles);
            }
            else
            {
                c.Document.ConstraintBars.ShowBars(handles);
            }
        }

        private static IEnumerable<CadPrompt> DelConstraint(CadCommandContext c)
        {
            c.Write("All constraints will be removed from selected objects");

            foreach (CadPrompt p in c.SelectObjects())
            {
                yield return p;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            int removed = 0;
            var erased = new List<CadEditRecord>();

            foreach (string handle in c.Selected)
            {
                for (int i = work.Set.Geometric.Count - 1; i >= 0; i--)
                {
                    if (
                        work
                            .Set.Geometric[i]
                            .Refs.Exists(r => string.Equals(r.Handle, handle, StringComparison.OrdinalIgnoreCase))
                    )
                    {
                        work.Set.Geometric.RemoveAt(i);
                        removed++;
                    }
                }

                for (int i = work.Set.Dimensional.Count - 1; i >= 0; i--)
                {
                    CadDimConstraint dim = work.Set.Dimensional[i];

                    if (dim.Refs.Exists(r => string.Equals(r.Handle, handle, StringComparison.OrdinalIgnoreCase)))
                    {
                        work.Set.Dimensional.RemoveAt(i);
                        CadConstraints.EraseDimension(work, dim, erased);
                        CadConstraints.DropParameter(work, dim.Name);
                        removed++;
                    }
                }
            }

            if (removed > 0)
            {
                var planned = new List<CadEditRecord>();

                foreach (string handle in c.Selected)
                {
                    planned.Add(
                        new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, CadConstraints.OwnProperty)
                    );
                }

                c.Apply(planned, s => CadConstraints.Write(work));
            }

            c.Write(removed.ToString(CultureInfo.InvariantCulture) + " constraint(s) removed");
        }

        #endregion

        private static IEnumerable<CadPrompt> ConstraintSettingsCommand(CadCommandContext c)
        {
            var request = new CadConstraintSettingsRequest(c.Session.Constraints.Clone(), 0);

            if (c.Session.Request("CONSTRAINTSETTINGS", c.Document, request))
            {
                if (request.Action == CadDialogAction.Ok)
                {
                    c.Session.Constraints = request.Settings;
                }
            }
            else
            {
                c.Write("The Constraint Settings dialog needs the window.");
            }

            yield break;
        }
    }
}
