// UniText Native - Unified library exports
// Combines: HarfBuzz, FreeType, Blend2D
//
// This file is compiled into a single native library for each platform:
// - Windows: unitext_native.dll
// - Linux: libunitext_native.so
// - macOS: libunitext_native.dylib
// - Android: libunitext_native.so
// - iOS/tvOS: libunitext_native.a (static)

#include <ft2build.h>
#include FT_FREETYPE_H
#include FT_COLOR_H
#include FT_TRUETYPE_TABLES_H
#include FT_MODULE_H

#include <hb.h>
#include <hb-ot.h>

#define BL_STATIC
#include <blend2d/blend2d.h>

#ifdef _WIN32
#define UNITEXT_EXPORT extern "C" __declspec(dllexport)
#else
#define UNITEXT_EXPORT extern "C" __attribute__((visibility("default")))
#endif

// === Version ===
UNITEXT_EXPORT const char* ut_version() {
    return "3.0.0-unified";
}

// =============================================================================
// Unified FreeType API (ut_ft_*)
// =============================================================================

UNITEXT_EXPORT int ut_ft_init(FT_Library* library) {
    return FT_Init_FreeType(library);
}

UNITEXT_EXPORT int ut_ft_done(FT_Library library) {
    return FT_Done_FreeType(library);
}

UNITEXT_EXPORT int ut_ft_new_memory_face(FT_Library library, const unsigned char* data, long size, long face_index, FT_Face* face) {
    return FT_New_Memory_Face(library, data, size, face_index, face);
}

UNITEXT_EXPORT int ut_ft_done_face(FT_Face face) {
    return FT_Done_Face(face);
}

UNITEXT_EXPORT unsigned int ut_ft_get_char_index(FT_Face face, unsigned long charcode) {
    return FT_Get_Char_Index(face, charcode);
}

UNITEXT_EXPORT int ut_ft_set_pixel_sizes(FT_Face face, unsigned int width, unsigned int height) {
    return FT_Set_Pixel_Sizes(face, width, height);
}

UNITEXT_EXPORT int ut_ft_select_size(FT_Face face, int strike_index) {
    return FT_Select_Size(face, strike_index);
}

UNITEXT_EXPORT int ut_ft_load_glyph(FT_Face face, unsigned int glyph_index, int load_flags) {
    return FT_Load_Glyph(face, glyph_index, load_flags);
}

UNITEXT_EXPORT int ut_ft_render_glyph(FT_GlyphSlot slot, int render_mode) {
    return FT_Render_Glyph(slot, (FT_Render_Mode)render_mode);
}

UNITEXT_EXPORT int ut_ft_palette_data_get(FT_Face face, FT_Palette_Data* palette_data) {
    return FT_Palette_Data_Get(face, palette_data);
}

UNITEXT_EXPORT int ut_ft_palette_select(FT_Face face, unsigned short palette_index, FT_Color** palette) {
    return FT_Palette_Select(face, palette_index, palette);
}

UNITEXT_EXPORT int ut_ft_get_color_glyph_clipbox(FT_Face face, unsigned int base_glyph, FT_ClipBox* clip_box) {
    return FT_Get_Color_Glyph_ClipBox(face, base_glyph, clip_box);
}

UNITEXT_EXPORT int ut_ft_get_color_glyph_layer(FT_Face face, unsigned int base_glyph, unsigned int* glyph_index, unsigned int* color_index, FT_LayerIterator* iterator) {
    return FT_Get_Color_Glyph_Layer(face, base_glyph, glyph_index, color_index, iterator);
}

// === SDF Configuration ===
UNITEXT_EXPORT int ut_ft_set_sdf_spread(FT_Library library, int spread) {
    FT_UInt s = (FT_UInt)spread;
    FT_Error err1 = FT_Property_Set(library, "sdf", "spread", &s);
    FT_Error err2 = FT_Property_Set(library, "bsdf", "spread", &s);
    return err1 != 0 ? err1 : err2;
}

// === Felzenszwalb & Huttenlocher EDT (Euclidean Distance Transform) ============
// Reference: "Distance Transforms of Sampled Functions" (2012)
// O(n) per row/column — used by mapbox/tiny-sdf, Unity SDFAA, etc.

#include <math.h>
#include <stdlib.h>
#include <string.h>

#define EDT_INF 1e20f

// 1D squared EDT using parabola lower envelope.
// f[n] = input squared distances, d[n] = output, v[n] + z[n+1] = scratch.
static void edt_1d(const float* f, float* d, int* v, float* z, int n) {
    int k = 0;
    v[0] = 0;
    z[0] = -EDT_INF;
    z[1] = +EDT_INF;
    for (int q = 1; q < n; q++) {
        float s = ((f[q] + (float)(q * q)) - (f[v[k]] + (float)(v[k] * v[k])))
                  / (float)(2 * q - 2 * v[k]);
        while (s <= z[k]) {
            k--;
            s = ((f[q] + (float)(q * q)) - (f[v[k]] + (float)(v[k] * v[k])))
                / (float)(2 * q - 2 * v[k]);
        }
        k++;
        v[k] = q;
        z[k] = s;
        z[k + 1] = +EDT_INF;
    }
    k = 0;
    for (int q = 0; q < n; q++) {
        while (z[k + 1] < (float)q) k++;
        d[q] = (float)((q - v[k]) * (q - v[k])) + f[v[k]];
    }
}

// 2D squared EDT in-place. grid[w*h], row-major.
// Caller provides workspace: f[maxdim], d[maxdim], z[maxdim+1], v[maxdim].
static void edt_2d(float* grid, int w, int h, float* f, float* d, float* z, int* v) {
    // Transform columns
    for (int x = 0; x < w; x++) {
        for (int y = 0; y < h; y++) f[y] = grid[y * w + x];
        edt_1d(f, d, v, z, h);
        for (int y = 0; y < h; y++) grid[y * w + x] = d[y];
    }
    // Transform rows
    for (int y = 0; y < h; y++) {
        edt_1d(grid + y * w, d, v, z, w);
        memcpy(grid + y * w, d, w * sizeof(float));
    }
}

// === Combined SDF Glyph Render (FT_RENDER_MODE_NORMAL + EDT) =================
//
// Renders glyph as anti-aliased bitmap, then computes SDF via Felzenszwalb EDT.
// Output is Alpha8 SDF with spread-pixel padding. bmp_buffer is malloc'd —
// caller MUST free via ut_ft_free_sdf_buffer().

