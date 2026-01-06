#!/usr/bin/env python3
"""
WASM Symbol Renamer - Renames symbols in WASM object files by adding a prefix.

This script edits the "linking" custom section's symbol table to add a prefix
to all symbol names. Works with both C and C++ symbols.

Used for WebGL builds to avoid symbol conflicts with Unity's built-in FreeType.
Unlike llvm-objcopy which doesn't support --redefine-syms for WASM, this script
works directly on the WASM binary format.

Reference: https://github.com/WebAssembly/tool-conventions/blob/main/Linking.md
"""

import sys
import struct
from pathlib import Path
from typing import BinaryIO, Tuple, List, Optional
from dataclasses import dataclass

# WASM constants
WASM_MAGIC = b'\x00asm'
WASM_VERSION = b'\x01\x00\x00\x00'
SECTION_CUSTOM = 0
SECTION_IMPORT = 2

# Import descriptor kinds
IMPORT_FUNC = 0
IMPORT_TABLE = 1
IMPORT_MEMORY = 2
IMPORT_GLOBAL = 3

# Symbol kinds
SYMTAB_FUNCTION = 0
SYMTAB_DATA = 1
SYMTAB_GLOBAL = 2
SYMTAB_SECTION = 3
SYMTAB_EVENT = 4
SYMTAB_TABLE = 5

# Symbol flags
WASM_SYM_UNDEFINED = 0x10
WASM_SYM_EXPLICIT_NAME = 0x40

# Linking subsection types
WASM_SYMBOL_TABLE = 8


def read_leb128_unsigned(data: bytes, offset: int) -> Tuple[int, int]:
    """Read unsigned LEB128, return (value, bytes_consumed)."""
    result = 0
    shift = 0
    consumed = 0
    while True:
        byte = data[offset + consumed]
        consumed += 1
        result |= (byte & 0x7F) << shift
        if (byte & 0x80) == 0:
            break
        shift += 7
    return result, consumed


def encode_leb128_unsigned(value: int) -> bytes:
    """Encode unsigned integer as LEB128."""
    result = bytearray()
    while True:
        byte = value & 0x7F
        value >>= 7
        if value != 0:
            byte |= 0x80
        result.append(byte)
        if value == 0:
            break
    return bytes(result)


@dataclass
class Symbol:
    """Represents a symbol from the symbol table."""
    kind: int
    flags: int
    index: Optional[int]
    name: Optional[str]
    # For data symbols
    segment_index: Optional[int] = None
    segment_offset: Optional[int] = None
    size: Optional[int] = None
    # For section symbols
    section_index: Optional[int] = None


def parse_symbol(data: bytes, offset: int) -> Tuple[Symbol, int]:
    """Parse a single symbol entry, return (Symbol, bytes_consumed)."""
    start = offset

    kind = data[offset]
    offset += 1

    flags, consumed = read_leb128_unsigned(data, offset)
    offset += consumed

    sym = Symbol(kind=kind, flags=flags, index=None, name=None)

    if kind in (SYMTAB_FUNCTION, SYMTAB_GLOBAL, SYMTAB_EVENT, SYMTAB_TABLE):
        # Function/Global/Event/Table symbol
        index, consumed = read_leb128_unsigned(data, offset)
        offset += consumed
        sym.index = index

        # Name is present if:
        # - Symbol is defined (not UNDEFINED), OR
        # - EXPLICIT_NAME flag is set
        is_undefined = (flags & WASM_SYM_UNDEFINED) != 0
        has_explicit_name = (flags & WASM_SYM_EXPLICIT_NAME) != 0

        if not is_undefined or has_explicit_name:
            name_len, consumed = read_leb128_unsigned(data, offset)
            offset += consumed
            sym.name = data[offset:offset + name_len].decode('utf-8')
            offset += name_len

    elif kind == SYMTAB_DATA:
        # Data symbol - always has name
        name_len, consumed = read_leb128_unsigned(data, offset)
        offset += consumed
        sym.name = data[offset:offset + name_len].decode('utf-8')
        offset += name_len

        # If defined, has segment info
        if (flags & WASM_SYM_UNDEFINED) == 0:
            seg_index, consumed = read_leb128_unsigned(data, offset)
            offset += consumed
            sym.segment_index = seg_index

            seg_offset, consumed = read_leb128_unsigned(data, offset)
            offset += consumed
            sym.segment_offset = seg_offset

            size, consumed = read_leb128_unsigned(data, offset)
            offset += consumed
            sym.size = size

    elif kind == SYMTAB_SECTION:
        # Section symbol - no name, just section index
        section, consumed = read_leb128_unsigned(data, offset)
        offset += consumed
        sym.section_index = section

    return sym, offset - start


