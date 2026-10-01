using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.TeklaConvert;
using Tekla.Structures.Model;

namespace Tekla2025Test
{
    /// <summary>
    /// Draws the parts and IFC objects selected in Tekla Structures as they are read, in the current work plane: either
    /// every face of each <see cref="GeoSolid3"/> (<see cref="DrawFaces"/>), or every triangle of
    /// <see cref="GeoSolid3.Triangulate()"/> (<see cref="DrawTriangles"/>). The "Draw selected as GeoSolid3" and "Draw
    /// selected as GeoSolid3 triangles" buttons of <see cref="Form1"/> run them.
    /// </summary>
    /// <remarks>
    /// What one run draws, the next run of either takes out again, and so does closing the test window, leaving the model
    /// as it was found; <see cref="Form1"/> keeps one instance for that.
    /// </remarks>
    internal sealed class BodyDraw
    {
        /// <summary>
        /// Whether the openings of IFC objects are cut from them first (<see cref="IfcConvertOptions.ApplyVoids"/>), so
        /// that a window or a bolt hole shows as the model view shows it. It costs a boolean operation per opening.
        /// </summary>
        private const bool CutIfcOpenings = true;

        /// <summary>
        /// How many polycurves a run draws without asking: Tekla inserts them one at a time, a few thousand a second.
        /// </summary>
        private const int DrawWithoutAsking = 5000;

        private const ControlObjectColorEnum FaceColor = ControlObjectColorEnum.BLUE;

        private const ControlObjectColorEnum HoleColor = ControlObjectColorEnum.CYAN;

        private const ControlObjectColorEnum TriangleColor = ControlObjectColorEnum.GREEN;

        /// <summary>
        /// What the last run drew, to take out again.
        /// </summary>
        private readonly List<ControlPolycurve> drawn = new List<ControlPolycurve>();

        private readonly Model model = new Model();

        /// <summary>
        /// Draws every face of each selected body, its outer edge blue and its holes cyan: a face Tekla left out of flat
        /// shows as the triangles it is read as.
        /// </summary>
        public void DrawFaces()
        {
            Run("GeoSolid3", bodies => bodies.Sum(body => body.Faces.Sum(face => 1 + face.Holes.Count)), DrawFacesOf);
        }

        /// <summary>
        /// Draws every triangle of each selected body, as <see cref="GeoSolid3.Triangulate()"/> breaks its faces up.
        /// </summary>
        public void DrawTriangles()
        {
            Run("GeoSolid3 triangles", bodies => bodies.Sum(body => body.Triangulate().Length), DrawTrianglesOf);
        }

        /// <summary>
        /// Takes what the last run drew out of the model; the caller commits.
        /// </summary>
        /// <returns>How many polycurves were taken out.</returns>
        public int RemoveDrawn()
        {
            int removed = 0;

            foreach (ControlPolycurve polycurve in drawn)
            {
                if (polycurve.Delete())
                {
                    removed++;
                }
            }

            drawn.Clear();
            return removed;
        }

        /// <summary>
        /// Reads what is selected, takes out what the last run drew, draws the bodies the given way and says what it did.
        /// </summary>
        /// <param name="what">What is drawn, for the window titles.</param>
        /// <param name="count">How many polycurves the bodies will take, asked before drawing them.</param>
        /// <param name="draw">Draws one body, returning its polycurves.</param>
        private void Run(string what, Func<List<GeoSolid3>, int> count, Func<GeoSolid3, IEnumerable<ControlPolycurve>> draw)
        {
            string title = "Draw " + what;

            try
            {
                // Tekla.Structures.Model has a ModelObjectSelector too, hence the full name.
                var parts = new List<Part>();
                var referenceObjects = new List<ReferenceModelObject>();
                ModelObjectEnumerator selection = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
                while (selection.MoveNext())
                {
                    if (selection.Current is Part part)
                    {
                        parts.Add(part);
                    }
                    else if (selection.Current is ReferenceModelObject referenceObject)
                    {
                        referenceObjects.Add(referenceObject);
                    }
                }

                if (parts.Count == 0 && referenceObjects.Count == 0)
                {
                    MessageBox.Show("Select the parts or IFC objects to draw, then press the button again.", title);
                    return;
                }

                // No work plane is set: the parts' solids and the IFC bodies come in the current one, and are drawn in it.
                Stopwatch reading = Stopwatch.StartNew();
                var bodies = new List<GeoSolid3>();
                int unreadable = 0;

                foreach (Part part in parts)
                {
                    if (part.GetSolid().TryToGeoSolid3(out GeoSolid3 body))
                    {
                        bodies.Add(body);
                    }
                    else
                    {
                        unreadable++;
                    }
                }

                int products = 0;
                if (referenceObjects.Count > 0)
                {
                    foreach (IfcProductGeometry product in referenceObjects.ToIfcGeometries(new IfcConvertOptions { ApplyVoids = CutIfcOpenings }))
                    {
                        bodies.AddRange(product.Solids);
                        products++;
                    }
                }

                reading.Stop();

                int polycurves = count(bodies);
                if (polycurves > DrawWithoutAsking &&
                    MessageBox.Show($"{bodies.Count} bodies take {polycurves} polycurves, which Tekla inserts one at a time. Draw them?",
                        title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    return;
                }

                if (RemoveDrawn() > 0)
                {
                    model.CommitChanges();
                }

                Stopwatch drawing = Stopwatch.StartNew();
                foreach (GeoSolid3 body in bodies)
                {
                    drawn.AddRange(draw(body));
                }

                if (drawn.Count > 0)
                {
                    model.CommitChanges();
                }

                drawing.Stop();

                MessageBox.Show(Summary(what, bodies, parts.Count, unreadable, referenceObjects.Count, products, reading.Elapsed, drawing.Elapsed), title);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), title + " failed");
            }
        }

        private static IEnumerable<ControlPolycurve> DrawFacesOf(GeoSolid3 body)
        {
            return body.DrawToTekla(FaceColor, HoleColor);
        }

        private static IEnumerable<ControlPolycurve> DrawTrianglesOf(GeoSolid3 body)
        {
            // DrawPolyline leaves out what it cannot draw, a triangle of no area among them, and returns null for it.
            return body.Triangulate()
                .Select(triangle => new[] { triangle.A, triangle.B, triangle.C }.DrawPolyline(true, TriangleColor))
                .Where(polycurve => polycurve != null);
        }

        /// <summary>
        /// What was read and drawn, and which bodies are not closed.
        /// </summary>
        private string Summary(string what, List<GeoSolid3> bodies, int parts, int unreadable, int referenceObjects, int products, TimeSpan reading, TimeSpan drawing)
        {
            List<GeoSolid3> open = bodies.Where(body => !body.IsClosed()).ToList();
            string notRead = unreadable > 0 ? $" ({unreadable} whose solid could not be read)" : string.Empty;
            var text = new StringBuilder();

            text.AppendLine($"{parts} part(s){notRead} and {referenceObjects} IFC object(s) ({products} product(s)): " +
                $"{bodies.Count} bodies, {bodies.Sum(body => body.Faces.Count)} faces, read in {reading.TotalMilliseconds:0} ms.");
            text.AppendLine($"Drawn as {what}: {drawn.Count} polycurves in {drawing.TotalMilliseconds:0} ms" +
                (what.EndsWith("triangles") ? ", green." : ", faces blue, holes cyan."));
            text.AppendLine(open.Count == 0 ? "Every body is closed." : $"{open.Count} bodies are not closed.");
            text.AppendLine("Pressing either draw button again, or closing the test window, takes them out.");

            return text.ToString().TrimEnd();
        }
    }
}