typedef struct {
    int success;          // 0 = ok, non-zero = FreeType error
    // Outline metrics (26.6 fixed-point >> 6, read BEFORE render)
    int metric_width;
    int metric_height;
    int metric_bearing_x;
    int metric_bearing_y;
    int metric_advance_x; // 26.6 raw (NOT shifted), caller divides by 64
    // SDF bitmap (padded by spread on each side)
    int bmp_width;
    int bmp_height;
    int bmp_pitch;
    int bitmap_left;
    int bitmap_top;
    void* bmp_buffer;     // malloc'd Alpha8 SDF — caller must free
} ut_sdf_glyph_result;

UNITEXT_EXPORT int ut_ft_render_sdf_glyph(FT_Face face, unsigned int glyph_index,
                                           int load_flags, int spread,
                                           ut_sdf_glyph_result* out_result) {
    if (!out_result) return -1;
    memset(out_result, 0, sizeof(ut_sdf_glyph_result));
    if (!face) { out_result->success = -1; return -1; }

    // Step 1: Load glyph outline
    FT_Error err = FT_Load_Glyph(face, glyph_index, load_flags);
    if (err) { out_result->success = (int)err; return (int)err; }

    // Step 2: Read outline metrics (before render, unaffected by spread)
    FT_Glyph_Metrics* m = &face->glyph->metrics;
    out_result->metric_width     = (int)(m->width >> 6);
    out_result->metric_height    = (int)(m->height >> 6);
    out_result->metric_bearing_x = (int)(m->horiBearingX >> 6);
    out_result->metric_bearing_y = (int)(m->horiBearingY >> 6);
    out_result->metric_advance_x = (int)m->horiAdvance; // raw 26.6

    // Step 3: Render as normal anti-aliased grayscale (fast — ~0.1ms)
    err = FT_Render_Glyph(face->glyph, FT_RENDER_MODE_NORMAL);
    if (err) { out_result->success = (int)err; return (int)err; }

    FT_Bitmap* b = &face->glyph->bitmap;
    int bw = (int)b->width;
    int bh = (int)b->rows;

    // Zero-size glyph (space, control chars)
    if (bw <= 0 || bh <= 0) {
        out_result->bitmap_left = face->glyph->bitmap_left;
        out_result->bitmap_top  = face->glyph->bitmap_top;
        out_result->success = 0;
        return 0;
    }

    // Step 4: Allocate workspace — one malloc for grids + EDT scratch
    int pw = bw + 2 * spread;
    int ph = bh + 2 * spread;
    int pcount = pw * ph;
    int maxdim = pw > ph ? pw : ph;

    size_t ws_size = (size_t)pcount * sizeof(float) * 2    // outside + inside
                   + (size_t)maxdim * sizeof(float) * 2    // edt_f + edt_d
                   + (size_t)(maxdim + 1) * sizeof(float)  // edt_z
                   + (size_t)maxdim * sizeof(int);          // edt_v
    char* ws = (char*)malloc(ws_size);
    if (!ws) { out_result->success = -1; return -1; }

    float* outside = (float*)ws;
    float* inside  = outside + pcount;
    float* edt_f   = inside + pcount;
    float* edt_d   = edt_f + maxdim;
    float* edt_z   = edt_d + maxdim;
    int*   edt_v   = (int*)(edt_z + maxdim + 1);

    // Initialize: outside = far, inside = 0
    for (int i = 0; i < pcount; i++) {
        outside[i] = EDT_INF;
        inside[i]  = 0.0f;
    }

    // Fill glyph region using alpha for sub-pixel accuracy (mapbox/tiny-sdf approach).
    for (int y = 0; y < bh; y++) {
        const unsigned char* row = b->buffer + y * b->pitch;
        for (int x = 0; x < bw; x++) {
            int pi = (y + spread) * pw + (x + spread);
            unsigned char a = row[x];
            if (a == 0) {
                outside[pi] = EDT_INF;
                inside[pi]  = 0.0f;
            } else if (a == 255) {
                outside[pi] = 0.0f;
                inside[pi]  = EDT_INF;
            } else {
                float d = 0.5f - (float)a / 255.0f;
                outside[pi] = d > 0.0f ? d * d : 0.0f;
                inside[pi]  = d < 0.0f ? d * d : 0.0f;
            }
        }
    }

    // Step 5: Compute 2D EDT for both fields
    edt_2d(outside, pw, ph, edt_f, edt_d, edt_z, edt_v);
    edt_2d(inside, pw, ph, edt_f, edt_d, edt_z, edt_v);

    // Step 6: Combine into Alpha8 SDF with Y-flip (FreeType top-down → Unity bottom-up).
    unsigned char* sdf = (unsigned char*)malloc(pcount);
    if (!sdf) { free(ws); out_result->success = -1; return -1; }

    float inv_spread = spread > 0 ? 128.0f / (float)spread : 128.0f;

    for (int y = 0; y < ph; y++) {
        int src_row = y * pw;
        int dst_row = (ph - 1 - y) * pw;  // Y-flip
        for (int x = 0; x < pw; x++) {
            float dist = sqrtf(outside[src_row + x]) - sqrtf(inside[src_row + x]);
            float val = 128.0f - dist * inv_spread;
            int ival = (int)(val + 0.5f);
            sdf[dst_row + x] = (unsigned char)(ival < 0 ? 0 : (ival > 255 ? 255 : ival));
        }
    }

    free(ws);

    // Step 7: Fill result
    out_result->bmp_width   = pw;
    out_result->bmp_height  = ph;
    out_result->bmp_pitch   = pw;
    out_result->bitmap_left = face->glyph->bitmap_left - spread;
    out_result->bitmap_top  = face->glyph->bitmap_top + spread;
    out_result->bmp_buffer  = sdf;
    out_result->success = 0;
    return 0;
}

UNITEXT_EXPORT void ut_ft_free_sdf_buffer(void* buffer) {
    free(buffer);
}

// =============================================================================
// Unified HarfBuzz API (ut_hb_*)
// =============================================================================

UNITEXT_EXPORT hb_blob_t* ut_hb_blob_create(const char* data, unsigned int length, hb_memory_mode_t mode, void* user_data, hb_destroy_func_t destroy) {
    return hb_blob_create(data, length, mode, user_data, destroy);
}

UNITEXT_EXPORT void ut_hb_blob_destroy(hb_blob_t* blob) {
    hb_blob_destroy(blob);
}

UNITEXT_EXPORT hb_face_t* ut_hb_face_create(hb_blob_t* blob, unsigned int index) {
    return hb_face_create(blob, index);
}

UNITEXT_EXPORT void ut_hb_face_destroy(hb_face_t* face) {
    hb_face_destroy(face);
}

