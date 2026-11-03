#!/usr/bin/env bash
set -euo pipefail

# Requires the disposable PostgreSQL service published on the CI runner at 5432.
image_name="${1:-pim4-api:ci}"
smoke_dir="$(mktemp -d)"
api_container="pim4-api-$(basename "$smoke_dir" | tr '[:upper:].' '[:lower:]-')"

cleanup() {
  result=$?
  if [ "$result" -ne 0 ]; then
    docker logs "$api_container" || true
  fi
  docker rm -f "$api_container" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker run --detach --name "$api_container" \
  --add-host host.docker.internal:host-gateway --publish 127.0.0.1:8080:8080 \
  --env ASPNETCORE_ENVIRONMENT=Production \
  --env Database__ApplyMigrationsOnStartup=true \
  --env Https__RedirectEnabled=false \
  --env Cors__AllowedOrigins__0=http://localhost:4200 \
  --env Jwt__Secret=ci-only-secret-not-for-production-at-least-32-bytes \
  --env 'ConnectionStrings__DefaultConnection=Host=host.docker.internal;Port=5432;Database=pim4_ci;Username=postgres;Password=ci-only-password' \
  "$image_name" >/dev/null

curl --fail --silent --show-error --retry 36 --retry-all-errors --retry-delay 2 \
  --max-time 5 http://127.0.0.1:8080/health --output "$smoke_dir/health.json"
curl --fail --silent --show-error --max-time 10 \
  http://127.0.0.1:8080/health/live --output "$smoke_dir/live.json"
curl --fail --silent --show-error --max-time 10 \
  http://127.0.0.1:8080/api/courses/catalog --output "$smoke_dir/catalog.json"
python3 - "$smoke_dir" <<'PY'
import json
import pathlib
import sys

root = pathlib.Path(sys.argv[1])
assert json.loads(root.joinpath('health.json').read_text())['status'] == 'healthy'
assert json.loads(root.joinpath('live.json').read_text())['status'] == 'alive'
catalog = json.loads(root.joinpath('catalog.json').read_text())
assert isinstance(catalog, dict) and isinstance(catalog.get('items'), list), 'Unexpected catalog'
PY
printf 'API image: startup, migrations, health and catalog verified against the CI database.\n'
