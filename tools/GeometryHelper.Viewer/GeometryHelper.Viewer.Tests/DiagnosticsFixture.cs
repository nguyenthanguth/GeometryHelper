using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// Scenes of hand-built faults for the diagnostics tests, and a way to run a check on them.
    /// </summary>
    internal static class Diag
    {
        internal static string N(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>
        /// A list of points as the JSON of a scene, from x, y pairs.
        /// </summary>
        internal static string Pts2(params double[] xy)
            => "[" + string.Join(",", Enumerable.Range(0, xy.Length / 2).Select(i => "[" + N(xy[2 * i]) + "," + N(xy[2 * i + 1]) + "]")) + "]";

        /// <summary>
        /// A list of points as the JSON of a scene, from x, y, z triples.
        /// </summary>
        internal static string Pts3(params double[] xyz)
            => "[" + string.Join(",", Enumerable.Range(0, xyz.Length / 3).Select(i => "[" + N(xyz[3 * i]) + "," + N(xyz[3 * i + 1]) + "," + N(xyz[3 * i + 2]) + "]")) + "]";

        /// <summary>
        /// An item written by hand, with the numbers exactly as given: the library would merge two vertices a thousandth apart, and the
        /// diagnostics read what the scene holds.
        /// </summary>
        internal static SceneItem Raw(string kind, string json, string name = null, string layer = "default")
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                return new SceneItem { Kind = kind, Data = document.RootElement.Clone(), Name = name ?? kind, Layer = layer };
            }
        }

        internal static SceneItem Polygon2(string name, params double[] xy) => Raw("GeoPolygon2", "{\"points\":" + Pts2(xy) + "}", name);

        internal static SceneItem Face3(string name, params double[] xyz) => Raw("GeoFace3", "{\"outer\":" + Pts3(xyz) + ",\"holes\":[]}", name);

        internal static Scene SceneOf(params SceneItem[] items)
        {
            var scene = new Scene { Title = "test" };
            scene.Layers.Add(new Layer { Name = "default", Color = "#1F77B4" });

            foreach (SceneItem item in items)
            {
                if (scene.FindLayer(item.Layer) == null)
                {
                    scene.Layers.Add(new Layer { Name = item.Layer, Color = SceneColor.FromCycle(scene.Layers.Count) });
                }

                scene.Items.Add(item);
            }

            return scene;
        }

        internal static DiagnosticReport Run(Scene scene, DiagnosticChecks checks, bool useLibrary = true, params int[] items)
        {
            Primitives primitives = Primitives.Build(scene);
            ExactGeometry geometry = ExactGeometry.Build(scene, primitives);
            var options = new DiagnosticOptions { Checks = checks, UseLibrary = useLibrary };
            return SceneDiagnostics.Run(scene, primitives, geometry, items.Length == 0 ? null : items, options, CancellationToken.None);
        }

        internal static IEnumerable<DiagnosticFinding> Of(DiagnosticReport report, DiagnosticKind kind) => report.Findings.Where(f => f.Kind == kind);

        internal static int Count(DiagnosticReport report, DiagnosticKind kind) => Of(report, kind).Count();

        /// <summary>
        /// The strokes of a colour in the overlay of a report.
        /// </summary>
        internal static int Strokes(DiagnosticReport report, string color) => report.Overlay.Strokes.Count(s => s.Color == color);

        /// <summary>
        /// A face from the corners given, counter-clockwise seen from the normal.
        /// </summary>
        internal static GeoFace3 Face(params double[] xyz)
            => new GeoFace3(new GeoPolygon3(Enumerable.Range(0, xyz.Length / 3).Select(i => new GeoPoint3(xyz[3 * i], xyz[3 * i + 1], xyz[3 * i + 2]))), null);

        /// <summary>
        /// A box 10 by 10 by 10 with its top left out.
        /// </summary>
        internal static GeoSolid3 OpenBox()
        {
            GeoSolid3 box = SampleShapes.Box(10.0, 10.0, 10.0, false);
            return new GeoSolid3(box.Faces.Where(face => face.Normal.Z < 0.5));
        }

        /// <summary>
        /// A box 10 by 10 by 10 whose first face is wound the wrong way round: each of its four edges is run the same way by it and by the
        /// face beside it.
        /// </summary>
        internal static GeoSolid3 TurnedBox()
        {
            GeoSolid3 box = SampleShapes.Box(10.0, 10.0, 10.0, false);
            var faces = box.Faces.ToList();
            faces[0] = new GeoFace3(new GeoPolygon3(faces[0].Boundary.Vertices.Reverse()), null);
            return new GeoSolid3(faces);
        }

        /// <summary>
        /// A box 10 by 10 by 10 with a fin 10 by 10 standing on its front top edge: that edge is used by three faces, the top,
        /// the front and the fin.
        /// </summary>
        internal static GeoSolid3 FinnedBox()
        {
            GeoSolid3 box = SampleShapes.Box(10.0, 10.0, 10.0, false);
            return new GeoSolid3(box.Faces.Concat(new[] { Face(0, 0, 10, 10, 0, 10, 10, 0, 20, 0, 0, 20) }));
        }
    }
}