UNITEXT_EXPORT unsigned int ut_hb_face_get_upem(const hb_face_t* face) {
    return hb_face_get_upem(face);
}

UNITEXT_EXPORT hb_font_t* ut_hb_font_create(hb_face_t* face) {
    return hb_font_create(face);
}

UNITEXT_EXPORT void ut_hb_font_destroy(hb_font_t* font) {
    hb_font_destroy(font);
}

UNITEXT_EXPORT void ut_hb_ot_font_set_funcs(hb_font_t* font) {
    hb_ot_font_set_funcs(font);
}

UNITEXT_EXPORT int ut_hb_font_get_glyph_h_advance(hb_font_t* font, unsigned int glyph) {
    return hb_font_get_glyph_h_advance(font, glyph);
}

UNITEXT_EXPORT int ut_hb_font_get_glyph(hb_font_t* font, unsigned int unicode, unsigned int variation_selector, unsigned int* glyph) {
    return hb_font_get_glyph(font, unicode, variation_selector, glyph);
}

UNITEXT_EXPORT hb_face_t* ut_hb_font_get_face(hb_font_t* font) {
    return hb_font_get_face(font);
}

UNITEXT_EXPORT hb_buffer_t* ut_hb_buffer_create() {
    return hb_buffer_create();
}

UNITEXT_EXPORT void ut_hb_buffer_destroy(hb_buffer_t* buffer) {
    hb_buffer_destroy(buffer);
}

UNITEXT_EXPORT void ut_hb_buffer_clear_contents(hb_buffer_t* buffer) {
    hb_buffer_clear_contents(buffer);
}

UNITEXT_EXPORT void ut_hb_buffer_set_direction(hb_buffer_t* buffer, hb_direction_t direction) {
    hb_buffer_set_direction(buffer, direction);
}

UNITEXT_EXPORT void ut_hb_buffer_set_script(hb_buffer_t* buffer, hb_script_t script) {
    hb_buffer_set_script(buffer, script);
}

UNITEXT_EXPORT void ut_hb_buffer_set_content_type(hb_buffer_t* buffer, hb_buffer_content_type_t content_type) {
    hb_buffer_set_content_type(buffer, content_type);
}

UNITEXT_EXPORT void ut_hb_buffer_set_flags(hb_buffer_t* buffer, hb_buffer_flags_t flags) {
    hb_buffer_set_flags(buffer, flags);
}

UNITEXT_EXPORT void ut_hb_buffer_add_codepoints(hb_buffer_t* buffer, const unsigned int* text, int text_length, unsigned int item_offset, int item_length) {
    hb_buffer_add_codepoints(buffer, text, text_length, item_offset, item_length);
}

UNITEXT_EXPORT unsigned int ut_hb_buffer_get_length(const hb_buffer_t* buffer) {
    return hb_buffer_get_length(buffer);
}

UNITEXT_EXPORT hb_glyph_info_t* ut_hb_buffer_get_glyph_infos(hb_buffer_t* buffer, unsigned int* length) {
    return hb_buffer_get_glyph_infos(buffer, length);
}

UNITEXT_EXPORT hb_glyph_position_t* ut_hb_buffer_get_glyph_positions(hb_buffer_t* buffer, unsigned int* length) {
    return hb_buffer_get_glyph_positions(buffer, length);
}

UNITEXT_EXPORT void ut_hb_shape(hb_font_t* font, hb_buffer_t* buffer, const hb_feature_t* features, unsigned int num_features) {
    hb_shape(font, buffer, features, num_features);
}

// === sbix Diagnostic ===
// Reads the graphicType from the first glyph in the sbix table
// Returns 1 on success, 0 on failure
// outGraphicType must be at least 5 bytes (4 chars + null terminator)
UNITEXT_EXPORT int ut_debug_sbix_graphic_type(FT_Face face, char* outGraphicType, int* outNumStrikes) {
    if (!face || !outGraphicType) return 0;

    outGraphicType[0] = 0;
    if (outNumStrikes) *outNumStrikes = 0;

    // Get sbix table size
    FT_ULong length = 0;
    FT_Error err = FT_Load_Sfnt_Table(face, FT_MAKE_TAG('s','b','i','x'), 0, NULL, &length);
    if (err != 0 || length < 16) return 0;

    // Load sbix table
    FT_Byte* buffer = (FT_Byte*)malloc(length);
    if (!buffer) return 0;

    err = FT_Load_Sfnt_Table(face, FT_MAKE_TAG('s','b','i','x'), 0, buffer, &length);
    if (err != 0) {
        free(buffer);
        return 0;
    }

    // sbix table structure:
    // [0-1]  version (uint16)
    // [2-3]  flags (uint16)
    // [4-7]  numStrikes (uint32)
    // [8...] strikeOffsets[] (uint32 each)

    uint32_t numStrikes = ((uint32_t)buffer[4] << 24) | ((uint32_t)buffer[5] << 16) |
                          ((uint32_t)buffer[6] << 8) | buffer[7];
    if (outNumStrikes) *outNumStrikes = (int)numStrikes;

    if (numStrikes == 0) {
        free(buffer);
        return 0;
    }

    // Get first strike offset
    uint32_t strikeOffset = ((uint32_t)buffer[8] << 24) | ((uint32_t)buffer[9] << 16) |
                            ((uint32_t)buffer[10] << 8) | buffer[11];

    // Strike structure:
    // [0-1]  ppem (uint16)
    // [2-3]  ppi (uint16)
    // [4...] glyphDataOffsets[] (uint32 each, numGlyphs+1 entries)

    if (strikeOffset + 8 >= length) {
        free(buffer);
        return 0;
    }

    // Get number of glyphs to find first non-empty glyph data
    FT_ULong numGlyphs = face->num_glyphs;

    // Find first glyph with actual data
    for (FT_ULong g = 0; g < numGlyphs && g < 10000; g++) {
        uint32_t offsetIdx = strikeOffset + 4 + g * 4;
        if (offsetIdx + 8 > length) break;

        uint32_t glyphDataOffset = ((uint32_t)buffer[offsetIdx] << 24) |
                                   ((uint32_t)buffer[offsetIdx + 1] << 16) |
                                   ((uint32_t)buffer[offsetIdx + 2] << 8) |
                                   buffer[offsetIdx + 3];
        uint32_t nextGlyphDataOffset = ((uint32_t)buffer[offsetIdx + 4] << 24) |
                                       ((uint32_t)buffer[offsetIdx + 5] << 16) |
                                       ((uint32_t)buffer[offsetIdx + 6] << 8) |
                                       buffer[offsetIdx + 7];

        // Check if this glyph has data
        if (nextGlyphDataOffset > glyphDataOffset) {
            // Glyph data structure:
            // [0-1]  originOffsetX (int16)
            // [2-3]  originOffsetY (int16)
            // [4-7]  graphicType (4 chars)
            // [8...] data

            uint32_t dataPos = strikeOffset + glyphDataOffset;
            if (dataPos + 8 <= length) {
                outGraphicType[0] = (char)buffer[dataPos + 4];
                outGraphicType[1] = (char)buffer[dataPos + 5];
                outGraphicType[2] = (char)buffer[dataPos + 6];
                outGraphicType[3] = (char)buffer[dataPos + 7];
                outGraphicType[4] = 0;
                free(buffer);
                return 1;
            }
        }
    }

    free(buffer);
    return 0;
}

