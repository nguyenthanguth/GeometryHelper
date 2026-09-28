using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.TeklaConvert;
using Tekla.Structures.Model;

namespace Tekla2025Test
{
    /// <summary>
    /// Checks the reinforcement selected in Tekla Structures against the selected IFC objects by the bars' centre
    /// lines and radii (<see cref="ClashBar"/>), with no body built for any bar, all in the current work plane, and
    /// lists every clash found, with the IFC member it is with, in a <see cref="ClashReportForm"/>. It is
    /// <see cref="RebarIfcClashCheck"/> stage for stage, with the bars read instead of built, so that the two can be
    /// timed side by side. The "Clash check by centre line: selected rebar vs IFC" button of <see cref="Form1"/> runs
    /// it.
    /// </summary>
    internal sealed class RebarIfcClashBarCheck
    {
        /// <summary>
        /// How near, in millimetres, a bar may come to an IFC body before the pair is reported, measured from the bar's
        /// surface. Keep it the same as in <see cref="RebarIfcClashCheck"/> to time the two alike.
        /// </summary>
        private const double Clearance = 0.0;

        /// <summary>
        /// How many pairs of clashing members the summary lists; every clash has a row of its own in the report window.
        /// </summary>
        private const int ListedPairs = 20;

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
                    MessageBox.Show("Select reinforcement and the reference objects to check it against.", "Clash check by centre line");
                    return;
                }

                // Every bar as the model view shows it (hooks and laps worked out, and moved off itself where a
                // stirrup's hooks would run through it), by its centre line, bends as arcs, and its own radius, and the
                // reinforcement it belongs to; no body is built. Asking Tekla for the geometries and reading them take
                // turns, so each is timed on its own.
                var bars = new List<ClashBar>();
                var barOwners = new List<Reinforcement>();
                int unread = 0;
                var asking = new Stopwatch();
                var reading = new Stopwatch();
                foreach (Reinforcement reinforcement in reinforcements)
                {
                    asking.Start();
                    var geometries = reinforcement.GetRebarGeometriesWithoutClashes(true);
                    asking.Stop();
                    if (geometries == null)
                    {
                        continue;
                    }

                    reading.Start();
                    foreach (RebarGeometry geometry in geometries.OfType<RebarGeometry>())
                    {
                        try
                        {
                            bars.Add(new ClashBar(geometry.ToGeoPolylineArc3(), geometry.ToBarRadius()));
                            barOwners.Add(reinforcement);
                        }
                        catch (Exception error) when (error is ArgumentException || error is InvalidOperationException)
                        {
                            // One bar with no length or no radius should not cost the rest.
                            unread++;
                        }
                    }
                    reading.Stop();
                }

                stages.Add(("Reading bar geometries from Tekla", asking.Elapsed));
                stages.Add(("Reading the bars by their centre lines", reading.Elapsed));
                lap.Restart();

                // The IFC products, each with the bodies it is made of, every body known by its product so that a clash
                // can say which member it is with: the same work as ToGeoSolids, which gives the bodies alone. Their
                // openings are left uncut here (ApplyVoids = false) for speed, as in RebarIfcClashCheck, so a bar
                // through a hole in a web is reported; set it to true when openings matter.
                IReadOnlyList<IfcProductGeometry> products = referenceObjects.ToIfcGeometries(new IfcConvertOptions { ApplyVoids = false });
                var bodies = new List<GeoSolid3>();
                var bodyOwners = new List<IfcProductGeometry>();
                foreach (IfcProductGeometry product in products)
                {
                    foreach (GeoSolid3 solid in product.Solids)
                    {
                        bodies.Add(solid);
                        bodyOwners.Add(product);
                    }
                }

                Lap("Reading IFC bodies (IFC file and conversion)");

                // The bars against the IFC bodies: First indexes the bars, Second the IFC bodies. Clash3.Find prepares the
                // IFC bodies itself (meshes and indexes) on all cores, so this stage times preparing and checking
                // together; a bar read by its centre line needs no mesh or index of its own.
                ClashResult[] clashes = Clash3.Find(bars, bodies, new ClashOptions(clearance: Clearance));
                Lap("Preparing the IFC bodies and checking the pairs (Clash3.Find)");

