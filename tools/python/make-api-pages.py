#!/usr/bin/env python3
"""Writes the Python API reference of GeometryHelper as Markdown, one page per type, for docfx.

It reads the generated stubs (src/python/GeometryHelper-stubs, made by tools/python/make-stubs.sh) with ast, so a page
shows what an editor shows, and the uids that docfx's metadata step wrote into docfx/api, so that a link is written
only to a page that exists. Run it between `docfx metadata` and `docfx build`:

    python tools/python/make-api-pages.py --stubs src/python/GeometryHelper-stubs --api docfx/api --out docfx/python-api

Standard library only. The folder given to --out is deleted and written again, with the same bytes on every run.
Exit 0 on success, 1 with `error: <reason>` on standard error when a folder is missing, a stub does not parse or a type
has no uid, 2 on wrong arguments.
"""

from __future__ import annotations

import argparse
import ast
import re
import shutil
import sys
from dataclasses import dataclass, field
from pathlib import Path

ROOT_NAMESPACE = "GeometryHelper"

# The .NET operator each Python operator method comes from, as docfx names the group.
OPERATORS = {
    "__add__": "op_Addition",
    "__radd__": "op_Addition",
    "__sub__": "op_Subtraction",
    "__rsub__": "op_Subtraction",
    "__mul__": "op_Multiply",
    "__rmul__": "op_Multiply",
    "__truediv__": "op_Division",
    "__neg__": "op_UnaryNegation",
    "__pos__": "op_UnaryPlus",
    "__eq__": "op_Equality",
    "__ne__": "op_Inequality",
    "__lt__": "op_LessThan",
    "__gt__": "op_GreaterThan",
    "__le__": "op_LessThanOrEqual",
    "__ge__": "op_GreaterThanOrEqual",
    "__or__": "op_BitwiseOr",
    "__and__": "op_BitwiseAnd",
    "__xor__": "op_ExclusiveOr",
    "__invert__": "op_OnesComplement",
    "__getitem__": "Item",
    "__setitem__": "Item",
}

SECTIONS = (
    "Values",
    "Constructors",
    "Properties and fields",
    "Events",
    "Methods",
    "Operators and Python protocols",
)

MARKDOWN_SPECIAL = re.compile(r"([\\`*_\[\]<>|#])")
OVERLOAD_LINE = re.compile(r"^  overload: (.+)$", re.MULTILINE)
ITEM_LINE = re.compile(r"^(?:- uid: (.+)|  type: (\w+))$", re.MULTILINE)
SENTENCE_END = re.compile(r"[.!?](?=\s+[A-Z]|$)")


class GeneratorError(Exception):
    """A reason the pages cannot be written; its text follows `error: ` on standard error."""


@dataclass
class Member:
    """One stub entry: an overload, a property with its setter, or an attribute."""

    block: str
    doc: str | None


@dataclass
class Group:
    """The entries of one name in one type, which share one .NET page."""

    name: str
    section: str
    plain: bool
    members: list[Member] = field(default_factory=list)


@dataclass
class TypeInfo:
    """A type read from a stub."""

    namespace: str
    name: str
    bases: str
    doc: str | None
    groups: list[Group]

    @property
    def uid(self) -> str:
        return f"{self.namespace}.{self.name}"


@dataclass
class Result:
    """What a run wrote."""

    pages: int
    types: int
    groups: int
    linked: int
    unlinked_names: list[str]

    def summary(self) -> str:
        unlinked = len(self.unlinked_names)
        return (
            f"Python API: {self.pages} pages, {self.types} types, {self.groups} member groups, "
            f"{self.linked} linked, {unlinked} unlinked"
        )


def escape(text: str) -> str:
    """Escapes the characters Markdown reads as markup."""
    return MARKDOWN_SPECIAL.sub(r"\\\1", text)


def paragraphs(doc: str | None) -> list[str]:
    """The paragraphs of a docstring, each on one line and escaped."""
    if not doc:
        return []
    return [escape(" ".join(p.split())) for p in doc.split("\n\n") if p.strip()]


def first_sentence(doc: str | None) -> str:
    """The first sentence of the first paragraph of a docstring, on one line and escaped."""
    if not doc:
        return ""
    text = " ".join(doc.split("\n\n")[0].split())
    end = SENTENCE_END.search(text)
    return escape(text[: end.end()] if end else text)


def read_uids(api: Path) -> tuple[set[str], dict[str, str]]:
    """The uids docfx's metadata wrote and the kind of each item (`class`, `struct`, ... or `property`, `event`...)."""
    uids: set[str] = set()
    kinds: dict[str, str] = {}
    for path in sorted(api.glob("*.yml")):
        current = ""
        text = path.read_text(encoding="utf-8")
        uids.update(m.group(1).strip().strip("'\"") for m in OVERLOAD_LINE.finditer(text))
        for m in ITEM_LINE.finditer(text):
            if m.group(1) is not None:
                current = m.group(1).strip().strip("'\"")
                uids.add(current)
            elif current and current not in kinds:
                kinds[current] = m.group(2).lower()
    return uids, kinds


