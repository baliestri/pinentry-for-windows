#!/usr/bin/env bash
# Prepare an unpublished release tag and emit a GitHub Actions environment file.
set -euo pipefail

tag="${1:-}"
candidate="${2:-}"
if [[ ! "$tag" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo 'Expected release tag vMAJOR.MINOR.PATCH.' >&2
  exit 1
fi
if [[ ! "$candidate" =~ ^[0-9a-f]{40}$ ]] || [[ "$(git rev-parse HEAD)" != "$candidate" ]]; then
  echo 'Release checkout does not match the candidate commit.' >&2
  exit 1
fi
if git show-ref --verify --quiet "refs/tags/$tag"; then
  echo 'Release tag already exists; refusing to replace it.' >&2
  exit 1
fi

git tag "$tag" "$candidate"
git checkout --detach "refs/tags/$tag" > /dev/null
if [[ "$(git rev-parse HEAD)" != "$candidate" ]] ||
   [[ "$(git describe --tags --long --match 'v*')" != "$tag"-0-g* ]]; then
  echo 'Release tag or Git version does not match the candidate.' >&2
  exit 1
fi

# MSBuild imports VersionPrefix from the environment for every project, including
# the relay. The Windows project additionally derives its Git metadata from the tag.
printf 'RELEASE_VERSION=%s\nVersionPrefix=%s\n' "${tag#v}" "${tag#v}"
