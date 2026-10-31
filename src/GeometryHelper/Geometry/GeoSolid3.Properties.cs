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
        /// <see cref="TryGetVolume(out double, Tolerance)"/> says so as well. Cut in at once, the body is parted by the
        /// planes of its openings and each piece judged by one point of it; where a plane runs through a piece and will not
        /// cut it, leaving it part in an opening and part out, the openings are taken out one at a time by a difference
        /// instead. A half of a hull cut near a face within a hundredth, a box 3.5 by 3.5 by 5 standing through the cut, held
        /// 1 465.33 cubic millimetres so, the box left in, where it holds 1 434.77. An opening that will not come out so has
        /// the openings cut as <see cref="TryCutOpenings(out GeoSolid3, Tolerance)"/> cuts them before it is left in: a plate
        /// 200 by 100 by 3 turned about in space and a million out, with a notch 20 by 20 through one end and one 20 wide and
        /// 1 deep across the other, flush with three faces, measured 58 800 at the rounding of its coordinates, the second
        /// notch left in; it measures 56 800, as it holds. Where walls of two openings, or of an opening and the body, stand
        /// apart within the tolerance of one plane, the openings are cut as <see cref="TryCutOpenings(out GeoSolid3, Tolerance)"/>
        /// cuts them, within a finer tolerance: an L 100 by 80 by 50, a million out, with two blind openings 27 apart
        /// whose walls stood 0.0009 apart across it, measured 153 674.08 within a thousandth, the second opening's wall
        /// cut in the plane of the first's; it measures 153 673.56, as it holds.
        /// </para>
        /// <para>
        /// Where the point tolerance is below the rounding of the coordinates, a quadrillionth of the largest of them, nought
        /// among them, the openings are cut as <see cref="TryCutOpenings(out GeoSolid3, Tolerance)"/> cuts them, within that
        /// rounding; where it refuses them, within what rounding can move a corner off a plane, a trillionth of the size of the
        /// coordinates, as <see cref="Section(GeoPlane3, Tolerance)"/> cuts them; and only where that refuses them too, at
        /// the tolerance given as they were, and <see cref="TryGetVolume(out double, Tolerance)"/> says the volume cannot be
        /// trusted. At nought the plate with a hole 20 by 20 through it in place of the notches measured 60 000, the hole left
        /// in, and <c>TryGetVolume</c> trusted it; it measures 58 800. With the notches, a thousand out, it measured nought,
        /// and that was trusted too. Such a plate at the origin, turned another way, with an opening 32.7 by 51 down from its
        /// top to 0.9 over its bottom, measured nought at nought, and the whole plate, 60 000, at 1E-15, neither trusted; it
        /// measures 56 497.83, trusted.
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
        /// <para>
        /// The material is judged closed with the ends of its edges matched first, each to the first corner seen within the
        /// tolerance, as the booleans judge the bodies they work on, and not as <see cref="IsClosed(Tolerance)"/> reads it,
        /// which the two can part on either way; this answer, not <c>IsClosed</c>, says whether the volume can be trusted. A
        /// cube of 100 whose top stands 0.0015 over its sides, a speck of a face at each corner of its top half way up given
        /// before the cube's faces, is trusted, 1 000 005, where <c>IsClosed</c> reads it open; the union of two turned boxes
        /// made within a thousandth is refused at a tolerance of nought, where <c>IsClosed</c> reads it closed to the
        /// rounding of its coordinates. A cube cracked across three faces 0.001 wide less a skewed box, at a tolerance of a
        /// thousandth, read open by <c>IsClosed</c> by a hair of rounding, keeps its volume, 860.046 where the cube less the
        /// box holds 860.047.
        /// </para>
        /// <para>
        /// Below the rounding of the coordinates, where the openings are cut within it, a body whose own faces do not close
        /// within it is not trusted, whatever its cut gives: a cube 10 across with cracks 0.0001 wide in its faces, less a
        /// tetrahedron, cut within the rounding at a tolerance of nought, is a closed sliver of 0.000042 cubic millimetres,
        /// where the cube less the tetrahedron holds 888.41.
        /// </para>
        /// </remarks>
        public bool TryGetVolume(out double volume, Tolerance tolerance)
        {
            GeoSolid3 material = GetMaterial(tolerance, out bool whole);

            if (!whole || !(material ?? this).ClosesByCorners(tolerance) || !ClosesWithinRounding(tolerance))
            {
                volume = 0.0;
                return false;
            }

            volume = material == null ? 0.0 : material.GrossVolume;
            return true;
        }

        /// <summary>
        /// Whether the faces of a body with openings close within the rounding of the coordinates, where the point tolerance is
        /// below it and the openings are cut within it; true otherwise.
        /// </summary>
        /// <remarks>
        /// A body that does not close within the rounding holds no closed material to trust, whatever cutting its openings in
        /// gives: a cube 10 across whose faces carry cracks 0.0001 wide, less a tetrahedron, cut within the rounding at a
        /// tolerance of nought, came out a closed sliver of 4.2E-5 cubic millimetres, where the cube less the tetrahedron holds
        /// 888.41.
        /// </remarks>
        private bool ClosesWithinRounding(Tolerance tolerance)
        {
            Tolerance within = Material3.ToRounding(this, tolerance);

            return !(within.EqualPoint > tolerance.EqualPoint) || ClosesByCorners(within);
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
        /// <para>
        /// The material is cut, so a hole the plane passes through is a hole in the section, and a body in two
        /// pieces gives two faces. A plane lying along a face of the body touches it without cutting it, and gives
        /// nothing there: the section is where the body has material on both sides of the plane.
        /// </para>
        /// <para>
        /// Asked at a point tolerance below the rounding of the coordinates, a quadrillionth of the largest of them, nought
        /// among them, the plane cuts the material within what rounding can move a corner off a plane, a trillionth of the
        /// size of the coordinates: within the tolerance asked, the two faces beside an edge each cut their own copy of it a
        /// rounding apart, and the rim did not close. At nought a plate 200 by 100 by 3 turned about in space, at the origin or a
        /// million out, had no section, where a plane square to its face cuts 20 000 of it and one across its length 300;
        /// with a hole 20 by 20 through it, it cuts 19 600, the hole in it. It is cut so too at and above the rounding while the
        /// point tolerance is below both that trillionth and a millionth: a million out, at 2E-9, twice the rounding, or at
        /// 1E-7, the plate had no section either. At a millionth and above, the default tolerance among them, the material is
        /// cut as it was, to the bit.
        /// </para>
        /// </remarks>
        public GeoFace3[] Section(GeoPlane3 plane, Tolerance tolerance)
        {
            // Below the rounding of the coordinates the material is cut by the plane within it; see the remarks.
            Splition3.BelowTheRounding(this, tolerance, out Tolerance within);

            return Splition3.Section(Material3.Whole(this, tolerance), plane, within);
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
        /// Cuts every opening out of the faces: where the point tolerance is below the rounding of the coordinates, as
        /// <see cref="TryCutOpenings(out GeoSolid3, Tolerance)"/> cuts them, within that rounding, or where that refuses, within
        /// what rounding can move a corner off a plane, a trillionth of the size of the coordinates; otherwise, or where both
        /// refuse, as <see cref="CutMaterialAsGiven"/> cuts them at the tolerance given, not to be trusted where they refused.
        /// </summary>
        /// <remarks>
        /// Cut at the tolerance given, the openings within the rounding came out wrong and were trusted: a plate 200 by 100 by
        /// 3 turned about in space and a thousand out, with a notch 20 by 20 through one end and one 20 wide and 1 deep across
        /// the other, measured nought at a tolerance of nought, said to be trusted so, where it holds 56 800. Where
        /// <c>TryCutOpenings</c> refuses them, cut as they were they can come out right, a plate a thousand out with three
        /// openings 48 758.41 at 1E-12, and are measured so, untrusted: of 5 000 bodies it refused 29 at nought, and cut as
        /// they were, 25 of those came out wrong and were trusted. Refused within the rounding, a plate with blind openings
        /// cut as they were measured nought or the whole plate, untrusted: of 4 000 prisms with up to three openings, turned
        /// about in space or not, at the origin and a thousand to a thousand million out, 22 at nought and 7 between 1E-12
        /// and 9E-7. Cut within the trillionth, as <see cref="Section(GeoPlane3, Tolerance)"/> and
        /// <see cref="Meshing.Mesh3.ToCells(GeoSolid3, Meshing.CellOptions3, Meshing.MeshPlacement3, Tolerance)"/> cut them,
        /// every one measures what it holds, and all but one star are trusted.
        /// </remarks>
        private MaterialCut CutMaterial(Tolerance tolerance)
        {
            Tolerance within = Material3.ToRounding(this, tolerance);

            if (!(within.EqualPoint > tolerance.EqualPoint))
            {
                return CutMaterialAsGiven(tolerance);
            }

            if (Boolean3.TryCutOpenings(this, out GeoSolid3 material, tolerance, out Exception failure))
            {
                return new MaterialCut(tolerance, material, true);
            }

            if (failure == null)
            {
                // The openings take all of it.
                return new MaterialCut(tolerance, null, true);
            }

            // Refused within the rounding, the openings are cut within what rounding can move a corner off a plane, a
            // trillionth of the size of the coordinates, as Section and Mesh3.ToCells cut them, and the corners that leaves
            // apart made one where that closes the material within the tolerance given; see the remarks.
            if (Splition3.BelowTheRounding(this, tolerance, out Tolerance trillionth)
                && Boolean3.TryCutOpenings(this, out GeoSolid3 coarser, trillionth, out _) && coarser != null)
            {
                if (!coarser.ClosesByCorners(tolerance) && Weld3.TryWeld(coarser, trillionth, tolerance, out GeoSolid3 welded))
                {
                    coarser = welded;
                }

                return new MaterialCut(tolerance, coarser, true);
            }

            GeometryHelperLog.Debug("GeoSolid3: the openings could not be cut out closed within the rounding of the coordinates; they are cut at the tolerance given, and the volume is not to be trusted.", failure);
            return new MaterialCut(tolerance, CutMaterialAsGiven(tolerance).Body, false);
        }

        /// <summary>
        /// Cuts every opening out of the faces at the tolerance given: all at once, which costs least, or where that cannot be
        /// worked out, opens a body that closed or leaves a piece of it part in an opening and part out, one at a time by a
        /// difference, leaving in any one that will not come out.
        /// </summary>
        /// <remarks>
        /// Where an opening will not come out by a difference at the tolerance alone, or one is said to take all that is left
        /// where the cut at once left material, the openings are cut as <see cref="TryCutOpenings(out GeoSolid3, Tolerance)"/>
        /// cuts them, which reads a corner as on a plane within the rounding of the coordinates too, and only where that gives
        /// no material is the opening left in. A difference at a tolerance at or just above that rounding is read on corners
        /// the rounding puts either side of a plane: the plate with the notches a million out measured 58 800 at the rounding
        /// and twice it, the second notch left in, where <c>TryCutOpenings</c> gave 56 800, as it holds.
        /// </remarks>
        private MaterialCut CutMaterialAsGiven(Tolerance tolerance)
        {
            // Walls standing apart within the tolerance of one plane are cut within a finer one, as TryCutOpenings cuts them.
            if (Boolean3.TryCutWallsApart(this, _openings, out GeoSolid3 apart, tolerance))
            {
                return new MaterialCut(tolerance, apart, true);
            }

            bool cut = Boolean3.TryCutOpeningsAtOnce(this, _openings, out GeoSolid3 material, tolerance, out Exception failure, out bool judgedWhole);

            if (judgedWhole)
            {
                GeometryHelperLog.Debug("GeoSolid3: cutting the openings out at once left a piece of the body part in an opening and part out; they are taken out one at a time.");
            }
            else if (cut)
            {
                // Faces that did not close leave no closed material to look for.
                if (material.ClosesByCorners(tolerance) || !ClosesByCorners(tolerance))
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
            bool cutAsTryCutOpenings = false;

            for (int i = 0; i < _openings.Length; i++)
            {
                if (Boolean3.TryTakeOut(remaining, _openings[i], out GeoSolid3 left, tolerance, out Exception failed))
                {
                    remaining = left;
                }
                else if (failed == null && !cut)
                {
                    // This opening takes all the others left, and with it whatever would not come out.
                    return new MaterialCut(tolerance, null, true);
                }
                else
                {
                    // An opening that will not come out, or is said to take all that is left where cutting them in at once
                    // left material, which the two cannot both be, has them all cut as TryCutOpenings cuts them, where that
                    // gives material, at or above the rounding; see the remarks.
                    if (!cutAsTryCutOpenings)
                    {
                        cutAsTryCutOpenings = true;

                        if (!(Material3.ToRounding(this, tolerance).EqualPoint > tolerance.EqualPoint)
                            && Boolean3.TryCutOpenings(this, out GeoSolid3 cutOpenings, tolerance, out _) && cutOpenings != null)
                        {
                            return new MaterialCut(tolerance, cutOpenings, true);
                        }
                    }

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

        /// <summary>
        /// The openings last cut into the body at once by the cells each opening's box reaches, as a boolean cuts them first;
        /// see Boolean3.FirstCut.
        /// </summary>
        internal Boolean3.AtOnceCut AtOnce
        {
            get => _atOnce;
            set => _atOnce = value;
        }

        /// <summary>
        /// Gets the region the gap to a face is measured with, made when first asked and kept for every query after it.
        /// </summary>
        /// <param name="index">The index of the face.</param>
        /// <remarks>
        /// Cutting a face into a frame and a layout costs more than most questions of a body, and a body cannot change, so the
        /// regions are kept as the cut of the material is: each slot is written whole or not at all, and two threads that make
        /// the same one make the same region, either of which is complete. A region lays itself out when it is first asked,
        /// the same way.
        /// </remarks>
        internal Core.Gap3.Region RegionOf(int index)
        {
            Core.Gap3.Region[] slots = _regions;

            if (slots == null)
            {
                System.Threading.Interlocked.CompareExchange(ref _regions, new Core.Gap3.Region[_faces.Length], null);
                slots = _regions;
            }

            Core.Gap3.Region region = System.Threading.Volatile.Read(ref slots[index]);

            if (region == null)
            {
                region = Core.Gap3.Region.Of(_faces[index]);
                System.Threading.Volatile.Write(ref slots[index], region);
            }

            return region;
        }
        #endregion
    }
}