// =============================================================================
// FreeType Wrapper Functions
// =============================================================================

UNITEXT_EXPORT void ut_ft_get_face_info(FT_Face face, long* out_face_flags, int* out_num_glyphs,
                                              int* out_units_per_em, int* out_num_fixed_sizes,
                                              int* out_num_faces, int* out_face_index,
                                              short* out_ascender, short* out_descender, short* out_height) {
    if (!face) return;
    if (out_face_flags) *out_face_flags = face->face_flags;
    if (out_num_glyphs) *out_num_glyphs = (int)face->num_glyphs;
    if (out_units_per_em) *out_units_per_em = face->units_per_EM;
    if (out_num_fixed_sizes) *out_num_fixed_sizes = face->num_fixed_sizes;
    if (out_num_faces) *out_num_faces = (int)face->num_faces;
    if (out_face_index) *out_face_index = (int)face->face_index;
    if (out_ascender) *out_ascender = face->ascender;
    if (out_descender) *out_descender = face->descender;
    if (out_height) *out_height = face->height;
}

// Reads OS/2 table metrics + post table underline + face names.
// Returns 1 if OS/2 table was found, 0 otherwise. Post/name data is always filled if available.
UNITEXT_EXPORT int ut_ft_get_extended_face_info(FT_Face face,
    // OS/2 table
    short* out_cap_height, short* out_x_height,
    short* out_y_superscript_y_offset, short* out_y_superscript_y_size,
    short* out_y_subscript_y_offset, short* out_y_subscript_y_size,
    short* out_y_strikeout_position, short* out_y_strikeout_size,
    // post table (via FT_FaceRec)
    short* out_underline_position, short* out_underline_thickness,
    // name table (via FT_FaceRec)
    const char** out_family_name, const char** out_style_name)
{
    if (!face) return 0;

    // Post table fields from FT_FaceRec
    if (out_underline_position) *out_underline_position = face->underline_position;
    if (out_underline_thickness) *out_underline_thickness = face->underline_thickness;

    // Name table fields from FT_FaceRec
    if (out_family_name) *out_family_name = face->family_name;
    if (out_style_name) *out_style_name = face->style_name;

    // OS/2 table
    TT_OS2* os2 = (TT_OS2*)FT_Get_Sfnt_Table(face, FT_SFNT_OS2);
    if (!os2) return 0;

    if (out_cap_height) *out_cap_height = os2->sCapHeight;
    if (out_x_height) *out_x_height = os2->sxHeight;
    if (out_y_superscript_y_offset) *out_y_superscript_y_offset = os2->ySuperscriptYOffset;
    if (out_y_superscript_y_size) *out_y_superscript_y_size = os2->ySuperscriptYSize;
    if (out_y_subscript_y_offset) *out_y_subscript_y_offset = os2->ySubscriptYOffset;
    if (out_y_subscript_y_size) *out_y_subscript_y_size = os2->ySubscriptYSize;
    if (out_y_strikeout_position) *out_y_strikeout_position = os2->yStrikeoutPosition;
    if (out_y_strikeout_size) *out_y_strikeout_size = os2->yStrikeoutSize;

    return 1;
}

UNITEXT_EXPORT int ut_ft_get_fixed_size(FT_Face face, int index) {
    if (!face || index < 0 || index >= face->num_fixed_sizes) return 0;
    return face->available_sizes[index].height;
}

UNITEXT_EXPORT void ut_ft_get_glyph_metrics(FT_Face face, int* out_width, int* out_height,
                                                  int* out_bearing_x, int* out_bearing_y,
                                                  int* out_advance_x, int* out_advance_y) {
    if (!face || !face->glyph) return;
    FT_Glyph_Metrics* m = &face->glyph->metrics;
    if (out_width) *out_width = (int)(m->width >> 6);
    if (out_height) *out_height = (int)(m->height >> 6);
    if (out_bearing_x) *out_bearing_x = (int)(m->horiBearingX >> 6);
    if (out_bearing_y) *out_bearing_y = (int)(m->horiBearingY >> 6);
    if (out_advance_x) *out_advance_x = (int)m->horiAdvance;
    if (out_advance_y) *out_advance_y = (int)m->vertAdvance;
}

UNITEXT_EXPORT void ut_ft_get_bitmap_info(FT_Face face, int* out_width, int* out_height,
                                                int* out_pitch, int* out_pixel_mode, void** out_buffer) {
    if (!face || !face->glyph) return;
    FT_Bitmap* b = &face->glyph->bitmap;
    if (out_width) *out_width = (int)b->width;
    if (out_height) *out_height = (int)b->rows;
    if (out_pitch) *out_pitch = b->pitch;
    if (out_pixel_mode) *out_pixel_mode = b->pixel_mode;
    if (out_buffer) *out_buffer = b->buffer;
}

UNITEXT_EXPORT FT_GlyphSlot ut_ft_get_glyph_slot(FT_Face face) {
    return face ? face->glyph : nullptr;
}

UNITEXT_EXPORT int ut_ft_get_bitmap_top(FT_Face face) {
    if (!face || !face->glyph) return 0;
    return face->glyph->bitmap_top;
}

UNITEXT_EXPORT int ut_ft_get_bitmap_left(FT_Face face) {
    if (!face || !face->glyph) return 0;
    return face->glyph->bitmap_left;
}

// =============================================================================
// FreeType Outline to Blend2D Path
// =============================================================================

