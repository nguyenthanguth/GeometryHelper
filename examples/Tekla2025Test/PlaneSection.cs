using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.TeklaConvert;
using Tekla.Structures.Model;
using TSG = Tekla.Structures.Geometry3d;

namespace Tekla2025Test
{
    /// <summary>
    /// Cuts the parts and IFC objects selected in Tekla Structures by a plane through three picked points, and draws the
    /// faces the cut leaves in the model view, all in the current work plane: <see cref="GeoSolid3.Section(GeoPlane3, Tolerance)"/>
    /// on every body. The "Section by 3 points: selected parts and IFC" button of <see cref="Form1"/> runs it. Each part's
    /// body is drawn too, as it is read (<see cref="DrawPartBodies"/>).
    /// </summary>
    /// <remarks>
    /// What one run draws, the next run takes out again, and so does closing the test window, leaving the model as it
    /// was found; <see cref="Form1"/> keeps one instance for that.
    /// </remarks>
    internal sealed class PlaneSection
    {
        /// <summary>
        /// Whether the openings of IFC objects are cut from them first (<see cref="IfcConvertOptions.ApplyVoids"/>), so that
        /// a window or a bolt hole the plane passes through shows in the section as the model view shows it. It costs a
        /// boolean operation per opening, and a body so cut can come back not closed and give no section.
        /// </summary>
        private const bool CutIfcOpenings = true;

        /// <summary>
        /// How many objects the summary lists; every section is drawn.
        /// </summary>
        private const int ListedObjects = 20;

        /// <summary>
        /// Whether each part's body is drawn too, as <see cref="SolidConvert.TryToGeoSolid3(Tekla.Structures.Solid.ISolid, out GeoSolid3)"/>
        /// reads it: every face dashed blue, its holes cyan, so a face Tekla left out of flat shows as the triangles it is
        /// read as, and the section in red stands out against it.
        /// </summary>
        private const bool DrawPartBodies = true;

        private const ControlObjectColorEnum BodyColor = ControlObjectColorEnum.BLUE;

        private const ControlObjectColorEnum BodyHoleColor = ControlObjectColorEnum.CYAN;

        /// <summary>
        /// What the last run drew, to take out again.
        /// </summary>
        private readonly List<ControlPolycurve> drawn = new List<ControlPolycurve>();

        private readonly Model model = new Model();

        /// <summary>
        /// Runs the section on what is selected in the model view now: takes out what the last run drew, asks for the
        /// three points, cuts every body and draws the sections.
        /// </summary>
        public void Run()
        {
            try
            {
                // The parts and IFC objects selected, read before picking starts (Tekla.Structures.Model has a
                // ModelObjectSelector too, hence the full name).
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
                    MessageBox.Show("Select the parts or IFC objects to cut, then press the button again.", "Section");
                    return;
                }

                // The last sections go first, so that they are not in the way of the picking.
                if (RemoveDrawn() > 0)
                {
                    model.CommitChanges();
                }

                if (!TryPickFrame(out GeoCoordinateSystem3 frame))
                {
                    return;
                }

                Stopwatch cutting = Stopwatch.StartNew();
                GeoPlane3 plane = frame.GetPlane();
                var sections = new List<ObjectSection>();
                var partBodies = new List<GeoSolid3>();
                int unreadable = 0;

                // No work plane is set: the parts' solids, the IFC bodies and the picked points all come in the current
                // one, and the sections are drawn in it.
                foreach (Part part in parts)
                {
                    if (!part.GetSolid().TryToGeoSolid3(out GeoSolid3 body))
                    {
                        unreadable++;
                        continue;
                    }

                    partBodies.Add(body);
                    sections.Add(Cut($"{part.GetType().Name} {part.Identifier.ID} {part.Name}", new[] { body }, plane));
                }

                int products = 0;
                if (referenceObjects.Count > 0)
                {
                    foreach (IfcProductGeometry product in referenceObjects.ToIfcGeometries(new IfcConvertOptions { ApplyVoids = CutIfcOpenings }))
                    {
                        sections.Add(Cut($"{NameOf(product)} ({product.GlobalId})", product.Solids, plane));
                        products++;
                    }
                }

                cutting.Stop();

                if (DrawPartBodies)
                {
                    drawn.AddRange(partBodies.DrawToTekla(BodyColor, BodyHoleColor, ControlObjectLineType.DashedLine));
                }

                foreach (GeoFace3 face in sections.SelectMany(section => section.Faces))
                {
                    drawn.AddRange(face.DrawToTekla());
                }

                if (drawn.Count > 0)
                {
                    model.CommitChanges();
                }

                MessageBox.Show(Summary(sections, parts.Count, unreadable, referenceObjects.Count, products, frame, cutting.Elapsed, partBodies), "Section");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Section failed");
            }
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
        /// The section of one object: its name, the faces the plane leaves across its bodies, whether the plane runs
        /// through any of them at all, so that a body it crosses and still gets nothing from can be told from one it
        /// misses, whether such a body is open, the usual reason, and whether the plane lies along a face of one without
        /// running through it.
        /// </summary>
        private struct ObjectSection
        {
            public string Name;
            public GeoFace3[] Faces;
            public bool Crossed;
            public bool Open;
            public bool Touched;
        }

