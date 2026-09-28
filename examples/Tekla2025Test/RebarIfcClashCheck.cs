using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.Spatial;
using GeometryHelper.TeklaConvert;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;

namespace Tekla2025Test
{
    /// <summary>
    /// Checks the reinforcement selected in Tekla Structures against the selected IFC objects, draws every clash
    /// found as temporary graphics, and shows what was found and how long each stage took, all in the current work
    /// plane. The "Clash check: selected rebar vs IFC" button of <see cref="Form1"/> runs it.
    /// </summary>
    internal sealed class RebarIfcClashCheck
    {
        /// <summary>
        /// How near, in millimetres, a bar may come to an IFC body before the pair is reported.
        /// </summary>
        private const double Clearance = 25.0;

        /// <summary>
        /// How far, in millimetres, the flat sides of a bar's body may stray from its round section.
        /// </summary>
        private const double BarChordTolerance = 0.1;

        /// <summary>
        /// How far, in millimetres, the marker drawn for a clash with no volume or face of its own reaches.
        /// </summary>
        private const double MarkerRadius = 25.0;

        private readonly GraphicsDrawer graphicsDrawer = new GraphicsDrawer();

        /// <summary>
        /// Time spent meshing what is drawn, and sending it to Tekla, over one check.
        /// </summary>
        private readonly Stopwatch meshing = new Stopwatch();
        private readonly Stopwatch sending = new Stopwatch();

        /// <summary>
        /// Runs the check on what is selected in the model view now.
        /// </summary>
        public void Run()
        {
            // How long each stage takes, in the order they run: a lap closes the stage just run.
            var stages = new List<(string Stage, TimeSpan Time)>();
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch lap = Stopwatch.StartNew();

            void Lap(string stage)
            {
                stages.Add((stage, lap.Elapsed));
                lap.Restart();
            }

            try
            {
                // The reinforcement and the IFC objects selected in the model view (Tekla.Structures.Model has a
                // ModelObjectSelector too, hence the full name). A rebar set is not a Reinforcement, but holds them.
                var reinforcements = new List<Reinforcement>();
                var referenceObjects = new List<ReferenceModelObject>();
                ModelObjectEnumerator selection = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
                while (selection.MoveNext())
                {
                    if (selection.Current is Reinforcement reinforcement)
                    {
                        reinforcements.Add(reinforcement);
                    }
                    else if (selection.Current is RebarSet rebarSet)
                    {
                        reinforcements.AddRange(ReinforcementsOf(rebarSet));
                    }
                    else if (selection.Current is ReferenceModelObject referenceObject)
                    {
                        referenceObjects.Add(referenceObject);
                    }
                }

                Lap("Reading the selection");

                if (reinforcements.Count == 0 || referenceObjects.Count == 0)
                {
                    MessageBox.Show("Select reinforcement and the reference objects to check it against.", "Clash check");
                    return;
                }

                // No work plane is set. Tekla gives the bars in the current one, ToGeoSolids gives the IFC bodies in it
                // and GraphicsDrawer draws in it, and the check finds the same clashes in any plane.

                // Every bar as the model view shows it (hooks and laps worked out, and moved off itself where a
                // stirrup's hooks would run through it), as a round body of the bar's own diameter. Asking Tekla for
                // the geometries and building the bodies take turns, so each is timed on its own.
                var bars = new List<GeoSolid3>();
                int unbuilt = 0;
                var asking = new Stopwatch();
                var building = new Stopwatch();
                foreach (Reinforcement reinforcement in reinforcements)
                {
                    asking.Start();
                    var geometries = reinforcement.GetRebarGeometriesWithoutClashes(true);
                    asking.Stop();
                    if (geometries == null)
                    {
                        continue;
                    }

                    building.Start();
                    foreach (RebarGeometry geometry in geometries.OfType<RebarGeometry>())
                    {
                        try
                        {
                            bars.Add(GeoSolid3.Pipe(geometry.ToGeoPolylineArc3(), geometry.ToBarRadius(), BarChordTolerance));
                        }
                        catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
                        {
                            // One bar with no length, or turning back on itself, should not cost the rest.
                            unbuilt++;
                        }
                    }
                    building.Stop();
                }

                stages.Add(("Reading bar geometries from Tekla", asking.Elapsed));
                stages.Add(("Building bar bodies", building.Elapsed));
                lap.Restart();

                // The IFC bodies. Their openings are left uncut here (ApplyVoids = false) for speed, so a bar
                // through a hole in a web is reported; set it to true when openings matter.
                GeoSolid3[] bodies = referenceObjects.ToGeoSolids(new IfcConvertOptions { ApplyVoids = false });
                Lap("Reading IFC bodies (IFC file and conversion)");

                // Prepared here rather than inside Clash3.Find, so that preparing and checking are timed apart.
                GeoPreparedSolid3[] preparedBars = Prepare(bars);
                GeoPreparedSolid3[] preparedBodies = Prepare(bodies);
                Lap("Preparing the parts (meshes and indexes)");

                // One set against the other: First indexes the bars, Second the IFC bodies.
                ClashResult[] clashes = Clash3.Find(preparedBars, preparedBodies, new ClashOptions(clearance: Clearance));
                Lap("Checking the pairs (Clash3.Find)");

                meshing.Reset();
                sending.Reset();
                foreach (ClashResult clash in clashes)
                {
                    DrawClash(clash);
                }

                stages.Add(("Meshing the clashes to draw", meshing.Elapsed));
                stages.Add(("Drawing in Tekla (DrawMeshSurface, DrawText)", sending.Elapsed));
                lap.Restart();

                string notBuilt = unbuilt > 0 ? $" ({unbuilt} could not be built)" : string.Empty;
                string found =
                    $"{bars.Count} bar(s){notBuilt}, {bars.Sum(bar => bar.Faces.Count)} faces, of {reinforcements.Count} reinforcement(s), " +
                    $"against {bodies.Length} IFC body(ies), {bodies.Sum(body => body.Faces.Count)} faces, of {referenceObjects.Count} reference object(s)." +
                    Environment.NewLine +
                    $"{clashes.Count(c => c.Kind == ClashKind.Hard)} hard, {clashes.Count(c => c.Kind == ClashKind.Touch)} touching, " +
                    $"{clashes.Count(c => c.Kind == ClashKind.Clearance)} nearer than {Clearance} mm, {clashes.Count(c => c.Kind == ClashKind.Unresolved)} unresolved.";

                // Nothing in the model changed, and temporary graphics need no commit.
                MessageBox.Show(found + Environment.NewLine + Environment.NewLine + Timings(stages, total.Elapsed), "Clash check");
            }
            catch (Exception ex)
            {
                Lap("Until it failed");
                MessageBox.Show(Timings(stages, total.Elapsed) + Environment.NewLine + Environment.NewLine + ex, "Clash check failed");
            }
        }

