using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The commands of the user coordinate system: UCS, UCSICON, PLAN and UCSMAN, with AutoCAD's prompts. The presenter shows
    /// the drawing from above, so a system stays in the plan: its origin moves and it turns about Z, and the options that
    /// tilt it out of the plan (X, Y, Face, a Z axis off upright) say so and change nothing.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] UcsOptions =
        {
            "Face",
            "NAmed",
            "OBject",
            "Previous",
            "View",
            "World",
            "X",
            "Y",
            "Z",
            "ZAxis",
        };

        private const string NotInPlan =
            "The presenter shows the drawing from above: a UCS that leaves the plan is not available here.";

        /// <summary>
        /// UCS, as AutoCAD 2021 asks it: an origin, then a point on the X axis and one in the XY plane, each Enter keeping what
        /// it would change; or an option. M (Move), 3 (3point), S, R, D, ? and G (orthoGraphic) of the older prompts are taken
        /// as well.
        /// </summary>
        private static IEnumerable<CadPrompt> Ucs(CadCommandContext c)
        {
            CadUcsManager systems = c.Document.Ucs;
            c.Write("Current ucs name:  " + UcsName(systems.Current));
            yield return CadPrompt.ForPoint("Specify origin of UCS", null, UcsOptions, "World", true);
            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().ToUpperInvariant() : null;

            if (c.Input.IsNone || c.Input.Is("World"))
            {
                systems.SetCurrent(CadUcs.World);
            }
            else if (c.Input.IsPoint)
            {
                foreach (CadPrompt prompt in UcsByPoints(c, c.Input.Point))
                {
                    yield return prompt;
                }
            }
            else if (typed == "M" || typed == "MOVE" || typed == "3" || typed == "3POINT")
            {
                yield return CadPrompt.ForPoint("Specify new origin point", null, null, "0,0,0");
                GeoPoint2 origin = c.Input.IsPoint ? c.Input.Point : Origin2(c.Session.Ucs);

                if (typed.StartsWith("M", StringComparison.Ordinal))
                {
                    systems.SetCurrent(Moved(c.Session.Ucs, origin));
                }
                else
                {
                    foreach (CadPrompt prompt in UcsByPoints(c, origin))
                    {
                        yield return prompt;
                    }
                }
            }
            else if (c.Input.Is("Previous"))
            {
                if (!systems.Previous())
                {
                    c.Write("No previous UCS saved.");
                }
            }
            else if (c.Input.Is("View"))
            {
                // The view is always the plan, X to the right and Y up: the world's axes at the origin kept.
                systems.SetCurrent(Turned(c.Session.Ucs, Origin2(c.Session.Ucs), 0.0));
            }
            else if (c.Input.Is("Z"))
            {
                yield return CadPrompt
                    .ForAngle("Specify rotation angle about Z axis", Origin2(c.Session.Ucs), null, "90")
                    .Turning();

                if (c.Input.IsNone || c.Input.Kind == CadInputKind.Number)
                {
                    CadUcs ucs = c.Session.Ucs;
                    systems.SetCurrent(
                        Turned(ucs, Origin2(ucs), ucs.AngleToWorld(c.Input.IsNone ? Math.PI / 2.0 : c.Input.Number))
                    );
                }
            }
            else if (c.Input.Is("ZAxis"))
            {
                foreach (CadPrompt prompt in UcsZAxis(c))
                {
                    yield return prompt;
                }
            }
            else if (c.Input.Is("OBject"))
            {
                yield return CadPrompt.ForEntity("Select object to align UCS");

                if (c.Input.Kind == CadInputKind.Selection && c.Input.Handles.Count > 0)
                {
                    CadUcs aligned = UcsOfObject(
                        c.Document.FindEntity(c.Input.Handles[0]),
                        c.Input.Point,
                        c.Session.Tolerance
                    );

                    if (aligned == null)
                    {
                        c.Write("Could not use object to define UCS.");
                    }
                    else
                    {
                        systems.SetCurrent(aligned);
                    }
                }
            }
            else if (c.Input.Is("NAmed"))
            {
                yield return CadPrompt.ForKeyword("Enter an option", new[] { "Restore", "Save", "Delete", "?" });

                foreach (CadPrompt prompt in UcsNamed(c, c.Input.Kind == CadInputKind.Keyword ? c.Input.Text : null))
                {
                    yield return prompt;
                }
            }
            else if (
                typed == "S"
                || typed == "SAVE"
                || typed == "R"
                || typed == "RESTORE"
                || typed == "D"
                || typed == "DEL"
                || typed == "DELETE"
                || typed == "?"
            )
            {
                string option =
                    typed == "?" ? "?"
                    : typed.StartsWith("S", StringComparison.Ordinal) ? "Save"
                    : typed.StartsWith("R", StringComparison.Ordinal) ? "Restore"
                    : "Delete";

                foreach (CadPrompt prompt in UcsNamed(c, option))
                {
                    yield return prompt;
                }
            }
            else if (typed == "G" || typed == "ORTHOGRAPHIC")
            {
                yield return CadPrompt.ForKeyword(
                    "Enter an option",
                    new[] { "Top", "Bottom", "Front", "Back", "Left", "Right" },
                    "Top"
                );

                if (c.Input.IsNone || c.Input.Is("Top"))
                {
                    systems.SetCurrent(CadUcs.World);
                }
                else if (c.Input.Kind == CadInputKind.Keyword)
                {
                    c.Write(NotInPlan);
                }
            }
            else if (typed == "A" || typed == "APPLY")
            {
                c.Write("Each space has one view here, which the current UCS applies to already.");
            }
            else if (c.Input.Is("X") || c.Input.Is("Y") || c.Input.Is("Face"))
            {
                c.Write(
                    "Rotation about the X or Y axis and a UCS on a face are not available in a 2D view. " + NotInPlan
                );
            }
            else if (typed != null)
            {
                c.Write("Point or option keyword required.");
            }
        }

        /// <summary>UCS by an origin given, then a point on the positive X axis and one on the positive Y side, each Enter keeping the axes as they are.</summary>
        private static IEnumerable<CadPrompt> UcsByPoints(CadCommandContext c, GeoPoint2 origin)
        {
            CadUcs now = c.Session.Ucs;
            double tolerance = c.Session.Tolerance.EqualPoint;
            yield return CadPrompt.ForPoint("Specify point on X-axis", origin, null, "Accept");

            if (c.Input.IsNone)
            {
                c.Document.Ucs.SetCurrent(Moved(now, origin));
                yield break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoVector2 x = c.Input.Point - origin;

            if (x.Length <= tolerance)
            {
                c.Write("*Invalid* The point on the X axis is the origin.");
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify point on the XY plane", origin, null, "Accept");

            if (c.Input.IsPoint)
            {
                GeoVector2 y = c.Input.Point - origin;
                double side = (x.X * y.Y - x.Y * y.X) / x.Length;

                if (Math.Abs(side) <= tolerance)
                {
                    c.Write("*Invalid* The point lies on the X axis.");
                    yield break;
                }

                if (side < 0.0)
                {
                    c.Write("The point lies on the side that turns the Z axis down. " + NotInPlan);
                    yield break;
                }
            }
            else if (!c.Input.IsNone)
            {
                yield break;
            }

            c.Document.Ucs.SetCurrent(Turned(now, origin, Math.Atan2(x.Y, x.X)));
        }

        /// <summary>UCS ZAxis: a new origin, and a Z axis that stays upright, as the plan needs.</summary>
        private static IEnumerable<CadPrompt> UcsZAxis(CadCommandContext c)
        {
            CadUcs now = c.Session.Ucs;
            yield return CadPrompt.ForPoint("Specify new origin point", null, new[] { "Object" }, "0,0,0");

            if (c.Input.Is("Object"))
            {
                c.Write("A Z axis along an object lies in the plan. " + NotInPlan);
                yield break;
            }

            if (!c.Input.IsPoint && !c.Input.IsNone)
            {
                yield break;
            }

            GeoPoint2 origin = c.Input.IsPoint ? c.Input.Point : Origin2(now);
            yield return CadPrompt.ForPoint("Specify point on positive portion of Z-axis", origin, null, "Accept");

            if (c.Input.IsPoint && (c.Input.Point - origin).Length > c.Session.Tolerance.EqualPoint)
            {
                c.Write("The Z axis has to stay upright in a 2D view, a point picked lies in the plan. " + NotInPlan);
                yield break;
            }

            if (c.Input.IsPoint || c.Input.IsNone)
            {
                c.Document.Ucs.SetCurrent(Moved(now, origin));
            }
        }

        /// <summary>UCS NAmed, Restore, Save, Delete and ?.</summary>
        private static IEnumerable<CadPrompt> UcsNamed(CadCommandContext c, string option)
        {
            CadUcsManager systems = c.Document.Ucs;

            if (option == "?")
            {
                yield return CadPrompt.ForString("Enter UCS name(s) to list", "*");
                ListUcs(c, c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text : "*");
                yield break;
            }

            if (option == "Restore")
            {
                yield return CadPrompt.ForString("Enter name of UCS to restore or [?]");
                string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                if (name == "?")
                {
                    ListUcs(c, "*");
                }
                else if (name.Length > 0)
                {
                    CadUcs found = systems.Find(name);

                    if (found == null)
                    {
                        c.Write("Cannot find UCS " + name + ".");
                    }
                    else if (!systems.Restore(name))
                    {
                        c.Write(
                            "The UCS " + found.Name + " is not parallel to the view, and is kept as it is. " + NotInPlan
                        );
                    }
                }

                yield break;
            }

            if (option == "Save")
            {
                yield return CadPrompt.ForString("Enter name to save current UCS or [?]");
                string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                if (name == "?")
                {
                    ListUcs(c, "*");
                    yield break;
                }

                if (name.Length == 0)
                {
                    yield break;
                }

                if (systems.Find(name) != null)
                {
                    yield return CadPrompt.ForKeyword(
                        "UCS " + systems.Find(name).Name + " already exists. Replace it?",
                        new[] { "Yes", "No" },
                        "No"
                    );

                    if (!c.Input.Is("Yes"))
                    {
                        yield break;
                    }
                }

                try
                {
                    systems.Save(name);
                }
                catch (ArgumentException exception)
                {
                    c.Write("*Invalid* " + exception.Message);
                }

                yield break;
            }

            if (option == "Delete")
            {
                yield return CadPrompt.ForString("Enter UCS name(s) to delete", "none");
                string names = c.Input.Kind == CadInputKind.Text ? c.Input.Text : string.Empty;
                int deleted = 0;

                foreach (CadUcs ucs in systems.Named)
                {
                    if (Matches(ucs.Name, names) && systems.Delete(ucs.Name))
                    {
                        deleted++;
                    }
                }

                c.Write(
                    deleted == 0
                        ? "No matching UCS names found."
                        : deleted.ToString(CultureInfo.InvariantCulture)
                            + " UCS"
                            + (deleted == 1 ? string.Empty : "s")
                            + " deleted."
                );
            }
        }

        /// <summary>
        /// UCSICON: ON, OFF, Noorigin (the icon at the lower left corner) and ORigin (at the origin where it is in view). All
        /// asks again, each space having one view; Selectable and Properties are not available.
        /// </summary>
        private static IEnumerable<CadPrompt> UcsIcon(CadCommandContext c)
        {
            CadUcsManager systems = c.Document.Ucs;
            string[] options = { "ON", "OFF", "All", "Noorigin", "ORigin", "Selectable", "Properties" };
            yield return CadPrompt.ForKeyword("Enter an option", options, systems.IconOn ? "ON" : "OFF");

            if (c.Input.Is("All"))
            {
                yield return CadPrompt.ForKeyword(
                    "Enter an option",
                    new[] { "ON", "OFF", "Noorigin", "ORigin" },
                    systems.IconOn ? "ON" : "OFF"
                );
            }

            if (c.Input.Is("ON") || c.Input.Is("OFF"))
            {
                systems.IconOn = c.Input.Is("ON");
            }
            else if (c.Input.Is("Noorigin") || c.Input.Is("ORigin"))
            {
                systems.IconAtOrigin = c.Input.Is("ORigin");
            }
            else if (c.Input.Is("Selectable") || c.Input.Is("Properties"))
            {
                c.Write("The UCS icon is drawn in AutoCAD's 2D style here, and is not selected.");
            }
        }

        /// <summary>
        /// PLAN: the view of the current UCS, a named one or the world, zoomed to the extents. The view of the presenter is not
        /// turned: where the system is turned, the grid and the crosshair follow it instead.
        /// </summary>
        private static IEnumerable<CadPrompt> Plan(CadCommandContext c)
        {
            CadUcsManager systems = c.Document.Ucs;
            yield return CadPrompt.ForKeyword(
                "Enter an option",
                new[] { "Current ucs", "Ucs", "World" },
                "Current ucs"
            );
            CadUcs of = c.Session.Ucs;

            if (c.Input.Is("World"))
            {
                of = CadUcs.World;
            }
            else if (c.Input.Is("Ucs"))
            {
                yield return CadPrompt.ForString("Enter name of UCS or [?]");
                string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                if (name == "?")
                {
                    ListUcs(c, "*");
                    yield break;
                }

                of = systems.Find(name);

                if (of == null)
                {
                    c.Write("Cannot find UCS " + name + ".");
                    yield break;
                }
            }
            else if (!c.Input.IsNone && !c.Input.Is("Current ucs"))
            {
                yield break;
            }

            if (Math.Abs(Math.Sin(of.Angle)) > c.Session.Tolerance.EqualVector || Math.Cos(of.Angle) < 0.0)
            {
                c.Write(
                    "PLAN does not turn this view: the world's Y stays up, and the grid and the crosshair follow the UCS instead."
                );
            }

            CadView view = c.Session.View;

            if (view != null)
            {
                c.Session.Views.Add(c.Document.CurrentSpace, view.VisibleBox);
                view.ZoomTo(c.Document.GetDisplay(c.Space).Bounds.Union(c.Space.PaperBounds));
            }
        }

        /// <summary>UCSMAN: the UCS dialog of a window that has one; else the named systems listed on the command line.</summary>
        private static IEnumerable<CadPrompt> UcsManager(CadCommandContext c)
        {
            if (!c.Session.Request("UCSMAN"))
            {
                c.Write(
                    "There is no UCS dialog here: the named systems are listed below, and UCS NAmed restores, saves and deletes them."
                );
                ListUcs(c, "*");
            }

            yield break;
        }

        /// <summary>The list of UCS ?, as AutoCAD writes it: each named system matching, its origin and axes in the coordinates of the current UCS.</summary>
        private static void ListUcs(CadCommandContext c, string pattern)
        {
            CadUcs current = c.Session.Ucs;
            c.Write("Current ucs name:  " + UcsName(c.Document.Ucs.Current));
            c.Write("Saved coordinate systems:");

            foreach (CadUcs ucs in c.Document.Ucs.Named)
            {
                if (!Matches(ucs.Name, pattern))
                {
                    continue;
                }

                GeoPoint3 origin = current.ToUcs(ucs.Origin);
                c.Write(string.Empty);
                c.Write(
                    "\""
                        + ucs.Name
                        + "\""
                        + (ucs.IsPlanar ? string.Empty : "  (not parallel to the view: kept, not made current here)")
                );
                c.Write("Origin = " + Triple(origin.X, origin.Y, origin.Z) + ", X Axis = " + Axis(current, ucs.XAxis));
                c.Write("Y Axis = " + Axis(current, ucs.YAxis) + ", Z Axis = " + Axis(current, ucs.ZAxis));
            }
        }

        /// <summary>The system an object gives, as UCS OBject does; null for an object it takes none from.</summary>
        private static CadUcs UcsOfObject(DxfEntity entity, GeoPoint2 picked, Tolerance tolerance)
        {
            switch (entity)
            {
                case DxfLine line:
                    // The origin at the end nearer the pick, the X axis along the line to the other.
                    GeoPoint2 start = Flat(line.StartPoint);
                    GeoPoint2 end = Flat(line.EndPoint);
                    bool fromEnd = (picked - end).Length < (picked - start).Length;
                    return Through(fromEnd ? end : start, fromEnd ? start : end, tolerance);
                case DxfCircle circle:
                    // The origin at the centre, the X axis through the pick.
                    return Through(Flat(circle.Center), picked, tolerance) ?? CadUcs.InPlane(Flat(circle.Center), 0.0);
                case DxfArc arc:
                    // The origin at the centre, the X axis through the end nearer the pick.
                    GeoPoint2 centre = Flat(arc.Center);
                    GeoPoint2 first =
                        centre + new GeoVector2(Math.Cos(arc.StartAngle), Math.Sin(arc.StartAngle)) * arc.Radius;
                    GeoPoint2 last =
                        centre + new GeoVector2(Math.Cos(arc.EndAngle), Math.Sin(arc.EndAngle)) * arc.Radius;
                    return Through(centre, (picked - last).Length < (picked - first).Length ? last : first, tolerance);
                case DxfPolyline polyline when polyline.NumberOfVertices >= 2:
                    // The origin at the start, the X axis along the first segment.
                    return Through(polyline.GetPoint2dAt(0), polyline.GetPoint2dAt(1), tolerance);
                case DxfDBText text:
                    return CadUcs.InPlane(Flat(text.Position), text.Rotation);
                case DxfMText mtext:
                    return CadUcs.InPlane(Flat(mtext.Location), mtext.Rotation);
                case DxfBlockReference insert:
                    return CadUcs.InPlane(Flat(insert.Position), insert.Rotation);
                case DxfDBPoint point:
                    return CadUcs.InPlane(Flat(point.Position), 0.0);
                default:
                    return null;
            }
        }

        /// <summary>A system at a point, its X axis towards another; null where the two are one point.</summary>
        private static CadUcs Through(GeoPoint2 origin, GeoPoint2 toward, Tolerance tolerance)
        {
            GeoVector2 x = toward - origin;
            return x.Length <= tolerance.EqualPoint ? null : CadUcs.InPlane(origin, Math.Atan2(x.Y, x.X));
        }

        /// <summary>A system moved to a new origin, its axes and the height of its origin kept.</summary>
        private static CadUcs Moved(CadUcs ucs, GeoPoint2 origin) =>
            new CadUcs(new GeoPoint3(origin.X, origin.Y, ucs.Origin.Z), ucs.XAxis, ucs.YAxis);

        /// <summary>A system in the plan at an origin, its X axis at an angle from the world's, the height of the origin kept.</summary>
        private static CadUcs Turned(CadUcs ucs, GeoPoint2 origin, double angle)
        {
            CadUcs turned = CadUcs.InPlane(origin, angle);
            return new CadUcs(new GeoPoint3(origin.X, origin.Y, ucs.Origin.Z), turned.XAxis, turned.YAxis);
        }

        private static GeoPoint2 Origin2(CadUcs ucs) => new GeoPoint2(ucs.Origin.X, ucs.Origin.Y);

        private static GeoPoint2 Flat(GeoPoint3 point) => new GeoPoint2(point.X, point.Y);

        /// <summary>The name AutoCAD writes for a system: its own, *WORLD* or *NO NAME*.</summary>
        private static string UcsName(CadUcs ucs) => ucs.Name ?? (ucs.IsWorld ? "*WORLD*" : "*NO NAME*");

        /// <summary>Whether a name matches one of the names or patterns typed, parted by commas, * and ? as wildcards, letters of either case the same.</summary>
        private static bool Matches(string name, string patterns)
        {
            foreach (string part in (patterns ?? string.Empty).Split(','))
            {
                string pattern = part.Trim();

                if (
                    pattern.Length > 0
                    && Regex.IsMatch(
                        name,
                        "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }

        private static string Axis(CadUcs current, GeoVector3 axis) =>
            Triple(axis.DotProduct(current.XAxis), axis.DotProduct(current.YAxis), axis.DotProduct(current.ZAxis));

        private static string Triple(double x, double y, double z) =>
            "<" + Length(x) + "," + Length(y) + "," + Length(z) + ">";
    }
}
