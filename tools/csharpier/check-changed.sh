#!/bin/sh
# Checks with CSharpier only the C# and XML files (.cs, .csproj, .props, .targets, .xml, .xaml) changed against a base
# (default: main), staged or not, plus new untracked ones. Old files nobody touched are not checked.
# Exits 1 when one is not formatted; format it with: dotnet csharpier format <file>
# Usage: sh tools/csharpier/check-changed.sh [base]   (run from the repo or a worktree root)
base=${1:-main}
set -- '*.cs' '*.csproj' '*.props' '*.targets' '*.xml' '*.xaml'
files=$( {
    git diff --name-only --diff-filter=ACMR "$base" -- "$@"
    git ls-files --others --exclude-standard -- "$@"
} | sort -u )
[ -z "$files" ] && { echo "CSharpier: no changed C# or XML files"; exit 0; }
n=$(echo "$files" | wc -l)
echo "$files" | xargs dotnet csharpier check
rc=$?
if [ $rc -eq 0 ]; then
    echo "CSharpier: $n changed file(s), all formatted"
else
    echo "CSharpier: format the files above with: dotnet csharpier format <file>"
    rc=1
fi
exit $rc
