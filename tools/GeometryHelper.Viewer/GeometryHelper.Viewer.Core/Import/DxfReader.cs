using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Reads a DXF with the library's <see cref="Export.DxfReader"/>, which gives every entity it reads as the library's shape, and makes the scene of
    /// them: a shape lying on the floor as the shape of the plane it is (<see cref="DxfReadOptions.PlanAs2D"/>), each INSERT as the entities of its
    /// block placed where it puts them, on their layers, in their colours, an entity placed through blocks named by them, and a TEXT or an MTEXT as a
    /// label at the point it stands on. The layers take their colours, and are hidden where they are off or frozen, from the library's layer table. The
    /// 3DFACEs of a layer are welded into meshes, one for each body. What the library leaves out is counted and said.
    /// </summary>
    internal sealed class DxfReader
    {
        private readonly ImportBuilder _b;
        private readonly CancellationToken _cancel;
        private readonly DxfDocument _document;
        private readonly List<FaceSet> _faceSets = new List<FaceSet>();
        private readonly Dictionary<string, FaceSet> _faceSetIndex = new Dictionary<string, FaceSet>(StringComparer.Ordinal);

        private DxfReader(ImportBuilder builder, CancellationToken cancel, DxfDocument document)
        {
            _b = builder;
            _cancel = cancel;
            _document = document;
        }

        /// <summary>
        /// The 3DFACEs of one layer in one colour, to be welded into meshes once they are all in.
        /// </summary>
        private sealed class FaceSet
        {
            internal string Layer;
            internal string Color;
            internal readonly MeshWelder Weld = new MeshWelder();
        }

        /// <summary>
        /// Reads the text of a DXF, decoded already.
        /// </summary>
        internal static Scene Read(string text, string title, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            var b = new ImportBuilder(title);
            return Take(b, Export.DxfReader.Parse(text, Options(b)), cancel);
        }

        /// <summary>
        /// Reads a DXF file from its bytes, which the library decodes: UTF-8 from R2007, and before that the code page the header names.
        /// </summary>
        internal static Scene Read(Stream stream, string title, CancellationToken cancel)
        {
            cancel.ThrowIfCancellationRequested();
            var b = new ImportBuilder(title);
            return Take(b, Export.DxfReader.Read(stream, Options(b)), cancel);
        }

        private static DxfReadOptions Options(ImportBuilder b)
            => new DxfReadOptions { PlanAs2D = true, ExpandBlocks = true, KeepGroups = false, Tolerance = b.Scene.Tolerance };

        private static Scene Take(ImportBuilder b, DxfDocument document, CancellationToken cancel)
        {
            new DxfReader(b, cancel, document).Take();
            return b.Finish();
        }

        private void Take()
        {
            foreach (DxfLayer layer in _document.LayerTable)
            {
                if (layer.IsOff || layer.IsFrozen)
                {
                    _b.Warn("DXF: the layer '" + layer.Name + "' is " + (layer.IsFrozen ? "frozen" : "turned off") + " in the file and is hidden; tick it in the scene tree to see it");
                }
            }

            for (int i = 0; i < _document.Entities.Count; i++)
            {
                if ((i & 1023) == 0)
                {
                    _cancel.ThrowIfCancellationRequested();
                }

                DxfEntity e = _document.Entities[i];
                string name = e.BlockPath.Count > 0 ? string.Join(" > ", e.BlockPath) : null;

                if (e.Type == "3DFACE")
                {
                    AddFace(e);
                    continue;
                }

                if (e.Shape is DxfText label)
                {
                    GeoPoint3 at = label.Position;
                    Emit(SceneKinds.Text, ImportData.Text(new Vec3(at.X, at.Y, at.Z), label.Value, label.Height), e.Type, e.Layer, ItemColour(e), e.Handle, name);
                    continue;
                }

                string kind = GeometryEncoder.KindOf(e.Shape);

                if (kind == null)
                {
                    _b.Warn("DXF: a " + e.Type + " the reader gave as a " + e.Shape.GetType().Name + ", which the viewer does not draw, was left out");
                    continue;
                }

                Emit(kind, GeometryEncoder.Encode(e.Shape), e.Type, e.Layer, ItemColour(e), e.Handle, name);
            }

            EmitFaceSets();

            foreach (string warning in _document.Warnings)
            {
                _b.Warn("DXF: " + warning);
            }

            // Every type is counted, INSERT too: the library gives a warning of its own for some of what it leaves out, not for all.
            foreach (KeyValuePair<string, int> skipped in _document.Skipped)
            {
                _b.Warn("DXF: " + skipped.Value.ToString(CultureInfo.InvariantCulture) + " " + skipped.Key + " entity(ies) are not read, as the library's DxfReader counts them; a warning above gives the reason where it has one");
            }
        }

        #region Layers and colours

        private static string Hex(int rgb)
            => "#" + ((rgb >> 16) & 255).ToString("X2", CultureInfo.InvariantCulture) + ((rgb >> 8) & 255).ToString("X2", CultureInfo.InvariantCulture) + (rgb & 255).ToString("X2", CultureInfo.InvariantCulture);

        /// <summary>
        /// The colour of an entity: its true colour, else that of its colour index, each its own or taken from the INSERT that places it.
        /// </summary>
        private static string ItemColour(DxfEntity e) => e.TrueColour.HasValue ? Hex(e.TrueColour.Value) : AciColour(e.Colour ?? 0);

        /// <summary>
        /// The colour of an AutoCAD colour index as #RRGGBB. Seven is white on a dark screen and black on a white page, a colour of nothing in
        /// particular, and 0 and 256 are by block and by layer: none of them is a colour, so null is given and the layer's cycle colour is kept.
        /// </summary>
        internal static string AciColour(int index)
        {
            if (index <= 0 || index == 7 || index > 255)
            {
                return null;
            }

            int r, g, b;

            if (index <= 9)
            {
                int[][] basic =
                {
                    new[] { 255, 0, 0 }, new[] { 255, 255, 0 }, new[] { 0, 255, 0 }, new[] { 0, 255, 255 }, new[] { 0, 0, 255 }, new[] { 255, 0, 255 },
                    new[] { 255, 255, 255 }, new[] { 65, 65, 65 }, new[] { 128, 128, 128 },
                };
                int[] c = basic[index - 1];
                r = c[0];
                g = c[1];
                b = c[2];
            }
            else if (index >= 250)
            {
                int[] greys = { 51, 91, 132, 173, 214, 255 };
                r = g = b = greys[index - 250];
            }
            else
            {
                // 24 hues of 15 degrees, each in ten shades: full and half saturation at five brightnesses.
                int n = index - 10;
                double hue = (n / 10) * 15.0;
                int shade = n % 10;
                double value = new[] { 1.0, 1.0, 0.8, 0.8, 0.6, 0.6, 0.5, 0.5, 0.3, 0.3 }[shade];
                double saturation = shade % 2 == 0 ? 1.0 : 0.5;
                Hsv(hue, saturation, value, out r, out g, out b);
            }

            return "#" + r.ToString("X2", CultureInfo.InvariantCulture) + g.ToString("X2", CultureInfo.InvariantCulture) + b.ToString("X2", CultureInfo.InvariantCulture);
        }

        private static void Hsv(double hue, double saturation, double value, out int r, out int g, out int b)
        {
            double c = value * saturation;
            double h = hue / 60.0;
            double x = c * (1.0 - Math.Abs(h % 2.0 - 1.0));
            double m = value - c;
            double rr, gg, bb;

            if (h < 1.0)
            {
                rr = c; gg = x; bb = 0.0;
            }
            else if (h < 2.0)
            {
                rr = x; gg = c; bb = 0.0;
            }
            else if (h < 3.0)
            {
                rr = 0.0; gg = c; bb = x;
            }
            else if (h < 4.0)
            {
                rr = 0.0; gg = x; bb = c;
            }
            else if (h < 5.0)
            {
                rr = x; gg = 0.0; bb = c;
            }
            else
            {
                rr = c; gg = 0.0; bb = x;
            }

            r = (int)Math.Floor((rr + m) * 255.0 + 1E-9);
            g = (int)Math.Floor((gg + m) * 255.0 + 1E-9);
            b = (int)Math.Floor((bb + m) * 255.0 + 1E-9);
        }

        /// <summary>
        /// Adds an item on a layer, making the layer with the colour and visibility the layer table gave it.
        /// </summary>
        private void Emit(string kind, System.Text.Json.JsonElement data, string type, string layer, string color, string handle, string name)
        {
            EnsureLayer(layer);
            SceneItem item = _b.Add(kind, data, layer, name, color, "DXF " + type);

            if (!string.IsNullOrWhiteSpace(handle))
            {
                item.Properties["handle"] = handle.Trim();
            }
        }

        private void EnsureLayer(string layer)
        {
            if (_b.Scene.FindLayer(layer) != null)
            {
                return;
            }

            DxfLayer info = _document.GetLayer(layer);

            if (info != null)
            {
                _b.Layer(layer, info.TrueColour.HasValue ? Hex(info.TrueColour.Value) : AciColour(info.Colour ?? 0), !info.IsOff && !info.IsFrozen);
            }
            else
            {
                _b.Layer(layer);
            }
        }

        #endregion

        #region 3DFACEs

        /// <summary>
        /// Takes the corners of a 3DFACE, as the library read it, into the meshes of its layer and colour: a triangle, a face of four, or each of the two
        /// triangles of one whose four corners are not in one plane.
        /// </summary>
        private void AddFace(DxfEntity e)
        {
            IReadOnlyList<GeoPoint3> corners = e.Shape is GeoTriangle3 t ? (IReadOnlyList<GeoPoint3>)new[] { t.A, t.B, t.C } : ((GeoFace3)e.Shape).Boundary.Vertices;
            string color = ItemColour(e);
            string key = e.Layer + "|" + color;

            if (!_faceSetIndex.TryGetValue(key, out FaceSet set))
            {
                set = new FaceSet { Layer = e.Layer, Color = color };
                _faceSetIndex.Add(key, set);
                _faceSets.Add(set);
            }

            set.Weld.Faces.Add(corners.Select(p => set.Weld.Vertex(new Vec3(p.X, p.Y, p.Z))).ToArray());
        }

        /// <summary>
        /// Makes meshes of the 3DFACEs, one for each body or loose piece of surface of a layer, told apart by their edges (see <see cref="MeshShells"/>): two bodies on
        /// one layer are two items however many corners they share, a body with a void in it is one, and a corner a hair off still closes a body.
        /// </summary>
        private void EmitFaceSets()
        {
            double tolerance = _b.Scene.EqualPoint;

            foreach (FaceSet set in _faceSets)
            {
                _cancel.ThrowIfCancellationRequested();
                List<ShellPiece> pieces = MeshShells.Split(set.Weld.Points, set.Weld.Faces, tolerance, _cancel);
                EnsureLayer(set.Layer);

                for (int p = 0; p < pieces.Count; p++)
                {
                    MeshShells.Extract(set.Weld.Points, set.Weld.Faces, pieces[p].Faces, false, out List<Vec3> points, out List<int[]> faces);
                    string name = set.Layer + " mesh" + (pieces.Count > 1 ? " " + (p + 1).ToString(CultureInfo.InvariantCulture) : string.Empty);
                    _b.Add("GeoMesh3", ImportData.Mesh(points, faces, 3), set.Layer, name, set.Color, "DXF 3DFACE");
                }
            }
        }

        #endregion
    }
}
