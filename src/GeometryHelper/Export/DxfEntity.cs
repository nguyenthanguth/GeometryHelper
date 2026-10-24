using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An entity of a DXF file, as AutoCAD's <c>Entity</c> is one of a drawing: what it is drawn on and with (its layer,
    /// colour, linetype, lineweight and whether it is shown), the block it is in, and its geometry as the library's shapes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each type of entity is a class of its own, named as AutoCAD names its own with Dxf in front: a LINE is a
    /// <see cref="DxfLine"/>; a type the library has no class for is a <see cref="DxfUnknownEntity"/>, kept as the file
    /// has it. An entity of a <see cref="DxfDatabase"/> is changed by setting its properties: its record is written anew,
    /// every group the change is not about kept as it was, and a property given the value it has changes nothing.
    /// </para>
    /// <para>
    /// An entity made in code, <c>new DxfLine(start, end) { Layer = "steel", ColorIndex = 1 }</c>, is added to a drawing by
    /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>, which gives it its handle and owner: the same object is
    /// the entity of the drawing from then on.
    /// </para>
    /// <para>
    /// The look is as AutoCAD reads it: an entity with no colour is <see cref="ColorIndex"/> 256, BYLAYER; with no linetype,
    /// <c>ByLayer</c>; with no lineweight, <see cref="DxfLineWeight.ByLayer"/>. Nothing is taken from a block: a LINE of a
    /// block on layer 0 is on layer 0.
    /// </para>
    /// </remarks>
    public abstract class DxfEntity : DxfDBObject
    {
        private static readonly IReadOnlyList<object> NoShapes = Array.AsReadOnly(new object[0]);

        private static readonly IReadOnlyList<int> NoOrder = Array.AsReadOnly(new int[0]);

        /// <summary>The object of a record of a file, the one that stands for it from now on.</summary>
        private protected DxfEntity(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// An entity made in code from a shape, written as <see cref="DxfWriter"/> writes the shape, on layer 0 with no look of
        /// its own: <c>base(new GeoLine3(start, end))</c> is a LINE.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the writer has no overload of Add for the shape, or writes it as more than one entity.</exception>
        private protected DxfEntity(object shape)
            : base(DxfFile.Detached(shape))
        {
        }

        /// <summary>
        /// An entity made in code from its groups: its type, layer 0, then the groups given, which begin with its subclass
        /// marker, such as <c>base("RAY", new[] { new DxfTypedValue(100, "AcDbRay"), ... })</c>.
        /// </summary>
        private protected DxfEntity(string dxfName, IEnumerable<DxfTypedValue> groups)
            : base(DxfFile.Detached(dxfName, groups))
        {
        }

        #region The look

        /// <summary>
        /// Gets or sets the layer the entity is on (group 8), as AutoCAD's <c>Layer</c>: the name as the layer table names
        /// it, the spaces around it taken off and its escapes (<c>\U+00E9</c>) read as letters; <c>0</c> where the record
        /// gives none.
        /// </summary>
        /// <value>
        /// Set, the name of a layer. One the layer table does not list, letters of either case the same, is added to it in the
        /// colour 7 with the linetype CONTINUOUS, and the log says so; an entity made in code is given its layer when it is
        /// added to a drawing. The vertices and end of a POLYLINE follow it.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set is empty, has a space at either end, or has a control
        /// character or one of &lt; &gt; / " : ; ? * | = ` in it, which AutoCAD does not take in a name.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        public string Layer
        {
            get
            {
                string layer = Item.Layer;
                string name = layer == null ? string.Empty : DxfReader.NameOf(layer);
                return name.Length == 0 ? "0" : name;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!string.Equals(value, Layer, StringComparison.Ordinal))
                {
                    Item.Layer = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the colour of the entity (group 62), as AutoCAD's <c>ColorIndex</c>: 1 to 255 for a colour of AutoCAD's
        /// index, 0 for BYBLOCK and 256 for BYLAYER, which an entity that gives none has.
        /// </summary>
        /// <value>Set, from 0 to 256.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the colour set is below 0 or above 256.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        public int ColorIndex
        {
            get => Item.Colour ?? 256;

            set
            {
                if (value != ColorIndex)
                {
                    Item.Colour = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the colour of the entity, as AutoCAD's <c>Color</c>: its true colour (group 420, its book's name 430)
        /// where it has one, else its colour index (62), BYLAYER where it gives none. It agrees with <see cref="ColorIndex"/>
        /// and <see cref="TrueColor"/>, which read the same groups; a true colour carries the index nearest it, as
        /// <see cref="DxfColor.FromRgb"/> gives it: orange, 0xFF8000, the index 30, whatever index the record writes beside it.
        /// </summary>
        /// <value>
        /// Set, BYLAYER, BYBLOCK or a colour of the index writes the index and takes out a true colour and its name; a true
        /// colour writes its red, green and blue (420) and the index nearest it (62), as AutoCAD writes one, and takes out a
        /// name. A colour equal to the one read changes nothing.
        /// </value>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is by pen, the foreground, a layer's off or frozen, or none, which an entity of DXF does not hold.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased, or a true colour is set on one of a file older than R2004 (AC1018).</exception>
        public DxfColor Color
        {
            get
            {
                int? colour = Item.TrueColour;

                if (colour.HasValue)
                {
                    return DxfColor.OfRgb(colour.Value, Item.File.CommonValueOf(Item, 430));
                }

                return DxfColor.OfIndex(ColorIndex);
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.Equals(Color))
                {
                    return;
                }

                switch (value.ColorMethod)
                {
                    case DxfColorMethod.ByLayer:
                    case DxfColorMethod.ByBlock:
                    case DxfColorMethod.ByAci:
                        TrueColor = null;
                        Item.File.SetCommon(Item, 430, null);
                        ColorIndex = value.ColorIndex;
                        break;
                    case DxfColorMethod.ByColor:
                        TrueColor = value.ColorValue;
                        Item.File.SetCommon(Item, 430, null);
                        ColorIndex = value.ColorIndex;
                        break;
                    default:
                        throw new ArgumentException("An entity of DXF is by layer, by block, of the index or of a true colour: " + value.ColorMethod + " is none of them.", nameof(value));
                }
            }
        }

        /// <summary>
        /// Gets or sets the colour of the entity as red, green and blue (group 420), 0xRRGGBB, which a CAD program draws in
        /// place of <see cref="ColorIndex"/>: 0xFF8000 is orange; null where it has none.
        /// </summary>
        /// <value>Set, from 0 to 0xFFFFFF; null takes it out.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the colour set is below 0 or above 0xFFFFFF.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased, or a colour is set on one of a file
        /// older than R2004 (AC1018), which has no true colours.</exception>
        public int? TrueColor
        {
            get
            {
                int? colour = Item.TrueColour;
                return colour.HasValue ? colour.Value & 0xFFFFFF : (int?)null;
            }

            set
            {
                if (value != TrueColor)
                {
                    Item.TrueColour = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the name of the linetype of the entity (group 6), as AutoCAD's <c>Linetype</c>: <c>DASHED</c>,
        /// <c>ByBlock</c>; <c>ByLayer</c> where the record gives none. The name is as the record spells it, the spaces around it
        /// taken off.
        /// </summary>
        /// <value>Set, a linetype of the file's linetype table, letters of either case the same, or BYLAYER or BYBLOCK.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the linetype set is not in the file's linetype table.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        public string Linetype
        {
            get
            {
                string linetype = Item.Linetype;
                string name = linetype == null ? string.Empty : DxfReader.NameOf(linetype);
                return name.Length == 0 ? "ByLayer" : name;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!string.Equals(value, Linetype, StringComparison.Ordinal))
                {
                    Item.Linetype = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the scale of the entity's linetype (group 48), as AutoCAD's <c>LinetypeScale</c>: 2 draws its dashes
        /// twice as long; 1 where the record gives none.
        /// </summary>
        /// <value>Set, a finite number above nought.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased, or a scale is set on one of a file of
        /// R12, which has no scale of an entity's own: R13 (AC1012) brought it.</exception>
        public double LinetypeScale
        {
            get => Numbers.TryRead(Item.File.CommonValueOf(Item, 48), out double scale) && Guard.IsFinite(scale) ? scale : 1.0;

            set
            {
                // The scale it has changes nothing, whatever the file holds.
                if (Same(value, LinetypeScale))
                {
                    return;
                }

                Guard.Positive(value, nameof(value), "A linetype scale has to be a finite number above nought.");
                Item.File.Requires(1012, "linetype scale of an entity's own", "R13 (AC1012)");
                Item.File.SetCommon(Item, 48, Numbers.Exact(value));
            }
        }

        /// <summary>
        /// Gets or sets the lineweight of the entity (group 370), as AutoCAD's <c>LineWeight</c>:
        /// <see cref="DxfLineWeight.LineWeight025"/> is a line 0.25 mm wide; <see cref="DxfLineWeight.ByLayer"/> where the
        /// record gives none.
        /// </summary>
        /// <value>Set, one of <see cref="DxfLineWeight"/>.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the weight set is not one of AutoCAD's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased, or a weight is set on one of a file
        /// older than R2000 (AC1015), which has no lineweights.</exception>
        public DxfLineWeight LineWeight
        {
            get => (DxfLineWeight)(Item.Lineweight ?? -1);

            set
            {
                if (value != LineWeight)
                {
                    Item.Lineweight = (int)value;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the entity is drawn (group 60), as AutoCAD's <c>Visible</c>: false where the record writes it
        /// invisible, 1; true where it gives none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased, or its visibility is changed in a file
        /// of R12, which writes none: R13 (AC1012) brought it.</exception>
        public bool Visible
        {
            get => !(int.TryParse(Item.File.CommonValueOf(Item, 60)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int hidden) && hidden == 1);

            set
            {
                if (value != Visible)
                {
                    Item.File.Requires(1012, "visibility of an entity", "R13 (AC1012)");
                    Item.File.SetCommon(Item, 60, value ? "0" : "1");
                }
            }
        }

        /// <summary>
        /// Gets what owns the entity, as AutoCAD's <c>OwnerId</c> names it: the block it is in,
        /// <see cref="DxfDatabase.ModelSpace"/> for an entity of model space; for a record that follows an entity, as a VERTEX
        /// of a POLYLINE or an ATTRIB of an INSERT, that entity. Null for one made in code and not yet added, or erased.
        /// </summary>
        public DxfDBObject Owner
        {
            get
            {
                if (IsErased)
                {
                    return null;
                }

                if (Item.Parent != null)
                {
                    return Of(Item.Parent);
                }

                if (!IsResident || Item.Space == null)
                {
                    return null;
                }

                // Model and paper space stand for their entities once the drawing is opened, whether walked yet or not.
                DxfDatabase database = Item.File.Database;
                DxfSpaceKind kind = Item.Space.Kind;
                return Item.Space.Record ?? (database == null || kind == DxfSpaceKind.Block ? null : kind == DxfSpaceKind.Model ? database.ModelSpace : database.PaperSpace);
            }
        }

        /// <summary>
        /// Gets the name of the block the entity is in, as AutoCAD's <c>BlockName</c>: <c>*Model_Space</c> for an entity of
        /// model space and <c>*Paper_Space</c> for one of paper space, whatever the file calls them; that of the entity it
        /// follows for a VERTEX or an ATTRIB; null where <see cref="Owner"/> is null.
        /// </summary>
        public string BlockName => Owner is DxfEntity parent ? parent.BlockName : (Owner as DxfBlockTableRecord)?.Name;

        /// <summary>
        /// Gives the entity the look of another, as AutoCAD's <c>SetPropertiesFrom</c> does: its layer, colour, true colour and
        /// the name of its colour book, linetype, linetype scale, lineweight and whether it is drawn. Everything is checked
        /// before anything is written: a look this entity's file cannot hold changes nothing.
        /// </summary>
        /// <param name="entity">The entity whose look is taken.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the layer's name is one AutoCAD does not take, or the linetype is not
        /// in the linetype table of this entity's file.</exception>
        /// <exception cref="InvalidOperationException">Thrown when this entity is erased, or the look has a true colour, a
        /// lineweight, a linetype scale or an invisibility this entity's file is too old to hold, as their setters say.</exception>
        public void SetPropertiesFrom(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (IsErased)
            {
                throw new InvalidOperationException("The " + Item.Type + " " + (Handle ?? string.Empty) + " is erased, and is not changed.");
            }

            string layer = entity.Layer;
            int colour = entity.ColorIndex;
            int? trueColour = entity.TrueColor;
            string book = trueColour.HasValue ? entity.Item.File.CommonValueOf(entity.Item, 430) : null;
            string linetype = entity.Linetype;
            double scale = entity.LinetypeScale;
            DxfLineWeight weight = entity.LineWeight;
            bool visible = entity.Visible;
            DxfFile file = Item.File;
            bool bookChanges = !string.Equals(book, file.CommonValueOf(Item, 430), StringComparison.Ordinal);

            if (!string.Equals(layer, Layer, StringComparison.Ordinal))
            {
                DxfSymbolTable.CheckName(layer, nameof(entity));
            }

            if (!string.Equals(linetype, Linetype, StringComparison.Ordinal) && !file.LinetypeListed(linetype))
            {
                throw new ArgumentException("The linetype " + linetype + " is not in the file's linetype table.", nameof(entity));
            }

            if ((trueColour.HasValue && trueColour != TrueColor) || (book != null && bookChanges))
            {
                file.Requires(1018, "true colours", "R2004 (AC1018)");
            }

            if (weight != LineWeight)
            {
                file.Requires(1015, "lineweights", "R2000 (AC1015)");
            }

            if (!Same(scale, LinetypeScale))
            {
                file.Requires(1012, "linetype scale of an entity's own", "R13 (AC1012)");
            }

            if (visible != Visible)
            {
                file.Requires(1012, "visibility of an entity", "R13 (AC1012)");
            }

            Layer = layer;
            ColorIndex = colour;
            TrueColor = trueColour;

            if (bookChanges)
            {
                file.SetCommon(Item, 430, book);
            }

            Linetype = linetype;
            LinetypeScale = scale;
            LineWeight = weight;
            Visible = visible;
        }

        /// <summary>
        /// Gives the entity the look a drawing gives new entities, as AutoCAD's <c>SetDatabaseDefaults</c>: the layer
        /// (<c>$CLAYER</c>), colour (<c>$CECOLOR</c>), linetype (<c>$CELTYPE</c>) and, in a drawing of R2000 or later,
        /// linetype scale (<c>$CELTSCALE</c>) and lineweight (<c>$CELWEIGHT</c>) of its header.
        /// </summary>
        /// <param name="database">The drawing whose header gives the look.</param>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the look cannot be held by the entity, as its setters say.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        public void SetDatabaseDefaults(DxfDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            Layer = database.Clayer;
            Color = database.Cecolor;
            Linetype = database.Celtype;

            if (!database.File.Supports(1015))
            {
                return;
            }

            IReadOnlyList<DxfTypedValue> scale = database.GetHeader("$CELTSCALE");

            if (scale != null && scale.Count > 0 && Numbers.TryRead(scale[0].Value, out double factor) && factor > 0.0 && Guard.IsFinite(factor))
            {
                LinetypeScale = factor;
            }

            LineWeight = database.Celweight;
        }

        /// <summary>
        /// Gives the entity the look of the drawing it is in, as AutoCAD's <c>SetDatabaseDefaults()</c> gives one that of its
        /// database, as <see cref="SetDatabaseDefaults(DxfDatabase)"/> says. AutoCAD gives an entity in no database the look
        /// of the drawing open in its window; an entity made in code is in no drawing until it is appended, so it is refused.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the look cannot be held by the entity, as its setters say.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is in no drawing, or is erased.</exception>
        public void SetDatabaseDefaults()
        {
            DxfDatabase database = IsResident ? Item.File.Database : null;

            if (database == null)
            {
                throw new InvalidOperationException("The " + Item.Type + " is in no drawing to take the look of: append it to a block first, or give the drawing.");
            }

            SetDatabaseDefaults(database);
        }

        #endregion

        #region The geometry

        /// <summary>
        /// Gets the box the entity's shapes fill, as AutoCAD's <c>GeometricExtents</c>: the least and greatest x, y and z the
        /// shapes of <see cref="GetGeometry"/> reach as <see cref="DxfWriter"/> writes them, its thickness not counted. An
        /// INSERT's are those of what it places; one whose block would place more than
        /// <see cref="DxfReadOptions.MaxPlacedEntities"/> by its default, 2 000 000 entities, places nothing, as the reader leaves
        /// it, and so has no extents, as an INSERT of a block the drawing does not define has none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the entity has no shape the library reads, as AutoCAD throws
        /// for an entity with no extents: a HATCH, a LINE of no length, an INSERT that places nothing.</exception>
        public GeoAabb3 GeometricExtents
        {
            get
            {
                double[] box = null;

                foreach (object shape in GetGeometry())
                {
                    if (!DxfWriter.CanWrite(shape))
                    {
                        continue;
                    }

                    DxfWriter.RecordsOf(shape, 0, 0.0, true, true, out double[] extents);

                    if (extents == null)
                    {
                        continue;
                    }

                    if (box == null)
                    {
                        box = (double[])extents.Clone();
                        continue;
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        box[i] = Math.Min(box[i], extents[i]);
                        box[i + 3] = Math.Max(box[i + 3], extents[i + 3]);
                    }
                }

                if (box == null)
                {
                    throw new InvalidOperationException("The " + Item.Type + " has no shape the library reads to give it extents.");
                }

                return new GeoAabb3(new GeoPoint3(box[0], box[1], box[2]), new GeoPoint3(box[3], box[4], box[5]));
            }
        }

        /// <summary>
        /// Gets the library's shapes for the entity, as <see cref="DxfReader"/> reads it with its default options: a
        /// <see cref="GeoLine3"/> for a LINE, two <see cref="GeoTriangle3"/> for a 3DFACE whose corners are not in one plane,
        /// the shapes of its block placed for an INSERT; none for a type the reader does not read, as a HATCH, or an entity with
        /// no shape to it, as a LINE of no length.
        /// </summary>
        /// <returns>The shapes, in the coordinates of the file, the same list until the entity is changed.</returns>
        public IReadOnlyList<object> GetGeometry()
        {
            if (Item.Type != "INSERT")
            {
                return Item.Shapes;
            }

            if (Item.Shape is DxfInsert insert)
            {
                var placed = new List<object>();

                foreach (DxfShape shape in insert.GetPlacedEntities())
                {
                    placed.Add(shape.Shape);
                }

                return placed.AsReadOnly();
            }

            return NoShapes;
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the entity by a transformation, as AutoCAD's <c>TransformBy</c> does: its record is
        /// written anew with the new geometry, its look kept.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown for an entity whose type the library keeps as it is, a
        /// <see cref="DxfUnknownEntity"/>.</exception>
        public abstract void TransformBy(GeoTransform3 transform);

        /// <summary>
        /// Gets a copy of the entity moved by a transformation, as AutoCAD's <c>GetTransformedCopy</c> does: an entity of the
        /// same class and look in no file, to be added to one by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <returns>The copy, with no handle, owner, reactors or extension dictionary; its extended data kept. The identity, to
        /// the bit, gives the entity's <see cref="Clone"/>, of any class.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased, as AutoCAD refuses an erased object.</exception>
        /// <exception cref="NotSupportedException">Thrown for a <see cref="DxfUnknownEntity"/> given another transformation than
        /// the identity.</exception>
        public DxfEntity GetTransformedCopy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            DxfEntity copy = Copy();

            if (!DxfOcsTransform.IsIdentity(transform))
            {
                copy.TransformBy(transform);
            }

            return copy;
        }

        /// <summary>
        /// Gets a copy of the entity as it is, as AutoCAD's <c>Clone</c> gives one of an object: an entity of the same class
        /// and look in no file, a <see cref="DxfUnknownEntity"/> among them, its records as the file has them but its handle,
        /// owner, space, reactors and extension dictionary; a VERTEX the vertex class it is, an ATTRIB an attribute. It is added
        /// to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>, as a copy from another drawing is: see
        /// that method for the pointers it carries.
        /// </summary>
        /// <returns>The copy.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased, as AutoCAD refuses an erased object.</exception>
        public DxfEntity Clone() => Copy();

        /// <summary>
        /// Gets the entities the entity breaks into, as AutoCAD's <c>Explode</c>: new entities in no file. An entity that does
        /// not break into others refuses, as AutoCAD answers <c>eNotApplicable</c>; a <see cref="DxfBlockReference"/> gives the
        /// entities of its block placed where it puts them.
        /// </summary>
        /// <returns>The entities, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown for a class that does not break into others.</exception>
        public virtual IReadOnlyList<DxfEntity> Explode()
            => throw new NotSupportedException("A " + GetType().Name + " does not break into other entities: AutoCAD answers eNotApplicable.");

        /// <summary>
        /// Gets the system of the entity's own coordinates, as AutoCAD's <c>Ecs</c>: the one the arbitrary axis algorithm of the
        /// DXF reference makes of its extrusion (group 210), taking a point of it to the world; the world's own where the
        /// extrusion is (0, 0, 1) or the record gives none.
        /// </summary>
        public GeoTransform3 Ecs
        {
            get
            {
                GeoVector3 normal = GetNormal();
                OcsAxes(normal, out GeoVector3 u, out GeoVector3 v);
                return new GeoTransform3(new double[,]
                {
                    { u.X, v.X, normal.X, 0.0 },
                    { u.Y, v.Y, normal.Y, 0.0 },
                    { u.Z, v.Z, normal.Z, 0.0 },
                    { 0.0, 0.0, 0.0, 1.0 },
                });
            }
        }

        #endregion

        #region Erasing

        /// <summary>
        /// Erases the entity from its block, as AutoCAD's <c>Erase</c> does: every record of it goes, a POLYLINE's vertices
        /// and end and an INSERT's attributes with it, its handle is not given again, and what pointed at it is seen to when the
        /// file is saved: a GROUP no longer lists it, a list of reactors no longer holds it, and the objects it owned, such as
        /// its extension dictionary, go with it. A record that follows an entity, a VERTEX of a POLYLINE or an ATTRIB of an
        /// INSERT, is taken out of it alone, every other record keeping its handle.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the entity is in no file, or is erased already; for the SEQEND
        /// that ends a POLYLINE's or an INSERT's records, which the entity keeps.</exception>
        public override void Erase()
        {
            if (Item.Parent != null)
            {
                Item.File.RemoveFollower(Item);
                return;
            }

            if (!IsResident)
            {
                throw new InvalidOperationException("The " + Item.Type + " is in no file to be erased from: add it to a block first.");
            }

            if (!Item.File.Erase(Item))
            {
                throw new InvalidOperationException("The " + Item.Type + " " + (Handle ?? string.Empty) + " is erased already.");
            }
        }

        #endregion

        #region What a class of entity is made of

        /// <summary>The object of a record: the one made for it before, else one of the class its type has, else a <see cref="DxfUnknownEntity"/>.</summary>
        internal static DxfEntity Of(DxfItem item)
        {
            if (item.Wrapper is DxfEntity kept)
            {
                return kept;
            }

            DxfEntity made = DxfEntityTypes.Create(item) ?? new DxfUnknownEntity(item);
            return (DxfEntity)item.KeepWrapper(made);
        }

        /// <summary>
        /// Checks that the entity, made in code, can be added to a block, before <see cref="DxfBlockTableRecord.AppendEntity"/>
        /// adds it: an INSERT refuses a block of another file, one not in the file, or one that would insert itself. Nothing
        /// unless the class says otherwise.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown where the class refuses the block.</exception>
        internal virtual void Appending(DxfBlockTableRecord owner)
        {
        }

        /// <summary>
        /// The codes of the type's own groups in the order AutoCAD writes them, a 100 where a subclass marker stands: where a
        /// group the record has not is put. Empty where a class sets none of its own.
        /// </summary>
        private protected virtual IReadOnlyList<int> GroupOrder => NoOrder;

        /// <summary>The text of the first group of a code in the type's own part of the record, as the file writes it; null where it has none.</summary>
        private protected string GetString(int code) => Item.File.ValueOf(Item, code);

        /// <summary>The number of the first group of a code in the type's own part, as the reader reads it; the fallback where it has none, or not a finite number.</summary>
        private protected double GetDouble(int code, double fallback) => Numbers.TryRead(GetString(code), out double value) && Guard.IsFinite(value) ? value : fallback;

        /// <summary>The whole number of the first group of a code in the type's own part; the fallback where it has none, or not a whole number.</summary>
        private protected int GetInt(int code, int fallback) => int.TryParse(GetString(code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        /// <summary>
        /// The point in the groups of a code and the codes 10 and 20 above it (10, 20, 30 for the first), as the reader reads
        /// one: a coordinate the record leaves out is nought, and the fallback where it has none of the three.
        /// </summary>
        private protected GeoPoint3 GetPoint(int code, GeoPoint3 fallback)
        {
            bool x = TryNumber(code, out double px);
            bool y = TryNumber(code + 10, out double py);
            bool z = TryNumber(code + 20, out double pz);
            return x || y || z ? new GeoPoint3(px, py, pz) : fallback;
        }

        /// <summary>
        /// The extrusion of the entity (groups 210, 220, 230) as the reader reads it: of unit length, as the file has it where
        /// it is within 1E-12 of that and made so where it is not; (0, 0, 1) where the record gives none, or one of no length.
        /// </summary>
        private protected GeoVector3 GetNormal()
        {
            bool any = TryNumber(210, out double x) | TryNumber(220, out double y) | TryNumber(230, out double z);
            double length = Math.Sqrt(x * x + y * y + z * z);

            if (!any || !(length > 0.0) || double.IsInfinity(length))
            {
                return new GeoVector3(0.0, 0.0, 1.0);
            }

            return Math.Abs(length - 1.0) <= 1E-12 ? new GeoVector3(x, y, z) : new GeoVector3(x / length, y / length, z / length);
        }

        /// <summary>Sets the text of a group of the type's own part as it is to be written; null takes it out. Nothing is written where it is as it was.</summary>
        private protected void SetString(int code, string value) => SetGroups(new KeyValuePair<int, string>(code, value));

        /// <summary>Sets a number of the type's own part, written as <see cref="DxfWriter"/> writes one; nothing is written where the number read is the same, to the bit.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number is not a finite number.</exception>
        private protected void SetDouble(int code, double value, double fallback)
        {
            Guard.Finite(value, nameof(value), "The number has to be a finite number.");

            if (!Same(value, GetDouble(code, fallback)))
            {
                SetString(code, Numbers.Exact(value + 0.0));
            }
        }

        /// <summary>Sets a whole number of the type's own part; nothing is written where the number read is the same.</summary>
        private protected void SetInt(int code, int value, int fallback)
        {
            if (value != GetInt(code, fallback))
            {
                SetString(code, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Sets a point in the groups of a code and the two above it, each coordinate that differs from the one read, to the
        /// bit, written anew, and the others of the point with it where the record leaves them out; nothing where none differs.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        private protected void SetPoint(int code, GeoPoint3 value, GeoPoint3 fallback) => SetGroups(PointGroups(code, value, fallback));

        /// <summary>Sets the extrusion (210, 220, 230) to a direction made of unit length, as <see cref="SetPoint"/> sets a point; nothing where it is the one read.</summary>
        /// <exception cref="ArgumentException">Thrown when the direction has no length or is not finite.</exception>
        private protected void SetNormal(GeoVector3 value) => SetGroups(NormalGroups(value));

        /// <summary>The groups that set a point, as <see cref="SetPoint"/> writes them; none where the point is as read.</summary>
        private protected KeyValuePair<int, string>[] PointGroups(int code, GeoPoint3 value, GeoPoint3 fallback)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", nameof(value));
            }

            GeoPoint3 now = GetPoint(code, fallback);
            return Changes(code, new[] { value.X, value.Y, value.Z }, new[] { now.X, now.Y, now.Z });
        }

        /// <summary>The groups that set the extrusion, as <see cref="SetNormal"/> writes them; none where it is as read.</summary>
        private protected KeyValuePair<int, string>[] NormalGroups(GeoVector3 value)
        {
            double length = value.Length;

            if (!value.IsValid || !(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("A normal has to be a direction of finite numbers and some length: " + value + " is not.", nameof(value));
            }

            GeoVector3 unit = Math.Abs(length - 1.0) <= 1E-12 ? value : value.Divide(length);
            GeoVector3 now = GetNormal();

            if (Same(unit.X, now.X) && Same(unit.Y, now.Y) && Same(unit.Z, now.Z))
            {
                return new KeyValuePair<int, string>[0];
            }

            // Compared with the groups the record holds, not with the normal read of them: one not of unit length, as
            // (1, 2, 3), is read made so, and a group the same as the new normal read would keep its old number.
            TryNumber(210, out double x);
            TryNumber(220, out double y);
            TryNumber(230, out double z);
            return Changes(210, new[] { unit.X, unit.Y, unit.Z }, new[] { x, y, z });
        }

        /// <summary>
        /// Sets groups of the type's own part in one writing of the record, each the first of its code, where the class's
        /// <see cref="GroupOrder"/> puts one the record has not; the extents of the drawing grown to hold the entity.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        private protected void SetGroups(params KeyValuePair<int, string>[] groups)
        {
            if (groups.Length > 0)
            {
                Item.File.SetValues(Item, groups, GroupOrder, true);
            }
        }

        /// <summary>How many records the entity is made of: 1, or a POLYLINE's with its vertices and SEQEND, an INSERT's with its attributes and SEQEND.</summary>
        private protected int RecordCount => Item.File.RecordCount(Item);

        /// <summary>The type of a record of the entity: its own (0), or one that follows it, as VERTEX, ATTRIB or SEQEND.</summary>
        private protected string RecordType(int record) => Item.File.RecordType(Item, record);

        /// <summary>
        /// The groups of the own part of a record of the entity (0 its own, 1 on those that follow it), in their order: from its
        /// first subclass marker other than AcDbEntity, the markers among them, or from its start in a file of R12; outside the
        /// groups of the applications and before the extended data. For runs, as the vertices of a LWPOLYLINE.
        /// </summary>
        private protected IReadOnlyList<DxfTypedValue> GetOwnGroups(int record) => Item.File.OwnGroupsOf(Item, record);

        /// <summary>Sets groups of the own part of a record that follows the entity, as <see cref="SetGroups"/> sets its own, by the class's <see cref="GroupOrder"/>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        private protected void SetGroupsAt(int record, IReadOnlyList<int> order, params KeyValuePair<int, string>[] groups)
        {
            if (groups.Length > 0)
            {
                Item.File.SetValuesAt(Item, record, groups, order, true);
            }
        }

        /// <summary>
        /// Replaces every group of some codes in the own part of a record of the entity by the groups given, in one writing:
        /// where the first of those was, or where <see cref="GroupOrder"/> puts the first given where there was none. Nothing is
        /// written where the groups are as they were. For runs, as the vertices of a LWPOLYLINE (10, 20, 40, 41, 42 and 91).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        private protected void ReplaceGroups(int record, IReadOnlyCollection<int> codes, params KeyValuePair<int, string>[] groups)
            => Item.File.ReplaceOwn(Item, record, new HashSet<int>(codes), groups, GroupOrder, true);

        /// <summary>
        /// Writes the records that follow the entity anew, as the vertices of a POLYLINE: each of the type and own groups given
        /// (its subclass markers among them, left out in a file of R12), with a handle of its own, the entity as its owner, its
        /// space and layer; its SEQEND kept.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        private protected void SetFollowers(IReadOnlyList<KeyValuePair<string, KeyValuePair<int, string>[]>> records)
        {
            var followers = new List<KeyValuePair<string, IList<KeyValuePair<int, string>>>>();

            foreach (KeyValuePair<string, KeyValuePair<int, string>[]> record in records)
            {
                followers.Add(new KeyValuePair<string, IList<KeyValuePair<int, string>>>(record.Key, record.Value));
            }

            Item.File.SetFollowers(Item, followers);
        }

        /// <summary>
        /// The object of a record that follows the entity (1 the first, as <see cref="RecordType"/> counts them): made the first
        /// time it is asked for, by the class registered for its type (<see cref="DxfTypeAttribute"/>), whose factory reads the
        /// entity it follows through <c>item.Parent</c>, else a <see cref="DxfUnknownEntity"/>; the same object while the record
        /// is there, however many are put in or taken out around it. Its reads and writes go to that record (its look and
        /// extended data among them), its <see cref="Owner"/> is this entity, and its <see cref="Erase"/> takes that one
        /// record out. Its object is taken out, erased, when <see cref="SetFollowers"/> or <see cref="SetShape"/> writes all
        /// the records anew.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not that of a record that follows the entity.</exception>
        private protected DxfEntity FollowerAt(int record) => Of(Item.File.FollowerOf(Item, record));

        /// <summary>
        /// Puts an entity made in code among the records that follow this one, before the record at an index (the count of the
        /// records to put it last, before the SEQEND where there is one), as AutoCAD's <c>AppendVertex</c> and
        /// <c>InsertVertexAt</c> put a vertex in: its own groups, look and extended data as made, a new handle, this entity as
        /// its owner, this entity's space and layer, its subclass markers left out in a file of R12. Every other record keeps
        /// its handle and bytes, and the object given stands for the new record from then on, as <see cref="FollowerAt"/> gives it.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not from 1 to the place of the SEQEND.</exception>
        /// <exception cref="ArgumentException">Thrown when its look has a lineweight or true colour the file's version has not, or
        /// a linetype its table does not list.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity given is in a file or follows an entity already, or
        /// this one is erased.</exception>
        private protected void InsertFollowerAt(int record, DxfEntity follower)
        {
            if (follower == null)
            {
                throw new ArgumentNullException(nameof(follower));
            }

            if (follower.Item.Parent != null || follower.IsResident || follower.IsErased)
            {
                throw new InvalidOperationException("The " + follower.Item.Type + " is of a file or follows an entity already: put in one made in code.");
            }

            follower.Bind(Item.File.InsertFollower(Item, record, follower.Item));
        }

        /// <summary>
        /// Sets groups of the own parts of several records of the entity in one writing of it, each record's as
        /// <see cref="SetGroupsAt"/> sets one's, by the order given: every vertex of a POLYLINE moved at the cost of one, where
        /// a <see cref="SetGroupsAt"/> for each would write the whole entity once for each. The handles are kept, and nothing is
        /// written where every value is as it was.
        /// </summary>
        /// <param name="records">For each record to change, its index (0 the entity's own) and its groups.</param>
        /// <param name="order">The codes of the records' own groups in the order AutoCAD writes them, as <see cref="GroupOrder"/>.</param>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        private protected void SetGroupsAtMany(IReadOnlyList<KeyValuePair<int, KeyValuePair<int, string>[]>> records, IReadOnlyList<int> order)
        {
            var values = new List<KeyValuePair<int, IList<KeyValuePair<int, string>>>>(records.Count);

            foreach (KeyValuePair<int, KeyValuePair<int, string>[]> record in records)
            {
                values.Add(new KeyValuePair<int, IList<KeyValuePair<int, string>>>(record.Key, record.Value));
            }

            Item.File.SetValuesAtMany(Item, values, order, true);
        }

        /// <summary>The entity this one follows, for a record that follows one (a VERTEX's POLYLINE, an ATTRIB's INSERT); null otherwise.</summary>
        private protected DxfEntity FollowedEntity => Item.Parent == null ? null : Of(Item.Parent);

        /// <summary>Which record of <see cref="FollowedEntity"/> this one is, 1 for the first that follows it, kept as others are put in or taken out; 0 for one that follows none.</summary>
        private protected int FollowerIndex => Item.Parent == null ? 0 : Item.Index;

        /// <summary>
        /// Writes the entity's record anew from a shape, as <see cref="DxfWriter"/> writes the shape: its geometric groups from
        /// the shape, every other group kept as it was and in its order. The shape has to be written as the entity's own type:
        /// a <see cref="DxfLine"/> takes a <see cref="GeoLine3"/>.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the shape is written as another type, or cannot be written.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        private protected void SetShape(object shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            string type = Item.File.TypeWritten(shape);

            if (type != Item.Type)
            {
                throw new ArgumentException("A " + shape.GetType().Name + " is written as a " + type + ", and a " + GetType().Name + " is a " + Item.Type + ".", nameof(shape));
            }

            Item.File.SetShape(Item, shape);
        }

        /// <summary>The thickness (39), nought where the record gives none.</summary>
        private protected double GetThickness() => Item.Thickness;

        /// <summary>Sets the thickness (39) where the type has one, as the file writes it; nought takes it out.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the thickness is not a finite number.</exception>
        private protected void SetThickness(double value)
        {
            Guard.Finite(value, nameof(value), "A thickness has to be a finite number.");

            if (!Same(value, GetThickness()))
            {
                Item.Thickness = value;
            }
        }

        /// <summary>A copy of the entity in no file, of the same class, as <see cref="GetTransformedCopy"/> gives one.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased, as AutoCAD refuses an erased object.</exception>
        private protected DxfEntity Copy()
        {
            if (IsErased)
            {
                throw new InvalidOperationException("The " + Item.Type + " " + (Handle ?? string.Empty) + " is erased, and is not copied.");
            }

            DxfItem copy = Item.File.CopyOf(Item);

            // The copy is of the entity's own class, whatever the classes registered for its type would make of a record that
            // follows no entity: a VERTEX copied is the vertex it was, not an unknown entity.
            return (DxfEntity)copy.KeepWrapper(SameClass(copy) ?? Of(copy));
        }

        private static readonly Dictionary<Type, ConstructorInfo> RecordConstructors = new Dictionary<Type, ConstructorInfo>();

        /// <summary>An object of the entity's own class for a record, through the constructor every class has of a record; null where it has none.</summary>
        private DxfEntity SameClass(DxfItem item)
        {
            Type type = GetType();
            ConstructorInfo constructor;

            lock (RecordConstructors)
            {
                if (!RecordConstructors.TryGetValue(type, out constructor))
                {
                    constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new[] { typeof(DxfItem) }, null);
                    RecordConstructors.Add(type, constructor);
                }
            }

            return constructor == null ? null : (DxfEntity)constructor.Invoke(new object[] { item });
        }

        /// <summary>
        /// The axes of the system of an extrusion by the arbitrary axis algorithm of the DXF reference, as the reader works
        /// them out: x is the world y crossed with the normal where x and y of the normal are both less than 1/64 from nought,
        /// and the world z crossed with it otherwise; y is the normal crossed with x.
        /// </summary>
        private protected static void OcsAxes(GeoVector3 normal, out GeoVector3 u, out GeoVector3 v)
        {
            double nx = normal.X;
            double ny = normal.Y;
            double nz = normal.Z;

            if (nx == 0.0 && ny == 0.0 && nz == 1.0)
            {
                u = new GeoVector3(1.0, 0.0, 0.0);
                v = new GeoVector3(0.0, 1.0, 0.0);
                return;
            }

            bool nearZ = Math.Abs(nx) < 1.0 / 64.0 && Math.Abs(ny) < 1.0 / 64.0;
            double ax = nearZ ? nz : -ny;
            double ay = nearZ ? 0.0 : nx;
            double az = nearZ ? -nx : 0.0;
            double length = Math.Sqrt(ax * ax + ay * ay + az * az);
            u = new GeoVector3(ax / length, ay / length, az / length);

            double bx = ny * u.Z - nz * u.Y;
            double by = nz * u.X - nx * u.Z;
            double bz = nx * u.Y - ny * u.X;
            length = Math.Sqrt(bx * bx + by * by + bz * bz);
            v = new GeoVector3(bx / length, by / length, bz / length);
        }

        /// <summary>A point of the system of an extrusion in the world, as the reader takes one there; the point itself for the extrusion (0, 0, 1).</summary>
        private protected static GeoPoint3 OcsToWorld(GeoVector3 normal, GeoPoint3 point)
        {
            if (normal.X == 0.0 && normal.Y == 0.0 && normal.Z == 1.0)
            {
                return point;
            }

            OcsAxes(normal, out GeoVector3 u, out GeoVector3 v);
            return new GeoPoint3(u.X * point.X + v.X * point.Y + normal.X * point.Z, u.Y * point.X + v.Y * point.Y + normal.Y * point.Z, u.Z * point.X + v.Z * point.Y + normal.Z * point.Z);
        }

        /// <summary>A point of the world in the system of an extrusion; the point itself for the extrusion (0, 0, 1).</summary>
        private protected static GeoPoint3 WorldToOcs(GeoVector3 normal, GeoPoint3 point)
        {
            if (normal.X == 0.0 && normal.Y == 0.0 && normal.Z == 1.0)
            {
                return point;
            }

            OcsAxes(normal, out GeoVector3 u, out GeoVector3 v);
            return new GeoPoint3(u.X * point.X + u.Y * point.Y + u.Z * point.Z, v.X * point.X + v.Y * point.Y + v.Z * point.Z, normal.X * point.X + normal.Y * point.Y + normal.Z * point.Z);
        }

        /// <summary>The groups of several setters together, to be set by one <see cref="SetGroups"/>, in one writing of the record.</summary>
        private protected static KeyValuePair<int, string>[] Join(params KeyValuePair<int, string>[][] parts)
        {
            var all = new List<KeyValuePair<int, string>>();

            foreach (KeyValuePair<int, string>[] part in parts)
            {
                all.AddRange(part);
            }

            return all.ToArray();
        }

        /// <summary>Whether two numbers are the same to the bit.</summary>
        private protected static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        private bool TryNumber(int code, out double value)
        {
            if (Numbers.TryRead(GetString(code), out value) && Guard.IsFinite(value))
            {
                return true;
            }

            value = 0.0;
            return false;
        }

        /// <summary>The groups of a code and those 10 and 20 above it whose numbers differ, to the bit, and those the record leaves out with them.</summary>
        private KeyValuePair<int, string>[] Changes(int code, double[] values, double[] now)
        {
            bool differs = false;

            for (int i = 0; i < values.Length; i++)
            {
                differs |= !Same(values[i], now[i]);
            }

            if (!differs)
            {
                return new KeyValuePair<int, string>[0];
            }

            var changes = new List<KeyValuePair<int, string>>();

            for (int i = 0; i < values.Length; i++)
            {
                int at = code + 10 * i;

                if (!Same(values[i], now[i]) || GetString(at) == null)
                {
                    changes.Add(new KeyValuePair<int, string>(at, Numbers.Exact(values[i] + 0.0)));
                }
            }

            return changes.ToArray();
        }

        #endregion

        /// <summary>
        /// Gives the entity as a line for debugging: its type, its handle and its layer.
        /// </summary>
        /// <returns>The entity as a line, such as <c>LINE 2F on steel</c>.</returns>
        public override string ToString() => Item.Type + (Handle != null ? " " + Handle : string.Empty) + " on " + Layer;
    }
}
