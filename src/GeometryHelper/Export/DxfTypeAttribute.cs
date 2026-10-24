using System;
using System.Collections.Generic;
using System.Reflection;

namespace GeometryHelper.Export
{
    /// <summary>
    /// Marks the method that makes the object of an entity class for a record of a DXF type: an <c>internal static</c> method
    /// of the class that takes the record and gives its object, or null where the record is not of the class, as a POLYLINE
    /// of a mesh is not a <c>DxfPolyline3d</c>.
    /// </summary>
    /// <remarks>
    /// Each class marks its own method, so no list is shared between the classes:
    /// <code>
    /// [DxfType("LINE")]
    /// internal static DxfEntity Create(DxfItem item) => new DxfLine(item);
    /// </code>
    /// A record of a type no method makes an object of, or whose methods all give null, is a <see cref="DxfUnknownEntity"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    internal sealed class DxfTypeAttribute : Attribute
    {
        internal DxfTypeAttribute(string dxfName)
        {
            DxfName = dxfName;
        }

        /// <summary>The type of the records, as group 0 names it: <c>LINE</c>, <c>LWPOLYLINE</c>, <c>INSERT</c>.</summary>
        internal string DxfName { get; }
    }

    /// <summary>
    /// The methods that make the objects of the entity classes, by DXF type, found once among the classes of the library that
    /// mark one with <see cref="DxfTypeAttribute"/>, those of a type in the order of the names of their classes.
    /// </summary>
    internal static class DxfEntityTypes
    {
        private static readonly Dictionary<string, Func<DxfItem, DxfEntity>[]> Makers = Find();

        /// <summary>The object of a record: that of the first method of its type that makes one; null where none does.</summary>
        internal static DxfEntity Create(DxfItem item)
        {
            if (!Makers.TryGetValue(item.Type, out Func<DxfItem, DxfEntity>[] makers))
            {
                return null;
            }

            foreach (Func<DxfItem, DxfEntity> make in makers)
            {
                DxfEntity made = make(item);

                if (made != null)
                {
                    return made;
                }
            }

            return null;
        }

        /// <summary>Whether a class makes the objects of a type.</summary>
        internal static bool IsKnown(string dxfName) => Makers.ContainsKey(dxfName);

        private static Dictionary<string, Func<DxfItem, DxfEntity>[]> Find()
        {
            var found = new SortedDictionary<string, SortedDictionary<string, Func<DxfItem, DxfEntity>>>(StringComparer.Ordinal);

            foreach (Type type in typeof(DxfEntity).Assembly.GetTypes())
            {
                if (!typeof(DxfEntity).IsAssignableFrom(type))
                {
                    continue;
                }

                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    foreach (DxfTypeAttribute mark in method.GetCustomAttributes<DxfTypeAttribute>())
                    {
                        var make = (Func<DxfItem, DxfEntity>)method.CreateDelegate(typeof(Func<DxfItem, DxfEntity>));

                        if (!found.TryGetValue(mark.DxfName, out SortedDictionary<string, Func<DxfItem, DxfEntity>> makers))
                        {
                            makers = new SortedDictionary<string, Func<DxfItem, DxfEntity>>(StringComparer.Ordinal);
                            found.Add(mark.DxfName, makers);
                        }

                        string key = type.FullName + "." + method.Name;

                        if (makers.ContainsKey(key))
                        {
                            throw new InvalidOperationException("The class " + type.FullName + " marks two methods named " + method.Name + " for the type " + mark.DxfName + ".");
                        }

                        makers.Add(key, make);
                    }
                }
            }

            var table = new Dictionary<string, Func<DxfItem, DxfEntity>[]>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, SortedDictionary<string, Func<DxfItem, DxfEntity>>> type in found)
            {
                table.Add(type.Key, new List<Func<DxfItem, DxfEntity>>(type.Value.Values).ToArray());
            }

            return table;
        }
    }
}