def should_rename(name: str) -> bool:
    """Check if symbol should be renamed.

    Whitelist approach: only rename symbols from our libraries.
    This ensures definitions and imports are renamed consistently.
    """
    if not name:
        return False

    # Skip internal labels like .L.str, .Lswitch.table, etc.
    if name.startswith('.'):
        return False

    # Skip Emscripten/compiler runtime symbols (start with __)
    if name.startswith('__'):
        return False

    # FreeType (public API and internal modules)
    freetype_prefixes = ('FT_', 'ft_', 'ps_', 'cf2_', 'cff_', 't1_', 't42_',
                         'sfnt_', 'tt_', 'af_', 'TT_', 'T1_')
    if any(name.startswith(p) for p in freetype_prefixes):
        return True

    # HarfBuzz C API and internal symbols
    if name.startswith('hb_') or name.startswith('_hb_'):
        return True

    # HarfBuzz internal static data (specific names)
    hb_internal = {'minus_1', 'endchar_str', 'NullPool', 'CrapPool'}
    if name in hb_internal:
        return True

    # C++ mangled names (HarfBuzz C++ code)
    if name.startswith('_Z'):
        return True

    # libpng
    if name.startswith('png_'):
        return True

    # zlib - use prefix matching for all zlib symbols
    zlib_prefixes = (
        'deflate', 'inflate', 'compress', 'uncompress',
        'adler32', 'crc32', 'zlib', 'gz',
        # Internal zlib functions
        'zcalloc', 'zcfree', 'z_errmsg',
    )
    if any(name.startswith(p) or name == p for p in zlib_prefixes):
        return True

    return False


def should_rename_import(name: str) -> bool:
    """Check if an imported symbol should be renamed.

    Uses the same logic as should_rename() to ensure consistency.
    """
    return should_rename(name)


def process_import_section(data: bytes, prefix: str) -> bytes:
    """Process import section, renaming field names that match our patterns."""
    offset = 0
    result = bytearray()

    # Read import count
    count, consumed = read_leb128_unsigned(data, offset)
    offset += consumed
    result.extend(encode_leb128_unsigned(count))

    renamed_count = 0

    for _ in range(count):
        # Module name
        mod_len, consumed = read_leb128_unsigned(data, offset)
        offset += consumed
        mod_name = data[offset:offset + mod_len].decode('utf-8')
        offset += mod_len

        # Field name
        field_len, consumed = read_leb128_unsigned(data, offset)
        offset += consumed
        field_name = data[offset:offset + field_len].decode('utf-8')
        offset += field_len

        # Rename field if it matches our patterns
        if should_rename_import(field_name):
            new_field_name = prefix + field_name
            renamed_count += 1
        else:
            new_field_name = field_name

        # Import descriptor (kind + type info)
        desc_kind = data[offset]
        offset += 1

        # Read the rest of the descriptor based on kind
        if desc_kind == IMPORT_FUNC:
            # Function: typeidx (LEB128)
            type_idx, consumed = read_leb128_unsigned(data, offset)
            offset += consumed
            desc_data = bytes([desc_kind]) + encode_leb128_unsigned(type_idx)
        elif desc_kind == IMPORT_TABLE:
            # Table: elemtype (1 byte) + limits
            elem_type = data[offset]
            offset += 1
            limits_data, consumed = read_limits(data, offset)
            offset += consumed
            desc_data = bytes([desc_kind, elem_type]) + limits_data
        elif desc_kind == IMPORT_MEMORY:
            # Memory: limits
            limits_data, consumed = read_limits(data, offset)
            offset += consumed
            desc_data = bytes([desc_kind]) + limits_data
        elif desc_kind == IMPORT_GLOBAL:
            # Global: valtype (1 byte) + mut (1 byte)
            val_type = data[offset]
            mut = data[offset + 1]
            offset += 2
            desc_data = bytes([desc_kind, val_type, mut])
        else:
            raise ValueError(f"Unknown import descriptor kind: {desc_kind}")

        # Write import entry
        mod_bytes = mod_name.encode('utf-8')
        result.extend(encode_leb128_unsigned(len(mod_bytes)))
        result.extend(mod_bytes)

        field_bytes = new_field_name.encode('utf-8')
        result.extend(encode_leb128_unsigned(len(field_bytes)))
        result.extend(field_bytes)

        result.extend(desc_data)

    print(f"  Import section: renamed {renamed_count} of {count} imports")
    return bytes(result)


