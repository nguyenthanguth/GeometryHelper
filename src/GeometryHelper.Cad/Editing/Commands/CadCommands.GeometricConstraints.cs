using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The geometric constraints of AutoCAD 2021's Parametric tab, asked as it asks them (the prompts read from its console):
    /// GCCOINCIDENT, GCCOLLINEAR, GCCONCENTRIC, GCFIX, GCPARALLEL, GCPERPENDICULAR, GCHORIZONTAL, GCVERTICAL, GCTANGENT,
    /// GCSYMMETRIC and GCEQUAL, GEOMCONSTRAINT which asks which, and GCSMOOTH (the presenter has no splines to smooth). A
    /// constraint is applied by moving the objects to meet it, the object picked first held (the second moves to it, as
    /// AutoCAD's does), and is one undo step. Where no place meets it with the constraints there already, nothing changes and
    /// the history says "Constraints cannot be satisfied."
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddGeometricConstraintsTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("GCCOINCIDENT", GcCoincident));
            registry.Add(new CadCommand("GCCOLLINEAR", GcCollinear));
            registry.Add(new CadCommand("GCCONCENTRIC", GcConcentric));
            registry.Add(new CadCommand("GCFIX", GcFix));
            registry.Add(new CadCommand("GCPARALLEL", GcParallel));
            registry.Add(new CadCommand("GCPERPENDICULAR", GcPerpendicular));
            registry.Add(new CadCommand("GCHORIZONTAL", c => GcLevel(c, true)));
            registry.Add(new CadCommand("GCVERTICAL", c => GcLevel(c, false)));
            registry.Add(new CadCommand("GCTANGENT", GcTangent));
            registry.Add(new CadCommand("GCSMOOTH", GcSmooth));
            registry.Add(new CadCommand("GCSYMMETRIC", GcSymmetric));
            registry.Add(new CadCommand("GCEQUAL", GcEqual));
            registry.Add(new CadCommand("GEOMCONSTRAINT", GeomConstraint));
        }

        #region Picking

        /// <summary>The work on the constraints of the working space, or null with the reason written where the drawing cannot hold them.</summary>
        private static CadConstraints.Work ConstraintWork(CadCommandContext c)
        {
            // The bars of what the last constraint command made show until this one starts.
            c.Document.ConstraintBars.EndCommand();

            if (!CadConstraints.CanHold(c.Database))
            {
                c.Write(CadConstraints.CannotHold);
                return null;
            }

            CadConstraints.Work work = CadConstraints.Open(c.Document, c.Space);

            if (work.Set.Refusal != null)
            {
                c.Write(work.Set.Refusal);
                return null;
            }

            return work;
        }

        /// <summary>
        /// The reference the object picked gives: the object whole, or the point of it nearest where it was picked; false, with
        /// the reason written, for an object constraints cannot be tied to.
        /// </summary>
        private static bool PickedRef(CadCommandContext c, CadConstraints.Work work, bool point, out CadRef reference)
        {
            reference = null;
            string handle = c.Input.Handles[0];
            DxfEntity entity = c.Document.FindEntity(handle);

            if (entity == null || !work.Take(entity.Handle))
            {
                c.Write("Invalid selection. Select a line, circle, arc or straight polyline segment.");
                return false;
            }

            CadShape shape = work.Shapes[entity.Handle];
            GeoPoint2? at = c.Input.IsPickedAt ? c.Input.Point : (GeoPoint2?)null;
            int segment = -1;

            if (shape.Kind == CadShapeKind.Polyline)
            {
                segment = SegmentNear(shape, at);
            }

            if (!point)
            {
                reference = new CadRef(entity.Handle, segment, CadPart.Whole);
                return true;
            }

            List<KeyValuePair<CadPart, GeoPoint2>> parts = CadConstraintShapes.PartsOf(shape, segment);
            KeyValuePair<CadPart, GeoPoint2> best = parts[0];

            if (at.HasValue)
            {
                double least = double.MaxValue;

                foreach (KeyValuePair<CadPart, GeoPoint2> part in parts)
                {
                    double d = (part.Value - at.Value).Length;

                    if (d < least)
                    {
                        least = d;
                        best = part;
                    }
                }
            }

            reference = new CadRef(entity.Handle, segment, best.Key);
            return true;
        }

        /// <summary>The segment of a polyline nearest a point; the first where none is given.</summary>
        private static int SegmentNear(CadShape shape, GeoPoint2? at)
        {
            if (!at.HasValue)
            {
                return 0;
            }

            int best = 0;
            double least = double.MaxValue;

            for (int i = 0; i < shape.Segments; i++)
            {
                GeoPoint2 a = CadConstraintShapes.PointOf(shape, new CadRef(shape.Handle, i, CadPart.Start)).Value;
                GeoPoint2 b = CadConstraintShapes.PointOf(shape, new CadRef(shape.Handle, i, CadPart.End)).Value;
                double d = GcDistanceToSegment(a, b, at.Value);

                if (d < least - 1E-12)
                {
                    least = d;
                    best = i;
                }
            }

            return best;
        }

        private static double GcDistanceToSegment(GeoPoint2 a, GeoPoint2 b, GeoPoint2 p)
        {
            GeoVector2 ab = b - a;
            double len2 = ab.X * ab.X + ab.Y * ab.Y;
            double t = len2 <= 0.0 ? 0.0 : Math.Max(0.0, Math.Min(1.0, ((p - a).X * ab.X + (p - a).Y * ab.Y) / len2));
            GeoPoint2 foot = a + ab * t;
            return (p - foot).Length;
        }

        private static bool IsLine(CadConstraints.Work work, CadRef r) =>
            r != null
            && !r.IsPoint
            && (work.Shapes[r.Handle].Kind == CadShapeKind.Line || work.Shapes[r.Handle].Kind == CadShapeKind.Polyline);

        private static bool IsRound(CadConstraints.Work work, CadRef r) =>
            r != null
            && !r.IsPoint
            && (work.Shapes[r.Handle].Kind == CadShapeKind.Circle || work.Shapes[r.Handle].Kind == CadShapeKind.Arc);

        private static readonly string[] LineWords = { "Select a line segment or polyline segment." };

        /// <summary>
        /// Applies a constraint: the objects moved to meet it (the first picked held), the constraint written; one undo step.
        /// False, with the reason written and nothing changed, where it cannot be met or only repeats the constraints there are.
        /// </summary>
        private static bool ApplyGeometric(CadCommandContext c, CadConstraints.Work work, CadGeoConstraint constraint)
        {
            string anchor = constraint.Refs.Count > 0 ? constraint.Refs[0].Handle : null;
            var held =
                constraint.Kind == CadGeoKind.Symmetric
                    ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { constraint.Refs[2].Handle }
                    : null;
            CadSolveResult before = CadConstraints.Solve(work, held, anchor);

            if (!before.Succeeded)
            {
                c.Write(before.Message ?? CadConstraints.CannotSatisfy);
                return false;
            }

            ShapeSide(work, constraint);
            work.Set.Geometric.Add(constraint);
            CadSolveResult result = CadConstraints.Solve(work, held, anchor);

            if (!result.Succeeded)
            {
                work.Set.Geometric.Remove(constraint);
                c.Write(result.Message ?? CadConstraints.CannotSatisfy);
                return false;
            }

            if (result.Equations - result.Rank > before.Equations - before.Rank)
            {
                work.Set.Geometric.Remove(constraint);
                c.Write("The constraint would over-constrain the objects: the others already hold them so.");
                return false;
            }

            List<CadEditRecord> planned = CadConstraints.Moves(work, result);

            if (planned.Count == 0)
            {
                planned.Add(
                    new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        constraint.Refs[0].Handle,
                        CadConstraints.OwnProperty
                    )
                );
            }

            CadEditResult applied = c.Apply(planned, s => CadConstraints.Commit(work, result));

            if (!applied.Succeeded)
            {
                c.Write(applied.Message);
                return false;
            }

            c.Document.ConstraintBars.Added(constraint);
            return true;
        }

        /// <summary>Makes a tangent remember the side it was made on, and a fix the place it holds, from where the objects stand.</summary>
        private static void ShapeSide(CadConstraints.Work work, CadGeoConstraint constraint)
        {
            if (constraint.Kind == CadGeoKind.Fix)
            {
                GeoPoint2? p = CadConstraintShapes.PointOf(work.Shapes[constraint.Refs[0].Handle], constraint.Refs[0]);

                if (p.HasValue)
                {
                    constraint.Place = new[] { p.Value.X, p.Value.Y };
                }

                return;
            }

            if (constraint.Kind != CadGeoKind.Tangent)
            {
                return;
            }

            CadShape a = work.Shapes[constraint.Refs[0].Handle];
            CadShape b = work.Shapes[constraint.Refs[1].Handle];
            bool aLine = a.Kind == CadShapeKind.Line || a.Kind == CadShapeKind.Polyline;
            bool bLine = b.Kind == CadShapeKind.Line || b.Kind == CadShapeKind.Polyline;

            if (aLine || bLine)
            {
                CadRef lineRef = aLine ? constraint.Refs[0] : constraint.Refs[1];
                CadShape line = aLine ? a : b;
                CadShape round = aLine ? b : a;
                GeoPoint2 p = CadConstraintShapes.PointOf(line, lineRef.Of(CadPart.Start)).Value;
                GeoPoint2 q = CadConstraintShapes.PointOf(line, lineRef.Of(CadPart.End)).Value;
                double across = (q.X - p.X) * (round.Values[1] - p.Y) - (q.Y - p.Y) * (round.Values[0] - p.X);
                constraint.Side = across >= 0.0 ? 1 : -1;
                return;
            }

            double dist = Math.Sqrt(
                (a.Values[0] - b.Values[0]) * (a.Values[0] - b.Values[0])
                    + (a.Values[1] - b.Values[1]) * (a.Values[1] - b.Values[1])
            );
            double outside = Math.Abs(dist - (a.Values[2] + b.Values[2]));
            double inside = Math.Abs(dist - Math.Abs(a.Values[2] - b.Values[2]));
            constraint.Side = outside <= inside ? 1 : (a.Values[2] >= b.Values[2] ? -1 : -2);
        }

        #endregion

        #region Commands

        private static IEnumerable<CadPrompt> GcCoincident(CadCommandContext c)
        {
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            yield return CadPrompt.ForEntity("Select first point", new[] { "Object", "Autoconstrain" }, "Object");

            if (c.Input.Is("Autoconstrain"))
            {
                foreach (CadPrompt p in AutoConstrainBody(c, work, CadConstraintSettings.OnlyCoincident))
                {
                    yield return p;
                }

                yield break;
            }

            if (c.Input.IsNone || c.Input.Is("Object"))
            {
                CadRef curve;

                while (true)
                {
                    yield return CadPrompt.ForEntity("Select object");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    if (PickedRef(c, work, false, out curve))
                    {
                        break;
                    }
                }

                CadRef onIt;

                while (true)
                {
                    yield return CadPrompt.ForEntity("Select point");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    if (PickedRef(c, work, true, out onIt))
                    {
                        break;
                    }
                }

                if (onIt.IsPoint)
                {
                    ApplyGeometric(c, work, new CadGeoConstraint(CadGeoKind.PointOnCurve, new[] { onIt, curve }));
                }

                yield break;
            }

            if (c.Input.Kind != CadInputKind.Selection || !PickedRef(c, work, true, out CadRef first))
            {
                yield break;
            }

            while (true)
            {
                yield return CadPrompt.ForEntity("Select second point", new[] { "Object" }, "Object");

                if (c.Input.Is("Object") || c.Input.IsNone)
                {
                    yield return CadPrompt.ForEntity("Select object");

                    if (c.Input.Kind == CadInputKind.Selection && PickedRef(c, work, false, out CadRef curve2))
                    {
                        ApplyGeometric(c, work, new CadGeoConstraint(CadGeoKind.PointOnCurve, new[] { first, curve2 }));
                        yield break;
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (PickedRef(c, work, true, out CadRef second))
                {
                    if (first.Equals(second))
                    {
                        c.Write("Invalid selection. Select a second point.");
                        continue;
                    }

                    ApplyGeometric(c, work, new CadGeoConstraint(CadGeoKind.Coincident, new[] { first, second }));
                    yield break;
                }
            }
        }

        /// <summary>Asks for two objects of kinds a test says are right, and applies the constraint to them.</summary>
        private static IEnumerable<CadPrompt> Pair(
            CadCommandContext c,
            CadGeoKind kind,
            string name,
            Func<CadConstraints.Work, CadRef, bool> fits,
            string rule
        )
        {
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            CadRef first;

            while (true)
            {
                yield return CadPrompt.ForEntity("Select first object");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (!PickedRef(c, work, false, out first))
                {
                    continue;
                }

                if (fits(work, first))
                {
                    break;
                }

                c.Write("Invalid selection for " + name + ". " + rule);
            }

            while (true)
            {
                yield return CadPrompt.ForEntity("Select second object");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (!PickedRef(c, work, false, out CadRef second))
                {
                    continue;
                }

                if (!fits(work, second))
                {
                    c.Write("Invalid selection for " + name + ". " + rule);
                    continue;
                }

                if (first.SameObject(second))
                {
                    c.Write("Invalid selection for " + name + ". Select a second object.");
                    continue;
                }

                ApplyGeometric(c, work, new CadGeoConstraint(kind, new[] { first, second }));
                yield break;
            }
        }

        private static IEnumerable<CadPrompt> GcParallel(CadCommandContext c) =>
            Pair(c, CadGeoKind.Parallel, "Parallel", IsLine, "Select a line segment or polyline segment.");

        private static IEnumerable<CadPrompt> GcPerpendicular(CadCommandContext c) =>
            Pair(c, CadGeoKind.Perpendicular, "Perpendicular", IsLine, "Select a line segment or polyline segment.");

        private static IEnumerable<CadPrompt> GcConcentric(CadCommandContext c) =>
            Pair(c, CadGeoKind.Concentric, "Concentric", IsRound, "Select a circle or arc.");

        private static IEnumerable<CadPrompt> GcTangent(CadCommandContext c) =>
            Pair(
                c,
                CadGeoKind.Tangent,
                "Tangent",
                (w, r) => IsLine(w, r) || IsRound(w, r),
                "Select a line, circle or arc."
            );

        private static IEnumerable<CadPrompt> GcSmooth(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select first spline curve");

            if (c.Input.Kind == CadInputKind.Selection)
            {
                c.Write(
                    "Invalid selection for Smooth. Select a spline curve: the presenter does not solve splines yet."
                );
            }
        }

        private static IEnumerable<CadPrompt> GcCollinear(CadCommandContext c) =>
            Multi(c, CadGeoKind.Collinear, "Collinear", IsLine, "Select a line segment or polyline segment.");

        private static IEnumerable<CadPrompt> GcEqual(CadCommandContext c)
        {
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            foreach (
                CadPrompt p in MultiOn(
                    c,
                    work,
                    null,
                    "Equal",
                    (w, r) => IsLine(w, r) || IsRound(w, r),
                    "Select a line, circle or arc."
                )
            )
            {
                yield return p;
            }
        }

        private static IEnumerable<CadPrompt> Multi(
            CadCommandContext c,
            CadGeoKind kind,
            string name,
            Func<CadConstraints.Work, CadRef, bool> fits,
            string rule
        )
        {
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            foreach (CadPrompt p in MultiOn(c, work, kind, name, fits, rule))
            {
                yield return p;
            }
        }

        /// <summary>
        /// Collinear and Equal: a first object and a second, or with Multiple a first and then as many others as are picked
        /// until Enter, each made as the first.
        /// </summary>
        private static IEnumerable<CadPrompt> MultiOn(
            CadCommandContext c,
            CadConstraints.Work work,
            CadGeoKind? kind,
            string name,
            Func<CadConstraints.Work, CadRef, bool> fits,
            string rule
        )
        {
            bool multiple = false;
            CadRef first = null;

            while (first == null)
            {
                yield return CadPrompt.ForEntity("Select first object", multiple ? null : new[] { "Multiple" });

                if (c.Input.Is("Multiple"))
                {
                    multiple = true;
                    continue;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (!PickedRef(c, work, false, out CadRef picked))
                {
                    continue;
                }

                if (!fits(work, picked))
                {
                    c.Write("Invalid selection for " + name + ". " + rule);
                    continue;
                }

                first = picked;
            }

            while (true)
            {
                yield return CadPrompt.ForEntity(multiple ? "Select object to make equal" : "Select second object");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (!PickedRef(c, work, false, out CadRef second))
                {
                    continue;
                }

                if (!fits(work, second) || first.SameObject(second))
                {
                    c.Write(
                        "Invalid selection for "
                            + name
                            + ". "
                            + (first.SameObject(second) ? "Select a second object." : rule)
                    );
                    continue;
                }

                CadGeoKind made = kind ?? (IsRound(work, first) ? CadGeoKind.EqualRadius : CadGeoKind.EqualLength);

                if ((made == CadGeoKind.EqualRadius) != IsRound(work, second) && kind == null)
                {
                    c.Write("Invalid selection for " + name + ". Select an object of the kind of the first.");
                    continue;
                }

                ApplyGeometric(c, work, new CadGeoConstraint(made, new[] { first, second }));

                if (!multiple)
                {
                    yield break;
                }

                // The work is read again: the objects moved.
                work = ConstraintWork(c);

                if (work == null || !work.Take(first.Handle))
                {
                    yield break;
                }
            }
        }

        private static IEnumerable<CadPrompt> GcFix(CadCommandContext c)
        {
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            yield return CadPrompt.ForEntity("Select point", new[] { "Object" }, "Object");

            if (c.Input.IsNone || c.Input.Is("Object"))
            {
                while (true)
                {
                    yield return CadPrompt.ForEntity("Select object");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    if (!PickedRef(c, work, false, out CadRef whole))
                    {
                        continue;
                    }

                    // An object is held by its defining points: the ends of a line, a segment or an arc, the centre of a circle.
                    CadShape shape = work.Shapes[whole.Handle];
                    var parts = new List<CadPart>();

                    switch (shape.Kind)
                    {
                        case CadShapeKind.Circle:
                            parts.Add(CadPart.Center);
                            break;
                        case CadShapeKind.Arc:
                            parts.AddRange(new[] { CadPart.Center, CadPart.Start, CadPart.End });
                            break;
                        default:
                            parts.AddRange(new[] { CadPart.Start, CadPart.End });
                            break;
                    }

                    foreach (CadPart part in parts)
                    {
                        ApplyGeometric(c, work, new CadGeoConstraint(CadGeoKind.Fix, new[] { whole.Of(part) }));
                        work = ConstraintWork(c);

                        if (work == null || !work.Take(whole.Handle))
                        {
                            yield break;
                        }
                    }

                    yield break;
                }
            }

            if (c.Input.Kind == CadInputKind.Selection && PickedRef(c, work, true, out CadRef point))
            {
                ApplyGeometric(c, work, new CadGeoConstraint(CadGeoKind.Fix, new[] { point }));
            }
        }

        private static IEnumerable<CadPrompt> GcLevel(CadCommandContext c, bool horizontal)
        {
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            string name = horizontal ? "Horizontal" : "Vertical";
            CadGeoKind kind = horizontal ? CadGeoKind.Horizontal : CadGeoKind.Vertical;

            while (true)
            {
                yield return CadPrompt.ForEntity("Select an object", new[] { "2Points" }, "2Points");

                if (c.Input.Kind == CadInputKind.Selection)
                {
                    if (!PickedRef(c, work, false, out CadRef line))
                    {
                        continue;
                    }

                    if (!IsLine(work, line))
                    {
                        c.Write("Invalid selection for " + name + ". Select a line segment or polyline segment.");
                        continue;
                    }

                    ApplyGeometric(c, work, new CadGeoConstraint(kind, new[] { line }));
                    yield break;
                }

                break;
            }

            if (!c.Input.IsNone && !c.Input.Is("2Points"))
            {
                yield break;
            }

            CadRef first = null;

            while (first == null)
            {
                yield return CadPrompt.ForEntity("Select first point");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                PickedRef(c, work, true, out first);
            }

            while (true)
            {
                yield return CadPrompt.ForEntity("Select second point");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (PickedRef(c, work, true, out CadRef second))
                {
                    if (first.Equals(second))
                    {
                        c.Write("Invalid selection for " + name + ". Select a second point.");
                        continue;
                    }

                    ApplyGeometric(c, work, new CadGeoConstraint(kind, new[] { first, second }));
                    yield break;
                }
            }
        }

        private static IEnumerable<CadPrompt> GcSymmetric(CadCommandContext c)
        {
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            yield return CadPrompt.ForEntity("Select first object", new[] { "2Points" }, "2Points");

            CadRef first = null;
            CadRef second = null;

            if (c.Input.Kind == CadInputKind.Selection)
            {
                if (!PickedRef(c, work, false, out first))
                {
                    yield break;
                }

                while (second == null)
                {
                    yield return CadPrompt.ForEntity("Select second object");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    if (PickedRef(c, work, false, out CadRef picked))
                    {
                        if (picked.SameObject(first) || (IsLine(work, first) != IsLine(work, picked)))
                        {
                            c.Write(
                                "Invalid selection for Symmetric. Select a second object of the kind of the first."
                            );
                            continue;
                        }

                        second = picked;
                    }
                }
            }
            else if (c.Input.IsNone || c.Input.Is("2Points"))
            {
                while (first == null)
                {
                    yield return CadPrompt.ForEntity("Select first point");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    PickedRef(c, work, true, out first);
                }

                while (second == null)
                {
                    yield return CadPrompt.ForEntity("Select second point");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    if (PickedRef(c, work, true, out CadRef picked) && !picked.Equals(first))
                    {
                        second = picked;
                    }
                }
            }
            else
            {
                yield break;
            }

            while (true)
            {
                yield return CadPrompt.ForEntity("Select symmetry line");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (!PickedRef(c, work, false, out CadRef axis))
                {
                    continue;
                }

                if (!IsLine(work, axis) || axis.SameObject(first) || axis.SameObject(second))
                {
                    c.Write("Invalid selection for Symmetric. Select a line segment as the symmetry line.");
                    continue;
                }

                ApplyGeometric(c, work, new CadGeoConstraint(CadGeoKind.Symmetric, new[] { first, second, axis }));
                yield break;
            }
        }

        private static IEnumerable<CadPrompt> GeomConstraint(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword(
                "Enter constraint type",
                new[]
                {
                    "Horizontal",
                    "Vertical",
                    "Perpendicular",
                    "PArallel",
                    "Tangent",
                    "SMooth",
                    "Coincident",
                    "CONcentric",
                    "COLlinear",
                    "Symmetric",
                    "Equal",
                    "Fix",
                },
                "Coincident"
            );

            string chosen = c.Input.IsNone ? "Coincident" : c.Input.Text;
            IEnumerable<CadPrompt> body;

            switch (chosen)
            {
                case "Horizontal":
                    body = GcLevel(c, true);
                    break;
                case "Vertical":
                    body = GcLevel(c, false);
                    break;
                case "Perpendicular":
                    body = GcPerpendicular(c);
                    break;
                case "PArallel":
                    body = GcParallel(c);
                    break;
                case "Tangent":
                    body = GcTangent(c);
                    break;
                case "SMooth":
                    body = GcSmooth(c);
                    break;
                case "CONcentric":
                    body = GcConcentric(c);
                    break;
                case "COLlinear":
                    body = GcCollinear(c);
                    break;
                case "Symmetric":
                    body = GcSymmetric(c);
                    break;
                case "Equal":
                    body = GcEqual(c);
                    break;
                case "Fix":
                    body = GcFix(c);
                    break;
                default:
                    body = GcCoincident(c);
                    break;
            }

            foreach (CadPrompt prompt in body)
            {
                yield return prompt;
            }
        }

        #endregion
    }
}
