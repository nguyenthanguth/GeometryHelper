using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The members of the kinds of entity a routine reads and writes: a line, a circle, an arc, a light-weight polyline, a
    /// text, an MText, a block reference, its attributes and the attribute definitions of its block (section 1 of the L4
    /// spec, D224 as D244 corrects it). Each is read from the typed entity of the DXF layer and written through it in the
    /// undo step of the evaluation (<see cref="ChangeEntity"/>), as AutoCAD's ActiveX object model does. A point is a variant
    /// of three doubles; an angle is in radians.
    /// </summary>
    internal sealed partial class LispActiveX
    {
        partial void RegisterTypeMembers()
        {
            RegisterLineMembers();
            RegisterCircleMembers();
            RegisterArcMembers();
            RegisterPolylineMembers();
            RegisterTextMembers();
            RegisterMTextMembers();
            RegisterReferenceMembers();
            RegisterAttributeMembers();
            RegisterTextBox();
        }

        #region Helpers

        private static LispVariant PointOf(GeoPoint3 point) =>
            LispVariant.Point(point.X + 0.0, point.Y + 0.0, point.Z + 0.0);

        private static LispVariant VectorOf(GeoVector3 vector) =>
            LispVariant.Point(vector.X + 0.0, vector.Y + 0.0, vector.Z + 0.0);

        private static GeoPoint3 ToPoint(object converted)
        {
            var point = (double[])converted;
            return new GeoPoint3(point[0], point[1], point[2]);
        }

        private T As<T>(LispVlaObject target)
            where T : DxfEntity => (T)EntityOf(target);

        private void Set<T>(LispVlaObject target, string property, Action<T> change)
            where T : DxfEntity => ChangeEntity(target, property, e => change((T)e));

        #endregion

        #region Line, circle, arc, polyline

        private void RegisterLineMembers()
        {
            Func<LispVlaObject, bool> line = OfInterfaces("IAcadLine");
            DxfLine Line(LispVlaObject o) => As<DxfLine>(o);

            Property(
                "StartPoint",
                line,
                o => PointOf(Line(o).StartPoint),
                LispAxType.Point,
                (o, v) => Set<DxfLine>(o, "Start point", e => e.StartPoint = ToPoint(v))
            );
            Property(
                "EndPoint",
                line,
                o => PointOf(Line(o).EndPoint),
                LispAxType.Point,
                (o, v) => Set<DxfLine>(o, "End point", e => e.EndPoint = ToPoint(v))
            );
            Property("Length", line, o => Line(o).Length);
            Property("Angle", line, o => Line(o).Angle);
            Property("Delta", line, o => VectorOf(Line(o).Delta));
            Property(
                "Thickness",
                line,
                o => Line(o).Thickness,
                LispAxType.Double,
                (o, v) => Set<DxfLine>(o, "Thickness", e => e.Thickness = (double)v)
            );
        }

        private void RegisterCircleMembers()
        {
            Func<LispVlaObject, bool> circle = OfInterfaces("IAcadCircle");
            DxfCircle Circle(LispVlaObject o) => As<DxfCircle>(o);

            Property(
                "Center",
                circle,
                o => PointOf(Circle(o).Center),
                LispAxType.Point,
                (o, v) => Set<DxfCircle>(o, "Center", e => e.Center = ToPoint(v))
            );
            Property(
                "Radius",
                circle,
                o => Circle(o).Radius,
                LispAxType.Double,
                (o, v) => Set<DxfCircle>(o, "Radius", e => e.Radius = (double)v)
            );
            Property(
                "Diameter",
                circle,
                o => Circle(o).Diameter,
                LispAxType.Double,
                (o, v) => Set<DxfCircle>(o, "Diameter", e => e.Diameter = (double)v)
            );
            Property(
                "Circumference",
                circle,
                o => Circle(o).Circumference,
                LispAxType.Double,
                (o, v) => Set<DxfCircle>(o, "Circumference", e => e.Circumference = (double)v)
            );

            // Area is writable on a circle: the radius that holds the area (AutoCAD 2021, GUI g2: 100.0 gives 5.6419).
            Property(
                "Area",
                circle,
                o => Circle(o).Area,
                LispAxType.Double,
                (o, v) =>
                {
                    double area = (double)v;

                    if (!(area >= 0.0) || double.IsInfinity(area))
                    {
                        throw Automation("Invalid argument");
                    }

                    Set<DxfCircle>(o, "Area", e => e.Radius = Math.Sqrt(area / Math.PI));
                }
            );
        }

        private void RegisterArcMembers()
        {
            Func<LispVlaObject, bool> arc = OfInterfaces("IAcadArc");
            DxfArc Arc(LispVlaObject o) => As<DxfArc>(o);

            Property(
                "Center",
                arc,
                o => PointOf(Arc(o).Center),
                LispAxType.Point,
                (o, v) => Set<DxfArc>(o, "Center", e => e.Center = ToPoint(v))
            );
            Property(
                "Radius",
                arc,
                o => Arc(o).Radius,
                LispAxType.Double,
                (o, v) => Set<DxfArc>(o, "Radius", e => e.Radius = (double)v)
            );
            Property(
                "StartAngle",
                arc,
                o => Arc(o).StartAngle,
                LispAxType.Double,
                (o, v) => Set<DxfArc>(o, "Start angle", e => e.StartAngle = (double)v)
            );
            Property(
                "EndAngle",
                arc,
                o => Arc(o).EndAngle,
                LispAxType.Double,
                (o, v) => Set<DxfArc>(o, "End angle", e => e.EndAngle = (double)v)
            );

            // StartPoint and EndPoint are read only on an arc: a write is Type mismatch (AutoCAD 2021, GUI g2).
            Property("StartPoint", arc, o => PointOf(Arc(o).StartPoint));
            Property("EndPoint", arc, o => PointOf(Arc(o).EndPoint));
            Property("ArcLength", arc, o => Arc(o).Length);
            Property("TotalAngle", arc, o => Arc(o).TotalAngle);
            Property("Area", arc, o => Arc(o).Area);
        }

        private void RegisterPolylineMembers()
        {
            Func<LispVlaObject, bool> polyline = OfInterfaces("IAcadLWPolyline");
            DxfPolyline Polyline(LispVlaObject o) => As<DxfPolyline>(o);

            Property(
                "Coordinates",
                polyline,
                o =>
                {
                    DxfPolyline p = Polyline(o);
                    var values = new double[p.NumberOfVertices * 2];

                    for (int i = 0; i < p.NumberOfVertices; i++)
                    {
                        GeoPoint2 vertex = p.GetPoint2dAt(i);
                        values[2 * i] = vertex.X + 0.0;
                        values[2 * i + 1] = vertex.Y + 0.0;
                    }

                    return LispVariant.OfArray(LispSafeArray.Doubles(values));
                },
                LispAxType.Any,
                (o, v) =>
                {
                    double[] values = CoordinatesOf(v);
                    Set<DxfPolyline>(o, "Coordinates", e => SetCoordinates(e, values));
                }
            );
            Property(
                "Closed",
                polyline,
                o => Polyline(o).Closed,
                LispAxType.Bool,
                (o, v) => Set<DxfPolyline>(o, "Closed", e => e.Closed = (bool)v)
            );
            Property(
                "Elevation",
                polyline,
                o => Polyline(o).Elevation,
                LispAxType.Double,
                (o, v) => Set<DxfPolyline>(o, "Elevation", e => e.Elevation = (double)v)
            );
            Property("Length", polyline, o => Polyline(o).Length);
            Property("Area", polyline, o => Polyline(o).Area);
        }

        /// <summary>
        /// The numbers of a variant or a safearray of doubles that a polyline's <c>Coordinates</c> is given: two for each vertex,
        /// at least two vertices (ours: AutoCAD's words for a count that does not fit are not known).
        /// </summary>
        private static double[] CoordinatesOf(object given)
        {
            object held = given is LispVariant variant ? variant.Value : given;

            if (held is LispCons)
            {
                // The coercion error a list gets where a variant goes (AutoCAD 2021, GUI g2: a point); ours for the coordinates.
                throw new LispException(
                    "lisp value has no coercion to VARIANT with this type:  " + LispInterpreter.Print(given, true)
                );
            }

            if (!(held is LispSafeArray array) || array.Rank != 1)
            {
                throw Mismatch();
            }

            var values = new double[array.Items.Length];

            for (int i = 0; i < values.Length; i++)
            {
                object item = array.Items[i] is LispVariant inner ? inner.Value : array.Items[i];
                values[i] =
                    item is double real ? real
                    : item is int whole ? whole
                    : throw Mismatch();
            }

            if (values.Length < 4 || values.Length % 2 != 0)
            {
                throw Automation("Invalid argument");
            }

            return values;
        }

        /// <summary>Gives a polyline the vertices of 2n numbers: the first of its vertices that are there are moved and keep their bulges and widths, the rest are taken out or added straight (AutoCAD 2021, GUI g3: four numbers make two vertices).</summary>
        private static void SetCoordinates(DxfPolyline polyline, double[] values)
        {
            int count = values.Length / 2;
            int had = polyline.NumberOfVertices;

            for (int i = 0; i < Math.Min(count, had); i++)
            {
                polyline.SetPointAt(i, new GeoPoint2(values[2 * i], values[2 * i + 1]));
            }

            for (int i = had; i < count; i++)
            {
                polyline.AddVertexAt(i, new GeoPoint2(values[2 * i], values[2 * i + 1]), 0.0, 0.0, 0.0);
            }

            for (int i = had - 1; i >= count; i--)
            {
                polyline.RemoveVertexAt(i);
            }
        }

        #endregion

        #region Text and MText

        /// <summary>The fonts the drawing measures a text in while it is measured: those of the document, and the Fonts folder of Windows, as MLEADEREDIT measures. A path the drawing has already is kept.</summary>
        private IDisposable Measuring() => new MeasuringScope(Document);

        private sealed class MeasuringScope : IDisposable
        {
            private readonly DxfDatabase _database;
            private readonly string _searched;
            private readonly bool _set;

            public MeasuringScope(CadDocument document)
            {
                _database = document.Database;
                _searched = _database.FontSearchPath;

                if (!string.IsNullOrEmpty(_searched))
                {
                    return;
                }

                string windows = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                IEnumerable<string> folders = document.Fonts.Folders;

                if (!string.IsNullOrEmpty(windows))
                {
                    folders = folders.Concat(new[] { windows });
                }

                string path = string.Join(";", folders);

                if (path.Trim().Length > 0)
                {
                    _database.FontSearchPath = path;
                    _set = true;
                }
            }

            public void Dispose()
            {
                if (_set)
                {
                    _database.FontSearchPath = _searched;
                }
            }
        }

        /// <summary>
        /// Changes a text, an MText or an attribute. A text that is not left on its baseline is worked out again from its words
        /// in the document's fonts as the change is made (<see cref="MeasuringScope"/>); an attribute is changed through its
        /// INSERT, whose edit the canvas draws again with its attributes, the attribute's own layer checked first.
        /// </summary>
        private void ChangeText(LispVlaObject target, string property, Action<DxfEntity> change)
        {
            DxfItem item = ItemOf(target);

            if (item.Type == "ATTRIB")
            {
                RefuseLocked(item);
                string handle = item.Handle;
                string ownerHandle = OwnerHandleOf(target);
                LispVlaObject owner = string.IsNullOrEmpty(ownerHandle) ? null : Wrap(ownerHandle);

                if (owner != null && owner.Kind == LispVlaKind.Entity)
                {
                    ChangeEntity(owner, property, e =>
                    {
                        DxfEntity attribute = DxfEntity.Of(LispDrawing.FindItem(Document, handle));

                        using (((DxfDBText)attribute).IsDefaultAlignment ? null : Measuring())
                        {
                            change(attribute);
                        }
                    });
                    return;
                }
            }

            ChangeEntity(target, property, e =>
            {
                using (e is DxfDBText text && !text.IsDefaultAlignment ? Measuring() : null)
                {
                    change(e);
                }
            });
        }

        /// <summary>Gives a TEXT or MTEXT words, which make a field where they hold field codes (AutoCAD 2021, GUI g2: <c>%&lt;\AcVar Filename \f "%fn2"&gt;%</c> reads back the file's name and keeps its codes for <c>FieldCode</c>).</summary>
        private void SetWords(LispVlaObject target, string words) =>
            ChangeText(target, "Contents", e =>
                CadFields.Set(e, words, CadCommands.FieldContext(Session, CadFieldContexts.Demand))
            );

        /// <summary>The name of a text style of the drawing as it is kept; <c>Automation Error. Key not found</c> for one that is not there (AutoCAD 2021, GUI g2).</summary>
        private string StyleNamed(string name)
        {
            DxfTextStyleTable styles = Document.Database.TextStyleTable;
            return styles.Has(name) ? styles[name].Name : throw Automation("Key not found");
        }

        /// <summary>The value AutoCAD's <c>Alignment</c> gives for a text: 0 left, 1 centre, 2 right, 3 aligned, 4 middle, 5 fit, then the nine of a height: 6 to 8 top, 9 to 11 middle, 12 to 14 bottom.</summary>
        private static int AlignmentOf(DxfDBText text)
        {
            int across = (int)text.HorizontalMode;

            switch (text.VerticalMode)
            {
                case DxfTextVerticalMode.TextTop:
                    return 6 + across;
                case DxfTextVerticalMode.TextVerticalMid:
                    return 9 + across;
                case DxfTextVerticalMode.TextBottom:
                    return 12 + across;
                default:
                    return across;
            }
        }

        private static void SetAlignment(DxfDBText text, int alignment)
        {
            DxfTextHorizontalMode across;
            DxfTextVerticalMode up;

            if (alignment < 6)
            {
                across = (DxfTextHorizontalMode)alignment;
                up = DxfTextVerticalMode.TextBase;
            }
            else
            {
                across = (DxfTextHorizontalMode)((alignment - 6) % 3);
                up =
                    alignment < 9 ? DxfTextVerticalMode.TextTop
                    : alignment < 12 ? DxfTextVerticalMode.TextVerticalMid
                    : DxfTextVerticalMode.TextBottom;
            }

            // The pair is never one AutoCAD has no place for in between: back to the baseline before an aligned, a fitted or a
            // middle mode, the mode across first before a height.
            if (up == DxfTextVerticalMode.TextBase)
            {
                text.VerticalMode = up;
                text.HorizontalMode = across;
            }
            else
            {
                text.HorizontalMode = across;
                text.VerticalMode = up;
            }
        }

        private void RegisterTextMembers()
        {
            Func<LispVlaObject, bool> text = OfInterfaces("IAcadText");
            DxfDBText Text(LispVlaObject o) => As<DxfDBText>(o);

            Property("TextString", text, o => Text(o).TextString, LispAxType.String, (o, v) => SetWords(o, (string)v));
            Property(
                "ScaleFactor",
                text,
                o => Text(o).WidthFactor,
                LispAxType.Double,
                (o, v) => ChangeText(o, "Width factor", e => ((DxfDBText)e).WidthFactor = (double)v)
            );
            Property(
                "ObliqueAngle",
                text,
                o => Text(o).Oblique,
                LispAxType.Double,
                (o, v) => ChangeText(o, "Oblique angle", e => ((DxfDBText)e).Oblique = (double)v)
            );
            Property(
                "Alignment",
                text,
                o => AlignmentOf(Text(o)),
                LispAxType.Int,
                (o, v) =>
                {
                    int alignment = (int)v;

                    if (alignment < 0 || alignment > 14)
                    {
                        throw Automation("Invalid argument");
                    }

                    // The words are worked out in their fonts even from a text that was left, as it is made to hold another point.
                    ChangeEntity(o, "Justify", e =>
                    {
                        using (Measuring())
                        {
                            SetAlignment((DxfDBText)e, alignment);
                        }
                    });
                }
            );

            // The second point of a text left on its baseline is not used: a put is not applicable (AutoCAD 2021, GUI g3), a read
            // gives the origin (GUI g2).
            Property(
                "TextAlignmentPoint",
                text,
                o => PointOf(Text(o).AlignmentPoint),
                LispAxType.Point,
                (o, v) =>
                {
                    if (Text(o).IsDefaultAlignment)
                    {
                        throw Automation("Not applicable");
                    }

                    ChangeText(o, "Alignment point", e => ((DxfDBText)e).AlignmentPoint = ToPoint(v));
                }
            );

            Func<LispVlaObject, bool> textLike = OfInterfaces("IAcadText", "IAcadAttributeReference", "IAcadAttribute");
            Property(
                "Height",
                textLike,
                o => Text(o).Height,
                LispAxType.Double,
                (o, v) => ChangeText(o, "Height", e => ((DxfDBText)e).Height = (double)v)
            );
            Property(
                "Rotation",
                textLike,
                o => Text(o).Rotation,
                LispAxType.Double,
                (o, v) => ChangeText(o, "Rotation", e => ((DxfDBText)e).Rotation = (double)v)
            );
            Property(
                "StyleName",
                textLike,
                o => Text(o).TextStyleName,
                LispAxType.String,
                (o, v) =>
                {
                    string style = StyleNamed((string)v);
                    ChangeText(o, "Text style", e => ((DxfDBText)e).TextStyleName = style);
                }
            );
            Property(
                "InsertionPoint",
                textLike,
                o => PointOf(Text(o).Position),
                LispAxType.Point,
                (o, v) => ChangeText(o, "Position", e => ((DxfDBText)e).Position = ToPoint(v))
            );

            Method("FieldCode", OfInterfaces("IAcadText", "IAcadMText"), 0, NoParameters, (o, a) =>
                CadFields.CodesOf(EntityOf(o))
            );

            // The box of a text is the box of its words in the document's fonts (D237): where a font is found, the same box the
            // textbox function gives moved to the insertion point; where none is, the insertion point alone.
            Method(
                "GetBoundingBox",
                OfInterfaces("IAcadText", "IAcadMText"),
                2,
                new[] { LispAxType.Symbol, LispAxType.Symbol },
                (o, a) => TextBoundingBox(o, (LispSymbol)a[0], (LispSymbol)a[1])
            );

            // O1's GetBoundingBox of every entity was registered first and would be the one found: the one of texts goes before it.
            List<LispAxMember> boxes = _members["GetBoundingBox"];
            LispAxMember own = boxes[boxes.Count - 1];
            boxes.RemoveAt(boxes.Count - 1);
            boxes.Insert(0, own);
        }

        private void RegisterMTextMembers()
        {
            Func<LispVlaObject, bool> mtext = OfInterfaces("IAcadMText");
            DxfMText MText(LispVlaObject o) => As<DxfMText>(o);

            Property("TextString", mtext, o => MText(o).Contents, LispAxType.String, (o, v) => SetWords(o, (string)v));
            Property(
                "Height",
                mtext,
                o => MText(o).TextHeight,
                LispAxType.Double,
                (o, v) => Set<DxfMText>(o, "Text height", e => e.TextHeight = (double)v)
            );
            Property(
                "Rotation",
                mtext,
                o => MText(o).Rotation,
                LispAxType.Double,
                (o, v) => Set<DxfMText>(o, "Rotation", e => e.Rotation = (double)v)
            );
            Property(
                "StyleName",
                mtext,
                o => MText(o).TextStyleName,
                LispAxType.String,
                (o, v) =>
                {
                    string style = StyleNamed((string)v);
                    Set<DxfMText>(o, "Text style", e => e.TextStyleName = style);
                }
            );
            Property(
                "InsertionPoint",
                mtext,
                o => PointOf(MText(o).Location),
                LispAxType.Point,
                (o, v) => Set<DxfMText>(o, "Location", e => e.Location = ToPoint(v))
            );
            Property(
                "Width",
                mtext,
                o => MText(o).Width,
                LispAxType.Double,
                (o, v) => Set<DxfMText>(o, "Width", e => e.Width = (double)v)
            );
            Property(
                "AttachmentPoint",
                mtext,
                o => (int)MText(o).Attachment,
                LispAxType.Int,
                (o, v) => Set<DxfMText>(o, "Attachment", e => e.Attachment = (DxfAttachmentPoint)(int)v)
            );
        }

        /// <summary>
        /// <c>GetBoundingBox</c> of a TEXT or an MTEXT: the box of its words as the DXF layer measures them in the document's
        /// fonts (<see cref="DxfDBText.GeometricExtents"/>, <see cref="DxfMText.GeometricExtents"/>), set in the two symbols as
        /// safearrays (D216). A text whose font is not found has its insertion point for both corners.
        /// </summary>
        private object TextBoundingBox(LispVlaObject target, LispSymbol low, LispSymbol high)
        {
            GeoAabb3 box;

            try
            {
                DxfEntity entity = EntityOf(target);

                using (Measuring())
                {
                    box = entity.GeometricExtents;
                }
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
                low.Value = LispSafeArray.Doubles(box.Min.X + 0.0, box.Min.Y + 0.0, box.Min.Z + 0.0);
            }

            if (high != null)
            {
                high.Value = LispSafeArray.Doubles(box.Max.X + 0.0, box.Max.Y + 0.0, box.Max.Z + 0.0);
            }

            return null;
        }

        #endregion

        #region Block references and attributes

        private void RegisterReferenceMembers()
        {
            Func<LispVlaObject, bool> reference = OfInterfaces("IAcadBlockReference", "IAcadMInsertBlock");
            DxfBlockReference Reference(LispVlaObject o) => As<DxfBlockReference>(o);

            Property(
                "Name",
                reference,
                o => Reference(o).Name,
                LispAxType.String,
                (o, v) =>
                {
                    string name = (string)v;

                    if (!Document.Database.BlockTable.Has(name))
                    {
                        throw Automation("Key not found");
                    }

                    Set<DxfBlockReference>(o, "Name", e => e.BlockTableRecord = Document.Database.BlockTable[name]);
                }
            );
            Property("EffectiveName", reference, o => CadDynamicBlocks.NameOf(Reference(o)));
            Property(
                "InsertionPoint",
                reference,
                o => PointOf(Reference(o).Position),
                LispAxType.Point,
                (o, v) => Set<DxfBlockReference>(o, "Position", e => e.Position = ToPoint(v))
            );
            Property(
                "Rotation",
                reference,
                o => Reference(o).Rotation,
                LispAxType.Double,
                (o, v) => Set<DxfBlockReference>(o, "Rotation", e => e.Rotation = (double)v)
            );
            Property(
                "XScaleFactor",
                reference,
                o => Reference(o).ScaleFactors.X,
                LispAxType.Double,
                (o, v) => SetScale(o, 0, (double)v)
            );
            Property(
                "YScaleFactor",
                reference,
                o => Reference(o).ScaleFactors.Y,
                LispAxType.Double,
                (o, v) => SetScale(o, 1, (double)v)
            );
            Property(
                "ZScaleFactor",
                reference,
                o => Reference(o).ScaleFactors.Z,
                LispAxType.Double,
                (o, v) => SetScale(o, 2, (double)v)
            );
            Property("HasAttributes", reference, o => AttributesOf(Reference(o)).Count > 0);
            Property("IsDynamicBlock", reference, o => CadDynamicBlocks.IsDynamic(Reference(o)));

            Method("GetAttributes", reference, 0, NoParameters, (o, a) =>
                LispVariant.OfArray(
                    LispSafeArray.Objects(AttributesOf(Reference(o)).Select(x => WrapEntity(x.Item)).ToList())
                )
            );
            Method("GetConstantAttributes", reference, 0, NoParameters, (o, a) =>
                LispVariant.OfArray(
                    LispSafeArray.Objects(ConstantAttributesOf(Reference(o)).Select(x => WrapEntity(x.Item)).ToList())
                )
            );
        }

        private void SetScale(LispVlaObject target, int axis, double factor) =>
            Set<DxfBlockReference>(target, "Scale", e =>
            {
                GeoVector3 now = e.ScaleFactors;
                e.ScaleFactors = new GeoVector3(
                    axis == 0 ? factor : now.X,
                    axis == 1 ? factor : now.Y,
                    axis == 2 ? factor : now.Z
                );
            });

        /// <summary>The attributes a reference holds, in order: the ATTRIBs of its record that are not erased.</summary>
        private static List<DxfAttributeReference> AttributesOf(DxfBlockReference reference) =>
            reference.AttributeCollection.Where(a => !a.IsErased).ToList();

        /// <summary>The attribute definitions of a reference's block that are constant, in order: their values are the block's, not the reference's.</summary>
        private static List<DxfAttributeDefinition> ConstantAttributesOf(DxfBlockReference reference)
        {
            DxfBlockTableRecord block = reference.BlockTableRecord;

            return block == null
                ? new List<DxfAttributeDefinition>()
                : block.OfType<DxfAttributeDefinition>().Where(d => !d.IsErased && d.Constant).ToList();
        }

        private void RegisterAttributeMembers()
        {
            Func<LispVlaObject, bool> reference = OfInterfaces("IAcadAttributeReference");
            Func<LispVlaObject, bool> definition = OfInterfaces("IAcadAttribute");
            Func<LispVlaObject, bool> both = OfInterfaces("IAcadAttributeReference", "IAcadAttribute");
            DxfAttributeReference Reference(LispVlaObject o) => As<DxfAttributeReference>(o);
            DxfAttributeDefinition Definition(LispVlaObject o) => As<DxfAttributeDefinition>(o);

            // Attributes write to the ATTRIB of their INSERT, as the Properties palette does (D224); entmod of an ATTRIB stays refused.
            Property(
                "TagString",
                reference,
                o => Reference(o).Tag,
                LispAxType.String,
                (o, v) => ChangeText(o, "Tag", e => ((DxfAttributeReference)e).Tag = (string)v)
            );
            Property(
                "TagString",
                definition,
                o => Definition(o).Tag,
                LispAxType.String,
                (o, v) => ChangeText(o, "Tag", e => ((DxfAttributeDefinition)e).Tag = (string)v)
            );
            Property(
                "PromptString",
                definition,
                o => Definition(o).Prompt,
                LispAxType.String,
                (o, v) => ChangeText(o, "Prompt", e => ((DxfAttributeDefinition)e).Prompt = (string)v)
            );
            Property(
                "TextString",
                both,
                o => As<DxfDBText>(o).TextString,
                LispAxType.String,
                (o, v) => ChangeText(o, "Value", e => ((DxfDBText)e).TextString = (string)v)
            );
            Property(
                "Invisible",
                reference,
                o => Reference(o).Invisible,
                LispAxType.Bool,
                (o, v) => ChangeText(o, "Invisible", e => ((DxfAttributeReference)e).Invisible = (bool)v)
            );
            Property(
                "Invisible",
                definition,
                o => Definition(o).Invisible,
                LispAxType.Bool,
                (o, v) => ChangeText(o, "Invisible", e => ((DxfAttributeDefinition)e).Invisible = (bool)v)
            );
            Property("Constant", reference, o => Reference(o).IsConstant);
            Property("Constant", definition, o => Definition(o).Constant);
        }

        #endregion
    }
}
