#!/usr/bin/env bash
# Exercises release preparation in a disposable shallow clone without tags.
set -euo pipefail

repo="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
prepare="$repo/tools/prepare-release-build.sh"
root="$(mktemp -d)"
trap 'rm -rf -- "$root"' EXIT

mkdir "$root/source"
git -C "$root/source" init -q
git -C "$root/source" config user.name 'Release version test'
git -C "$root/source" config user.email 'release-version@invalid.test'
cat > "$root/source/VersionFixture.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <Import Project="$(VersionTargetsPath)" Condition="'$(VersionTargetsPath)' != ''" />
</Project>
EOF
cat > "$root/source/Program.cs" <<'EOF'
using System;
using System.Diagnostics;
using System.Reflection;

var assembly = Assembly.GetExecutingAssembly();
Console.WriteLine($"{assembly.GetName().Version}|{FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion}");
EOF
# Keep build outputs out of the fixture's commits.
printf 'bin/\nobj/\n' > "$root/source/.gitignore"
git -C "$root/source" add .
git -C "$root/source" commit -qm 'Initial fixture'
git -C "$root/source" tag v2.0.0
git -C "$root/source" commit --allow-empty -qm 'Release candidate'
candidate="$(git -C "$root/source" rev-parse HEAD)"
git clone -q --no-local --depth 1 --no-tags "$root/source" "$root/checkout"
cd "$root/checkout"

[[ "$(git rev-parse --is-shallow-repository)" == true ]]
[[ -z "$(git tag --list)" ]]

expect_failure() {
  if bash "$prepare" "$1" "$2" > "$root/rejected-env" 2> "$root/rejected-log"; then
    echo "Unexpectedly accepted release tag '$1' and commit '$2'." >&2
    exit 1
  fi
  [[ ! -s "$root/rejected-env" ]]
}

expect_failure '' "$candidate"
expect_failure 'v2.1.0-beta' "$candidate"
expect_failure $'v2.1.0\nVersionPrefix=99.0.0' "$candidate"
expect_failure 'v2.1.0' '0000000000000000000000000000000000000000'
[[ -z "$(git tag --list)" ]]

bash "$prepare" v2.1.0 "$candidate" > "$root/github-env"
[[ "$(git rev-parse 'refs/tags/v2.1.0^{commit}')" == "$candidate" ]]
[[ "$(git describe --tags --long --match 'v*')" == v2.1.0-0-g* ]]
if git symbolic-ref -q HEAD; then
  echo 'Release build must use a detached tag checkout.' >&2
  exit 1
fi
[[ -z "$(git -C "$root/source" tag --list v2.1.0)" ]]
expect_failure 'v2.1.0' "$candidate"

# Consume exactly the environment file that the workflow supplies to later steps.
set -a
source "$root/github-env"
set +a
[[ "$RELEASE_VERSION" == 2.1.0 ]]
[[ "$VersionPrefix" == 2.1.0 ]]

targets="$repo/src/PinentryForWindows/Directory.Build.targets"
if command -v cygpath > /dev/null 2>&1; then
  targets="$(cygpath -w "$targets")"
fi

check_version() {
  if ! dotnet build VersionFixture.csproj -c Release --nologo "$@" > "$root/build.log" 2>&1; then
    cat "$root/build.log" >&2
    exit 1
  fi
  local actual
  actual="$(dotnet bin/Release/net10.0/VersionFixture.dll | tr -d '\r')"
  if [[ "$actual" != '2.1.0.0|2.1.0.0' ]]; then
    echo "Wrong assembly/file version: $actual" >&2
    exit 1
  fi
}

# The Windows app derives its version from Git; the relay uses SDK versioning.
check_version "-p:VersionTargetsPath=$targets"
check_version
echo 'Release version regression checks passed.'
