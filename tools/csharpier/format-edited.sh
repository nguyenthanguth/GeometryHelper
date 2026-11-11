#!/bin/sh
# Claude Code PostToolUse hook (Edit|Write): formats the .cs, .csproj or .xml file just written with the repo's
# CSharpier (dotnet-tools.json, .csharpierrc.json). Files nobody touches stay as they are and are formatted when edited.
# Never fails the tool call: every outcome exits 0.
f=$(node -e 'let s="";process.stdin.on("data",d=>s+=d).on("end",()=>{try{process.stdout.write((JSON.parse(s).tool_input.file_path||"").split(String.fromCharCode(92)).join("/"))}catch(e){}})' 2>/dev/null)
case "$f" in
  *.cs|*.csproj|*.xml) ;;
  *) exit 0 ;;
esac
[ -f "$f" ] || exit 0
cd "$(dirname "$f")" 2>/dev/null || exit 0
dotnet csharpier format "$f" >/dev/null 2>&1
exit 0
