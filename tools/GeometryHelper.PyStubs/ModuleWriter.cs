using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace GeometryHelper.PyStubs
{
    /// <summary>
    /// Writes the stub text of one namespace: its classes, structs, interfaces and enums, sorted by name, each with its
    /// members in the order of the stubs (fields and enum members, constructors, properties, indexers, events, methods,
    /// operators), every list sorted so the text is the same on every run.
    /// </summary>
    internal sealed class ModuleWriter
    {
        private const BindingFlags Declared =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private const int MaxSignatureWidth = 120;
        private const int FieldGroup = 0;
        private const int ConstructorGroup = 1;
        private const int PropertyGroup = 2;
        private const int IndexerGroup = 3;
        private const int EventGroup = 4;
        private const int MethodGroup = 5;
        private const int OperatorGroup = 6;

        private readonly ModuleContext _module;
        private readonly TypeMapper _mapper;
        private readonly DocLibrary _docs;
        private readonly Func<Type, bool> _isOurs;
        private readonly List<string> _leftOut;

        public ModuleWriter(
            ModuleContext module,
            TypeMapper mapper,
            DocLibrary docs,
            Func<Type, bool> isOurs,
            List<string> leftOut
        )
        {
            _module = module;
            _mapper = mapper;
            _docs = docs;
            _isOurs = isOurs;
            _leftOut = leftOut;
        }

        /// <summary>One member as it will be written: where it sorts, and how to write it once it is known whether its name is overloaded.</summary>
        private sealed class Piece
        {
            public int Group;
            public string Name;
            public int ParameterCount;
            public string ParameterKey = "";
            public string ReturnKey = "";
            public bool Overloadable;
            public bool Counts = true;
            public Func<bool, string> Render;
        }

        /// <summary>The text of one type, ending in a line feed, and how many members of it were written.</summary>
        public string WriteType(Type type, out int members)
        {
            var pieces = new List<Piece>();
            if (type.IsEnum)
            {
                CollectEnum(type, pieces);
            }
            else if (type.BaseType?.FullName == "System.MulticastDelegate")
            {
                CollectDelegate(type, pieces);
            }
            else
            {
                CollectFields(type, pieces);
                CollectConstructors(type, pieces);
                CollectProperties(type, pieces);
                CollectEvents(type, pieces);
                CollectMethods(type, pieces);
                CollectDisposable(type, pieces);
                CollectIteration(type, pieces);
            }

            List<Piece> sorted = pieces
                .OrderBy(p => p.Group)
                .ThenBy(p => p.Name, StringComparer.Ordinal)
                .ThenBy(p => p.ParameterCount)
                .ThenBy(p => p.ParameterKey, StringComparer.Ordinal)
                .ThenBy(p => p.ReturnKey, StringComparer.Ordinal)
                .ToList();
            members = sorted.Count(p => p.Counts);

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Piece piece in sorted.Where(p => p.Overloadable))
            {
                counts[piece.Name] = counts.TryGetValue(piece.Name, out int n) ? n + 1 : 1;
            }

            var text = new StringBuilder();
            text.Append("class ").Append(type.Name).Append('(').Append(string.Join(", ", Bases(type))).Append("):");
            IReadOnlyList<string> doc = _docs.Of(type);
            if (doc.Count == 0 && sorted.Count == 0)
            {
                text.Append(" ...\n");
                return text.ToString();
            }

            text.Append('\n');
            AppendDocstring(text, doc, 4);
            foreach (Piece piece in sorted)
            {
                text.Append(piece.Render(piece.Overloadable && counts[piece.Name] > 1));
            }

            return text.ToString();
        }

        #region Bases

        private List<string> Bases(Type type)
        {
            if (type.IsEnum)
            {
                return new List<string> { _module.Bcl("System", "Enum") };
            }

            var bases = new List<string>();
            Type parent = type.BaseType;
            if (type.IsValueType)
            {
                bases.Add(_module.Bcl("System", "ValueType"));
            }
            else if (parent != null && _isOurs(parent))
            {
                bases.Add(_module.Ours(parent));
            }
            else
            {
                while (parent != null && !_isOurs(parent) && !TypeMapper.IsStubClass(parent))
                {
                    parent = parent.BaseType;
                }

                if (parent != null && parent.FullName != "System.Object")
                {
                    bases.Add(_module.Bcl(parent.Namespace, parent.Name));
                }
            }

            bases.AddRange(
                NewInterfaces(type)
                    .OrderBy(i => i.Name, StringComparer.Ordinal)
                    .ThenBy(i => i.FullName, StringComparer.Ordinal)
                    .Select(i => _module.Ours(i))
            );
            if (bases.Count == 0)
            {
                bases.Add(_module.Bcl("System", "Object"));
            }

            return bases;
        }

        /// <summary>
        /// The interfaces of ours that <paramref name="type"/> names as bases: those it implements, less those its base
        /// class already has and those another of its interfaces already brings, so the bases cannot disagree on an order.
        /// </summary>
        private List<Type> NewInterfaces(Type type)
        {
            List<Type> all = type.GetInterfaces().Where(_isOurs).ToList();
            HashSet<Type> inherited =
                type.BaseType != null && _isOurs(type.BaseType)
                    ? new HashSet<Type>(type.BaseType.GetInterfaces())
                    : new HashSet<Type>();
            return all.Where(i => !inherited.Contains(i) && !all.Any(j => j != i && j.GetInterfaces().Contains(i)))
                .ToList();
        }

        #endregion

        #region Enums, delegates and fields

        /// <summary>
        /// A delegate type of ours is a class made from a Python function, <c>CadSnapFinder(f)</c>, since pythonnet refuses
        /// a bare function where a delegate is wanted, and called as the function. Its one member is <c>Invoke</c>.
        /// </summary>
        private void CollectDelegate(Type type, List<Piece> pieces)
        {
            MethodInfo invoke = type.GetMethod("Invoke");
            ParameterInfo[] parameters = invoke.GetParameters();
            pieces.Add(
                new Piece
                {
                    Group = ConstructorGroup,
                    Name = "__init__",
                    Counts = false,
                    Render = overloaded =>
                    {
                        // What the function is handed flows the other way from what it gives back.
                        string handed = string.Join(
                            ", ",
                            parameters.Select(p => _mapper.Map(p.ParameterType, false, _module))
                        );
                        string function =
                            _module.Typing("Callable")
                            + "[["
                            + handed
                            + "], "
                            + _mapper.Map(invoke.ReturnType, true, _module)
                            + "]";
                        return Def(
                            4,
                            "__init__",
                            new List<string> { "self", "function: " + function },
                            "None",
                            null,
                            false,
                            false
                        );
                    },
                }
            );
            pieces.Add(
                new Piece
                {
                    Group = MethodGroup,
                    Name = "__call__",
                    Render = overloaded =>
                        Def(
                            4,
                            "__call__",
                            Parameters(parameters, true),
                            ReturnAnnotation(invoke.ReturnType, parameters),
                            _docs.Of(invoke),
                            false,
                            false
                        ),
                }
            );
        }

        private void CollectEnum(Type type, List<Piece> pieces)
        {
            CollectFields(type, pieces);
            pieces.Add(
                new Piece
                {
                    Group = MethodGroup,
                    Name = "__int__",
                    Counts = false,
                    Render = overloaded => Def(4, "__int__", new List<string> { "self" }, "int", null, false, false),
                }
            );

            // Only a [Flags] enum has the bit operators in pythonnet, each between two of the enum (not with an int); on
            // another enum they overflow the stack.
            if (type.CustomAttributes.Any(a => a.AttributeType.FullName == "System.FlagsAttribute"))
            {
                foreach (string name in new[] { "__and__", "__or__", "__xor__" })
                {
                    string captured = name;
                    pieces.Add(
                        new Piece
                        {
                            Group = MethodGroup,
                            Name = name,
                            Counts = false,
                            Render = overloaded =>
                                Def(
                                    4,
                                    captured,
                                    new List<string> { "self", "other: " + _module.Ours(type) },
                                    _module.Ours(type),
                                    null,
                                    false,
                                    false
                                ),
                        }
                    );
                }

                pieces.Add(
                    new Piece
                    {
                        Group = MethodGroup,
                        Name = "__invert__",
                        Counts = false,
                        Render = overloaded =>
                            Def(4, "__invert__", new List<string> { "self" }, _module.Ours(type), null, false, false),
                    }
                );
            }
        }

        private void CollectFields(Type type, List<Piece> pieces)
        {
            foreach (FieldInfo field in type.GetFields(Declared))
            {
                if (field.IsSpecialName || !CanWrite(type, field, field.Name))
                {
                    continue;
                }

                FieldInfo captured = field;
                pieces.Add(
                    new Piece
                    {
                        Group = FieldGroup,
                        Name = field.Name,
                        Render = overloaded =>
                            AttributeText(
                                captured.Name,
                                captured.IsStatic,
                                captured.FieldType,
                                false,
                                _docs.Of(captured)
                            ),
                    }
                );
            }
        }

        /// <summary>Attribute line (a field, a static property or event): <c>Name: T</c> or <c>Name: ClassVar[T]</c>, then its docstring.</summary>
        private string AttributeText(string name, bool isStatic, Type type, bool asParameter, IReadOnlyList<string> doc)
        {
            string annotation = _mapper.Map(type, asParameter, _module);
            var text = new StringBuilder();
            text.Append("    ")
                .Append(name)
                .Append(": ")
                .Append(isStatic ? _module.Typing("ClassVar") + "[" + annotation + "]" : annotation)
                .Append('\n');
            AppendDocstring(text, doc, 4);
            return text.ToString();
        }

        #endregion

        #region Constructors, properties, events

        private void CollectConstructors(Type type, List<Piece> pieces)
        {
            foreach (
                ConstructorInfo constructor in type.GetConstructors(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly
                )
            )
            {
                ConstructorInfo captured = constructor;
                ParameterInfo[] parameters = constructor.GetParameters();
                pieces.Add(
                    new Piece
                    {
                        Group = ConstructorGroup,
                        Name = "__init__",
                        ParameterCount = parameters.Length,
                        ParameterKey = ParameterKey(parameters),
                        Overloadable = true,
                        Render = overloaded =>
                            Def(
                                4,
                                "__init__",
                                Parameters(parameters, true),
                                "None",
                                _docs.Of(captured),
                                overloaded,
                                false
                            ),
                    }
                );
                if (HasParamArray(parameters))
                {
                    pieces.Add(
                        new Piece
                        {
                            Group = ConstructorGroup,
                            Name = "__init__",
                            ParameterCount = parameters.Length,
                            ParameterKey = ParameterKey(parameters) + "*",
                            Overloadable = true,
                            Counts = false,
                            Render = overloaded =>
                                Def(
                                    4,
                                    "__init__",
                                    Parameters(parameters, true, true),
                                    "None",
                                    _docs.Of(captured),
                                    overloaded,
                                    false
                                ),
                        }
                    );
                }
            }
        }

        private void CollectProperties(Type type, List<Piece> pieces)
        {
            foreach (PropertyInfo property in type.GetProperties(Declared))
            {
                MethodInfo getter =
                    property.GetMethod != null && property.GetMethod.IsPublic ? property.GetMethod : null;
                MethodInfo setter =
                    property.SetMethod != null && property.SetMethod.IsPublic ? property.SetMethod : null;
                MethodInfo any = getter ?? setter;
                if (any == null)
                {
                    continue;
                }

                ParameterInfo[] index = property.GetIndexParameters();
                if (!CanWrite(type, property, property.Name))
                {
                    continue;
                }

                PropertyInfo captured = property;
                if (index.Length > 0)
                {
                    CollectIndexer(captured, index, getter != null, setter != null, pieces);
                }
                else if (any.IsStatic || getter == null)
                {
                    // A static property is a class variable. A property that can only be set is written as a plain
                    // attribute, since a Python property needs a getter and a stub must not let it be read as one.
                    bool isStatic = any.IsStatic;
                    pieces.Add(
                        new Piece
                        {
                            Group = PropertyGroup,
                            Name = property.Name,
                            Render = overloaded =>
                                AttributeText(
                                    captured.Name,
                                    isStatic,
                                    captured.PropertyType,
                                    getter == null,
                                    _docs.Of(captured)
                                ),
                        }
                    );
                }
                else
                {
                    bool settable = setter != null;
                    pieces.Add(
                        new Piece
                        {
                            Group = PropertyGroup,
                            Name = property.Name,
                            Render = overloaded =>
                                PropertyText(captured.Name, captured.PropertyType, settable, _docs.Of(captured)),
                        }
                    );
                }
            }
        }

        private string PropertyText(string name, Type type, bool settable, IReadOnlyList<string> doc)
        {
            var text = new StringBuilder();
            text.Append("    @property\n");
            text.Append(
                Def(4, name, new List<string> { "self" }, _mapper.Map(type, false, _module), doc, false, false)
            );
            if (settable)
            {
                text.Append("    @").Append(name).Append(".setter\n");
                text.Append(
                    Def(
                        4,
                        name,
                        new List<string> { "self", "value: " + _mapper.Map(type, true, _module) },
                        "None",
                        null,
                        false,
                        false
                    )
                );
            }

            return text.ToString();
        }

        private void CollectIndexer(
            PropertyInfo property,
            ParameterInfo[] index,
            bool hasGetter,
            bool hasSetter,
            List<Piece> pieces
        )
        {
            if (hasGetter)
            {
                pieces.Add(
                    new Piece
                    {
                        Group = IndexerGroup,
                        Name = "__getitem__",
                        ParameterCount = index.Length,
                        ParameterKey = ParameterKey(index),
                        Overloadable = true,
                        Render = overloaded =>
                            Def(
                                4,
                                "__getitem__",
                                Parameters(index, true),
                                _mapper.Map(property.PropertyType, false, _module),
                                _docs.Of(property),
                                overloaded,
                                false
                            ),
                    }
                );
            }

            if (hasSetter)
            {
                pieces.Add(
                    new Piece
                    {
                        Group = IndexerGroup,
                        Name = "__setitem__",
                        ParameterCount = index.Length,
                        ParameterKey = ParameterKey(index),
                        Overloadable = true,
                        Render = overloaded =>
                        {
                            List<string> parameters = Parameters(index, true);
                            parameters.Add("value: " + _mapper.Map(property.PropertyType, true, _module));
                            return Def(4, "__setitem__", parameters, "None", _docs.Of(property), overloaded, false);
                        },
                    }
                );
            }
        }

        private void CollectEvents(Type type, List<Piece> pieces)
        {
            foreach (EventInfo eventInfo in type.GetEvents(Declared))
            {
                if (
                    eventInfo.AddMethod == null
                    || !eventInfo.AddMethod.IsPublic
                    || !CanWrite(type, eventInfo, eventInfo.Name)
                )
                {
                    continue;
                }

                EventInfo captured = eventInfo;
                pieces.Add(
                    new Piece
                    {
                        Group = EventGroup,
                        Name = eventInfo.Name,
                        Render = overloaded =>
                            captured.AddMethod.IsStatic
                                ? AttributeText(
                                    captured.Name,
                                    true,
                                    captured.EventHandlerType,
                                    false,
                                    _docs.Of(captured)
                                )
                                : PropertyText(captured.Name, captured.EventHandlerType, true, _docs.Of(captured)),
                    }
                );
            }
        }

        #endregion

        #region Methods and operators

        private void CollectMethods(Type type, List<Piece> pieces)
        {
            foreach (MethodInfo method in type.GetMethods(Declared))
            {
                if (method.IsSpecialName)
                {
                    if (method.Name.StartsWith("op_", StringComparison.Ordinal))
                    {
                        CollectOperator(type, method, pieces);
                    }

                    continue;
                }

                if (!CanWrite(type, method, method.Name))
                {
                    continue;
                }

                MethodInfo captured = method;
                ParameterInfo[] parameters = method.GetParameters();
                pieces.Add(
                    new Piece
                    {
                        Group = MethodGroup,
                        Name = method.Name,
                        ParameterCount = parameters.Length,
                        ParameterKey = ParameterKey(parameters),
                        ReturnKey = SortName(method.ReturnType),
                        Overloadable = true,
                        Render = overloaded =>
                            Def(
                                4,
                                captured.Name,
                                Parameters(parameters, !captured.IsStatic),
                                ReturnAnnotation(captured.ReturnType, parameters),
                                _docs.Of(captured),
                                overloaded,
                                captured.IsStatic
                            ),
                    }
                );
                if (HasParamArray(parameters))
                {
                    pieces.Add(
                        new Piece
                        {
                            Group = MethodGroup,
                            Name = method.Name,
                            ParameterCount = parameters.Length,
                            ParameterKey = ParameterKey(parameters) + "*",
                            ReturnKey = SortName(method.ReturnType),
                            Overloadable = true,
                            Counts = false,
                            Render = overloaded =>
                                Def(
                                    4,
                                    captured.Name,
                                    Parameters(parameters, !captured.IsStatic, true),
                                    ReturnAnnotation(captured.ReturnType, parameters),
                                    _docs.Of(captured),
                                    overloaded,
                                    captured.IsStatic
                                ),
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Whether the last parameter is a <c>params</c> array. pythonnet takes its elements as separate arguments as well
        /// as an array or a Python list, so it is written both ways.
        /// </summary>
        private static bool HasParamArray(ParameterInfo[] parameters) =>
            parameters.Length > 0
            && parameters[parameters.Length - 1]
                .CustomAttributes.Any(a => a.AttributeType.FullName == "System.ParamArrayAttribute");

        private void CollectOperator(Type type, MethodInfo method, List<Piece> pieces)
        {
            ParameterInfo[] parameters = method.GetParameters();
            string netName = method.Name;
            if (netName == "op_Equality" || netName == "op_Inequality")
            {
                bool isEquality = netName == "op_Equality";
                string name = isEquality ? "__eq__" : "__ne__";
                pieces.Add(
                    new Piece
                    {
                        Group = OperatorGroup,
                        Name = name,
                        Render = overloaded =>
                            Def(
                                4,
                                name,
                                new List<string> { "self", "other: object" },
                                "bool",
                                _docs.Of(method),
                                false,
                                false
                            ),
                    }
                );
                if (isEquality)
                {
                    // Python drops the hash of a class that defines __eq__ unless it says so again.
                    pieces.Add(
                        new Piece
                        {
                            Group = OperatorGroup,
                            Name = "__hash__",
                            Counts = false,
                            Render = overloaded =>
                                Def(4, "__hash__", new List<string> { "self" }, "int", null, false, false),
                        }
                    );
                }

                return;
            }

            string pythonName = null;
            Type other = null;
            if (parameters.Length == 1)
            {
                pythonName = PythonNames.UnaryOperator(netName);
            }
            else if (parameters.Length == 2)
            {
                bool leftIsClass = parameters[0].ParameterType == type;
                if (leftIsClass || parameters[1].ParameterType == type)
                {
                    pythonName = PythonNames.BinaryOperator(netName, !leftIsClass);
                    other = parameters[leftIsClass ? 1 : 0].ParameterType;
                }
            }

            if (pythonName == null)
            {
                _leftOut.Add("operator without a Python form: " + type.FullName + "." + netName);
                return;
            }

            pieces.Add(
                new Piece
                {
                    Group = OperatorGroup,
                    Name = pythonName,
                    ParameterCount = parameters.Length,
                    ParameterKey = ParameterKey(parameters),
                    ReturnKey = SortName(method.ReturnType),
                    Overloadable = true,
                    Render = overloaded =>
                    {
                        var arguments = new List<string> { "self" };
                        if (other != null)
                        {
                            arguments.Add("other: " + _mapper.Map(other, true, _module));
                        }

                        return Def(
                            4,
                            pythonName,
                            arguments,
                            _mapper.Map(method.ReturnType, false, _module),
                            _docs.Of(method),
                            overloaded,
                            false
                        );
                    },
                }
            );
        }

        /// <summary>
        /// pythonnet makes every <c>IDisposable</c> a context manager (<c>with CadApplication() as app:</c>), so a class of
        /// ours that implements it gets <c>__enter__</c> and <c>__exit__</c>, unless its base class of ours does already.
        /// <c>__exit__</c> says it returns <c>None</c>, so a checker does not take the block as one that swallows an exception.
        /// </summary>
        private void CollectDisposable(Type type, List<Piece> pieces)
        {
            if (
                type.IsInterface
                || !IsDisposable(type)
                || (type.BaseType != null && _isOurs(type.BaseType) && IsDisposable(type.BaseType))
            )
            {
                return;
            }

            pieces.Add(
                new Piece
                {
                    Group = MethodGroup,
                    Name = "__enter__",
                    Counts = false,
                    Render = overloaded =>
                        Def(4, "__enter__", new List<string> { "self" }, _module.Typing("Self"), null, false, false),
                }
            );
            pieces.Add(
                new Piece
                {
                    Group = MethodGroup,
                    Name = "__exit__",
                    Counts = false,
                    Render = overloaded =>
                        Def(4, "__exit__", new List<string> { "self", "*args: object" }, "None", null, false, false),
                }
            );
        }

        private static bool IsDisposable(Type type) =>
            type.GetInterfaces().Any(i => i.FullName == "System.IDisposable");

        /// <summary>
        /// A class that implements <c>IEnumerable&lt;T&gt;</c> is iterable in Python. Its base class or interfaces of ours
        /// that already say so are not repeated.
        /// </summary>
        private void CollectIteration(Type type, List<Piece> pieces)
        {
            var inheritedFrom = new List<Type>(type.GetInterfaces().Where(_isOurs));
            if (type.BaseType != null && _isOurs(type.BaseType))
            {
                inheritedFrom.Add(type.BaseType);
            }

            HashSet<string> inherited = new HashSet<string>(
                inheritedFrom.SelectMany(ElementTypes).Select(SortName),
                StringComparer.Ordinal
            );
            Type element = ElementTypes(type).FirstOrDefault(e => !inherited.Contains(SortName(e)));
            if (element != null)
            {
                pieces.Add(
                    new Piece
                    {
                        Group = MethodGroup,
                        Name = "__iter__",
                        Counts = false,
                        Render = overloaded =>
                            Def(
                                4,
                                "__iter__",
                                new List<string> { "self" },
                                _module.Typing("Iterator") + "[" + _mapper.Map(element, false, _module) + "]",
                                null,
                                false,
                                false
                            ),
                    }
                );
            }

            // len() works on a class that is a collection (ICollection<T> or IReadOnlyCollection<T>), but not on the
            // interface it is handed over as, nor on a class that is only enumerable (measured), so it is written for the
            // classes of ours that are one and whose base class of ours is not already.
            if (
                !type.IsInterface
                && IsCollection(type)
                && !(type.BaseType != null && _isOurs(type.BaseType) && IsCollection(type.BaseType))
            )
            {
                pieces.Add(
                    new Piece
                    {
                        Group = MethodGroup,
                        Name = "__len__",
                        Counts = false,
                        Render = overloaded =>
                            Def(4, "__len__", new List<string> { "self" }, "int", null, false, false),
                    }
                );
            }
        }

        private static bool IsCollection(Type type) =>
            type.GetInterfaces()
                .Any(i =>
                    i.IsGenericType
                    && (
                        i.GetGenericTypeDefinition().FullName == "System.Collections.Generic.ICollection`1"
                        || i.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IReadOnlyCollection`1"
                    )
                );

        private static IEnumerable<Type> ElementTypes(Type type) =>
            type.GetInterfaces()
                .Where(i =>
                    i.IsGenericType
                    && i.GetGenericTypeDefinition().FullName == "System.Collections.Generic.IEnumerable`1"
                )
                .Select(i => i.GetGenericArguments()[0])
                .OrderBy(SortName, StringComparer.Ordinal);

        #endregion

        #region Signatures

        /// <summary>
        /// Whether a member can be written: not a compiler-generated one, and not one whose name is a Python keyword
        /// (<c>None</c>, <c>True</c>, <c>False</c>), which stays reachable as <c>getattr(Kind, "None")</c>. The others are
        /// remembered for the summary.
        /// </summary>
        private bool CanWrite(Type type, MemberInfo member, string name)
        {
            if (PythonNames.IsKeyword(name))
            {
                _leftOut.Add("keyword name: " + type.FullName + "." + name);
                return false;
            }

            if (
                name.IndexOfAny(new[] { '<', '>', '$' }) >= 0
                || member.CustomAttributes.Any(a =>
                    a.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute"
                )
            )
            {
                _leftOut.Add("compiler generated: " + type.FullName + "." + name);
                return false;
            }

            return true;
        }

        /// <summary>
        /// The parameters a caller passes: all but the <c>out</c> ones, which pythonnet returns instead. With
        /// <paramref name="varargs"/> the last one, a <c>params</c> array, is written as <c>*name: T</c>, for the call with
        /// separate arguments. pythonnet leaves out a trailing <c>out</c> argument by itself, but one that a parameter of the
        /// call follows (<c>TrySubtract(tool, out result, tolerance)</c>) only when that parameter is given by keyword, so the
        /// parameters from the first such <c>out</c> on are written after a <c>*</c>: keyword-only.
        /// </summary>
        private List<string> Parameters(IEnumerable<ParameterInfo> parameters, bool withSelf, bool varargs = false)
        {
            ParameterInfo[] all = parameters.ToArray();
            List<ParameterInfo> list = all.Where(p => !p.IsOut).ToList();

            // Everything before the first out parameter is passed, so its index is where the keyword-only ones start, and
            // it is followed by a parameter of the call when more are passed than come before it.
            int firstOut = Array.FindIndex(all, p => p.IsOut);
            int keywordOnly = firstOut >= 0 && firstOut < list.Count ? firstOut : -1;
            var result = new List<string>();
            if (withSelf)
            {
                result.Add("self");
            }

            // Python cannot take a keyword as a name, so the parameter is renamed and, since the .NET name is the one
            // pythonnet matches a keyword argument against, it and those before it can only be given by position.
            int lastKeyword = list.FindLastIndex(p => PythonNames.IsKeyword(p.Name));
            bool defaulted = false;
            for (int i = 0; i < list.Count; i++)
            {
                ParameterInfo p = list[i];
                string name = PythonNames.ParameterName(p.Name ?? "arg" + i);
                if (i == keywordOnly)
                {
                    // A keyword cannot be written for a Python keyword or a params array, so such a member has no stub form.
                    if (varargs || list.Skip(i).Any(q => PythonNames.IsKeyword(q.Name)))
                    {
                        throw new InvalidOperationException(
                            "A parameter after an out parameter cannot be keyword-only: "
                                + p.Member.DeclaringType?.FullName
                                + "."
                                + p.Member.Name
                        );
                    }

                    result.Add("*");
                }

                if (varargs && i == list.Count - 1)
                {
                    result.Add("*" + name + ": " + _mapper.Map(p.ParameterType.GetElementType(), true, _module));
                    break;
                }

                defaulted |= p.IsOptional;
                result.Add(name + ": " + _mapper.Map(p.ParameterType, true, _module) + (defaulted ? " = ..." : ""));
                if (i == lastKeyword)
                {
                    result.Add("/");
                }
            }

            return result;
        }

        /// <summary>
        /// The return annotation. pythonnet gives back the <c>out</c> and <c>ref</c> arguments after the result, as a tuple, so
        /// they are written that way, and the result is left out of the tuple when the method returns nothing.
        /// </summary>
        private string ReturnAnnotation(Type returnType, ParameterInfo[] parameters)
        {
            List<string> given = parameters
                .Where(p => p.ParameterType.IsByRef && (p.IsOut || !p.IsIn))
                .Select(p => _mapper.Map(p.ParameterType, false, _module))
                .ToList();
            string result = _mapper.Map(returnType, false, _module);
            if (given.Count == 0)
            {
                return result;
            }

            if (returnType.FullName != "System.Void")
            {
                given.Insert(0, result);
            }

            return "tuple[" + string.Join(", ", given) + "]";
        }

        private static string ParameterKey(ParameterInfo[] parameters) =>
            string.Join(",", parameters.Select(p => SortName(p.ParameterType)));

        private static string SortName(Type type) => type.FullName ?? type.Name;

        #endregion

        #region Text

        /// <summary>
        /// A <c>def</c>, with its decorators and docstring; on several lines, one parameter each, when the signature passes
        /// 120 columns.
        /// </summary>
        private string Def(
            int indent,
            string name,
            List<string> parameters,
            string returnType,
            IReadOnlyList<string> doc,
            bool overloaded,
            bool isStatic
        )
        {
            string pad = new string(' ', indent);
            var text = new StringBuilder();
            if (overloaded)
            {
                text.Append(pad).Append('@').Append(_module.Typing("overload")).Append('\n');
            }

            if (isStatic)
            {
                text.Append(pad).Append("@staticmethod\n");
            }

            string single = pad + "def " + name + "(" + string.Join(", ", parameters) + ") -> " + returnType + ":";
            if (single.Length <= MaxSignatureWidth)
            {
                text.Append(single);
            }
            else
            {
                text.Append(pad).Append("def ").Append(name).Append("(\n");
                foreach (string parameter in parameters)
                {
                    text.Append(pad).Append("    ").Append(parameter).Append(",\n");
                }

                text.Append(pad).Append(") -> ").Append(returnType).Append(':');
            }

            if (doc == null || doc.Count == 0)
            {
                text.Append(" ...\n");
            }
            else
            {
                text.Append('\n');
                AppendDocstring(text, doc, indent + 4);
            }

            return text.ToString();
        }

        private static void AppendDocstring(StringBuilder text, IReadOnlyList<string> doc, int indent)
        {
            if (doc == null || doc.Count == 0)
            {
                return;
            }

            foreach (string line in DocText.DocstringLines(doc, indent))
            {
                text.Append(line).Append('\n');
            }
        }

        #endregion
    }
}