        /// <summary>
        /// Lists how long each stage took, with its share of the whole.
        /// </summary>
        private static string Timings(List<(string Stage, TimeSpan Time)> stages, TimeSpan total)
        {
            var text = new StringBuilder();

            foreach ((string stage, TimeSpan time) in stages)
            {
                text.AppendLine($"{stage}: {time.TotalMilliseconds:0.#} ms ({time.TotalMilliseconds / total.TotalMilliseconds:0%})");
            }

            text.Append($"Total: {total.TotalMilliseconds:0.#} ms");
            return text.ToString();
        }

        /// <summary>
        /// Prepares every part on all cores, as Clash3.Find does when it is handed bodies.
        /// </summary>
        private static GeoPreparedSolid3[] Prepare(IReadOnlyList<GeoSolid3> parts)
        {
            var prepared = new GeoPreparedSolid3[parts.Count];
            Parallel.For(0, parts.Count, i => prepared[i] = parts[i].Prepare());
            return prepared;
        }

        /// <summary>
        /// Draws a clash as temporary graphics in the active rendered view: the volume the two share, the faces
        /// they touch by, or a marker where there is neither, labelled with what it is.
        /// </summary>
        private void DrawClash(ClashResult clash)
        {
            Color color = ColorOf(clash.Kind);
            string label;

            switch (clash.Kind)
            {
                case ClashKind.Hard:
                    // The part of the bar inside the IFC body, hidden by the body unless that is drawn see-through.
                    DrawSurface(clash.Overlaps.SelectMany(overlap => overlap.TriangulateSurface()), color);
                    label = $"Hard {clash.Volume:0} mm3";
                    break;

                case ClashKind.Touch:
                    // Face to face, the patches they lie against each other by; along an edge or at a corner, none.
                    DrawSurface(clash.Contact.Count > 0 ? clash.Contact.SelectMany(patch => patch.TriangulateSurface()) : Marker(clash.Location), color);
                    label = $"Touch {clash.ContactArea:0} mm2";
                    break;

                case ClashKind.Clearance:
                    // Apart but too near: the shortest segment across the gap, and a marker at its middle.
                    GeoLine3 gap = clash.Gap.Value;
                    sending.Start();
                    graphicsDrawer.DrawLineSegment(gap.StartPoint.ToTeklaPoint(), gap.EndPoint.ToTeklaPoint(), color);
                    sending.Stop();
                    DrawSurface(Marker(clash.Location), color);
                    label = $"Gap {clash.Distance:0.0} mm";
                    break;

                default:
                    // Checking the pair failed: a marker in the space their boxes share.
                    DrawSurface(Marker(clash.Location), color);
                    label = "Unresolved: " + clash.Error.GetType().Name;
                    break;
            }

            sending.Start();
            graphicsDrawer.DrawText(clash.Location.ToTeklaPoint(), label, color);
            sending.Stop();
        }

