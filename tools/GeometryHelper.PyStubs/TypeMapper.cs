using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace GeometryHelper.PyStubs
{
    /// <summary>
    /// Turns a .NET type into the Python annotation that describes it, by the table of the stubs: the numbers and
    /// strings become <c>int</c>, <c>float</c> and <c>str</c>, the collections become the classes of
    /// <c>System-stubs</c> (the interface pythonnet really hands over, not a Python list), and a type the table does not
    /// know becomes <c>typing.Any</c> and is remembered, so the summary can list it.
    /// </summary>
    internal sealed class TypeMapper
    {
        private static readonly Regex DelegateName = new Regex(@"^System\.(Action|Func)`\d+$", RegexOptions.Compiled);

        /// <summary>The types that have a class of their own in <c>System-stubs</c>.</summary>
        private static readonly HashSet<string> StubClasses = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Attribute",
            "System.Collections.IEnumerable",
            "System.Collections.IEnumerator",
            "System.DateTime",
            "System.Enum",
            "System.EventArgs",
            "System.Exception",
            "System.IConvertible",
            "System.IDisposable",
            "System.IFormattable",
            "System.IO.Stream",
            "System.IO.TextWriter",
            "System.Reflection.Assembly",
            "System.Runtime.Serialization.ISerializable",
            "System.Threading.CancellationToken",
            "System.TimeSpan",
            "System.Type",
            "System.ValueType",
            "System.Version",
        };

        /// <summary>The generic types that have a class of their own in <c>System-stubs</c>, written <c>Name[T]</c>.</summary>
        private static readonly HashSet<string> GenericStubClasses = new HashSet<string>(StringComparer.Ordinal)
        {
            "System.Collections.Generic.Dictionary`2",
            "System.Collections.Generic.HashSet`1",
            "System.Collections.Generic.ICollection`1",
            "System.Collections.Generic.IComparer`1",
            "System.Collections.Generic.IDictionary`2",
            "System.Collections.Generic.IEnumerable`1",
            "System.Collections.Generic.IEnumerator`1",
            "System.Collections.Generic.IList`1",
            "System.Collections.Generic.IReadOnlyCollection`1",
            "System.Collections.Generic.IReadOnlyDictionary`2",
            "System.Collections.Generic.IReadOnlyList`1",
            "System.Collections.Generic.ISet`1",
            "System.Collections.Generic.KeyValuePair`2",
            "System.Collections.Generic.List`1",
            "System.Collections.Generic.SortedDictionary`2",
            "System.EventHandler`1",
            "System.IComparable`1",
            "System.IEquatable`1",
            "System.IProgress`1",
            "System.ValueTuple`2",
        };

        private readonly Func<Type, bool> _isOurs;
        private readonly SortedSet<string> _anyMapped = new SortedSet<string>(StringComparer.Ordinal);

        public TypeMapper(Func<Type, bool> isOurs)
        {
            _isOurs = isOurs;
        }

        /// <summary>The .NET types that were turned into <c>typing.Any</c>, by full name, sorted.</summary>
        public IReadOnlyCollection<string> AnyMapped => _anyMapped;

        /// <summary>Whether this .NET type has a class of its own in <c>System-stubs</c> that a stub class can derive from.</summary>
        public static bool IsStubClass(Type type) => type.FullName != null && StubClasses.Contains(type.FullName);

        /// <summary>
        /// The annotation for <paramref name="type"/>. A parameter (<paramref name="asParameter"/>) takes what pythonnet
        /// converts for it: a Python list or tuple for an array, and nothing but the .NET class for any collection (a
        /// Python list is refused for <c>IEnumerable&lt;T&gt;</c>, <c>IReadOnlyList&lt;T&gt;</c> and the others, measured);
        /// a return or a property gives the .NET class itself. A by-reference type is taken as the type it points to.
        /// </summary>
        public string Map(Type type, bool asParameter, ModuleContext module)
        {
            if (type.IsByRef)
            {
                type = type.GetElementType();
            }

            if (type.IsGenericParameter)
            {
                return module.Typing("Any");
            }

            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1)
                {
                    return AnyOf(type.FullName, module);
                }

                string element = Map(type.GetElementType(), asParameter, module);
                string array = module.Bcl("System", "Array") + "[" + element + "]";
                return asParameter ? module.Typing("Sequence") + "[" + element + "] | " + array : array;
            }

            if (type.IsPointer)
            {
                return AnyOf(type.ToString(), module);
            }

            if (_isOurs(type))
            {
                return type.IsGenericType ? AnyOf(type.FullName, module) : module.Ours(type);
            }

            return type.IsGenericType ? MapGeneric(type, asParameter, module) : MapPlain(type, asParameter, module);
        }

        private string MapPlain(Type type, bool asParameter, ModuleContext module)
        {
            string full = type.FullName;
            switch (full)
            {
                case "System.Boolean":
                    return "bool";
                case "System.Byte":
                case "System.SByte":
                case "System.Int16":
                case "System.UInt16":
                case "System.Int32":
                case "System.UInt32":
                case "System.Int64":
                case "System.UInt64":
                    return "int";
                case "System.Single":
                case "System.Double":
                case "System.Decimal":
                    return "float";
                case "System.String":
                case "System.Char":
                    return "str";
                case "System.Void":
                    return "None";
                case "System.Object":
                    return asParameter ? "object" : module.Typing("Any");
                case "System.Action":
                    return module.Bcl("System", "Action") + "[()]";
                case "System.EventHandler":
                    // One class stands for both EventHandler and EventHandler<T>, so the plain one is the one of EventArgs.
                    return module.Bcl("System", "EventHandler") + "[" + module.Bcl("System", "EventArgs") + "]";
                case "System.IComparable":
                    return module.Bcl("System", "IComparable") + "[" + module.Typing("Any") + "]";
            }

            if (full != null && StubClasses.Contains(full))
            {
                return module.Bcl(type.Namespace, type.Name);
            }

            return AnyOf(full ?? type.Name, module);
        }

        private string MapGeneric(Type type, bool asParameter, ModuleContext module)
        {
            Type definition = type.GetGenericTypeDefinition();
            string full = definition.FullName;
            Type[] arguments = type.GetGenericArguments();

            if (full == "System.Nullable`1")
            {
                return Map(arguments[0], asParameter, module) + " | None";
            }

            string name = definition.Name.Substring(0, definition.Name.IndexOf('`'));
            if (DelegateName.IsMatch(full))
            {
                // pythonnet refuses a Python function for a delegate parameter or property: it has to be made into one,
                // Action[int](f), so the stub names the delegate class, which takes the function. The arguments are
                // the .NET ones (a type argument is no place for the Python sequence that a parameter would take).
                return module.Bcl("System", name)
                    + "["
                    + string.Join(", ", arguments.Select(a => Map(a, false, module)))
                    + "]";
            }

            if (GenericStubClasses.Contains(full))
            {
                return module.Bcl(definition.Namespace, name)
                    + "["
                    + string.Join(", ", arguments.Select(a => Map(a, asParameter, module)))
                    + "]";
            }

            return AnyOf(full, module);
        }

        private string AnyOf(string name, ModuleContext module)
        {
            _anyMapped.Add(name);
            return module.Typing("Any");
        }
    }
}
