using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace GeometryHelper.PyStubs
{
    /// <summary>
    /// The XML documentation of the assemblies, looked up by type or member. A type or member gives the
    /// <c>&lt;summary&gt;</c> of its comment; one with <c>&lt;inheritdoc/&gt;</c> instead takes the summary of the member it
    /// overrides or implements (the base class first, then the interfaces), and one with none gives nothing.
    /// </summary>
    internal sealed class DocLibrary
    {
        private const BindingFlags AllMembers =
            BindingFlags.Public
            | BindingFlags.NonPublic
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        private const int MaxDepth = 16;
        private readonly Dictionary<string, XElement> _members = new Dictionary<string, XElement>(
            StringComparer.Ordinal
        );

        public DocLibrary(IEnumerable<string> xmlFiles)
        {
            foreach (string file in xmlFiles)
            {
                foreach (XElement member in XDocument.Load(file).Descendants("member"))
                {
                    string name = (string)member.Attribute("name");
                    if (name != null)
                    {
                        _members[name] = member;
                    }
                }
            }
        }

        /// <summary>The paragraphs of the summary of a type, empty when it has none.</summary>
        public IReadOnlyList<string> Of(Type type) => Of(type, 0);

        /// <summary>The paragraphs of the summary of a member, empty when it has none.</summary>
        public IReadOnlyList<string> Of(MemberInfo member) => Of(member, 0);

        private IReadOnlyList<string> Of(Type type, int depth)
        {
            if (depth > MaxDepth || !_members.TryGetValue(DocIds.Of(type), out XElement element))
            {
                return Array.Empty<string>();
            }

            IReadOnlyList<string> own = Summary(element, out XElement inherit);
            if (own.Count > 0 || inherit == null)
            {
                return own;
            }

            IReadOnlyList<string> byCref = ByCref(inherit);
            if (byCref != null)
            {
                return byCref;
            }

            var sources = new List<Type>();
            if (type.BaseType != null)
            {
                sources.Add(type.BaseType);
            }

            sources.AddRange(type.GetInterfaces().OrderBy(i => i.FullName, StringComparer.Ordinal));
            return sources.Select(s => Of(s, depth + 1)).FirstOrDefault(d => d.Count > 0) ?? Array.Empty<string>();
        }

        private IReadOnlyList<string> Of(MemberInfo member, int depth)
        {
            if (depth > MaxDepth || !_members.TryGetValue(DocIds.Of(member), out XElement element))
            {
                return Array.Empty<string>();
            }

            IReadOnlyList<string> own = Summary(element, out XElement inherit);
            if (own.Count > 0 || inherit == null)
            {
                return own;
            }

            IReadOnlyList<string> byCref = ByCref(inherit);
            if (byCref != null)
            {
                return byCref;
            }

            return BaseMembers(member).Select(m => Of(m, depth + 1)).FirstOrDefault(d => d.Count > 0)
                ?? Array.Empty<string>();
        }

        private static IReadOnlyList<string> Summary(XElement member, out XElement inherit)
        {
            inherit = member.Element("inheritdoc");

            // The parts of a partial class each keep their own summary, so a type can have several. All are kept, in order.
            return member.Elements("summary").SelectMany(DocText.Paragraphs).ToList();
        }

        /// <summary>The summary of the member an <c>&lt;inheritdoc cref="..."/&gt;</c> names, or null when it names none.</summary>
        private IReadOnlyList<string> ByCref(XElement inheritdoc)
        {
            string cref = (string)inheritdoc.Attribute("cref");
            if (cref == null)
            {
                return null;
            }

            return _members.TryGetValue(cref, out XElement target)
                ? Summary(target, out XElement _)
                : Array.Empty<string>();
        }

        /// <summary>The members <paramref name="member"/> overrides or implements: those of its base classes, nearest first, then of its interfaces.</summary>
        private static IEnumerable<MemberInfo> BaseMembers(MemberInfo member)
        {
            var owners = new List<Type>();
            for (Type baseType = member.DeclaringType.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                owners.Add(baseType);
            }

            owners.AddRange(member.DeclaringType.GetInterfaces().OrderBy(i => i.FullName, StringComparer.Ordinal));
            foreach (Type owner in owners)
            {
                switch (member)
                {
                    case MethodInfo method:
                        foreach (
                            MethodInfo candidate in owner
                                .GetMethods(AllMembers)
                                .Where(c => c.Name == method.Name && Signature(c) == Signature(method))
                        )
                        {
                            yield return candidate;
                        }

                        break;
                    case PropertyInfo property:
                        foreach (
                            PropertyInfo candidate in owner
                                .GetProperties(AllMembers)
                                .Where(c => c.Name == property.Name && Signature(c) == Signature(property))
                        )
                        {
                            yield return candidate;
                        }

                        break;
                    case EventInfo eventInfo:
                        EventInfo found = owner.GetEvent(eventInfo.Name, AllMembers);
                        if (found != null)
                        {
                            yield return found;
                        }

                        break;
                }
            }
        }

        private static string Signature(MethodBase method) => Signature(method.GetParameters());

        private static string Signature(PropertyInfo property) => Signature(property.GetIndexParameters());

        private static string Signature(ParameterInfo[] parameters) =>
            string.Join(",", parameters.Select(p => p.ParameterType.FullName ?? p.ParameterType.Name));
    }
}
