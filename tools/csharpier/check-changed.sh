#!/bin/sh
# Checks with CSharpier only the .cs, .csproj and .xml files changed against a base (default: main), staged or not,
# plus new untracked ones. Old files nobody touched are not checked. Exit 1 when one is not formatted.
# Usage: sh tools/csharpier/check-changed.sh [base]   (run from the repo or a worktree root)
base=${1:-main}
files=$( { git diff --name-only --diff-filter=ACMR "$base" -- '*.cs' '*.csproj' '*.xml'; \
           git ls-files --others --exclude-standard -- '*.cs' '*.csproj' '*.xml'; } | sort -u )
[ -z "$files" ] && { echo "CSharpier: no changed .cs/.csproj/.xml files"; exit 0; }
n=$(echo "$files" | wc -l)
echo "$files" | xargs dotnet csharpier check
rc=$?; [ $rc -ne 0 ] && rc=1
[ $rc -eq 0 ] && echo "CSharpier: $n changed file(s) formatted" || echo "CSharpier: fix with: sh tools/csharpier/check-changed.sh $base  ->  dotnet csharpier format <file>"
exit $rc
