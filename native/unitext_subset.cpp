// UniText Subset - Editor-only font subsetting library
// Exposes HarfBuzz subset API directly (no wrappers needed - separate DLL = no conflicts)
//
// Platforms: Windows x64, macOS Universal, Linux x64

#include <hb.h>
#include <hb-ot.h>
#include <hb-subset.h>
#include <cstring>

#ifdef _WIN32
#define EXPORT extern "C" __declspec(dllexport)
#else
#define EXPORT extern "C" __attribute__((visibility("default")))
#endif

// High-level convenience function for C# - does everything in one call
// Returns subset font size, or 0 on failure
// Pass outData=null to query required size first
EXPORT unsigned int subset_font(
    const void* fontData, unsigned int fontDataSize,
    const unsigned int* codepoints, unsigned int codepointCount,
    void* outData, unsigned int outDataCapacity)
{
    if (!fontData || !fontDataSize || !codepoints || !codepointCount)
        return 0;

    hb_blob_t* blob = hb_blob_create((const char*)fontData, fontDataSize,
                                      HB_MEMORY_MODE_READONLY, nullptr, nullptr);
    if (!blob) return 0;

    hb_face_t* face = hb_face_create(blob, 0);
    hb_blob_destroy(blob);
    if (!face) return 0;

    hb_subset_input_t* input = hb_subset_input_create_or_fail();
    if (!input) {
        hb_face_destroy(face);
        return 0;
    }

    hb_set_t* unicodes = hb_subset_input_unicode_set(input);
    for (unsigned int i = 0; i < codepointCount; i++)
        hb_set_add(unicodes, codepoints[i]);

    hb_face_t* subset = hb_subset_or_fail(face, input);
    hb_subset_input_destroy(input);
    hb_face_destroy(face);
    if (!subset) return 0;

    hb_blob_t* result = hb_face_reference_blob(subset);
    hb_face_destroy(subset);
    if (!result) return 0;

    unsigned int size = 0;
    const char* data = hb_blob_get_data(result, &size);

    if (outData && outDataCapacity >= size && data)
        memcpy(outData, data, size);

    hb_blob_destroy(result);
    return size;
}

// Removes specific Unicode codepoints from a font.
// Starts with full Unicode range (0..10FFFF), removes specified codepoints.
// HarfBuzz intersects with the font's cmap during subsetting — missing codepoints are ignored.
// GSUB closure is applied (default behavior).
// Returns subset font size, or 0 on failure.
// Pass outData=null to query required size first.
EXPORT unsigned int subset_font_remove_codepoints(
    const void* fontData, unsigned int fontDataSize,
    const unsigned int* codepoints, unsigned int codepointCount,
    void* outData, unsigned int outDataCapacity)
{
    if (!fontData || !fontDataSize || !codepoints || !codepointCount)
        return 0;

    hb_blob_t* blob = hb_blob_create((const char*)fontData, fontDataSize,
                                      HB_MEMORY_MODE_READONLY, nullptr, nullptr);
    if (!blob) return 0;

    hb_face_t* face = hb_face_create(blob, 0);
    hb_blob_destroy(blob);
    if (!face) return 0;

    hb_subset_input_t* input = hb_subset_input_create_or_fail();
    if (!input) {
        hb_face_destroy(face);
        return 0;
    }

    // Start with all possible codepoints — HarfBuzz intersects with cmap during subsetting
    hb_set_t* unicodes = hb_subset_input_unicode_set(input);
    hb_set_add_range(unicodes, 0, 0x10FFFF);

    // Remove the specified codepoints
    for (unsigned int i = 0; i < codepointCount; i++)
        hb_set_del(unicodes, codepoints[i]);

    hb_face_t* subset = hb_subset_or_fail(face, input);
    hb_subset_input_destroy(input);
    hb_face_destroy(face);
    if (!subset) return 0;

    hb_blob_t* result = hb_face_reference_blob(subset);
    hb_face_destroy(subset);
    if (!result) return 0;

    unsigned int size = 0;
    const char* data = hb_blob_get_data(result, &size);

    if (outData && outDataCapacity >= size && data)
        memcpy(outData, data, size);

    hb_blob_destroy(result);
    return size;
}

// Returns total glyph count in the font (from maxp table)
EXPORT unsigned int get_glyph_count(
    const void* fontData, unsigned int fontDataSize)
{
    if (!fontData || !fontDataSize)
        return 0;

    hb_blob_t* blob = hb_blob_create((const char*)fontData, fontDataSize,
                                      HB_MEMORY_MODE_READONLY, nullptr, nullptr);
    if (!blob) return 0;

    hb_face_t* face = hb_face_create(blob, 0);
    hb_blob_destroy(blob);
    if (!face) return 0;

    unsigned int count = hb_face_get_glyph_count(face);
    hb_face_destroy(face);
    return count;
}

