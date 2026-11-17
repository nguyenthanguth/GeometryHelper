using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The commands of PDF underlays, in AutoCAD 2021's words: PDFATTACH and -PDFATTACH (a page of a PDF placed as a PDFUNDERLAY),
    /// PDFADJUST and -PDFADJUST (fade, contrast and monochrome), PDFCLIP (the boundary), PDFLAYERS (the PDF's layers on and off),
    /// the variables PDFFRAME, FRAME and PDFOSNAP, and -PDFUNDERLAY, the typed form the External References palette runs to
    /// list, detach, unload, reload and re-path the definitions of PDF underlays, as -IMAGE does for pictures. Each change of
    /// the drawing is one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] PdfUnderlayOptions = { "?", "Detach", "Path", "Reload", "Unload" };
        private static readonly string[] PdfAdjustOptions = { "Fade", "Contrast", "Monochrome" };
        private static readonly string[] PdfUnits =
        {
            "MM",
            "Centimeter",
            "Meter",
            "Kilometer",
            "Inch",
            "Foot",
            "Yard",
            "MILe",
            "Unitless",
        };

        internal static void AddPdfTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("PDFATTACH", PdfAttach));
            registry.Add(new CadCommand("-PDFATTACH", PdfAttachTyped));
            registry.Add(new CadCommand("PDFADJUST", PdfAdjust, true));
            registry.Add(new CadCommand("-PDFADJUST", PdfAdjust, true));
            registry.Add(new CadCommand("PDFCLIP", PdfClip));
            registry.Add(new CadCommand("PDFLAYERS", PdfLayers));
            registry.Add(new CadCommand("-PDFUNDERLAY", PdfUnderlayTyped));

            // Whether the frames of PDF underlays are drawn and plotted: none, shown and plotted, shown and left off a plot.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "PDFFRAME",
                    false,
                    0,
                    2,
                    s => s.Document.PdfFrame,
                    (c, v) =>
                    {
                        c.Document.PdfFrame = v;
                        c.Document.Regen();
                    }
                )
            );

            // Whether object snaps find the geometry of PDF underlays.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "PDFOSNAP",
                    false,
                    0,
                    1,
                    s => s.Document.PdfOsnap ? 1 : 0,
                    (c, v) =>
                    {
                        c.Document.PdfOsnap = v != 0;
                        c.Document.Regen();
                    }
                )
            );

            // The frames of images and PDF underlays set together: 0 none, 1 shown and plotted, 2 shown and not plotted; 3 where the
            // two differ, as AutoCAD reads FRAME of a drawing whose frames are set differently.
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "FRAME",
                    false,
                    0,
                    2,
                    s =>
                    {
                        int images = ImageFrameOf(s);
                        return images == s.Document.PdfFrame ? images : 3;
                    },
                    (c, v) =>
                    {
                        (ImageVariables(c.Database) ?? DxfRasterVariables.GetRasterVariables(c.Database)).ImageFrame =
                            FrameSettingOf(v);
                        c.Document.PdfFrame = v;
                        c.Document.Regen();
                    }
                )
            );
        }

        #region PDFATTACH

        /// <summary>
        /// PDFATTACH: the window's Attach PDF Underlay dialog, after the file dialog or, with FILEDIA 0, "Path to PDF file to
        /// attach:"; OK attaches each page chosen as an underlay, asking on the command line what is to be specified on screen.
        /// With no window, -PDFATTACH's prompts.
        /// </summary>
        private static IEnumerable<CadPrompt> PdfAttach(CadCommandContext c)
        {
            var request = new CadAttachRequest(c.Document, CadAttachKind.Pdf);

            if (!c.Session.FileDialogs)
            {
                yield return CadPrompt.ForString("Path to PDF file to attach");
                string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;

                if (typed.Length == 0)
                {
                    yield break;
                }

                request.FileName = FileOf(c, typed, ".pdf");

                if (request.Error != null)
                {
                    c.Write(request.Error);
                    yield break;
                }
            }

            if (!c.Session.Request("PDFATTACH", c.Document, request))
            {
                // With no window, the file typed already for FILEDIA 0 asks its page and placing as the typed command does.
                foreach (CadPrompt prompt in PdfAttachTyped(c, request))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (request.Action != CadDialogAction.Ok)
            {
                yield break;
            }

            foreach (CadPrompt prompt in PlacePdf(c, request, false))
            {
                yield return prompt;
            }
        }

        private static IEnumerable<CadPrompt> PdfAttachTyped(CadCommandContext c) =>
            PdfAttachTyped(c, new CadAttachRequest(c.Document, CadAttachKind.Pdf));

        /// <summary>
        /// -PDFATTACH: "Path to PDF file to attach:", "Enter page number or [?] &lt;1&gt;:" (? lists the pages), then the insertion
        /// point, the scale and the rotation as -IMAGE Attach asks them.
        /// </summary>
        private static IEnumerable<CadPrompt> PdfAttachTyped(CadCommandContext c, CadAttachRequest request)
        {
            if (request.FileName == null)
            {
                yield return CadPrompt.ForString("Path to PDF file to attach");
                string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;

                if (typed.Length == 0)
                {
                    yield break;
                }

                request.FileName = FileOf(c, typed, ".pdf");
            }

            if (request.Error != null)
            {
                c.Write(request.Error);
                yield break;
            }

            while (true)
            {
                yield return CadPrompt.ForInteger("Enter page number or", new[] { "?" }, "1");

                if (c.Input.Kind == CadInputKind.Keyword)
                {
                    yield return CadPrompt.ForString("Enter page(s) to list", "*");
                    ListPdfPages(c, request, c.Input.Kind == CadInputKind.Text ? c.Input.Text : "*");
                    continue;
                }

                int page = 1;

                if (c.Input.Kind == CadInputKind.Number)
                {
                    page = (int)c.Input.Number;
                }
                else if (!c.Input.IsNone)
                {
                    yield break;
                }

                if (page < 1 || page > request.PageCount)
                {
                    c.Write(
                        "Requires an integer value between 1 and "
                            + request.PageCount.ToString(CultureInfo.InvariantCulture)
                            + "."
                    );
                    continue;
                }

                request.SelectedPages.Clear();
                request.SelectedPages.Add(page);
                break;
            }

            request.SpecifyScaleOnScreen = true;
            request.SpecifyRotationOnScreen = true;

            foreach (CadPrompt prompt in PlacePdf(c, request, true))
            {
                yield return prompt;
            }
        }

        /// <summary>-PDFATTACH ?: the pages whose numbers match, each as "Page 1 - 8.5 x 11 inches"; "No matching pages found." where none does.</summary>
        private static void ListPdfPages(CadCommandContext c, CadAttachRequest request, string wanted)
        {
            string[] patterns = wanted.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            int listed = 0;

            for (int page = 1; page <= request.PageCount; page++)
            {
                string number = page.ToString(CultureInfo.InvariantCulture);

                if (patterns.Any(p => CadWildcard.IsMatch(number, p, true)))
                {
                    c.Write(
                        "Page "
                            + number
                            + " - "
                            + Plain(request.PageWidth(page))
                            + " x "
                            + Plain(request.PageHeight(page))
                            + " inches"
                    );
                    listed++;
                }
            }

            if (listed == 0)
            {
                c.Write("No matching pages found.");
            }
        }

        /// <summary>The factor that makes the inches of a PDF page the unit asked: the scale of a page in a drawing of a unit, as the Unit option of the scale prompt sets it.</summary>
        internal static double PdfUnitScale(string unit, DxfUnits drawing)
        {
            double from;

            switch (unit)
            {
                case "MM":
                    from = 1.0;
                    break;
                case "Centimeter":
                    from = 10.0;
                    break;
                case "Meter":
                    from = 1000.0;
                    break;
                case "Kilometer":
                    from = 1E6;
                    break;
                case "Inch":
                    from = 25.4;
                    break;
                case "Foot":
                    from = 304.8;
                    break;
                case "Yard":
                    from = 914.4;
                    break;
                case "MILe":
                    from = 1609344.0;
                    break;
                default:
                    return 1.0;
            }

            double to = UnitMillimetres(drawing);
            return to > 0.0 ? from / to : 1.0;
        }

        /// <summary>
        /// Attaches the pages a request chooses and places their underlays, in one undo step, as AutoCAD 2021 asks: "Specify
        /// insertion point &lt;0,0&gt;:", "Base image size: Width: 2.777778, Height: 1.388889, Inches", "Specify scale factor or
        /// [Unit] &lt;1&gt;:" and "Specify rotation angle &lt;0&gt;:". A page of a file already attached is shown by the
        /// definition it has; each page chosen is placed at the one point, scale and rotation.
        /// </summary>
        private static IEnumerable<CadPrompt> PlacePdf(CadCommandContext c, CadAttachRequest request, bool typed)
        {
            if (request.Error != null)
            {
                c.Write(request.Error);
                yield break;
            }

            if (request.SelectedPages.Count == 0)
            {
                yield break;
            }

            if (
                c.Database.NamedObjectsDictionary == null
                || string.CompareOrdinal(c.Database.Version ?? "AC1032", "AC1024") < 0
            )
            {
                c.Write("A drawing older than AutoCAD 2010 cannot hold a PDF underlay.");
                yield break;
            }

            GeoPoint3 point = request.Point;
            double scale = request.Scale.X;
            double rotation = request.Rotation;

            if (request.SpecifyPointOnScreen)
            {
                yield return CadPrompt.ForPoint("Specify insertion point", null, null, typed ? "0,0" : null);

                if (c.Input.IsPoint)
                {
                    point = P3(c.Input.Point);
                }
                else if (!c.Input.IsNone || !typed)
                {
                    yield break;
                }
            }

            if (request.SpecifyScaleOnScreen)
            {
                int first = request.SelectedPages[0];
                c.Write(
                    "Base image size: Width: "
                        + Plain(request.PageWidth(first))
                        + ", Height: "
                        + Plain(request.PageHeight(first))
                        + ", "
                        + UnitName(c.Database.Insunits)
                );

                bool again = true;
                string offered = Plain(scale);

                while (again)
                {
                    again = false;
                    yield return CadPrompt.ForDistance(
                        "Specify scale factor",
                        new GeoPoint2(point.X, point.Y),
                        new[] { "Unit" },
                        offered
                    );

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        yield return CadPrompt.ForKeyword("Enter unit", PdfUnits, "Unitless");

                        if (c.Input.Kind == CadInputKind.Keyword)
                        {
                            scale = PdfUnitScale(c.Input.Text, c.Database.Insunits);
                            offered = Plain(scale);
                            again = true;
                        }
                    }
                    else if (c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0)
                    {
                        scale = c.Input.Number;
                    }
                }
            }

            if (request.SpecifyRotationOnScreen)
            {
                yield return CadPrompt
                    .ForAngle("Specify rotation angle", new GeoPoint2(point.X, point.Y), null, Number(0.0))
                    .Turning();

                if (c.Input.Kind == CadInputKind.Number)
                {
                    rotation = c.Input.Number;
                }
            }

            double angle = c.Session.Ucs.AngleToWorld(rotation);
            string saved = request.SavedPath;
            CadSpace space = c.Space;
            string[] pages = request.SelectedPages.Select(p => p.ToString(CultureInfo.InvariantCulture)).ToArray();
            GeoPoint3 at = point;
            double factor = scale;

            c.Apply(null, s =>
            {
                DxfDBDictionary dictionary = DxfPdfDefinition.CreatePdfDictionary(s.Database);

                foreach (string page in pages)
                {
                    DxfPdfDefinition definition = null;

                    foreach (DxfDBDictionaryEntry entry in dictionary)
                    {
                        if (
                            entry.Value is DxfPdfDefinition existing
                            && existing.ItemName == page
                            && string.Equals(existing.SourceFileName, saved, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            definition = existing;
                            break;
                        }
                    }

                    if (definition == null)
                    {
                        definition = new DxfPdfDefinition(saved, page);
                        dictionary.SetAt(DxfPdfDefinition.SuggestName(dictionary, saved, page), definition);
                    }

                    var underlay = new DxfPdfUnderlay(definition);
                    s.Append(space, Current(c, underlay));
                    underlay.Position = at;
                    underlay.ScaleFactors = new GeoVector3(factor, factor, factor);
                    underlay.Rotation = angle;
                }
            });
        }

        #endregion

        #region PDFADJUST

        /// <summary>
        /// PDFADJUST and -PDFADJUST: "Select PDF underlay:", then "Enter PDF underlay option [Fade/Contrast/Monochrome] &lt;Fade&gt;:",
        /// "Enter fade value (0-100) &lt;0&gt;:", "Enter contrast value (0-100) &lt;100&gt;:" or "Monochrome? [Yes/No] &lt;No&gt;:", for
        /// the underlays selected, which must all be of PDFs. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> PdfAdjust(CadCommandContext c)
        {
            foreach (
                CadPrompt prompt in c.SelectFirst(CadPrompt.ForSelection("Select PDF underlay"), e =>
                    e is DxfPdfUnderlay
                )
            )
            {
                yield return prompt;
            }

            List<string> handles = PdfUnderlaysOf(c, c.Input.Handles);

            if (handles == null || handles.Count == 0)
            {
                yield break;
            }

            var first = (DxfPdfUnderlay)c.Document.FindEntity(handles[0]);
            yield return CadPrompt.ForKeyword("Enter PDF underlay option", PdfAdjustOptions, "Fade");

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;

            if (option == "Monochrome")
            {
                yield return CadPrompt.ForKeyword(
                    "Monochrome?",
                    new[] { "Yes", "No" },
                    first.Monochrome ? "Yes" : "No"
                );

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                bool mono = c.Input.Is("Yes");
                SetPdfUnderlays(c, handles, "Monochrome", mono ? "On" : "Off", u => u.Monochrome = mono);
                yield break;
            }

            bool fade = option == "Fade";
            int now = fade ? first.Fade : first.Contrast;
            yield return CadPrompt.ForInteger(
                "Enter " + (fade ? "fade" : "contrast") + " value (0-100)",
                null,
                now.ToString(CultureInfo.InvariantCulture)
            );
            int value = now;

            if (c.Input.Kind == CadInputKind.Number)
            {
                value = (int)Math.Round(c.Input.Number);
            }
            else if (!c.Input.IsNone)
            {
                yield break;
            }

            if (value < 0 || value > 100)
            {
                c.Write("Requires an integer value between 0 and 100.");
                yield break;
            }

            SetPdfUnderlays(c, handles, fade ? "Fade" : "Contrast", value.ToString(CultureInfo.InvariantCulture), u =>
            {
                if (fade)
                {
                    u.Fade = value;
                }
                else
                {
                    u.Contrast = value;
                }
            });
        }

        /// <summary>The handles among those selected that are of PDF underlays on layers that are not locked; "Object selected was not a PDF underlay." where one is not.</summary>
        private static List<string> PdfUnderlaysOf(CadCommandContext c, IReadOnlyList<string> selected)
        {
            var handles = new List<string>();

            foreach (string handle in selected ?? Array.Empty<string>())
            {
                if (!(c.Document.FindEntity(handle) is DxfPdfUnderlay))
                {
                    c.Write("Object selected was not a PDF underlay.");
                    return null;
                }

                handles.Add(handle);
            }

            return handles;
        }

        /// <summary>Sets a property of the underlays of some handles in one undo step.</summary>
        private static void SetPdfUnderlays(
            CadCommandContext c,
            List<string> handles,
            string property,
            string text,
            Action<DxfPdfUnderlay> set
        )
        {
            c.Apply(
                handles
                    .Select(h => new CadEditRecord(CadEditRecordKind.PropertyChanged, h, property, null, text))
                    .ToList(),
                s =>
                {
                    foreach (string handle in handles)
                    {
                        set((DxfPdfUnderlay)s.Document.FindEntity(handle));
                    }
                }
            );
        }

        #endregion

        #region PDFCLIP

        /// <summary>
        /// PDFCLIP: "Select PDF to clip:", then "Enter PDF clipping option [ON/OFF/Delete/New boundary] &lt;New&gt;:": ON and OFF
        /// switch the clip, Delete takes the boundary away, New boundary asks a rectangle, a polygon or a closed polyline, Invert
        /// clip switching between hiding what lies outside and what lies inside, as IMAGECLIP does for pictures.
        /// </summary>
        private static IEnumerable<CadPrompt> PdfClip(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select PDF to clip");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            string handle = c.Input.Handles[0];

            if (!(c.Document.FindEntity(handle) is DxfPdfUnderlay picked))
            {
                c.Write("Object selected was not a PDF underlay.");
                yield break;
            }

            yield return CadPrompt.ForKeyword(
                "Enter PDF clipping option",
                new[] { "ON", "OFF", "Delete", "New boundary" },
                "New"
            );

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;

            if (option == "ON" || option == "OFF")
            {
                bool on = option == "ON";
                SetPdfUnderlays(c, new List<string> { handle }, "Clipping", on ? "On" : "Off", u => u.IsClipped = on);
                yield break;
            }

            if (option == "Delete")
            {
                SetPdfUnderlays(c, new List<string> { handle }, "Clip boundary", "Whole page", u =>
                    u.ClearClipBoundary()
                );
                yield break;
            }

            if (picked.HasClipBoundary)
            {
                yield return CadPrompt.ForKeyword("Delete old boundary?", new[] { "Yes", "No" }, "Yes");

                if (c.Input.Is("No"))
                {
                    yield break;
                }
            }

            bool inverted = false;
            List<GeoPoint2> corners = null;
            bool rectangle = true;

            while (corners == null)
            {
                c.Write(
                    inverted
                        ? "Inside mode - Objects inside boundary will be hidden."
                        : "Outside mode - Objects outside boundary will be hidden."
                );
                yield return CadPrompt.ForKeyword(
                    "Specify clipping boundary or select invert option",
                    new[] { "Select polyline", "Polygonal", "Rectangular", "Invert clip" },
                    "Rectangular"
                );

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                if (c.Input.Is("Invert clip"))
                {
                    inverted = !inverted;
                    continue;
                }

                if (c.Input.Is("Rectangular"))
                {
                    yield return CadPrompt.ForPoint("Specify first corner point");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoPoint2 first = c.Input.Point;
                    yield return CadPrompt.ForPoint("Specify opposite corner point", first);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoPoint2 second = c.Input.Point;
                    corners = new List<GeoPoint2>
                    {
                        first,
                        new GeoPoint2(second.X, first.Y),
                        second,
                        new GeoPoint2(first.X, second.Y),
                    };
                    rectangle = true;
                    continue;
                }

                if (c.Input.Is("Polygonal"))
                {
                    var points = new List<GeoPoint2>();
                    yield return CadPrompt.ForPoint("Specify first point");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    points.Add(c.Input.Point);

                    while (true)
                    {
                        yield return CadPrompt.ForPoint(
                            "Specify next point",
                            points[points.Count - 1],
                            points.Count >= 3 ? new[] { "Close", "Undo" } : new[] { "Undo" }
                        );

                        if (c.Input.IsPoint)
                        {
                            points.Add(c.Input.Point);
                            continue;
                        }

                        if (c.Input.Is("Undo"))
                        {
                            if (points.Count > 1)
                            {
                                points.RemoveAt(points.Count - 1);
                            }

                            continue;
                        }

                        break;
                    }

                    if (points.Count < 3)
                    {
                        yield break;
                    }

                    corners = points;
                    rectangle = false;
                    continue;
                }

                yield return CadPrompt.ForEntity("Select polyline");

                if (
                    c.Input.Kind != CadInputKind.Selection
                    || !(c.Document.FindEntity(c.Input.Handles[0]) is DxfPolyline outline)
                )
                {
                    c.Write("Selected object is not a polyline.");
                    yield break;
                }

                if (!outline.Closed || HasArcs(outline))
                {
                    c.Write("Invalid clipping boundary.");
                    yield break;
                }

                corners = new List<GeoPoint2>();

                for (int i = 0; i < outline.NumberOfVertices; i++)
                {
                    GeoPoint3 at = outline.GetPoint3dAt(i);
                    corners.Add(new GeoPoint2(at.X, at.Y));
                }

                rectangle = false;
            }

            if (PdfSelfIntersects(corners))
            {
                c.Write("Self-intersecting clip boundaries are not acceptable.");
                yield break;
            }

            List<GeoPoint2> world = corners;
            bool invert = inverted;
            bool asRectangle = rectangle;
            Tolerance tolerance = c.Session.Tolerance;
            c.Apply(
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        handle,
                        "Clip boundary",
                        null,
                        asRectangle ? "Rectangular" : "Polygonal"
                    ),
                },
                s =>
                {
                    var underlay = (DxfPdfUnderlay)s.Document.FindEntity(handle);
                    GeoTransform3 toPage = underlay.Transform.Inverse(tolerance);
                    var inches = new List<GeoPoint2>();

                    foreach (GeoPoint2 corner in world)
                    {
                        GeoPoint3 page = toPage.Transform(new GeoPoint3(corner.X, corner.Y, 0.0));
                        inches.Add(new GeoPoint2(page.X, page.Y));
                    }

                    // A rectangle along the page's sides is kept as AutoCAD keeps one, by two corners; one turned against the page, as a polygon.
                    bool along =
                        asRectangle
                        && Math.Abs(inches[0].Y - inches[1].Y) <= 1E-9 * Math.Max(1.0, Math.Abs(inches[0].Y))
                        && Math.Abs(inches[1].X - inches[2].X) <= 1E-9 * Math.Max(1.0, Math.Abs(inches[1].X));
                    underlay.SetClipBoundary(along ? new[] { inches[0], inches[2] } : inches.ToArray());
                    underlay.IsClipInverted = invert;
                }
            );
        }

        /// <summary>Whether the polygon of some corners crosses itself: two of its sides that do not share a corner meet.</summary>
        private static bool PdfSelfIntersects(IReadOnlyList<GeoPoint2> corners)
        {
            int n = corners.Count;

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1)
                    {
                        continue;
                    }

                    if (PdfSidesCross(corners[i], corners[(i + 1) % n], corners[j], corners[(j + 1) % n]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool PdfSidesCross(GeoPoint2 a, GeoPoint2 b, GeoPoint2 c, GeoPoint2 d)
        {
            double d1 = PdfSide(c, d, a);
            double d2 = PdfSide(c, d, b);
            double d3 = PdfSide(a, b, c);
            double d4 = PdfSide(a, b, d);
            return ((d1 > 0.0 && d2 < 0.0) || (d1 < 0.0 && d2 > 0.0))
                && ((d3 > 0.0 && d4 < 0.0) || (d3 < 0.0 && d4 > 0.0));
        }

        private static double PdfSide(GeoPoint2 a, GeoPoint2 b, GeoPoint2 p) =>
            (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);

        #endregion

        #region PDFLAYERS

        /// <summary>
        /// PDFLAYERS: "Select PDF underlay:", then the window's PDF Layers dialog, a list of the PDF's layers each on or off. With no
        /// window the layers are listed and "Enter layer name to switch or [All on/All off]" asks which to switch, until Enter. One
        /// undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> PdfLayers(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select PDF underlay");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            string handle = c.Input.Handles[0];

            if (!(c.Document.FindEntity(handle) is DxfPdfUnderlay underlay))
            {
                c.Write("Object selected was not a PDF underlay.");
                yield break;
            }

            PdfDocument pdf = PdfOf(c, underlay);

            if (pdf == null)
            {
                c.Write("The PDF of the underlay cannot be read.");
                yield break;
            }

            if (pdf.Layers.Count == 0)
            {
                c.Write("There is no layer in the PDF.");
                yield break;
            }

            var request = new CadPdfLayersRequest(
                underlay.Name ?? string.Empty,
                pdf.Layers.Select(l => l.Name).ToList(),
                CadPdfLayers.Overrides(underlay),
                pdf.Layers
            );

            if (c.Session.Request("PDFLAYERS", c.Document, request))
            {
                if (request.Action == CadDialogAction.Ok)
                {
                    WritePdfLayers(c, handle, request.Overrides);
                }

                yield break;
            }

            var overrides = new Dictionary<string, bool>(request.Overrides, StringComparer.Ordinal);
            bool changed = false;

            while (true)
            {
                foreach (PdfLayer layer in pdf.Layers)
                {
                    c.Write("  " + layer.Name.PadRight(30) + (CadPdfLayers.IsOn(layer, overrides) ? "On" : "Off"));
                }

                yield return CadPrompt.ForKeywordOrText(
                    "Enter layer name to switch or",
                    new[] { "All on", "All off" },
                    "done"
                );

                if (c.Input.Kind == CadInputKind.Keyword)
                {
                    bool on = c.Input.Is("All on");

                    foreach (PdfLayer layer in pdf.Layers)
                    {
                        overrides[layer.Name] = on;
                    }

                    changed = true;
                    continue;
                }

                if (
                    c.Input.Kind != CadInputKind.Text
                    || c.Input.Text.Trim().Length == 0
                    || c.Input.Text.Trim() == "done"
                )
                {
                    break;
                }

                PdfLayer named = pdf.Layers.FirstOrDefault(l =>
                    string.Equals(l.Name, c.Input.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                );

                if (named == null)
                {
                    c.Write("No matching layer found.");
                    continue;
                }

                overrides[named.Name] = !CadPdfLayers.IsOn(named, overrides);
                changed = true;
            }

            if (changed)
            {
                WritePdfLayers(c, handle, overrides);
            }
        }

        private static void WritePdfLayers(
            CadCommandContext c,
            string handle,
            IReadOnlyDictionary<string, bool> overrides
        )
        {
            var kept = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, bool> pair in overrides)
            {
                kept[pair.Key] = pair.Value;
            }

            c.Apply(
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        handle,
                        "PDF layers",
                        null,
                        kept.Count.ToString(CultureInfo.InvariantCulture)
                    ),
                },
                s => CadPdfLayers.Set((DxfPdfUnderlay)s.Document.FindEntity(handle), kept)
            );
        }

        /// <summary>The PDF an underlay shows, read; null where its file is not found or cannot be read.</summary>
        private static PdfDocument PdfOf(CadCommandContext c, DxfPdfUnderlay underlay)
        {
            string found = c.Document.Xrefs.Locate(underlay.Path, c.Document.DrawingFolder);
            return found == null ? null : CadPdfCache.Get(found, out _);
        }

        #endregion

        #region -PDFUNDERLAY

        /// <summary>
        /// -PDFUNDERLAY: "Enter PDF underlay option [?/Detach/Path/Reload/Unload]:", each option asking the names of the definitions
        /// it acts on with wildcards and commas, as -IMAGE does: the typed form of what the External References palette does to a
        /// PDF underlay.
        /// </summary>
        private static IEnumerable<CadPrompt> PdfUnderlayTyped(CadCommandContext c)
        {
            if (PdfsNamed(c.Database, "*").Count == 0)
            {
                c.Write("No PDF underlays in current drawing.");
                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter PDF underlay option", PdfUnderlayOptions);

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;
            yield return CadPrompt.ForString(
                option == "?" ? "PDF underlays to list" : "Enter list of PDF underlays to " + option.ToLowerInvariant(),
                option == "?" ? "*" : null
            );
            string names =
                c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim()
                : option == "?" ? "*"
                : null;

            if (names == null)
            {
                yield break;
            }

            List<KeyValuePair<string, DxfPdfDefinition>> found = PdfsNamed(c.Database, names);

            if (found.Count == 0 && option != "?")
            {
                c.Write("No matching PDF underlay names found.");
                yield break;
            }

            switch (option)
            {
                case "?":
                    c.Write("PDF Underlay Name".PadRight(33) + "Instances  Path");
                    c.Write(new string('-', 17).PadRight(33) + new string('-', 9) + "  " + new string('-', 10));

                    foreach (KeyValuePair<string, DxfPdfDefinition> pdf in found)
                    {
                        c.Write(
                            pdf.Key.PadRight(33)
                                + pdf.Value.GetEntityCount().ToString(CultureInfo.InvariantCulture).PadLeft(9)
                                + "  "
                                + pdf.Value.SourceFileName
                        );
                    }

                    c.Write("Total PDF underlay definitions: " + found.Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case "Detach":
                    var planned = new List<CadEditRecord>();
                    var handles = new HashSet<string>(
                        found.Select(p => p.Value.Handle),
                        StringComparer.OrdinalIgnoreCase
                    );

                    foreach (CadSpace space in c.Document.Spaces)
                    {
                        foreach (DxfEntity entity in space.Block)
                        {
                            if (
                                entity is DxfPdfUnderlay underlay
                                && handles.Contains(underlay.DefinitionHandle ?? string.Empty)
                            )
                            {
                                planned.Add(new CadEditRecord(CadEditRecordKind.Erased, entity.Handle));
                            }
                        }
                    }

                    List<string> detaching = found.Select(p => p.Key).ToList();
                    planned.Add(
                        new CadEditRecord(
                            CadEditRecordKind.TableChanged,
                            null,
                            "Detach PDF underlay",
                            string.Join(",", detaching),
                            null
                        )
                    );
                    c.Apply(planned, s =>
                    {
                        DxfDBDictionary dictionary = CadXrefManager.Pdfs(s.Database);

                        foreach (string name in detaching)
                        {
                            dictionary.GetAt(name).Erase();
                        }
                    });
                    break;
                case "Unload":
                    foreach (KeyValuePair<string, DxfPdfDefinition> pdf in found)
                    {
                        c.Document.Xrefs.SetPdfLoaded(pdf.Key, false);
                    }

                    Redisplay(c, found.Count);
                    break;
                case "Reload":
                    foreach (KeyValuePair<string, DxfPdfDefinition> pdf in found)
                    {
                        c.Write("  Reloading...");
                        c.Document.Xrefs.SetPdfLoaded(pdf.Key, true);
                        string at = c.Document.Xrefs.Locate(pdf.Value.SourceFileName);
                        CadPdfCache.Clear();
                        c.Write("Reload PDF underlay " + pdf.Key + ": " + (at ?? pdf.Value.SourceFileName));
                        c.Write(at == null ? pdf.Key + ": PDF file not found." : pdf.Key + " loaded and relinked.");
                    }

                    Redisplay(c, found.Count);
                    break;
                case "Path":
                    foreach (KeyValuePair<string, DxfPdfDefinition> pdf in found)
                    {
                        c.Write("PDF underlay name: " + pdf.Key);
                        c.Write("Old path:          " + pdf.Value.SourceFileName);
                        yield return CadPrompt.ForString("Enter New path");
                        string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;

                        if (
                            typed.Length > 0
                            && !string.Equals(typed, pdf.Value.SourceFileName, StringComparison.Ordinal)
                        )
                        {
                            string key = pdf.Key;
                            c.Apply(
                                new[]
                                {
                                    new CadEditRecord(
                                        CadEditRecordKind.TableChanged,
                                        null,
                                        "PDF underlay path",
                                        pdf.Value.SourceFileName,
                                        typed
                                    ),
                                },
                                s =>
                                    ((DxfPdfDefinition)CadXrefManager.Pdfs(s.Database).GetAt(key)).SourceFileName =
                                        typed
                            );
                        }
                    }

                    break;
            }
        }

        /// <summary>The definitions of PDF underlays of a drawing whose names match a list of names with wildcards, in ordinal order.</summary>
        private static List<KeyValuePair<string, DxfPdfDefinition>> PdfsNamed(DxfDatabase database, string names)
        {
            string[] patterns = names.Split(',').Select(p => p.Trim().Trim('"')).Where(p => p.Length > 0).ToArray();
            var found = new List<KeyValuePair<string, DxfPdfDefinition>>();
            DxfDBDictionary dictionary = CadXrefManager.Pdfs(database);

            if (dictionary != null)
            {
                foreach (DxfDBDictionaryEntry entry in dictionary)
                {
                    if (
                        entry.Value is DxfPdfDefinition definition
                        && patterns.Any(p => CadWildcard.IsMatch(entry.Key, p, true))
                    )
                    {
                        found.Add(new KeyValuePair<string, DxfPdfDefinition>(entry.Key, definition));
                    }
                }
            }

            found.Sort(
                (a, b) =>
                {
                    int byName = string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                    return byName != 0 ? byName : string.CompareOrdinal(a.Key, b.Key);
                }
            );
            return found;
        }

        #endregion
    }

    /// <summary>
    /// What PDFLAYERS gives its PDF Layers dialog: the underlay's name, the layers the PDF has in its order, which of them the
    /// underlay has switched on or off, and what the dialog was closed with. The window writes <see cref="Overrides"/>.
    /// </summary>
    public sealed class CadPdfLayersRequest
    {
        private readonly IReadOnlyList<PdfLayer> _layers;

        internal CadPdfLayersRequest(
            string underlay,
            IReadOnlyList<string> names,
            Dictionary<string, bool> overrides,
            IReadOnlyList<PdfLayer> layers
        )
        {
            UnderlayName = underlay;
            LayerNames = names;
            Overrides = overrides;
            _layers = layers;
        }

        /// <summary>Gets the name of the underlay.</summary>
        public string UnderlayName { get; }

        /// <summary>Gets the names of the layers of the PDF, in the order it lists them.</summary>
        public IReadOnlyList<string> LayerNames { get; }

        /// <summary>Gets the layers the underlay switches on (true) or off (false) against the state the PDF opens with; the dialog's OK writes it.</summary>
        public Dictionary<string, bool> Overrides { get; }

        /// <summary>Gets or sets what the dialog was closed with; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        /// <summary>Gets whether a layer is on as the PDF opens, which the underlay shows until it overrides it.</summary>
        public bool IsOnByDefault(string name)
        {
            foreach (PdfLayer layer in _layers)
            {
                if (layer.Name == name)
                {
                    return layer.IsOn;
                }
            }

            return true;
        }

        /// <summary>Gets whether a layer is on as the underlay shows it now.</summary>
        public bool IsOn(string name)
        {
            foreach (PdfLayer layer in _layers)
            {
                if (layer.Name == name)
                {
                    return CadPdfLayers.IsOn(layer, Overrides);
                }
            }

            return true;
        }
    }
}
