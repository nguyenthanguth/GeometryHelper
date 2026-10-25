using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        /// Gets or sets how much the entity lets show through it (group 440), as AutoCAD's <c>Transparency</c>: BYLAYER where the
        /// record gives none, BYBLOCK, or an alpha from 0, clear, to 255, solid, as <see cref="DxfTransparency"/> says.
        /// </summary>
        /// <value>
        /// Set, BYLAYER takes the group out; BYBLOCK or an alpha writes it, 0x01000000 or 0x020000TT, after the colour where
        /// AutoCAD writes it. The transparency the entity has changes nothing.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when the transparency set is an error value.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased, or a transparency is set on one of a
        /// file older than R2004 (AC1018), which has none.</exception>
        public DxfTransparency Transparency
        {
            get => int.TryParse(Item.File.CommonValueOf(Item, 440)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? DxfTransparency.OfValue(value) : DxfTransparency.ByLayer;

            set
            {
                if (value == Transparency)
                {
                    return;
                }

                if (value.IsInvalid)
                {
                    throw new ArgumentException("A transparency is by layer, by block or by alpha: an error value is not written.", nameof(value));
                }

                Item.File.Requires(1018, "transparency", "R2004 (AC1018)");
                Item.File.SetCommon(Item, 440, value.IsByLayer ? null : value.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Gets the hyperlinks of the entity, as AutoCAD's <c>Hyperlinks</c>: those of its extended data of the application
        /// PE_URL, read and written as <see cref="DxfHyperLinkCollection"/> says.
        /// </summary>
        public DxfHyperLinkCollection Hyperlinks => new DxfHyperLinkCollection(this);

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
        /// it, and so has no extents, as an INSERT of a block the drawing does not define has none. A class of a type the reader
        /// gives no shapes for gives its own, as a HATCH the box of its loops.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the entity has no shape the library reads, as AutoCAD throws
        /// for an entity with no extents: an IMAGE, a LINE of no length, an INSERT that places nothing.</exception>
        public virtual GeoAabb3 GeometricExtents
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
        /// written anew with the new geometry, its look kept, and its extended data moved with it in the same writing, as
        /// <see cref="DxfDBObject.XDataTransformBy"/> moves it: a world position (1011) moved, a displacement (1012) and a
        /// direction (1013) turned, a distance (1041) and a scale factor (1042) scaled. An entity with none of those keeps its
        /// extended data's bytes. Each class moves its own geometry as its page says.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown where the class refuses the transformation, as an INSERT one that would
        /// shear it; nothing is written then.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        /// <exception cref="NotSupportedException">Thrown for an entity whose type the library keeps as it is, a
        /// <see cref="DxfUnknownEntity"/>.</exception>
        public void TransformBy(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            DxfItem item = Item;
            DxfFile file = item.File;
            List<DxfItem.PendingData> pending = null;
            int count = item.Parent == null ? file.RecordCount(item) : 1;

            // A record that follows an entity moved with it has its data moved by that entity, once.
            if (item.Parent == null || !item.Parent.CarryingFollowers)
            {
                foreach (int r in file.WorldDataRecords(item))
                {
                    List<DxfTypedValue> data = TransformedData(file.XDataOf(item, r), transform);

                    if (data != null)
                    {
                        pending = pending ?? new List<DxfItem.PendingData>();
                        pending.Add(new DxfItem.PendingData(r, file.RecordType(item, r), data));
                    }
                }
            }

            item.PendingRecords = pending;
            item.CarryingFollowers = item.Parent == null && count > 1;
            List<DxfItem.PendingData> left;

            try
            {
                TransformEntity(transform);
            }
            finally
            {
                left = item.PendingRecords;
                item.PendingRecords = null;
                item.CarryingFollowers = false;
            }

            // Where the class wrote nothing of a record, its extended data is written alone.
            foreach (DxfItem.PendingData data in left ?? new List<DxfItem.PendingData>())
            {
                if (data.Index == 0)
                {
                    XData = data.Groups;
                }
                else
                {
                    FollowerAt(data.Index).XData = data.Groups;
                }
            }
        }

        /// <summary>
        /// Moves the entity's geometry by a transformation, as the class's page says, in one writing of its records where it
        /// can: <see cref="TransformBy"/> gives the extended data moved to that writing.
        /// </summary>
        /// <param name="transform">The transformation, not null.</param>
        private protected abstract void TransformEntity(GeoTransform3 transform);

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
        /// Gets the entities the entity breaks into, as AutoCAD's <c>Explode</c>: new entities in no file. A
        /// <see cref="DxfBlockReference"/> gives the entities of its block placed where it puts them, a MINSERT those of every cell;
        /// a polyline its lines and arcs; an MTEXT a TEXT for each line; a dimension and a table the entities of the block they are
        /// drawn with, placed; a straight LEADER its lines and arrowhead. Any other entity refuses, as AutoCAD answers
        /// <c>eNotApplicable</c> for one that does not break: a HATCH, a MESH, a polyface or polygon mesh and an MLINE among them,
        /// which AutoCAD explodes, and a MULTILEADER, whose parts AutoCAD works out.
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

        #region Planes and crossings

        private static readonly Dictionary<Tuple<Type, Type>, MethodInfo> Crossings = new Dictionary<Tuple<Type, Type>, MethodInfo>();

        /// <summary>
        /// Gets whether the entity lies in one plane, as AutoCAD's <c>IsPlanar</c>: whether <see cref="GetPlane"/> finds one, to
        /// <see cref="Tolerance.Default"/>. A line, and a polyline whose vertices run along one line, lie in one; a 3D polyline
        /// with a corner 0.002 off the plane of the others does not. A <see cref="DxfSpline"/> gives what its record says, the
        /// flag 8 of its group 70.
        /// </summary>
        public virtual bool IsPlanar => TryGetPlane(out _);

        /// <summary>
        /// Gets the plane the entity lies in, as AutoCAD's <c>GetPlane</c>: that of its extrusion through its first point where
        /// all of it lies in that, as a circle, an arc, a polyline or a text does; else the plane its shapes span, as a 3DFACE's
        /// or a SPLINE's. An entity along one line, as a LINE, lies in the plane through it whose normal is nearest its
        /// extrusion, the extrusion itself where the line runs across it; a point, in the plane of its extrusion. Points and planes are compared to
        /// <see cref="Tolerance.Default"/>. A class of a type the reader gives no shapes for gives its own, as a HATCH the plane of
        /// its extrusion.
        /// </summary>
        /// <returns>The plane, its origin the entity's first point.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the entity does not lie in one plane, as AutoCAD answers
        /// eNotPlanar, or has no shape the library reads.</exception>
        public virtual GeoPlane3 GetPlane()
        {
            if (!TryGetPlane(out GeoPlane3 plane))
            {
                throw new InvalidOperationException("The " + Item.Type + " " + (Handle ?? string.Empty) + " does not lie in one plane, or has no shape the library reads, as AutoCAD answers eNotPlanar.");
            }

            return plane;
        }

        /// <summary>
        /// Gets the points where the entity and another meet, as AutoCAD's <c>IntersectWith</c>: worked out by the library's
        /// shapes for both (<see cref="GetGeometry"/>), each pair of a shape of this entity and one of the other, to
        /// <see cref="Tolerance.Default"/>. As AutoCAD's, it meets curves and edges, not areas: a closed polyline, a SOLID, a
        /// 3DFACE and a mesh by the segments and arcs of their edges, so that the square of side 4 about the origin meets the line
        /// from (-10, 0, 0) to (10, 0, 0) at (-2, 0, 0) and (2, 0, 0), and the line through its middle up the z axis nowhere. A
        /// chain the library has no intersection of with a shape is taken segment by segment and arc by arc, and a POINT meets
        /// a shape where it lies on it. An entity that is to run on past its ends, as the type of intersection says, is taken as
        /// its line or circle where it is a LINE or an ARC; any other is taken as it is.
        /// </summary>
        /// <param name="entityPointer">The other entity.</param>
        /// <param name="intersectType">Which entity runs on past its ends.</param>
        /// <returns>
        /// The points, each once, in the order they are found: the line from (0, 0, 0) to (10, 0, 0) meets the circle of radius 3
        /// about (10, 0, 0) at (7, 0, 0), and extended (<see cref="DxfIntersect.ExtendThis"/>) at (13, 0, 0) too.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not one of <see cref="DxfIntersect"/>.</exception>
        /// <exception cref="NotSupportedException">Thrown for a pair of shapes the library has no intersection of, as a text and
        /// a line, rather than giving none.</exception>
        public IReadOnlyList<GeoPoint3> IntersectWith(DxfEntity entityPointer, DxfIntersect intersectType)
        {
            if (entityPointer == null)
            {
                throw new ArgumentNullException(nameof(entityPointer));
            }

            if (intersectType < DxfIntersect.OnBothOperands || intersectType > DxfIntersect.ExtendBoth)
            {
                throw new ArgumentOutOfRangeException(nameof(intersectType), intersectType, "The type of intersection is one of DxfIntersect.");
            }

            bool extendThis = intersectType == DxfIntersect.ExtendThis || intersectType == DxfIntersect.ExtendBoth;
            bool extendOther = intersectType == DxfIntersect.ExtendArgument || intersectType == DxfIntersect.ExtendBoth;
            Tolerance tolerance = Tolerance.Default;
            var points = new List<GeoPoint3>();

            List<object> ours = GetGeometry().Select(s => extendThis ? Extended(s) : s).SelectMany(Outline).ToList();
            List<object> others = entityPointer.GetGeometry().Select(s => extendOther ? Extended(s) : s).SelectMany(Outline).ToList();

            foreach (object mine in ours)
            {
                foreach (object theirs in others)
                {
                    foreach (GeoPoint3 point in Meeting(mine, theirs, tolerance))
                    {
                        AddOnce(points, point, tolerance);
                    }
                }
            }

            return points.AsReadOnly();
        }

        /// <summary>
        /// Gets the points where the boxes of the entity and another meet, as AutoCAD's <c>BoundingBoxIntersectWith</c>: where
        /// an edge of the one box of <see cref="GeometricExtents"/> enters or leaves the other box, each way, to
        /// <see cref="Tolerance.Default"/>. Two flat boxes in one plane meet where their outlines cross: the box of the line from
        /// (0, 0, 0) to (10, 10, 0) and that of the line from (5, 5, 0) to (15, 15, 0) meet at (10, 5, 0) and (5, 10, 0). A box
        /// is never extended, whatever the type of intersection.
        /// </summary>
        /// <param name="entityPointer">The other entity.</param>
        /// <param name="intersectType">The type of intersection, as AutoCAD takes it; a box does not run on.</param>
        /// <returns>The points, each once, in the order they are found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not one of <see cref="DxfIntersect"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when either entity has no extents, as <see cref="GeometricExtents"/> says.</exception>
        public IReadOnlyList<GeoPoint3> BoundingBoxIntersectWith(DxfEntity entityPointer, DxfIntersect intersectType)
        {
            if (entityPointer == null)
            {
                throw new ArgumentNullException(nameof(entityPointer));
            }

            if (intersectType < DxfIntersect.OnBothOperands || intersectType > DxfIntersect.ExtendBoth)
            {
                throw new ArgumentOutOfRangeException(nameof(intersectType), intersectType, "The type of intersection is one of DxfIntersect.");
            }

            GeoAabb3 mine = GeometricExtents;
            GeoAabb3 theirs = entityPointer.GeometricExtents;
            Tolerance tolerance = Tolerance.Default;
            var points = new List<GeoPoint3>();

            foreach (KeyValuePair<GeoAabb3, GeoAabb3> pair in new[] { new KeyValuePair<GeoAabb3, GeoAabb3>(mine, theirs), new KeyValuePair<GeoAabb3, GeoAabb3>(theirs, mine) })
            {
                foreach (GeoLine3 edge in Edges(pair.Key, tolerance))
                {
                    foreach (GeoPoint3 point in pair.Value.GetIntersections(edge, tolerance))
                    {
                        AddOnce(points, point, tolerance);
                    }
                }
            }

            return points.AsReadOnly();
        }

        /// <summary>The plane of the entity, as <see cref="GetPlane"/> finds it; false where it lies in none.</summary>
        private bool TryGetPlane(out GeoPlane3 plane)
        {
            plane = default;
            Tolerance tolerance = Tolerance.Default;
            var points = new List<GeoPoint3>();
            var normals = new List<GeoVector3>();

            foreach (object shape in GetGeometry())
            {
                if (!Collect(shape, points, normals, tolerance))
                {
                    return false;
                }
            }

            if (points.Count == 0)
            {
                return false;
            }

            GeoPoint3 first = points[0];
            GeoVector3 own = GetNormal();

            // The extrusion first, as AutoCAD gives a circle's or a polyline's plane, then the plane a shape has of its own.
            var tried = new List<GeoVector3> { own };
            tried.AddRange(normals);

            foreach (GeoVector3 normal in tried)
            {
                if (Holds(new GeoPlane3(first, normal), points, normals, tolerance))
                {
                    plane = new GeoPlane3(first, normal);
                    return true;
                }
            }

            GeoPoint3 far = first;
            double farthest = 0.0;

            foreach (GeoPoint3 point in points)
            {
                double distance = first.DistanceTo(point);

                if (distance > farthest)
                {
                    farthest = distance;
                    far = point;
                }
            }

            if (farthest <= tolerance.EqualPoint)
            {
                plane = new GeoPlane3(first, own);
                return true;
            }

            GeoVector3 along = first.GetVectorTo(far).Divide(farthest);
            GeoPoint3 wide = first;
            double widest = 0.0;

            foreach (GeoPoint3 point in points)
            {
                double distance = first.GetVectorTo(point).CrossProduct(along).Length;

                if (distance > widest)
                {
                    widest = distance;
                    wide = point;
                }
            }

            if (widest <= tolerance.EqualPoint)
            {
                // Along one line: the plane through it whose normal is nearest the extrusion.
                GeoVector3 across = own.Subtract(along.Multiply(own.DotProduct(along)));

                if (!across.TryGetNormal(out GeoVector3 normal, tolerance))
                {
                    OcsAxes(along, out GeoVector3 u, out _);
                    normal = u;
                }

                plane = new GeoPlane3(first, normal);
                return true;
            }

            GeoVector3 spanned = first.GetVectorTo(far).CrossProduct(first.GetVectorTo(wide));

            if (!spanned.TryGetNormal(out GeoVector3 unit, tolerance) || !Holds(new GeoPlane3(first, unit), points, normals, tolerance))
            {
                return false;
            }

            plane = new GeoPlane3(first, unit);
            return true;
        }

        /// <summary>Whether a plane holds every point and is the plane of every shape that has one of its own.</summary>
        private static bool Holds(GeoPlane3 plane, List<GeoPoint3> points, List<GeoVector3> normals, Tolerance tolerance)
            => plane.ContainsAll(points, tolerance) && normals.TrueForAll(n => n.IsParallelTo(plane.Normal, tolerance));

        /// <summary>
        /// The points of a shape that fix where it lies, and the normal of its own plane where it has one: a circle's centre and
        /// normal, a polyline's vertices; false for a shape of which the library knows no plane, as a body.
        /// </summary>
        private static bool Collect(object shape, List<GeoPoint3> points, List<GeoVector3> normals, Tolerance tolerance)
        {
            switch (shape)
            {
                case GeoPoint3 s: points.Add(s); return true;
                case GeoLine3 s: points.Add(s.StartPoint); points.Add(s.EndPoint); return true;
                case GeoRay3 s: points.Add(s.Origin); points.Add(s.Origin.Add(s.Direction)); return true;
                case GeoXLine3 s: points.Add(s.Origin); points.Add(s.Origin.Add(s.Direction)); return true;
                case GeoCircle3 s: points.Add(s.Center); normals.Add(s.Normal); return true;
                case GeoArc3 s: points.Add(s.Center); points.Add(s.StartPoint); normals.Add(s.Normal); return true;
                case GeoEllipse3 s: points.Add(s.Center); normals.Add(s.Normal); return true;
                case GeoTriangle3 s: points.Add(s.A); points.Add(s.B); points.Add(s.C); return true;
                case GeoPolygon3 s: points.AddRange(s.Vertices); normals.Add(s.Normal); return true;
                case GeoPolygonArc3 s: points.AddRange(s.Vertices); normals.Add(s.Normal); return true;
                case GeoPolyline3 s: points.AddRange(s.Vertices); return true;
                case GeoPolylineArc3 s:
                    points.AddRange(s.Vertices);

                    if (s.TryGetPlane(out GeoPlane3 chain, tolerance))
                    {
                        normals.Add(chain.Normal);
                        return true;
                    }

                    // A chain the library gives no plane is straight along one line, or does not lie in one.
                    return AlongOneLine(s.Vertices, tolerance);
                case GeoSpline3 s: points.AddRange(s.ControlPoints); return true;
                case DxfText s: points.Add(s.Position); normals.Add(s.Normal); return true;
                default: return false;
            }
        }

        /// <summary>Whether points all lie along one line, or at one point.</summary>
        private static bool AlongOneLine(IReadOnlyList<GeoPoint3> points, Tolerance tolerance)
        {
            GeoPoint3 first = points[0];
            GeoVector3 along = default;
            bool found = false;

            foreach (GeoPoint3 point in points)
            {
                GeoVector3 to = first.GetVectorTo(point);

                if (!found)
                {
                    found = to.TryGetNormal(out along, tolerance) && to.Length > tolerance.EqualPoint;
                    continue;
                }

                if (to.CrossProduct(along).Length > tolerance.EqualPoint)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A shape as it runs on past its ends: a segment as its infinite line, an arc as its circle; any other as it is.</summary>
        private static object Extended(object shape)
        {
            switch (shape)
            {
                case GeoLine3 s when s.StartPoint.DistanceTo(s.EndPoint) > 0.0: return new GeoXLine3(s.StartPoint, s.Direction);
                case GeoArc3 s: return new GeoCircle3(s.Center, s.Normal, s.Radius);
                default: return shape;
            }
        }

        /// <summary>
        /// The curves a shape is drawn with, as AutoCAD intersects an entity by its curves and edges: a closed polygon, a
        /// face, a triangle or a body by the segments and arcs of its edges, any other shape as it is.
        /// </summary>
        private static IEnumerable<object> Outline(object shape)
        {
            switch (shape)
            {
                case GeoPolygon3 s: return Segments(s.GetEdges());
                case GeoPolygonArc3 s: return EdgePieces(s.GetEdges());
                case GeoTriangle3 s: return Segments(new[] { new GeoLine3(s.A, s.B), new GeoLine3(s.B, s.C), new GeoLine3(s.C, s.A) });
                case GeoFace3 s: return FaceEdges(s);
                case GeoSolid3 s: return s.Faces.SelectMany(FaceEdges);
                default: return new[] { shape };
            }
        }

        /// <summary>The pieces of a chain the library has no intersection of with a shape, to be intersected one by one; null for a shape that is no chain.</summary>
        private static IEnumerable<object> Pieces(object shape)
        {
            switch (shape)
            {
                case GeoPolyline3 s: return Segments(s.GetEdges());
                case GeoPolylineArc3 s: return EdgePieces(s.GetEdges());
                default: return null;
            }
        }

        private static IEnumerable<object> FaceEdges(GeoFace3 face)
            => Segments(face.Boundary.GetEdges()).Concat(face.Holes.SelectMany(h => Segments(h.GetEdges())));

        private static IEnumerable<object> Segments(IEnumerable<GeoLine3> lines)
            => lines.Where(l => l.StartPoint.DistanceTo(l.EndPoint) > 0.0).Cast<object>();

        private static IEnumerable<object> EdgePieces(IEnumerable<GeoEdge3> edges)
            => edges.Select(e => e.IsArc ? (object)e.ToArc() : e.ToLine()).Where(p => !(p is GeoLine3 l) || l.StartPoint.DistanceTo(l.EndPoint) > 0.0);

        /// <summary>
        /// Where two shapes meet: by the library's <c>GetIntersections(other, tolerance)</c> of the one or of the other, whichever
        /// class has it; else piece by piece where one is a chain; else, for a point, the point where it lies on the other.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown for a pair the library has no intersection of, as a text and a line.</exception>
        private static IEnumerable<GeoPoint3> Meeting(object a, object b, Tolerance tolerance)
        {
            MethodInfo method = CrossingOf(a.GetType(), b.GetType());

            if (method != null)
            {
                return (GeoPoint3[])method.Invoke(a, new[] { b, tolerance });
            }

            method = CrossingOf(b.GetType(), a.GetType());

            if (method != null)
            {
                return (GeoPoint3[])method.Invoke(b, new[] { a, tolerance });
            }

            IEnumerable<object> pieces = Pieces(a);

            if (pieces != null)
            {
                return pieces.SelectMany(p => Meeting(p, b, tolerance)).ToList();
            }

            pieces = Pieces(b);

            if (pieces != null)
            {
                return pieces.SelectMany(p => Meeting(a, p, tolerance)).ToList();
            }

            if (a is GeoPoint3 point)
            {
                return OnShape(point, b, tolerance);
            }

            if (b is GeoPoint3 other)
            {
                return OnShape(other, a, tolerance);
            }

            throw new NotSupportedException("The library has no intersection of a " + a.GetType().Name + " and a " + b.GetType().Name + ".");
        }

        /// <summary>A point where it lies on a shape, within the tolerance: by the shape's <c>DistanceTo(GeoPoint3)</c>; none where it lies off it.</summary>
        /// <exception cref="NotSupportedException">Thrown for a shape the library measures no distance to, as a text.</exception>
        private static IEnumerable<GeoPoint3> OnShape(GeoPoint3 point, object shape, Tolerance tolerance)
        {
            MethodInfo distance = shape.GetType().GetMethod("DistanceTo", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(GeoPoint3) }, null);

            if (distance == null || distance.ReturnType != typeof(double))
            {
                throw new NotSupportedException("The library measures no distance from a point to a " + shape.GetType().Name + ".");
            }

            return (double)distance.Invoke(shape, new object[] { point }) <= tolerance.EqualPoint ? new[] { point } : new GeoPoint3[0];
        }

        /// <summary>The method <c>GeoPoint3[] GetIntersections(other, Tolerance)</c> of a class of shape for another, found once; null where it has none.</summary>
        private static MethodInfo CrossingOf(Type type, Type other)
        {
            var key = Tuple.Create(type, other);

            lock (Crossings)
            {
                if (!Crossings.TryGetValue(key, out MethodInfo method))
                {
                    method = type.GetMethod("GetIntersections", BindingFlags.Instance | BindingFlags.Public, null, new[] { other, typeof(Tolerance) }, null);
                    method = method != null && method.ReturnType == typeof(GeoPoint3[]) ? method : null;
                    Crossings.Add(key, method);
                }

                return method;
            }
        }

        /// <summary>A point added where none of those found is within the tolerance of it.</summary>
        private static void AddOnce(List<GeoPoint3> points, GeoPoint3 point, Tolerance tolerance)
        {
            if (!points.Exists(p => p.IsEqualTo(point, tolerance)))
            {
                points.Add(point);
            }
        }

        /// <summary>The edges of a box longer than the tolerance: twelve, fewer for a box flat along an axis.</summary>
        private static IEnumerable<GeoLine3> Edges(GeoAabb3 box, Tolerance tolerance)
        {
            GeoPoint3 a = box.Min;
            GeoPoint3 b = box.Max;
            var corners = new GeoPoint3[8];

            for (int i = 0; i < 8; i++)
            {
                corners[i] = new GeoPoint3((i & 1) == 0 ? a.X : b.X, (i & 2) == 0 ? a.Y : b.Y, (i & 4) == 0 ? a.Z : b.Z);
            }

            for (int i = 0; i < 8; i++)
            {
                for (int bit = 1; bit < 8; bit <<= 1)
                {
                    int j = i | bit;

                    if (j != i && corners[i].DistanceTo(corners[j]) > tolerance.EqualPoint)
                    {
                        yield return new GeoLine3(corners[i], corners[j]);
                    }
                }
            }
        }

        #endregion

        #region Erasing

        /// <summary>
        /// Erases the entity from its block, as AutoCAD's <c>Erase</c> does: every record of it goes, a POLYLINE's vertices
        /// and end and an INSERT's attributes with it, its handle is not given again, and what pointed at it is seen to when the
        /// file is saved: a GROUP no longer lists it, a list of reactors no longer holds it, and the objects it owned, such as
        /// its extension dictionary, go with it; any other pointer at it, as a table cell's at an attribute definition (331) or
        /// an XRECORD's data, stays where it stands, named in the log once. A record that follows an entity, a VERTEX of a
        /// POLYLINE or an ATTRIB of an INSERT, is taken out of it alone, every other record keeping its handle.
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

        /// <summary>
        /// A part of the entity exploded, a shape made an entity in no file as <see cref="DxfWriter"/> writes it, with the entity's
        /// look (layer, colour, true colour, linetype and its scale, lineweight, visibility) and, a LINE or an ARC, its thickness.
        /// </summary>
        private protected DxfEntity PartOf(object shape)
        {
            DxfEntity made = Of(DxfFile.Detached(shape));
            GiveLook(made);
            double thickness = GetThickness();

            if (!Same(thickness, 0.0) && (made.Item.Type == "LINE" || made.Item.Type == "ARC"))
            {
                made.Item.Thickness = thickness;
            }

            return made;
        }

        /// <summary>Gives a part made in no file the entity's look: its layer, colour, true colour, linetype and its scale, lineweight and visibility.</summary>
        private protected void GiveLook(DxfEntity made)
        {
            made.Layer = Layer;
            made.ColorIndex = ColorIndex;
            made.TrueColor = TrueColor;
            made.Linetype = Linetype;
            made.LinetypeScale = LinetypeScale;
            made.LineWeight = LineWeight;
            made.Visible = Visible;
        }

        /// <summary>
        /// The parts of a chain the entity is read as (a polyline): a LINE or an ARC for each edge of each of its shapes, in their
        /// order, an edge of no length left out, as AutoCAD's <c>Explode</c> gives a polyline's. Given the normal of the
        /// polyline's plane, each LINE and ARC is on it, as AutoCAD gives them its 210, so that their walls stand up the way
        /// the polyline's do: an ARC about it, its angles in that normal's own system, from the end of its edge where the edge
        /// runs clockwise about it, as ezdxf's <c>virtual_entities</c> gives it. Given the polyline's vertices in the world, two
        /// arcs that meet at a point no vertex stands on are one segment, one ARC: a closed polyline of two vertices, which
        /// <see cref="DxfReader"/> reads with its arc cut in two, explodes into one part for each segment, a donut into two
        /// half circles.
        /// </summary>
        private protected List<DxfEntity> ChainParts(GeoVector3? normal, IReadOnlyList<GeoPoint3> vertices)
        {
            var parts = new List<DxfEntity>();
            double tolerance = Tolerance.Default.EqualPoint;

            bool OnVertex(GeoPoint3 point)
            {
                foreach (GeoPoint3 vertex in vertices)
                {
                    if (vertex.DistanceTo(point) <= tolerance)
                    {
                        return true;
                    }
                }

                return false;
            }

            void Edges(IEnumerable<GeoEdge3> chain)
            {
                List<GeoEdge3> edges = chain.ToList();

                for (int i = 0; i < edges.Count; i++)
                {
                    GeoEdge3 edge = edges[i];

                    if (vertices != null && edge.IsArc && i + 1 < edges.Count && edges[i + 1].IsArc && !OnVertex(edge.EndPoint))
                    {
                        // The two halves of one arc, cut where no vertex is: one ARC from the start of the first to the end of the second.
                        GeoArc3 first = edge.ToArc();
                        parts.Add(ArcOn(first.Center, first.Normal, first.Radius, edge.StartPoint, edges[i + 1].EndPoint, normal ?? first.Normal));
                        i++;
                    }
                    else if (edge.StartPoint.DistanceTo(edge.EndPoint) > tolerance)
                    {
                        parts.Add(PartOn(edge.IsArc ? (object)edge.ToArc() : edge.ToLine(), normal));
                    }
                }
            }

            void Line(GeoLine3 line)
            {
                if (line.Length > tolerance)
                {
                    parts.Add(PartOn(line, normal));
                }
            }

            foreach (object shape in Item.Shapes)
            {
                switch (shape)
                {
                    case GeoPolylineArc3 chain:
                        Edges(chain.GetEdges());
                        break;
                    case GeoPolygonArc3 ring:
                        Edges(ring.GetEdges());
                        break;
                    case GeoPolyline3 lines:
                        foreach (GeoLine3 line in lines.GetEdges())
                        {
                            Line(line);
                        }

                        break;
                    case GeoPolygon3 polygon:
                        foreach (GeoLine3 line in polygon.GetEdges())
                        {
                            Line(line);
                        }

                        break;
                    case GeoLine3 line:
                        Line(line);
                        break;
                    case GeoArc3 _:
                    case GeoCircle3 _:
                        parts.Add(PartOn(shape, normal));
                        break;
                }
            }

            return parts;
        }

        /// <summary>
        /// A part of <see cref="ChainParts"/> (<see cref="PartOf"/>) on the normal of the polyline's plane: a LINE given it, an
        /// ARC made about it, its ends kept, from its end where it runs the other way about; any other part, or one of no normal
        /// given, as <see cref="PartOf"/> makes it.
        /// </summary>
        private DxfEntity PartOn(object shape, GeoVector3? normal)
        {
            if (normal == null)
            {
                return PartOf(shape);
            }

            GeoVector3 up = normal.Value;

            // An arc off the world's plane is made on the normal at once, rather than made and made again.
            if (shape is GeoArc3 bulge && !(up.X == 0.0 && up.Y == 0.0 && up.Z == 1.0))
            {
                return ArcOn(bulge.Center, bulge.Normal, bulge.Radius, bulge.StartPoint, bulge.EndPoint, up);
            }

            DxfEntity part = PartOf(shape);

            switch (part)
            {
                case DxfLine line:
                    line.Normal = up;
                    return part;
                case DxfArc arc when !(Same(arc.Normal.X, up.X) && Same(arc.Normal.Y, up.Y) && Same(arc.Normal.Z, up.Z)):
                    return ArcOn(arc.Center, arc.Normal, arc.Radius, arc.StartPoint, arc.EndPoint, up);
                default:
                    return part;
            }
        }

        /// <summary>
        /// An ARC in no file about a centre on a normal (<paramref name="up"/>), with the entity's look and thickness, from one
        /// end to the other counter-clockwise about it: from its start where it ran that way about its own normal, else from its
        /// end; its angles in the system of the normal.
        /// </summary>
        private DxfEntity ArcOn(GeoPoint3 center, GeoVector3 own, double radius, GeoPoint3 start, GeoPoint3 end, GeoVector3 up)
        {
            bool back = own.DotProduct(up) < 0.0;
            GeoPoint3 middle = WorldToOcs(up, center);
            GeoPoint3 from = WorldToOcs(up, back ? end : start);
            GeoPoint3 to = WorldToOcs(up, back ? start : end);
            var arc = new DxfArc(center, up, radius, Math.Atan2(from.Y - middle.Y, from.X - middle.X), Math.Atan2(to.Y - middle.Y, to.X - middle.X));
            GiveLook(arc);
            arc.Thickness = GetThickness();
            return arc;
        }

        /// <summary>
        /// Copies of the entities of a block placed by a transformation, as the parts of an entity drawn by the block (a
        /// dimension's <c>*D</c>, a table's <c>*T</c>) exploded: each moved as <see cref="GetTransformedCopy"/> moves it; one its
        /// class cannot hold so placed, or of a type the library keeps as it is, left out, said in the log at Debug.
        /// </summary>
        private protected List<DxfEntity> PlacedCopies(DxfBlockTableRecord block, GeoTransform3 placing)
        {
            var parts = new List<DxfEntity>();
            int left = 0;

            foreach (DxfEntity entity in block)
            {
                try
                {
                    DxfEntity copy = entity.GetTransformedCopy(placing);
                    (copy as DxfMText)?.Unlink();
                    parts.Add(copy);
                }
                catch (Exception e) when (e is ArgumentException || e is NotSupportedException)
                {
                    left++;
                }
            }

            if (left > 0)
            {
                GeometryHelperLog.Debug("DxfEntity: " + left.ToString(CultureInfo.InvariantCulture) + " entities of the block " + block.Name + " could not be placed, and were left out of the " + Item.Type + " exploded.");
            }

            return parts;
        }

        /// <summary>
        /// The text style of a name of the entity's drawing, letters of either case the same: of the drawing it is in, or of the
        /// one a copy in no file was made from; null where that drawing has none of the name, or there is no drawing.
        /// </summary>
        private protected DxfTextStyleTableRecord TextStyleNamed(string name)
        {
            DxfDatabase database = (IsResident ? Item.File : Item.File.Origin)?.Database;
            return database != null && database.TextStyleTable.Has(name) ? database.TextStyleTable[name] : null;
        }

        /// <summary>
        /// The name a text style set by its name is written by (group 7): the name the drawing's table gives the style, for an
        /// entity of a drawing; the name given, for one in no drawing, which <see cref="DxfBlockTableRecord.AppendEntity"/> checks.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the name is one AutoCAD does not take, or the entity's drawing has no
        /// text style of it.</exception>
        private protected string TextStyleToWrite(string name, string parameter)
        {
            DxfSymbolTable.CheckName(name, parameter);

            if (!IsResident)
            {
                return name;
            }

            DxfTextStyleTableRecord style = TextStyleNamed(name) ?? throw new ArgumentException("The drawing has no text style named " + name + ".", parameter);
            return style.Name;
        }

        /// <summary>The name of a text style set as its record: one of a drawing, not erased, of the entity's own where it is in one.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the record is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the record is erased, in no drawing, or of another drawing than the entity's.</exception>
        private protected string TextStyleOf(DxfTextStyleTableRecord style, string parameter)
        {
            if (style == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (style.IsErased || !style.IsResident)
            {
                throw new ArgumentException("The text style " + style.Name + " is erased or in no drawing: give a style of a drawing's text style table.", parameter);
            }

            if (IsResident && style.Item.File != Item.File)
            {
                throw new ArgumentException("The text style " + style.Name + " is of another drawing than the " + Item.Type + ".", parameter);
            }

            return style.Name;
        }

        /// <summary>
        /// Refuses a text style (group 7, as written) the text style table of a drawing an entity is added to does not list,
        /// unless the entity is a copy of another drawing (<paramref name="origin"/>) that has it, which is copied with it;
        /// nothing for no style or no drawing.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the drawing has no text style of the name, nor is given it.</exception>
        private protected static void CheckTextStyle(DxfFile file, DxfFile origin, string style, string parameter)
        {
            if (style == null || file == null)
            {
                return;
            }

            string name = DxfReader.NameOf(style);

            foreach (DxfItem item in file.TableItems("STYLE"))
            {
                if (string.Equals(DxfReader.NameOf(item.File.ValueOf(item, 2) ?? string.Empty), name, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            // A copy of another drawing takes the style with it where that drawing has it, as AutoCAD's deep clone does.
            if (file.CopiesRecordFrom(origin, "STYLE", name))
            {
                return;
            }

            throw new ArgumentException("The text style " + name + " is not in the file's text style table.", parameter);
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
            var made = (DxfEntity)copy.KeepWrapper(SameClass(copy) ?? Of(copy));
            made.Copied(this);
            return made;
        }

        /// <summary>
        /// Gives a copy what the entity holds outside its records, as a dimension in no drawing the records set on it, which it
        /// takes to their namesakes when it is appended; nothing unless the class says otherwise.
        /// </summary>
        private protected virtual void Copied(DxfEntity original)
        {
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
