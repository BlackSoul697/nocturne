#!/usr/bin/env bash
# Regression test for the Helm bootstrap Job's password escaping.
#
# The bootstrap run.sh embeds role passwords into SQL SET statements. Before
# the escaping fix, a password containing a single quote produced broken SQL
# (e.g. `SET nocturne.web_password TO 'pw'rd';`) and the bootstrap Job failed
# under `ON_ERROR_STOP=1`. This test extracts run.sh verbatim from the Helm
# template, runs it against mock pg_isready/psql binaries, and asserts that
# single quotes in passwords are doubled (SQL-standard escaping).
#
# Requires sudo to place bootstrap-roles.sql at /scripts (the absolute path
# run.sh reads). On a GitHub Actions runner this is available without a
# password; locally use `sudo bash scripts/test-helm-bootstrap-escaping.sh`.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
template="$repo_root/deploy/helm/nocturne/templates/bootstrap-configmap.yaml"
roles_sql="$repo_root/deploy/helm/nocturne/files/bootstrap-roles.sql"

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# Extract run.sh verbatim (it is a literal block scalar in the template, so
# this does not need Helm to render).
awk '/^  run.sh: \|-/{flag=1;next} /^{{- end }}/{flag=0} flag{sub(/^    /,""); print}' \
  "$template" > "$work/run.sh"
test -s "$work/run.sh"
grep -q 'escape_sql_literal()' "$work/run.sh"

# Mock pg_isready (always healthy) and psql (dumps the SQL file it is asked
# to run via -f) so the Job completes without a real PostgreSQL server.
mkdir -p "$work/bin"
printf '%s\n' '#!/bin/sh' 'exit 0' > "$work/bin/pg_isready"
printf '%s\n' \
  '#!/bin/sh' \
  'prev=""' \
  'for a in "$@"; do' \
  '  if [ "$prev" = "-f" ]; then cat "$a"; fi' \
  '  prev="$a"' \
  'done' \
  'exit 0' > "$work/bin/psql"
chmod +x "$work/bin/pg_isready" "$work/bin/psql"

# run.sh reads /scripts/bootstrap-roles.sql (hardcoded absolute path).
sudo mkdir -p /scripts
sudo cp "$roles_sql" /scripts/bootstrap-roles.sql

out="$(PATH="$work/bin:$PATH" \
  PGHOST=localhost PGPORT=5432 PGDATABASE=nocturne PGUSER=postgres PGPASSWORD=admin \
  MIGRATOR_PASSWORD="mig'rator" APP_PASSWORD="ap'p" WEB_PASSWORD="we'b" \
  /bin/sh "$work/run.sh" 2>/dev/null)"

printf '%s\n' "$out" | grep -Fq "SET nocturne.migrator_password TO 'mig''rator';"
printf '%s\n' "$out" | grep -Fq "SET nocturne.app_password TO 'ap''p';"
printf '%s\n' "$out" | grep -Fq "SET nocturne.web_password TO 'we''b';"

echo "OK: bootstrap password escaping regression test passed."