        /// <summary>
        /// Asks for the three points of the plane: the first is its origin, the second fixes its X axis and the third the
        /// side its Y axis runs to, so each section is measured along the first two.
        /// </summary>
        /// <returns>false when the picking was interrupted, or the points do not make a plane.</returns>
        private static bool TryPickFrame(out GeoCoordinateSystem3 frame)
        {
            frame = default;
            var picker = new Tekla.Structures.Model.UI.Picker();
            TSG.Point first, second, third;

            try
            {
                first = picker.PickPoint("Section plane: pick the first point");
                second = picker.PickPoint("Section plane: pick the second point, along the plane's X axis", first);
                third = picker.PickPoint("Section plane: pick the third point, on the plane", second);
            }
            catch (ApplicationException)
            {
                // Tekla ends a pick the user interrupts with an exception.
                return false;
            }

            try
            {
                GeoPoint3 origin = first.ToGeoPoint3();
                frame = new GeoCoordinateSystem3(origin, origin.GetVectorTo(second.ToGeoPoint3()), origin.GetVectorTo(third.ToGeoPoint3()));
                return true;
            }
            catch (ArgumentException)
            {
                MessageBox.Show("The three points lie on one line, or two of them on each other, so they make no plane.", "Section");
                return false;
            }
        }

        /// <summary>
        /// Cuts the bodies of one object by the plane. A body with no vertex on one side of it is passed over: its box is
        /// asked first, which is quick, and then its own vertices, since a slanted beam can have its box cut and not itself.
        /// </summary>
        private static ObjectSection Cut(string name, IEnumerable<GeoSolid3> bodies, GeoPlane3 plane)
        {
            var faces = new List<GeoFace3>();
            bool crossed = false;
            bool open = false;
            bool touched = false;

            foreach (GeoSolid3 body in bodies)
            {
                if (!Straddles(body.GetAabb().GetCorners(), plane) ||
                    !Straddles(body.Faces.SelectMany(face => face.Boundary.Vertices), plane))
                {
                    // Three points picked on a face of the body make a plane along that face: every corner is on it or
                    // to one side, so the plane touches the body and cuts nothing.
                    touched |= body.Faces.Any(face => face.Boundary.Vertices.All(v => Containment3.GetSide(plane, v) == PlaneSide.On));
                    continue;
                }

                crossed = true;
                GeoFace3[] cut = body.Section(plane);
                faces.AddRange(cut);
                open |= cut.Length == 0 && !body.IsClosed();
            }

            return new ObjectSection { Name = name, Faces = faces.ToArray(), Crossed = crossed, Open = open, Touched = touched && !crossed };
        }