        /// <summary>
        /// Draws triangles as one mesh, both sides of each: DrawMeshSurface shows only the side a triangle turns
        /// counterclockwise to, and a contact face may be looked at from either.
        /// </summary>
        private void DrawSurface(IEnumerable<GeoTriangle3> triangles, Color color)
        {
            // The triangles are worked out as they are read, so meshing an overlap is timed here too.
            meshing.Start();
            var mesh = new Mesh();

            foreach (GeoTriangle3 triangle in triangles)
            {
                int a = mesh.AddPoint(triangle.A.ToTeklaPoint());
                int b = mesh.AddPoint(triangle.B.ToTeklaPoint());
                int c = mesh.AddPoint(triangle.C.ToTeklaPoint());

                mesh.AddTriangle(a, b, c);
                mesh.AddTriangle(a, c, b);
            }

            meshing.Stop();

            sending.Start();
            graphicsDrawer.DrawMeshSurface(mesh, color);
            sending.Stop();
        }

        /// <summary>
        /// A diamond around a point, for a clash with no volume or face of its own to draw.
        /// </summary>
        private static GeoTriangle3[] Marker(GeoPoint3 at)
        {
            var east = new GeoPoint3(at.X + MarkerRadius, at.Y, at.Z);
            var west = new GeoPoint3(at.X - MarkerRadius, at.Y, at.Z);
            var north = new GeoPoint3(at.X, at.Y + MarkerRadius, at.Z);
            var south = new GeoPoint3(at.X, at.Y - MarkerRadius, at.Z);
            var top = new GeoPoint3(at.X, at.Y, at.Z + MarkerRadius);
            var bottom = new GeoPoint3(at.X, at.Y, at.Z - MarkerRadius);

            return new[]
            {
                new GeoTriangle3(east, north, top), new GeoTriangle3(north, west, top),
                new GeoTriangle3(west, south, top), new GeoTriangle3(south, east, top),
                new GeoTriangle3(north, east, bottom), new GeoTriangle3(west, north, bottom),
                new GeoTriangle3(south, west, bottom), new GeoTriangle3(east, south, bottom),
            };
        }

        /// <summary>
        /// The colour a kind of clash is drawn in: hard red, touching yellow, too near orange, unresolved magenta.
        /// </summary>
        private static Color ColorOf(ClashKind kind)
        {
            switch (kind)
            {
                case ClashKind.Hard:
                    return new Color(1.0, 0.0, 0.0);
                case ClashKind.Touch:
                    return new Color(1.0, 1.0, 0.0);
                case ClashKind.Clearance:
                    return new Color(1.0, 0.5, 0.0);
                default:
                    return new Color(1.0, 0.0, 1.0);
            }
        }

        /// <summary>
        /// The reinforcements a rebar set made.
        /// </summary>
        private static IEnumerable<Reinforcement> ReinforcementsOf(RebarSet rebarSet)
        {
            ModelObjectEnumerator made = rebarSet.GetReinforcements();

            while (made != null && made.MoveNext())
            {
                if (made.Current is Reinforcement reinforcement)
                {
                    yield return reinforcement;
                }
            }
        }
    }
}