                string notRead = unread > 0 ? $" ({unread} could not be read)" : string.Empty;
                string found =
                    $"{bars.Count} bar(s){notRead} of {reinforcements.Count} reinforcement(s), by centre line, " +
                    $"against {bodies.Count} IFC body(ies), {bodies.Sum(body => body.Faces.Count)} faces, " +
                    $"of {products.Count} IFC product(s) from {referenceObjects.Count} reference object(s)." +
                    Environment.NewLine +
                    $"{clashes.Count(c => c.Kind == ClashKind.Hard)} hard, {clashes.Count(c => c.Kind == ClashKind.Touch)} touching, " +
                    $"{clashes.Count(c => c.Kind == ClashKind.Clearance)} nearer than {Clearance} mm, {clashes.Count(c => c.Kind == ClashKind.Unresolved)} unresolved.";

                // Nothing in the model changed. The clashes are listed in a window of their own, which leaves Tekla free to
                // use, and draws a clash and zooms to it when asked.
                string summary = found + Environment.NewLine + Environment.NewLine + Members(clashes, barOwners, bodyOwners) +
                    Environment.NewLine + Environment.NewLine + Timings(stages, total.Elapsed);
                new ClashReportForm("Clash check by centre line", summary, clashes, barOwners, bodyOwners).Show();
            }
            catch (Exception ex)
            {
                Lap("Until it failed");
                MessageBox.Show(Timings(stages, total.Elapsed) + Environment.NewLine + Environment.NewLine + ex, "Clash check by centre line failed");
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
        /// The members that clash, a line to a pair: which reinforcement, which IFC product, and what they do. A
        /// reinforcement of several bars, or a product of several bodies, can clash more than once; each pair of
        /// members is listed once, with how many clashes of each kind it has.
        /// </summary>
        private static string Members(IEnumerable<ClashResult> clashes, IReadOnlyList<Reinforcement> barOwners, IReadOnlyList<IfcProductGeometry> bodyOwners)
        {
            // By the product itself rather than its GlobalId: the same IFC file loaded twice is two sets of members.
            var pairs = clashes.GroupBy(clash => (Rebar: barOwners[clash.First].Identifier.ID, Product: bodyOwners[clash.Second])).ToList();

            if (pairs.Count == 0)
            {
                return "No members clash.";
            }

            var text = new StringBuilder();
            text.AppendLine(pairs.Count > ListedPairs
                ? $"{pairs.Count} pairs of members clash; the first {ListedPairs}:"
                : $"{pairs.Count} pair(s) of members clash:");

            foreach (var pair in pairs.Take(ListedPairs))
            {
                ClashResult[] hard = pair.Where(clash => clash.Kind == ClashKind.Hard).ToArray();
                ClashResult[] near = pair.Where(clash => clash.Kind == ClashKind.Clearance).ToArray();
                int touching = pair.Count(clash => clash.Kind == ClashKind.Touch);
                int unresolved = pair.Count(clash => clash.Kind == ClashKind.Unresolved);
                var what = new List<string>();

                if (hard.Length > 0)
                {
                    what.Add($"{hard.Length} hard, the deepest {hard.Max(clash => clash.Depth):0.0} mm");
                }

                if (touching > 0)
                {
                    what.Add($"{touching} touching");
                }

                if (near.Length > 0)
                {
                    what.Add($"{near.Length} nearer than {Clearance} mm, the nearest {near.Min(clash => clash.Distance):0.0} mm");
                }

                if (unresolved > 0)
                {
                    what.Add($"{unresolved} unresolved");
                }

                text.AppendLine($"   rebar {pair.Key.Rebar} x {NameOf(pair.Key.Product)} ({pair.Key.Product.GlobalId}): {string.Join(", ", what)}");
            }

            return text.ToString().TrimEnd();
        }

        /// <summary>
        /// An IFC member in a few words: its type, and its name when it has one.
        /// </summary>
        private static string NameOf(IfcProductGeometry member)
        {
            return string.IsNullOrWhiteSpace(member.Name) ? member.IfcType : member.IfcType + " " + member.Name;
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
