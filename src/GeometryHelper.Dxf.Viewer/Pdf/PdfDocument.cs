using System;
using System.Collections.Generic;
using System.IO;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>One page of a PDF: its dictionary with the attributes it inherits, and the box it shows.</summary>
    internal sealed class PdfPage
    {
        /// <summary>Gets the number of the page, from 1.</summary>
        public int Number { get; set; }

        /// <summary>Gets the page's dictionary.</summary>
        public PdfDict Dictionary { get; set; }

        /// <summary>Gets the resources the page inherits or has.</summary>
        public PdfDict Resources { get; set; }

        /// <summary>Gets the left of the box the page shows, in points: its crop box within its media box.</summary>
        public double X0 { get; set; }

        public double Y0 { get; set; }

        public double X1 { get; set; }

        public double Y1 { get; set; }

        /// <summary>Gets the page's turn in degrees, 0, 90, 180 or 270, clockwise.</summary>
        public int Rotate { get; set; }

        /// <summary>Gets the size of a unit of the page in points: 1, or the UserUnit the page gives.</summary>
        public double UserUnit { get; set; } = 1.0;

        /// <summary>Gets the width of the page as it is shown, in points.</summary>
        public double Width => Rotate == 90 || Rotate == 270 ? Y1 - Y0 : X1 - X0;

        /// <summary>Gets the height of the page as it is shown, in points.</summary>
        public double Height => Rotate == 90 || Rotate == 270 ? X1 - X0 : Y1 - Y0;

        /// <summary>Gets the width of the page in inches, as AutoCAD measures a PDF underlay: 200 points are 2.7778 inches.</summary>
        public double WidthInches => Width * UserUnit / 72.0;

        /// <summary>Gets the height of the page in inches.</summary>
        public double HeightInches => Height * UserUnit / 72.0;
    }

    /// <summary>An optional content group of a PDF, which AutoCAD lists as a layer of the underlay.</summary>
    internal sealed class PdfLayer
    {
        /// <summary>Gets the number of the group's object, which names it in the page's content.</summary>
        public int Id { get; set; }

        /// <summary>Gets the group's name.</summary>
        public string Name { get; set; }

        /// <summary>Gets whether the group is on as the PDF opens.</summary>
        public bool IsOn { get; set; }
    }

    /// <summary>
    /// A PDF file read for its pages and layers, and its pages turned into scenes (<see cref="PdfScene"/>) a drawing can show.
    /// Pages are interpreted once each, the scene kept.
    /// </summary>
    internal sealed class PdfDocument
    {
        private readonly Dictionary<int, PdfScene> _scenes = new Dictionary<int, PdfScene>();
        private readonly object _lock = new object();

        private PdfDocument(PdfFile file)
        {
            File = file;
        }

        /// <summary>Gets the structure of the file.</summary>
        public PdfFile File { get; }

        /// <summary>Gets the pages in their order.</summary>
        public List<PdfPage> Pages { get; } = new List<PdfPage>();

        /// <summary>Gets the optional content groups in the order the PDF lists them.</summary>
        public List<PdfLayer> Layers { get; } = new List<PdfLayer>();

        /// <summary>Opens a PDF from its bytes.</summary>
        /// <exception cref="PdfException">Thrown when the file cannot be read as a PDF.</exception>
        public static PdfDocument Open(byte[] bytes)
        {
            var document = new PdfDocument(PdfFile.Open(bytes));
            document.ReadPages();
            document.ReadLayers();

            if (document.Pages.Count == 0)
            {
                throw new PdfException("The PDF has no pages.");
            }

            return document;
        }

        /// <summary>Opens a PDF file.</summary>
        /// <exception cref="PdfException">Thrown when the file cannot be read as a PDF.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        public static PdfDocument OpenFile(string path) => Open(System.IO.File.ReadAllBytes(path));

        /// <summary>Gives the scene of a page, interpreted on the first call.</summary>
        /// <param name="number">The page's number from 1.</param>
        /// <returns>The scene, or null for a page the file has not.</returns>
        public PdfScene Scene(int number)
        {
            if (number < 1 || number > Pages.Count)
            {
                return null;
            }

            lock (_lock)
            {
                if (!_scenes.TryGetValue(number, out PdfScene scene))
                {
                    try
                    {
                        scene = new PdfInterpreter(this, Pages[number - 1]).Run();
                    }
                    catch (Exception exception)
                    {
                        // A page the interpreter cannot run (a damaged file takes many forms) is an empty page, not a fault of the drawing.
                        GeometryHelperLog.Warn("PdfDocument: page " + number + " could not be read.", exception);
                        scene = new PdfScene { Width = Pages[number - 1].Width, Height = Pages[number - 1].Height };
                        scene.Dropped.Add("content that could not be read");
                    }

                    _scenes[number] = scene;

                    // One aggregated line for what the page holds that is not drawn, as the bridges say what they skip.
                    if (scene.Dropped.Count > 0)
                    {
                        GeometryHelperLog.Debug(
                            "PdfDocument: page "
                                + number
                                + " has "
                                + string.Join("; ", scene.Dropped)
                                + " that are not drawn."
                        );
                    }
                }

                return scene;
            }
        }

        private void ReadPages()
        {
            PdfDict catalog = File.Dict(File.Trailer.TryGetValue("Root", out object root) ? root : null);
            object pages = catalog == null ? null : File.Get(catalog, "Pages");
            var seen = new HashSet<PdfDict>();
            Walk(File.Dict(pages), new Inherited(), seen, 0);
        }

        private sealed class Inherited
        {
            public object Resources;
            public object MediaBox;
            public object CropBox;
            public object Rotate;
            public object UserUnit;

            public Inherited Copy() =>
                new Inherited
                {
                    Resources = Resources,
                    MediaBox = MediaBox,
                    CropBox = CropBox,
                    Rotate = Rotate,
                    UserUnit = UserUnit,
                };
        }

        private void Walk(PdfDict node, Inherited inherited, HashSet<PdfDict> seen, int depth)
        {
            if (node == null || depth > 64 || !seen.Add(node) || Pages.Count >= 5000)
            {
                return;
            }

            Inherited here = inherited.Copy();

            foreach (string key in new[] { "Resources", "MediaBox", "CropBox", "Rotate", "UserUnit" })
            {
                if (node.TryGetValue(key, out object value) && value != null)
                {
                    switch (key)
                    {
                        case "Resources":
                            here.Resources = value;
                            break;
                        case "MediaBox":
                            here.MediaBox = value;
                            break;
                        case "CropBox":
                            here.CropBox = value;
                            break;
                        case "Rotate":
                            here.Rotate = value;
                            break;
                        default:
                            here.UserUnit = value;
                            break;
                    }
                }
            }

            List<object> kids = File.Array(File.Get(node, "Kids"));
            string type = File.Name(File.Get(node, "Type"));

            if (kids != null && type != "Page")
            {
                foreach (object kid in kids)
                {
                    Walk(File.Dict(kid), here, seen, depth + 1);
                }

                return;
            }

            double[] media = File.Numbers(here.MediaBox);

            if (media == null || media.Length < 4)
            {
                media = new[] { 0.0, 0.0, 612.0, 792.0 };
            }

            double[] box = Normal(media);
            double[] crop = File.Numbers(here.CropBox);

            if (crop != null && crop.Length >= 4)
            {
                double[] c = Normal(crop);
                double x0 = Math.Max(box[0], c[0]);
                double y0 = Math.Max(box[1], c[1]);
                double x1 = Math.Min(box[2], c[2]);
                double y1 = Math.Min(box[3], c[3]);

                if (x1 > x0 && y1 > y0)
                {
                    box = new[] { x0, y0, x1, y1 };
                }
            }

            int rotate = (((int)File.Number(here.Rotate) % 360) + 360) % 360;
            double unit = File.Number(here.UserUnit, 1.0);

            Pages.Add(
                new PdfPage
                {
                    Number = Pages.Count + 1,
                    Dictionary = node,
                    Resources = File.Dict(here.Resources) ?? new PdfDict(),
                    X0 = box[0],
                    Y0 = box[1],
                    X1 = box[2],
                    Y1 = box[3],
                    Rotate = rotate - rotate % 90,
                    UserUnit = unit > 0.0 ? unit : 1.0,
                }
            );
        }

        private static double[] Normal(double[] b) =>
            new[] { Math.Min(b[0], b[2]), Math.Min(b[1], b[3]), Math.Max(b[0], b[2]), Math.Max(b[1], b[3]) };

        private void ReadLayers()
        {
            PdfDict catalog = File.Dict(File.Trailer.TryGetValue("Root", out object root) ? root : null);
            PdfDict properties = File.Dict(File.Get(catalog, "OCProperties"));

            if (properties == null)
            {
                return;
            }

            List<object> groups = File.Array(File.Get(properties, "OCGs"));
            PdfDict configuration = File.Dict(File.Get(properties, "D"));

            if (groups == null)
            {
                return;
            }

            bool baseOn = File.Name(File.Get(configuration, "BaseState")) != "OFF";
            var on = new HashSet<int>();
            var off = new HashSet<int>();

            foreach (object item in File.Array(File.Get(configuration, "ON")) ?? new List<object>())
            {
                if (item is PdfRef r)
                {
                    on.Add(r.Number);
                }
            }

            foreach (object item in File.Array(File.Get(configuration, "OFF")) ?? new List<object>())
            {
                if (item is PdfRef r)
                {
                    off.Add(r.Number);
                }
            }

            var seen = new HashSet<int>();

            foreach (object item in groups)
            {
                if (!(item is PdfRef reference) || !seen.Add(reference.Number))
                {
                    continue;
                }

                PdfDict group = File.Dict(item);
                string name = File.Text(File.Get(group, "Name")) ?? "Layer " + (Layers.Count + 1);
                bool isOn = off.Contains(reference.Number) ? false : on.Contains(reference.Number) || baseOn;
                Layers.Add(
                    new PdfLayer
                    {
                        Id = reference.Number,
                        Name = name,
                        IsOn = isOn,
                    }
                );
            }
        }
    }
}
