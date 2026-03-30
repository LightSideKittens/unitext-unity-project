#!/bin/bash
# Patch FreeType SDF renderer: increase MAX_SPREAD from 32 to 256.
# FreeType's default limit of 32 pixels is too restrictive for UniText's
# configurable spread strength (AtlasPadding = PointSize * SpreadStrength).
set -e
SRC="${1:-freetype-src}/src/sdf/ftsdfcommon.h"
sed -i.bak 's/#define MAX_SPREAD  *[0-9]*/#define MAX_SPREAD  256/' "$SRC"
rm -f "$SRC.bak"
