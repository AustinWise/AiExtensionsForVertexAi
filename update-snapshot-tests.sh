#!/usr/bin/env bash

set -e

export AWISE_AIEXTENSIONSFORVERTEXAI_RECORD_SNAPSHOT_TESTS=1

dotnet test tests/SnapshotTests/SnapshotTests.csproj "$@"