def source_block(lines: list[str], node: ast.FunctionDef) -> str:
    """The decorators worth showing and the def line of a stub function, as the stub writes it."""
    shown = []
    for decorator in node.decorator_list:
        text = ast.unparse(decorator)
        if text != "overload":
            shown.append("@" + text)
    returns = node.returns
    if returns is None or returns.end_lineno is None or returns.end_col_offset is None:
        head = [ast.unparse(node).split("\n")[0].rstrip(":")]
    else:
        head = lines[node.lineno - 1 : returns.end_lineno]
        last = head[-1].encode("utf-8")[: returns.end_col_offset].decode("utf-8")
        head = head[:-1] + [last]
        head = [line.removeprefix("    ") for line in head]
    return "\n".join(shown + head)


def attribute_doc(body: list[ast.stmt], index: int) -> str | None:
    """The string that follows an attribute in a stub, which is its docstring."""
    if index + 1 < len(body):
        nxt = body[index + 1]
        if isinstance(nxt, ast.Expr) and isinstance(nxt.value, ast.Constant) and isinstance(nxt.value.value, str):
            return nxt.value.value
    return None


def is_setter(node: ast.FunctionDef) -> bool:
    return any(isinstance(d, ast.Attribute) and d.attr == "setter" for d in node.decorator_list)


def is_property(node: ast.FunctionDef) -> bool:
    return any(isinstance(d, ast.Name) and d.id == "property" for d in node.decorator_list)


def read_type(namespace: str, cls: ast.ClassDef, lines: list[str], kinds: dict[str, str]) -> TypeInfo:
    """Reads a class of a stub into its groups, in the stub's order."""
    bases = ", ".join(ast.unparse(b) for b in cls.bases)
    is_enum = any(ast.unparse(b) == "System.Enum" for b in cls.bases)
    uid = f"{namespace}.{cls.name}"
    groups: dict[str, Group] = {}
    order: list[Group] = []
    body = cls.body
    for index, node in enumerate(body):
        if isinstance(node, ast.FunctionDef):
            name = node.name
            if is_setter(node):
                # The setter joins the block of its property, so that a reader sees it can be set.
                group = groups.get(name)
                if group is not None and group.members:
                    group.members[-1].block += "\n" + source_block(lines, node)
                continue
            if name == "__init__":
                section = "Constructors"
            elif name.startswith("__") and name.endswith("__"):
                section = "Operators and Python protocols"
            elif is_property(node):
                section = "Events" if kinds.get(f"{uid}.{name}") == "event" else "Properties and fields"
            else:
                section = "Methods"
            plain = False
            member = Member(source_block(lines, node), ast.get_docstring(node))
        elif isinstance(node, ast.AnnAssign) and isinstance(node.target, ast.Name):
            name = node.target.id
            section = "Values" if is_enum else "Properties and fields"
            plain = True
            member = Member(ast.unparse(node), attribute_doc(body, index))
        else:
            continue
        group = groups.get(name)
        if group is None:
            group = groups[name] = Group(name, section, plain)
            order.append(group)
        group.members.append(member)
    return TypeInfo(namespace, cls.name, bases, ast.get_docstring(cls), order)


def read_stubs(stubs: Path, kinds: dict[str, str]) -> dict[str, list[TypeInfo]]:
    """The types of every `__init__.pyi` under the stubs folder, by namespace."""
    namespaces: dict[str, list[TypeInfo]] = {}
    files = sorted(stubs.rglob("__init__.pyi"))
    if not files:
        raise GeneratorError(f"no __init__.pyi under {stubs}")
    for pyi in files:
        parts = pyi.parent.relative_to(stubs).parts
        namespace = ".".join((ROOT_NAMESPACE, *parts))
        text = pyi.read_text(encoding="utf-8")
        try:
            tree = ast.parse(text)
        except SyntaxError as error:
            raise GeneratorError(f"{pyi}: {error}") from error
        lines = text.split("\n")
        types = [read_type(namespace, n, lines, kinds) for n in tree.body if isinstance(n, ast.ClassDef)]
        if types:
            namespaces[namespace] = sorted(types, key=lambda t: t.name)
    return namespaces


def link_target(info: TypeInfo, group: Group, uids: set[str]) -> str | None:
    """The uid of the .NET page of a member group, as written in a link, or None when there is no such page."""
    name = group.name
    if name == "__init__":
        candidates = [f"{info.uid}.#ctor*"]
    elif name in OPERATORS:
        candidates = [f"{info.uid}.{OPERATORS[name]}*"]
    elif group.plain:
        # An enum value or a field has a page of its own and no overload group.
        candidates = [f"{info.uid}.{name}"]
    else:
        # An event has a page of its own too, where a method or a property has an overload group.
        candidates = [f"{info.uid}.{name}*", f"{info.uid}.{name}"]
    for candidate in candidates:
        if candidate in uids:
            # docfx reads a # in a uid as the start of an anchor.
            return candidate.replace("#", "%23")
    return None


