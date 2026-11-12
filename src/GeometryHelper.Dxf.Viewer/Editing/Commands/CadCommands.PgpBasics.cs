using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The basic commands of AutoCAD 2021's acad.pgp the presenter lacked: DIVIDE and MEASURE, DIMCENTER, COLOR and -COLOR,
    /// LWEIGHT and -LWEIGHT, REDRAW, REDRAWALL, REGENALL, and ISOLATEOBJECTS, HIDEOBJECTS and UNISOLATEOBJECTS; each asks
    /// AutoCAD's prompts, checked in its console, and makes one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        // AutoCAD's lineweights, in hundredths of a millimetre, as -LWEIGHT ? lists them.
        private static readonly int[] Lineweights =
        {
            0,
            5,
            9,
            13,
            15,
            18,
            20,
            25,
            30,
            35,
            40,
            50,
            53,
            60,
            70,
            80,
            90,
            100,
            106,
            120,
            140,
            158,
            200,
            211,
        };

        private static void AddPgpBasicsTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("REDRAW", Redraw));
            registry.Add(new CadCommand("REDRAWALL", Redraw));
            registry.Add(new CadCommand("REGENALL", Regen));
            registry.Add(new CadCommand("COLOR", Color));
            registry.Add(
                new CadCommand("-COLOR", c => CurrentColorAsked(c, "Enter default object color", ColorOffered))
            );
            registry.Add(new CadCommand("LWEIGHT", Lineweight));
            registry.Add(new CadCommand("-LWEIGHT", LineweightTyped));
            registry.Add(new CadCommand("DIVIDE", c => DivideOrMeasure(c, false)));
            registry.Add(new CadCommand("MEASURE", c => DivideOrMeasure(c, true)));
            registry.Add(new CadCommand("DIMCENTER", DimCenter));
            registry.Add(new CadCommand("ISOLATEOBJECTS", c => IsolateOrHide(c, true), true));
            registry.Add(new CadCommand("HIDEOBJECTS", c => IsolateOrHide(c, false), true));
            registry.Add(new CadCommand("UNISOLATEOBJECTS", Unisolate));
        }

        /// <summary>REDRAW and REDRAWALL: the window draws every change as it is made, so nothing is left to draw; AutoCAD says nothing either.</summary>
        private static IEnumerable<CadPrompt> Redraw(CadCommandContext c)
        {
            yield break;
        }

        /// <summary>COLOR: the window's Select Color dialog for the colour of new objects; with no window, -COLOR's prompt.</summary>
        private static IEnumerable<CadPrompt> Color(CadCommandContext c) =>
            c.Session.Request("COLOR")
                ? Array.Empty<CadPrompt>()
                : CurrentColorAsked(c, "Enter default object color", ColorOffered);

        /// <summary>The colour of new objects as -COLOR offers it: BYLAYER, BYBLOCK, "1 (red)" for the seven named, else the index.</summary>
        private static string ColorOffered(DxfColor color)
        {
            string name = CadPropertyEditor.ColorName(color);

            if (color.IsByLayer || color.IsByBlock)
            {
                return name.ToUpperInvariant();
            }

            return color.ColorIndex >= 1 && color.ColorIndex <= 7
                ? color.ColorIndex.ToString(CultureInfo.InvariantCulture) + " (" + name.ToLowerInvariant() + ")"
                : name;
        }

        /// <summary>LWEIGHT: the window's Lineweight Settings dialog; with no window, -LWEIGHT's prompt.</summary>
        private static IEnumerable<CadPrompt> Lineweight(CadCommandContext c) =>
            c.Session.Request("LWEIGHT") ? Array.Empty<CadPrompt>() : LineweightTyped(c);

        /// <summary>
        /// -LWEIGHT: the lineweight of new objects ($CELWEIGHT), as AutoCAD 2021 asks it: ByLayer, ByBlock, Default or a width
        /// in millimetres, rounded to the nearest of AutoCAD's with a word where it is not one ("0.33" gives 0.35 mm); ? lists
        /// them. One undo step; Enter keeps it.
        /// </summary>
        private static IEnumerable<CadPrompt> LineweightTyped(CadCommandContext c)
        {
            DxfLineWeight old = c.Database.Celweight;
            c.Write("Current lineweight: " + LineweightShown(old));

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText(
                    "Enter default lineweight for new objects or",
                    new[] { "?" },
                    null
                );

                if (c.Input.Is("?"))
                {
                    c.Write("ByLayer   ByBlock      Default");
                    c.Write("0.00 mm 0.05 mm  0.09 mm    0.13 mm 0.15 mm  0.18 mm");
                    c.Write("0.20 mm 0.25 mm  0.30 mm    0.35 mm 0.40 mm  0.50 mm");
                    c.Write("0.53 mm 0.60 mm  0.70 mm    0.80 mm 0.90 mm  1.00 mm");
                    c.Write("1.06 mm 1.20 mm  1.40 mm    1.58 mm 2.00 mm  2.11 mm");
                    continue;
                }

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                string typed = c.Input.Text.Trim().TrimStart('_');
                DxfLineWeight weight;

                if (string.Equals(typed, "BYLAYER", StringComparison.OrdinalIgnoreCase))
                {
                    weight = DxfLineWeight.ByLayer;
                }
                else if (string.Equals(typed, "BYBLOCK", StringComparison.OrdinalIgnoreCase))
                {
                    weight = DxfLineWeight.ByBlock;
                }
                else if (string.Equals(typed, "DEFAULT", StringComparison.OrdinalIgnoreCase))
                {
                    weight = DxfLineWeight.ByLineWeightDefault;
                }
                else if (
                    double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double millimetres)
                    && !double.IsNaN(millimetres)
                    && !double.IsInfinity(millimetres)
                )
                {
                    if (millimetres < 0.0)
                    {
                        c.Write("Invalid lweight number.");
                        continue;
                    }

                    int nearest = NearestLineweight(millimetres * 100.0);

                    if (Math.Abs(nearest - millimetres * 100.0) > 1E-9)
                    {
                        c.Write(
                            "Lineweight rounded to nearest valid value of "
                                + LineweightShown((DxfLineWeight)nearest).Replace("None", "0.00 mm")
                        );
                    }

                    weight = (DxfLineWeight)nearest;
                }
                else
                {
                    c.Write("A weight in lineweight units is required.");
                    continue;
                }

                if (weight != old)
                {
                    c.Apply(
                        new[]
                        {
                            new CadEditRecord(
                                CadEditRecordKind.SettingChanged,
                                null,
                                "$CELWEIGHT",
                                ((int)old).ToString(CultureInfo.InvariantCulture),
                                ((int)weight).ToString(CultureInfo.InvariantCulture)
                            ),
                        },
                        s => s.Database.Celweight = weight
                    );
                }

                yield break;
            }
        }

        /// <summary>A lineweight as -LWEIGHT writes the current one: BYLAYER, BYBLOCK, Default, None for nought, else "0.35 mm".</summary>
        private static string LineweightShown(DxfLineWeight weight)
        {
            switch (weight)
            {
                case DxfLineWeight.ByLayer:
                    return "BYLAYER";
                case DxfLineWeight.ByBlock:
                    return "BYBLOCK";
                case DxfLineWeight.ByLineWeightDefault:
                    return "Default";
                case DxfLineWeight.LineWeight000:
                    return "None";
                default:
                    return ((int)weight / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm";
            }
        }

        /// <summary>The lineweight of AutoCAD's nearest a width in hundredths of a millimetre; the thinner of two as near.</summary>
        private static int NearestLineweight(double hundredths)
        {
            int best = Lineweights[0];

            foreach (int weight in Lineweights)
            {
                if (Math.Abs(weight - hundredths) < Math.Abs(best - hundredths))
                {
                    best = weight;
                }
            }

            return best;
        }

        /// <summary>
        /// DIVIDE and MEASURE, as AutoCAD 2021's: points (or a block, turned along the curve unless told not to) at equal
        /// lengths along a line, arc, circle, ellipse, polyline or spline. DIVIDE puts one between each of the segments asked
        /// for, 2 to 32767, and on a closed curve one at its start too; MEASURE steps the length given from the end nearest
        /// the pick, from the start of a closed curve, a circle from angle nought, and says "Object isn't that long." where the
        /// length is past the curve. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> DivideOrMeasure(CadCommandContext c, bool measure)
        {
            yield return CadPrompt.ForEntity(measure ? "Select object to measure" : "Select object to divide");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            GeoPoint2? pickedAt = c.Input.IsPickedAt ? c.Input.Point : (GeoPoint2?)null;
            var curve = c.Document.FindEntity(c.Input.Handles[0]) as DxfCurve;
            double total = curve == null || curve is DxfXline || curve is DxfRay ? 0.0 : CurveLength(curve);

            if (!(total > 0.0))
            {
                c.Write(measure ? "Cannot measure that object." : "Cannot divide that object.");
                yield break;
            }

            string block = null;
            bool align = true;
            double answer;

            while (true)
            {
                string[] options = block == null ? new[] { "Block" } : null;

                if (measure)
                {
                    yield return CadPrompt.ForDistance("Specify length of segment", pickedAt, options);
                }
                else
                {
                    yield return CadPrompt.ForInteger("Enter the number of segments", options);
                }

                if (block == null && c.Input.Is("Block"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForString("Enter name of block to insert");

                        if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                        {
                            yield break;
                        }

                        string name = c.Input.Text.Trim();

                        if (c.Database.BlockTable.Has(name))
                        {
                            block = c.Database.BlockTable[name].Name;
                            break;
                        }

                        c.Write("Cannot find block \"" + name + "\".");
                    }

                    yield return CadPrompt.ForKeyword("Align block with object?", new[] { "Yes", "No" }, "Y");
                    align = !c.Input.Is("No");
                    continue;
                }

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                answer = c.Input.Number;

                if (measure ? answer > 0.0 : answer >= 2 && answer <= 32767)
                {
                    break;
                }

                c.Write(
                    measure ? "Requires a nonzero positive distance."
                    : block == null ? "Requires an integer between 2 and 32767, or option keyword."
                    : "Requires an integer between 2 and 32767."
                );
            }

            bool closed = curve.Closed || curve.StartPoint.DistanceTo(curve.EndPoint) <= 1E-10 * Math.Max(1.0, total);
            var distances = new List<double>();

            if (!measure)
            {
                int segments = (int)answer;

                for (int k = closed ? 0 : 1; k < segments; k++)
                {
                    distances.Add(total * k / segments);
                }
            }
            else if (answer > total)
            {
                c.Write("Object isn't that long.");
                yield break;
            }
            else
            {
                // An open curve is measured from the end nearest the pick, as AutoCAD's.
                bool fromEnd =
                    !closed
                    && pickedAt.HasValue
                    && Flat(curve.EndPoint).DistanceTo(pickedAt.Value)
                        < Flat(curve.StartPoint).DistanceTo(pickedAt.Value);

                for (int k = 1; k * answer < total - 1E-10 * total; k++)
                {
                    distances.Add(fromEnd ? total - k * answer : k * answer);
                }
            }

            if (distances.Count == 0)
            {
                yield break;
            }

            CadSpace space = c.Space;
            c.Apply(null, s =>
            {
                foreach (double distance in distances)
                {
                    GeoPoint3 at = curve.GetPointAtDist(distance);

                    if (block == null)
                    {
                        s.Append(space, Current(c, new DxfDBPoint(at)));
                        continue;
                    }

                    var insert = Current(c, new DxfBlockReference(at, s.Database.BlockTable[block]));

                    if (align)
                    {
                        GeoVector3 along = curve.GetFirstDerivative(curve.GetParameterAtDistance(distance));
                        double angle = Math.Atan2(along.Y, along.X);
                        insert.Rotation = angle < 0.0 ? angle + 2.0 * Math.PI : angle;
                    }

                    s.Append(space, insert);
                }
            });
        }

        /// <summary>The length of a curve, nought where it cannot be measured.</summary>
        private static double CurveLength(DxfCurve curve)
        {
            try
            {
                return curve.GetDistanceAtParameter(curve.EndParam);
            }
            catch (Exception e)
                when (e is InvalidOperationException || e is ArgumentException || e is NotSupportedException)
            {
                return 0.0;
            }
        }

        /// <summary>
        /// DIMCENTER: the centre mark of DIMCEN (times DIMSCALE) on a circle or arc, as lines of the current layer and
        /// properties: a cross DIMCEN long each way, and where DIMCEN is below nought centre lines from twice its length out to
        /// its length past the rim, where the radius is over twice it, as AutoCAD 2021 draws them. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> DimCenter(CadCommandContext c)
        {
            DxfDimStyleTableRecord style = DimStyle(c);
            double dimcen = style?.Dimcen ?? c.Database.Dimcen;

            if (dimcen == 0.0)
            {
                c.Write("DIMCEN = 0.0, not drawing center cross. *Invalid*");
                yield break;
            }

            yield return CadPrompt.ForEntity("Select arc or circle");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            GeoPoint2 center;
            double radius;

            switch (c.Document.FindEntity(c.Input.Handles[0]))
            {
                case DxfCircle circle:
                    center = Flat(circle.Center);
                    radius = circle.Radius;
                    break;
                case DxfArc arc:
                    center = Flat(arc.Center);
                    radius = arc.Radius;
                    break;
                default:
                    c.Write("Object selected is not a circle or arc.");
                    yield break;
            }

            double mark = Math.Abs(dimcen) * Dimscale(c);
            var x = new GeoVector2(1.0, 0.0);
            var y = new GeoVector2(0.0, 1.0);
            var lines = new List<DxfLine>
            {
                new DxfLine(P3(center - x * mark), P3(center + x * mark)),
                new DxfLine(P3(center - y * mark), P3(center + y * mark)),
            };

            if (dimcen < 0.0 && CentreLinesFit(radius, mark))
            {
                foreach (GeoVector2 way in new[] { x, -x, y, -y })
                {
                    lines.Add(new DxfLine(P3(center + way * (2.0 * mark)), P3(center + way * (radius + mark))));
                }
            }

            CadSpace space = c.Space;
            c.Apply(null, s =>
            {
                foreach (DxfLine line in lines)
                {
                    s.Append(space, Current(c, line));
                }
            });
        }

        /// <summary>Whether centre lines are drawn past a mark: where the radius is over twice the mark, as AutoCAD 2021 (5.01 under 2.5 has them, 5 none).</summary>
        private static bool CentreLinesFit(double radius, double mark) => radius > 2.0 * mark;

        /// <summary>
        /// ISOLATEOBJECTS and HIDEOBJECTS: the other objects of the space, or those selected, hidden as AutoCAD 2021 hides them
        /// (their Visible off, group 60) for the session, as its OBJECTISOLATIONMODE 0: the drawing remembers them and writes
        /// them shown when saved. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> IsolateOrHide(CadCommandContext c, bool isolate)
        {
            // R12 writes no visibility of an entity (group 60 came with R13).
            if (string.Equals(c.Database.Version, "AC1009", StringComparison.OrdinalIgnoreCase))
            {
                c.Write("A drawing of R12 cannot hide objects: save it in a later version first.");
                yield break;
            }

            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            var hidden = new List<string>();

            if (isolate)
            {
                var kept = new HashSet<string>(c.Selected, StringComparer.OrdinalIgnoreCase);

                foreach (DxfEntity entity in c.Space.Block)
                {
                    if (
                        !string.IsNullOrEmpty(entity.Handle)
                        && !kept.Contains(entity.Handle)
                        && !(entity is DxfViewport)
                        && entity.Visible
                    )
                    {
                        hidden.Add(entity.Handle);
                    }
                }
            }
            else
            {
                foreach (string handle in c.Selected)
                {
                    DxfEntity entity = c.Document.FindEntity(handle);

                    if (entity != null && !(entity is DxfViewport) && entity.Visible)
                    {
                        hidden.Add(handle);
                    }
                }
            }

            SetShown(c, hidden, false);

            foreach (string handle in hidden)
            {
                c.Document.IsolationHidden.Add(handle);
            }
        }

        /// <summary>
        /// UNISOLATEOBJECTS: the objects ISOLATEOBJECTS and HIDEOBJECTS hid shown again, "1 object(s) unisolated.", or "No
        /// objects to unisolate." where none is hidden still (erased, or shown again by U). One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> Unisolate(CadCommandContext c)
        {
            var shown = new List<string>();

            foreach (string handle in c.Document.IsolationHidden)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity != null && !entity.Visible)
                {
                    shown.Add(handle);
                }
            }

            c.Document.IsolationHidden.Clear();

            if (shown.Count == 0)
            {
                c.Write("No objects to unisolate.");
                yield break;
            }

            SetShown(c, shown, true);
            c.Write(shown.Count.ToString(CultureInfo.InvariantCulture) + " object(s) unisolated.");
        }

        private static void SetShown(CadCommandContext c, IReadOnlyList<string> handles, bool visible)
        {
            if (handles.Count == 0)
            {
                return;
            }

            var records = new List<CadEditRecord>();

            foreach (string handle in handles)
            {
                records.Add(
                    new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        handle,
                        "Visible",
                        visible ? "No" : "Yes",
                        visible ? "Yes" : "No"
                    )
                );
            }

            c.Apply(records, s =>
            {
                foreach (string handle in handles)
                {
                    DxfEntity entity = s.Document.FindEntity(handle);

                    if (entity != null)
                    {
                        entity.Visible = visible;
                    }
                }
            });
        }
    }
}