def read_limits(data: bytes, offset: int) -> Tuple[bytes, int]:
    """Read limits structure, return (encoded_bytes, bytes_consumed)."""
    start = offset
    flags = data[offset]
    offset += 1

    # Minimum
    min_val, consumed = read_leb128_unsigned(data, offset)
    offset += consumed

    result = bytes([flags]) + encode_leb128_unsigned(min_val)

    # Maximum (if flags & 1)
    if flags & 1:
        max_val, consumed = read_leb128_unsigned(data, offset)
        offset += consumed
        result += encode_leb128_unsigned(max_val)

    return result, offset - start


def encode_symbol(sym: Symbol, prefix: str) -> bytes:
    """Encode a symbol, adding prefix to the name if present."""
    result = bytearray()

    result.append(sym.kind)
    result.extend(encode_leb128_unsigned(sym.flags))

    if sym.kind in (SYMTAB_FUNCTION, SYMTAB_GLOBAL, SYMTAB_EVENT, SYMTAB_TABLE):
        result.extend(encode_leb128_unsigned(sym.index))

        is_undefined = (sym.flags & WASM_SYM_UNDEFINED) != 0
        has_explicit_name = (sym.flags & WASM_SYM_EXPLICIT_NAME) != 0

        if not is_undefined or has_explicit_name:
            # Only add prefix if symbol should be renamed
            new_name = (prefix + sym.name) if should_rename(sym.name) else sym.name
            name_bytes = new_name.encode('utf-8')
            result.extend(encode_leb128_unsigned(len(name_bytes)))
            result.extend(name_bytes)

    elif sym.kind == SYMTAB_DATA:
        # Only add prefix if symbol should be renamed
        new_name = (prefix + sym.name) if should_rename(sym.name) else sym.name
        name_bytes = new_name.encode('utf-8')
        result.extend(encode_leb128_unsigned(len(name_bytes)))
        result.extend(name_bytes)

        if (sym.flags & WASM_SYM_UNDEFINED) == 0:
            result.extend(encode_leb128_unsigned(sym.segment_index))
            result.extend(encode_leb128_unsigned(sym.segment_offset))
            result.extend(encode_leb128_unsigned(sym.size))

    elif sym.kind == SYMTAB_SECTION:
        result.extend(encode_leb128_unsigned(sym.section_index))

    return bytes(result)


def process_linking_section(content: bytes, prefix: str) -> bytes:
    """Process linking section, rename symbols in SYMTAB subsection."""
    offset = 0

    # Version
    version, consumed = read_leb128_unsigned(content, offset)
    offset += consumed

    result = bytearray()
    result.extend(encode_leb128_unsigned(version))

    # Process subsections
    while offset < len(content):
        subsec_type = content[offset]
        offset += 1

        subsec_size, consumed = read_leb128_unsigned(content, offset)
        offset += consumed

        subsec_data = content[offset:offset + subsec_size]
        offset += subsec_size

        if subsec_type == WASM_SYMBOL_TABLE:
            # Process symbol table
            new_subsec_data = process_symbol_table(subsec_data, prefix)
            result.append(subsec_type)
            result.extend(encode_leb128_unsigned(len(new_subsec_data)))
            result.extend(new_subsec_data)
        else:
            # Copy other subsections as-is
            result.append(subsec_type)
            result.extend(encode_leb128_unsigned(subsec_size))
            result.extend(subsec_data)

    return bytes(result)


def process_symbol_table(data: bytes, prefix: str) -> bytes:
    """Process symbol table, adding prefix to all symbol names."""
    offset = 0

    count, consumed = read_leb128_unsigned(data, offset)
    offset += consumed

    symbols = []
    for _ in range(count):
        sym, consumed = parse_symbol(data, offset)
        offset += consumed
        symbols.append(sym)

    # Encode with prefix
    result = bytearray()
    result.extend(encode_leb128_unsigned(count))

    renamed_count = 0
    skipped_count = 0
    for sym in symbols:
        if sym.name is not None:
            if should_rename(sym.name):
                renamed_count += 1
            else:
                skipped_count += 1
        result.extend(encode_symbol(sym, prefix))

    print(f"  Renamed {renamed_count} symbols, skipped {skipped_count} internal labels, {count} total")
    return bytes(result)


