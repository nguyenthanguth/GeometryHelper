using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An INSERT of a DXF file, as AutoCAD's <c>BlockReference</c>: a block placed at <see cref="Position"/>, turned by
    /// <see cref="Rotation"/> and scaled by <see cref="ScaleFactors"/> in the plane of its <see cref="Normal"/>, with the
    /// values of its attributes in <see cref="AttributeCollection"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The INSERT of the block BOLT at (0, -60, 0), turned by nought and scaled by 1, whose block's origin is (5, 5, 0), has the
    /// <see cref="BlockTransform"/> that moves by (-5, -65, 0): a point of the block at its origin lands on the insertion
    /// point. <see cref="Name"/> is the name of the block it places, BOLT; <see cref="DxfEntity.BlockName"/> is that of the
    /// block it is in, <c>*Model_Space</c>, as AutoCAD names them.
    /// </para>
    /// <para>
    /// Its points are in world coordinates, as AutoCAD gives them, though the file writes the insertion point in the system of
    /// the normal's plane. Moved, turned, scaled or given a new normal, by a setter or by
    /// <see cref="DxfEntity.TransformBy(GeoTransform3)"/>, an INSERT carries its ATTRIBs with it, each by the same change of placement,
    /// their values untouched. <see cref="DxfEntity.GetGeometry"/> gives the shapes of its block placed as
    /// <see cref="DxfReader"/> places them; <see cref="Explode"/> gives its block's entities placed, in no file.
    /// </para>
    /// <para>
    /// <c>new DxfBlockReference(new GeoPoint3(10, 20, 0), db.BlockTable["BOLT"])</c> makes one in code, to be added by
    /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> to a block of the same drawing; a block that would come to
    /// insert itself is refused. A copy of an INSERT, <see cref="DxfEntity.GetTransformedCopy(GeoTransform3)"/>'s, added to
    /// another drawing places the block of its name there, whatever that drawing's block of the name holds, as AutoCAD binds
    /// a block by its name; it is refused where that drawing has no block of the name.
    /// </para>
    /// <para>
    /// <see cref="DxfEntity.GetGeometry"/> places the block anew on each call, a new list each time; where what it would place
    /// passes <see cref="DxfReadOptions.MaxPlacedEntities"/> it places nothing, and gives no shapes.
    /// </para>
    /// </remarks>
    public class DxfBlockReference : DxfEntity
    {
        // The groups of AcDbBlockReference (and AcDbMInsertBlock) in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 66, 2, 10, 20, 30, 41, 42, 43, 50, 70, 71, 44, 45, 210, 220, 230 });

        // The types whose thickness a placed shape's is written to.
        private static readonly HashSet<string> Thick = new HashSet<string>(StringComparer.Ordinal) { "LINE", "POINT", "CIRCLE", "ARC", "LWPOLYLINE", "POLYLINE", "TEXT", "SOLID", "TRACE" };

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        private DxfBlockTableRecord _record;
        private DxfAttributeCollection _collection;

        /// <summary>
        /// Initializes an INSERT of a block at a point, as AutoCAD's <c>new BlockReference(position, blockTableRecord)</c>: not
        /// turned, scaled by 1, on layer 0, in no file until it is added to a block of the drawing the block is of.
        /// </summary>
        /// <param name="position">The insertion point, in world coordinates.</param>
        /// <param name="blockTableRecord">The block it places, of a drawing.</param>
        /// <exception cref="ArgumentNullException">Thrown when the block is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number, or the block is a layout.</exception>
        public DxfBlockReference(GeoPoint3 position, DxfBlockTableRecord blockTableRecord)
            : this("AcDbBlockReference", position, blockTableRecord, null)
        {
        }

        /// <summary>The object of a record of a file.</summary>
        private protected DxfBlockReference(DxfItem item)
            : base(item)
        {
        }

        /// <summary>An INSERT made in code under a subclass marker, with the groups of an array after its point where given.</summary>
        private protected DxfBlockReference(string marker, GeoPoint3 position, DxfBlockTableRecord blockTableRecord, IEnumerable<DxfTypedValue> array)
            : base("INSERT", Groups(marker, position, blockTableRecord, array))
        {
            _record = blockTableRecord;
        }

        /// <summary>
        /// Gets the name of the block the INSERT places (group 2), as AutoCAD's <c>Name</c>: as the block table names it, its
        /// escapes read as letters, such as <c>BOLT</c> or <c>*U1</c>.
        /// </summary>
        public string Name => DxfReader.NameOf(GetString(2) ?? string.Empty);

        /// <summary>
        /// Gets or sets the block the INSERT places, as AutoCAD's <c>BlockTableRecord</c> gives its id: the record of the block
        /// table named by <see cref="Name"/>; null where the drawing has no block of that name. A copy in no file yet, as
        /// <see cref="Explode"/> gives a nested INSERT, places the block of the drawing it was copied from, as AutoCAD's copy
        /// keeps its block's id, until it is added to a drawing: a copy in no file gives the block of the drawing it was copied
        /// from while that block is there, renamed or not.
        /// </summary>
        /// <value>Set, a block of the same drawing, written as its name; the attributes are kept as they are.</value>
        /// <exception cref="ArgumentNullException">Thrown when the block set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the block set is in no file or another, is a layout, or would make the
        /// block the INSERT is in insert itself.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the INSERT is erased.</exception>
        public DxfBlockTableRecord BlockTableRecord
        {
            get
            {
                string name = Name;

                if (_record != null && string.Equals(_record.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return _record;
                }

                return Item.File.BlockSource(Item) is DxfFile source ? Resolve(source, name) : null;
            }

            set
            {
                DxfBlockTableRecord now = BlockTableRecord;

                // The block it places already, or none where the drawing has none of its name: nothing to check or write.
                if (ReferenceEquals(value, now) && (value == null || string.Equals(value.Name, Name, StringComparison.Ordinal)))
                {
                    return;
                }

                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                CheckBlock(value, nameof(value));

                if (IsResident)
                {
                    if (value.File != Item.File)
                    {
                        throw new ArgumentException("The block " + value.Name + " is of another drawing than the INSERT.", nameof(value));
                    }

                    DxfBlockTableRecord owner = Owner as DxfBlockTableRecord;

                    if (owner != null && !owner.IsLayout && Inserts(value, owner, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
                    {
                        throw new ArgumentException("The block " + value.Name + " would make the block " + owner.Name + " insert itself.", nameof(value));
                    }
                }

                _record = value;

                if (!string.Equals(value.Name, GetString(2), StringComparison.Ordinal))
                {
                    SetString(2, value.Name);
                }
            }
        }

        /// <summary>
        /// Gets or sets where the INSERT puts its block's origin (groups 10, 20, 30), as AutoCAD's <c>Position</c>: in world
        /// coordinates, the file's point taken from the system of the <see cref="Normal"/>'s plane; the origin where the record
        /// gives none.
        /// </summary>
        /// <value>Set, the point the INSERT is moved to, its ATTRIBs moved with it.</value>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the INSERT is erased.</exception>
        public GeoPoint3 Position
        {
            get => OcsToWorld(Normal, GetPoint(10, Origin));

            set
            {
                CheckPoint(value);

                if (!SamePoint(value, Position))
                {
                    Place(PointGroups(10, WorldToOcs(Normal, value), Origin));
                }
            }
        }

        /// <summary>
        /// Gets or sets how far the INSERT turns its block (group 50, in degrees), as AutoCAD's <c>Rotation</c>: in radians,
        /// counter-clockwise about its normal; nought where the record gives none.
        /// </summary>
        /// <value>Set, the angle the INSERT is turned to about its insertion point, its ATTRIBs turned with it.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the INSERT is erased.</exception>
        public double Rotation
        {
            get => GetDouble(50, 0.0) * (Math.PI / 180.0);

            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                if (!Same(value, Rotation))
                {
                    Place(new KeyValuePair<int, string>(50, Numbers.Exact(DxfOcsTransform.Degrees(value))));
                }
            }
        }

        /// <summary>
        /// Gets or sets how the INSERT scales its block along its x, y and z (groups 41, 42, 43), as AutoCAD's
        /// <c>ScaleFactors</c>; 1 for each the record gives none of. A factor below nought mirrors the block.
        /// </summary>
        /// <value>Set, three finite factors none of them nought, its ATTRIBs scaled with it.</value>
        /// <exception cref="ArgumentException">Thrown when a factor set is nought or not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the INSERT is erased.</exception>
        public GeoVector3 ScaleFactors
        {
            get => new GeoVector3(GetDouble(41, 1.0), GetDouble(42, 1.0), GetDouble(43, 1.0));

            set
            {
                GeoVector3 now = ScaleFactors;

                if (Same(value.X, now.X) && Same(value.Y, now.Y) && Same(value.Z, now.Z))
                {
                    return;
                }

                if (!value.IsValid || value.X == 0.0 || value.Y == 0.0 || value.Z == 0.0)
                {
                    throw new ArgumentException("Scale factors have to be finite numbers, none of them nought: " + value + " is not.", nameof(value));
                }

                Place(Join(Factor(41, value.X, now.X), Factor(42, value.Y, now.Y), Factor(43, value.Z, now.Z)));
            }
        }

        /// <summary>
        /// Gets or sets the normal of the plane the INSERT turns its block in (groups 210, 220, 230), as AutoCAD's
        /// <c>Normal</c>; of unit length, (0, 0, 1) where the record gives none.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length; the insertion point stays where it is in the
        /// world, written anew in the system of the new plane, and the ATTRIBs follow the block.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the INSERT is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();

            set
            {
                KeyValuePair<int, string>[] normal = NormalGroups(value);

                if (normal.Length == 0)
                {
                    return;
                }

                double length = value.Length;
                GeoVector3 unit = Math.Abs(length - 1.0) <= 1E-12 ? value : value.Divide(length);
                Place(Join(PointGroups(10, WorldToOcs(unit, Position), Origin), normal));
            }
        }

        /// <summary>
        /// Gets or sets the transformation that places the block, as AutoCAD's <c>BlockTransform</c>: from the coordinates of
        /// the block to the world, the block's origin taken to <see cref="Position"/>, its axes scaled by
        /// <see cref="ScaleFactors"/>, turned by <see cref="Rotation"/> and stood in the plane of <see cref="Normal"/>. The
        /// origin of the block is taken as (0, 0, 0) where the drawing has no block of the INSERT's name.
        /// </summary>
        /// <value>Set, a transformation an INSERT can hold, its axes square to one another: the position, rotation, scale
        /// factors and normal that make it written, the ATTRIBs carried by the change.</value>
        /// <exception cref="ArgumentNullException">Thrown when the transformation set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation set shears or flattens, which an INSERT does not hold.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the INSERT is erased.</exception>
        public GeoTransform3 BlockTransform
        {
            get => PlacedFrom(BlockTableRecord?.Origin ?? Origin);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                GeoTransform3 now = BlockTransform;
                bool same = true;

                for (int r = 0; r < 4 && same; r++)
                {
                    for (int c = 0; c < 4 && same; c++)
                    {
                        same = Same(value[r, c], now[r, c]);
                    }
                }

                if (same)
                {
                    return;
                }

                // The INSERT that stands unturned at the origin of the world, with no normal of its own, carried by the transformation.
                GeoPoint3 origin = BlockTableRecord?.Origin ?? Origin;
                System(new GeoPoint3(0.0, 0.0, 0.0), new GeoVector3(1.0, 1.0, 1.0), 0.0, new GeoVector3(0.0, 0.0, 1.0), value, nameof(value),
                    out GeoVector3 scale, out double rotation, out GeoVector3 normal, out _, out _);
                GeoPoint3 position = value.Transform(origin);
                GeoVector3 factors = ScaleFactors;
                double degrees = rotation;
                GeoVector3 normalNow = Normal;
                Place(Join(
                    PointGroups(10, WorldToOcs(normal, position), Origin),
                    Factor(41, scale.X, factors.X),
                    Factor(42, scale.Y, factors.Y),
                    Factor(43, scale.Z, factors.Z),
                    Same(degrees * (Math.PI / 180.0), Rotation) ? new KeyValuePair<int, string>[0] : new[] { new KeyValuePair<int, string>(50, Numbers.Exact(degrees + 0.0)) },
                    Same(normal.X, normalNow.X) && Same(normal.Y, normalNow.Y) && Same(normal.Z, normalNow.Z) ? new KeyValuePair<int, string>[0] : NormalGroups(normal)));
            }
        }

        /// <summary>
        /// Gets the ATTRIBs of the INSERT, as AutoCAD's <c>AttributeCollection</c>: the values it gives its block's attributes,
        /// in the order of the file, read as they stand; empty where it has none. Each ATTRIB is the same object while its
        /// record is there; <see cref="DxfAttributeCollection.AppendAttribute"/> adds one.
        /// </summary>
        public DxfAttributeCollection AttributeCollection => _collection ?? (_collection = new DxfAttributeCollection(this));

        /// <summary>The ATTRIBs of the INSERT as they stand, in the order of the file.</summary>
        internal IReadOnlyList<DxfAttributeReference> Attributes()
        {
            int count = RecordCount;
            var attributes = new List<DxfAttributeReference>();

            for (int r = 1; r < count; r++)
            {
                if (RecordType(r) == "ATTRIB" && FollowerAt(r) is DxfAttributeReference attribute)
                {
                    attributes.Add(attribute);
                }
            }

            return attributes.AsReadOnly();
        }

        /// <summary>
        /// Adds an attribute made in code after the INSERT's other ATTRIBs, as <see cref="DxfAttributeCollection.AppendAttribute"/>
        /// says: the attributes-follow flag (66) and a SEQEND given to an INSERT that has none, the attribute's own layer kept.
        /// </summary>
        internal void AppendAttribute(DxfAttributeReference attribute)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            if (attribute.IsResident || attribute.IsErased || attribute.Record != 0)
            {
                throw new InvalidOperationException("The ATTRIB is of a file or an INSERT already: append one made in code.");
            }

            if (attribute.Tag.Length == 0)
            {
                throw new ArgumentException("An ATTRIB is named by its tag: give it one before it is appended.", nameof(attribute));
            }

            string layer = attribute.Layer;
            int count = RecordCount;
            bool ended = count > 1 && RecordType(count - 1) == "SEQEND";

            if (!ended)
            {
                SetInt(66, 1, 0);
            }

            InsertFollowerAt(ended ? count - 1 : count, attribute);

            if (!ended)
            {
                InsertFollowerAt(RecordCount, new DxfUnknownEntity(DxfFile.Detached("SEQEND", new DxfTypedValue[0])));
            }

            // A record put among an entity's takes the entity's layer; an ATTRIB keeps its own, as AutoCAD keeps it.
            attribute.Layer = layer;
        }

        /// <summary>
        /// Gets the units the block is drawn in (group 70 of its BLOCK_RECORD), as AutoCAD's <c>BlockUnit</c>;
        /// <see cref="DxfUnits.Undefined"/> where the drawing gives none, as one older than R2007 does not.
        /// </summary>
        public DxfUnits BlockUnit
        {
            get
            {
                DxfItem record = BlockTableRecord?.Item;

                if (record == null || record.Type != "BLOCK_RECORD")
                {
                    return DxfUnits.Undefined;
                }

                return int.TryParse(record.File.ValueOf(record, 70)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int units) ? (DxfUnits)units : DxfUnits.Undefined;
            }
        }

        /// <summary>
        /// Gets how a length of the block is scaled to the drawing by their units, as AutoCAD's <c>UnitFactor</c>: the
        /// block's <see cref="BlockUnit"/> against the drawing's <c>$INSUNITS</c>; 1 where either is
        /// <see cref="DxfUnits.Undefined"/>. A block drawn in inches inserted in a drawing of millimetres has 25.4; in one of
        /// metres, 0.0254. It is read from the file alone: the INSERT's <see cref="ScaleFactors"/> and
        /// <see cref="BlockTransform"/> do not take it in.
        /// </summary>
        public double UnitFactor
        {
            get
            {
                DxfBlockTableRecord record = BlockTableRecord;
                IReadOnlyList<DxfTypedValue> insunits = record?.File?.HeaderOf("$INSUNITS");
                double block = Millimetres((int)BlockUnit);
                double drawing = insunits != null && insunits.Count > 0 && int.TryParse(insunits[0].Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int units) ? Millimetres(units) : 0.0;
                return block > 0.0 && drawing > 0.0 ? block / drawing : 1.0;
            }
        }

        /// <summary>
        /// Gets whether the INSERT is exploded as a block reference, as AutoCAD's <c>TreatAsBlockRefForExplode</c>: true for
        /// every INSERT and MINSERT, which <see cref="Explode"/> breaks into its block's entities.
        /// </summary>
        public bool TreatAsBlockRefForExplode => true;

        /// <summary>
        /// Gets whether the INSERT places a dynamic block, as AutoCAD's <c>IsDynamicBlock</c>: its block is one (its
        /// BLOCK_RECORD has the extended data <c>AcDbDynamicBlockGUID</c>), or is the anonymous block that stands for one, whose
        /// extended data <c>AcDbBlockRepBTag</c> names the dynamic block's BLOCK_RECORD by its handle (1005), as AutoCAD writes
        /// them and ezdxf reads them. An anonymous block whose tag names no block of the drawing is not.
        /// </summary>
        /// <remarks>
        /// The dynamic properties themselves, AutoCAD's <c>DynamicBlockReferencePropertyCollection</c>, <c>ResetBlock</c> and
        /// <c>ConvertToStaticBlock</c>, are not given: they are worked out by AutoCAD's engine of dynamic blocks from the
        /// evaluation graph of the block's extension dictionary, which a file holds but does not evaluate.
        /// </remarks>
        public bool IsDynamicBlock => Dynamic(out _) != null;

        /// <summary>
        /// Gets the dynamic block the INSERT places, as AutoCAD's <c>DynamicBlockTableRecord</c>: the block whose anonymous
        /// copy it places, or its own block where that is the dynamic one; the INSERT's <see cref="BlockTableRecord"/> where it
        /// places no dynamic block, as AutoCAD gives it.
        /// </summary>
        public DxfBlockTableRecord DynamicBlockTableRecord => Dynamic(out _) ?? BlockTableRecord;

        /// <summary>
        /// Gets the anonymous block that stands for the dynamic block the INSERT places, as AutoCAD's
        /// <c>AnonymousBlockTableRecord</c>: its own block where that is such a copy, <c>*U7</c> of the dynamic block BOLT;
        /// null where it places the dynamic block itself, or no dynamic block.
        /// </summary>
        public DxfBlockTableRecord AnonymousBlockTableRecord
        {
            get
            {
                Dynamic(out DxfBlockTableRecord anonymous);
                return anonymous;
            }
        }

        /// <summary>
        /// Gets the box the shapes of the block take where the INSERT places them, as AutoCAD's <c>GeometryExtentsBestFit</c>:
        /// the least and greatest x, y and z of each shape placed, not of the block's box placed, so that a block turned by 45
        /// degrees has the box of its shapes, not the larger one of its turned box; as <see cref="DxfEntity.GeometricExtents"/>
        /// gives them.
        /// </summary>
        /// <returns>The box, in the coordinates of the file.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the INSERT places no shape the library reads, or would place
        /// more than the 2 000 000 entities of <see cref="DxfReadOptions.MaxPlacedEntities"/>.</exception>
        public GeoAabb3 GeometryExtentsBestFit() => GeometryExtentsBestFit(GeoTransform3.Identity);

        /// <summary>
        /// Gets the box the shapes of the block take where the INSERT places them, then moved by a transformation, as AutoCAD's
        /// <c>GeometryExtentsBestFit(parentTransform)</c>: the transformation that places the block the INSERT is in, an
        /// outer INSERT's <see cref="BlockTransform"/>, so that an INSERT of BOLT inside FRAME has the box of its bolt where the
        /// INSERT of FRAME puts it. The INSERT of a block of the line from (0, 0, 0) to (0, 1, 0), turned by 90 degrees at
        /// (5, 0, 0), has the box (4, 0, 0) to (5, 0, 0); moved by (0, 10, 0), the box (4, 10, 0) to (5, 10, 0).
        /// </summary>
        /// <param name="parentTransform">The transformation, applied after the INSERT's own placing.</param>
        /// <returns>The box, in the coordinates the transformation takes the file's to.</returns>
        /// <remarks>
        /// The identity gives <see cref="DxfEntity.GeometricExtents"/> of an INSERT of a drawing; any other transformation, or
        /// an INSERT in no drawing, the box of the entities of <see cref="Explode"/> each moved by it, an INSERT among them
        /// exploded in its turn, so that what the block now holds is what is measured.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation flattens or shears an entity of the block that
        /// cannot hold it, or the block inserts itself.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the INSERT places no shape the library reads, its drawing has
        /// no block of its name, or it would place more than the 2 000 000 entities of
        /// <see cref="DxfReadOptions.MaxPlacedEntities"/>.</exception>
        public GeoAabb3 GeometryExtentsBestFit(GeoTransform3 parentTransform)
        {
            if (parentTransform == null)
            {
                throw new ArgumentNullException(nameof(parentTransform));
            }

            if (IsResident && DxfOcsTransform.IsIdentity(parentTransform))
            {
                return GeometricExtents;
            }

            double[] box = null;
            var walk = new Walk(Name, BlockToExplode(Item.File).Name);
            Measure(this, parentTransform, walk, ref box);

            if (box == null)
            {
                throw new InvalidOperationException("The INSERT of " + Name + " places no shape the library reads to give it extents.");
            }

            return new GeoAabb3(new GeoPoint3(box[0], box[1], box[2]), new GeoPoint3(box[3], box[4], box[5]));
        }

        /// <summary>
        /// Explodes the INSERT into the block it is in, as AutoCAD's <c>ExplodeToOwnerSpace</c>: the entities
        /// <see cref="Explode"/> gives, an ATTDEF of the block among them, appended to the INSERT's owner, model space for
        /// one of model space, in the order of the block, with handles of their own. The INSERT itself is kept, with its
        /// ATTRIBs, as AutoCAD keeps it: <c>insert.ExplodeToOwnerSpace(); insert.Erase();</c> explodes it as AutoCAD's EXPLODE
        /// does, the attributes turned back into their definitions.
        /// </summary>
        /// <remarks>
        /// An entity the drawing cannot hold, as the ELLIPSE a circle comes out as under a scale unlike along its axes in a
        /// drawing of R12, is refused: those appended before it are erased again, the drawing as it was but for the handles
        /// given on the way.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the INSERT is in no drawing, is erased, its drawing has no
        /// block of its name, or exploding would make more than the 2 000 000 entities of
        /// <see cref="DxfReadOptions.MaxPlacedEntities"/>.</exception>
        /// <exception cref="NotSupportedException">Thrown for a MINSERT of more than one cell: <see cref="Explode"/> gives the
        /// entities of its first cell alone, so the others would be lost from the owner.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing cannot hold an entity exploding makes, or the block
        /// inserts itself.</exception>
        public void ExplodeToOwnerSpace()
        {
            if (!(IsResident && Owner is DxfBlockTableRecord owner))
            {
                throw new InvalidOperationException("The INSERT of " + Name + " is in no block of a drawing to be exploded into: append it to one first.");
            }

            if (CellCount > 1)
            {
                throw new NotSupportedException("A MINSERT of more than one cell is not exploded into its owner: Explode gives its first cell alone, and the others would be lost.");
            }

            IReadOnlyList<DxfEntity> parts = Explode();
            var appended = new List<DxfEntity>(parts.Count);

            try
            {
                foreach (DxfEntity part in parts)
                {
                    owner.AppendEntity(part);
                    appended.Add(part);
                }
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                for (int i = appended.Count - 1; i >= 0; i--)
                {
                    appended[i].Erase();
                }

                throw;
            }
        }

        /// <summary>
        /// Gets the entities of the block placed where the INSERT puts them, as AutoCAD's <c>Explode</c> gives them: new
        /// entities in no file, one level down, so that an INSERT in the block comes out an INSERT, and an ATTDEF of the block a
        /// <see cref="DxfAttributeDefinition"/> placed as the rest are. The INSERT's ATTRIBs are not among them: their values
        /// stay where they are, in <see cref="AttributeCollection"/>. A MINSERT gives those of its first cell.
        /// </summary>
        /// <returns>The entities, in the order of the block, each of its own look; to be added to a drawing by
        /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>. The INSERT of BOLT whose block holds a CIRCLE, a LINE and
        /// the ATTDEF MARK, and which gives MARK the value B-12, explodes into a <see cref="DxfCircle"/>, a <see cref="DxfLine"/>
        /// and the <see cref="DxfAttributeDefinition"/> MARK; B-12 is not among them.</returns>
        /// <remarks>
        /// <para>
        /// An entity whose class can be moved by the block's placing is a copy of it moved so, as
        /// <see cref="DxfEntity.GetTransformedCopy(GeoTransform3)"/> gives it. One the placing cannot keep its class under, as a
        /// circle scaled unlike along its two axes, is made of the shapes <see cref="DxfReader"/> places of it alone, written as
        /// <see cref="DxfWriter"/> writes them, an ELLIPSE for that circle, with its look; nothing nested is placed for it. An
        /// INSERT in the block that cannot hold the placing, one turned in its block under a scale unlike along the world's
        /// axes, is exploded in its turn and its parts placed, so that what is drawn stays where it was. A type the library
        /// neither moves nor reads, as a HATCH, is left out.
        /// </para>
        /// <para>
        /// The entities are made in a drawing of R2018: an ELLIPSE or a SPLINE that a circle or an arc comes out as under a scale
        /// unlike along its axes is refused by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> in a drawing of R12,
        /// which has no such type.
        /// </para>
        /// <para>
        /// What exploding makes is counted before any INSERT of the block is exploded in its turn, and held to the bound the reader
        /// holds the INSERTs it places to, <see cref="DxfReadOptions.MaxPlacedEntities"/>'s 2 000 000: each entity given, and each
        /// INSERT moved or exploded on the way through the nest; past it, nothing is given. A block of 100 INSERTs, each turned by 20 degrees, of a block of 100 INSERTs turned by 10
        /// degrees of a block of 100 lines, inserted stretched to twice its width, comes out as 1 000 000 lines, its INSERTs
        /// exploded all the way down; the same with 200 lines in the last block is refused. An INSERT that has to be exploded in
        /// its turn, of a block already being exploded further out, would have the block insert itself without end, and is
        /// refused too; one that holds the placing comes out an INSERT, as the block's other INSERTs do.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when an INSERT in the nest that has to be exploded in its turn is of a block
        /// already being exploded, so that the block would insert itself.</exception>
        /// <exception cref="InvalidOperationException">Thrown when exploding would make more than 2 000 000 entities, the bound of
        /// <see cref="DxfReadOptions.MaxPlacedEntities"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no block of the INSERT's name.</exception>
        public override IReadOnlyList<DxfEntity> Explode() => Parts(Item.File, 0).AsReadOnly();

        /// <summary>
        /// Moves, turns, scales or mirrors the INSERT, as AutoCAD's <c>TransformBy</c> does, and as ezdxf's <c>transform()</c>
        /// carries one: its system carried to the one the transformation makes of it, its scale factors stretched as its axes
        /// are, one turned negative where an axis is mirrored, and its ATTRIBs moved by the same transformation.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation would shear the block, its axes no longer square,
        /// which an INSERT does not hold, or flattens it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the INSERT is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            // The identity leaves the INSERT and its ATTRIBs as they are: no angle is taken through degrees and back.
            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            GeoVector3 normalNow = Normal;
            GeoVector3 scaleNow = ScaleFactors;
            double degreesNow = GetDouble(50, 0.0);
            GeoPoint3 insert = System(GetPoint(10, Origin), scaleNow, degreesNow, normalNow, transform, nameof(transform), out GeoVector3 scale, out double degrees, out GeoVector3 normal, out double xStretch, out double yStretch);
            KeyValuePair<int, string>[] array = ArrayGroups(xStretch, yStretch);

            SetGroups(Join(
                PointGroups(10, insert, Origin),
                Factor(41, scale.X, scaleNow.X),
                Factor(42, scale.Y, scaleNow.Y),
                Factor(43, scale.Z, scaleNow.Z),
                Same(degrees, degreesNow) ? new KeyValuePair<int, string>[0] : new[] { new KeyValuePair<int, string>(50, Numbers.Exact(degrees + 0.0)) },
                array,
                NormalGroups(normal)));

            Carry(Attributes(), transform);
        }

        [DxfType("INSERT")]
        internal static DxfEntity Create(DxfItem item) => IsArray(item) ? null : new DxfBlockReference(item);

        /// <summary>Whether an INSERT is an array, a MINSERT: more than one column (70) or row (71).</summary>
        internal static bool IsArray(DxfItem item)
            => (int.TryParse(item.File.ValueOf(item, 70)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int columns) && columns > 1)
            || (int.TryParse(item.File.ValueOf(item, 71)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rows) && rows > 1);

        /// <summary>Refuses a block of no drawing or another, a layout, or one that would make the block it is added to insert itself.</summary>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            DxfBlockTableRecord block = _record != null && string.Equals(_record.Name, Name, StringComparison.OrdinalIgnoreCase) ? _record : owner.File == null ? null : Resolve(owner.File, Name);

            if (block == null)
            {
                throw new ArgumentException("The drawing has no block " + Name + " for the INSERT to place.", "entity");
            }

            CheckBlock(block, "entity");

            if (block.File != owner.File)
            {
                throw new ArgumentException("The block " + block.Name + " is of another drawing than the block the INSERT is added to.", "entity");
            }

            if (!owner.IsLayout && Inserts(block, owner, new HashSet<string>(StringComparer.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("The INSERT of " + block.Name + " would make the block " + owner.Name + " insert itself.", "entity");
            }

            _record = block;

            // A drawing older than R2018 has no MTEXT embedded in an ATTRIB: its text's part keeps the first line.
            if (owner.File != null && !owner.File.Supports(1032))
            {
                foreach (DxfAttributeReference attribute in Attributes())
                {
                    attribute.DropEmbedded();
                }
            }
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>The groups a transformation gives the array of a MINSERT, by how far it stretches the turned x and y axes (the y below nought where mirrored): none for an INSERT.</summary>
        private protected virtual KeyValuePair<int, string>[] ArrayGroups(double xStretch, double yStretch) => new KeyValuePair<int, string>[0];

        /// <summary>How many cells the INSERT places its block in: 1, a MINSERT's columns by its rows.</summary>
        private protected virtual int CellCount => 1;

        /// <summary>The millimetres in a unit of <c>$INSUNITS</c>, as the reader takes them; nought for none, or a number the DXF reference does not list.</summary>
        private static double Millimetres(int units)
        {
            const double SurveyFoot = 1200.0 / 3937.0 * 1000.0;

            switch (units)
            {
                case 1: return 25.4;
                case 2: return 304.8;
                case 3: return 1609344.0;
                case 4: return 1.0;
                case 5: return 10.0;
                case 6: return 1000.0;
                case 7: return 1E6;
                case 8: return 2.54E-5;
                case 9: return 0.0254;
                case 10: return 914.4;
                case 11: return 1E-7;
                case 12: return 1E-6;
                case 13: return 1E-3;
                case 14: return 100.0;
                case 15: return 1E4;
                case 16: return 1E5;
                case 17: return 1E12;
                case 18: return 1.495978707E14;
                case 19: return 9.4607304725808E18;
                case 20: return 3.0856775814913673E19;
                case 21: return SurveyFoot;
                case 22: return SurveyFoot / 12.0;
                case 23: return SurveyFoot * 3.0;
                case 24: return SurveyFoot * 5280.0;
                default: return 0.0;
            }
        }

        /// <summary>
        /// Grows a box by what an INSERT explodes into, each entity moved by a transformation, an INSERT among them measured in
        /// its turn; one of a block already being measured further out refused, as the block would insert itself.
        /// </summary>
        private static void Measure(DxfBlockReference insert, GeoTransform3 transform, Walk walk, ref double[] box)
        {
            IReadOnlyList<DxfEntity> parts = insert.Explode();
            walk.Spend(parts.Count);

            foreach (DxfEntity part in parts)
            {
                if (part is DxfBlockReference nested)
                {
                    string name = nested.BlockToExplode(nested.Item.File).Name;

                    if (!walk.Path.Add(name))
                    {
                        throw new ArgumentException("The INSERT of " + name + " would make the block " + name + " insert itself.");
                    }

                    Measure(nested, transform, walk, ref box);
                    walk.Path.Remove(name);
                    continue;
                }

                if (!DxfOcsTransform.IsIdentity(transform))
                {
                    part.TransformBy(transform);
                }

                GeoAabb3 extents;

                try
                {
                    extents = part.GeometricExtents;
                }
                catch (InvalidOperationException)
                {
                    // An entity with no shape the library reads, as an ATTDEF, has no extents.
                    continue;
                }

                if (box == null)
                {
                    box = new[] { extents.Min.X, extents.Min.Y, extents.Min.Z, extents.Max.X, extents.Max.Y, extents.Max.Z };
                    continue;
                }

                box[0] = Math.Min(box[0], extents.Min.X);
                box[1] = Math.Min(box[1], extents.Min.Y);
                box[2] = Math.Min(box[2], extents.Min.Z);
                box[3] = Math.Max(box[3], extents.Max.X);
                box[4] = Math.Max(box[4], extents.Max.Y);
                box[5] = Math.Max(box[5], extents.Max.Z);
            }
        }

        /// <summary>
        /// The dynamic block the INSERT places, as ezdxf's <c>get_dynamic_block_definition</c> finds it: its own block where its
        /// BLOCK_RECORD has the extended data AcDbDynamicBlockGUID, else the block whose BLOCK_RECORD the extended data
        /// AcDbBlockRepBTag of its block names by handle (1005), that block then the anonymous one; null for neither.
        /// </summary>
        private DxfBlockTableRecord Dynamic(out DxfBlockTableRecord anonymous)
        {
            anonymous = null;
            DxfBlockTableRecord block = BlockTableRecord;

            if (block?.File == null || block.Item == null)
            {
                return null;
            }

            if (block.GetXDataForApplication("AcDbDynamicBlockGUID") != null)
            {
                return block;
            }

            string handle = null;

            foreach (DxfTypedValue group in block.GetXDataForApplication("AcDbBlockRepBTag") ?? new DxfTypedValue[0])
            {
                if (group.TypeCode == 1005)
                {
                    handle = group.Value?.Trim();
                    break;
                }
            }

            if (string.IsNullOrEmpty(handle))
            {
                return null;
            }

            foreach (DxfItem item in block.File.TableItems("BLOCK_RECORD"))
            {
                if (string.Equals(item.Handle, handle, StringComparison.OrdinalIgnoreCase))
                {
                    DxfBlockTableRecord dynamic = Resolve(block.File, DxfReader.NameOf(item.File.ValueOf(item, 2) ?? string.Empty));
                    anonymous = dynamic == null ? null : block;
                    return dynamic;
                }
            }

            return null;
        }

        /// <summary>The group that sets a scale factor, none where it is the one read, to the bit.</summary>
        private protected static KeyValuePair<int, string>[] Factor(int code, double value, double now)
            => Same(value, now) ? new KeyValuePair<int, string>[0] : new[] { new KeyValuePair<int, string>(code, Numbers.Exact(value + 0.0)) };

        /// <summary>
        /// The INSERT of a system carried by a transformation, as ezdxf's <c>InsertCoordinateSystem.transform</c> carries it, but
        /// judged on the block's own axes, turned by the INSERT's rotation: its point in the system of the new normal, its scale
        /// factors and its angle in degrees; and how far the turned x and y axes are stretched, the y's below nought where it
        /// comes out mirrored, for the spacing of a MINSERT. Refused where those axes come out no longer square, which an INSERT
        /// does not hold: a turned INSERT stretched unlike along the world's axes.
        /// </summary>
        private static GeoPoint3 System(GeoPoint3 insert, GeoVector3 scaleNow, double degreesNow, GeoVector3 normalNow, GeoTransform3 transform, string parameter, out GeoVector3 scale, out double degrees, out GeoVector3 normal, out double xStretch, out double yStretch)
        {
            const double Tol = 1E-9;
            var axes = new DxfOcsTransform.Axes(normalNow);
            GeoVector3 alongX = axes.X;
            GeoVector3 alongY = axes.Y;

            if (degreesNow != 0.0)
            {
                double turn = degreesNow * (Math.PI / 180.0);
                double cos = Math.Cos(turn);
                double sin = Math.Sin(turn);
                alongX = axes.X.Multiply(cos).Add(axes.Y.Multiply(sin));
                alongY = axes.Y.Multiply(cos).Subtract(axes.X.Multiply(sin));
            }

            GeoVector3 ux = transform.Transform(alongX);
            GeoVector3 uy = transform.Transform(alongY);
            GeoVector3 uz = transform.Transform(axes.Z);
            xStretch = ux.Length;
            yStretch = uy.Length;
            double xScale = ux.Length * scaleNow.X;
            double yScale = uy.Length * scaleNow.Y;
            double zScale = uz.Length * scaleNow.Z;

            if (!(ux.Length > 0.0) || !(uy.Length > 0.0) || !(uz.Length > 0.0) || !ux.IsValid || !uy.IsValid || !uz.IsValid)
            {
                throw new ArgumentException("The transformation flattens the INSERT.", parameter);
            }

            ux = DxfOcsTransform.Normalized(ux);
            uy = DxfOcsTransform.Normalized(uy);
            uz = DxfOcsTransform.Normalized(uz);

            if (Math.Abs(ux.DotProduct(uz)) > Tol || Math.Abs(ux.DotProduct(uy)) > Tol || Math.Abs(uz.DotProduct(uy)) > Tol)
            {
                throw new ArgumentException("The transformation would shear the INSERT: its axes would no longer be square, which an INSERT does not hold.", parameter);
            }

            GeoVector3 expected = DxfOcsTransform.Cross(uz, ux);

            if (!Close(expected.X, uy.X) || !Close(expected.Y, uy.Y) || !Close(expected.Z, uy.Z))
            {
                yScale = -yScale;
                yStretch = -yStretch;
            }

            var ocs = new DxfOcsTransform(normalNow, uz, transform);
            scale = new GeoVector3(xScale, yScale, zScale);
            degrees = ocs.TransformDegrees(degreesNow);
            normal = uz;
            return ocs.TransformVertex(insert);

            bool Close(double a, double b) => Math.Abs(a - b) <= Math.Max(1E-9 * Math.Max(Math.Abs(a), Math.Abs(b)), Tol);
        }

        /// <summary>The block of a name of a file: the record the block table gives for it; null where the file has none.</summary>
        private static DxfBlockTableRecord Resolve(DxfFile file, string name)
        {
            if (!file.Blocks.TryGetValue(name, out DxfFileBlock block))
            {
                return null;
            }

            if (block.Items.Record != null)
            {
                return block.Items.Record;
            }

            DxfItem record = null;

            foreach (DxfItem item in file.TableItems("BLOCK_RECORD"))
            {
                if (string.Equals(DxfReader.NameOf(item.File.ValueOf(item, 2) ?? string.Empty), block.Name, StringComparison.OrdinalIgnoreCase))
                {
                    record = item;
                    break;
                }
            }

            return record?.Wrapper as DxfBlockTableRecord ?? new DxfBlockTableRecord(file, record, block);
        }

        /// <summary>Whether a block, or a block it inserts at any depth, is a given one.</summary>
        private static bool Inserts(DxfBlockTableRecord block, DxfBlockTableRecord owner, HashSet<string> seen)
        {
            if (string.Equals(block.Name, owner.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!seen.Add(block.Name))
            {
                return false;
            }

            foreach (DxfEntity entity in block)
            {
                if (entity is DxfBlockReference reference && reference.BlockTableRecord is DxfBlockTableRecord inner && Inserts(inner, owner, seen))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CheckBlock(DxfBlockTableRecord block, string parameter)
        {
            if (block.File == null)
            {
                throw new ArgumentException("The block " + block.Name + " is in no drawing: add it to a block table first.", parameter);
            }

            if (block.IsLayout)
            {
                throw new ArgumentException("The block " + block.Name + " is a layout, which an INSERT does not place.", parameter);
            }
        }

        private static void CheckPoint(GeoPoint3 value)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", nameof(value));
            }
        }

        private static bool SamePoint(GeoPoint3 a, GeoPoint3 b) => Same(a.X, b.X) && Same(a.Y, b.Y) && Same(a.Z, b.Z);

        private static IEnumerable<DxfTypedValue> Groups(string marker, GeoPoint3 position, DxfBlockTableRecord block, IEnumerable<DxfTypedValue> array)
        {
            CheckPoint(position);

            var groups = new List<DxfTypedValue>
            {
                new DxfTypedValue(100, marker),
                new DxfTypedValue(2, block == null ? string.Empty : CheckedName(block)),
                new DxfTypedValue(10, Numbers.Exact(position.X + 0.0)),
                new DxfTypedValue(20, Numbers.Exact(position.Y + 0.0)),
                new DxfTypedValue(30, Numbers.Exact(position.Z + 0.0)),
            };

            if (array != null)
            {
                groups.AddRange(array);
            }

            return groups;
        }

        private static string CheckedName(DxfBlockTableRecord block)
        {
            if (block.IsLayout)
            {
                throw new ArgumentException("The block " + block.Name + " is a layout, which an INSERT does not place.", "blockTableRecord");
            }

            return block.Name;
        }

        /// <summary>The placing of the block's axes, without its move: scaled, turned and stood in the plane of the normal.</summary>
        private GeoTransform3 Placing()
        {
            GeoVector3 normal = Normal;
            OcsAxes(normal, out GeoVector3 u, out GeoVector3 v);
            GeoVector3 scale = ScaleFactors;
            double angle = Rotation;
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);
            GeoVector3 x = u.Multiply(cos).Add(v.Multiply(sin)).Multiply(scale.X);
            GeoVector3 y = v.Multiply(cos).Subtract(u.Multiply(sin)).Multiply(scale.Y);
            GeoVector3 z = normal.Multiply(scale.Z);
            return new GeoTransform3(new double[,]
            {
                { x.X, y.X, z.X, 0.0 },
                { x.Y, y.Y, z.Y, 0.0 },
                { x.Z, y.Z, z.Z, 0.0 },
                { 0.0, 0.0, 0.0, 1.0 },
            });
        }

        /// <summary>The placing of the block with its move to the insertion point, its origin left out.</summary>
        private GeoTransform3 Placement()
        {
            GeoTransform3 placing = Placing();
            GeoPoint3 at = Position;
            return new GeoTransform3(new double[,]
            {
                { placing[0, 0], placing[0, 1], placing[0, 2], at.X },
                { placing[1, 0], placing[1, 1], placing[1, 2], at.Y },
                { placing[2, 0], placing[2, 1], placing[2, 2], at.Z },
                { 0.0, 0.0, 0.0, 1.0 },
            });
        }

        /// <summary>Writes groups of the INSERT's placing at once, and carries its ATTRIBs by the change of placing.</summary>
        private void Place(params KeyValuePair<int, string>[] groups)
        {
            if (groups.Length == 0)
            {
                return;
            }

            IReadOnlyList<DxfAttributeReference> attributes = Attributes();
            GeoTransform3 before = attributes.Count > 0 ? Placement() : null;
            SetGroups(groups);

            if (before == null || !before.TryGetInverse(out GeoTransform3 undo, Tolerance.Default))
            {
                return;
            }

            Carry(attributes, Placement().Multiply(undo));
        }

        /// <summary>
        /// Moves the ATTRIBs by a transformation, all in one writing of the INSERT; one whose groups would land in the wrong
        /// part of its record, as an ATTRIB with an embedded MTEXT may, is moved alone.
        /// </summary>
        private void Carry(IReadOnlyList<DxfAttributeReference> attributes, GeoTransform3 transform)
        {
            var records = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>(attributes.Count);
            var apart = new List<DxfAttributeReference>();

            foreach (DxfAttributeReference attribute in attributes)
            {
                KeyValuePair<int, string>[] groups = attribute.CarriedGroups(transform);

                if (groups == null)
                {
                    apart.Add(attribute);
                }
                else if (groups.Length > 0)
                {
                    records.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(attribute.Record, groups));
                }
            }

            if (records.Count > 0)
            {
                SetGroupsAtMany(records, DxfAttributeReference.AttributeOrder);
            }

            foreach (DxfAttributeReference attribute in apart)
            {
                attribute.TransformBy(transform);
            }
        }

        /// <summary>
        /// The entities of the INSERT's block placed where it puts them, its ATTDEFs among them, as <see cref="Explode"/> gives
        /// them: its block found in a file where the INSERT is a copy in none.
        /// </summary>
        private List<DxfEntity> Parts(DxfFile file, int depth)
        {
            DxfBlockTableRecord block = BlockToExplode(file);
            var parts = new List<DxfEntity>();
            PlaceParts(block.ToList(), block.Origin, file ?? block.File, depth, parts, depth == 0 ? new Walk(Name, block.Name) : null);
            return parts;
        }

        /// <summary>
        /// Places entities of the block, in its coordinates, where the INSERT puts them: each one a copy of itself moved, where
        /// its class holds the placing; an INSERT that cannot hold it exploded in its turn and its parts placed so; any other,
        /// all together, as the reader places it alone in a block of its own, one level deep. Given a walk, as for the INSERT
        /// exploded first, what the INSERTs to explode in their turn make is counted, and a block that inserts itself found,
        /// before any of them is exploded.
        /// </summary>
        private void PlaceParts(IReadOnlyList<DxfEntity> entities, GeoPoint3 origin, DxfFile file, int depth, List<DxfEntity> into, Walk walk = null)
        {
            GeoTransform3 placing = PlacedFrom(origin);
            var slots = new List<object>();
            var pending = new List<DxfEntity>();
            var exploded = new List<DxfBlockReference>();

            foreach (DxfEntity entity in entities)
            {
                if (!(entity is DxfUnknownEntity))
                {
                    try
                    {
                        DxfEntity copy = entity.GetTransformedCopy(placing);
                        (copy as DxfMText)?.Unlink();
                        slots.Add(copy);
                        continue;
                    }
                    catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
                    {
                        // The class cannot hold its entity so placed.
                    }
                }

                if (entity is DxfBlockReference nested)
                {
                    // Its own parts, in this block's coordinates, placed in turn: the INSERT that cannot hold the placing is
                    // exploded one level more, as nothing else keeps its image; its slot the complement of its place.
                    if (depth < 64)
                    {
                        slots.Add(~exploded.Count);
                        exploded.Add(nested);
                    }

                    continue;
                }

                slots.Add(pending.Count);
                pending.Add(entity);
            }

            if (walk != null)
            {
                walk.Spend(slots.Count - exploded.Count);

                foreach (DxfBlockReference nested in exploded)
                {
                    CountExploded(nested, origin, file, walk, new List<DxfBlockReference>());
                }
            }

            List<DxfShape>[] shapes = pending.Count == 0 ? new List<DxfShape>[0] : PlacedAlone(pending, origin, file);

            foreach (object slot in slots)
            {
                if (slot is DxfEntity made)
                {
                    into.Add(made);
                    continue;
                }

                int at = (int)slot;

                if (at < 0)
                {
                    PlaceParts(exploded[~at].Parts(file, depth + 1), origin, file, depth + 1, into);
                    continue;
                }

                foreach (DxfShape shape in shapes[at])
                {
                    DxfEntity part = Made(pending[at], shape);

                    if (part != null)
                    {
                        into.Add(part);
                    }
                }
            }
        }

        /// <summary>The block the INSERT explodes: its block found in a file where the INSERT is a copy in none.</summary>
        private DxfBlockTableRecord BlockToExplode(DxfFile file)
        {
            DxfFile drawing = file != null && file.IsDetached ? file.Origin : file;
            DxfBlockTableRecord block = BlockTableRecord ?? (drawing == null || drawing.IsDetached ? null : Resolve(drawing, Name));

            if (block == null)
            {
                throw new InvalidOperationException("The drawing has no block " + Name + " for the INSERT to explode.");
            }

            return block;
        }

        /// <summary>
        /// Counts what <see cref="Parts"/> gives of the INSERT's block without making it: the INSERTs among the parts, each moved,
        /// are given back to be placed again; every other part is counted alone.
        /// </summary>
        private List<DxfBlockReference> Counted(DxfBlockTableRecord block, DxfFile file, Walk walk)
        {
            var inserts = new List<DxfBlockReference>();
            CountPlaced(block.ToList(), block.Origin, file, walk, inserts);
            return inserts;
        }

        /// <summary>
        /// Counts entities of a block placed as <see cref="PlaceParts"/> places them: one for each that is not an INSERT, one for
        /// each INSERT moved, kept in the list to be placed again, and one for each INSERT exploded in its turn, whose parts are
        /// counted so; an INSERT exploded of a block already on the walk's path refused.
        /// </summary>
        private void CountPlaced(IReadOnlyList<DxfEntity> entities, GeoPoint3 origin, DxfFile file, Walk walk, List<DxfBlockReference> inserts)
        {
            GeoTransform3 placing = PlacedFrom(origin);

            foreach (DxfEntity entity in entities)
            {
                if (!(entity is DxfBlockReference nested))
                {
                    walk.Spend(1);
                    continue;
                }

                try
                {
                    inserts.Add((DxfBlockReference)nested.GetTransformedCopy(placing));
                    walk.Spend(1);
                    continue;
                }
                catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
                {
                    // The INSERT cannot hold the placing.
                }

                // As deep as PlaceParts explodes: the path holds the block of the INSERT exploded first.
                if (walk.Path.Count <= 64)
                {
                    CountExploded(nested, origin, file, walk, inserts);
                }
            }
        }

        /// <summary>
        /// Counts an INSERT of the block exploded in its turn, as <see cref="PlaceParts"/> explodes it: one for it, and its parts
        /// placed so; refused when its block is already on the walk's path, being exploded further out.
        /// </summary>
        private void CountExploded(DxfBlockReference nested, GeoPoint3 origin, DxfFile file, Walk walk, List<DxfBlockReference> inserts)
        {
            DxfBlockTableRecord block = nested.BlockToExplode(file);

            if (!walk.Path.Add(block.Name))
            {
                throw new ArgumentException("The INSERT of " + block.Name + " would make the block " + block.Name + " insert itself.");
            }

            walk.Spend(1);
            CountPlaced(nested.Counted(block, file, walk), origin, file, walk, inserts);
            walk.Path.Remove(block.Name);
        }

        /// <summary>
        /// The names of the blocks being exploded, from the INSERT's own inwards, and how many entities exploding may yet make,
        /// <see cref="DxfReadOptions.MaxPlacedEntities"/> by default, as the reader places them.
        /// </summary>
        private sealed class Walk
        {
            private readonly string _name;
            private readonly int _most = new DxfReadOptions().MaxPlacedEntities;
            private long _left;

            internal Walk(string name, string block)
            {
                _name = name;
                _left = _most;
                Path.Add(block);
            }

            internal HashSet<string> Path { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            internal void Spend(int count)
            {
                _left -= count;

                if (_left < 0)
                {
                    throw new InvalidOperationException("Exploding the INSERT of " + _name + " would make more than " + _most.ToString(CultureInfo.InvariantCulture)
                        + " entities, the bound of DxfReadOptions.MaxPlacedEntities.");
                }
            }
        }

        /// <summary>
        /// The shapes the reader places of entities alone, each by its own, as this INSERT places them: written in a drawing of
        /// one block that holds them, each on a layer of its own to tell its shapes by, and one INSERT of it with this one's
        /// point, scales, angle and normal; no nested INSERT among them, so nothing past them is placed.
        /// </summary>
        private List<DxfShape>[] PlacedAlone(IReadOnlyList<DxfEntity> entities, GeoPoint3 origin, DxfFile file)
        {
            var text = new StringBuilder();

            void Group(int code, string value) => text.Append(code.ToString(CultureInfo.InvariantCulture)).Append('\n').Append(value).Append('\n');

            Group(0, "SECTION");
            Group(2, "HEADER");

            if (file?.Version != null)
            {
                Group(9, "$ACADVER");
                Group(1, file.Version);
            }

            Group(0, "ENDSEC");
            Group(0, "SECTION");
            Group(2, "BLOCKS");
            Group(0, "BLOCK");
            Group(8, "0");
            Group(2, "EXPLODED");
            Group(70, "0");
            Group(10, Numbers.Exact(origin.X + 0.0));
            Group(20, Numbers.Exact(origin.Y + 0.0));
            Group(30, Numbers.Exact(origin.Z + 0.0));
            Group(3, "EXPLODED");

            for (int i = 0; i < entities.Count; i++)
            {
                bool head = true;
                Group(0, entities[i].Item.Type);
                Group(8, "E" + i.ToString(CultureInfo.InvariantCulture));

                foreach (DxfTypedValue group in entities[i].Item.Groups)
                {
                    head &= group.TypeCode != 0;

                    if (!(head && group.TypeCode == 8))
                    {
                        Group(group.TypeCode, group.Value);
                    }
                }
            }

            Group(0, "ENDBLK");
            Group(8, "0");
            Group(0, "ENDSEC");
            Group(0, "SECTION");
            Group(2, "ENTITIES");
            Group(0, "INSERT");
            Group(8, "0");
            Group(2, "EXPLODED");

            foreach (int code in new[] { 10, 20, 30, 41, 42, 43, 50, 210, 220, 230 })
            {
                string value = GetString(code);

                if (value != null)
                {
                    Group(code, value);
                }
            }

            Group(0, "ENDSEC");
            Group(0, "EOF");

            var shapes = new List<DxfShape>[entities.Count];

            for (int i = 0; i < shapes.Length; i++)
            {
                shapes[i] = new List<DxfShape>();
            }

            foreach (DxfShape shape in DxfReader.Parse(text.ToString(), new DxfReadOptions { Tolerance = Tolerance.Default }).Shapes)
            {
                if (shape.BlockPath.Count == 1 && shape.Layer.Length > 1 && shape.Layer[0] == 'E'
                    && int.TryParse(shape.Layer.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out int at) && at < shapes.Length)
                {
                    shapes[at].Add(shape);
                }
            }

            return shapes;
        }

        /// <summary>The placing of the block whose origin is given: its axes as <see cref="Placing"/>, its origin taken to the insertion point.</summary>
        private GeoTransform3 PlacedFrom(GeoPoint3 origin)
        {
            GeoTransform3 placing = Placing();
            GeoVector3 shift = placing.Transform(new GeoVector3(origin.X, origin.Y, origin.Z));
            GeoPoint3 at = Position;
            return new GeoTransform3(new double[,]
            {
                { placing[0, 0], placing[0, 1], placing[0, 2], at.X - shift.X },
                { placing[1, 0], placing[1, 1], placing[1, 2], at.Y - shift.Y },
                { placing[2, 0], placing[2, 1], placing[2, 2], at.Z - shift.Z },
                { 0.0, 0.0, 0.0, 1.0 },
            });
        }

        /// <summary>A new entity of a placed shape with the look of the entity of the block it came of.</summary>
        private static DxfEntity Made(DxfEntity entity, DxfShape shape)
        {
            DxfEntity made;

            try
            {
                made = Of(DxfFile.Detached(shape.Shape));
            }
            catch (ArgumentException)
            {
                // A shape the writer does not write as one entity of its own is left out.
                return null;
            }

            made.Layer = entity.Layer;
            made.ColorIndex = entity.ColorIndex;
            made.TrueColor = entity.TrueColor;
            made.Linetype = entity.Linetype;
            made.LinetypeScale = entity.LinetypeScale;
            made.LineWeight = entity.LineWeight;
            made.Visible = entity.Visible;

            if (shape.Thickness != 0.0 && Thick.Contains(made.Item.Type))
            {
                made.Item.Thickness = shape.Thickness;
            }

            return made;
        }
    }
}
