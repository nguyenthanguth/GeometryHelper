using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// Reads the text of a DCL file into its resolved definitions (D301-D309): the definitions, <c>@include</c>, and the
    /// predefined tiles of <see cref="DclBase"/>. A definition may name its parent before the parent is defined; the chain of
    /// parents is followed when the file is loaded, so every tile that comes out is a primitive with the attributes merged.
    /// </summary>
    internal static class DclParser
    {
        private static readonly string[] Primitives =
        {
            "dialog",
            "cluster",
            "radio_cluster",
            "button",
            "edit_box",
            "list_box",
            "popup_list",
            "toggle",
            "radio_button",
            "text",
            "image",
            "image_button",
            "slider",
            "spacer",
        };

        private static readonly Lazy<IReadOnlyList<DclTile>> Predefined = new Lazy<IReadOnlyList<DclTile>>(() =>
            Load(DclBase.Text, DclBase.Path, null, new[] { DclBase.Path }, new Dictionary<string, DclTile>())
        );

        /// <summary>Reads a DCL file: its includes, its definitions and the predefined tiles, each resolved to primitives.</summary>
        /// <param name="text">The file's text.</param>
        /// <param name="path">The file's full path, for the messages and the folder includes are looked for in first.</param>
        /// <param name="include">Finds a file an <c>@include</c> names, given the name as written and the including file's
        /// path: its full path and text, or null when it is not found.</param>
        /// <returns>The file's definitions.</returns>
        /// <exception cref="DclException">Thrown for a syntax error, a tile not defined, a predefined tile defined again, an include not found or one that loops.</exception>
        internal static DclFile Parse(
            string text,
            string path,
            Func<string, string, KeyValuePair<string, string>?> include
        )
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var predefined = new Dictionary<string, DclTile>(StringComparer.Ordinal);

            foreach (DclTile tile in Predefined.Value)
            {
                predefined[tile.Name] = tile;
            }

            IReadOnlyList<DclTile> loaded = Load(text, path, include, new[] { path }, predefined);

            // The predefined tiles first, then the included files' and the file's own; none of those may share a predefined name.
            return new DclFile(path, Predefined.Value.Select(tile => tile.Copy()).Concat(loaded));
        }

        /// <summary>
        /// Reads one file and what it includes. Gives the included definitions and then the file's own, each resolved against
        /// the file's own definitions, the included ones, the predefined ones and the primitives, in that order.
        /// </summary>
        private static IReadOnlyList<DclTile> Load(
            string text,
            string path,
            Func<string, string, KeyValuePair<string, string>?> include,
            IReadOnlyList<string> chain,
            IReadOnlyDictionary<string, DclTile> predefined
        )
        {
            var reader = new Reader(new DclLexer(text, path), path);
            var definitions = new List<RawTile>();
            var included = new List<DclTile>();

            while (true)
            {
                DclToken token = reader.Take();

                if (token.Kind == DclTokenKind.End)
                {
                    break;
                }

                if (token.Kind == DclTokenKind.At)
                {
                    DclToken word = reader.Expect(DclTokenKind.Name);

                    if (!string.Equals(word.Text, "include", StringComparison.Ordinal))
                    {
                        throw reader.Error("@" + word.Raw, token.Line);
                    }

                    DclToken name = reader.Expect(DclTokenKind.String);
                    reader.ExpectSemicolon();
                    included.AddRange(Include(name.Text, token.Line, path, include, chain, predefined));
                }
                else if (token.Kind == DclTokenKind.Name)
                {
                    reader.Expect(DclTokenKind.Colon);
                    DclToken parent = reader.Expect(DclTokenKind.Name);

                    // A file may not define a name of the predefined set again (AutoCAD: "warning: redefining object.").
                    if (predefined.ContainsKey(token.Text))
                    {
                        throw new DclException(
                            path,
                            token.Line,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "warning: redefining object.\nSymbol: \"{0}\".",
                                token.Text
                            ),
                            token.Text
                        );
                    }

                    var raw = new RawTile(token.Text, parent.Text, token.Line);
                    reader.ReadBody(raw);
                    definitions.Add(raw);
                }
                else
                {
                    throw reader.Error(token);
                }
            }

            return new Resolver(path, definitions, included, predefined).ResolveAll(included);
        }

        private static IEnumerable<DclTile> Include(
            string name,
            int line,
            string path,
            Func<string, string, KeyValuePair<string, string>?> include,
            IReadOnlyList<string> chain,
            IReadOnlyDictionary<string, DclTile> predefined
        )
        {
            // The predefined set is in every file: including it adds nothing.
            if (
                string.Equals(name, "base", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "base.dcl", StringComparison.OrdinalIgnoreCase)
            )
            {
                return Enumerable.Empty<DclTile>();
            }

            KeyValuePair<string, string>? found = include?.Invoke(name, path);

            if (found == null)
            {
                throw new DclException(
                    path,
                    line,
                    string.Format(CultureInfo.InvariantCulture, "Include file \"{0}\" not found.", name)
                );
            }

            string foundPath = found.Value.Key;

            if (chain.Any(visited => string.Equals(visited, foundPath, StringComparison.OrdinalIgnoreCase)))
            {
                throw new DclException(
                    path,
                    line,
                    string.Format(CultureInfo.InvariantCulture, "Include of \"{0}\" loops.", name)
                );
            }

            return Load(found.Value.Value, foundPath, include, chain.Concat(new[] { foundPath }).ToArray(), predefined);
        }

        /// <summary>A definition or an inner tile as written, before its parent is looked up.</summary>
        private sealed class RawTile
        {
            internal RawTile(string name, string parent, int line)
            {
                Name = name;
                Parent = parent;
                Line = line;
            }

            internal string Name { get; }
            internal string Parent { get; }
            internal int Line { get; }
            internal List<DclAttribute> Attributes { get; } = new List<DclAttribute>();
            internal List<RawTile> Children { get; } = new List<RawTile>();
        }

        /// <summary>Reads tokens with one token of look-ahead and the grammar of a tile's body.</summary>
        private sealed class Reader
        {
            private readonly DclLexer _lexer;
            private readonly string _path;
            private DclToken? _peeked;

            internal Reader(DclLexer lexer, string path)
            {
                _lexer = lexer;
                _path = path;
            }

            internal DclToken Take()
            {
                if (_peeked.HasValue)
                {
                    DclToken peeked = _peeked.Value;
                    _peeked = null;
                    return peeked;
                }

                return _lexer.Next();
            }

            internal DclToken Expect(DclTokenKind kind)
            {
                DclToken token = Take();

                if (token.Kind != kind)
                {
                    throw Error(token);
                }

                return token;
            }

            /// <summary>
            /// Reads the <c>;</c> that ends an entry. A tile or a closing brace where it should be is a missing semicolon, on the
            /// line of that token (AutoCAD gives no symbol for it); any other token is a syntax error at that token.
            /// </summary>
            internal void ExpectSemicolon()
            {
                DclToken token = Take();

                if (token.Kind == DclTokenKind.Colon || token.Kind == DclTokenKind.CloseBrace)
                {
                    throw DclException.MissingSemicolon(_path, token.Line);
                }

                if (token.Kind != DclTokenKind.Semicolon)
                {
                    throw Error(token);
                }
            }

            internal DclException Error(DclToken token) => Error(token.Raw, token.Line);

            internal DclException Error(string symbol, int line) => DclException.SyntaxError(_path, line, symbol);

            /// <summary>Reads <c>{ entry* }</c> and the optional <c>;</c> after it into a tile.</summary>
            internal void ReadBody(RawTile tile)
            {
                Expect(DclTokenKind.OpenBrace);

                while (true)
                {
                    DclToken token = Take();

                    if (token.Kind == DclTokenKind.CloseBrace)
                    {
                        break;
                    }

                    if (token.Kind == DclTokenKind.Colon)
                    {
                        DclToken parent = Expect(DclTokenKind.Name);
                        var inner = new RawTile(parent.Text, parent.Text, token.Line);
                        ReadBody(inner);
                        tile.Children.Add(inner);
                    }
                    else if (token.Kind == DclTokenKind.Name)
                    {
                        DclToken next = Take();

                        if (next.Kind == DclTokenKind.Equals)
                        {
                            DclToken value = Take();
                            DclValueKind kind;

                            switch (value.Kind)
                            {
                                case DclTokenKind.String:
                                    kind = DclValueKind.String;
                                    break;
                                case DclTokenKind.Integer:
                                    kind = DclValueKind.Integer;
                                    break;
                                case DclTokenKind.Real:
                                    kind = DclValueKind.Real;
                                    break;
                                case DclTokenKind.Name:
                                    kind = DclValueKind.Name;
                                    break;
                                default:
                                    throw Error(value);
                            }

                            ExpectSemicolon();
                            tile.Attributes.Add(new DclAttribute(token.Text, kind, value.Text, token.Line));
                        }
                        else if (next.Kind == DclTokenKind.Semicolon)
                        {
                            tile.Children.Add(new RawTile(token.Text, token.Text, token.Line));
                        }
                        else if (next.Kind == DclTokenKind.Colon || next.Kind == DclTokenKind.CloseBrace)
                        {
                            throw DclException.MissingSemicolon(_path, next.Line);
                        }
                        else
                        {
                            throw Error(next);
                        }
                    }
                    else
                    {
                        throw Error(token);
                    }
                }

                // A ";" after the closing brace is optional.
                _peeked = _lexer.Next();

                if (_peeked.Value.Kind == DclTokenKind.Semicolon)
                {
                    _peeked = null;
                }
            }
        }

        /// <summary>Follows each definition's chain of parents down to a primitive, once.</summary>
        private sealed class Resolver
        {
            private readonly string _path;
            private readonly List<RawTile> _definitions;
            private readonly Dictionary<string, RawTile> _own = new Dictionary<string, RawTile>(StringComparer.Ordinal);
            private readonly Dictionary<string, DclTile> _outer = new Dictionary<string, DclTile>(
                StringComparer.Ordinal
            );
            private readonly Dictionary<RawTile, DclTile> _done = new Dictionary<RawTile, DclTile>();
            private readonly HashSet<RawTile> _busy = new HashSet<RawTile>();

            internal Resolver(
                string path,
                List<RawTile> definitions,
                IEnumerable<DclTile> included,
                IReadOnlyDictionary<string, DclTile> predefined
            )
            {
                _path = path;
                _definitions = definitions;

                foreach (RawTile definition in definitions)
                {
                    _own[definition.Name] = definition;
                }

                // Of two definitions with one name the later wins: the predefined first, then the included, in order.
                foreach (KeyValuePair<string, DclTile> pair in predefined)
                {
                    _outer[pair.Key] = pair.Value;
                }

                foreach (DclTile tile in included)
                {
                    _outer[tile.Name] = tile;
                }
            }

            /// <summary>Gives <paramref name="included"/> and then every definition of the file, resolved, in order.</summary>
            internal IReadOnlyList<DclTile> ResolveAll(IEnumerable<DclTile> included)
            {
                var all = new List<DclTile>(included);

                foreach (RawTile definition in _definitions)
                {
                    all.Add(ResolveDefinition(definition));
                }

                return all;
            }

            private DclTile ResolveDefinition(RawTile raw)
            {
                if (_done.TryGetValue(raw, out DclTile done))
                {
                    return done;
                }

                if (!_busy.Add(raw))
                {
                    // The chain of parents (or a tile inside the definition) comes back to the definition itself.
                    throw new DclException(
                        _path,
                        raw.Line,
                        string.Format(CultureInfo.InvariantCulture, "Tile \"{0}\" loops.", raw.Name)
                    );
                }

                DclTile resolved = ResolveTile(raw);
                _busy.Remove(raw);
                _done[raw] = resolved;
                return resolved;
            }

            private DclTile ResolveTile(RawTile raw)
            {
                string kind;
                IEnumerable<DclAttribute> attributes;
                IEnumerable<DclTile> children;

                if (_own.TryGetValue(raw.Parent, out RawTile ownParent))
                {
                    DclTile parent = ResolveDefinition(ownParent);
                    kind = parent.Kind;
                    attributes = parent.Attributes.Values;
                    children = parent.Children.Select(child => child.Copy());
                }
                else if (_outer.TryGetValue(raw.Parent, out DclTile outerParent))
                {
                    kind = outerParent.Kind;
                    attributes = outerParent.Attributes.Values;
                    children = outerParent.Children.Select(child => child.Copy());
                }
                else if (Array.IndexOf(Primitives, raw.Parent) >= 0)
                {
                    kind = raw.Parent;
                    attributes = Enumerable.Empty<DclAttribute>();
                    children = Enumerable.Empty<DclTile>();
                }
                else
                {
                    throw new DclException(
                        _path,
                        raw.Line,
                        string.Format(CultureInfo.InvariantCulture, "Tile \"{0}\" is not defined.", raw.Parent),
                        semantic: true
                    );
                }

                // The parent's attributes first and the tile's own over them; the parent's children first, then its own.
                return new DclTile(
                    kind,
                    raw.Name,
                    attributes.ToList().Concat(raw.Attributes),
                    children.ToList().Concat(raw.Children.Select(ResolveTile)),
                    _path,
                    raw.Line
                );
            }
        }
    }
}