UNITEXT_EXPORT int ut_ft_outline_to_blpath(FT_Face face, void* blPath) {
    if (!face || !face->glyph || !blPath) return 0;

    FT_Outline* outline = &face->glyph->outline;
    if (outline->n_points <= 0) return 0;

    BLPath* path = static_cast<BLPath*>(blPath);
    path->clear();

    int contourStart = 0;
    for (int c = 0; c < outline->n_contours; c++) {
        int contourEnd = outline->contours[c];

        // Find first on-curve point
        int firstOnCurve = -1;
        for (int i = contourStart; i <= contourEnd; i++) {
            if (outline->tags[i] & 1) { // FT_CURVE_TAG_ON
                firstOnCurve = i;
                break;
            }
        }

        if (firstOnCurve < 0) {
            // All off-curve - create midpoint
            FT_Vector* p0 = &outline->points[contourStart];
            FT_Vector* p1 = &outline->points[contourStart + 1];
            double mx = (p0->x + p1->x) / 2.0;
            double my = (p0->y + p1->y) / 2.0;
            path->move_to(mx, my);
            firstOnCurve = contourStart;
        } else {
            FT_Vector* p = &outline->points[firstOnCurve];
            path->move_to(p->x, p->y);
        }

        int i = firstOnCurve;
        int numPoints = contourEnd - contourStart + 1;

        for (int j = 0; j < numPoints; j++) {
            int idx = contourStart + ((i - contourStart + 1) % numPoints);
            FT_Vector* p = &outline->points[idx];
            char tag = outline->tags[idx];

            if (tag & 1) { // On curve
                path->line_to(p->x, p->y);
            } else if (tag & 2) { // Cubic
                int idx2 = contourStart + ((idx - contourStart + 1) % numPoints);
                int idx3 = contourStart + ((idx - contourStart + 2) % numPoints);
                FT_Vector* p2 = &outline->points[idx2];
                FT_Vector* p3 = &outline->points[idx3];
                path->cubic_to(p->x, p->y, p2->x, p2->y, p3->x, p3->y);
                j += 2;
                i = idx3;
                continue;
            } else { // Quadratic (conic)
                int idx2 = contourStart + ((idx - contourStart + 1) % numPoints);
                FT_Vector* p2 = &outline->points[idx2];
                char tag2 = outline->tags[idx2];

                double cx = p->x;
                double cy = p->y;
                double ex, ey;

                if (tag2 & 1) { // Next is on-curve
                    ex = p2->x;
                    ey = p2->y;
                    path->quad_to(cx, cy, ex, ey);
                    j++;
                    i = idx2;
                    continue;
                } else { // Next is also off-curve
                    ex = (p->x + p2->x) / 2.0;
                    ey = (p->y + p2->y) / 2.0;
                    path->quad_to(cx, cy, ex, ey);
                }
            }
            i = idx;
        }

        path->close();
        contourStart = contourEnd + 1;
    }

    return 1;
}

UNITEXT_EXPORT int ut_ft_get_outline_info(FT_Face face, int* outNumContours, int* outNumPoints) {
    if (!face || !face->glyph) return 0;
    FT_Outline* outline = &face->glyph->outline;
    if (outNumContours) *outNumContours = outline->n_contours;
    if (outNumPoints) *outNumPoints = outline->n_points;
    return 1;
}

// =============================================================================
// COLRv1 Wrapper Functions
// All structs decomposed to primitives for cross-platform ABI safety
// =============================================================================

static inline FT_OpaquePaint MakeOpaquePaint(void* p, int insertRoot) {
    FT_OpaquePaint op;
    op.p = (FT_Byte*)p;
    op.insert_root_transform = (FT_Bool)insertRoot;
    return op;
}

UNITEXT_EXPORT int ut_colr_get_glyph_paint(FT_Face face, uint32_t baseGlyph, int rootTransform,
                                                 void** outPaintP, int* outPaintInsert) {
    FT_OpaquePaint paint = { NULL, 0 };
    FT_Bool result = FT_Get_Color_Glyph_Paint(face, baseGlyph,
                          rootTransform ? FT_COLOR_INCLUDE_ROOT_TRANSFORM : FT_COLOR_NO_ROOT_TRANSFORM,
                          &paint);
    if (!result) return 0;
    if (outPaintP) *outPaintP = paint.p;
    if (outPaintInsert) *outPaintInsert = paint.insert_root_transform;
    return 1;
}

UNITEXT_EXPORT int ut_colr_debug_glyph_paint(FT_Face face, uint32_t baseGlyph,
                                                   int* outHasColrTable, int* outHasCpalTable,
                                                   int* outFtResult) {
    FT_ULong colrLength = 0;
    FT_Error colrErr = FT_Load_Sfnt_Table(face, FT_MAKE_TAG('C','O','L','R'), 0, NULL, &colrLength);
    if (outHasColrTable) *outHasColrTable = (colrErr == 0 && colrLength > 0) ? 1 : 0;

    FT_ULong cpalLength = 0;
    FT_Error cpalErr = FT_Load_Sfnt_Table(face, FT_MAKE_TAG('C','P','A','L'), 0, NULL, &cpalLength);
    if (outHasCpalTable) *outHasCpalTable = (cpalErr == 0 && cpalLength > 0) ? 1 : 0;

    FT_OpaquePaint paint = { NULL, 0 };
    FT_Bool result = FT_Get_Color_Glyph_Paint(face, baseGlyph, FT_COLOR_INCLUDE_ROOT_TRANSFORM, &paint);
    if (outFtResult) *outFtResult = result;

    return (colrErr == 0 && colrLength > 0) ? 1 : 0;
}

UNITEXT_EXPORT int ut_colr_get_paint_format(FT_Face face, void* paintP, int paintInsert) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return -1;
    return paint.format;
}