def type_page(info: TypeInfo, uids: set[str], unlinked: list[str]) -> tuple[str, int, int]:
    """The page of one type, and how many of its member groups it has and how many it links."""
    out = [
        f"---\nuid: py.{info.uid}\ntitle: {info.name} (Python)\n---\n",
        f"# {escape(info.name)}\n",
        f"`from {info.namespace} import {info.name}`\n",
    ]
    header = f"class {info.name}({info.bases})" if info.bases else f"class {info.name}"
    out.append(f"`{header}` &middot; [{escape(info.name)} in the .NET reference](xref:{info.uid})\n")
    out.extend(f"{p}\n" for p in paragraphs(info.doc))
    linked = 0
    for section in SECTIONS:
        groups = [g for g in info.groups if g.section == section]
        if not groups:
            continue
        out.append(f"## {section}\n")
        for group in groups:
            target = link_target(info, group, uids)
            out.append(f"### {escape(group.name)}\n")
            if target is None:
                unlinked.append(f"{info.uid}.{group.name}")
            else:
                linked += 1
                out.append(f"[.NET](xref:{target})\n")
            for member in group.members:
                out.append(f"```python\n{member.block}\n```\n")
                out.extend(f"{p}\n" for p in paragraphs(member.doc))
    return "\n".join(out), len(info.groups), linked


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(text.encode("utf-8"))


def build(stubs: Path, api: Path, out: Path, prose: str) -> Result:
    """Reads the stubs and the uids and writes every page under `out`, which is deleted first."""
    for folder, label in ((stubs, "stubs"), (api, "api")):
        if not folder.is_dir():
            raise GeneratorError(f"the {label} folder is missing: {folder}")
    for folder in (stubs.resolve(), api.resolve()):
        if out.resolve() == folder or out.resolve() in folder.parents:
            raise GeneratorError(f"the out folder would delete a folder this reads: {out}")
    uids, kinds = read_uids(api)
    namespaces = read_stubs(stubs, kinds)

    pages: dict[str, str] = {}
    unlinked: list[str] = []
    groups = linked = types = 0
    for namespace in sorted(namespaces):
        rows = []
        for info in namespaces[namespace]:
            if info.uid not in uids:
                raise GeneratorError(f"the type {info.uid} has no uid in {api}")
            kind = kinds.get(info.uid, "class")
            page, group_count, linked_count = type_page(info, uids, unlinked)
            pages[f"{namespace}/{info.name}.md"] = page
            groups += group_count
            linked += linked_count
            types += 1
            rows.append(f"| [{escape(info.name)}]({info.name}.md) | {kind} | {first_sentence(info.doc)} |")
        pages[f"{namespace}/index.md"] = (
            f"---\nuid: py.{namespace}\ntitle: {namespace} (Python)\n---\n\n# {namespace}\n\n"
            + "| Type | Kind | Summary |\n|---|---|---|\n"
            + "\n".join(rows)
            + "\n"
        )

    table = ["| Namespace | Types |", "|---|---|"]
    toc = ["- name: Overview", "  href: index.md"]
    for namespace in sorted(namespaces):
        table.append(f"| [{namespace}]({namespace}/index.md) | {len(namespaces[namespace])} |")
        toc.append(f"- name: {namespace}")
        toc.append(f"  href: {namespace}/index.md")
        toc.append("  items:")
        for info in namespaces[namespace]:
            toc.append(f"  - name: {info.name}")
            toc.append(f"    href: {namespace}/{info.name}.md")
    pages["index.md"] = prose.rstrip("\n") + "\n\n" + "\n".join(table) + "\n"
    pages["toc.yml"] = "\n".join(toc) + "\n"

    if out.exists():
        shutil.rmtree(out)
    for relative in sorted(pages):
        write_text(out / relative, pages[relative])
    page_count = sum(1 for name in pages if name.endswith(".md"))
    return Result(page_count, types, groups, linked, unlinked)


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(
        prog="make-api-pages.py",
        description="Writes the Python API reference of GeometryHelper as Markdown pages for docfx.",
        allow_abbrev=False,
    )
    parser.add_argument("--stubs", required=True, type=Path, help="the GeometryHelper-stubs folder")
    parser.add_argument("--api", required=True, type=Path, help="docfx's metadata folder (docfx/api)")
    parser.add_argument("--out", required=True, type=Path, help="the folder to write, deleted first")
    args = parser.parse_args(argv)
    prose_path = Path(__file__).resolve().with_name("api-index.md")
    try:
        prose = prose_path.read_text(encoding="utf-8")
        result = build(args.stubs, args.api, args.out, prose)
    except (GeneratorError, OSError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1
    print(result.summary())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
