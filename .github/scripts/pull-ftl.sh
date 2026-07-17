#!/usr/bin/env bash
set -euo pipefail

MATRIX="${1:?usage: pull-ftl.sh <testMatrixId> [outDir]}"
OUT="${2:-ftl-results}"
PROJECT="$(gcloud config get-value project 2>/dev/null)"

GCS=$(curl -s -H "Authorization: Bearer $(gcloud auth print-access-token)" \
  "https://testing.googleapis.com/v1/projects/${PROJECT}/testMatrices/${MATRIX}" \
  | python -c "import sys,json;print(json.load(sys.stdin)['resultStorage']['googleCloudStorage']['gcsPath'])")

echo "Matrix : $MATRIX"
echo "GCS    : $GCS"
mkdir -p "$OUT"

for dev in $(gsutil ls "$GCS" 2>/dev/null | grep -E '/$'); do
  name=$(basename "$dev")
  mkdir -p "$OUT/$name"
  gsutil -m cp "$dev/logcat" "$dev/*TOMBSTONE*" "$dev/*native_crash*" "$OUT/$name/" 2>/dev/null || true
done

echo "=== crashed devices (tombstones present) ==="
find "$OUT" -iname "*tombstone*" | while read -r t; do
  d=$(basename "$(dirname "$t")")
  sig=$(grep -m1 "signal " "$t" || true)
  thr=$(grep -m1 ">>>" "$t" || true)
  echo "$d: $sig | $thr"
done
