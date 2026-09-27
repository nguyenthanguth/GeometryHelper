using GeometryHelper;
using GeometryHelper.Clash;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.Geometry;
using GeometryHelper.TeklaConvert;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GeometryHelper.Enums;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;
using Tekla.Structures.Model.UI;
using Color = Tekla.Structures.Model.UI.Color;

namespace Tekla2025Test
{
    public partial class Form1 : Form
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

        private readonly Model model;
        private readonly GraphicsDrawer graphicsDrawer;

        public Form1()
        {
            InitializeComponent();

            model = new Model();
            graphicsDrawer = new GraphicsDrawer();

            this.Text = model.GetConnectionStatus().ToString();
        }

        /// <summary>
        /// Checks the selected reinforcement against the selected IFC objects and draws every clash found.
        /// </summary>
        private void button1_Click(object sender, EventArgs e)
        {
            WorkPlaneHandler workPlanes = model.GetWorkPlaneHandler();
            TransformationPlane userPlane = workPlanes.GetCurrentTransformationPlane();

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

                if (reinforcements.Count == 0 || referenceObjects.Count == 0)
                {
                    Operation.DisplayPrompt("Select reinforcement and the reference objects to check it against.");
                    return;
                }

                // All in the global plane, so that the bars, the IFC bodies and the temporary graphics share one
                // frame whatever the work plane is; the user's plane is put back in finally.
                workPlanes.SetCurrentTransformationPlane(new TransformationPlane());
                Stopwatch stopwatch = Stopwatch.StartNew();

                // Every bar as the model view shows it (hooks and laps worked out, and moved off itself where a
                // stirrup's hooks would run through it), as a round body of the bar's own diameter.
                var bars = new List<GeoSolid3>();
                int unbuilt = 0;
                foreach (Reinforcement reinforcement in reinforcements)
                {
                    var geometries = reinforcement.GetRebarGeometriesWithoutClashes(true);
                    if (geometries == null)
                    {
                        continue;
                    }

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
                }

                // The IFC bodies with their openings cut, since a bar through a hole in a web is no clash. Cutting
                // costs time: set ApplyVoids to false for speed when no opening matters.
                GeoSolid3[] bodies = referenceObjects.ToGeoSolids(new IfcConvertOptions { ApplyVoids = false });
                TimeSpan read = stopwatch.Elapsed;

                // One set against the other: First indexes the bars, Second the IFC bodies.
                ClashResult[] clashes = Clash3.Find(bars, bodies, new ClashOptions(clearance: Clearance));
                TimeSpan check = stopwatch.Elapsed - read;

                foreach (ClashResult clash in clashes)
                {
                    DrawClash(clash);
                }

                string notBuilt = unbuilt > 0 ? $" ({unbuilt} could not be built)" : string.Empty;
                Operation.DisplayPrompt(
                    $"{bars.Count} bar(s){notBuilt} of {reinforcements.Count} reinforcement(s) against {bodies.Length} IFC body(ies) " +
                    $"of {referenceObjects.Count} reference object(s): {clashes.Count(c => c.Kind == ClashKind.Hard)} hard, " +
                    $"{clashes.Count(c => c.Kind == ClashKind.Touch)} touching, {clashes.Count(c => c.Kind == ClashKind.Clearance)} " +
                    $"nearer than {Clearance} mm, {clashes.Count(c => c.Kind == ClashKind.Unresolved)} unresolved. " +
                    $"Read in {read.TotalSeconds:0.00} s, checked in {check.TotalSeconds:0.00} s.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
            finally
            {
                workPlanes.SetCurrentTransformationPlane(userPlane);
                model.CommitChanges();
            }
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
                    graphicsDrawer.DrawLineSegment(gap.StartPoint.ToTeklaPoint(), gap.EndPoint.ToTeklaPoint(), color);
                    DrawSurface(Marker(clash.Location), color);
                    label = $"Gap {clash.Distance:0.0} mm";
                    break;

                default:
                    // Checking the pair failed: a marker in the space their boxes share.
                    DrawSurface(Marker(clash.Location), color);
                    label = "Unresolved: " + clash.Error.GetType().Name;
                    break;
            }

            graphicsDrawer.DrawText(clash.Location.ToTeklaPoint(), label, color);
        }

        /// <summary>
        /// Draws triangles as one mesh, both sides of each: DrawMeshSurface shows only the side a triangle turns
        /// counterclockwise to, and a contact face may be looked at from either.
        /// </summary>
        private void DrawSurface(IEnumerable<GeoTriangle3> triangles, Color color)
        {
            var mesh = new Mesh();

            foreach (GeoTriangle3 triangle in triangles)
            {
                int a = mesh.AddPoint(triangle.A.ToTeklaPoint());
                int b = mesh.AddPoint(triangle.B.ToTeklaPoint());
                int c = mesh.AddPoint(triangle.C.ToTeklaPoint());

                mesh.AddTriangle(a, b, c);
                mesh.AddTriangle(a, c, b);
            }

            graphicsDrawer.DrawMeshSurface(mesh, color);
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
