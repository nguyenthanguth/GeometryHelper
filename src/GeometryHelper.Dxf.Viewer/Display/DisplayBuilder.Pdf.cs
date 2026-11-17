using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>The PDF underlays of a drawing, drawn as AutoCAD 2021 shows them: the page's lines, fills, words and pictures, then the frame.</summary>
    internal sealed partial class DisplayBuilder
    {
        /// <summary>Where the page's points go: the page's inches (a point is 1/72 of an inch, or the page's user unit) through the underlay's transform and the block it stands in.</summary>
        private sealed class PdfPlacement
        {
            private readonly Affine2 _m;
            private readonly double _ox;
            private readonly double _oy;
            private readonly double _ax;
            private readonly double _ay;
            private readonly double _bx;
            private readonly double _by;

            public PdfPlacement(DxfPdfUnderlay underlay, Affine2 m, double inchesPerPoint)
            {
                _m = m;
                GeoTransform3 t = underlay.Transform;
                GeoPoint3 o = t.Transform(new GeoPoint3(0.0, 0.0, 0.0));
                GeoPoint3 ex = t.Transform(new GeoPoint3(1.0, 0.0, 0.0));
                GeoPoint3 ey = t.Transform(new GeoPoint3(0.0, 1.0, 0.0));
                _ox = o.X;
                _oy = o.Y;
                _ax = ex.X - o.X;
                _ay = ex.Y - o.Y;
                _bx = ey.X - o.X;
                _by = ey.Y - o.Y;
                InchesPerPoint = inchesPerPoint;
                Unit = Math.Sqrt(_ax * _ax + _ay * _ay) * inchesPerPoint;
            }

            /// <summary>Gets how many inches a point of the page is.</summary>
            public double InchesPerPoint { get; }

            /// <summary>Gets how many drawing units a point of the page is along the page's width.</summary>
            public double Unit { get; }

            /// <summary>The X of a place given in inches from the page's corner.</summary>
            public double XInches(double x, double y) => _m.X(_ox + _ax * x + _bx * y, _oy + _ay * x + _by * y);

            /// <summary>The Y of a place given in inches from the page's corner.</summary>
            public double YInches(double x, double y) => _m.Y(_ox + _ax * x + _bx * y, _oy + _ay * x + _by * y);

            /// <summary>The X of a place given in points of the page.</summary>
            public double X(double x, double y) => XInches(x * InchesPerPoint, y * InchesPerPoint);

            /// <summary>The Y of a place given in points of the page.</summary>
            public double Y(double x, double y) => YInches(x * InchesPerPoint, y * InchesPerPoint);

            /// <summary>The ring of some points, as x and y in turn.</summary>
            public double[] Ring(double[] points)
            {
                var ring = new double[points.Length];

                for (int i = 0; i + 1 < points.Length; i += 2)
                {
                    ring[i] = X(points[i], points[i + 1]);
                    ring[i + 1] = Y(points[i], points[i + 1]);
                }

                return ring;
            }
        }

        /// <summary>
        /// A PDF underlay: the lines, fills, words and pictures of its page at its place, scale and turn, clipped by its boundary,
        /// the layers of the PDF that are on, in the colours its fade, contrast, monochrome and background adjusting make; then its
        /// frame (the boundary, or the page's rectangle) where PDFFRAME shows frames. An underlay whose file is not found, whose
        /// page the file has not, which cannot be read or is switched off (Show underlay off, or unloaded) is drawn as its frame
        /// alone, whatever PDFFRAME says; one not found has its file's name written in its corner, as AutoCAD shows it.
        /// </summary>
        private void DrawPdf(
            DxfPdfUnderlay underlay,
            Frame frame,
            DisplayList2 list,
            DisplayStyle style,
            string handle,
            string layerName
        )
        {
            DxfPdfDefinition definition = underlay.Definition;
            string saved = definition?.SourceFileName ?? string.Empty;
            string name = definition == null ? null : CadXrefManager.PdfName(_database, definition);
            bool shown = definition != null && underlay.IsOn;

            if (shown && _document != null && _host == null && name != null)
            {
                shown = _document.Xrefs.IsPdfLoaded(name);
            }

            string found = null;
            PdfDocument pdf = null;
            PdfPage page = null;
            PdfScene scene = null;

            if (shown)
            {
                found =
                    _document != null ? _document.Xrefs.Locate(saved, Folder)
                    : File.Exists(saved) ? saved
                    : null;

                if (found != null)
                {
                    pdf = CadPdfCache.Get(found, out _);

                    if (
                        pdf != null
                        && int.TryParse(
                            definition.ItemName,
                            NumberStyles.Integer,
                            CultureInfo.InvariantCulture,
                            out int number
                        )
                        && number >= 1
                        && number <= pdf.Pages.Count
                    )
                    {
                        page = pdf.Pages[number - 1];
                        scene = pdf.Scene(number);
                    }
                }
            }

            double inchesPerPoint = page == null ? 1.0 / 72.0 : page.UserUnit / 72.0;
            double widthPoints = page == null ? 72.0 : page.Width;
            double heightPoints = page == null ? 72.0 : page.Height;
            var placement = new PdfPlacement(underlay, frame.Matrix, inchesPerPoint);
            DisplayClipRegion root = RootClip(underlay, placement, widthPoints, heightPoints);

            if (scene != null)
            {
                DrawPdfScene(underlay, pdf, scene, placement, root, list, handle, layerName);
            }

            int frames = _document?.PdfFrame ?? 1;

            if (frames == 0 && scene != null)
            {
                return;
            }

            int framesFrom = list.Items.Count;
            double[] ring = FrameRing(underlay, placement, widthPoints, heightPoints);

            if (ring.Length >= 6)
            {
                list.Add(new DisplayPolyline2(ring, true, style, handle, layerName));
            }

            // PDFFRAME 2 shows the frame and leaves it out of a plot; a frame that stands for an underlay not drawn is left off a plot too.
            if (frames != 1 || scene == null)
            {
                for (int k = framesFrom; k < list.Items.Count; k++)
                {
                    list.Items[k].IsNotPlotted = true;
                }
            }

            if (scene == null && shown && found == null && saved.Length > 0 && ring.Length >= 8)
            {
                // The file's name in the corner of the frame, a tenth of its shorter side high.
                double ux = ring[2] - ring[0];
                double uy = ring[3] - ring[1];
                double vx = ring[6] - ring[0];
                double vy = ring[7] - ring[1];
                double width = Math.Sqrt(ux * ux + uy * uy);
                double height = Math.Sqrt(vx * vx + vy * vy);
                double size = Math.Min(width, height) / 10.0;

                if (size > 0.0)
                {
                    double ax = ux / width;
                    double ay = uy / width;
                    double bx = vx / height;
                    double by = vy / height;
                    list.Add(
                        Text(
                            FaceOf(null),
                            saved,
                            ring[0] + (ax + bx) * size / 2.0,
                            ring[1] + (ay + by) * size / 2.0,
                            size,
                            Math.Atan2(uy, ux),
                            1.0,
                            0,
                            0,
                            style,
                            handle,
                            layerName,
                            0.0,
                            false
                        )
                    );
                }
            }
        }

        /// <summary>The ring of the underlay's frame in drawing units: its boundary where it is clipped by one, else the page's rectangle; four corners from the lower left, or the corners of a polygon.</summary>
        private static double[] FrameRing(
            DxfPdfUnderlay underlay,
            PdfPlacement placement,
            double widthPoints,
            double heightPoints
        )
        {
            IReadOnlyList<GeoPoint2> boundary = underlay.IsClipped ? underlay.GetClipBoundary() : null;
            double w = widthPoints * placement.InchesPerPoint;
            double h = heightPoints * placement.InchesPerPoint;

            if (boundary == null || boundary.Count < 2)
            {
                return new[]
                {
                    placement.XInches(0.0, 0.0),
                    placement.YInches(0.0, 0.0),
                    placement.XInches(w, 0.0),
                    placement.YInches(w, 0.0),
                    placement.XInches(w, h),
                    placement.YInches(w, h),
                    placement.XInches(0.0, h),
                    placement.YInches(0.0, h),
                };
            }

            List<GeoPoint2> corners = Corners(boundary);
            var ring = new double[2 * corners.Count];

            for (int i = 0; i < corners.Count; i++)
            {
                ring[2 * i] = placement.XInches(corners[i].X, corners[i].Y);
                ring[2 * i + 1] = placement.YInches(corners[i].X, corners[i].Y);
            }

            return ring;
        }

        /// <summary>The corners of a boundary: those of a polygon, the four of a rectangle given by two.</summary>
        private static List<GeoPoint2> Corners(IReadOnlyList<GeoPoint2> boundary)
        {
            if (boundary.Count != 2)
            {
                return new List<GeoPoint2>(boundary);
            }

            double x0 = Math.Min(boundary[0].X, boundary[1].X);
            double x1 = Math.Max(boundary[0].X, boundary[1].X);
            double y0 = Math.Min(boundary[0].Y, boundary[1].Y);
            double y1 = Math.Max(boundary[0].Y, boundary[1].Y);
            return new List<GeoPoint2>
            {
                new GeoPoint2(x0, y0),
                new GeoPoint2(x1, y0),
                new GeoPoint2(x1, y1),
                new GeoPoint2(x0, y1),
            };
        }

        /// <summary>The region an underlay's boundary lets through (PDFCLIP); null where it is not clipped. Inverted, a ring round the page that goes in to the boundary by a slit, as XCLIP keeps one.</summary>
        private static DisplayClipRegion RootClip(
            DxfPdfUnderlay underlay,
            PdfPlacement placement,
            double widthPoints,
            double heightPoints
        )
        {
            if (!underlay.IsClipped || !underlay.HasClipBoundary)
            {
                return null;
            }

            double[] ring = FrameRing(underlay, placement, widthPoints, heightPoints);

            if (ring.Length < 6)
            {
                return null;
            }

            if (underlay.IsClipInverted)
            {
                double w = widthPoints * placement.InchesPerPoint;
                double h = heightPoints * placement.InchesPerPoint;
                double[] outer =
                {
                    placement.XInches(-w, -h),
                    placement.YInches(-w, -h),
                    placement.XInches(2 * w, -h),
                    placement.YInches(2 * w, -h),
                    placement.XInches(2 * w, 2 * h),
                    placement.YInches(2 * w, 2 * h),
                    placement.XInches(-w, 2 * h),
                    placement.YInches(-w, 2 * h),
                };
                var slit = new List<double>(outer);
                slit.Add(outer[0]);
                slit.Add(outer[1]);
                slit.AddRange(ring);
                slit.Add(ring[0]);
                slit.Add(ring[1]);
                ring = slit.ToArray();
            }

            return new DisplayClipRegion(new DisplayClip2(ring, new double[ring.Length / 2]), null);
        }

        private void DrawPdfScene(
            DxfPdfUnderlay underlay,
            PdfDocument pdf,
            PdfScene scene,
            PdfPlacement placement,
            DisplayClipRegion root,
            DisplayList2 list,
            string handle,
            string layerName
        )
        {
            var look = new DisplayUnderlayLook(
                underlay.Fade,
                underlay.Contrast,
                underlay.Monochrome,
                underlay.AdjustColorForBackground
            );
            Dictionary<string, bool> overrides = CadPdfLayers.Overrides(underlay);
            var byId = new Dictionary<int, PdfLayer>();

            foreach (PdfLayer layer in pdf.Layers)
            {
                byId[layer.Id] = layer;
            }

            Func<int, bool> isOn = id =>
                !byId.TryGetValue(id, out PdfLayer layer) || CadPdfLayers.IsOn(layer, overrides);
            var styles = new Dictionary<string, DisplayStyle>(StringComparer.Ordinal);
            var regions = new Dictionary<PdfClip, DisplayClipRegion>();
            var chain = new List<PdfClip>();

            foreach (PdfItem item in scene.Items)
            {
                if (item.Oc != null && !item.Oc.IsShown(isOn))
                {
                    continue;
                }

                DisplayBox box = PageBox(item);

                if (box.IsEmpty)
                {
                    continue;
                }

                // The clips the item stands in, outermost first; a rectangle it lies wholly inside clips nothing, one it lies wholly outside hides it.
                chain.Clear();
                bool hidden = false;

                for (PdfClip clip = item.Clip; clip != null; clip = clip.Parent)
                {
                    if (clip.IsBox)
                    {
                        if (
                            box.MaxX < clip.MinX
                            || box.MinX > clip.MaxX
                            || box.MaxY < clip.MinY
                            || box.MinY > clip.MaxY
                        )
                        {
                            hidden = true;
                            break;
                        }

                        if (
                            box.MinX >= clip.MinX
                            && box.MaxX <= clip.MaxX
                            && box.MinY >= clip.MinY
                            && box.MaxY <= clip.MaxY
                        )
                        {
                            continue;
                        }
                    }

                    chain.Insert(0, clip);
                }

                if (hidden)
                {
                    continue;
                }

                DisplayClipRegion region = root;

                foreach (PdfClip clip in chain)
                {
                    if (!regions.TryGetValue(clip, out DisplayClipRegion made) || made.Parent != region)
                    {
                        double[] ring = placement.Ring(clip.Ring);
                        made =
                            ring.Length >= 6
                                ? new DisplayClipRegion(new DisplayClip2(ring, new double[ring.Length / 2]), region)
                                : region;
                        regions[clip] = made;
                    }

                    region = made;
                }

                int before = list.Count;
                AddPdfItem(item, placement, look, styles, list, handle, layerName, underlay);

                // The snaps take the lines of a PDF (End, Mid, Int of its strokes) while PDFOSNAP is on; its filled shapes and letters
                // give none, and with PDFOSNAP off nothing does.
                bool snaps = (_document?.PdfOsnap ?? true) && !(item is PdfFill) && !(item is PdfWords);

                for (int k = before; k < list.Count; k++)
                {
                    if (region != null)
                    {
                        list.Items[k].BlockClip = region;
                    }

                    if (!snaps)
                    {
                        list.Items[k].IsFrameInside = true;
                    }
                }
            }
        }

        /// <summary>The box of an item in page points.</summary>
        private static DisplayBox PageBox(PdfItem item)
        {
            DisplayBox box = DisplayBox.Empty;

            switch (item)
            {
                case PdfStroke stroke:
                    foreach (double[] line in stroke.Lines)
                    {
                        box = Grow(box, line);
                    }

                    break;
                case PdfFill fill:
                    foreach (double[] ring in fill.Rings)
                    {
                        box = Grow(box, ring);
                    }

                    break;
                case PdfWords words:
                    double reach = Math.Max(words.Width, words.Height);
                    box = box.Add(words.X - reach, words.Y - reach).Add(words.X + reach, words.Y + reach);
                    break;
                case PdfPicture picture:
                    double[] m = picture.Matrix;
                    box = box.Add(m[4], m[5])
                        .Add(m[4] + m[0], m[5] + m[1])
                        .Add(m[4] + m[2], m[5] + m[3])
                        .Add(m[4] + m[0] + m[2], m[5] + m[1] + m[3]);
                    break;
            }

            return box;
        }

        private static DisplayBox Grow(DisplayBox box, double[] points)
        {
            for (int i = 0; i + 1 < points.Length; i += 2)
            {
                box = box.Add(points[i], points[i + 1]);
            }

            return box;
        }

        private static DisplayStyle PdfStyle(
            Dictionary<string, DisplayStyle> styles,
            DisplayUnderlayLook look,
            int rgb,
            double lineWeight,
            double[] dashes,
            double phase
        )
        {
            string key =
                rgb.ToString("X6", CultureInfo.InvariantCulture)
                + "|"
                + lineWeight.ToString("R", CultureInfo.InvariantCulture);

            if (dashes != null)
            {
                key +=
                    "|"
                    + string.Join(",", Array.ConvertAll(dashes, d => d.ToString("R", CultureInfo.InvariantCulture)))
                    + "|"
                    + phase.ToString("R", CultureInfo.InvariantCulture);
            }

            if (!styles.TryGetValue(key, out DisplayStyle style))
            {
                style =
                    dashes == null
                        ? new DisplayStyle(rgb, lineWeight, null)
                        : new DisplayStyle(rgb, lineWeight, dashes, phase);
                style.Underlay = look.IsPlain ? null : look;
                styles[key] = style;
            }

            return style;
        }

        private void AddPdfItem(
            PdfItem item,
            PdfPlacement placement,
            DisplayUnderlayLook look,
            Dictionary<string, DisplayStyle> styles,
            DisplayList2 list,
            string handle,
            string layerName,
            DxfPdfUnderlay underlay
        )
        {
            switch (item)
            {
                case PdfStroke stroke:
                {
                    double[] dashes = null;

                    if (stroke.Dash != null && placement.Unit > 0.0)
                    {
                        dashes = new double[stroke.Dash.Length % 2 == 1 ? stroke.Dash.Length * 2 : stroke.Dash.Length];

                        for (int i = 0; i < dashes.Length; i++)
                        {
                            dashes[i] = stroke.Dash[i % stroke.Dash.Length] * placement.Unit;
                        }
                    }

                    DisplayStyle style = PdfStyle(
                        styles,
                        look,
                        stroke.Rgb,
                        Math.Round(stroke.Width * 25.4 / 72.0, 2),
                        dashes,
                        stroke.Phase * placement.Unit
                    );

                    for (int i = 0; i < stroke.Lines.Count; i++)
                    {
                        double[] ring = placement.Ring(stroke.Lines[i]);

                        if (ring.Length >= 4)
                        {
                            list.Add(new DisplayPolyline2(ring, stroke.Closed[i], style, handle, layerName));
                        }
                    }

                    break;
                }
                case PdfFill fill:
                {
                    var rings = new List<double[]>(fill.Rings.Count);

                    foreach (double[] ring in fill.Rings)
                    {
                        rings.Add(placement.Ring(ring));
                    }

                    list.Add(
                        new DisplayFill2(
                            rings,
                            false,
                            PdfStyle(styles, look, fill.Rgb, 0.0, null, 0.0),
                            handle,
                            layerName
                        )
                    );
                    break;
                }
                case PdfWords words:
                {
                    double x0 = placement.X(words.X, words.Y);
                    double y0 = placement.Y(words.X, words.Y);
                    double c = Math.Cos(words.Rotation);
                    double s = Math.Sin(words.Rotation);
                    double ux = placement.X(words.X + c, words.Y + s) - x0;
                    double uy = placement.Y(words.X + c, words.Y + s) - y0;
                    double vx = placement.X(words.X - s, words.Y + c) - x0;
                    double vy = placement.Y(words.X - s, words.Y + c) - y0;
                    double along = Math.Sqrt(ux * ux + uy * uy);
                    double up = Math.Sqrt(vx * vx + vy * vy);

                    if (along > 0.0 && up > 0.0)
                    {
                        list.Add(
                            new DisplayText2(
                                words.Text,
                                x0,
                                y0,
                                words.Height * up,
                                Math.Atan2(uy, ux),
                                along / up,
                                0,
                                0,
                                PdfStyle(styles, look, words.Rgb, 0.0, null, 0.0),
                                handle,
                                layerName,
                                words.Family,
                                0.0,
                                false
                            )
                        );
                    }

                    break;
                }
                case PdfPicture picture:
                {
                    double[] m = picture.Matrix;
                    double x = placement.X(m[4], m[5]);
                    double y = placement.Y(m[4], m[5]);
                    list.Add(
                        new DisplayImage2(
                            x,
                            y,
                            placement.X(m[4] + m[0], m[5] + m[1]) - x,
                            placement.Y(m[4] + m[0], m[5] + m[1]) - y,
                            placement.X(m[4] + m[2], m[5] + m[3]) - x,
                            placement.Y(m[4] + m[2], m[5] + m[3]) - y,
                            null,
                            false,
                            null,
                            string.Empty,
                            PdfStyle(styles, look, 0x808080, 0.0, null, 0.0),
                            handle,
                            layerName
                        )
                        {
                            Picture = picture.Bytes,
                            PictureKey = PictureKeyOf(picture.Bytes),
                            IsTransparent = true,
                            Fade = underlay.Fade,
                            IsMonochrome = underlay.Monochrome,
                        }
                    );
                    break;
                }
            }
        }
    }
}