        /// <summary>
        /// Whether some of the points lie on either side of the plane, judged as the section judges them: a point within
        /// the planar tolerance of it is on it, so a plane lying along a face touches the body rather than cuts it.
        /// </summary>
        private static bool Straddles(IEnumerable<GeoPoint3> points, GeoPlane3 plane)
        {
            bool below = false;
            bool above = false;

            foreach (GeoPoint3 point in points)
            {
                PlaneSide side = Containment3.GetSide(plane, point);
                below |= side == PlaneSide.Below;
                above |= side == PlaneSide.Above;

                if (below && above)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// What was cut and drawn, and a line to each object.
        /// </summary>
        private static string Summary(List<ObjectSection> sections, int parts, int unreadable, int referenceObjects, int products, GeoCoordinateSystem3 frame, TimeSpan time, List<GeoSolid3> partBodies)
        {
            int faces = sections.Sum(section => section.Faces.Length);
            int cut = sections.Count(section => section.Faces.Length > 0);
            int crossedOnly = sections.Count(section => section.Crossed && section.Faces.Length == 0);
            int open = sections.Count(section => section.Open);
            int touched = sections.Count(section => section.Touched);
            string notRead = unreadable > 0 ? $" ({unreadable} whose solid could not be read)" : string.Empty;
            var text = new StringBuilder();

            text.AppendLine($"{parts} part(s){notRead} and {referenceObjects} IFC object(s) ({products} product(s)) against the " +
                $"plane through the three points: {cut} cut, {faces} face(s), in {time.TotalMilliseconds:0} ms.");

            if (crossedOnly > 0)
            {
                text.AppendLine($"{crossedOnly} crossed by the plane gave no section, {open} of them because a body is not closed.");
            }

            if (touched > 0)
            {
                text.AppendLine($"{touched} lie against the plane along a face and are not cut: a plane along a face touches a body without cutting it.");
            }

            text.AppendLine("Sections drawn red, holes white." + (DrawPartBodies && partBodies.Count > 0
                ? $" Part bodies as read dashed blue, holes cyan: {partBodies.Count} bodies, {partBodies.Sum(body => body.Faces.Count)} faces."
                : string.Empty));
            text.AppendLine("Pressing the button again, or closing the test window, takes them out.");
            text.AppendLine();
            text.AppendLine(sections.Count > ListedObjects ? $"The first {ListedObjects} of {sections.Count}:" : "Each object:");

            foreach (ObjectSection section in sections.Take(ListedObjects))
            {
                text.AppendLine("   " + Describe(section, frame));
            }

            return text.ToString().TrimEnd();
        }

        /// <summary>
        /// One object in a line: how many faces its section has, their area, and how far they reach along the plane's two
        /// axes, read from the faces laid out flat in the plane's own coordinates.
        /// </summary>
        private static string Describe(ObjectSection section, GeoCoordinateSystem3 frame)
        {
            if (section.Faces.Length == 0)
            {
                return section.Name + (section.Touched ? ": the plane lies along a face of it, touching it without cutting it"
                    : !section.Crossed ? ": not reached by the plane"
                    : section.Open ? ": crossed, but its body is not closed, so it cannot be cut"
                    : ": crossed, but no section");
            }

            GeoPoint2[] corners = section.Faces
                .Select(face => face.ProjectToFace2(frame))
                .SelectMany(face => face.Boundary.Vertices)
                .ToArray();
            double along = corners.Max(corner => corner.X) - corners.Min(corner => corner.X);
            double across = corners.Max(corner => corner.Y) - corners.Min(corner => corner.Y);

            return $"{section.Name}: {section.Faces.Length} face(s), {section.Faces.Sum(face => face.Area):0} mm2, " +
                $"{along:0} x {across:0} mm along the first two points and across";
        }

        /// <summary>
        /// An IFC member in a few words: its type, and its name when it has one.
        /// </summary>
        private static string NameOf(IfcProductGeometry member)
        {
            return string.IsNullOrWhiteSpace(member.Name) ? member.IfcType : member.IfcType + " " + member.Name;
        }
    }
}
