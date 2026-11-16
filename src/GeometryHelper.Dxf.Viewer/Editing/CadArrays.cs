using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The associative arrays of ARRAYRECT, ARRAYPOLAR and ARRAYPATH as AutoCAD 2021 stores them (read from what its
    /// <c>AssocArray</c> API and its commands wrote, and checked by loading what is written here into AutoCAD): an INSERT of
    /// an anonymous block that holds one INSERT of the source block for each item, the source block holding the source
    /// objects as they lie from the base point; and in the drawing's associative network an <c>ACDBASSOCACTION</c> with an
    /// <c>ACDBASSOCARRAYACTIONBODY</c> (the kind of parameters, the items with their matrices and the INSERT that draws each),
    /// the values of the parameters by name, dependencies on the two blocks, on the INSERT and, for a path array, on the
    /// path curve, and the parameters of the action: the base normal, the origin, the base point and, where the array has a
    /// row axis angle or a path, the edge that is its profile.
    /// </summary>
    internal static partial class CadArrays
    {
        internal const string BodyType = "ACDBASSOCARRAYACTIONBODY";
        private const int InsertStatus = int.MinValue;

        private static readonly string[] Classes =
        {
            "ACDBASSOCNETWORK|AcDbAssocNetwork",
            "ACDBASSOCACTION|AcDbAssocAction",
            "ACDBASSOCDEPENDENCY|AcDbAssocDependency",
            "ACDBASSOCARRAYACTIONBODY|AcDbAssocArrayActionBody",
            "ACDBASSOCVERTEXACTIONPARAM|AcDbAssocVertexActionParam",
            "ACDBASSOCEDGEACTIONPARAM|AcDbAssocEdgeActionParam",
        };

        private static readonly string[] ParameterClasses =
        {
            "ACDBASSOCARRAYRECTANGULARPARAMETERS|AcDbAssocArrayRectangularParameters",
            "ACDBASSOCARRAYPOLARPARAMETERS|AcDbAssocArrayPolarParameters",
            "ACDBASSOCARRAYPATHPARAMETERS|AcDbAssocArrayPathParameters",
        };

        /// <summary>The objects of one array in the drawing: its action, body, dependencies and parameters, the blocks and the INSERT.</summary>
        internal sealed class Record
        {
            internal DxfItem Action;
            internal DxfItem Body;
            internal List<DxfItem> Dependencies = new List<DxfItem>();
            internal List<DxfItem> Parameters = new List<DxfItem>();
            internal string InsertHandle;
            internal DxfItem InsertDependency;
            internal string PathHandle;
            internal DxfItem PathDependency;
            internal DxfItem ProfileEdge;
        }

        /// <summary>Whether the drawing holds an associative network, so that an array may be there.</summary>
        internal static bool Holds(DxfDatabase database)
        {
            try
            {
                return database.File.HoldsObjects
                    && database.NamedObjectsDictionary.Contains(CadCenterMarks.NetworkKey);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>The name of the parameters' class in the body of an array of a kind.</summary>
        internal static string ParametersName(CadArrayKind kind) =>
            kind == CadArrayKind.Rectangular ? "AcDbAssocArrayRectangularParameters"
            : kind == CadArrayKind.Polar ? "AcDbAssocArrayPolarParameters"
            : "AcDbAssocArrayPathParameters";

        /// <summary>
        /// The array an INSERT is, read from the drawing's network: its record where the INSERT carries a dependency of an
        /// action whose body is an array's and that names the INSERT; null for any other entity.
        /// </summary>
        internal static Record Find(DxfDatabase database, DxfEntity entity)
        {
            if (!(entity is DxfBlockReference) || !database.File.HoldsObjects)
            {
                return null;
            }

            DxfFile file = database.File;

            foreach (string reactor in entity.GetPersistentReactorIds())
            {
                DxfItem dependency = file.ObjectItem(reactor);

                if (dependency == null || dependency.IsRemoved || dependency.Type != "ACDBASSOCDEPENDENCY")
                {
                    continue;
                }

                DxfItem action = file.ObjectItem(file.OwnerOf(dependency) ?? string.Empty);

                if (action == null || action.IsRemoved || action.Type != "ACDBASSOCACTION")
                {
                    continue;
                }

                Record record = Read(file, action);

                if (
                    record != null
                    && string.Equals(record.InsertHandle, entity.Handle, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return record;
                }
            }

            return null;
        }

        /// <summary>An action read; null where its body is not an array's or it has no dependency on an INSERT.</summary>
        private static Record Read(DxfFile file, DxfItem action)
        {
            var record = new Record { Action = action };

            foreach (DxfTypedValue group in action.Groups)
            {
                if (group.TypeCode != 360)
                {
                    continue;
                }

                DxfItem item = file.ObjectItem(group.Value.Trim());

                if (item == null || item.IsRemoved)
                {
                    continue;
                }

                switch (item.Type)
                {
                    case BodyType:
                        record.Body = item;
                        break;
                    case "ACDBASSOCDEPENDENCY":
                        record.Dependencies.Add(item);
                        break;
                    case "ACDBASSOCVERTEXACTIONPARAM":
                        record.Parameters.Add(item);
                        break;
                    case "ACDBASSOCEDGEACTIONPARAM":
                        record.Parameters.Add(item);
                        record.ProfileEdge = item;
                        break;
                }
            }

            if (record.Body == null)
            {
                return null;
            }

            // The dependencies as AutoCAD writes them: the path (of a path array only), the block of items, the source block, and
            // last the INSERT, which carries the status AutoCAD gives it (an INSERT is no object, so it is told by that).
            foreach (DxfItem dependency in record.Dependencies)
            {
                int at = CadCenterMarks.Start(dependency.Groups, "AcDbAssocDependency");

                if (
                    at >= 0
                    && at + 6 < dependency.Groups.Count
                    && dependency.Groups[at + 6].Value.Trim() == InsertStatus.ToString(CultureInfo.InvariantCulture)
                )
                {
                    record.InsertHandle = ObjectOf(dependency);
                    record.InsertDependency = dependency;
                }
            }

            if (record.InsertDependency != null && record.Dependencies.Count == 4)
            {
                record.PathDependency = record.Dependencies[0];
                record.PathHandle = ObjectOf(record.PathDependency);
            }

            return record.InsertHandle == null ? null : record;
        }

        /// <summary>The handle of the object a dependency is on: the first 330 after the group 90 that follows its flags.</summary>
        internal static string ObjectOf(DxfItem dependency)
        {
            int at = CadCenterMarks.Start(dependency.Groups, "AcDbAssocDependency");
            return at >= 0 && at + 7 < dependency.Groups.Count && dependency.Groups[at + 7].TypeCode == 330
                ? dependency.Groups[at + 7].Value.Trim()
                : null;
        }

        /// <summary>The dependencies of the drawing on an entity, in the order of its persistent reactors.</summary>
        private static string LastDependency(DxfFile file, DxfEntity entity)
        {
            string last = "0";

            foreach (string reactor in entity.GetPersistentReactorIds())
            {
                if (
                    file.ObjectItem(reactor) is DxfItem item
                    && !item.IsRemoved
                    && (item.Type == "ACDBASSOCGEOMDEPENDENCY" || item.Type == "ACDBASSOCDEPENDENCY")
                )
                {
                    last = item.Handle;
                }
            }

            return last;
        }

        /// <summary>
        /// Makes the array's objects (see the class) for an INSERT already in a space, over the source block and the block of
        /// items given: the classes listed, the action in the network of the space, its body with the items, and the
        /// dependencies, parameters and reactors. Returns what it made.
        /// </summary>
        internal static Record WriteObjects(
            DxfDatabase database,
            CadSpace space,
            CadAssocArray array,
            DxfBlockReference insert,
            DxfBlockTableRecord source,
            DxfBlockTableRecord block,
            IReadOnlyList<CadArrayItem> items
        )
        {
            DxfFile file = database.File;

            foreach (string name in Classes)
            {
                string[] parts = name.Split('|');
                file.AddClass(parts[0], parts[1], 1025);
                file.CountClass(parts[0]);
            }

            foreach (string name in ParameterClasses)
            {
                string[] parts = name.Split('|');
                file.AddClass(parts[0], parts[1], 1025);
            }

            DxfItem network = CadCenterMarks.EnsureNetwork(database, space.Block, out _);
            int index = CadCenterMarks.ActionsOf(network).Count + 1;

            // The action first and empty, so that what it owns can name it; written whole at the end.
            DxfItem action = file.AddObject("ACDBASSOCACTION", null, b =>
            {
                b.Group(330, network.Handle);
                b.Group(100, "AcDbAssocAction");
            });

            // The body: the kind of parameters, the items, and the transformation of the INSERT.
            var body = new CadCenterMarks.Gs();
            body.G(330, action.Handle);
            body.G(100, "AcDbAssocActionBody");
            body.G(90, 2);
            body.G(100, "AcDbAssocArrayActionBody");
            body.G(90, 1);
            body.G(1, ParametersName(array.Kind));
            body.G(90, 0);
            body.G(90, items.Count);
            body.G(1, "AcDbAssocArrayItem");

            foreach (CadArrayItem item in items)
            {
                body.G(100, "AcDbAssocArrayItem");
                body.G(90, 0);
                body.G(90, item.Index);
                body.G(90, item.Row);
                body.G(90, item.Level);

                if (item.IsTranslation)
                {
                    body.G(90, 8);
                    body.P(11, item.Matrix[0, 3], item.Matrix[1, 3], item.Matrix[2, 3]);
                }
                else
                {
                    body.G(90, 12);
                    Matrix(body, item.Matrix);
                }

                body.G(330, item.Handle ?? "0");
            }

            Matrix(body, insert.BlockTransform);
            DxfItem bodyItem = file.AddObject(BodyType, null, body.Emit);

            // The dependencies, in the order AutoCAD writes them: the path, the block of items, the source block, the INSERT.
            var dependencies = new List<string>();
            int order = 0;
            string pathDependency = null;

            if (array.Kind == CadArrayKind.Path)
            {
                pathDependency = AddDependency(file, action, array.Path, 1, 0, 0, ++order);
                dependencies.Add(pathDependency);
            }

            string blockDependency = AddDependency(file, action, block.Item, 0, 1, 0, ++order);
            dependencies.Add(blockDependency);
            string sourceDependency = AddDependency(file, action, source.Item, 1, 0, 0, ++order);
            dependencies.Add(sourceDependency);
            string insertDependency = AddDependency(file, action, insert, 1, 1, InsertStatus, ++order);
            dependencies.Add(insertDependency);

            // The parameters: base normal, origin, base point, and the profile edge of a row angle or a path.
            var parameters = new List<string>();
            parameters.Add(VertexParameter(file, action, "BaseNormal", 0.0, 0.0, 1.0));
            parameters.Add(VertexParameter(file, action, "Origin", 0.0, 0.0, 0.0));

            if (array.Kind == CadArrayKind.Path)
            {
                parameters.Add(ProfileParameter(file, action, array, pathDependency));
                parameters.Add(VertexParameter(file, action, "basePoint", 0.0, 0.0, 0.0));
            }
            else
            {
                parameters.Add(VertexParameter(file, action, "basePoint", 0.0, 0.0, 0.0));

                if (array.Kind == CadArrayKind.Rectangular && Math.Abs(array.RowAngle) > 1E-12)
                {
                    parameters.Add(ProfileParameter(file, action, array, null));
                }
            }

            var whole = new CadCenterMarks.Gs();
            whole.G(5, action.Handle);
            whole.G(330, network.Handle);
            whole.G(100, "AcDbAssocAction");
            whole.G(90, 2);
            whole.G(90, 0);
            whole.G(330, network.Handle);
            whole.G(360, bodyItem.Handle);
            whole.G(90, index);
            whole.G(90, dependencies.Count);
            whole.G(90, dependencies.Count);

            foreach (string handle in dependencies)
            {
                whole.G(360, handle);
            }

            whole.G(90, 0);
            whole.G(90, parameters.Count);

            foreach (string handle in parameters)
            {
                whole.G(360, handle);
            }

            whole.G(90, 0);
            List<KeyValuePair<string, object>> values = Values(array);
            whole.G(90, values.Count);

            foreach (KeyValuePair<string, object> value in values)
            {
                Value(whole, value.Key, value.Value);
            }

            file.ReplaceTail(action, 0, whole);
            CadCenterMarks.List(file, network, 360, action.Handle);
            return Read(file, action);
        }

        private static void Matrix(CadCenterMarks.Gs g, GeoTransform3 matrix)
        {
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    g.G(40, matrix[r, c]);
                }
            }
        }

        /// <summary>
        /// Adds a dependency of the action on an object, linked after the last of the dependencies the object already carries,
        /// and sets it among the object's reactors. The flags are the four AutoCAD gives each kind of target: (1, 0, 1, 1) for
        /// a path curve and the source block, (0, 1, 1, 1) for the block of items and (1, 1, 1, 1) with a status for the INSERT.
        /// </summary>
        private static string AddDependency(
            DxfFile file,
            DxfItem action,
            object target,
            int first,
            int second,
            int status,
            int order
        )
        {
            DxfItem on = target is DxfItem item ? item : ((DxfEntity)target).Item;
            string handle = on.Handle;
            string previous = target is DxfEntity entity ? LastDependency(file, entity) : "0";
            var g = new CadCenterMarks.Gs();
            g.G(330, action.Handle);
            g.G(100, "AcDbAssocDependency");
            g.G(90, 2);
            g.G(90, 0);
            g.G(290, first);
            g.G(290, second);
            g.G(290, 1);
            g.G(290, 1);
            g.G(90, status);
            g.G(330, handle);
            g.G(290, 0);
            g.G(330, previous);
            g.G(330, "0");
            g.G(360, "0");
            g.G(90, order);
            DxfItem made = file.AddObject("ACDBASSOCDEPENDENCY", null, g.Emit);

            if (previous != "0" && file.ObjectItem(previous) is DxfItem before)
            {
                int at = CadCenterMarks.Start(before.Groups, "AcDbAssocDependency");

                if (at >= 0)
                {
                    file.SetValueAt(before, at + 10, made.Handle);
                }
            }

            file.SetReactor(on, made.Handle, true);
            return made.Handle;
        }

        private static string VertexParameter(DxfFile file, DxfItem action, string name, double x, double y, double z)
        {
            var g = new CadCenterMarks.Gs();
            g.G(330, action.Handle);
            g.G(100, "AcDbAssocActionParam");
            g.G(90, 1);
            g.G(90, 0);
            g.G(1, name);
            g.G(100, "AcDbAssocSingleDependencyActionParam");
            g.G(90, 0);
            g.G(330, "0");
            g.G(100, "AcDbAssocVertexActionParam");
            g.G(90, 0);
            g.P(10, x, y, z);
            return file.AddObject("ACDBASSOCVERTEXACTIONPARAM", null, g.Emit).Handle;
        }

        /// <summary>
        /// The row profile: for a path array the path curve as an edge, held by the dependency on it; for a rectangular array
        /// the line from the origin along its column axis, which is how AutoCAD keeps a row axis angle.
        /// </summary>
        private static string ProfileParameter(DxfFile file, DxfItem action, CadAssocArray array, string dependency)
        {
            var g = new CadCenterMarks.Gs();
            g.G(330, action.Handle);
            g.G(100, "AcDbAssocActionParam");
            g.G(90, 1);
            g.G(90, 0);
            g.G(1, "RowProfile");
            g.G(100, "AcDbAssocSingleDependencyActionParam");
            g.G(90, 0);
            g.G(330, dependency ?? "0");
            g.G(100, "AcDbAssocEdgeActionParam");
            g.G(90, 0);
            g.G(330, "0");
            g.G(290, 1);

            if (array.Kind == CadArrayKind.Path)
            {
                Edge(g, array.Path);
            }
            else
            {
                g.G(90, 19);
                g.P(10, 0.0, 0.0, 0.0);
                g.P(10, Math.Cos(array.RowAngle), Math.Sin(array.RowAngle), 0.0);
            }

            return file.AddObject("ACDBASSOCEDGEACTIONPARAM", null, g.Emit).Handle;
        }

        /// <summary>Whether a curve may be the path of an associative array: a line, an arc, a circle or a polyline of lines and arcs in the plan.</summary>
        internal static bool CanBePath(DxfCurve curve)
        {
            switch (curve)
            {
                case DxfLine _:
                case DxfArc _:
                case DxfCircle _:
                    return true;
                case DxfPolyline polyline:
                    return polyline.NumberOfVertices >= 2
                        && Math.Abs(polyline.Normal.X) < 1E-12
                        && Math.Abs(polyline.Normal.Y) < 1E-12;
                default:
                    return false;
            }
        }

        /// <summary>The geometry of a path curve as AutoCAD keeps it: its kind (23 a line, 11 an arc or circle, 47 a chain of them) and the numbers of each.</summary>
        private static void Edge(CadCenterMarks.Gs g, DxfCurve curve)
        {
            switch (curve)
            {
                case DxfLine line:
                    g.G(90, 23);
                    LineEdge(g, line.StartPoint, line.EndPoint);
                    break;
                case DxfCircle circle:
                    g.G(90, 11);
                    ArcEdge(g, circle.Center, circle.Normal, circle.Radius, 0.0, 2.0 * Math.PI);
                    break;
                case DxfArc arc:
                    g.G(90, 11);
                    ArcEdge(g, arc.Center, arc.Normal, arc.Radius, arc.StartAngle, arc.EndAngle);
                    break;
                case DxfPolyline polyline:
                    Chain(g, polyline);
                    break;
                default:
                    throw new NotSupportedException("The path is not a line, arc, circle or polyline.");
            }
        }

        private static void LineEdge(CadCenterMarks.Gs g, GeoPoint3 start, GeoPoint3 end)
        {
            g.P(10, start.X, start.Y, start.Z);
            g.P(10, end.X - start.X, end.Y - start.Y, end.Z - start.Z);
        }

        private static void ArcEdge(
            CadCenterMarks.Gs g,
            GeoPoint3 centre,
            GeoVector3 normal,
            double radius,
            double start,
            double end
        )
        {
            GeoVector3 axis = CadCenterMarks.AxisOf(normal);
            g.P(10, centre.X, centre.Y, centre.Z);
            g.P(10, normal.X, normal.Y, normal.Z);
            g.P(10, axis.X, axis.Y, axis.Z);
            g.G(40, radius);
            g.G(40, start);
            g.G(40, end > start ? end : end + 2.0 * Math.PI);
            g.G(40, 0.0);
        }

        /// <summary>A polyline in the plan as a chain of lines and arcs: a segment with a bulge is the arc it bends, counterclockwise about Z or clockwise about -Z.</summary>
        private static void Chain(CadCenterMarks.Gs g, DxfPolyline polyline)
        {
            int count = polyline.NumberOfVertices;
            int segments = polyline.Closed ? count : count - 1;
            double z = polyline.Elevation;
            g.G(90, 47);
            g.G(90, segments);

            for (int i = 0; i < segments; i++)
            {
                GeoPoint2 a = polyline.GetPoint2dAt(i);
                GeoPoint2 b = polyline.GetPoint2dAt((i + 1) % count);
                double bulge = polyline.GetBulgeAt(i);

                if (Math.Abs(bulge) <= 1E-12)
                {
                    g.G(90, 23);
                    LineEdge(g, new GeoPoint3(a.X, a.Y, z), new GeoPoint3(b.X, b.Y, z));
                    continue;
                }

                double angle = 4.0 * Math.Atan(bulge);
                double vx = b.X - a.X;
                double vy = b.Y - a.Y;
                double length = Math.Sqrt(vx * vx + vy * vy);
                double half = length / 2.0 / Math.Tan(angle / 2.0);
                var centre = new GeoPoint2(
                    (a.X + b.X) / 2.0 - vy / length * half,
                    (a.Y + b.Y) / 2.0 + vx / length * half
                );
                double radius = Math.Abs(length / 2.0 / Math.Sin(angle / 2.0));
                double from = Math.Atan2(a.Y - centre.Y, a.X - centre.X);
                g.G(90, 11);
                g.P(10, centre.X, centre.Y, z);

                if (bulge > 0.0)
                {
                    g.P(10, 0.0, 0.0, 1.0);
                    g.P(10, 1.0, 0.0, 0.0);
                    g.G(40, radius);
                    g.G(40, from);
                    g.G(40, from + angle);
                }
                else
                {
                    g.P(10, 0.0, 0.0, -1.0);
                    g.P(10, 1.0, 0.0, 0.0);
                    g.G(40, radius);
                    g.G(40, -from);
                    g.G(40, -from - angle);
                }

                g.G(40, 0.0);
            }
        }

        /// <summary>The values of the action by name, in the order AutoCAD writes them for the kind of array.</summary>
        private static List<KeyValuePair<string, object>> Values(CadAssocArray a)
        {
            var values = new List<KeyValuePair<string, object>>();

            void Add(string name, object value) => values.Add(new KeyValuePair<string, object>(name, value));

            switch (a.Kind)
            {
                case CadArrayKind.Rectangular:
                    Add("AlignItems", 0);
                    Add("AxesAngle", Angle(a.AxesAngle));
                    Add("ItemSpacing", Length(a.ColumnSpacing));
                    Add("Items", a.Columns);
                    Add("LevelSpacing", Length(a.LevelSpacing));
                    Add("Levels", a.Levels);
                    Add("RowElevation", Length(a.RowElevation));
                    Add("RowSpacing", Length(a.RowSpacing));
                    Add("Rows", a.Rows);
                    break;
                case CadArrayKind.Polar:
                    Add("FillAngle", Angle(Math.Abs(a.Fill)));
                    Add("ItemSpacing", Angle(a.Between));
                    Add("Items", a.Items);
                    Add("LevelSpacing", Length(a.LevelSpacing));
                    Add("Levels", a.Levels);
                    Add("RowElevation", Length(a.RowElevation));
                    Add("RowSpacing", Length(a.RowSpacing));
                    Add("Rows", a.Rows);
                    Add("Radius", Length(a.Radius));
                    Add("StartAngle", Angle(a.StartAngle));

                    if (a.Clockwise)
                    {
                        Add("Direction", 0);
                    }

                    if (!a.Rotate)
                    {
                        Add("AlignItems", 0);
                    }

                    break;
                default:
                    Add("ItemSpacing", Length(a.Spacing));
                    Add("Items", a.Items);
                    Add("LevelSpacing", Length(a.LevelSpacing));
                    Add("Levels", a.Levels);
                    Add("RowElevation", Length(a.RowElevation));
                    Add("RowSpacing", Length(a.RowSpacing));
                    Add("Rows", a.Rows);
                    Add("AlignItems", a.Align ? 1 : 0);
                    Add("MaintainZ", a.MaintainZ ? 1.0 : 0.0);
                    Add("Orientation", a.Tangent == CadPathTangent.Normal ? 2 : 1);

                    if (a.FromEnd)
                    {
                        Add("PathDirection", 0);
                    }

                    Add("TangentOrientationAngle", a.Tangent == CadPathTangent.Vector ? a.TangentAngle : 0.0);

                    if (a.Divide)
                    {
                        Add("Method", 0);
                        Add("FillPath", 0.0);
                    }

                    break;
            }

            return values;
        }

        /// <summary>A value that is an angle in radians, to be written with the unit AutoCAD gives it.</summary>
        private sealed class AngleValue
        {
            internal double Radians;
        }

        /// <summary>A value that is a distance.</summary>
        private sealed class LengthValue
        {
            internal double Distance;
        }

        private static AngleValue Angle(double radians) => new AngleValue { Radians = radians };

        private static LengthValue Length(double distance) => new LengthValue { Distance = distance };

        /// <summary>One value of the action: its name, its unit (1 a distance, 2 an angle, 0 none) and whether it is there, then the number.</summary>
        private static void Value(CadCenterMarks.Gs g, string name, object value)
        {
            g.G(90, 0);
            g.G(1, name);

            switch (value)
            {
                case int whole:
                    g.G(90, 0);
                    g.G(90, 1);
                    g.G(90, whole);
                    break;
                case double real:
                    g.G(90, 0);
                    g.G(90, 1);
                    g.G(40, real);
                    break;
                case LengthValue distance:
                    g.G(90, 1);
                    g.G(90, 1);
                    g.G(40, distance.Distance);
                    break;
                case AngleValue angle:
                    g.G(90, 2);
                    g.G(90, 1);
                    g.G(40, angle.Radians);
                    break;
            }

            g.G(330, "0");
            g.G(330, "0");
        }
    }
}
