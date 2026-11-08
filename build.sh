#!/usr/bin/env bash
# build.sh — compile-check for the Morsoul Unity project.
#
# Builds every Morsoul.* asmdef assembly (runtime + editor + EditMode tests)
# with the C# compiler bundled in the Unity install, against the engine's
# managed assemblies and Library/ScriptAssemblies. Unity itself is NOT
# launched. Prints an error/warning count per assembly; exit code is nonzero
# when any assembly has errors.
#
# Usage: ./build.sh            (from the project root)
#        UNITY_DATA=".../Editor/Data" ./build.sh   (override Unity install)

set -uo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd -W 2>/dev/null || pwd)"
UNITY_DATA="${UNITY_DATA:-C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Data}"
DOTNET="$UNITY_DATA/DotNetSdk/dotnet.exe"
CSC="$(ls "$UNITY_DATA"/DotNetSdk/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | head -n 1)"
OUT="$ROOT/Temp/build"
mkdir -p "$OUT"

if [ ! -x "$DOTNET" ] || [ ! -f "$CSC" ]; then
    echo "build.sh: Unity dotnet/csc not found under UNITY_DATA='$UNITY_DATA'" >&2
    exit 2
fi

# --- Reference set -----------------------------------------------------------
REFS=()
# Engine + editor modules and the netstandard2.1 reference surface.
for d in "$UNITY_DATA"/Managed/UnityEngine/*.dll \
         "$UNITY_DATA"/Managed/UnityEditor/*.dll \
         "$UNITY_DATA"/NetStandard/ref/2.1.0/netstandard.dll \
         "$UNITY_DATA"/NetStandard/compat/2.1.0/shims/netstandard/*.dll \
         "$UNITY_DATA"/NetStandard/compat/2.1.0/shims/netfx/*.dll \
         "$UNITY_DATA"/BCLExtensions/TargetingPacks/netstandard2.1/ref/*.dll; do
    [ -f "$d" ] && REFS+=("-r:$d")
done
# Package/compiled assemblies (InputSystem, TestRunner, ...); skip the very
# assemblies we are compiling here to avoid type collisions.
for d in "$ROOT"/Library/ScriptAssemblies/*.dll; do
    base="$(basename "$d")"
    case "$base" in
        Morsoul.*|Assembly-CSharp*) ;;
        *) REFS+=("-r:$d") ;;
    esac
done
# NUnit for the test assembly.
NUNIT="$(ls "$ROOT"/Library/PackageCache/com.unity.ext.nunit@*/net472/unity-custom/nunit.framework.dll 2>/dev/null | head -n 1)"
[ -n "$NUNIT" ] && REFS+=("-r:$NUNIT")

# --- Compile driver ------------------------------------------------------------
errors=0
warnings=0

# compile <assembly-name> <extra -r: refs...> -- <source files...>
compile() {
    local name="$1"; shift
    local extra_refs=()
    while [ "$1" != "--" ]; do extra_refs+=("$1"); shift; done
    shift
    # Arguments go through a response file: ~400 -r: refs exceed the Windows
    # command-line limit, which previously failed silently and reported OK.
    local rsp="$OUT/$name.rsp"
    : > "$rsp"
    local a
    for a in -nologo -nostdlib -langversion:9 -target:library -nowarn:1701 \
             "-out:$OUT/$name.dll" "$@" "${extra_refs[@]}" "${REFS[@]}"; do
        printf '"%s"\n' "$a" >> "$rsp"
    done
    rm -f "$OUT/$name.dll"
    local log status
    log="$("$DOTNET" "$CSC" -noconfig "@$rsp" 2>&1)"; status=$?
    local e w
    e="$(printf '%s\n' "$log" | grep -c 'error CS' || true)"
    w="$(printf '%s\n' "$log" | grep -c 'warning CS' || true)"
    if [ "$status" -ne 0 ] || [ ! -f "$OUT/$name.dll" ]; then
        [ "$e" -eq 0 ] && e=1
    fi
    errors=$((errors + e)); warnings=$((warnings + w))
    if [ "$e" -gt 0 ] || [ "$w" -gt 0 ]; then
        printf '%-28s %s error(s), %s warning(s)\n%s\n' "$name" "$e" "$w" "$log"
    else
        printf '%-28s OK\n' "$name"
    fi
}

# Source glob: top-level .cs of a Scripts folder (Editor subfolders excluded).
src() { find "$ROOT/Assets/Scripts/$1" -maxdepth 1 -name '*.cs' -print; }
tst() { find "$ROOT/Assets/Tests/$1" -maxdepth 1 -name '*.cs' -print; }

# Runtime assemblies, in dependency order.
compile Morsoul.Engine -- $(src Engine)
compile Morsoul.Data "-r:$OUT/Morsoul.Engine.dll" -- $(src Data)
compile Morsoul.Core "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Engine.dll" -- $(src Core)
compile Morsoul.Gameplay "-r:$OUT/Morsoul.Data.dll" -- $(src Gameplay)
compile Morsoul.Dialogue "-r:$OUT/Morsoul.Data.dll" -- $(src Dialogue)
compile Morsoul.Board "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Engine.dll" -- $(src Board)
compile Morsoul.Interrogation "-r:$OUT/Morsoul.Data.dll" -- $(src Interrogation)
compile Morsoul.Hub "-r:$OUT/Morsoul.Core.dll" "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Dialogue.dll" \
    "-r:$OUT/Morsoul.Board.dll" "-r:$OUT/Morsoul.Engine.dll" -- $(src Hub)