def process_wasm_file(input_path: Path, output_path: Path, prefix: str) -> None:
    """Process a WASM object file, renaming all symbols."""
    print(f"Processing: {input_path}")

    with open(input_path, 'rb') as f:
        data = f.read()

    # Verify WASM header
    if data[:4] != WASM_MAGIC:
        raise ValueError(f"Not a WASM file: {input_path}")
    if data[4:8] != WASM_VERSION:
        raise ValueError(f"Unsupported WASM version: {input_path}")

    offset = 8
    result = bytearray(data[:8])

    linking_found = False
    import_processed = False

    while offset < len(data):
        section_id = data[offset]
        offset += 1

        section_size, consumed = read_leb128_unsigned(data, offset)
        offset += consumed

        section_data = data[offset:offset + section_size]
        offset += section_size

        if section_id == SECTION_IMPORT:
            # Process import section - rename imported symbol names
            print(f"  Found import section ({section_size} bytes)")
            new_section_data = process_import_section(section_data, prefix)

            result.append(section_id)
            result.extend(encode_leb128_unsigned(len(new_section_data)))
            result.extend(new_section_data)
            import_processed = True

        elif section_id == SECTION_CUSTOM:
            # Read custom section name
            name_len, name_consumed = read_leb128_unsigned(section_data, 0)
            name = section_data[name_consumed:name_consumed + name_len].decode('utf-8')
            content = section_data[name_consumed + name_len:]

            if name == "linking":
                linking_found = True
                print(f"  Found 'linking' section ({len(content)} bytes)")

                # Process linking section
                new_content = process_linking_section(content, prefix)

                # Rebuild section
                name_bytes = name.encode('utf-8')
                new_section_data = bytearray()
                new_section_data.extend(encode_leb128_unsigned(len(name_bytes)))
                new_section_data.extend(name_bytes)
                new_section_data.extend(new_content)

                result.append(section_id)
                result.extend(encode_leb128_unsigned(len(new_section_data)))
                result.extend(new_section_data)

                print(f"  New 'linking' section size: {len(new_content)} bytes")
            else:
                # Copy other custom sections as-is
                result.append(section_id)
                result.extend(encode_leb128_unsigned(section_size))
                result.extend(section_data)
        else:
            # Copy non-custom sections as-is
            result.append(section_id)
            result.extend(encode_leb128_unsigned(section_size))
            result.extend(section_data)

    if not linking_found:
        raise ValueError(f"No 'linking' section found - is this an object file? {input_path}")

    if import_processed:
        print(f"  Processed import section")

    with open(output_path, 'wb') as f:
        f.write(result)

    print(f"  Written: {output_path}")


def main():
    if len(sys.argv) < 3:
        print("Usage: wasm_symbol_renamer.py <prefix> <file1.o> [file2.o ...]")
        print("       wasm_symbol_renamer.py <prefix> <file.a>")
        print()
        print("Renames all symbols in WASM object files by adding a prefix.")
        print("Works with both .o files and .a archives (extracts, renames, repacks).")
        sys.exit(1)

    prefix = sys.argv[1]
    files = [Path(f) for f in sys.argv[2:]]

    print(f"Symbol prefix: '{prefix}'")

    for file_path in files:
        if file_path.suffix == '.a':
            process_archive(file_path, prefix)
        elif file_path.suffix == '.o':
            output_path = file_path  # Overwrite in place
            process_wasm_file(file_path, output_path, prefix)
        else:
            print(f"Warning: Unknown file type: {file_path}")


def process_archive(archive_path: Path, prefix: str) -> None:
    """Process a .a archive file."""
    import subprocess
    import tempfile
    import shutil

    print(f"Processing archive: {archive_path}")

    # Create temp directory
    with tempfile.TemporaryDirectory() as tmpdir:
        tmpdir = Path(tmpdir)

        # Extract archive
        # Use emar (Emscripten's ar wrapper) for WASM archives
        subprocess.run(
            ['emar', 'x', str(archive_path.absolute())],
            cwd=tmpdir,
            check=True
        )

        # Find all .o files
        object_files = list(tmpdir.glob('*.o'))
        print(f"  Found {len(object_files)} object files")

        # Process each object file
        for obj_file in object_files:
            try:
                process_wasm_file(obj_file, obj_file, prefix)
            except ValueError as e:
                print(f"  Skipping {obj_file.name}: {e}")

        # Repack archive
        # First remove old archive
        archive_path.unlink()

        # Create new archive
        subprocess.run(
            ['emar', 'rcs', str(archive_path.absolute())] +
            [str(f) for f in object_files],
            cwd=tmpdir,
            check=True
        )

    print(f"  Repacked: {archive_path}")


if __name__ == '__main__':
    main()
