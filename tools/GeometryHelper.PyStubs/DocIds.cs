using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GeometryHelper.PyStubs
{
    /// <summary>
    /// The documentation comment ID of a type or member: the key the compiler writes in the XML file
    /// (<c>T:GeometryHelper.Geometry.GeoPoint3</c>, <c>M:GeometryHelper.Geometry.GeoPoint3.DistanceTo(GeometryHelper.Geometry.GeoPoint3)</c>).
    /// </summary>
    internal static class DocIds
    {
        /// <summary>The ID of a type.</summary>
        public static string Of(Type type) => "T:" + TypeName(type);

        /// <summary>The ID of a field, property, event, constructor or method.</summary>
        public static string Of(MemberInfo member)
        {
            string owner = TypeName(member.DeclaringType) + ".";
            switch (member)
            {
                case FieldInfo field:
                    return "F:" + owner + field.Name;
                case EventInfo eventInfo:
                    return "E:" + owner + eventInfo.Name;
                case PropertyInfo property:
                    return "P:" + owner + property.Name + ParameterList(property.GetIndexParameters());
                case ConstructorInfo constructor:
                    return "M:" + owner + "#ctor" + ParameterList(constructor.GetParameters());
                case MethodInfo method:
                    string id = "M:" + owner + method.Name;
                    if (method.IsGenericMethodDefinition)
                    {
                        id += "``" + method.GetGenericArguments().Length;
                    }

                    id += ParameterList(method.GetParameters());
                    if (method.Name == "op_Implicit" || method.Name == "op_Explicit")
                    {
                        id += "~" + TypeName(method.ReturnType);
                    }

                    return id;
                default:
                    throw new NotSupportedException("No documentation ID for " + member.MemberType + " " + member.Name);
            }
        }

        private static string ParameterList(ParameterInfo[] parameters) =>
            parameters.Length == 0
                ? ""
                : "(" + string.Join(",", parameters.Select(p => TypeName(p.ParameterType))) + ")";

        private static string TypeName(Type type)
        {
            if (type.IsByRef)
            {
                return TypeName(type.GetElementType()) + "@";
            }

            if (type.IsPointer)
            {
                return TypeName(type.GetElementType()) + "*";
            }

            if (type.IsArray)
            {
                int rank = type.GetArrayRank();
                string dimensions = rank == 1 ? "" : string.Join(",", Enumerable.Repeat("0:", rank));
                return TypeName(type.GetElementType()) + "[" + dimensions + "]";
            }

            if (type.IsGenericParameter)
            {
                return (type.DeclaringMethod != null ? "``" : "`") + type.GenericParameterPosition;
            }

            if (type.IsGenericType && !type.IsGenericTypeDefinition)
            {
                string definition = type.GetGenericTypeDefinition().FullName;
                definition = definition.Substring(0, definition.IndexOf('`')).Replace('+', '.');
                return definition + "{" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + "}";
            }

            return type.FullName.Replace('+', '.');
        }
    }
}
