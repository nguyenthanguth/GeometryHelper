using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    internal static partial class CadCenterMarks
    {
        /// <summary>One action read back: its record, the kind of its body, what it is built on and the INSERT it draws.</summary>
        private sealed class Tied
        {
            internal DxfItem Action;
            internal bool IsMark;
            internal List<string> Inputs = new List<string>();
            internal List<string> GeomDependencies = new List<string>();
            internal List<string> Edges = new List<string>();
            internal string InsertHandle;
            internal string InsertDependency;
        }

        /// <summary>Whether a center mark or a center line is tied to a circle, an arc or a line: the drawing's named objects hold the network.</summary>
        private static bool Holds(DxfDatabase database)
        {
            try
            {
                return database.File.HoldsObjects && database.NamedObjectsDictionary.Contains(NetworkKey);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>Whether two segments have a way between them, as <see cref="MakeLine"/> draws one.</summary>
        internal static bool HasWayBetween(Segment one, Segment two) =>
            LineBetween(
                new GeoPoint2(one.A.X, one.A.Y),
                new GeoPoint2(one.B.X, one.B.Y),
                new GeoPoint2(two.A.X, two.A.Y),
                new GeoPoint2(two.B.X, two.B.Y),
                out _,
                out _,
                out _,
                out _
            );

        /// <summary>
        /// The actions of center marks and center lines an entity is tied to, as input or as the INSERT, in the order of its
        /// persistent reactors.
        /// </summary>
        private static List<Tied> ActionsOf(DxfFile file, DxfEntity entity)
        {
            var found = new List<Tied>();

            foreach (string reactor in entity.GetPersistentReactorIds())
            {
                DxfItem item = file.ObjectItem(reactor);

                if (
                    item == null
                    || item.IsRemoved
                    || (item.Type != "ACDBASSOCGEOMDEPENDENCY" && item.Type != "ACDBASSOCDEPENDENCY")
                )
                {
                    continue;
                }

                DxfItem owner = file.ObjectItem(file.OwnerOf(item) ?? string.Empty);

                if (owner == null || owner.IsRemoved || owner.Type != "ACDBASSOCACTION")
                {
                    continue;
                }

                Tied tied = Read(file, owner);

                if (tied != null && !found.Exists(t => ReferenceEquals(t.Action, tied.Action)))
                {
                    found.Add(tied);
                }
            }

            return found;
        }

        /// <summary>An action read; null where its body is none of a center mark or a center line, or it has no INSERT.</summary>
        private static Tied Read(DxfFile file, DxfItem action)
        {
            var tied = new Tied { Action = action };
            bool body = false;

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
                    case MarkBody:
                    case LineBody:
                        body = true;
                        tied.IsMark = item.Type == MarkBody;
                        break;
                    case "ACDBASSOCGEOMDEPENDENCY":
                        tied.GeomDependencies.Add(item.Handle);
                        tied.Inputs.Add(ObjectOf(item));
                        break;
                    case "ACDBASSOCDEPENDENCY":
                        tied.InsertDependency = item.Handle;
                        tied.InsertHandle = ObjectOf(item);
                        break;
                    case "ACDBASSOCEDGEACTIONPARAM":
                        tied.Edges.Add(item.Handle);
                        break;
                }
            }

            return body && tied.InsertHandle != null && tied.Inputs.Count > 0 ? tied : null;
        }

        /// <summary>The handle of the object a dependency is on: the first 330 after its marker.</summary>
        private static string ObjectOf(DxfItem dependency)
        {
            int at = Start(dependency.Groups, "AcDbAssocDependency");
            return at >= 0 && at + 7 < dependency.Groups.Count && dependency.Groups[at + 7].TypeCode == 330
                ? dependency.Groups[at + 7].Value.Trim()
                : null;
        }

        /// <summary>The index of the value group of a named value of an action, -1 where it has none.</summary>
        private static int ValueAt(DxfItem action, string name)
        {
            IReadOnlyList<DxfTypedValue> groups = action.Groups;

            for (int i = 0; i + 3 < groups.Count; i++)
            {
                if (
                    groups[i].TypeCode == 1
                    && groups[i].Value == name
                    && groups[i + 1].TypeCode == 90
                    && groups[i + 2].TypeCode == 90
                )
                {
                    return i + 3;
                }
            }

            return -1;
        }

        private static double RealOf(DxfItem action, string name, double fallback)
        {
            int at = ValueAt(action, name);
            return
                at >= 0
                && double.TryParse(
                    action.Groups[at].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double v
                )
                ? v
                : fallback;
        }

        private static string TextOf(DxfItem action, string name, string fallback)
        {
            int at = ValueAt(action, name);
            return at >= 0 ? action.Groups[at].Value : fallback;
        }

        private static void SetReal(DxfFile file, DxfItem action, string name, double value)
        {
            int at = ValueAt(action, name);

            if (at >= 0)
            {
                file.SetValueAt(action, at, Export.Numbers.Exact(value + 0.0));
            }
        }

        /// <summary>
        /// Follows an edit: each center mark or center line whose circle, arc or lines the edit changed is drawn again (its
        /// INSERT put at the middle, its lines made for the new radius or the new lines, with the sizes it was made with), and
        /// each whose INSERT the edit moved, turned, scaled or reshaped without them is untied, as AutoCAD 2021 disassociates a
        /// mark moved on its own (it stays where it is moved to and no longer follows the circle); one whose INSERT was erased
        /// is taken out. Returns the records of the INSERTs changed, for the edit's step.
        /// </summary>
        internal static List<CadEditRecord> Follow(
            CadDocument document,
            IReadOnlyList<CadEditRecord> planned,
            IReadOnlyList<CadEditRecord> made
        )
        {
            var followed = new List<CadEditRecord>();

            if (!Holds(document.Database))
            {
                return followed;
            }

            DxfFile file = document.Database.File;
            var moved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var erased = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (IReadOnlyList<CadEditRecord> records in new[] { planned, made })
            {
                foreach (CadEditRecord record in records)
                {
                    if (record.Handle == null)
                    {
                        continue;
                    }

                    switch (record.Kind)
                    {
                        case CadEditRecordKind.Transformed:
                        case CadEditRecordKind.Reshaped:
                            moved.Add(record.Handle);
                            changed.Add(record.Handle);
                            break;
                        case CadEditRecordKind.PropertyChanged:
                            changed.Add(record.Handle);
                            break;
                        case CadEditRecordKind.Erased:
                            erased.Add(record.Handle);
                            break;
                    }
                }
            }

            var actions = new SortedDictionary<string, Tied>(StringComparer.Ordinal);

            foreach (string handle in changed.Concat(erased).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                DxfEntity entity = document.FindKept(handle);

                if (entity == null)
                {
                    continue;
                }

                foreach (Tied tied in ActionsOf(file, entity))
                {
                    actions[tied.Action.Handle] = tied;
                }
            }

            foreach (Tied tied in actions.Values)
            {
                string space = document.FindSpace(tied.InsertHandle)?.Name;

                if (erased.Contains(tied.InsertHandle))
                {
                    Release(document, tied);
                    continue;
                }

                if (!(document.FindEntity(tied.InsertHandle) is DxfBlockReference insert))
                {
                    continue;
                }

                bool inputsChanged = tied.Inputs.Exists(h => changed.Contains(h) || erased.Contains(h));

                if (!inputsChanged)
                {
                    if (moved.Contains(tied.InsertHandle))
                    {
                        Release(document, tied);
                    }

                    continue;
                }

                if (Refresh(document, tied, insert))
                {
                    followed.Add(new CadEditRecord(CadEditRecordKind.Reshaped, insert.Handle) { Space = space });
                }
            }

            return followed;
        }

        /// <summary>Draws a mark or a line again from its circle or lines: false where an input is gone or the drawing is not as it was made.</summary>
        private static bool Refresh(CadDocument document, Tied tied, DxfBlockReference insert)
        {
            DxfFile file = document.Database.File;
            List<DxfEntity> inputs = tied.Inputs.Select(h => document.FindEntity(h)).ToList();

            if (inputs.Exists(e => e == null))
            {
                return false;
            }

            DxfBlockTableRecord block = insert.BlockTableRecord;
            var lines = new List<DxfLine>();

            foreach (DxfEntity entity in block)
            {
                if (entity is DxfLine line)
                {
                    lines.Add(line);
                }
            }

            DxfItem action = tied.Action;
            double overshoot = RealOf(action, tied.IsMark ? "HorizontalStartOvershoot" : "StartOvershoot", 0.12);

            if (tied.IsMark)
            {
                GeoPoint3 centre;
                GeoVector3 normal;
                double radius;
                double start = 0.0;
                double end = 2.0 * Math.PI;

                switch (inputs[0])
                {
                    case DxfArc arc:
                        centre = arc.Center;
                        normal = arc.Normal;
                        radius = arc.Radius;
                        start = arc.StartAngle;
                        end = arc.EndAngle;
                        break;
                    case DxfCircle circle:
                        centre = circle.Center;
                        normal = circle.Normal;
                        radius = circle.Radius;
                        break;
                    default:
                        return false;
                }

                List<Stroke> strokes = MarkStrokes(
                    TextOf(action, "CrossHairLengthExpression", "0.1x"),
                    TextOf(action, "CrossHairGapExpression", "0.05x"),
                    overshoot,
                    radius
                );

                if (lines.Count != strokes.Count)
                {
                    return false;
                }

                Place(insert, centre, normal);

                for (int i = 0; i < lines.Count; i++)
                {
                    lines[i].StartPoint = strokes[i].A;
                    lines[i].EndPoint = strokes[i].B;
                }

                SetReal(file, action, "CachedCircleRadius", radius);
                GeoVector3 axis = AxisOf(normal);
                var numbers = new List<double>
                {
                    centre.X,
                    centre.Y,
                    centre.Z,
                    normal.X,
                    normal.Y,
                    normal.Z,
                    axis.X,
                    axis.Y,
                    axis.Z,
                    radius,
                    start,
                    end,
                    0.0,
                };
                WriteEdge(file, tied.Edges[0], numbers);
                return true;
            }

            if (inputs.Count != 2 || lines.Count != 1)
            {
                return false;
            }

            var segments = new Segment[2];

            for (int i = 0; i < 2; i++)
            {
                if (!SegmentOf(file, tied.GeomDependencies[i], inputs[i], out segments[i]))
                {
                    return false;
                }
            }

            if (
                !LineBetween(
                    new GeoPoint2(segments[0].A.X, segments[0].A.Y),
                    new GeoPoint2(segments[0].B.X, segments[0].B.Y),
                    new GeoPoint2(segments[1].A.X, segments[1].A.Y),
                    new GeoPoint2(segments[1].B.X, segments[1].B.Y),
                    out GeoPoint2 middle,
                    out GeoVector2 direction,
                    out double first,
                    out double last
                )
            )
            {
                return false;
            }

            double out0 = first <= last ? -Math.Max(0.0, overshoot) : Math.Max(0.0, overshoot);
            Place(insert, new GeoPoint3(middle.X, middle.Y, segments[0].A.Z), new GeoVector3(0.0, 0.0, 1.0));
            lines[0].StartPoint = new GeoPoint3(direction.X * (first + out0), direction.Y * (first + out0), 0.0);
            lines[0].EndPoint = new GeoPoint3(direction.X * (last - out0), direction.Y * (last - out0), 0.0);
            SetReal(file, action, "CachedCenterLineLength", Math.Abs(last - first));
            SetReal(file, action, "CachedCenterLineDirectionX", direction.X);
            SetReal(file, action, "CachedCenterLineDirectionY", direction.Y);

            for (int i = 0; i < 2; i++)
            {
                WriteEdge(
                    file,
                    tied.Edges[i],
                    new List<double>
                    {
                        segments[i].A.X,
                        segments[i].A.Y,
                        segments[i].A.Z,
                        segments[i].B.X - segments[i].A.X,
                        segments[i].B.Y - segments[i].A.Y,
                        segments[i].B.Z - segments[i].A.Z,
                    }
                );
            }

            return true;
        }

        /// <summary>Puts a mark's INSERT where it is drawn: at the centre, upright and unscaled, in the plane of the circle.</summary>
        private static void Place(DxfBlockReference insert, GeoPoint3 position, GeoVector3 normal)
        {
            insert.Position = position;
            insert.Normal = normal;
            insert.ScaleFactors = new GeoVector3(1.0, 1.0, 1.0);
            insert.Rotation = 0.0;
        }

        /// <summary>The segment a dependency names on a line or a polyline now: a line whole, a polyline's segment by the marker the dependency's subentity names.</summary>
        private static bool SegmentOf(DxfFile file, string dependency, DxfEntity entity, out Segment segment)
        {
            segment = default;

            if (entity is DxfLine line)
            {
                segment = new Segment(entity, line.StartPoint, line.EndPoint, -1);
                return true;
            }

            if (!(entity is DxfPolyline polyline) || !(file.ObjectItem(dependency) is DxfItem item))
            {
                return false;
            }

            IReadOnlyList<DxfTypedValue> groups = item.Groups;
            int marker = -1;

            for (int i = 0; i + 3 < groups.Count; i++)
            {
                if (
                    groups[i].TypeCode == 1
                    && groups[i].Value == "AcDbAssocEdgePersSubentId"
                    && int.TryParse(groups[i + 2].Value.Trim(), out int id)
                )
                {
                    marker = id - 1;
                    break;
                }
            }

            int count = polyline.NumberOfVertices;

            if (marker < 0 || marker >= (polyline.Closed ? count : count - 1))
            {
                return false;
            }

            GeoPoint2 a = polyline.GetPoint2dAt(marker);
            GeoPoint2 b = polyline.GetPoint2dAt((marker + 1) % count);
            segment = new Segment(
                entity,
                new GeoPoint3(a.X, a.Y, polyline.Elevation),
                new GeoPoint3(b.X, b.Y, polyline.Elevation),
                marker
            );
            return true;
        }

        /// <summary>Writes the numbers of an edge parameter again: those after the group 90 that names its kind (11 or 23).</summary>
        private static void WriteEdge(DxfFile file, string edge, IReadOnlyList<double> numbers)
        {
            DxfItem item = file.ObjectItem(edge);

            if (item == null || item.IsRemoved)
            {
                return;
            }

            IReadOnlyList<DxfTypedValue> groups = item.Groups;
            int at = Start(groups, "AcDbAssocEdgeActionParam");

            if (at < 0 || at + 4 >= groups.Count)
            {
                return;
            }

            // The groups after the marker: 90, 330, 290, 90 (the kind), then the points in threes and the numbers.
            int number = 0;

            for (int i = at + 5; i < groups.Count && number < numbers.Count; i++)
            {
                int code = groups[i].TypeCode;

                if (code == 10 || code == 20 || code == 30 || code == 40)
                {
                    file.SetValueAt(item, i, Export.Numbers.Exact(numbers[number++] + 0.0));
                }
            }
        }

        /// <summary>
        /// Takes the action of a mark or a line out, as AutoCAD 2021 does where its INSERT is erased: the reactors it set on the
        /// INSERT and on its circles or lines taken off, the dependencies linked out of the chains on those objects, its objects
        /// erased and it taken out of its network's list, and the network, the extension dictionary and the dictionaries that
        /// held it where it was the last.
        /// </summary>
        private static void Release(CadDocument document, Tied tied)
        {
            DxfDatabase database = document.Database;
            DxfFile file = database.File;

            foreach (string dependency in tied.GeomDependencies)
            {
                DxfItem item = file.ObjectItem(dependency);

                if (item == null || item.IsRemoved)
                {
                    continue;
                }

                string target = ObjectOf(item);
                int at = Start(item.Groups, "AcDbAssocDependency");

                if (at >= 0 && at + 10 < item.Groups.Count)
                {
                    string previous = item.Groups[at + 9].Value.Trim();
                    string next = item.Groups[at + 10].Value.Trim();
                    Relink(file, previous, 10, next);
                    Relink(file, next, 9, previous);
                }

                if (target != null && file.ObjectItem(target) is DxfItem on && !on.IsRemoved)
                {
                    file.SetReactor(on, dependency, false);
                }
            }

            if (
                file.ObjectItem(tied.InsertHandle) is DxfItem insert
                && !insert.IsRemoved
                && tied.InsertDependency != null
            )
            {
                file.SetReactor(insert, tied.InsertDependency, false);
            }

            DxfItem network = file.ObjectItem(file.OwnerOf(tied.Action) ?? string.Empty);

            if (network != null && !network.IsRemoved)
            {
                Unlist(file, network, tied.Action.Handle);
            }

            var doomed = new List<string>(tied.Edges);
            doomed.AddRange(tied.GeomDependencies);

            if (tied.InsertDependency != null)
            {
                doomed.Add(tied.InsertDependency);
            }

            foreach (DxfTypedValue group in tied.Action.Groups)
            {
                if (
                    group.TypeCode == 360
                    && file.ObjectItem(group.Value.Trim()) is DxfItem body
                    && body.Type.StartsWith("AcDbCenter", StringComparison.Ordinal)
                )
                {
                    doomed.Add(body.Handle);
                }
            }

            foreach (string handle in doomed)
            {
                if (file.ObjectItem(handle) is DxfItem item && !item.IsRemoved)
                {
                    file.RemoveRecord(item);
                }
            }

            if (!tied.Action.IsRemoved)
            {
                file.RemoveRecord(tied.Action);
            }

            if (network != null && !network.IsRemoved && ActionsOf(network).Count == 0)
            {
                ReleaseNetwork(document, file, network);
            }
        }

        private static void Relink(DxfFile file, string handle, int slot, string to)
        {
            if (handle == "0" || !(file.ObjectItem(handle) is DxfItem other) || other.IsRemoved)
            {
                return;
            }

            int at = Start(other.Groups, "AcDbAssocDependency");

            if (at >= 0 && at + slot < other.Groups.Count)
            {
                file.SetValueAt(other, at + slot, to);
            }
        }

        /// <summary>Takes out a network that holds no action: out of the list of the network of the drawing, with the dictionaries round it, and the extension dictionary of the space where that holds nothing else.</summary>
        private static void ReleaseNetwork(CadDocument document, DxfFile file, DxfItem network)
        {
            DxfDatabase database = document.Database;
            DxfItem holder = file.ObjectItem(file.OwnerOf(network) ?? string.Empty);
            DxfItem wrapper = holder == null ? null : file.ObjectItem(file.OwnerOf(holder) ?? string.Empty);
            DxfItem owner = wrapper == null ? null : file.ObjectItem(file.OwnerOf(wrapper) ?? string.Empty);
            DxfItem top = file.ObjectItem(OwnerNetwork(network) ?? string.Empty);

            if (top != null && !top.IsRemoved)
            {
                Unlist(file, top, network.Handle);
            }

            if (DxfDBObject.OfObject(holder) is DxfDBDictionary held && held.Contains(NetworkKey))
            {
                held.Remove(NetworkKey);
            }

            if (wrapper != null && DxfDBObject.OfObject(wrapper) is DxfDBDictionary wrap && wrap.Contains(NetworkKey))
            {
                wrap.Remove(NetworkKey);
            }

            if (owner != null && DxfDBObject.OfObject(owner) is DxfDBObject block)
            {
                DxfDBDictionary extension = block.ExtensionDictionary;

                if (extension != null && extension.Count == 0)
                {
                    block.ReleaseExtensionDictionary();
                }
            }

            if (top != null && !top.IsRemoved && ActionsOf(top).Count == 0)
            {
                DxfItem rootWrapper = file.ObjectItem(file.OwnerOf(top) ?? string.Empty);
                DxfDBDictionary root = database.NamedObjectsDictionary;

                if (
                    rootWrapper != null
                    && DxfDBObject.OfObject(rootWrapper) is DxfDBDictionary inner
                    && inner.Contains(NetworkKey)
                )
                {
                    inner.Remove(NetworkKey);
                }

                if (root.Contains(NetworkKey))
                {
                    root.Remove(NetworkKey);
                }
            }
        }

        /// <summary>The handle of the network a network is listed in: the 330 of its first group after its marker AcDbAssocAction that names one.</summary>
        private static string OwnerNetwork(DxfItem network)
        {
            int at = Start(network.Groups, "AcDbAssocAction");
            return at >= 0 && at + 2 < network.Groups.Count && network.Groups[at + 2].TypeCode == 330
                ? network.Groups[at + 2].Value.Trim()
                : null;
        }
    }
}