// Removes specific glyphs from a font by glyph ID.
// Uses HB_SUBSET_FLAGS_NO_LAYOUT_CLOSURE to prevent GSUB from re-adding removed glyphs.
// glyphIds = glyph IDs to REMOVE (not keep).
// Returns subset font size, or 0 on failure.
// Pass outData=null to query required size first.
EXPORT unsigned int subset_font_remove_glyphs(
    const void* fontData, unsigned int fontDataSize,
    const unsigned int* glyphIds, unsigned int glyphCount,
    void* outData, unsigned int outDataCapacity)
{
    if (!fontData || !fontDataSize || !glyphIds || !glyphCount)
        return 0;

    hb_blob_t* blob = hb_blob_create((const char*)fontData, fontDataSize,
                                      HB_MEMORY_MODE_READONLY, nullptr, nullptr);
    if (!blob) return 0;

    hb_face_t* face = hb_face_create(blob, 0);
    hb_blob_destroy(blob);
    if (!face) return 0;

    unsigned int totalGlyphs = hb_face_get_glyph_count(face);

    hb_subset_input_t* input = hb_subset_input_create_or_fail();
    if (!input) {
        hb_face_destroy(face);
        return 0;
    }

    // Prevent GSUB closure from re-adding removed glyphs; keep .notdef outline
    hb_subset_input_set_flags(input,
        HB_SUBSET_FLAGS_NO_LAYOUT_CLOSURE | HB_SUBSET_FLAGS_NOTDEF_OUTLINE);

    // Start with all glyphs, then remove the specified ones
    hb_set_t* glyphs = hb_subset_input_glyph_set(input);
    hb_set_add_range(glyphs, 0, totalGlyphs - 1);

    for (unsigned int i = 0; i < glyphCount; i++)
    {
        // Never remove .notdef (glyph 0) — required by spec
        if (glyphIds[i] != 0)
            hb_set_del(glyphs, glyphIds[i]);
    }

    hb_face_t* subset = hb_subset_or_fail(face, input);
    hb_subset_input_destroy(input);
    hb_face_destroy(face);
    if (!subset) return 0;

    hb_blob_t* result = hb_face_reference_blob(subset);
    hb_face_destroy(subset);
    if (!result) return 0;

    unsigned int size = 0;
    const char* data = hb_blob_get_data(result, &size);

    if (outData && outDataCapacity >= size && data)
        memcpy(outData, data, size);

    hb_blob_destroy(result);
    return size;
}

// Shapes a sequence of codepoints and returns the resulting glyph IDs.
// Uses hb_buffer_guess_segment_properties for auto-detection of script/direction.
// Returns number of output glyphs, or 0 on failure.
// Pass outGlyphIds=null to query required count first.
EXPORT unsigned int shape_text(
    const void* fontData, unsigned int fontDataSize,
    const unsigned int* codepoints, unsigned int codepointCount,
    unsigned int* outGlyphIds, unsigned int outCapacity)
{
    if (!fontData || !fontDataSize || !codepoints || !codepointCount)
        return 0;

    hb_blob_t* blob = hb_blob_create((const char*)fontData, fontDataSize,
                                      HB_MEMORY_MODE_READONLY, nullptr, nullptr);
    if (!blob) return 0;

    hb_face_t* face = hb_face_create(blob, 0);
    hb_blob_destroy(blob);
    if (!face) return 0;

    hb_font_t* font = hb_font_create(face);
    hb_face_destroy(face);
    if (!font) return 0;

    hb_ot_font_set_funcs(font);

    hb_buffer_t* buffer = hb_buffer_create();
    hb_buffer_add_codepoints(buffer, codepoints, codepointCount, 0, codepointCount);
    hb_buffer_guess_segment_properties(buffer);

    hb_shape(font, buffer, nullptr, 0);

    unsigned int glyphCount = hb_buffer_get_length(buffer);

    if (outGlyphIds && outCapacity >= glyphCount)
    {
        hb_glyph_info_t* infos = hb_buffer_get_glyph_infos(buffer, nullptr);
        for (unsigned int i = 0; i < glyphCount; i++)
            outGlyphIds[i] = infos[i].codepoint;
    }

    hb_buffer_destroy(buffer);
    hb_font_destroy(font);
    return glyphCount;
}

// Collects all Unicode codepoints supported by the font (via cmap lookup).
// Returns codepoint count, or 0 on failure.
// Pass outCodepoints=null to query required count first.
EXPORT unsigned int get_font_codepoints(
    const void* fontData, unsigned int fontDataSize,
    unsigned int* outCodepoints, unsigned int outCapacity)
{
    if (!fontData || !fontDataSize)
        return 0;

    hb_blob_t* blob = hb_blob_create((const char*)fontData, fontDataSize,
                                      HB_MEMORY_MODE_READONLY, nullptr, nullptr);
    if (!blob) return 0;

    hb_face_t* face = hb_face_create(blob, 0);
    hb_blob_destroy(blob);
    if (!face) return 0;

    hb_font_t* font = hb_font_create(face);
    hb_face_destroy(face);
    if (!font) return 0;

    hb_ot_font_set_funcs(font);

    hb_codepoint_t glyph;
    unsigned int count = 0;

    for (hb_codepoint_t cp = 1; cp <= 0x10FFFF; cp++)
        if (hb_font_get_glyph(font, cp, 0, &glyph) && glyph != 0)
            count++;

    if (outCodepoints && outCapacity >= count)
    {
        unsigned int i = 0;
        for (hb_codepoint_t cp = 1; cp <= 0x10FFFF; cp++)
            if (hb_font_get_glyph(font, cp, 0, &glyph) && glyph != 0)
                outCodepoints[i++] = cp;
    }

    hb_font_destroy(font);
    return count;
}
