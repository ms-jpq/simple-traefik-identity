#!/bin/bash

set -eu
set -o pipefail

IMAGE="msjpq/simple-traefik-identity:latest"

cd "$(dirname "$0")"
docker build -t "$IMAGE" .

if [[ $# -gt 0 ]]
then
  docker push "$IMAGE"
fi