compile Morsoul.Journal "-r:$OUT/Morsoul.Core.dll" "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Hub.dll" -- $(src Journal)
compile Morsoul.Cutscene "-r:$OUT/Morsoul.Core.dll" "-r:$OUT/Morsoul.Data.dll" -- $(src Cutscene)
compile Morsoul.Title "-r:$OUT/Morsoul.Core.dll" "-r:$OUT/Morsoul.Data.dll" -- $(src Title)
compile Morsoul.Resolution "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Gameplay.dll" -- $(src Resolution)
compile Morsoul.Flow \
    "-r:$OUT/Morsoul.Core.dll" "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Engine.dll" \
    "-r:$OUT/Morsoul.Board.dll" "-r:$OUT/Morsoul.Gameplay.dll" \
    "-r:$OUT/Morsoul.Interrogation.dll" "-r:$OUT/Morsoul.Dialogue.dll" "-r:$OUT/Morsoul.Cutscene.dll" \
    "-r:$OUT/Morsoul.Resolution.dll" "-r:$OUT/Morsoul.Hub.dll" "-r:$OUT/Morsoul.Journal.dll" \
    -- $(src Flow)
compile Morsoul.Dev \
    "-r:$OUT/Morsoul.Core.dll" "-r:$OUT/Morsoul.Data.dll" \
    "-r:$OUT/Morsoul.Hub.dll" "-r:$OUT/Morsoul.Dialogue.dll" \
    -- $(src Dev)

# Editor assemblies.
compile Morsoul.Data.Editor "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Engine.dll" -- $(src Data/Editor)
compile Morsoul.Board.Editor "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Board.dll" -- $(src Board/Editor)
compile Morsoul.Dialogue.Editor "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Dialogue.dll" -- $(src Dialogue/Editor)
compile Morsoul.Interrogation.Editor "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Interrogation.dll" -- $(src Interrogation/Editor)
compile Morsoul.Hub.Editor \
    "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Dialogue.dll" "-r:$OUT/Morsoul.Hub.dll" \
    "-r:$OUT/Morsoul.Journal.dll" "-r:$OUT/Morsoul.Board.dll" "-r:$OUT/Morsoul.Dialogue.Editor.dll" \
    -- $(src Hub/Editor)
compile Morsoul.Journal.Editor \
    "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Hub.dll" "-r:$OUT/Morsoul.Journal.dll" \
    -- $(src Journal/Editor)
compile Morsoul.Cutscene.Editor "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Cutscene.dll" -- $(src Cutscene/Editor)
compile Morsoul.Title.Editor "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Title.dll" -- $(src Title/Editor)
compile Morsoul.Resolution.Editor \
    "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Gameplay.dll" "-r:$OUT/Morsoul.Resolution.dll" \
    -- $(src Resolution/Editor)
compile Morsoul.Flow.Editor \
    "-r:$OUT/Morsoul.Core.dll" "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Data.Editor.dll" \
    "-r:$OUT/Morsoul.Board.dll" "-r:$OUT/Morsoul.Gameplay.dll" \
    "-r:$OUT/Morsoul.Interrogation.dll" "-r:$OUT/Morsoul.Dialogue.dll" "-r:$OUT/Morsoul.Cutscene.dll" \
    "-r:$OUT/Morsoul.Resolution.dll" "-r:$OUT/Morsoul.Hub.dll" "-r:$OUT/Morsoul.Hub.Editor.dll" \
    "-r:$OUT/Morsoul.Journal.dll" "-r:$OUT/Morsoul.Flow.dll" "-r:$OUT/Morsoul.Dialogue.Editor.dll" \
    -- $(src Flow/Editor)
compile Morsoul.Dev.Editor \
    "-r:$OUT/Morsoul.Dev.dll" \
    "-r:$OUT/Morsoul.Core.dll" "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Data.Editor.dll" \
    "-r:$OUT/Morsoul.Dialogue.dll" "-r:$OUT/Morsoul.Dialogue.Editor.dll" \
    "-r:$OUT/Morsoul.Hub.dll" "-r:$OUT/Morsoul.Hub.Editor.dll" \
    "-r:$OUT/Morsoul.Flow.dll" "-r:$OUT/Morsoul.Flow.Editor.dll" \
    -- $(src Dev/Editor)

# EditMode tests.
compile Morsoul.Tests.EditMode \
    "-r:$OUT/Morsoul.Core.dll" \
    "-r:$OUT/Morsoul.Engine.dll" \
    "-r:$OUT/Morsoul.Data.dll" "-r:$OUT/Morsoul.Data.Editor.dll" \
    "-r:$OUT/Morsoul.Board.dll" "-r:$OUT/Morsoul.Cutscene.dll" \
    "-r:$OUT/Morsoul.Dialogue.dll" "-r:$OUT/Morsoul.Gameplay.dll" \
    "-r:$OUT/Morsoul.Hub.dll" "-r:$OUT/Morsoul.Interrogation.dll" \
    "-r:$OUT/Morsoul.Journal.dll" \
    "-r:$OUT/Morsoul.Resolution.dll" "-r:$OUT/Morsoul.Title.dll" \
    "-r:$OUT/Morsoul.Flow.dll" \
    -- $(tst EditMode)

echo "---------------------------------------------------------------"
printf 'TOTAL: %d error(s), %d warning(s)\n' "$errors" "$warnings"
[ "$errors" -eq 0 ]
