#!/bin/sh
# Claude Code PostToolUse hook (Edit|Write|MultiEdit): formats the C# or XML file just written (.cs, .csproj, .props,
# .targets, .xml, .xaml) with the repo's CSharpier (dotnet-tools.json, .csharpierrc.json). Files nobody touches stay
# as they are and are formatted when edited. Never fails the tool call: every outcome exits 0.
f=$(node "$(dirname "$0")/hook-file-path.js" 2>/dev/null)
case "$f" in
    *.cs | *.csproj | *.props | *.targets | *.xml | *.xaml) ;;
    *) exit 0 ;;
esac
[ -f "$f" ] || exit 0
cd "$(dirname "$f")" 2>/dev/null || exit 0
dotnet csharpier format "$f" >/dev/null 2>&1
exit 0