UNITEXT_EXPORT int ut_colr_get_paint_solid(FT_Face face, void* paintP, int paintInsert,
                                                 uint16_t* outColorIndex, int32_t* outAlpha) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_SOLID) return 0;
    if (outColorIndex) *outColorIndex = paint.u.solid.color.palette_index;
    if (outAlpha) *outAlpha = paint.u.solid.color.alpha;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_layers(FT_Face face, void* paintP, int paintInsert,
                                                  uint32_t* outNumLayers, uint32_t* outLayer, void** outIterP) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_COLR_LAYERS) return 0;
    FT_LayerIterator iter = paint.u.colr_layers.layer_iterator;
    if (outNumLayers) *outNumLayers = iter.num_layers;
    if (outLayer) *outLayer = iter.layer;
    if (outIterP) *outIterP = iter.p;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_next_layer(FT_Face face,
                                                uint32_t* ioNumLayers, uint32_t* ioLayer, void** ioIterP,
                                                void** outPaintP, int* outPaintInsert) {
    FT_LayerIterator iter;
    iter.num_layers = ioNumLayers ? *ioNumLayers : 0;
    iter.layer = ioLayer ? *ioLayer : 0;
    iter.p = ioIterP ? (FT_Byte*)*ioIterP : nullptr;

    FT_OpaquePaint layerPaint;
    if (!FT_Get_Paint_Layers(face, &iter, &layerPaint)) return 0;

    if (ioNumLayers) *ioNumLayers = iter.num_layers;
    if (ioLayer) *ioLayer = iter.layer;
    if (ioIterP) *ioIterP = iter.p;
    if (outPaintP) *outPaintP = layerPaint.p;
    if (outPaintInsert) *outPaintInsert = layerPaint.insert_root_transform;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_glyph(FT_Face face, void* paintP, int paintInsert,
                                                 uint32_t* outGlyphId,
                                                 void** outChildP, int* outChildInsert) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_GLYPH) return 0;
    if (outGlyphId) *outGlyphId = paint.u.glyph.glyphID;
    if (outChildP) *outChildP = paint.u.glyph.paint.p;
    if (outChildInsert) *outChildInsert = paint.u.glyph.paint.insert_root_transform;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_colr_glyph(FT_Face face, void* paintP, int paintInsert,
                                                      uint32_t* outGlyphId) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_COLR_GLYPH) return 0;
    if (outGlyphId) *outGlyphId = paint.u.colr_glyph.glyphID;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_translate(FT_Face face, void* paintP, int paintInsert,
                                                     int32_t* outDx, int32_t* outDy,
                                                     void** outChildP, int* outChildInsert) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_TRANSLATE) return 0;
    if (outDx) *outDx = paint.u.translate.dx;
    if (outDy) *outDy = paint.u.translate.dy;
    if (outChildP) *outChildP = paint.u.translate.paint.p;
    if (outChildInsert) *outChildInsert = paint.u.translate.paint.insert_root_transform;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_scale(FT_Face face, void* paintP, int paintInsert,
                                                 int32_t* outScaleX, int32_t* outScaleY,
                                                 int32_t* outCenterX, int32_t* outCenterY,
                                                 void** outChildP, int* outChildInsert) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_SCALE) return 0;
    if (outScaleX) *outScaleX = paint.u.scale.scale_x;
    if (outScaleY) *outScaleY = paint.u.scale.scale_y;
    if (outCenterX) *outCenterX = paint.u.scale.center_x;
    if (outCenterY) *outCenterY = paint.u.scale.center_y;
    if (outChildP) *outChildP = paint.u.scale.paint.p;
    if (outChildInsert) *outChildInsert = paint.u.scale.paint.insert_root_transform;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_rotate(FT_Face face, void* paintP, int paintInsert,
                                                  int32_t* outAngle, int32_t* outCenterX, int32_t* outCenterY,
                                                  void** outChildP, int* outChildInsert) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_ROTATE) return 0;
    if (outAngle) *outAngle = paint.u.rotate.angle;
    if (outCenterX) *outCenterX = paint.u.rotate.center_x;
    if (outCenterY) *outCenterY = paint.u.rotate.center_y;
    if (outChildP) *outChildP = paint.u.rotate.paint.p;
    if (outChildInsert) *outChildInsert = paint.u.rotate.paint.insert_root_transform;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_skew(FT_Face face, void* paintP, int paintInsert,
                                                int32_t* outXSkew, int32_t* outYSkew,
                                                int32_t* outCenterX, int32_t* outCenterY,
                                                void** outChildP, int* outChildInsert) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_SKEW) return 0;
    if (outXSkew) *outXSkew = paint.u.skew.x_skew_angle;
    if (outYSkew) *outYSkew = paint.u.skew.y_skew_angle;
    if (outCenterX) *outCenterX = paint.u.skew.center_x;
    if (outCenterY) *outCenterY = paint.u.skew.center_y;
    if (outChildP) *outChildP = paint.u.skew.paint.p;
    if (outChildInsert) *outChildInsert = paint.u.skew.paint.insert_root_transform;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_transform(FT_Face face, void* paintP, int paintInsert,
                                                     int32_t* outXX, int32_t* outXY, int32_t* outDX,
                                                     int32_t* outYX, int32_t* outYY, int32_t* outDY,
                                                     void** outChildP, int* outChildInsert) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_TRANSFORM) return 0;
    if (outXX) *outXX = paint.u.transform.affine.xx;
    if (outXY) *outXY = paint.u.transform.affine.xy;
    if (outDX) *outDX = paint.u.transform.affine.dx;
    if (outYX) *outYX = paint.u.transform.affine.yx;
    if (outYY) *outYY = paint.u.transform.affine.yy;
    if (outDY) *outDY = paint.u.transform.affine.dy;
    if (outChildP) *outChildP = paint.u.transform.paint.p;
    if (outChildInsert) *outChildInsert = paint.u.transform.paint.insert_root_transform;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_composite(FT_Face face, void* paintP, int paintInsert,
                                                     int32_t* outMode,
                                                     void** outBackdropP, int* outBackdropInsert,
                                                     void** outSourceP, int* outSourceInsert) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_COMPOSITE) return 0;
    if (outMode) *outMode = paint.u.composite.composite_mode;
    if (outBackdropP) *outBackdropP = paint.u.composite.backdrop_paint.p;
    if (outBackdropInsert) *outBackdropInsert = paint.u.composite.backdrop_paint.insert_root_transform;
    if (outSourceP) *outSourceP = paint.u.composite.source_paint.p;
    if (outSourceInsert) *outSourceInsert = paint.u.composite.source_paint.insert_root_transform;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_linear_gradient(FT_Face face, void* paintP, int paintInsert,
                                                           int32_t* outP0x, int32_t* outP0y,
                                                           int32_t* outP1x, int32_t* outP1y,
                                                           int32_t* outP2x, int32_t* outP2y,
                                                           int32_t* outExtend,
                                                           uint32_t* outNumStops, uint32_t* outCurrentStop,
                                                           void** outStopIterP, int* outReadVar) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_LINEAR_GRADIENT) return 0;
    if (outP0x) *outP0x = paint.u.linear_gradient.p0.x;
    if (outP0y) *outP0y = paint.u.linear_gradient.p0.y;
    if (outP1x) *outP1x = paint.u.linear_gradient.p1.x;
    if (outP1y) *outP1y = paint.u.linear_gradient.p1.y;
    if (outP2x) *outP2x = paint.u.linear_gradient.p2.x;
    if (outP2y) *outP2y = paint.u.linear_gradient.p2.y;
    if (outExtend) *outExtend = paint.u.linear_gradient.colorline.extend;
    FT_ColorStopIterator iter = paint.u.linear_gradient.colorline.color_stop_iterator;
    if (outNumStops) *outNumStops = iter.num_color_stops;
    if (outCurrentStop) *outCurrentStop = iter.current_color_stop;
    if (outStopIterP) *outStopIterP = iter.p;
    if (outReadVar) *outReadVar = iter.read_variable;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_radial_gradient(FT_Face face, void* paintP, int paintInsert,
                                                           int32_t* outC0x, int32_t* outC0y, int32_t* outR0,
                                                           int32_t* outC1x, int32_t* outC1y, int32_t* outR1,
                                                           int32_t* outExtend,
                                                           uint32_t* outNumStops, uint32_t* outCurrentStop,
                                                           void** outStopIterP, int* outReadVar) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_RADIAL_GRADIENT) return 0;
    if (outC0x) *outC0x = paint.u.radial_gradient.c0.x;
    if (outC0y) *outC0y = paint.u.radial_gradient.c0.y;
    if (outR0) *outR0 = paint.u.radial_gradient.r0;
    if (outC1x) *outC1x = paint.u.radial_gradient.c1.x;
    if (outC1y) *outC1y = paint.u.radial_gradient.c1.y;
    if (outR1) *outR1 = paint.u.radial_gradient.r1;
    if (outExtend) *outExtend = paint.u.radial_gradient.colorline.extend;
    FT_ColorStopIterator iter = paint.u.radial_gradient.colorline.color_stop_iterator;
    if (outNumStops) *outNumStops = iter.num_color_stops;
    if (outCurrentStop) *outCurrentStop = iter.current_color_stop;
    if (outStopIterP) *outStopIterP = iter.p;
    if (outReadVar) *outReadVar = iter.read_variable;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_paint_sweep_gradient(FT_Face face, void* paintP, int paintInsert,
                                                          int32_t* outCx, int32_t* outCy,
                                                          int32_t* outStartAngle, int32_t* outEndAngle,
                                                          int32_t* outExtend,
                                                          uint32_t* outNumStops, uint32_t* outCurrentStop,
                                                          void** outStopIterP, int* outReadVar) {
    FT_OpaquePaint opaquePaint = MakeOpaquePaint(paintP, paintInsert);
    FT_COLR_Paint paint;
    if (!FT_Get_Paint(face, opaquePaint, &paint)) return 0;
    if (paint.format != FT_COLR_PAINTFORMAT_SWEEP_GRADIENT) return 0;
    if (outCx) *outCx = paint.u.sweep_gradient.center.x;
    if (outCy) *outCy = paint.u.sweep_gradient.center.y;
    if (outStartAngle) *outStartAngle = paint.u.sweep_gradient.start_angle;
    if (outEndAngle) *outEndAngle = paint.u.sweep_gradient.end_angle;
    if (outExtend) *outExtend = paint.u.sweep_gradient.colorline.extend;
    FT_ColorStopIterator iter = paint.u.sweep_gradient.colorline.color_stop_iterator;
    if (outNumStops) *outNumStops = iter.num_color_stops;
    if (outCurrentStop) *outCurrentStop = iter.current_color_stop;
    if (outStopIterP) *outStopIterP = iter.p;
    if (outReadVar) *outReadVar = iter.read_variable;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_colorstop(FT_Face face,
                                               uint32_t* ioNumStops, uint32_t* ioCurrentStop,
                                               void** ioIterP, int* ioReadVar,
                                               int32_t* outStopOffset, uint16_t* outColorIndex, int32_t* outAlpha) {
    FT_ColorStopIterator iter;
    iter.num_color_stops = ioNumStops ? *ioNumStops : 0;
    iter.current_color_stop = ioCurrentStop ? *ioCurrentStop : 0;
    iter.p = ioIterP ? (FT_Byte*)*ioIterP : nullptr;
    iter.read_variable = ioReadVar ? (FT_Bool)*ioReadVar : 0;

    FT_ColorStop stop;
    if (!FT_Get_Colorline_Stops(face, &stop, &iter)) return 0;

    if (ioNumStops) *ioNumStops = iter.num_color_stops;
    if (ioCurrentStop) *ioCurrentStop = iter.current_color_stop;
    if (ioIterP) *ioIterP = iter.p;
    if (ioReadVar) *ioReadVar = iter.read_variable;

    if (outStopOffset) *outStopOffset = stop.stop_offset;
    if (outColorIndex) *outColorIndex = stop.color.palette_index;
    if (outAlpha) *outAlpha = stop.color.alpha;
    return 1;
}

UNITEXT_EXPORT int ut_colr_get_clipbox(FT_Face face, uint32_t baseGlyph,
                                             int32_t* outBLX, int32_t* outBLY,
                                             int32_t* outTLX, int32_t* outTLY,
                                             int32_t* outTRX, int32_t* outTRY,
                                             int32_t* outBRX, int32_t* outBRY) {
    FT_ClipBox clipBox;
    if (!FT_Get_Color_Glyph_ClipBox(face, baseGlyph, &clipBox)) {
        return 0;
    }
    if (outBLX) *outBLX = (int32_t)clipBox.bottom_left.x;
    if (outBLY) *outBLY = (int32_t)clipBox.bottom_left.y;
    if (outTLX) *outTLX = (int32_t)clipBox.top_left.x;
    if (outTLY) *outTLY = (int32_t)clipBox.top_left.y;
    if (outTRX) *outTRX = (int32_t)clipBox.top_right.x;
    if (outTRY) *outTRY = (int32_t)clipBox.top_right.y;
    if (outBRX) *outBRX = (int32_t)clipBox.bottom_right.x;
    if (outBRY) *outBRY = (int32_t)clipBox.bottom_right.y;
    return 1;
}

// =============================================================================
// Blend2D C++ API Wrappers
// =============================================================================

// --- Image ---
UNITEXT_EXPORT void* ut_blImageCreate(int w, int h, uint32_t format) {
    BLImage* img = new BLImage();
    if (img->create(w, h, (BLFormat)format) != BL_SUCCESS) {
        delete img;
        return nullptr;
    }
    return img;
}

UNITEXT_EXPORT void ut_blImageDestroy(void* img) {
    delete static_cast<BLImage*>(img);
}

UNITEXT_EXPORT void* ut_blImageGetData(void* img, int* outStride) {
    BLImageData data;
    data.reset();
    BLResult result = static_cast<BLImage*>(img)->get_data(&data);
    if (result != BL_SUCCESS || data.pixel_data == nullptr) {
        if (outStride) *outStride = 0;
        return nullptr;
    }
    if (outStride) *outStride = (int)data.stride;
    return data.pixel_data;
}

// --- Context ---
UNITEXT_EXPORT void* ut_blContextCreate(void* img) {
    BLContext* ctx = new BLContext(*static_cast<BLImage*>(img));
    return ctx;
}

UNITEXT_EXPORT void ut_blContextDestroy(void* ctx) {
    delete static_cast<BLContext*>(ctx);
}

UNITEXT_EXPORT void ut_blContextEnd(void* ctx) {
    static_cast<BLContext*>(ctx)->end();
}

UNITEXT_EXPORT void ut_blContextSetFillStyleRgba32(void* ctx, uint32_t rgba32) {
    static_cast<BLContext*>(ctx)->set_fill_style(BLRgba32(rgba32));
}

UNITEXT_EXPORT void ut_blContextFillAll(void* ctx) {
    static_cast<BLContext*>(ctx)->fill_all();
}

UNITEXT_EXPORT void ut_blContextFillRect(void* ctx, double x, double y, double w, double h) {
    static_cast<BLContext*>(ctx)->fill_rect(x, y, w, h);
}

UNITEXT_EXPORT void ut_blContextFillPath(void* ctx, void* path) {
    static_cast<BLContext*>(ctx)->fill_path(*static_cast<BLPath*>(path));
}

UNITEXT_EXPORT void ut_blContextSetFillStyleGradient(void* ctx, void* gradient) {
    static_cast<BLContext*>(ctx)->set_fill_style(*static_cast<BLGradient*>(gradient));
}

UNITEXT_EXPORT void ut_blContextSave(void* ctx) {
    static_cast<BLContext*>(ctx)->save();
}

UNITEXT_EXPORT void ut_blContextRestore(void* ctx) {
    static_cast<BLContext*>(ctx)->restore();
}

UNITEXT_EXPORT void ut_blContextTranslate(void* ctx, double x, double y) {
    static_cast<BLContext*>(ctx)->translate(x, y);
}

UNITEXT_EXPORT void ut_blContextScale(void* ctx, double x, double y) {
    static_cast<BLContext*>(ctx)->scale(x, y);
}

UNITEXT_EXPORT void ut_blContextRotate(void* ctx, double angle) {
    static_cast<BLContext*>(ctx)->rotate(angle);
}

UNITEXT_EXPORT void ut_blContextTransform(void* ctx, double m00, double m01, double m10, double m11, double m20, double m21) {
    BLMatrix2D mat(m00, m01, m10, m11, m20, m21);
    static_cast<BLContext*>(ctx)->apply_transform(mat);
}

UNITEXT_EXPORT void ut_blContextResetMatrix(void* ctx) {
    static_cast<BLContext*>(ctx)->reset_transform();
}

UNITEXT_EXPORT void ut_blContextSetCompOp(void* ctx, uint32_t compOp) {
    static_cast<BLContext*>(ctx)->set_comp_op((BLCompOp)compOp);
}

UNITEXT_EXPORT void ut_blContextSetFillRule(void* ctx, uint32_t fillRule) {
    static_cast<BLContext*>(ctx)->set_fill_rule((BLFillRule)fillRule);
}

UNITEXT_EXPORT void ut_blContextClipToRect(void* ctx, double x, double y, double w, double h) {
    static_cast<BLContext*>(ctx)->clip_to_rect(x, y, w, h);
}

UNITEXT_EXPORT void ut_blContextRestoreClipping(void* ctx) {
    static_cast<BLContext*>(ctx)->restore_clipping();
}

UNITEXT_EXPORT void ut_blContextBlitImage(void* ctx, void* img, double x, double y) {
    BLPointI pt((int)x, (int)y);
    static_cast<BLContext*>(ctx)->blit_image(pt, *static_cast<BLImage*>(img));
}

// --- Path ---
UNITEXT_EXPORT void* ut_blPathCreate() {
    return new BLPath();
}

UNITEXT_EXPORT void ut_blPathDestroy(void* path) {
    delete static_cast<BLPath*>(path);
}

UNITEXT_EXPORT void ut_blPathClear(void* path) {
    static_cast<BLPath*>(path)->clear();
}

UNITEXT_EXPORT void ut_blPathMoveTo(void* path, double x, double y) {
    static_cast<BLPath*>(path)->move_to(x, y);
}

UNITEXT_EXPORT void ut_blPathLineTo(void* path, double x, double y) {
    static_cast<BLPath*>(path)->line_to(x, y);
}

UNITEXT_EXPORT void ut_blPathQuadTo(void* path, double x1, double y1, double x2, double y2) {
    static_cast<BLPath*>(path)->quad_to(x1, y1, x2, y2);
}

UNITEXT_EXPORT void ut_blPathCubicTo(void* path, double x1, double y1, double x2, double y2, double x3, double y3) {
    static_cast<BLPath*>(path)->cubic_to(x1, y1, x2, y2, x3, y3);
}

UNITEXT_EXPORT void ut_blPathClose(void* path) {
    static_cast<BLPath*>(path)->close();
}

UNITEXT_EXPORT void ut_blPathTransform(void* path, double m00, double m01, double m10, double m11, double m20, double m21) {
    BLMatrix2D matrix(m00, m01, m10, m11, m20, m21);
    static_cast<BLPath*>(path)->transform(matrix);
}

// --- Gradient ---
UNITEXT_EXPORT void* ut_blGradientCreateLinear(double x0, double y0, double x1, double y1) {
    BLGradient* grad = new BLGradient(BLLinearGradientValues(x0, y0, x1, y1));
    return grad;
}

UNITEXT_EXPORT void* ut_blGradientCreateRadial(double cx, double cy, double fx, double fy, double r) {
    BLGradient* grad = new BLGradient(BLRadialGradientValues(cx, cy, fx, fy, r));
    return grad;
}

UNITEXT_EXPORT void* ut_blGradientCreateConic(double cx, double cy, double angle) {
    BLGradient* grad = new BLGradient(BLConicGradientValues(cx, cy, angle));
    return grad;
}

UNITEXT_EXPORT void ut_blGradientDestroy(void* grad) {
    delete static_cast<BLGradient*>(grad);
}

UNITEXT_EXPORT void ut_blGradientAddStop(void* grad, double offset, uint32_t rgba32) {
    static_cast<BLGradient*>(grad)->add_stop(offset, BLRgba32(rgba32));
}

UNITEXT_EXPORT void ut_blGradientResetStops(void* grad) {
    static_cast<BLGradient*>(grad)->reset_stops();
}

UNITEXT_EXPORT void ut_blGradientApplyTransform(void* grad, double m00, double m01, double m10, double m11, double m20, double m21) {
    BLMatrix2D mat(m00, m01, m10, m11, m20, m21);
    static_cast<BLGradient*>(grad)->apply_transform(mat);
}

