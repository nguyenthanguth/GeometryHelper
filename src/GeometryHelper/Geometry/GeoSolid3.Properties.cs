using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    public sealed partial class GeoSolid3
    {
        #region Volume, area and centroid

        /// <summary>
        /// Gets the volume of the material, every opening cut out, using the default tolerance.
        /// </summary>
        public double GetVolume() => GetVolume(Tolerance.Global);

        /// <summary>
        /// Gets the volume of the material: the faces with every opening cut out.
        /// </summary>
        /// <param name="tolerance">The tolerance the openings are cut out within.</param>
        /// <returns>The volume, positive whichever way the faces are wound; nought where the openings take all of it.</returns>
        /// <remarks>
        /// <para>
        /// The openings are cut out as shapes, not taken off as numbers: the part of an opening reaching outside the
        /// body costs nothing, as a through hole drawn past the faces it runs between does, two openings overlapping
        /// are taken out once, and the material inside an opening, its own openings, stays.
        /// </para>
        /// <para>
        /// A body without openings is measured by its faces at once, and nothing is cut: every part
        /// GeometryHelper.TeklaConvert reads is one, its cuts already in its faces, and a slab cut by five other
        /// parts matched Tekla's VOLUME_NET to five parts in a million million. A body with openings has them cut out
        /// once for the tolerance, and the cut kept, so its volume, area and centroid are of one material and cost
        /// one cut. An opening that cannot be cut out is left in the material and the log warns of it;
        /// <see cref="TryGetVolume(out double, Tolerance)"/> says so as well.
        /// </para>
        /// <para>
        /// Each face is taken as the fan of its boundary from its first corner, which is exact for a flat face and
        /// the way Tekla Structures reports a part's volume. <see cref="Measure3"/> measures the volume other ways
        /// too, and sets the ways side by side where a face a hair out of flat makes them part.
        /// </para>
        /// </remarks>
        public double GetVolume(Tolerance tolerance)
        {
            GeoSolid3 material = GetMaterial(tolerance, out _);

            return material == null ? 0.0 : material.GrossVolume;
        }

        /// <summary>
        /// Gets the volume of the material where it can be trusted, using the default tolerance.
        /// </summary>
        public bool TryGetVolume(out double volume) => TryGetVolume(out volume, Tolerance.Global);

        /// <summary>
        /// Gets the volume of the material where it can be trusted: every opening cut out, and the material closed.
        /// </summary>
        /// <param name="volume">The volume <see cref="GetVolume(Tolerance)"/> gives; nought when the method returns false.</param>
        /// <param name="tolerance">The tolerance the openings are cut out and the faces judged closed within.</param>
        /// <returns>
        /// true when every opening was cut out and the material closes, or the openings take all of it; false when an
        /// opening could not be cut out, or the faces do not close and so enclose no volume to measure.
        /// </returns>
        /// <remarks>
        /// The area and the centroid are of the same material, so the answer holds for them too. A body without
        /// openings costs one check that its faces close, and nothing is cut.
        /// </remarks>
        public bool TryGetVolume(out double volume, Tolerance tolerance)
        {
            GeoSolid3 material = GetMaterial(tolerance, out bool whole);

            if (!whole || !(material ?? this).IsClosed(tolerance))
            {
                volume = 0.0;
                return false;
            }

            volume = material == null ? 0.0 : material.GrossVolume;
            return true;
        }

        /// <summary>
        /// Gets the area of where the material ends, the walls of the openings included, using the default tolerance.
        /// </summary>
        public double GetSurfaceArea() => GetSurfaceArea(Tolerance.Global);

        /// <summary>
        /// Gets the area of where the material ends: the faces with every opening cut out, the walls of the openings
        /// included.
        /// </summary>
        /// <param name="tolerance">The tolerance the openings are cut out within.</param>
        /// <returns>The area; nought where the openings take all of the body.</returns>
        /// <remarks>
        /// Each face's area as its outline gives it, holes and all. A body without openings is measured by its faces
        /// at once: a slab cut by five other parts matched Tekla's AREA to five parts in a million million. A plate
        /// with a bolt hole loses the hole from both of its faces and gains the hole's wall. The material is the one
        /// <see cref="GetVolume(Tolerance)"/> measures, cut once.
        /// </remarks>
        public double GetSurfaceArea(Tolerance tolerance)
        {
            GeoSolid3 material = GetMaterial(tolerance, out _);

            return material == null ? 0.0 : material.GrossSurfaceArea;
        }

        /// <summary>
        /// Gets the centre of the material, every opening cut out, using the default tolerance.
        /// </summary>
        public GeoPoint3 GetCentroid() => GetCentroid(Tolerance.Global);

        /// <summary>
        /// Gets the centre of the material, which is its centre of mass: the faces with every opening cut out.
        /// </summary>
        /// <param name="tolerance">The tolerance the openings are cut out within.</param>
        /// <returns>
        /// The centroid; the middle of the body's box where the openings take all of it, and the average of the
        /// corners where the faces enclose no volume.
        /// </returns>
        /// <remarks>
        /// A bolt hole moves the centre of a plate away from it. The material is the one
        /// <see cref="GetVolume(Tolerance)"/> measures, cut once.
        /// </remarks>
        public GeoPoint3 GetCentroid(Tolerance tolerance)
        {
            GeoSolid3 material = GetMaterial(tolerance, out _);

            return material == null ? _box.Center : material.GetGrossCentroid(tolerance);
        }

        #endregion

        #region Mass properties and sections

        /// <summary>
        /// Gets the mass properties of the material at a density of one, using the default tolerance.
        /// </summary>
        public MassProperties3 GetMassProperties() => GetMassProperties(1.0, Tolerance.Global);

        /// <summary>
        /// Gets the mass properties of the material at a density, using the default tolerance.
        /// </summary>
        public MassProperties3 GetMassProperties(double density) => GetMassProperties(density, Tolerance.Global);

        /// <summary>
        /// Gets the mass properties of the material at a density: volume, mass, centroid, the moments and products
        /// of inertia about the centroid, and the principal moments and axes.
        /// </summary>
        /// <param name="density">The mass of a unit of volume; in kilograms per cubic millimetre, steel is 7.85E-6.</param>
        /// <param name="tolerance">The tolerance the openings are cut in and the surface meshed to.</param>
        /// <returns>The properties; openings are taken out of every one of them, and a body they take whole holds none.</returns>
        /// <remarks>
        /// Every integral is exact for the body's faces, taken over the surface by the divergence theorem, and the
        /// body is read as its material: a plate's bolt holes come out of its weight and move its centroid. The faces
        /// are read as the triangles lying in them, <see cref="VolumeMethod.Surface"/>; <see cref="Measure3"/> reads them
        /// other ways too, and sets the ways side by side.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the density is not a positive number.</exception>
        public MassProperties3 GetMassProperties(double density, Tolerance tolerance) => Measure3.MassProperties(this, density, VolumeMethod.Surface, tolerance);

        /// <summary>
        /// Gets where a plane cuts the material, using the default tolerance.
        /// </summary>
        public GeoFace3[] Section(GeoPlane3 plane) => Section(plane, Tolerance.Global);

        /// <summary>
        /// Gets where a plane cuts the material: the faces the cut leaves, holes and all, facing along the plane's
        /// normal.
        /// </summary>
        /// <param name="plane">The cutting plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>One face per region of the cut; empty where the plane misses the body or only grazes it.</returns>
        /// <remarks>
        /// The material is cut, so a hole the plane passes through is a hole in the section, and a body in two
        /// pieces gives two faces. A plane lying along a face of the body touches it without cutting it, and gives
        /// nothing there: the section is where the body has material on both sides of the plane.
        /// </remarks>
        public GeoFace3[] Section(GeoPlane3 plane, Tolerance tolerance)
        {
            return Splition3.Section(Material3.Whole(this, tolerance), plane, tolerance);
        }

        #endregion

        #region The material

        /// <summary>
        /// Gets the material of this body: its faces with every opening cut out, cut once for a tolerance and kept.
        /// </summary>
        /// <param name="tolerance">The tolerance the openings are cut out within.</param>
        /// <param name="whole">Whether every opening was cut out; false where one could not be, and is left in, warned of.</param>
        /// <returns>The body itself where it has no openings; otherwise a body without any; null where they take all of it.</returns>
        /// <remarks>
        /// The volume, the area, the centroid and the mass of a body with openings are all of its material, and
        /// cutting it is a boolean costing far more than any of them: the cut is kept, for the last tolerance asked, so
        /// that measuring the body every way costs one cut and the measures describe one body. The body cannot change,
        /// and so neither can its cut; two threads asking at once may both cut it, and either cut is kept.
        /// </remarks>
        internal GeoSolid3 GetMaterial(Tolerance tolerance, out bool whole)
        {
            if (_openings.Length == 0)
            {
                whole = true;
                return this;
            }

            MaterialCut cut = _material;

            if (cut == null || !cut.Tolerance.Equals(tolerance))
            {
                cut = CutMaterial(tolerance);
                _material = cut;
            }

            whole = cut.Whole;
            return cut.Body;
        }

        /// <summary>
        /// Cuts every opening out of the faces: all at once, which costs least, or where that cannot be worked out or
        /// opens a body that closed, one at a time by a difference, leaving in any one that will not come out.
        /// </summary>
        private MaterialCut CutMaterial(Tolerance tolerance)
        {
            if (Boolean3.TryCutOpenings(this, _openings, out GeoSolid3 material, tolerance, out Exception failure))
            {
                // Faces that did not close leave no closed material to look for.
                if (material.IsClosed(tolerance) || !IsClosed(tolerance))
                {
                    return new MaterialCut(tolerance, material, true);
                }

                GeometryHelperLog.Debug("GeoSolid3: cutting the openings out at once left the material open; they are taken out one at a time.");
            }
            else if (failure == null)
            {
                // The openings take all of it.
                return new MaterialCut(tolerance, null, true);
            }
            else
            {
                GeometryHelperLog.Debug("GeoSolid3: the openings could not be cut out at once; they are taken out one at a time.", failure);
            }

            GeoSolid3 remaining = new GeoSolid3(_faces);
            bool whole = true;

            for (int i = 0; i < _openings.Length; i++)
            {
                if (Boolean3.TryTakeOut(remaining, _openings[i], out GeoSolid3 left, tolerance, out Exception failed))
                {
                    remaining = left;
                }
                else if (failed == null)
                {
                    // This opening takes all the others left, and with it whatever would not come out.
                    return new MaterialCut(tolerance, null, true);
                }
                else
                {
                    whole = false;
                    GeometryHelperLog.Warn(
                        $"GeoSolid3: opening {i + 1} of {_openings.Length}, within {_openings[i].GetAabb()}, could not be cut out of the body, and is measured as material.",
                        failed);
                }
            }

            return new MaterialCut(tolerance, remaining, whole);
        }

        /// <summary>
        /// The material of a body as cut within one tolerance, kept whole so that a reader sees one cut or none.
        /// </summary>
        private sealed class MaterialCut
        {
            internal MaterialCut(Tolerance tolerance, GeoSolid3 body, bool whole)
            {
                Tolerance = tolerance;
                Body = body;
                Whole = whole;
            }

            /// <summary>The tolerance the openings were cut out within.</summary>
            internal Tolerance Tolerance { get; }

            /// <summary>The material, without openings; null where the openings take all of it.</summary>
            internal GeoSolid3 Body { get; }

            /// <summary>Whether every opening was cut out; false where one could not be, and is left in the material.</summary>
            internal bool Whole { get; }
        }

        #endregion
    }
}
