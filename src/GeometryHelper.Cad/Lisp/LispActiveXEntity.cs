using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The entities of the object model: the value of an entity and its interface (D212), what every record, block and entity
    /// has (<c>Handle</c>, <c>ObjectName</c>, <c>ObjectID</c>, <c>OwnerID</c>), what every entity has (layer, color, linetype,
    /// scale, lineweight, visibility, true color, <c>Delete</c>, <c>GetBoundingBox</c>), and the change of an entity through the
    /// session as <c>entmod</c> makes one, in the undo step of the evaluation (D216, D217, D218, D223, D226).
    /// </summary>
    internal sealed partial class LispActiveX
    {
        #region Values of entities

        /// <summary>The value of an object of a handle: an entity, or the record or block that has the handle; an entity erased is still given one (its use is the error).</summary>
        internal LispVlaObject Wrap(string handle)
        {
            string now = State.Resolve(handle);
            DxfItem item = LispDrawing.FindItem(Document, now);

            if (item == null)
            {
                return null;
            }

            // The BLOCK that opens a block definition, which tblobjname names, stands for the block.
            if (item.Type == "BLOCK")
            {
                foreach (DxfTypedValue group in item.Groups)
                {
                    if (group.TypeCode == 2 && Document.Database.BlockTable.Has(group.Value.Trim()))
                    {
                        return WrapRecord(Document.Database.BlockTable[group.Value.Trim()]);
                    }
                }
            }

            switch (item.Type)
            {
                case "BLOCK_RECORD":
                case "LAYER":
                case "STYLE":
                case "LTYPE":
                case "DIMSTYLE":
                    DxfSymbolTableRecord record = FindRecord(now);

                    if (record != null)
                    {
                        return WrapRecord(record);
                    }

                    break;
            }

            return WrapEntity(item);
        }

        /// <summary>The value of an entity of a record, with the interface its type has (D212).</summary>
        internal LispVlaObject WrapEntity(DxfItem item) => Make(LispVlaKind.Entity, InterfaceOf(item), item.Handle);

        /// <summary>The interface an entity has by its DXF type (D212); the dimensions by their kind, any entity of no interface of its own IAcadEntity.</summary>
        internal static string InterfaceOf(DxfItem item)
        {
            switch ((item.Type ?? "").ToUpperInvariant())
            {
                case "LINE":
                    return "IAcadLine";
                case "CIRCLE":
                    return "IAcadCircle";
                case "ARC":
                    return "IAcadArc";
                case "ELLIPSE":
                    return "IAcadEllipse";
                case "LWPOLYLINE":
                    return "IAcadLWPolyline";
                case "POLYLINE":
                    return DxfEntity.Of(item) is DxfPolyline3d ? "IAcad3DPolyline" : "IAcadPolyline";
                case "SPLINE":
                    return "IAcadSpline";
                case "POINT":
                    return "IAcadPoint";
                case "TEXT":
                    return "IAcadText";
                case "MTEXT":
                    return "IAcadMText";
                case "INSERT":
                    return DxfEntity.Of(item) is DxfMInsertBlock ? "IAcadMInsertBlock" : "IAcadBlockReference";
                case "ATTRIB":
                    return "IAcadAttributeReference";
                case "ATTDEF":
                    return "IAcadAttribute";
                case "HATCH":
                    return "IAcadHatch";
                case "DIMENSION":
                    return DimensionInterface(DxfEntity.Of(item));
                case "LEADER":
                    return "IAcadLeader";
                case "MULTILEADER":
                    return "IAcadMLeader";
                case "SOLID":
                    return "IAcadSolid";
                case "3DFACE":
                    return "IAcad3DFace";
                case "XLINE":
                    return "IAcadXline";
                case "RAY":
                    return "IAcadRay";
                case "TABLE":
                    return "IAcadTable";
                case "VIEWPORT":
                    return "IAcadPViewport";
                case "IMAGE":
                    return "IAcadRasterImage";
                case "WIPEOUT":
                    return "IAcadWipeout";
                case "REGION":
                    return "IAcadRegion";
                case "MLINE":
                    return "IAcadMLine";
                case "TOLERANCE":
                    return "IAcadTolerance";
                case "OLE2FRAME":
                    return "IAcadOle";
                case "SHAPE":
                    return "IAcadShape";
                case "TRACE":
                    return "IAcadTrace";
                default:
                    return "IAcadEntity";
            }
        }

        private static string DimensionInterface(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfRotatedDimension _:
                    return "IAcadDimRotated";
                case DxfAlignedDimension _:
                    return "IAcadDimAligned";
                case DxfLineAngularDimension2 _:
                    return "IAcadDimAngular";
                case DxfPoint3AngularDimension _:
                    return "IAcadDim3PointAngular";
                case DxfRadialDimension _:
                    return "IAcadDimRadial";
                case DxfDiametricDimension _:
                    return "IAcadDimDiametric";
                case DxfOrdinateDimension _:
                    return "IAcadDimOrdinate";
                case DxfArcDimension _:
                    return "IAcadDimArcLength";
                case DxfRadialDimensionLarge _:
                    return "IAcadDimRadialLarge";
                default:
                    return "IAcadEntity";
            }
        }

        /// <summary>The record of the entity a value stands for, by the handle it has now; null where it is erased.</summary>
        internal DxfItem FindItemOf(LispVlaObject target) => LispDrawing.FindItem(Document, State.Resolve(target.Key));

        /// <summary>The record of the entity a value stands for; <c>Automation Error. Object was erased</c> where it is gone (D217).</summary>
        internal DxfItem ItemOf(LispVlaObject target) => FindItemOf(target) ?? throw Automation("Object was erased");

        /// <summary>The entity a value stands for.</summary>
        internal DxfEntity EntityOf(LispVlaObject target) =>
            DxfEntity.Of(ItemOf(target)) ?? throw Automation("Object was erased");

        /// <summary>
        /// <c>vlax-ename->vla-object</c>: the value of the object an entity name has: an entity, or the block or table record
        /// <c>tblobjname</c> gave the name of. One value per object while it is not released.
        /// </summary>
        internal LispVlaObject OfName(LispEntityName name) => Wrap(name.Handle);

        /// <summary>The name of the object of a value (<c>vlax-vla-object->ename</c>), even where it is erased; null for an object that has none (the application, the document, a collection, a selection set, a color).</summary>
        internal LispEntityName NameOf(LispVlaObject target)
        {
            switch (target.Kind)
            {
                case LispVlaKind.Entity:
                    return new LispEntityName(State.Resolve(target.Key));
                case LispVlaKind.Block:
                case LispVlaKind.Record:
                    return new LispEntityName(target.Key);
                default:
                    return null;
            }
        }

        #endregion

        #region Changing an entity

        /// <summary>
        /// Changes an entity through the session, as <c>entmod</c> does (D226): the evaluation's undo step first, then an edit
        /// of the drawing that the canvas redraws. An entity on a locked layer is refused (D217). A change the entity does not
        /// take is the error <c>Automation Error. </c> and the reason.
        /// </summary>
        internal void ChangeEntity(LispVlaObject target, string property, Action<DxfEntity> change)
        {
            DxfItem item = ItemOf(target);
            RefuseLocked(item);
            string handle = item.Handle;
            bool inSpace = Document.FindEntity(handle) != null;
            string failure = null;
            Session.OpenLispStep();
            Session.HoldLispStep();
            CadEditResult result = Session.Apply(
                "ACTIVEX",
                inSpace
                    ? new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, property, null, null) }
                    : null,
                s =>
                {
                    if (s.IsHandled(handle))
                    {
                        return;
                    }

                    DxfItem now = LispDrawing.FindItem(s.Document, handle);

                    try
                    {
                        change(DxfEntity.Of(now));
                    }
                    catch (Exception exception)
                        when (exception is ArgumentException
                            || exception is InvalidOperationException
                            || exception is KeyNotFoundException
                        )
                    {
                        failure = exception.Message;
                    }
                }
            );

            if (failure != null)
            {
                throw Automation(failure);
            }

            if (!result.Succeeded)
            {
                throw Automation(result.Message);
            }
        }

        /// <summary>Sets the color of an entity; by layer takes group 62 out as the entity had none (AutoCAD 2021, GUI g2: <c>(vla-put-Color o 256)</c> leaves no 62).</summary>
        private static void SetColor(DxfEntity entity, DxfColor color)
        {
            entity.Color = color;

            if (color.IsByLayer)
            {
                entity.Item.Colour = null;
            }
        }

        /// <summary>An entity on a locked layer is not changed: <c>Automation Error. On locked layer</c> (D217).</summary>
        private void RefuseLocked(DxfItem item)
        {
            if (IsOnLockedLayer(item))
            {
                throw Automation("On locked layer");
            }
        }

        private bool IsOnLockedLayer(DxfItem item)
        {
            DxfEntity entity = DxfEntity.Of(item);
            DxfLayerTable layers = Document.Database.LayerTable;
            return entity != null && layers.Has(entity.Layer) && layers[entity.Layer].IsLocked;
        }

        /// <summary><c>vlax-write-enabled-p</c>: T of an object that is there and not on a locked layer.</summary>
        internal bool IsWritable(LispVlaObject target) => !IsErased(target) && !IsLockedObject(target);

        private bool IsLockedObject(LispVlaObject target)
        {
            DxfItem item = target.Kind == LispVlaKind.Entity ? FindItemOf(target) : null;
            return item != null && IsOnLockedLayer(item);
        }

        private string LayerNamed(string name)
        {
            DxfLayerTable layers = Document.Database.LayerTable;
            return layers.Has(name) ? layers[name].Name : throw Automation("Key not found");
        }

        private string LinetypeNamed(string name)
        {
            if (string.Equals(name, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                return "ByLayer";
            }

            if (string.Equals(name, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                return "ByBlock";
            }

            DxfLinetypeTable linetypes = Document.Database.LinetypeTable;
            return linetypes.Has(name) ? linetypes[name].Name : throw Automation("Key not found");
        }

        #endregion

        #region Registration

        private void RegisterEntityMembers()
        {
            Func<LispVlaObject, bool> common = OfKinds(LispVlaKind.Block, LispVlaKind.Record, LispVlaKind.Entity);
            Func<LispVlaObject, bool> entity = OfKinds(LispVlaKind.Entity);

            Property("Document", common, o => DocumentObject);
            Property("Handle", common, o => HandleOf(o));
            Property("ObjectName", common, o => ObjectNameOf(o));
            Property("ObjectID", common, o => Whole(HandleNumber(HandleOf(o))));
            Property("OwnerID", common, o => Whole(HandleNumber(OwnerHandleOf(o))));

            Property(
                "Layer",
                entity,
                o => EntityOf(o).Layer,
                LispAxType.String,
                (o, v) =>
                {
                    string layer = LayerNamed((string)v);
                    ChangeEntity(o, "Layer", e => e.Layer = layer);
                }
            );
            Property(
                "Color",
                entity,
                o => EntityOf(o).ColorIndex,
                LispAxType.Int,
                (o, v) =>
                {
                    int index = (int)v;

                    if (index < 0 || index > 256)
                    {
                        throw Automation("Invalid argument color in IAcadEntity::put_Color");
                    }

                    DxfColor color = ColorOfIndex(index);
                    ChangeEntity(o, "Color", e => SetColor(e, color));
                }
            );
            Property(
                "Linetype",
                entity,
                o => EntityOf(o).Linetype,
                LispAxType.String,
                (o, v) =>
                {
                    string linetype = LinetypeNamed((string)v);
                    ChangeEntity(o, "Linetype", e => e.Linetype = linetype);
                }
            );
            Property(
                "LinetypeScale",
                entity,
                o => EntityOf(o).LinetypeScale,
                LispAxType.Double,
                (o, v) =>
                {
                    double scale = (double)v;

                    if (!(scale > 0.0) || double.IsInfinity(scale))
                    {
                        throw Automation("Invalid argument");
                    }

                    ChangeEntity(o, "Linetype scale", e => e.LinetypeScale = scale);
                }
            );
            Property(
                "Lineweight",
                entity,
                o => (int)EntityOf(o).LineWeight,
                LispAxType.Int,
                (o, v) =>
                {
                    if (!Enum.IsDefined(typeof(DxfLineWeight), (int)v))
                    {
                        throw Automation("Invalid argument");
                    }

                    ChangeEntity(o, "Lineweight", e => e.LineWeight = (DxfLineWeight)(int)v);
                }
            );
            Property(
                "Visible",
                entity,
                o => EntityOf(o).Visible,
                LispAxType.Bool,
                (o, v) => ChangeEntity(o, "Visible", e => e.Visible = (bool)v)
            );
            Property(
                "TrueColor",
                entity,
                o => ColorObject(EntityOf(o).Color),
                LispAxType.Object,
                (o, v) =>
                {
                    var given = (LispVlaObject)v;

                    if (given.Kind != LispVlaKind.Color)
                    {
                        throw Mismatch();
                    }

                    EnsureLive(given);
                    DxfColor color = ((LispAxColor)given.State).Color;
                    ChangeEntity(o, "Color", e => SetColor(e, color));
                }
            );

            Method("Delete", entity, 0, NoParameters, (o, a) => Delete(o));
            Method("Update", entity, 0, NoParameters, (o, a) => null);
            Method("Highlight", entity, 1, new[] { LispAxType.Bool }, (o, a) => null);
            Method("GetBoundingBox", entity, 2, new[] { LispAxType.Symbol, LispAxType.Symbol }, (o, a) =>
                BoundingBox(o, (LispSymbol)a[0], (LispSymbol)a[1])
            );
        }

        private string HandleOf(LispVlaObject target) =>
            target.Kind == LispVlaKind.Entity ? ItemOf(target).Handle : RecordOf(target).Handle;

        private string OwnerHandleOf(LispVlaObject target)
        {
            if (target.Kind == LispVlaKind.Entity)
            {
                DxfItem item = ItemOf(target);
                return item.File.OwnerOf(item);
            }

            return RecordOf(target).OwnerHandle;
        }

        private static long HandleNumber(string handle) =>
            long.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long value) ? value : 0;

        /// <summary>The last subclass marker of the list <c>entget</c> gives (D212): <c>AcDbLine</c>, <c>AcDbAttributeDefinition</c>; of a record <c>AcDbLayerTableRecord</c>.</summary>
        private string ObjectNameOf(LispVlaObject target)
        {
            if (target.Kind != LispVlaKind.Entity)
            {
                return ObjectNameOfRecord(RecordOf(target));
            }

            DxfItem item = ItemOf(target);
            string name = "AcDbEntity";

            foreach (KeyValuePair<int, object> pair in LispDrawing.GroupsOf(Document, item))
            {
                if (pair.Key == 100 && pair.Value is string marker)
                {
                    name = marker;
                }
            }

            return name;
        }

        /// <summary><c>Delete</c> erases as <c>entdel</c> does, in the undo step of the evaluation (D223); an entity of a block definition is refused, as <c>entdel</c> cannot take it out.</summary>
        private object Delete(LispVlaObject target)
        {
            DxfItem item = ItemOf(target);
            RefuseLocked(item);

            if (Document.FindEntity(item.Handle) == null)
            {
                throw new LispException(
                    "vla-Delete: deleting an object of a block definition is not supported by GeometryHelper.Cad yet"
                );
            }

            LispDrawing.EntDel(Session, State, new LispEntityName(item.Handle));
            Session.HoldLispStep();
            return null;
        }

        /// <summary>
        /// <c>GetBoundingBox</c> (D216): sets the two symbols to safearrays of three doubles, the lower and upper corners of the
        /// extents of the entity in the WCS, and gives nil.
        /// </summary>
        private object BoundingBox(LispVlaObject target, LispSymbol low, LispSymbol high)
        {
            GeoAabb3 box;

            try
            {
                box = EntityOf(target).GeometricExtents;
            }
            catch (InvalidOperationException)
            {
                throw Automation("Invalid extents");
            }

            if (box.IsEmpty)
            {
                throw Automation("Invalid extents");
            }

            if (low != null)
            {
                low.Value = LispSafeArray.Doubles(box.Min.X, box.Min.Y, box.Min.Z);
            }

            if (high != null)
            {
                high.Value = LispSafeArray.Doubles(box.Max.X, box.Max.Y, box.Max.Z);
            }

            return null;
        }

        #endregion
    }
}
