using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// FILLET and CHAMFER as AutoCAD 2021 asks them: two objects picked by the parts to keep, an arc of the radius or a line at
    /// the distances between them, the objects trimmed or run on to it (Trim) or left (No trim); Polyline rounds or cuts every
    /// corner of a polyline, Multiple goes on until Enter, and Undo inside takes the last pick back. The radius and the
    /// distances are the drawing's FILLETRAD and CHAMFERA to CHAMFERD, TRIMMODE the session's.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Fillet(CadCommandContext c)
        {
            bool multiple = false;
            var marks = new List<CadGroupMark>();

            bool trim = c.Remembered("trimmode", 1.0) != 0.0;
            c.Write(
                "Current settings: Mode = " + (trim ? "TRIM" : "NOTRIM") + ", Radius = " + Length(c.Database.Filletrad)
            );

            while (true)
            {
                yield return CadPrompt.ForEntity(
                    "Select first object",
                    new[] { "Undo", "Polyline", "Radius", "Trim", "Multiple" }
                );

                if (c.Input.Is("Undo"))
                {
                    TakeBackLast(c, marks);
                    continue;
                }

                if (c.Input.Is("Radius"))
                {
                    foreach (CadPrompt prompt in AskRadius(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Trim"))
                {
                    foreach (CadPrompt prompt in AskTrim(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Multiple"))
                {
                    multiple = true;
                    continue;
                }

                if (c.Input.Is("Polyline"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForEntity("Select 2D polyline", new[] { "Radius" });

                        if (c.Input.Is("Radius"))
                        {
                            foreach (CadPrompt prompt in AskRadius(c))
                            {
                                yield return prompt;
                            }

                            continue;
                        }

                        break;
                    }

                    if (c.Input.Kind == CadInputKind.Selection)
                    {
                        Marked(c, marks, () => AllCorners(c, c.Input.Handles[0], true, c.Database.Filletrad, 0.0));
                    }

                    if (multiple)
                    {
                        continue;
                    }

                    yield break;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                string first = c.Input.Handles[0];
                GeoPoint2 firstPick = c.Input.Point;

                if (!Cornerable(c, first, true))
                {
                    continue;
                }

                while (true)
                {
                    yield return CadPrompt
                        .ForEntity("Select second object or shift-select to apply corner", new[] { "Radius" })
                        .Previewing(
                            CadPreview.Making(c, input =>
                                Cornered(c, input, true, (second, pick, preview) =>
                                    Round(
                                        c,
                                        first,
                                        firstPick,
                                        second,
                                        pick,
                                        c.Database.Filletrad,
                                        c.Remembered("trimmode", 1.0) != 0.0,
                                        preview
                                    )
                                )
                            )
                        );

                    if (c.Input.Is("Radius"))
                    {
                        foreach (CadPrompt prompt in AskRadius(c))
                        {
                            yield return prompt;
                        }

                        continue;
                    }

                    break;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                string second = c.Input.Handles[0];
                GeoPoint2 secondPick = c.Input.Point;

                if (Cornerable(c, second, true))
                {
                    Marked(c, marks, () =>
                        Round(
                            c,
                            first,
                            firstPick,
                            second,
                            secondPick,
                            c.Database.Filletrad,
                            c.Remembered("trimmode", 1.0) != 0.0
                        )
                    );
                }

                if (!multiple)
                {
                    yield break;
                }
            }
        }

        private static IEnumerable<CadPrompt> Chamfer(CadCommandContext c)
        {
            bool multiple = false;
            var marks = new List<CadGroupMark>();

            bool trim = c.Remembered("trimmode", 1.0) != 0.0;
            bool byAngle = ChamferByAngle(c);
            c.Write(
                "("
                    + (trim ? "TRIM" : "NOTRIM")
                    + " mode) Current chamfer "
                    + (
                        byAngle
                            ? "Length = "
                                + Length(c.Database.Chamferc)
                                + ", Angle = "
                                + Number(c.Database.Chamferd * 180.0 / Math.PI)
                            : "Dist1 = " + Length(c.Database.Chamfera) + ", Dist2 = " + Length(c.Database.Chamferb)
                    )
            );

            while (true)
            {
                yield return CadPrompt.ForEntity(
                    "Select first line",
                    new[] { "Undo", "Polyline", "Distance", "Angle", "Trim", "mEthod", "Multiple" }
                );

                if (c.Input.Is("Undo"))
                {
                    TakeBackLast(c, marks);
                    continue;
                }

                if (c.Input.Is("Trim"))
                {
                    foreach (CadPrompt prompt in AskTrim(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Multiple"))
                {
                    multiple = true;
                    continue;
                }

                if (c.Input.Is("Distance") || c.Input.Is("Angle") || c.Input.Is("mEthod"))
                {
                    foreach (CadPrompt prompt in AskChamfer(c, c.Input.Text))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Polyline"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForEntity("Select 2D polyline", new[] { "Distance", "Angle", "Method" });

                        if (c.Input.Is("Distance") || c.Input.Is("Angle") || c.Input.Is("Method"))
                        {
                            foreach (CadPrompt prompt in AskChamfer(c, c.Input.Text))
                            {
                                yield return prompt;
                            }

                            continue;
                        }

                        break;
                    }

                    if (c.Input.Kind == CadInputKind.Selection)
                    {
                        ChamferDistances(c, out double d1, out double d2, out double angle);
                        Marked(c, marks, () =>
                            AllCorners(c, c.Input.Handles[0], false, d1, double.IsNaN(angle) ? d2 : -angle)
                        );
                    }

                    if (multiple)
                    {
                        continue;
                    }

                    yield break;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                string first = c.Input.Handles[0];
                GeoPoint2 firstPick = c.Input.Point;

                if (!Cornerable(c, first, false))
                {
                    continue;
                }

                while (true)
                {
                    yield return CadPrompt
                        .ForEntity(
                            "Select second line or shift-select to apply corner",
                            new[] { "Distance", "Angle", "Method" }
                        )
                        .Previewing(CadPreview.Making(c, input => Cornered(c, input, false, (second, pick, preview) =>
                                {
                                    ChamferDistances(c, out double d1, out double d2, out double angle);
                                    return Cut(
                                        c,
                                        first,
                                        firstPick,
                                        second,
                                        pick,
                                        d1,
                                        d2,
                                        angle,
                                        c.Remembered("trimmode", 1.0) != 0.0,
                                        preview
                                    );
                                })));

                    if (c.Input.Is("Distance") || c.Input.Is("Angle") || c.Input.Is("Method"))
                    {
                        foreach (CadPrompt prompt in AskChamfer(c, c.Input.Text))
                        {
                            yield return prompt;
                        }

                        continue;
                    }

                    break;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                string second = c.Input.Handles[0];
                GeoPoint2 secondPick = c.Input.Point;

                if (Cornerable(c, second, false))
                {
                    ChamferDistances(c, out double d1, out double d2, out double angle);
                    Marked(c, marks, () =>
                        Cut(
                            c,
                            first,
                            firstPick,
                            second,
                            secondPick,
                            d1,
                            d2,
                            angle,
                            c.Remembered("trimmode", 1.0) != 0.0
                        )
                    );
                }

                if (!multiple)
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// What FILLET or CHAMFER would make of the object under the cursor taken as the second: the copies of the objects it
        /// trims and its arc or line, as AutoCAD 2021 previews them; null where the object cannot be taken or nothing is made.
        /// </summary>
        private static IEnumerable<DxfEntity> Cornered(
            CadCommandContext c,
            CadInput input,
            bool fillet,
            Func<string, GeoPoint2, List<DxfEntity>, bool> pick
        )
        {
            if (input.Kind != CadInputKind.Selection || !Cornerable(c, input.Handles[0], fillet))
            {
                return null;
            }

            var preview = new List<DxfEntity>();
            return pick(input.Handles[0], input.Point, preview) ? preview : null;
        }

        /// <summary>Runs one pick of FILLET or CHAMFER after a savepoint, which Undo goes back to where the pick made a change.</summary>
        private static void Marked(CadCommandContext c, List<CadGroupMark> marks, Func<bool> pick)
        {
            c.OpenUndoStep();
            CadGroupMark mark = c.Document.MarkGroup();

            if (pick())
            {
                marks.Add(mark);
            }
        }

        private static IEnumerable<CadPrompt> AskRadius(CadCommandContext c)
        {
            yield return CadPrompt.ForDistance("Specify fillet radius", null, null, Length(c.Database.Filletrad));

            if (c.Input.Kind != CadInputKind.Number)
            {
                yield break;
            }

            if (c.Input.Number < 0.0)
            {
                c.Write("Value must be positive.");
                yield break;
            }

            double radius = c.Input.Number;
            SetHeader(c, "$FILLETRAD", c.Database.Filletrad, radius, d => d.Filletrad = radius);
        }

        private static IEnumerable<CadPrompt> AskTrim(CadCommandContext c)
        {
            bool trim = c.Remembered("trimmode", 1.0) != 0.0;
            yield return CadPrompt.ForKeyword(
                "Enter Trim mode option",
                new[] { "Trim", "No trim" },
                trim ? "Trim" : "No trim"
            );

            if (c.Input.Kind == CadInputKind.Keyword)
            {
                c.Remember("trimmode", c.Input.Is("Trim") ? 1.0 : 0.0);
            }
        }

        /// <summary>Asks the chamfer's distances, its length and angle, or its method, and keeps them in the drawing's header.</summary>
        private static IEnumerable<CadPrompt> AskChamfer(CadCommandContext c, string option)
        {
            if (string.Equals(option, "Distance", StringComparison.Ordinal))
            {
                yield return CadPrompt.ForDistance(
                    "Specify first chamfer distance",
                    null,
                    null,
                    Length(c.Database.Chamfera)
                );
                double first = c.Input.Kind == CadInputKind.Number ? c.Input.Number : c.Database.Chamfera;

                if (first < 0.0)
                {
                    c.Write("Value must be positive.");
                    yield break;
                }

                yield return CadPrompt.ForDistance("Specify second chamfer distance", null, null, Length(first));
                double second = c.Input.Kind == CadInputKind.Number ? c.Input.Number : first;

                if (second < 0.0)
                {
                    c.Write("Value must be positive.");
                    yield break;
                }

                SetHeader(c, "$CHAMFERA", c.Database.Chamfera, first, d => d.Chamfera = first);
                SetHeader(c, "$CHAMFERB", c.Database.Chamferb, second, d => d.Chamferb = second);
                SetChamferMode(c, false);
            }
            else if (string.Equals(option, "Angle", StringComparison.Ordinal))
            {
                yield return CadPrompt.ForDistance(
                    "Specify chamfer length on the first line",
                    null,
                    null,
                    Length(c.Database.Chamferc)
                );
                double length = c.Input.Kind == CadInputKind.Number ? c.Input.Number : c.Database.Chamferc;

                if (length < 0.0)
                {
                    c.Write("Value must be positive.");
                    yield break;
                }

                yield return CadPrompt
                    .ForAngle(
                        "Specify chamfer angle from the first line",
                        null,
                        null,
                        Number(c.Database.Chamferd * 180.0 / Math.PI)
                    )
                    .Turning();
                double angle = c.Input.Kind == CadInputKind.Number ? c.Input.Number : c.Database.Chamferd;
                SetHeader(c, "$CHAMFERC", c.Database.Chamferc, length, d => d.Chamferc = length);
                SetHeader(c, "$CHAMFERD", c.Database.Chamferd, angle, d => d.Chamferd = angle);
                SetChamferMode(c, true);
            }
            else
            {
                yield return CadPrompt.ForKeyword(
                    "Enter trim method",
                    new[] { "Distance", "Angle" },
                    ChamferByAngle(c) ? "Angle" : "Distance"
                );

                if (c.Input.Kind == CadInputKind.Keyword)
                {
                    SetChamferMode(c, c.Input.Is("Angle"));
                }
            }
        }

        /// <summary>Whether CHAMFER cuts by a length and an angle (CHAMMODE 1) rather than two distances.</summary>
        private static bool ChamferByAngle(CadCommandContext c)
        {
            IReadOnlyList<DxfTypedValue> mode = c.Database.GetHeader("$CHAMMODE");
            return mode != null && mode.Count > 0 && mode[0].Value != null && mode[0].Value.Trim() == "1";
        }

        private static void SetChamferMode(CadCommandContext c, bool byAngle)
        {
            if (ChamferByAngle(c) == byAngle)
            {
                return;
            }

            string value = byAngle ? "1" : "0";
            c.Apply(
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$CHAMMODE", byAngle ? "0" : "1", value),
                },
                s => s.Database.SetHeader("$CHAMMODE", new DxfTypedValue(70, value))
            );
        }

        /// <summary>The chamfer's distances as the method says: two distances, or a length and an angle (NaN for none).</summary>
        private static void ChamferDistances(CadCommandContext c, out double first, out double second, out double angle)
        {
            if (ChamferByAngle(c))
            {
                first = c.Database.Chamferc;
                second = 0.0;
                angle = c.Database.Chamferd;
            }
            else
            {
                first = c.Database.Chamfera;
                second = c.Database.Chamferb;
                angle = double.NaN;
            }
        }

        /// <summary>Writes a variable of the header as one edit of the command, where it changes.</summary>
        private static void SetHeader(
            CadCommandContext c,
            string name,
            double before,
            double after,
            Action<DxfDatabase> set
        )
        {
            if (before.Equals(after))
            {
                return;
            }

            c.Apply(
                new[]
                {
                    new CadEditRecord(CadEditRecordKind.SettingChanged, null, name, Values(before), Values(after)),
                },
                s => set(s.Database)
            );
        }

        /// <summary>
        /// Whether an object picked can be filleted (a line, an arc, a circle, a polyline of the plan) or chamfered (a line, a
        /// polyline), on a layer not locked and of no owner, saying why not as AutoCAD does.
        /// </summary>
        private static bool Cornerable(CadCommandContext c, string handle, bool fillet)
        {
            DxfEntity entity = c.Document.FindEntity(handle);
            bool kind =
                entity is DxfLine || entity is DxfPolyline || (fillet && (entity is DxfArc || entity is DxfCircle));

            if (!kind && CadMlineEdit.IsMline(entity))
            {
                // AutoCAD 2021's own words for a multiline.
                c.Write(fillet ? "Fillet requires 2 lines, arcs, or circles." : "Chamfer requires 2 lines.");
                return false;
            }

            if (!kind)
            {
                c.Write(fillet ? "Cannot fillet that object." : "Cannot chamfer that object.");
                return false;
            }

            if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
            {
                c.Write("Object is on a locked layer.");
                return false;
            }

            if (!Flat(entity))
            {
                c.Write("The object does not lie in the plan.");
                return false;
            }

            if (c.Document.Owners.GetEditRule(entity) != CadEditRule.Free)
            {
                c.Write(
                    "This "
                        + CadOwnerEditGate.Describe(entity)
                        + " belongs to the model and cannot be "
                        + (fillet ? "filleted" : "chamfered")
                        + " here."
                );
                return false;
            }

            return true;
        }

        /// <summary>Whether an entity lies in the plan: a line level, an arc, a circle or a polyline whose normal is the plan's either way.</summary>
        private static bool Flat(DxfEntity entity)
        {
            double tolerance = Tolerance.Default.EqualVector;

            switch (entity)
            {
                case DxfLine line:
                    return Math.Abs(line.StartPoint.Z - line.EndPoint.Z) <= Tolerance.Default.EqualPoint;
                case DxfArc arc:
                    return Math.Abs(Math.Abs(arc.Normal.Z) - 1.0) <= tolerance;
                case DxfCircle circle:
                    return Math.Abs(Math.Abs(circle.Normal.Z) - 1.0) <= tolerance;
                case DxfPolyline polyline:
                    return Math.Abs(Math.Abs(polyline.Normal.Z) - 1.0) <= tolerance;
                default:
                    return false;
            }
        }

        /// <summary>One FILLET pick: the two objects rounded by an arc of the radius, or run to a corner where it is nought.</summary>
        private static bool Round(
            CadCommandContext c,
            string first,
            GeoPoint2 firstPick,
            string second,
            GeoPoint2 secondPick,
            double radius,
            bool trim,
            List<DxfEntity> preview = null
        )
        {
            DxfEntity a = c.Document.FindEntity(first);
            DxfEntity b = c.Document.FindEntity(second);

            if (a is DxfPolyline || b is DxfPolyline)
            {
                return PolylineCorner(
                    c,
                    first,
                    firstPick,
                    second,
                    secondPick,
                    true,
                    radius,
                    0.0,
                    double.NaN,
                    trim,
                    preview
                );
            }

            if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
            {
                c.Write("Cannot fillet an object to itself.");
                return false;
            }

            var ends = new List<Trimmed>();
            GeoPoint2 centre;
            GeoPoint2 from;
            GeoPoint2 to;

            if (a is DxfLine lineA && b is DxfLine lineB)
            {
                if (
                    !LineCorner(
                        c,
                        lineA,
                        firstPick,
                        lineB,
                        secondPick,
                        radius,
                        out GeoPoint2 corner,
                        out GeoVector2 u1,
                        out GeoVector2 u2,
                        out bool parallel
                    )
                )
                {
                    if (parallel)
                    {
                        return ParallelFillet(c, lineA, firstPick, lineB, trim, preview);
                    }

                    return false;
                }

                double half = Math.Acos(Math.Max(-1.0, Math.Min(1.0, u1.X * u2.X + u1.Y * u2.Y))) / 2.0;
                double along = radius > 0.0 ? radius / Math.Tan(half) : 0.0;
                from = corner + u1 * along;
                to = corner + u2 * along;

                if (!KeepsRoom(lineA, corner, u1, along, c) || !KeepsRoom(lineB, corner, u2, along, c))
                {
                    c.Write("Radius is too large.");
                    return false;
                }

                ends.Add(Trimmed.Line(lineA, corner, u1, from));
                ends.Add(Trimmed.Line(lineB, corner, u2, to));
                GeoVector2 bisector = u1 + u2;
                centre = corner + bisector / bisector.Length * (radius / Math.Sin(half));
            }
            else
            {
                Shape one = Shape.Of(a);
                Shape two = Shape.Of(b);

                if (
                    !Tangent(one, firstPick, two, secondPick, radius, c.Session.Tolerance, out centre, out from, out to)
                )
                {
                    c.Write(radius > 0.0 ? "Radius is too large." : "The objects do not meet.");
                    return false;
                }

                Trimmed endA = one.TrimTo(a, from, firstPick, c.Session.Tolerance);
                Trimmed endB = two.TrimTo(b, to, secondPick, c.Session.Tolerance);

                if (endA == null || endB == null)
                {
                    c.Write("Radius is too large.");
                    return false;
                }

                foreach (Trimmed end in new[] { endA, endB })
                {
                    if (end != Trimmed.Whole)
                    {
                        ends.Add(end);
                    }
                }
            }

            DxfArc round = null;

            if (radius > c.Session.Tolerance.EqualPoint)
            {
                double start = Math.Atan2(from.Y - centre.Y, from.X - centre.X);
                double end = Math.Atan2(to.Y - centre.Y, to.X - centre.X);

                if (Turn(end - start) > Math.PI)
                {
                    double swap = start;
                    start = end;
                    end = swap;
                }

                round = Current(
                    c,
                    new DxfArc(new GeoPoint3(centre.X, centre.Y, Height(a)), radius, Turn(start), Turn(end))
                );
                SameLayer(round, a, b);
            }
            else if (!trim)
            {
                return false;
            }

            return Write(c, trim ? ends : new List<Trimmed>(), round, preview);
        }

        /// <summary>One CHAMFER pick of two lines: a line cut across the corner at the distances, or the corner where both are nought.</summary>
        private static bool Cut(
            CadCommandContext c,
            string first,
            GeoPoint2 firstPick,
            string second,
            GeoPoint2 secondPick,
            double d1,
            double d2,
            double angle,
            bool trim,
            List<DxfEntity> preview = null
        )
        {
            DxfEntity a = c.Document.FindEntity(first);
            DxfEntity b = c.Document.FindEntity(second);

            if (a is DxfPolyline || b is DxfPolyline)
            {
                return PolylineCorner(
                    c,
                    first,
                    firstPick,
                    second,
                    secondPick,
                    false,
                    0.0,
                    d1,
                    double.IsNaN(angle) ? d2 : -angle,
                    trim,
                    preview
                );
            }

            if (string.Equals(first, second, StringComparison.OrdinalIgnoreCase))
            {
                c.Write("Cannot chamfer an object to itself.");
                return false;
            }

            var lineA = (DxfLine)a;
            var lineB = (DxfLine)b;

            if (
                !LineCorner(
                    c,
                    lineA,
                    firstPick,
                    lineB,
                    secondPick,
                    0.0,
                    out GeoPoint2 corner,
                    out GeoVector2 u1,
                    out GeoVector2 u2,
                    out bool parallel
                )
            )
            {
                if (parallel)
                {
                    c.Write("Lines are parallel.");
                }

                return false;
            }

            double along2 = d2;

            if (!double.IsNaN(angle))
            {
                // A length along the first line and an angle from it: the second distance by the law of sines.
                double between = Math.Acos(Math.Max(-1.0, Math.Min(1.0, u1.X * u2.X + u1.Y * u2.Y)));
                double sine = Math.Sin(between + angle);

                if (!(sine > 1E-12))
                {
                    c.Write("Distance is too large.");
                    return false;
                }

                along2 = d1 * Math.Sin(angle) / sine;
            }

            GeoPoint2 from = corner + u1 * d1;
            GeoPoint2 to = corner + u2 * along2;

            if (!KeepsRoom(lineA, corner, u1, d1, c) || !KeepsRoom(lineB, corner, u2, along2, c))
            {
                c.Write("Distance is too large.");
                return false;
            }

            DxfLine cut = null;

            if ((to - from).Length > c.Session.Tolerance.EqualPoint)
            {
                cut = Current(
                    c,
                    new DxfLine(new GeoPoint3(from.X, from.Y, Height(a)), new GeoPoint3(to.X, to.Y, Height(a)))
                );
                SameLayer(cut, a, b);
            }
            else if (!trim)
            {
                return false;
            }

            var ends = new List<Trimmed>();

            if (trim)
            {
                ends.Add(Trimmed.Line(lineA, corner, u1, from));
                ends.Add(Trimmed.Line(lineB, corner, u2, to));
            }

            return Write(c, ends, cut, preview);
        }

        /// <summary>
        /// The corner of two lines that are not parallel and the way from it along each to the part picked: the side of the
        /// corner its pick lies on, or the far end where the pick is at the corner.
        /// </summary>
        private static bool LineCorner(
            CadCommandContext c,
            DxfLine a,
            GeoPoint2 pickA,
            DxfLine b,
            GeoPoint2 pickB,
            double radius,
            out GeoPoint2 corner,
            out GeoVector2 u1,
            out GeoVector2 u2,
            out bool parallel
        )
        {
            corner = default;
            u1 = default;
            u2 = default;
            GeoPoint2 a0 = P2(a.StartPoint),
                a1 = P2(a.EndPoint),
                b0 = P2(b.StartPoint),
                b1 = P2(b.EndPoint);
            GeoVector2 da = a1 - a0;
            GeoVector2 db = b1 - b0;

            if (da.Length <= c.Session.Tolerance.EqualPoint || db.Length <= c.Session.Tolerance.EqualPoint)
            {
                parallel = false;
                c.Write("A line of no length cannot be filleted.");
                return false;
            }

            da /= da.Length;
            db /= db.Length;
            double cross = da.X * db.Y - da.Y * db.X;
            parallel = Math.Abs(cross) <= c.Session.Tolerance.EqualVector;

            if (parallel)
            {
                return false;
            }

            GeoVector2 gap = b0 - a0;
            double t = (gap.X * db.Y - gap.Y * db.X) / cross;
            corner = a0 + da * t;
            u1 = Toward(corner, da, pickA, a0, a1);
            u2 = Toward(corner, db, pickB, b0, b1);
            return true;
        }

        /// <summary>The way from a corner along a line to the part of it picked.</summary>
        private static GeoVector2 Toward(
            GeoPoint2 corner,
            GeoVector2 along,
            GeoPoint2 pick,
            GeoPoint2 start,
            GeoPoint2 end
        )
        {
            double side = (pick - corner).X * along.X + (pick - corner).Y * along.Y;

            if (Math.Abs(side) <= 1E-12)
            {
                GeoPoint2 far = (start - corner).Length >= (end - corner).Length ? start : end;
                side = (far - corner).X * along.X + (far - corner).Y * along.Y;
            }

            return side >= 0.0 ? along : along * -1.0;
        }

        /// <summary>Whether the part of a line kept reaches past the point a distance from the corner, so something of it is left.</summary>
        private static bool KeepsRoom(DxfLine line, GeoPoint2 corner, GeoVector2 way, double along, CadCommandContext c)
        {
            double reach = Math.Max(Dot(P2(line.StartPoint) - corner, way), Dot(P2(line.EndPoint) - corner, way));
            return reach > along + c.Session.Tolerance.EqualPoint;
        }

        /// <summary>
        /// Two parallel lines joined by a half circle as AutoCAD joins them, whatever the radius: at the end of the first line
        /// nearer its pick, the second run on or cut back to stand across from it.
        /// </summary>
        private static bool ParallelFillet(
            CadCommandContext c,
            DxfLine a,
            GeoPoint2 pickA,
            DxfLine b,
            bool trim,
            List<DxfEntity> preview
        )
        {
            GeoPoint2 a0 = P2(a.StartPoint),
                a1 = P2(a.EndPoint),
                b0 = P2(b.StartPoint),
                b1 = P2(b.EndPoint);
            GeoVector2 da = (a1 - a0) / (a1 - a0).Length;
            GeoVector2 db = (b1 - b0) / (b1 - b0).Length;
            double width = Math.Abs((b0 - a0).X * da.Y - (b0 - a0).Y * da.X);

            if (width <= c.Session.Tolerance.EqualPoint)
            {
                c.Write("Cannot fillet lines that lie on one line.");
                return false;
            }

            bool atEnd = (pickA - a1).Length < (pickA - a0).Length;
            GeoPoint2 end = atEnd ? a1 : a0;
            GeoVector2 outward = atEnd ? da : da * -1.0;
            GeoPoint2 across = b0 + db * Dot(end - b0, db);
            GeoPoint2 centre = new GeoPoint2((end.X + across.X) / 2.0, (end.Y + across.Y) / 2.0);
            double start = Math.Atan2(end.Y - centre.Y, end.X - centre.X);
            double finish = Math.Atan2(across.Y - centre.Y, across.X - centre.X);
            double middle = Math.Atan2(outward.Y, outward.X);

            if (Turn(middle - start) > Turn(finish - start))
            {
                double swap = start;
                start = finish;
                finish = swap;
            }

            DxfArc round = Current(
                c,
                new DxfArc(new GeoPoint3(centre.X, centre.Y, a.StartPoint.Z), width / 2.0, Turn(start), Turn(finish))
            );
            SameLayer(round, a, b);
            var ends = new List<Trimmed>();

            if (trim)
            {
                bool moveStart = Dot(b0 - end, outward) > Dot(b1 - end, outward);
                ends.Add(new Trimmed(b.Handle, moveStart, across));
            }

            return Write(c, ends, round, preview);
        }

        /// <summary>
        /// The circle of a radius touching two shapes (a line, an arc's or a circle's circle) nearest the picks: of the centres
        /// where the shapes moved the radius either way cross, the one whose points of touching lie nearest the picks; at a
        /// radius of nought, the crossing of the shapes nearest them.
        /// </summary>
        private static bool Tangent(
            Shape one,
            GeoPoint2 pickOne,
            Shape two,
            GeoPoint2 pickTwo,
            double radius,
            Tolerance tolerance,
            out GeoPoint2 centre,
            out GeoPoint2 from,
            out GeoPoint2 to
        )
        {
            centre = default;
            from = default;
            to = default;
            double best = double.PositiveInfinity;
            bool found = false;
            double[] sides = radius > tolerance.EqualPoint ? new[] { radius, -radius } : new[] { 0.0 };

            foreach (double s1 in sides)
            {
                foreach (double s2 in sides)
                {
                    Shape moved1 = one.Moved(s1, tolerance);
                    Shape moved2 = two.Moved(s2, tolerance);

                    if (moved1 == null || moved2 == null)
                    {
                        continue;
                    }

                    foreach (GeoPoint2 at in moved1.Crossings(moved2, tolerance))
                    {
                        if (
                            !one.TryFoot(at, tolerance, out GeoPoint2 f1)
                            || !two.TryFoot(at, tolerance, out GeoPoint2 f2)
                        )
                        {
                            continue;
                        }

                        double score = (f1 - pickOne).Length + (f2 - pickTwo).Length;

                        if (
                            score < best - 1E-12
                            || (
                                Math.Abs(score - best) <= 1E-12
                                && (at.X < centre.X || (at.X == centre.X && at.Y < centre.Y))
                            )
                        )
                        {
                            best = score;
                            centre = at;
                            from = f1;
                            to = f2;
                            found = true;
                        }
                    }
                }
            }

            return found;
        }

        /// <summary>
        /// Writes the trims and the new arc or line as one edit; gives whether it was made. Given a list to preview into, it adds
        /// the copies of the objects trimmed and the arc or line to it instead, and writes nothing.
        /// </summary>
        private static bool Write(CadCommandContext c, List<Trimmed> ends, DxfEntity added, List<DxfEntity> preview)
        {
            var planned = new List<CadEditRecord>();

            foreach (Trimmed end in ends)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Reshaped, end.Handle, c.Command.Name));
            }

            if (planned.Count == 0 && added == null)
            {
                return false;
            }

            if (preview != null)
            {
                foreach (Trimmed end in ends)
                {
                    var copy = (DxfEntity)c.Document.FindEntity(end.Handle).Clone();
                    end.Apply(copy);
                    preview.Add(copy);
                }

                if (added != null)
                {
                    preview.Add(added);
                }

                return true;
            }

            CadEditResult result = c.Apply(planned, s =>
            {
                CadSpace space = null;

                foreach (Trimmed end in ends)
                {
                    end.Apply(s.Document.FindEntity(end.Handle));
                    space = space ?? s.Document.FindSpace(end.Handle);
                }

                if (added != null)
                {
                    s.Append(space ?? s.Document.WorkingSpace, added);
                }
            });

            return result.Succeeded;
        }

        /// <summary>Puts a new arc or line on the layer of the two objects where they share one, as AutoCAD does; else it keeps the current layer.</summary>
        private static void SameLayer(DxfEntity made, DxfEntity a, DxfEntity b)
        {
            if (string.Equals(a.Layer, b.Layer, StringComparison.OrdinalIgnoreCase))
            {
                made.Layer = a.Layer;
            }
        }

        private static double Height(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfLine line:
                    return line.StartPoint.Z;
                case DxfArc arc:
                    return arc.Center.Z;
                case DxfCircle circle:
                    return circle.Center.Z;
                default:
                    return 0.0;
            }
        }

        private static double Dot(GeoVector2 a, GeoVector2 b) => a.X * b.X + a.Y * b.Y;

        /// <summary>An angle turned into [0, 2 pi).</summary>
        private static double Turn(double angle)
        {
            double turned = angle % (2.0 * Math.PI);
            return turned < 0.0 ? turned + 2.0 * Math.PI : turned;
        }

        /// <summary>
        /// FILLET or CHAMFER of two straight segments of one polyline that meet at a vertex: the vertex cut back along both and an
        /// arc segment or a straight one put between, or, with No trim, an arc or a line drawn apart from the polyline.
        /// </summary>
        private static bool PolylineCorner(
            CadCommandContext c,
            string first,
            GeoPoint2 firstPick,
            string second,
            GeoPoint2 secondPick,
            bool fillet,
            double radius,
            double d1,
            double d2,
            bool trim,
            List<DxfEntity> preview
        )
        {
            if (
                !string.Equals(first, second, StringComparison.OrdinalIgnoreCase)
                || !(c.Document.FindEntity(first) is DxfPolyline polyline)
            )
            {
                c.Write(
                    "A polyline segment is "
                        + (fillet ? "filleted" : "chamfered")
                        + " here only with a segment of the same polyline next to it."
                );
                return false;
            }

            int count = polyline.NumberOfVertices;
            bool closed = polyline.Closed;
            int segments = closed ? count : count - 1;
            int i = SegmentAt(polyline, firstPick, segments);
            int j = SegmentAt(polyline, secondPick, segments);
            int vertex = -1;

            if (j == (i + 1) % count && (closed || j > i))
            {
                vertex = j;
            }
            else if (i == (j + 1) % count && (closed || i > j))
            {
                vertex = i;
            }

            if (
                vertex < 0
                || !Corner(polyline, vertex, fillet, radius, d1, d2, out PolyCorner corner)
                || corner.Straight
            )
            {
                c.Write(
                    "Only two straight segments of a polyline next to each other are "
                        + (fillet ? "filleted" : "chamfered")
                        + " here."
                );
                return false;
            }

            if (
                corner.Along1 > corner.Length1 + c.Session.Tolerance.EqualPoint
                || corner.Along2 > corner.Length2 + c.Session.Tolerance.EqualPoint
            )
            {
                c.Write(fillet ? "Radius is too large." : "Distance is too large.");
                return false;
            }

            if (corner.Along1 <= c.Session.Tolerance.EqualPoint && corner.Along2 <= c.Session.Tolerance.EqualPoint)
            {
                return false;
            }

            string handle = first;

            if (!trim)
            {
                DxfEntity apart = fillet ? (DxfEntity)corner.ArcInWorld(polyline) : corner.LineInWorld(polyline);
                apart = Current(c, apart);
                apart.Layer = polyline.Layer;
                return Write(c, new List<Trimmed>(), apart, preview);
            }

            if (preview != null)
            {
                var copy = (DxfPolyline)polyline.Clone();
                corner.Apply(copy);
                preview.Add(copy);
                return true;
            }

            CadEditResult result = c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, c.Command.Name) },
                s =>
                {
                    corner.Apply((DxfPolyline)s.Document.FindEntity(handle));
                }
            );

            return result.Succeeded;
        }

        /// <summary>The segment of a polyline nearest a point picked on it.</summary>
        private static int SegmentAt(DxfPolyline polyline, GeoPoint2 pick, int segments)
        {
            GeoPoint3 near = polyline.GetClosestPointTo(P3(pick), false);
            double parameter = polyline.GetParameterAtPoint(near);
            int segment = (int)Math.Floor(parameter);
            return Math.Max(0, Math.Min(segments - 1, segment));
        }

        /// <summary>
        /// Polyline of FILLET or CHAMFER: every corner of a polyline between two straight segments rounded or cut where both
        /// segments have room for it, the corners taken in turn and one left where its neighbour took the room, with AutoCAD's
        /// count of lines filleted and of those too short. For a chamfer <paramref name="second"/> is the second distance, or the
        /// angle from the first line turned negative.
        /// </summary>
        private static bool AllCorners(CadCommandContext c, string handle, bool fillet, double first, double second)
        {
            if (!(c.Document.FindEntity(handle) is DxfPolyline polyline))
            {
                c.Write("Object is not a polyline.");
                return false;
            }

            if (!Cornerable(c, handle, fillet))
            {
                return false;
            }

            int count = polyline.NumberOfVertices;
            bool closed = polyline.Closed;
            var corners = new List<PolyCorner>();
            var cutIn = new double[count];
            var cutOut = new double[count];
            int done = 0;
            int tooShort = 0;
            double slack = c.Session.Tolerance.EqualPoint;

            for (int k = closed ? 0 : 1; k < (closed ? count : count - 1); k++)
            {
                if (!Corner(polyline, k, fillet, first, first, second, out PolyCorner corner) || corner.Straight)
                {
                    continue;
                }

                if (fillet ? !(first > slack) : !(corner.Along1 > slack || corner.Along2 > slack))
                {
                    continue;
                }

                int before = (k - 1 + count) % count;
                int after = (k + 1) % count;

                if (
                    corner.Along1 > corner.Length1 - cutOut[before] + slack
                    || corner.Along2 > corner.Length2 - cutIn[after] + slack
                )
                {
                    tooShort++;
                    continue;
                }

                cutIn[k] = corner.Along1;
                cutOut[k] = corner.Along2;
                corners.Add(corner);
                done++;
            }

            c.Write(
                done.ToString(CultureInfo.InvariantCulture)
                    + (done == 1 ? " line was " : " lines were ")
                    + (fillet ? "filleted" : "chamfered")
            );

            if (tooShort > 0)
            {
                c.Write(
                    tooShort.ToString(CultureInfo.InvariantCulture) + (tooShort == 1 ? " was" : " were") + " too short"
                );
            }

            if (corners.Count == 0)
            {
                return false;
            }

            CadEditResult result = c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, c.Command.Name) },
                s =>
                {
                    var target = (DxfPolyline)s.Document.FindEntity(handle);

                    // The corners from the last back, so that a vertex put in does not move the index of one still to do.
                    for (int n = corners.Count - 1; n >= 0; n--)
                    {
                        corners[n].Apply(target);
                    }
                }
            );

            return result.Succeeded;
        }

        /// <summary>
        /// The corner of a polyline at a vertex between two straight segments, in the polyline's own plane: cut back along the
        /// segment coming in and the one going out, by the fillet's tangent length or the chamfer's distances; a chamfer's
        /// second distance below nought is the angle from the first line.
        /// </summary>
        private static bool Corner(
            DxfPolyline polyline,
            int vertex,
            bool fillet,
            double radius,
            double d1,
            double d2,
            out PolyCorner corner
        )
        {
            corner = null;
            int count = polyline.NumberOfVertices;
            int before = (vertex - 1 + count) % count;
            int after = (vertex + 1) % count;

            if (polyline.GetBulgeAt(before) != 0.0 || polyline.GetBulgeAt(vertex) != 0.0)
            {
                return false;
            }

            GeoPoint2 p = polyline.GetPoint2dAt(before);
            GeoPoint2 v = polyline.GetPoint2dAt(vertex);
            GeoPoint2 n = polyline.GetPoint2dAt(after);
            double length1 = (p - v).Length;
            double length2 = (n - v).Length;

            if (!(length1 > 0.0) || !(length2 > 0.0))
            {
                return false;
            }

            GeoVector2 u1 = (p - v) / length1;
            GeoVector2 u2 = (n - v) / length2;
            double between = Math.Acos(Math.Max(-1.0, Math.Min(1.0, Dot(u1, u2))));
            var made = new PolyCorner
            {
                Vertex = vertex,
                Length1 = length1,
                Length2 = length2,
                Corner = v,
                U1 = u1,
                U2 = u2,
                Fillet = fillet,
            };

            if (between >= Math.PI - Tolerance.Default.EqualAngleRad)
            {
                made.Straight = true;
                corner = made;
                return true;
            }

            if (fillet)
            {
                double along = radius / Math.Tan(between / 2.0);
                made.Along1 = along;
                made.Along2 = along;
                double turn = (v - p).X * (n - v).Y - (v - p).Y * (n - v).X;
                made.Bulge = Math.Tan((Math.PI - between) / 4.0) * (turn >= 0.0 ? 1.0 : -1.0);
                made.Radius = radius;
                made.Half = between / 2.0;
            }
            else
            {
                made.Along1 = d1;
                made.Along2 = d2 < 0.0 ? d1 * Math.Sin(-d2) / Math.Sin(between - d2) : d2;

                if (d2 < 0.0 && !(Math.Sin(between - d2) > 1E-12))
                {
                    made.Along2 = double.PositiveInfinity;
                }
            }

            corner = made;
            return true;
        }

        /// <summary>A corner of a polyline to round or cut, in its own plane.</summary>
        private sealed class PolyCorner
        {
            public int Vertex;
            public double Length1;
            public double Length2;
            public double Along1;
            public double Along2;
            public GeoPoint2 Corner;
            public GeoVector2 U1;
            public GeoVector2 U2;
            public double Bulge;
            public double Radius;
            public double Half;
            public bool Fillet;
            public bool Straight;

            public GeoPoint2 From => Corner + U1 * Along1;

            public GeoPoint2 To => Corner + U2 * Along2;

            /// <summary>Moves the vertex back along the segment coming in and puts one in along the segment going out.</summary>
            public void Apply(DxfPolyline polyline)
            {
                double startWidth = polyline.GetStartWidthAt(Vertex);
                double endWidth = polyline.GetEndWidthAt(Vertex);
                polyline.SetPointAt(Vertex, From);
                polyline.SetBulgeAt(Vertex, Fillet ? Bulge : 0.0);
                polyline.AddVertexAt(Vertex + 1, To, 0.0, startWidth, endWidth);
            }

            public DxfArc ArcInWorld(DxfPolyline polyline)
            {
                GeoVector2 bisector = U1 + U2;
                GeoPoint2 centre = Corner + bisector / bisector.Length * (Radius / Math.Sin(Half));
                GeoPoint2 a = World(polyline, From);
                GeoPoint2 b = World(polyline, To);
                GeoPoint2 c = World(polyline, centre);
                double start = Math.Atan2(a.Y - c.Y, a.X - c.X);
                double end = Math.Atan2(b.Y - c.Y, b.X - c.X);

                if (Turn(end - start) > Math.PI)
                {
                    double swap = start;
                    start = end;
                    end = swap;
                }

                return new DxfArc(new GeoPoint3(c.X, c.Y, Z(polyline)), Radius, Turn(start), Turn(end));
            }

            public DxfLine LineInWorld(DxfPolyline polyline)
            {
                GeoPoint2 a = World(polyline, From);
                GeoPoint2 b = World(polyline, To);
                return new DxfLine(new GeoPoint3(a.X, a.Y, Z(polyline)), new GeoPoint3(b.X, b.Y, Z(polyline)));
            }

            /// <summary>A point of the plane of a polyline of the plan in the world: the plane's x is the world's -x where its normal points down.</summary>
            private static GeoPoint2 World(DxfPolyline polyline, GeoPoint2 point) =>
                polyline.Normal.Z < 0.0 ? new GeoPoint2(-point.X, point.Y) : point;

            private static double Z(DxfPolyline polyline) =>
                polyline.Normal.Z < 0.0 ? -polyline.Elevation : polyline.Elevation;
        }

        /// <summary>A change of one object a fillet or a chamfer trims: a line's end moved, or an arc's angles set.</summary>
        private sealed class Trimmed
        {
            private readonly bool _isArc;
            private readonly bool _moveStart;
            private readonly GeoPoint2 _to;
            private readonly double _start;
            private readonly double _end;

            public Trimmed(string handle, bool moveStart, GeoPoint2 to)
            {
                Handle = handle;
                _moveStart = moveStart;
                _to = to;
            }

            private Trimmed(string handle, double start, double end)
            {
                Handle = handle;
                _isArc = true;
                _start = start;
                _end = end;
            }

            public string Handle { get; }

            /// <summary>Gets the change of a circle, which a fillet leaves whole, as AutoCAD leaves it.</summary>
            public static Trimmed Whole { get; } = new Trimmed(null, false, default);

            /// <summary>A line trimmed or run on to a point, the end on the side of the corner away from the way kept moved.</summary>
            public static Trimmed Line(DxfLine line, GeoPoint2 corner, GeoVector2 way, GeoPoint2 to)
            {
                bool moveStart = Dot(P2(line.StartPoint) - corner, way) < Dot(P2(line.EndPoint) - corner, way);
                return new Trimmed(line.Handle, moveStart, to);
            }

            /// <summary>An arc given new angles, counter-clockwise in the world.</summary>
            public static Trimmed Arc(string handle, double start, double end) => new Trimmed(handle, start, end);

            public void Apply(DxfEntity entity)
            {
                if (_isArc)
                {
                    var arc = (DxfArc)entity;

                    if (arc.Normal.Z < 0.0)
                    {
                        // About a normal pointing down the arc runs the other way and its plane's x is the world's -x.
                        arc.StartAngle = Turn(Math.PI - _end);
                        arc.EndAngle = Turn(Math.PI - _start);
                    }
                    else
                    {
                        arc.StartAngle = Turn(_start);
                        arc.EndAngle = Turn(_end);
                    }

                    return;
                }

                var line = (DxfLine)entity;

                if (_moveStart)
                {
                    line.StartPoint = new GeoPoint3(_to.X, _to.Y, line.StartPoint.Z);
                }
                else
                {
                    line.EndPoint = new GeoPoint3(_to.X, _to.Y, line.EndPoint.Z);
                }
            }
        }

        /// <summary>A shape a fillet touches: a line run on both ways, or the circle of an arc or of a circle.</summary>
        private sealed class Shape
        {
            private GeoPoint2 _point;
            private GeoVector2 _way;
            private double _radius;
            private bool _isLine;
            private bool _isArc;
            private double _start;
            private double _end;

            public static Shape Of(DxfEntity entity)
            {
                switch (entity)
                {
                    case DxfLine line:
                        GeoVector2 way = P2(line.EndPoint) - P2(line.StartPoint);
                        return new Shape
                        {
                            _isLine = true,
                            _point = P2(line.StartPoint),
                            _way = way / way.Length,
                        };
                    case DxfArc arc:
                        bool down = arc.Normal.Z < 0.0;
                        return new Shape
                        {
                            _point = P2(arc.Center),
                            _radius = arc.Radius,
                            _isArc = true,
                            _start = down ? Turn(Math.PI - arc.EndAngle) : arc.StartAngle,
                            _end = down ? Turn(Math.PI - arc.StartAngle) : arc.EndAngle,
                        };
                    default:
                        var circle = (DxfCircle)entity;
                        return new Shape { _point = P2(circle.Center), _radius = circle.Radius };
                }
            }

            /// <summary>The shape moved a distance across: a line to its left, a circle's radius grown; null where nothing is left.</summary>
            public Shape Moved(double distance, Tolerance tolerance)
            {
                if (_isLine)
                {
                    return new Shape
                    {
                        _isLine = true,
                        _point = _point + new GeoVector2(-_way.Y, _way.X) * distance,
                        _way = _way,
                    };
                }

                double radius = _radius + distance;
                return radius > tolerance.EqualPoint ? new Shape { _point = _point, _radius = radius } : null;
            }

            /// <summary>The point of the shape nearest a point; false for the centre of a circle, which has none.</summary>
            public bool TryFoot(GeoPoint2 at, Tolerance tolerance, out GeoPoint2 foot)
            {
                if (_isLine)
                {
                    foot = _point + _way * Dot(at - _point, _way);
                    return true;
                }

                GeoVector2 out1 = at - _point;
                double length = out1.Length;
                foot = default;

                if (length <= tolerance.EqualPoint)
                {
                    return false;
                }

                foot = _point + out1 * (_radius / length);
                return true;
            }

            /// <summary>Where two shapes cross, in a fixed order.</summary>
            public List<GeoPoint2> Crossings(Shape other, Tolerance tolerance)
            {
                var found = new List<GeoPoint2>();

                if (_isLine && other._isLine)
                {
                    double cross = _way.X * other._way.Y - _way.Y * other._way.X;

                    if (Math.Abs(cross) > tolerance.EqualVector)
                    {
                        GeoVector2 gap = other._point - _point;
                        found.Add(_point + _way * ((gap.X * other._way.Y - gap.Y * other._way.X) / cross));
                    }

                    return found;
                }

                if (_isLine || other._isLine)
                {
                    Shape line = _isLine ? this : other;
                    Shape circle = _isLine ? other : this;
                    GeoPoint2 foot = line._point + line._way * Dot(circle._point - line._point, line._way);
                    double off = (circle._point - foot).Length;

                    if (off > circle._radius + tolerance.EqualPoint)
                    {
                        return found;
                    }

                    double half = Math.Sqrt(Math.Max(0.0, circle._radius * circle._radius - off * off));
                    found.Add(foot - line._way * half);

                    if (half > tolerance.EqualPoint)
                    {
                        found.Add(foot + line._way * half);
                    }

                    return found;
                }

                GeoVector2 between = other._point - _point;
                double d = between.Length;

                if (
                    d <= tolerance.EqualPoint
                    || d > _radius + other._radius + tolerance.EqualPoint
                    || d < Math.Abs(_radius - other._radius) - tolerance.EqualPoint
                )
                {
                    return found;
                }

                double a = (_radius * _radius - other._radius * other._radius + d * d) / (2.0 * d);
                double h = Math.Sqrt(Math.Max(0.0, _radius * _radius - a * a));
                GeoVector2 unit = between / d;
                GeoPoint2 mid = _point + unit * a;
                var across = new GeoVector2(-unit.Y, unit.X);
                found.Add(mid - across * h);

                if (h > tolerance.EqualPoint)
                {
                    found.Add(mid + across * h);
                }

                return found;
            }

            /// <summary>
            /// The change trimming the object to a point keeps the part picked with: a line its end beyond the point on the pick's
            /// side, an arc the shortest run from the point to one of its ends that holds the pick; a circle is left whole.
            /// Null where nothing would be left.
            /// </summary>
            public Trimmed TrimTo(DxfEntity entity, GeoPoint2 at, GeoPoint2 pick, Tolerance tolerance)
            {
                if (!_isLine && !_isArc)
                {
                    return Trimmed.Whole;
                }

                if (_isLine)
                {
                    var line = (DxfLine)entity;
                    GeoVector2 way = pick - at;

                    if (way.Length <= tolerance.EqualPoint)
                    {
                        way =
                            (
                                (P2(line.StartPoint) - at).Length >= (P2(line.EndPoint) - at).Length
                                    ? P2(line.StartPoint)
                                    : P2(line.EndPoint)
                            ) - at;
                    }

                    way /= Math.Max(way.Length, 1E-300);
                    double reach = Math.Max(Dot(P2(line.StartPoint) - at, way), Dot(P2(line.EndPoint) - at, way));
                    return reach > tolerance.EqualPoint ? Trimmed.Line(line, at, way, at) : null;
                }

                double t = Turn(Math.Atan2(at.Y - _point.Y, at.X - _point.X));
                double p = Turn(Math.Atan2(pick.Y - _point.Y, pick.X - _point.X));
                double least = 2.0 * Math.PI - tolerance.EqualAngleRad;
                Trimmed best = null;

                // The run from the point to the end, keeping the end; or from the start to the point, keeping the start.
                foreach (bool keepEnd in new[] { true, false })
                {
                    double a0 = keepEnd ? t : _start;
                    double a1 = keepEnd ? _end : t;
                    double sweep = Turn(a1 - a0);

                    if (sweep <= tolerance.EqualAngleRad || sweep >= least || Turn(p - a0) > sweep)
                    {
                        continue;
                    }

                    least = sweep;
                    best = Trimmed.Arc(entity.Handle, a0, a1);
                }

                return best;
            }
        }
    }
}
