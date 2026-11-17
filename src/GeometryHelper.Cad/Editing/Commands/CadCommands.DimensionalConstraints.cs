using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The dimensional constraints of AutoCAD 2021's Parametric tab, asked as it asks them: DCLINEAR, DCHORIZONTAL,
    /// DCVERTICAL, DCALIGNED, DCANGULAR, DCRADIUS and DCDIAMETER, DIMCONSTRAINT which asks which, DCCONVERT (an associative
    /// dimension made a constraint), DCFORM and DCDISPLAY; and -PARAMETERS with its New, Edit, Rename, Delete and ? options,
    /// PARAMETERS and PARAMETERSCLOSE for the palette. A dimensional constraint is a parameter with a name (d1, rad1, dia1,
    /// ang1), a value and the DIMENSION that shows <c>name=value</c>; a value typed moves the objects to it.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddDimensionalConstraintsTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("DCLINEAR", c => DcLinear(c, 'L')));
            registry.Add(new CadCommand("DCHORIZONTAL", c => DcLinear(c, 'H')));
            registry.Add(new CadCommand("DCVERTICAL", c => DcLinear(c, 'V')));
            registry.Add(new CadCommand("DCALIGNED", c => DcLinear(c, 'A')));
            registry.Add(new CadCommand("DCANGULAR", DcAngular));
            registry.Add(new CadCommand("DCRADIUS", c => DcRound(c, false)));
            registry.Add(new CadCommand("DCDIAMETER", c => DcRound(c, true)));
            registry.Add(new CadCommand("DIMCONSTRAINT", DimConstraintCommand));
            registry.Add(new CadCommand("DCCONVERT", DcConvert));
            registry.Add(new CadCommand("DCFORM", DcForm));
            registry.Add(new CadCommand("DCDISPLAY", DcDisplay, true));
            registry.Add(new CadCommand("-PARAMETERS", ParametersTyped));
            registry.Add(new CadCommand("PARAMETERS", ParametersPalette));
            registry.Add(new CadCommand("PARAMETERSCLOSE", ParametersClose));
        }

        private static string FormName(CadConstraintSettings settings) =>
            settings.Form == 0 ? "Dynamic" : "Annotational";

        #region Making a dimensional constraint

        /// <summary>The number the work holds a measurement as: a text with up to ten places, which AutoCAD writes into an expression.</summary>
        private static string PlainNumber(double value) =>
            Math.Round(value, 10).ToString("0.##########", CultureInfo.InvariantCulture);

        /// <summary>The value a constraint of a kind measures where the objects stand: a distance, an angle in degrees, a radius or a diameter.</summary>
        private static double Measured(CadConstraints.Work work, CadDimKind kind, List<CadRef> refs, out int direction)
        {
            direction = 1;

            switch (kind)
            {
                case CadDimKind.Horizontal:
                case CadDimKind.Vertical:
                {
                    GeoPoint2 p = AtOf(work, refs[0]);
                    GeoPoint2 q = AtOf(work, refs[1]);
                    double d = kind == CadDimKind.Horizontal ? q.X - p.X : q.Y - p.Y;
                    direction = d >= 0.0 ? 1 : -1;
                    return Math.Abs(d);
                }

                case CadDimKind.Aligned:
                    return (AtOf(work, refs[1]) - AtOf(work, refs[0])).Length;
                case CadDimKind.Angular:
                {
                    double a = Direction(work, refs[0]);
                    double b = Direction(work, refs[1]);
                    double turn = (b - a) % (2.0 * Math.PI);

                    if (turn < 0.0)
                    {
                        turn += 2.0 * Math.PI;
                    }

                    return turn * 180.0 / Math.PI;
                }

                case CadDimKind.Radius:
                    return work.Shapes[refs[0].Handle].Values[2];
                default:
                    return 2.0 * work.Shapes[refs[0].Handle].Values[2];
            }
        }

        /// <summary>The words an answer to the name prompt is read as: "name=expression", "name=" with a default expression, or an expression alone.</summary>
        private static bool ReadNameAnswer(
            string typed,
            string name,
            string defaultExpression,
            out string newName,
            out string expression
        )
        {
            newName = name;
            expression = defaultExpression;

            if (string.IsNullOrWhiteSpace(typed))
            {
                return true;
            }

            int equals = typed.IndexOf('=');

            if (equals >= 0)
            {
                string left = typed.Substring(0, equals).Trim();

                if (left.Length > 0)
                {
                    newName = left;
                }

                string right = typed.Substring(equals + 1).Trim();

                if (right.Length > 0)
                {
                    expression = right;
                }

                return true;
            }

            expression = typed.Trim();
            return true;
        }

        /// <summary>
        /// Makes a dimensional constraint from objects: the parameter named and valued, the constraint solved with the others (the
        /// objects moved where the value typed is not the one they measure), the DIMENSION that shows it drawn through
        /// <paramref name="location"/>, one undo step. False, with the reason written, where it cannot be.
        /// </summary>
        private static bool ApplyDimensional(
            CadCommandContext c,
            CadConstraints.Work work,
            CadDimKind kind,
            List<CadRef> refs,
            GeoPoint2 location,
            string typedName,
            string typedExpression
        )
        {
            CadConstraintSettings settings = c.Session.Constraints;
            string name = typedName ?? work.Set.NextName(CadDimConstraint.PrefixOf(kind));

            if (!CadConstraintExpression.IsName(name))
            {
                c.Write("\"" + name + "\" is not a name a parameter can have.");
                return false;
            }

            if (work.Set.Parameter(name) != null)
            {
                c.Write("There is a parameter " + name + " already.");
                return false;
            }

            double measured = Measured(work, kind, refs, out int direction);
            string anchor = refs[0].Handle;
            CadSolveResult before = CadConstraints.Solve(work, null, anchor);

            if (!before.Succeeded)
            {
                c.Write(before.Message ?? CadConstraints.CannotSatisfy);
                return false;
            }

            var constraint = new CadDimConstraint(kind, refs, name)
            {
                Direction = direction,
                Dynamic = settings.Form == 0,
                NameFormat = settings.NameFormat,
            };
            var parameter = new CadParameter(name, PlainNumber(measured), measured, false);
            work.Set.Parameters.Add(parameter);
            work.Set.Dimensional.Add(constraint);

            if (!string.IsNullOrWhiteSpace(typedExpression))
            {
                parameter.Expression = typedExpression;
            }

            if (!CadConstraints.Evaluate(work.Set, out string error))
            {
                Undo(work, constraint, parameter);
                c.Write(error);
                return false;
            }

            CadSolveResult result = CadConstraints.Solve(work, null, anchor);

            if (!result.Succeeded)
            {
                Undo(work, constraint, parameter);
                c.Write(result.Message ?? CadConstraints.CannotSatisfy);
                return false;
            }

            if (result.Equations - result.Rank > before.Equations - before.Rank)
            {
                Undo(work, constraint, parameter);
                c.Write("The dimension would over-constrain the objects: the others already hold them so.");
                return false;
            }

            List<CadEditRecord> planned = CadConstraints.Moves(work, result);

            if (planned.Count == 0)
            {
                planned.Add(
                    new CadEditRecord(CadEditRecordKind.PropertyChanged, refs[0].Handle, CadConstraints.OwnProperty)
                );
            }

            CadEditResult first = c.Apply(planned, s => CadConstraints.Commit(work, result));

            if (!first.Succeeded)
            {
                Undo(work, constraint, parameter);
                c.Write(first.Message);
                return false;
            }

            string handle = MakeConstraintDimension(c, work, constraint, parameter.Value, location);

            if (handle == null)
            {
                c.Write("*Invalid* The dimension measures nothing.");
                return true;
            }

            constraint.DimensionHandle = handle;
            c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, CadConstraints.OwnProperty) },
                s => CadConstraints.Write(work)
            );
            return true;
        }

        private static void Undo(CadConstraints.Work work, CadDimConstraint constraint, CadParameter parameter)
        {
            work.Set.Dimensional.Remove(constraint);
            work.Set.Parameters.Remove(parameter);
        }

        /// <summary>The prompt for the name and the value of a new dimensional constraint, which Enter takes as it stands.</summary>
        private static CadPrompt NamePrompt(CadConstraints.Work work, CadDimKind kind, double measured) =>
            CadPrompt.ForString(
                "Enter name or value",
                work.Set.NextName(CadDimConstraint.PrefixOf(kind)) + "=" + PlainNumber(measured)
            );

        #endregion

        #region Commands

        private static IEnumerable<CadPrompt> DcLinear(CadCommandContext c, char mode)
        {
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            CadRef first = null;
            CadRef second = null;
            CadPrompt ask = CadPrompt.ForEntity(
                "Specify first constraint point",
                mode == 'A' ? new[] { "Object", "Point & line", "2Lines" } : new[] { "Object" },
                "Object"
            );

            yield return ask;

            if (c.Input.Is("Point & line") || c.Input.Is("2Lines"))
            {
                c.Write("The presenter does not solve a distance to a line yet: pick two points, or an object.");
                yield break;
            }

            if (c.Input.IsNone || c.Input.Is("Object"))
            {
                CadRef line = null;

                while (line == null)
                {
                    yield return CadPrompt.ForEntity("Select object");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    if (PickedRef(c, work, false, out CadRef picked))
                    {
                        if (IsLine(work, picked))
                        {
                            line = picked;
                        }
                        else
                        {
                            c.Write("Invalid selection for Linear. Select a line segment or polyline segment.");
                        }
                    }
                }

                first = line.Of(CadPart.Start);
                second = line.Of(CadPart.End);
            }
            else if (c.Input.Kind == CadInputKind.Selection && PickedRef(c, work, true, out first))
            {
                while (second == null)
                {
                    yield return CadPrompt.ForEntity("Specify second constraint point");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    if (PickedRef(c, work, true, out CadRef picked))
                    {
                        if (picked.Equals(first))
                        {
                            c.Write("Invalid selection. Select a second point.");
                            continue;
                        }

                        second = picked;
                    }
                }
            }
            else
            {
                yield break;
            }

            GeoPoint2 p = AtOf(work, first);
            GeoPoint2 q = AtOf(work, second);

            if ((q - p).Length <= 1E-9)
            {
                c.Write("*Invalid* The dimension measures nothing.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify dimension line location");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 at = c.Input.Point;
            CadDimKind kind =
                mode == 'A' ? CadDimKind.Aligned
                : mode == 'H' ? CadDimKind.Horizontal
                : mode == 'V' ? CadDimKind.Vertical
                : LinearKind(p, q, at);
            double measured = Measured(work, kind, new List<CadRef> { first, second }, out _);
            yield return NamePrompt(work, kind, measured);
            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text : null;
            string defaultName = work.Set.NextName(CadDimConstraint.PrefixOf(kind));
            ReadNameAnswer(typed, defaultName, PlainNumber(measured), out string name, out string expression);
            ApplyDimensional(
                c,
                work,
                kind,
                new List<CadRef> { first, second },
                at,
                name,
                expression == PlainNumber(measured) ? null : expression
            );
        }

        /// <summary>Horizontal where the line through the location lies above or below the two points, vertical where beside them, as DIMLINEAR's.</summary>
        private static CadDimKind LinearKind(GeoPoint2 p, GeoPoint2 q, GeoPoint2 at)
        {
            double left = Math.Min(p.X, q.X);
            double right = Math.Max(p.X, q.X);
            double low = Math.Min(p.Y, q.Y);
            double high = Math.Max(p.Y, q.Y);
            double sideways = at.X < left ? left - at.X : (at.X > right ? at.X - right : 0.0);
            double upward = at.Y < low ? low - at.Y : (at.Y > high ? at.Y - high : 0.0);
            return upward >= sideways ? CadDimKind.Horizontal : CadDimKind.Vertical;
        }

        private static IEnumerable<CadPrompt> DcAngular(CadCommandContext c)
        {
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            yield return CadPrompt.ForEntity("Select first line or arc", new[] { "3Point" }, "3Point");

            if (c.Input.IsNone || c.Input.Is("3Point") || c.Input.Kind != CadInputKind.Selection)
            {
                if (c.Input.IsNone || c.Input.Is("3Point"))
                {
                    c.Write("The presenter does not solve a three-point angle yet: pick two lines.");
                }

                yield break;
            }

            if (!PickedRef(c, work, false, out CadRef first) || !IsLine(work, first))
            {
                c.Write("Invalid selection for Angular. Select a line segment or polyline segment.");
                yield break;
            }

            CadRef second = null;

            while (second == null)
            {
                yield return CadPrompt.ForEntity("Select second line");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (PickedRef(c, work, false, out CadRef picked))
                {
                    if (!IsLine(work, picked) || picked.SameObject(first))
                    {
                        c.Write("Invalid selection for Angular. Select another line segment or polyline segment.");
                        continue;
                    }

                    second = picked;
                }
            }

            yield return CadPrompt.ForPoint("Specify dimension arc line location");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 at = c.Input.Point;
            var refs = new List<CadRef> { first, second };
            double measured = Measured(work, CadDimKind.Angular, refs, out _);
            yield return NamePrompt(work, CadDimKind.Angular, Math.Round(measured, 10));
            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text : null;
            ReadNameAnswer(
                typed,
                work.Set.NextName("ang"),
                PlainNumber(measured),
                out string name,
                out string expression
            );
            ApplyDimensional(
                c,
                work,
                CadDimKind.Angular,
                refs,
                at,
                name,
                expression == PlainNumber(measured) ? null : expression
            );
        }

        private static IEnumerable<CadPrompt> DcRound(CadCommandContext c, bool diameter)
        {
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            CadRef round = null;

            while (round == null)
            {
                yield return CadPrompt.ForEntity("Select arc or circle");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                if (PickedRef(c, work, false, out CadRef picked))
                {
                    if (IsRound(work, picked))
                    {
                        round = picked;
                    }
                    else
                    {
                        c.Write("Invalid selection for Radial. Select a circle or arc.");
                    }
                }
            }

            yield return CadPrompt.ForPoint("Specify dimension line location");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 at = c.Input.Point;
            CadDimKind kind = diameter ? CadDimKind.Diameter : CadDimKind.Radius;
            var refs = new List<CadRef> { round };
            double measured = Measured(work, kind, refs, out _);
            yield return NamePrompt(work, kind, measured);
            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text : null;
            ReadNameAnswer(
                typed,
                work.Set.NextName(CadDimConstraint.PrefixOf(kind)),
                PlainNumber(measured),
                out string name,
                out string expression
            );
            ApplyDimensional(c, work, kind, refs, at, name, expression == PlainNumber(measured) ? null : expression);
        }

        private static IEnumerable<CadPrompt> DimConstraintCommand(CadCommandContext c)
        {
            c.Write("Current settings:  Constraint form = " + FormName(c.Session.Constraints));
            yield return CadPrompt.ForKeyword(
                "Enter a dimensional constraint option",
                new[]
                {
                    "Linear",
                    "Horizontal",
                    "Vertical",
                    "Aligned",
                    "ANgular",
                    "Radius",
                    "Diameter",
                    "Form",
                    "Convert",
                },
                "Aligned"
            );

            IEnumerable<CadPrompt> body;
            string chosen = c.Input.IsNone ? "Aligned" : c.Input.Text;

            switch (chosen)
            {
                case "Linear":
                    body = DcLinear(c, 'L');
                    break;
                case "Horizontal":
                    body = DcLinear(c, 'H');
                    break;
                case "Vertical":
                    body = DcLinear(c, 'V');
                    break;
                case "ANgular":
                    body = DcAngular(c);
                    break;
                case "Radius":
                    body = DcRound(c, false);
                    break;
                case "Diameter":
                    body = DcRound(c, true);
                    break;
                case "Form":
                    body = DcForm(c);
                    break;
                case "Convert":
                    body = DcConvert(c);
                    break;
                default:
                    body = DcLinear(c, 'A');
                    break;
            }

            foreach (CadPrompt prompt in body)
            {
                yield return prompt;
            }
        }

        private static IEnumerable<CadPrompt> DcForm(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword(
                "Enter constraint form",
                new[] { "Annotational", "Dynamic" },
                FormName(c.Session.Constraints)
            );

            if (c.Input.Is("Annotational"))
            {
                c.Session.Constraints.Form = 1;
            }
            else if (c.Input.Is("Dynamic"))
            {
                c.Session.Constraints.Form = 0;
            }
        }

        private static IEnumerable<CadPrompt> DcDisplay(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectFirst(CadPrompt.ForSelection("Select objects")))
            {
                yield return prompt;
            }

            if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
            {
                yield break;
            }

            IReadOnlyList<string> handles = c.Input.Handles;
            yield return CadPrompt.ForKeyword("Enter an option", new[] { "Display", "Hide" }, "Display");
            bool show = !c.Input.Is("Hide");
            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            var planned = new List<CadEditRecord>();
            var targets = new List<DxfEntity>();

            foreach (CadDimConstraint dim in work.Set.Dimensional)
            {
                if (
                    dim.DimensionHandle == null
                    || !dim.Dynamic
                    || !dim.Refs.Exists(r => handles.Contains(r.Handle, StringComparer.OrdinalIgnoreCase))
                )
                {
                    continue;
                }

                if (work.Entity(dim.DimensionHandle) is DxfEntity dimension && dimension.Visible != show)
                {
                    targets.Add(dimension);
                    planned.Add(
                        new CadEditRecord(
                            CadEditRecordKind.PropertyChanged,
                            dim.DimensionHandle,
                            "Visible",
                            show ? "No" : "Yes",
                            show ? "Yes" : "No"
                        )
                    );
                }
            }

            if (planned.Count > 0)
            {
                c.Apply(planned, s =>
                {
                    foreach (DxfEntity dimension in targets)
                    {
                        dimension.Visible = show;
                    }
                });
            }
        }

        #endregion

        #region DCCONVERT

        private static IEnumerable<CadPrompt> DcConvert(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection("Select associative dimensions to convert");

            if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
            {
                yield break;
            }

            CadConstraints.Work work = ConstraintWork(c);

            if (work == null)
            {
                yield break;
            }

            int converted = 0;
            int failed = 0;
            var handles = new List<string>(c.Input.Handles);
            handles.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (string handle in handles)
            {
                if (!(c.Document.FindEntity(handle) is DxfDimension dimension))
                {
                    continue;
                }

                if (dimension.DimAssoc == null)
                {
                    continue;
                }

                if (ConvertDimension(c, work, dimension))
                {
                    converted++;
                    work = ConstraintWork(c);

                    if (work == null)
                    {
                        break;
                    }
                }
                else
                {
                    failed++;
                }
            }

            c.Write(converted.ToString(CultureInfo.InvariantCulture) + " associative dimensions converted");
            c.Write(failed.ToString(CultureInfo.InvariantCulture) + " associative dimension(s) could not be converted");
        }

        /// <summary>The reference of the point a dimension's DIMASSOC ties: the object and the end, middle or centre of it the snap named; null for another snap.</summary>
        private static CadRef AssocRef(CadConstraints.Work work, DxfOsnapPointRef point)
        {
            if (!work.Take(point.ObjectHandle))
            {
                return null;
            }

            CadShape shape = work.Shapes[point.ObjectHandle];
            int segment = shape.Kind == CadShapeKind.Polyline ? Math.Max(0, point.GsMarker - 1) : -1;
            CadPart part;

            switch (point.OsnapType)
            {
                case DxfOsnapType.StartPoint:
                    part = CadPart.Start;
                    break;
                case DxfOsnapType.Endpoint:
                    part =
                        shape.Kind == CadShapeKind.Arc
                        || shape.Kind == CadShapeKind.Line
                        || shape.Kind == CadShapeKind.Polyline
                            ? CadPart.End
                            : CadPart.Start;
                    break;
                case DxfOsnapType.Midpoint:
                    part = CadPart.Mid;
                    break;
                case DxfOsnapType.Center:
                    part = CadPart.Center;
                    break;
                default:
                    return null;
            }

            return CadConstraintShapes.PointOf(shape, new CadRef(shape.Handle, segment, part)).HasValue
                ? new CadRef(shape.Handle, segment, part)
                : null;
        }

        private static bool ConvertDimension(CadCommandContext c, CadConstraints.Work work, DxfDimension dimension)
        {
            IReadOnlyList<DxfOsnapPointRef> points = dimension.DimAssoc.PointReferences;
            CadDimKind kind;
            var refs = new List<CadRef>();

            switch (dimension)
            {
                case DxfRotatedDimension rotated:
                {
                    double angle = Math.Abs(rotated.Rotation) % Math.PI;

                    if (Math.Min(angle, Math.PI - angle) < 1E-6)
                    {
                        kind = CadDimKind.Horizontal;
                    }
                    else if (Math.Abs(angle - Math.PI / 2.0) < 1E-6)
                    {
                        kind = CadDimKind.Vertical;
                    }
                    else
                    {
                        return false;
                    }

                    break;
                }

                case DxfAlignedDimension _:
                    kind = CadDimKind.Aligned;
                    break;
                case DxfRadialDimension _:
                    kind = CadDimKind.Radius;
                    break;
                case DxfDiametricDimension _:
                    kind = CadDimKind.Diameter;
                    break;
                default:
                    return false;
            }

            if (kind == CadDimKind.Radius || kind == CadDimKind.Diameter)
            {
                if (points.Count == 0)
                {
                    return false;
                }

                if (
                    !work.Take(points[0].ObjectHandle)
                    || !IsRound(work, new CadRef(points[0].ObjectHandle, -1, CadPart.Whole))
                )
                {
                    return false;
                }

                refs.Add(new CadRef(points[0].ObjectHandle, -1, CadPart.Whole));
            }
            else
            {
                DxfOsnapPointRef a = points.FirstOrDefault(p => p.PointIndex == 0);
                DxfOsnapPointRef b = points.FirstOrDefault(p => p.PointIndex == 1);
                CadRef ra = a == null ? null : AssocRef(work, a);
                CadRef rb = b == null ? null : AssocRef(work, b);

                if (ra == null || rb == null || ra.Equals(rb))
                {
                    return false;
                }

                refs.Add(ra);
                refs.Add(rb);
            }

            CadConstraintSettings settings = c.Session.Constraints;
            double measured = Measured(work, kind, refs, out int direction);
            string name = work.Set.NextName(CadDimConstraint.PrefixOf(kind));
            var constraint = new CadDimConstraint(kind, refs, name)
            {
                Direction = direction,
                Dynamic = settings.Form == 0,
                NameFormat = settings.NameFormat,
                DimensionHandle = dimension.Handle,
            };
            var parameter = new CadParameter(name, PlainNumber(measured), measured, false);
            CadSolveResult before = CadConstraints.Solve(work, null, refs[0].Handle);
            work.Set.Parameters.Add(parameter);
            work.Set.Dimensional.Add(constraint);
            CadSolveResult after = CadConstraints.Solve(work, null, refs[0].Handle);

            if (!before.Succeeded || !after.Succeeded || after.Equations - after.Rank > before.Equations - before.Rank)
            {
                Undo(work, constraint, parameter);
                return false;
            }

            string layer = constraint.Dynamic ? CadConstraintStore.DynamicLayer : null;
            c.Apply(
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.PropertyChanged, dimension.Handle, CadConstraints.OwnProperty),
                },
                s =>
                {
                    if (layer != null)
                    {
                        EnsureConstraintLayer(s.Database);
                    }

                    dimension.ReleaseDimAssoc();

                    if (layer != null)
                    {
                        PutOnConstraintLayer(dimension);
                    }

                    dimension.DimensionText = ConstraintWords(
                        dimension,
                        kind,
                        name,
                        parameter.Value,
                        constraint.NameFormat
                    );
                    Redraw(dimension);
                    CadConstraints.Write(work);
                }
            );
            return true;
        }

        #endregion

        #region PARAMETERS

        private static string ParameterLine(CadCommandContext c, CadParameterRow row)
        {
            string value = row.IsAngle
                ? row.Value.ToString(
                    "F" + Math.Max(0, c.Database.Auprec).ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture
                )
                : row.Value.ToString(
                    "F" + Math.Max(0, c.Database.Luprec).ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture
                );
            return "Parameter: "
                + row.Name.PadRight(12)
                + "Expression: "
                + (row.Expression ?? string.Empty).PadRight(22)
                + "Value: "
                + value;
        }

        private static IEnumerable<CadPrompt> ParametersTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword(
                "Enter a parameter option",
                new[] { "New", "Edit", "Rename", "Delete", "?" }
            );

            string option = c.Input.IsNone ? null : c.Input.Text;
            string name;
            string text;

            switch (option)
            {
                case "New":
                    yield return CadPrompt.ForString("Enter name for new user parameter");
                    name = c.Input.Text;
                    yield return CadPrompt.ForString("Enter expression");
                    text = c.Input.Text;
                    Report(c, CadParameters.New(c.Document, name, text));
                    break;
                case "Edit":
                    yield return CadPrompt.ForString("Enter parameter name");
                    name = c.Input.Text;
                    CadParameterRow row = CadParameters
                        .Rows(c.Document)
                        .FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

                    if (row == null)
                    {
                        c.Write("There is no parameter " + name + ".");
                        yield break;
                    }

                    c.Write(
                        "Old Expression = "
                            + row.Expression
                            + ", Value = "
                            + (
                                row.IsAngle
                                    ? row.Value.ToString(
                                        "F" + Math.Max(0, c.Database.Auprec).ToString(CultureInfo.InvariantCulture),
                                        CultureInfo.InvariantCulture
                                    )
                                    : row.Value.ToString(
                                        "F" + Math.Max(0, c.Database.Luprec).ToString(CultureInfo.InvariantCulture),
                                        CultureInfo.InvariantCulture
                                    )
                            )
                    );
                    yield return CadPrompt.ForString("Enter expression");
                    Report(c, CadParameters.Edit(c.Document, name, c.Input.Text));
                    break;
                case "Rename":
                    yield return CadPrompt.ForString("Enter parameter name");
                    name = c.Input.Text;
                    yield return CadPrompt.ForString("Enter new name");
                    Report(c, CadParameters.Rename(c.Document, name, c.Input.Text));
                    break;
                case "Delete":
                    yield return CadPrompt.ForString("Enter parameter name");
                    Report(c, CadParameters.Delete(c.Document, c.Input.Text));
                    break;
                case "?":
                    var rule = new string('-', 74);
                    c.Write(rule);

                    foreach (CadParameterRow each in CadParameters.Rows(c.Document))
                    {
                        c.Write(ParameterLine(c, each));
                    }

                    c.Write(rule);
                    break;
            }
        }

        private static void Report(CadCommandContext c, CadEditResult result)
        {
            if (!result.Succeeded && result.Message != null)
            {
                c.Write(result.Message);
            }
        }

        private static IEnumerable<CadPrompt> ParametersPalette(CadCommandContext c)
        {
            if (!c.Session.Request("PARAMETERS", c.Document, true))
            {
                c.Write("The Parameters Manager needs the window; -PARAMETERS works at the command line.");
            }

            yield break;
        }

        private static IEnumerable<CadPrompt> ParametersClose(CadCommandContext c)
        {
            c.Session.Request("PARAMETERS", c.Document, false);
            yield break;
        }

        #endregion
    }
}
