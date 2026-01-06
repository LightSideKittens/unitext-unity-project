#!/usr/bin/env python3
"""
Mach-O Symbol Renamer - Renames symbols in Mach-O static libraries by adding a prefix.

This script uses nm to extract symbols and llvm-objcopy to rename them.
Works with both C and C++ symbols, including mangled names.

Used for iOS/tvOS builds to avoid symbol conflicts with Unity's built-in libraries.

Usage:
    python macho_symbol_renamer.py <prefix> <file1.a> [file2.a ...]

Example:
    python macho_symbol_renamer.py __ut_ libfreetype.a libharfbuzz.a
"""

import sys
import subprocess
import tempfile
from pathlib import Path
from typing import Dict, Set

# =============================================================================
# Symbol patterns (same as wasm_symbol_renamer.py)
# =============================================================================

# FreeType public API and internal modules
FREETYPE_PREFIXES = ('FT_', 'ft_', 'ps_', 'cf2_', 'cff_', 't1_', 't42_',
                     'sfnt_', 'tt_', 'af_', 'TT_', 'T1_')

# HarfBuzz internal static data (specific names)
HARFBUZZ_INTERNAL = {'minus_1', 'endchar_str', 'NullPool', 'CrapPool'}


def should_rename(name: str) -> bool:
    """Check if symbol should be renamed.

    Whitelist approach: only rename symbols from our libraries.
    This ensures definitions and imports are renamed consistently.

    Note: This is the same logic as wasm_symbol_renamer.py.
    """
    if not name:
        return False

    # Skip internal labels like .L.str, .Lswitch.table, etc.
    if name.startswith('.'):
        return False

    # Skip compiler runtime symbols (start with __)
    if name.startswith('__'):
        return False

    # FreeType (public API and internal modules)
    if any(name.startswith(p) for p in FREETYPE_PREFIXES):
        return True

    # HarfBuzz C API and internal symbols
    if name.startswith('hb_') or name.startswith('_hb_'):
        return True

    # HarfBuzz internal static data (specific names)
    if name in HARFBUZZ_INTERNAL:
        return True

    # C++ mangled names (HarfBuzz C++ code)
    if name.startswith('_Z'):
        return True

    return False


def should_rename_macho(name: str) -> bool:
    """Adapt should_rename for Mach-O naming convention.

    Mach-O symbols have a leading underscore:
    - _FT_Init_FreeType (C function)
    - __ZN11hb_buffer_t... (C++ mangled, starts with __)

    We strip the leading underscore before checking patterns.
    """
    if not name or not name.startswith('_'):
        return False

    # Strip Mach-O leading underscore
    stripped = name[1:]

    # C++ mangled names: __Z... -> _Z... after strip
    # The original C++ mangled name is _Z..., Mach-O adds another underscore
    if stripped.startswith('_Z'):
        return True

    return should_rename(stripped)


def get_llvm_objcopy() -> str:
    """Find llvm-objcopy executable."""
    # Try Homebrew LLVM first (macOS)
    homebrew_path = Path('/opt/homebrew/opt/llvm/bin/llvm-objcopy')
    if homebrew_path.exists():
        return str(homebrew_path)

    # Try Intel Homebrew
    intel_homebrew = Path('/usr/local/opt/llvm/bin/llvm-objcopy')
    if intel_homebrew.exists():
        return str(intel_homebrew)

    # Try environment variable
    import os
    if 'LLVM_OBJCOPY' in os.environ:
        return os.environ['LLVM_OBJCOPY']

    # Fall back to PATH
    return 'llvm-objcopy'


def generate_symbol_map(lib_path: Path, prefix: str) -> Dict[str, str]:
    """Run nm and filter symbols to rename.

    Returns a dict of {old_symbol: new_symbol}.
    """
    result = subprocess.run(
        ['nm', '-g', str(lib_path)],
        capture_output=True, text=True
    )

    if result.returncode != 0:
        print(f"Warning: nm failed for {lib_path}: {result.stderr}", file=sys.stderr)
        return {}

    symbol_map = {}
    for line in result.stdout.splitlines():
        parts = line.split()
        # nm output format: "address type symbol" or "type symbol" (for undefined)
        if len(parts) >= 2:
            # Symbol is always the last column
            sym = parts[-1]
            # Type is second-to-last (T, t, D, d, B, b, etc.)
            sym_type = parts[-2] if len(parts) >= 2 else ''

            # Only rename defined symbols (uppercase type) and data symbols
            if len(sym_type) == 1 and sym_type in 'TtDdBbSs':
                if should_rename_macho(sym):
                    # _FT_Init -> ___ut_FT_Init
                    # Keep leading underscore, add prefix after it
                    new_sym = '_' + prefix + sym[1:]
                    symbol_map[sym] = new_sym

    return symbol_map


def process_library(lib_path: Path, prefix: str) -> None:
    """Rename symbols in a Mach-O static library."""
    print(f"Processing: {lib_path}")

    symbol_map = generate_symbol_map(lib_path, prefix)

    if not symbol_map:
        print(f"  No symbols to rename")
        return

    # Write symbol map to temp file
    with tempfile.NamedTemporaryFile(mode='w', suffix='.txt', delete=False) as f:
        for old, new in sorted(symbol_map.items()):
            f.write(f'{old} {new}\n')
        map_file = Path(f.name)

    try:
        # Apply symbol renaming with llvm-objcopy
        objcopy = get_llvm_objcopy()
        result = subprocess.run(
            [objcopy, f'--redefine-syms={map_file}', str(lib_path)],
            capture_output=True, text=True
        )

        if result.returncode != 0:
            print(f"Error: llvm-objcopy failed: {result.stderr}", file=sys.stderr)
            sys.exit(1)

        # Count by category
        ft_count = sum(1 for s in symbol_map if any(s[1:].startswith(p) for p in FREETYPE_PREFIXES))
        hb_count = sum(1 for s in symbol_map if s[1:].startswith('hb_') or s[1:].startswith('_hb_'))
        cpp_count = sum(1 for s in symbol_map if s.startswith('__Z'))

        print(f"  Renamed {len(symbol_map)} symbols (FT: {ft_count}, HB: {hb_count}, C++: {cpp_count})")

    finally:
        map_file.unlink()


def main():
    if len(sys.argv) < 3:
        print("Usage: macho_symbol_renamer.py <prefix> <file1.a> [file2.a ...]")
        print()
        print("Renames symbols in Mach-O static libraries by adding a prefix.")
        print("Uses llvm-objcopy for the actual renaming.")
        print()
        print("Example:")
        print("  macho_symbol_renamer.py __ut_ libfreetype.a libharfbuzz.a")
        sys.exit(1)

    prefix = sys.argv[1]
    files = [Path(f) for f in sys.argv[2:]]

    print(f"Symbol prefix: '{prefix}'")

    for file_path in files:
        if not file_path.exists():
            print(f"Error: File not found: {file_path}", file=sys.stderr)
            sys.exit(1)

        if file_path.suffix != '.a':
            print(f"Warning: Expected .a file, got: {file_path}")

        process_library(file_path, prefix)


if __name__ == '__main__':
    main()
