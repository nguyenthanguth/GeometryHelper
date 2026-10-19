using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Looks for faults in the shapes of a scene: bodies and meshes that do not close, loops that cross themselves, vertices that are one
    /// point to the library, needles and slivers, and the gap between two shapes. Where the library has a reading it is used, at the
    /// tolerance of the scene, and the finding names the member that gave it; the viewer's own checks fill the gaps, from the exact
    /// numbers of the items. Every result is in a fixed order: no finding follows the order of a hash table.
    /// </summary>
    public static class SceneDiagnostics
    {
        /// <summary>
        /// Runs the checks on some items of a scene, with the library under the tolerance of the scene on this thread.
        /// </summary>
        /// <param name="scene">The scene.</param>
        /// <param name="primitives">What the scene is drawn with, which gives the pieces their outlines and the gap its lens.</param>
        /// <param name="geometry">The exact curves of the scene, which give the tolerance spheres their vertices; null reads them again.</param>
        /// <param name="items">The indexes of the items to check; null or none means every item.</param>
        /// <param name="options">The checks to run and the limits; null means the defaults.</param>
        /// <param name="cancel">Stops the run between items and inside the long sweeps, by throwing <see cref="OperationCanceledException"/>.</param>
        /// <returns>The findings, numbered, with what is drawn for them.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the scene or the primitives are null.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
        public static DiagnosticReport Run(Scene scene, Primitives primitives, ExactGeometry geometry, IReadOnlyCollection<int> items, DiagnosticOptions options, CancellationToken cancel)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            if (primitives == null)
            {
                throw new ArgumentNullException(nameof(primitives));
            }

            return new DiagnosticRun(scene, primitives, geometry, options ?? new DiagnosticOptions(), cancel).Execute(items);
        }
    }

    /// <summary>
    /// One run of the checks.
    /// </summary>
    internal sealed partial class DiagnosticRun
    {
        private readonly Scene _scene;
        private readonly Primitives _primitives;
        private ExactGeometry _geometry;
        private readonly DiagnosticOptions _options;
        private readonly CancellationToken _cancel;
        private readonly double _tol;
        private readonly double _planar;
        private readonly List<DiagnosticFinding> _all = new List<DiagnosticFinding>();
        private readonly List<DiagnosticFinding> _pending = new List<DiagnosticFinding>();
        private GapReading _gap;

        internal DiagnosticRun(Scene scene, Primitives primitives, ExactGeometry geometry, DiagnosticOptions options, CancellationToken cancel)
        {
            _scene = scene;
            _primitives = primitives;
            _geometry = geometry;
            _options = options;
            _cancel = cancel;
            _tol = scene.EqualPoint;
            _planar = scene.EqualPlanar;
        }

        private bool On(DiagnosticChecks check) => (_options.Checks & check) != 0;

        internal DiagnosticReport Execute(IReadOnlyCollection<int> items)
        {
            var targets = new List<int>();

            if (items == null || items.Count == 0)
            {
                for (int i = 0; i < _scene.Items.Count; i++)
                {
                    targets.Add(i);
                }
            }
            else
            {
                targets.AddRange(items.Where(i => i >= 0 && i < _scene.Items.Count).Distinct().OrderBy(i => i));
            }

            using (Tolerance.Use(_scene.Tolerance))
            {
                foreach (int index in targets)
                {
                    _cancel.ThrowIfCancellationRequested();
                    _pending.Clear();
                    SceneItem item = _scene.Items[index];

                    if (item.Kind != SceneKinds.Text && SceneKinds.IsKnown(item.Kind))
                    {
                        ItemModel model = null;

                        try
                        {
                            model = ItemModel.Read(item, index);
                            CheckItem(model);
                        }
                        catch (Exception e) when (e is FormatException || e is InvalidOperationException || e is ArgumentException || e is KeyNotFoundException || e is NotSupportedException)
                        {
                            Add(DiagnosticChecks.None, DiagnosticKind.Note, false, index, item.Name ?? item.Kind, "could not be read for the checks: " + e.Message, "viewer", default(Vec3), 0.0, 0.0);
                        }
                    }

                    Flush();
                }

                if (On(DiagnosticChecks.Gap))
                {
                    GapCheck(targets);
                    Flush();
                }

                if (On(DiagnosticChecks.Pieces))
                {
                    PiecesCheck(targets);
                    Flush();
                }
            }

            var overlay = new DiagnosticOverlay();

            for (int n = 0; n < _all.Count; n++)
            {
                _all[n].Number = n + 1;
                overlay.Strokes.AddRange(_all[n].Drawing.Strokes);
                overlay.Dots.AddRange(_all[n].Drawing.Dots);
                overlay.Labels.AddRange(_all[n].Drawing.Labels);
            }

            return new DiagnosticReport(_all.ToArray(), overlay, targets, _gap);
        }

        /// <summary>
        /// Sorts what one item found into its fixed order, keeps the most that may be kept of each kind, and adds the rest to the report.
        /// </summary>
        private void Flush()
        {
            _pending.Sort(DiagnosticFinding.Compare);
            int cap = Math.Max(1, _options.MaxFindingsPerKind);
            int run = 0;
            DiagnosticFinding previous = null;
            int dropped = 0;

            for (int i = 0; i <= _pending.Count; i++)
            {
                DiagnosticFinding f = i < _pending.Count ? _pending[i] : null;

                if (previous != null && (f == null || f.Check != previous.Check || f.Kind != previous.Kind || f.ItemIndex != previous.ItemIndex))
                {
                    if (dropped > 0)
                    {
                        _all.Add(new DiagnosticFinding(previous.Check, DiagnosticKind.Note, false, previous.ItemIndex, previous.ItemName, dropped.ToString(CultureInfo.InvariantCulture) + " more of " + previous.Kind + " are not listed: only the first " + cap.ToString(CultureInfo.InvariantCulture) + " in order of place are kept", "viewer", previous.At, 0.0, previous.SpotRadius));
                    }

                    run = 0;
                    dropped = 0;
                }

                if (f == null)
                {
                    break;
                }

                if (run < cap)
                {
                    _all.Add(f);
                    run++;
                }
                else
                {
                    dropped++;
                }

                previous = f;
            }

            _pending.Clear();
        }

        private DiagnosticFinding Add(DiagnosticChecks check, DiagnosticKind kind, bool fault, int itemIndex, string itemName, string message, string source, Vec3 at, double size, double spot)
        {
            var finding = new DiagnosticFinding(check, kind, fault, itemIndex, itemName, message, source, at, size, Math.Max(spot, 20.0 * Math.Max(_tol, 1E-9)));
            _pending.Add(finding);
            return finding;
        }

        private DiagnosticFinding Add(ItemModel m, DiagnosticChecks check, DiagnosticKind kind, bool fault, string message, string source, Vec3 at, double size, double spot)
            => Add(check, kind, fault, m.Index, m.Name, message, source, at, size, spot);

        internal static string N(double value) => value.ToString("G10", CultureInfo.InvariantCulture);

        private static string Count(int n, string noun) => n.ToString(CultureInfo.InvariantCulture) + " " + (n == 1 ? noun : noun == "vertex" ? "vertices" : noun + "s");

        private static string Number(int zeroBased) => (zeroBased + 1).ToString(CultureInfo.InvariantCulture);

        private ExactGeometry Geometry()
        {
            if (_geometry == null)
            {
                _geometry = ExactGeometry.Build(_scene, _primitives);
            }

            return _geometry;
        }

        private void CheckItem(ItemModel m)
        {
            // The library first: a body is rebuilt and asked, and what it cannot say, or is not asked, the viewer's own checks say.
            bool wantsLibrary = _options.UseLibrary && (On(DiagnosticChecks.Report) || On(DiagnosticChecks.Edges) || On(DiagnosticChecks.Flatness) || On(DiagnosticChecks.Slivers) || On(DiagnosticChecks.Duplicates));

            foreach (BodyUnit unit in m.Units)
            {
                _cancel.ThrowIfCancellationRequested();
                bool library = unit.IsSolid && wantsLibrary && LibraryBody(m, unit);
                BodyChecks(m, unit, library);
            }

            foreach (LoopData loop in m.Loops)
            {
                _cancel.ThrowIfCancellationRequested();
                LoopChecks(m, loop);
            }

            NeedleFacts(m);

            if (On(DiagnosticChecks.Spheres))
            {
                SpheresCheck(m);
            }
        }
    }
}
