#!/usr/bin/env python3
"""
Generate symbol prefix header for wrapper compilation.

Creates a C header with #define macros to rename FT_*/hb_* symbols
to prefixed versions (__ut_*) for linking with post-processed libraries.

Usage:
    python generate_prefix_header.py --prefix __ut_ --wrapper-only -o ut_wrapper_prefix.h
"""

import sys
import argparse
from pathlib import Path
from typing import Set

DEFAULT_PREFIX = "__ut_"

# Symbols used by wrapper (unitext_native.cpp, webgl_wrapper.c)
WRAPPER_FREETYPE_SYMBOLS = {
    'FT_Init_FreeType', 'FT_Done_FreeType',
    'FT_New_Memory_Face', 'FT_Done_Face',
    'FT_Set_Pixel_Sizes', 'FT_Set_Char_Size', 'FT_Select_Size',
    'FT_Get_Char_Index', 'FT_Load_Glyph', 'FT_Render_Glyph',
    'FT_Palette_Data_Get', 'FT_Palette_Select',
    'FT_Get_Color_Glyph_Layer', 'FT_Get_Color_Glyph_ClipBox',
    'FT_Get_Color_Glyph_Paint', 'FT_Get_Paint_Layers',
    'FT_Get_Paint', 'FT_Get_Colorline_Stops', 'FT_Load_Sfnt_Table',
    'FT_Get_Sfnt_Table', 'FT_Property_Set',
}

WRAPPER_HARFBUZZ_SYMBOLS = {
    'hb_blob_create', 'hb_blob_destroy',
    'hb_face_create', 'hb_face_destroy', 'hb_face_get_upem',
    'hb_font_create', 'hb_font_destroy', 'hb_font_get_face',
    'hb_font_get_glyph', 'hb_font_get_glyph_h_advance',
    'hb_ot_font_set_funcs',
    'hb_buffer_create', 'hb_buffer_destroy', 'hb_buffer_clear_contents',
    'hb_buffer_set_direction', 'hb_buffer_set_script',
    'hb_buffer_set_content_type', 'hb_buffer_set_flags',
    'hb_buffer_add_codepoints', 'hb_buffer_get_length',
    'hb_buffer_get_glyph_infos', 'hb_buffer_get_glyph_positions',
    'hb_shape',
}


def generate_header(symbols: Set[str], output_path: Path, prefix: str):
    """Generate C header with #define macros for symbol prefixing."""
    sorted_symbols = sorted(symbols)

    with open(output_path, 'w', encoding='utf-8') as f:
        f.write(f"""/*
 * Auto-generated symbol prefix header
 * Prefix: {prefix}
 * Symbols: {len(sorted_symbols)}
 *
 * Maps FT_xxx/hb_xxx calls to {prefix}xxx prefixed symbols.
 * Usage: Compile with -include {output_path.name}
 */

#ifndef UT_PREFIX_H
#define UT_PREFIX_H

/* FreeType */
""")
        for sym in sorted_symbols:
            if sym.startswith('FT_'):
                f.write(f"#define {sym} {prefix}{sym}\n")

        f.write("\n/* HarfBuzz */\n")
        for sym in sorted_symbols:
            if sym.startswith('hb_'):
                f.write(f"#define {sym} {prefix}{sym}\n")

        f.write("\n#endif /* UT_PREFIX_H */\n")

    print(f"Generated {output_path}: {len(sorted_symbols)} symbols")


def main():
    parser = argparse.ArgumentParser(description='Generate symbol prefix header')
    parser.add_argument('--prefix', default=DEFAULT_PREFIX, help=f'Symbol prefix (default: {DEFAULT_PREFIX})')
    parser.add_argument('--wrapper-only', action='store_true', help='Use wrapper symbols (required)')
    parser.add_argument('-o', '--output', type=Path, required=True, help='Output header file')

    args = parser.parse_args()

    if not args.wrapper_only:
        print("Error: --wrapper-only is required", file=sys.stderr)
        return 1

    symbols = WRAPPER_FREETYPE_SYMBOLS | WRAPPER_HARFBUZZ_SYMBOLS
    generate_header(symbols, args.output, args.prefix)
    return 0


if __name__ == '__main__':
    sys.exit(main())
