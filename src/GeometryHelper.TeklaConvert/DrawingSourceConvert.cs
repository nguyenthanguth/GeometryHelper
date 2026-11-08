using System;
using System.Collections;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Drawing;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Geometry;
using TSG = Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;
using TSS = Tekla.Structures.Solid;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Reads Tekla model objects into the <see cref="DrawingSource"/> a drawing of GeometryHelper.Drawing is made from:
    /// parts, the bolts and holes of their bolt groups, and their welds.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything is read in the current work plane, in millimetres, and the model is never changed. A part and a weld
    /// keep their Tekla GUID as their id, and a bolt takes its group's GUID and its number, "-1", "-2" and on, so a drawing
    /// finds the same objects again when it is updated.
    /// </para>
    /// <para>
    /// A part becomes an object of the kind <see cref="DrawingObjectKind.Part"/>: its body read with
    /// <see cref="SolidConvert.TryToGeoSolid3(TSS.ISolid, out GeoSolid3, Tolerance)"/>, cuts and holes in it, its
    /// coordinate system as its frame (X along its length), its name, its position (PART_POS) as its mark, its profile and
    /// material, its assembly as its parent, and the report properties ASSEMBLY_POS, LENGTH and WEIGHT as attributes under
    /// those names.
    /// </para>
    /// <para>
    /// A bolt group gives one object for each of its positions: a <see cref="DrawingObjectKind.Bolt"/>, or a
    /// <see cref="DrawingObjectKind.Hole"/> where the group has holes only. Each is made from the group's data rather than
    /// from Tekla's faceted body: a cylinder of 24 sides of the bolt's diameter, or the hole's, along the group's Z axis,
    /// with its frame's X along that axis. A bolt runs as far as the body Tekla draws for it, head, washers and nut; a
    /// hole runs through the parts bolted that are read too. An M20 bolt group of four, 60 long, through a plate 20 thick,
    /// gives four bolts with the attributes BoltGroup (the group's GUID), Count "4", Diameter "20", HoleDiameter "22" with
    /// Tekla's 2 of tolerance, Length "60", Standard, and BoltedParts (the GUIDs of the parts bolted); their parent is the
    /// part bolted to.
    /// </para>
    /// <para>
    /// A weld becomes a <see cref="DrawingObjectKind.Weld"/>: its body, the part welded to as its parent, and what its
    /// symbol reads, by the names of <see cref="WeldSymbol"/>: a fillet weld of 5 above the line,
    /// 200 long, is WeldType "Fillet", WeldSide "Arrow", WeldSize "a5" and WeldLength "200", and WeldSecondaryPart is the
    /// GUID of the part welded on.
    /// </para>
    /// <para>
    /// An object of a kind not drawn, one whose solid cannot be read and a bolt with no length along its axis are left out
    /// and counted in one Debug line of <see cref="GeometryHelperLog"/>; an object Tekla cannot describe is written as a
    /// Warn and left out; each call ends with one Info line of what it read. An object reached twice, a part through its
    /// assembly and its own selection, or a bolt group through each part it bolts, is read once.
    /// </para>
    /// </remarks>
    public static class DrawingSourceConvert
    {
        /// <summary>
        /// Reads an assembly: its main part, its secondaries and those of its sub-assemblies, with the bolts and welds of
        /// them all, set in the frame of the main part.
        /// </summary>
        /// <param name="assembly">The assembly.</param>
        /// <returns>
        /// The source. A beam 3 000 long lying along the model's Y with an end plate welded on comes in the beam's frame,
        /// X along its length, so its front view shows it across the sheet.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
        public static DrawingSource FromAssembly(TSM.Assembly assembly)
        {
            if (assembly == null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            var reader = new Reader(Tolerance.Global);
            string mainId = reader.AddAssembly(assembly);
            reader.AddConnections();
            return DrawingSourceBuild.Build(reader.Data, mainId, "DrawingSourceConvert.FromAssembly", "assembly " + Describe(assembly), reader.Tolerance);
        }

        /// <summary>
        /// Reads a part, with its bolts and welds, set in the part's frame.
        /// </summary>
        /// <param name="part">The part.</param>
        /// <returns>The source.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="part"/> is null.</exception>
        public static DrawingSource FromPart(TSM.Part part)
        {
            if (part == null)
            {
                throw new ArgumentNullException(nameof(part));
            }

            var reader = new Reader(Tolerance.Global);
            string id = reader.AddPart(part, null, false);
            reader.AddConnections();
            return DrawingSourceBuild.Build(reader.Data, id, "DrawingSourceConvert.FromPart", "part " + Describe(part), reader.Tolerance);
        }

        /// <summary>
        /// Reads model objects, such as those selected: assemblies as <see cref="FromAssembly"/> reads them, parts with
        /// their bolts and welds, bolt groups and welds; any other object is left out.
        /// </summary>
        /// <param name="objects">The objects, in any order; null entries are passed over.</param>
        /// <returns>
        /// The source, set in the frame its rule gives: that of the main part with the lowest id among those read.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="objects"/> is null.</exception>
        public static DrawingSource FromObjects(IEnumerable<TSM.ModelObject> objects)
        {
            if (objects == null)
            {
                throw new ArgumentNullException(nameof(objects));
            }

            var reader = new Reader(Tolerance.Global);
            int given = 0;

            foreach (TSM.ModelObject item in objects)
            {
                if (item == null)
                {
                    continue;
                }

                given++;
                reader.AddObject(item);
            }

            reader.AddConnections();
            return DrawingSourceBuild.Build(reader.Data, null, "DrawingSourceConvert.FromObjects", given + " model objects", reader.Tolerance);
        }

        /// <summary>
        /// The id of a model object: its GUID, selected first where the object came without one; null where it has none.
        /// </summary>
        private static string IdOf(TSM.ModelObject item)
        {
            Guid guid = item.Identifier.GUID;

            if (guid == Guid.Empty && item.Select())
            {
                guid = item.Identifier.GUID;
            }

            return guid == Guid.Empty ? null : guid.ToString();
        }

        /// <summary>The id of an object for the log, without asking Tekla.</summary>
        private static string Describe(TSM.ModelObject item)
        {
            Guid guid = item.Identifier.GUID;
            return guid == Guid.Empty ? item.Identifier.ID.ToString(System.Globalization.CultureInfo.InvariantCulture) : guid.ToString();
        }

        /// <summary>A Tekla coordinate system as a frame; null where its axes are parallel or of no length.</summary>
        private static GeoCoordinateSystem3? BoltFrame(TSG.CoordinateSystem system)
        {
            if (system == null)
            {
                return null;
            }

            try
            {
                return DrawingSourceBuild.BoltFrame(system.Origin.ToGeoPoint3(), system.AxisX.ToGeoVector3(), system.AxisY.ToGeoVector3());
            }
            catch (ArgumentException)
            {
                // Axes that pin down no frame: the group is counted among those left out, not warned of.
                return null;
            }
        }

        private static GeoCoordinateSystem3? Frame(TSG.CoordinateSystem system)
        {
            if (system == null)
            {
                return null;
            }

            try
            {
                GeoCoordinateSystem3 frame = system.ToGeoCoordinateSystem3();
                return frame.IsValid ? frame : (GeoCoordinateSystem3?)null;
            }
            catch (ArgumentException)
            {
                // Axes that pin down no frame: the object is counted among those left out, not warned of.
                return null;
            }
        }

        private static string ReportText(TSM.ModelObject item, string name)
        {
            string value = string.Empty;
            return item.GetReportProperty(name, ref value) ? value : null;
        }

        private static double? ReportNumber(TSM.ModelObject item, string name)
        {
            double value = 0.0;
            return item.GetReportProperty(name, ref value) ? value : (double?)null;
        }

        private static GeoSolid3 Body(TSS.ISolid solid, Tolerance tolerance)
            => solid != null && solid.TryToGeoSolid3(out GeoSolid3 body, tolerance) ? body : null;

        /// <summary>Every corner of every face of a Tekla solid, holes and all.</summary>
        private static List<GeoPoint3> Corners(TSS.ISolid solid)
        {
            var corners = new List<GeoPoint3>();

            if (solid == null)
            {
                return corners;
            }

            TSS.FaceEnumerator faces = solid.GetFaceEnumerator();

            while (faces.MoveNext())
            {
                if (!(faces.Current is TSS.Face face))
                {
                    continue;
                }

                TSS.LoopEnumerator loops = face.GetLoopEnumerator();

                while (loops.MoveNext())
                {
                    if (loops.Current is TSS.Loop loop)
                    {
                        corners.AddRange(loop.ReadCorners());
                    }
                }
            }

            return corners;
        }

        /// <summary>
        /// Reads Tekla objects into a <see cref="TeklaDrawingData"/>, each once. Every member here asks Tekla, so none of
        /// it runs without a running Tekla Structures.
        /// </summary>
        private sealed class Reader
        {
            // The Tekla parts read, in the order read, for their bolts and welds.
            private readonly List<TSM.Part> _parts = new List<TSM.Part>();

            internal Reader(Tolerance tolerance)
            {
                Tolerance = tolerance;
            }

            internal TeklaDrawingData Data { get; } = new TeklaDrawingData();

            internal Tolerance Tolerance { get; }

            internal void AddObject(TSM.ModelObject item)
            {
                switch (item)
                {
                    case TSM.Assembly assembly:
                        AddAssembly(assembly);
                        break;
                    case TSM.Part part:
                        AddPart(part, null, false);
                        break;
                    case TSM.BoltGroup group:
                        AddBoltGroup(group);
                        break;
                    case TSM.BaseWeld weld:
                        AddWeld(weld);
                        break;
                    default:
                        Data.Skip("objects of a kind not drawn", item.GetType().Name);
                        break;
                }
            }

            /// <summary>Reads an assembly's parts and those of its sub-assemblies; returns the id of its main part, or null.</summary>
            internal string AddAssembly(TSM.Assembly assembly)
            {
                string id;
                TSM.Part main;
                ArrayList secondaries;
                ArrayList subAssemblies;

                try
                {
                    id = IdOf(assembly);

                    if (id == null)
                    {
                        Data.Skip("objects with no GUID", nameof(TSM.Assembly));
                        return null;
                    }

                    if (!Data.Visit(id))
                    {
                        return null;
                    }

                    main = assembly.GetMainPart() as TSM.Part;
                    secondaries = assembly.GetSecondaries();
                    subAssemblies = assembly.GetSubAssemblies();
                }
                catch (Exception exception)
                {
                    GeometryHelperLog.Warn($"Assembly {Describe(assembly)} could not be read from Tekla; it is left out.", exception);
                    return null;
                }

                string mainId = main == null ? null : AddPart(main, id, true);

                foreach (object secondary in secondaries ?? new ArrayList())
                {
                    if (secondary is TSM.Part part)
                    {
                        AddPart(part, id, false);
                    }
                    else if (secondary != null)
                    {
                        Data.Skip("objects of a kind not drawn", secondary.GetType().Name);
                    }
                }

                foreach (object sub in subAssemblies ?? new ArrayList())
                {
                    if (sub is TSM.Assembly subAssembly)
                    {
                        AddAssembly(subAssembly);
                    }
                }

                return mainId;
            }

            /// <summary>
            /// Reads a part; its assembly and whether it is the main part are asked of Tekla where they are not given.
            /// Returns its id, or null where it has none.
            /// </summary>
            internal string AddPart(TSM.Part part, string assemblyId, bool isMain)
            {
                try
                {
                    string id = IdOf(part);

                    if (id == null)
                    {
                        Data.Skip("objects with no GUID", part.GetType().Name);
                        return null;
                    }

                    if (!Data.Visit(id))
                    {
                        return id;
                    }

                    if (assemblyId == null)
                    {
                        TSM.Assembly assembly = part.GetAssembly();

                        if (assembly != null)
                        {
                            assemblyId = IdOf(assembly);
                            isMain = assembly.GetMainPart() is TSM.Part main && string.Equals(IdOf(main), id, StringComparison.Ordinal);
                        }
                    }

                    Data.Parts.Add(new TeklaPartData
                    {
                        Id = id,
                        Solid = Body(part.GetSolid(), Tolerance),
                        Frame = Frame(part.GetCoordinateSystem()),
                        Name = part.Name,
                        Mark = ReportText(part, "PART_POS"),
                        Profile = part.Profile?.ProfileString,
                        Material = part.Material?.MaterialString,
                        AssemblyId = assemblyId,
                        IsMain = isMain,
                        AssemblyPosition = ReportText(part, "ASSEMBLY_POS"),
                        Length = ReportNumber(part, "LENGTH"),
                        Weight = ReportNumber(part, "WEIGHT"),
                    });

                    _parts.Add(part);
                    return id;
                }
                catch (Exception exception)
                {
                    GeometryHelperLog.Warn($"Part {Describe(part)} could not be read from Tekla; it is left out.", exception);
                    return null;
                }
            }

            /// <summary>Reads the bolt groups and welds of every part read so far.</summary>
            internal void AddConnections()
            {
                foreach (TSM.Part part in _parts.ToArray())
                {
                    var groups = new List<TSM.BoltGroup>();
                    var welds = new List<TSM.BaseWeld>();

                    try
                    {
                        TSM.ModelObjectEnumerator bolts = part.GetBolts();

                        while (bolts != null && bolts.MoveNext())
                        {
                            if (bolts.Current is TSM.BoltGroup group)
                            {
                                groups.Add(group);
                            }
                        }

                        TSM.ModelObjectEnumerator joined = part.GetWelds();

                        while (joined != null && joined.MoveNext())
                        {
                            if (joined.Current is TSM.BaseWeld weld)
                            {
                                welds.Add(weld);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        GeometryHelperLog.Warn($"The bolts and welds of part {Describe(part)} could not be read from Tekla; they are left out.", exception);
                        continue;
                    }

                    foreach (TSM.BoltGroup group in groups)
                    {
                        AddBoltGroup(group);
                    }

                    foreach (TSM.BaseWeld weld in welds)
                    {
                        AddWeld(weld);
                    }
                }
            }

            internal void AddBoltGroup(TSM.BoltGroup group)
            {
                try
                {
                    string id = IdOf(group);

                    if (id == null)
                    {
                        Data.Skip("objects with no GUID", group.GetType().Name);
                        return;
                    }

                    if (!Data.Visit(id))
                    {
                        return;
                    }

                    GeoCoordinateSystem3? frame = BoltFrame(group.GetCoordinateSystem());

                    if (!frame.HasValue)
                    {
                        Data.Skip("bolt groups with no coordinate system", id);
                        return;
                    }

                    var positions = new List<GeoPoint3>();

                    foreach (object position in group.BoltPositions ?? new ArrayList())
                    {
                        if (position is TSG.Point point)
                        {
                            positions.Add(point.ToGeoPoint3());
                        }
                    }

                    var bolted = new List<string>();
                    string toId = PartId(group.PartToBoltTo);
                    bolted.Add(toId);
                    bolted.Add(PartId(group.PartToBeBolted));

                    foreach (object other in group.GetOtherPartsToBolt() ?? new ArrayList())
                    {
                        bolted.Add(PartId(other as TSM.ModelObject));
                    }

                    Data.BoltGroups.Add(new TeklaBoltGroupData
                    {
                        Id = id,
                        HasBolt = group.Bolt,
                        Diameter = group.BoltSize,
                        HoleTolerance = group.Tolerance,
                        Length = group.Length > 0.0 ? group.Length : group.CutLength,
                        Standard = group.BoltStandard,
                        Frame = frame.Value,
                        Positions = positions,
                        BodyCorners = group.Bolt ? Corners(group.GetSolid()) : new List<GeoPoint3>(),
                        PartToBoltToId = toId,
                        BoltedPartIds = bolted.FindAll(b => b != null),
                    });
                }
                catch (Exception exception)
                {
                    GeometryHelperLog.Warn($"Bolt group {Describe(group)} could not be read from Tekla; it is left out.", exception);
                }
            }

            internal void AddWeld(TSM.BaseWeld weld)
            {
                try
                {
                    string id = IdOf(weld);

                    if (id == null)
                    {
                        Data.Skip("objects with no GUID", weld.GetType().Name);
                        return;
                    }

                    if (!Data.Visit(id))
                    {
                        return;
                    }

                    Data.Welds.Add(new TeklaWeldData
                    {
                        Id = id,
                        Solid = Body(weld.GetSolid(), Tolerance),
                        Frame = Frame(weld.GetCoordinateSystem()),
                        TypeAbove = weld.TypeAbove.ToString(),
                        TypeBelow = weld.TypeBelow.ToString(),
                        SizeAbove = weld.SizeAbove,
                        SizeBelow = weld.SizeBelow,
                        PrefixAbove = weld.PrefixAboveLine,
                        PrefixBelow = weld.PrefixBelowLine,
                        LengthAbove = weld.LengthAbove,
                        LengthBelow = weld.LengthBelow,
                        Length = ReportNumber(weld, "LENGTH"),
                        MainPartId = PartId(weld.MainObject),
                        SecondaryPartId = PartId(weld.SecondaryObject),
                    });
                }
                catch (Exception exception)
                {
                    GeometryHelperLog.Warn($"Weld {Describe(weld)} could not be read from Tekla; it is left out.", exception);
                }
            }

            private static string PartId(TSM.ModelObject item) => item == null ? null : IdOf(item);
        }
    }
}
