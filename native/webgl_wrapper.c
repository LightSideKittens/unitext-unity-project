// WebGL Wrapper - Unified ut_* API for Emscripten
// Exports the same API as unitext_native.cpp for C# P/Invoke compatibility

#include <stdlib.h>
#include <string.h>
#include <ft2build.h>
#include FT_FREETYPE_H
#include FT_BITMAP_H
#include FT_COLOR_H
#include FT_TRUETYPE_TABLES_H
#include <hb.h>
#include <hb-ot.h>
#include <hb-ft.h>

#ifdef __EMSCRIPTEN__
#include <emscripten.h>
#define EXPORT EMSCRIPTEN_KEEPALIVE
#else
#define EXPORT
#endif

// =============================================================================
// Version
// =============================================================================

EXPORT const char* ut_version(void) {
    return "3.0.0-unified-webgl";
}

// =============================================================================
// FreeType Unified API (ut_ft_*)
// =============================================================================

EXPORT int ut_ft_init(FT_Library* library) {
    return FT_Init_FreeType(library);
}

EXPORT int ut_ft_done(FT_Library library) {
    return FT_Done_FreeType(library);
}

EXPORT int ut_ft_new_memory_face(FT_Library library, const unsigned char* data, long size, long face_index, FT_Face* face) {
    return FT_New_Memory_Face(library, data, size, face_index, face);
}

EXPORT int ut_ft_done_face(FT_Face face) {
    return FT_Done_Face(face);
}

EXPORT unsigned int ut_ft_get_char_index(FT_Face face, unsigned long charcode) {
    return FT_Get_Char_Index(face, charcode);
}

EXPORT int ut_ft_set_pixel_sizes(FT_Face face, unsigned int width, unsigned int height) {
    return FT_Set_Pixel_Sizes(face, width, height);
}

EXPORT int ut_ft_select_size(FT_Face face, int strike_index) {
    return FT_Select_Size(face, strike_index);
}

EXPORT int ut_ft_load_glyph(FT_Face face, unsigned int glyph_index, int load_flags) {
    return FT_Load_Glyph(face, glyph_index, load_flags);
}

EXPORT int ut_ft_render_glyph(FT_GlyphSlot slot, int render_mode) {
    return FT_Render_Glyph(slot, (FT_Render_Mode)render_mode);
}

EXPORT int ut_ft_palette_data_get(FT_Face face, FT_Palette_Data* palette_data) {
    return FT_Palette_Data_Get(face, palette_data);
}

EXPORT int ut_ft_palette_select(FT_Face face, unsigned short palette_index, FT_Color** palette) {
    return FT_Palette_Select(face, palette_index, palette);
}

EXPORT int ut_ft_get_color_glyph_clipbox(FT_Face face, unsigned int base_glyph, FT_ClipBox* clip_box) {
    return FT_Get_Color_Glyph_ClipBox(face, base_glyph, clip_box);
}

EXPORT int ut_ft_get_color_glyph_layer(FT_Face face, unsigned int base_glyph, unsigned int* glyph_index, unsigned int* color_index, FT_LayerIterator* iterator) {
    return FT_Get_Color_Glyph_Layer(face, base_glyph, glyph_index, color_index, iterator);
}

// =============================================================================
// FreeType Wrapper Functions (ut_ft_*)
// =============================================================================

EXPORT void ut_ft_get_face_info(FT_Face face, long* out_face_flags, int* out_num_glyphs,
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

EXPORT int ut_ft_get_extended_face_info(FT_Face face,
    short* out_cap_height, short* out_x_height,
    short* out_y_superscript_y_offset, short* out_y_superscript_y_size,
    short* out_y_subscript_y_offset, short* out_y_subscript_y_size,
    short* out_y_strikeout_position, short* out_y_strikeout_size,
    short* out_underline_position, short* out_underline_thickness,
    const char** out_family_name, const char** out_style_name)
{
    if (!face) return 0;

    if (out_underline_position) *out_underline_position = face->underline_position;
    if (out_underline_thickness) *out_underline_thickness = face->underline_thickness;
    if (out_family_name) *out_family_name = face->family_name;
    if (out_style_name) *out_style_name = face->style_name;

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

EXPORT int ut_ft_get_fixed_size(FT_Face face, int index) {
    if (!face || index < 0 || index >= face->num_fixed_sizes) return 0;
    return face->available_sizes[index].height;
}

EXPORT void ut_ft_get_glyph_metrics(FT_Face face, int* out_width, int* out_height,
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

EXPORT void ut_ft_get_bitmap_info(FT_Face face, int* out_width, int* out_height,
                                   int* out_pitch, int* out_pixel_mode, void** out_buffer) {
    if (!face || !face->glyph) return;
    FT_Bitmap* b = &face->glyph->bitmap;
    if (out_width) *out_width = (int)b->width;
    if (out_height) *out_height = (int)b->rows;
    if (out_pitch) *out_pitch = b->pitch;
    if (out_pixel_mode) *out_pixel_mode = b->pixel_mode;
    if (out_buffer) *out_buffer = b->buffer;
}

EXPORT FT_GlyphSlot ut_ft_get_glyph_slot(FT_Face face) {
    return face ? face->glyph : NULL;
}

// === Felzenszwalb & Huttenlocher EDT ===

#include <math.h>

#define EDT_INF 1e20f

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
    for (int x = 0; x < w; x++) {
        for (int y = 0; y < h; y++) f[y] = grid[y * w + x];
        edt_1d(f, d, v, z, h);
        for (int y = 0; y < h; y++) grid[y * w + x] = d[y];
    }
    for (int y = 0; y < h; y++) {
        edt_1d(grid + y * w, d, v, z, w);
        memcpy(grid + y * w, d, w * sizeof(float));
    }
}

// === Combined SDF Glyph Render (FT_RENDER_MODE_NORMAL + EDT) ===

typedef struct {
    int success;
    int metric_width, metric_height;
    int metric_bearing_x, metric_bearing_y;
    int metric_advance_x;
    int bmp_width, bmp_height, bmp_pitch;
    int bitmap_left, bitmap_top;
    void* bmp_buffer;
} ut_sdf_glyph_result;

EXPORT int ut_ft_render_sdf_glyph(FT_Face face, unsigned int glyph_index,
                                   int load_flags, int spread,
                                   ut_sdf_glyph_result* out_result) {
    if (!out_result) return -1;
    memset(out_result, 0, sizeof(ut_sdf_glyph_result));
    if (!face) { out_result->success = -1; return -1; }

    FT_Error err = FT_Load_Glyph(face, glyph_index, load_flags);
    if (err) { out_result->success = (int)err; return (int)err; }

    FT_Glyph_Metrics* m = &face->glyph->metrics;
    out_result->metric_width     = (int)(m->width >> 6);
    out_result->metric_height    = (int)(m->height >> 6);
    out_result->metric_bearing_x = (int)(m->horiBearingX >> 6);
    out_result->metric_bearing_y = (int)(m->horiBearingY >> 6);
    out_result->metric_advance_x = (int)m->horiAdvance;

    err = FT_Render_Glyph(face->glyph, FT_RENDER_MODE_NORMAL);
    if (err) { out_result->success = (int)err; return (int)err; }

    FT_Bitmap* b = &face->glyph->bitmap;
    int bw = (int)b->width;
    int bh = (int)b->rows;

    if (bw <= 0 || bh <= 0) {
        out_result->bitmap_left = face->glyph->bitmap_left;
        out_result->bitmap_top  = face->glyph->bitmap_top;
        out_result->success = 0;
        return 0;
    }

    int pw = bw + 2 * spread;
    int ph = bh + 2 * spread;
    int pcount = pw * ph;
    int maxdim = pw > ph ? pw : ph;

    // One malloc for all workspace: outside + inside + edt_f + edt_d + edt_z + edt_v
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

    for (int i = 0; i < pcount; i++) {
        outside[i] = EDT_INF;
        inside[i]  = 0.0f;
    }

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

    edt_2d(outside, pw, ph, edt_f, edt_d, edt_z, edt_v);
    edt_2d(inside, pw, ph, edt_f, edt_d, edt_z, edt_v);

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

    out_result->bmp_width   = pw;
    out_result->bmp_height  = ph;
    out_result->bmp_pitch   = pw;
    out_result->bitmap_left = face->glyph->bitmap_left - spread;
    out_result->bitmap_top  = face->glyph->bitmap_top + spread;
    out_result->bmp_buffer  = sdf;
    out_result->success = 0;
    return 0;
}

EXPORT void ut_ft_free_sdf_buffer(void* buffer) {
    free(buffer);
}

// =============================================================================
// HarfBuzz Unified API (ut_hb_*)
// =============================================================================

EXPORT hb_blob_t* ut_hb_blob_create(const char* data, unsigned int length, hb_memory_mode_t mode, void* user_data, hb_destroy_func_t destroy) {
    return hb_blob_create(data, length, mode, user_data, destroy);
}

EXPORT void ut_hb_blob_destroy(hb_blob_t* blob) {
    hb_blob_destroy(blob);
}

EXPORT hb_face_t* ut_hb_face_create(hb_blob_t* blob, unsigned int index) {
    return hb_face_create(blob, index);
}

EXPORT void ut_hb_face_destroy(hb_face_t* face) {
    hb_face_destroy(face);
}

EXPORT unsigned int ut_hb_face_get_upem(const hb_face_t* face) {
    return hb_face_get_upem(face);
}

EXPORT hb_font_t* ut_hb_font_create(hb_face_t* face) {
    return hb_font_create(face);
}

EXPORT void ut_hb_font_destroy(hb_font_t* font) {
    hb_font_destroy(font);
}

EXPORT void ut_hb_ot_font_set_funcs(hb_font_t* font) {
    hb_ot_font_set_funcs(font);
}

EXPORT int ut_hb_font_get_glyph_h_advance(hb_font_t* font, unsigned int glyph) {
    return hb_font_get_glyph_h_advance(font, glyph);
}

EXPORT int ut_hb_font_get_glyph(hb_font_t* font, unsigned int unicode, unsigned int variation_selector, unsigned int* glyph) {
    return hb_font_get_glyph(font, unicode, variation_selector, glyph);
}

EXPORT hb_face_t* ut_hb_font_get_face(hb_font_t* font) {
    return hb_font_get_face(font);
}

EXPORT hb_buffer_t* ut_hb_buffer_create(void) {
    return hb_buffer_create();
}

EXPORT void ut_hb_buffer_destroy(hb_buffer_t* buffer) {
    hb_buffer_destroy(buffer);
}

EXPORT void ut_hb_buffer_clear_contents(hb_buffer_t* buffer) {
    hb_buffer_clear_contents(buffer);
}

EXPORT void ut_hb_buffer_set_direction(hb_buffer_t* buffer, hb_direction_t direction) {
    hb_buffer_set_direction(buffer, direction);
}

EXPORT void ut_hb_buffer_set_script(hb_buffer_t* buffer, hb_script_t script) {
    hb_buffer_set_script(buffer, script);
}

EXPORT void ut_hb_buffer_set_content_type(hb_buffer_t* buffer, hb_buffer_content_type_t content_type) {
    hb_buffer_set_content_type(buffer, content_type);
}

EXPORT void ut_hb_buffer_set_flags(hb_buffer_t* buffer, hb_buffer_flags_t flags) {
    hb_buffer_set_flags(buffer, flags);
}

EXPORT void ut_hb_buffer_add_codepoints(hb_buffer_t* buffer, const unsigned int* text, int text_length, unsigned int item_offset, int item_length) {
    hb_buffer_add_codepoints(buffer, text, text_length, item_offset, item_length);
}

EXPORT unsigned int ut_hb_buffer_get_length(const hb_buffer_t* buffer) {
    return hb_buffer_get_length(buffer);
}

EXPORT hb_glyph_info_t* ut_hb_buffer_get_glyph_infos(hb_buffer_t* buffer, unsigned int* length) {
    return hb_buffer_get_glyph_infos(buffer, length);
}

EXPORT hb_glyph_position_t* ut_hb_buffer_get_glyph_positions(hb_buffer_t* buffer, unsigned int* length) {
    return hb_buffer_get_glyph_positions(buffer, length);
}

EXPORT void ut_hb_shape(hb_font_t* font, hb_buffer_t* buffer, const hb_feature_t* features, unsigned int num_features) {
    hb_shape(font, buffer, features, num_features);
}

// =============================================================================
// COLRv1 Stubs (not supported on WebGL)
// =============================================================================

EXPORT int ut_colr_get_glyph_paint(FT_Face face, unsigned int baseGlyph, int rootTransform,
                                    void** outPaintP, int* outPaintInsert) {
    if (outPaintP) *outPaintP = NULL;
    if (outPaintInsert) *outPaintInsert = 0;
    return 0;
}

EXPORT int ut_colr_debug_glyph_paint(FT_Face face, unsigned int baseGlyph,
                                      int* hasColr, int* hasCpal, int* ftResult) {
    if (hasColr) *hasColr = 0;
    if (hasCpal) *hasCpal = 0;
    if (ftResult) *ftResult = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_format(FT_Face face, void* paintP, int paintInsert) {
    return -1;
}

EXPORT int ut_colr_get_paint_solid(FT_Face face, void* paintP, int paintInsert,
                                    unsigned short* colorIndex, int* alpha) {
    if (colorIndex) *colorIndex = 0;
    if (alpha) *alpha = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_layers(FT_Face face, void* paintP, int paintInsert,
                                     unsigned int* numLayers, unsigned int* layer, void** iterP) {
    if (numLayers) *numLayers = 0;
    if (layer) *layer = 0;
    if (iterP) *iterP = NULL;
    return 0;
}

EXPORT int ut_colr_get_next_layer(FT_Face face, unsigned int* numLayers, unsigned int* layer,
                                   void** iterP, void** childP, int* childInsert) {
    if (childP) *childP = NULL;
    if (childInsert) *childInsert = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_glyph(FT_Face face, void* paintP, int paintInsert,
                                    unsigned int* glyphId, void** childP, int* childInsert) {
    if (glyphId) *glyphId = 0;
    if (childP) *childP = NULL;
    if (childInsert) *childInsert = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_colr_glyph(FT_Face face, void* paintP, int paintInsert,
                                         unsigned int* glyphId) {
    if (glyphId) *glyphId = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_translate(FT_Face face, void* paintP, int paintInsert,
                                        int* dx, int* dy, void** childP, int* childInsert) {
    if (dx) *dx = 0;
    if (dy) *dy = 0;
    if (childP) *childP = NULL;
    if (childInsert) *childInsert = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_scale(FT_Face face, void* paintP, int paintInsert,
                                    int* scaleX, int* scaleY, int* centerX, int* centerY,
                                    void** childP, int* childInsert) {
    if (scaleX) *scaleX = 0;
    if (scaleY) *scaleY = 0;
    if (centerX) *centerX = 0;
    if (centerY) *centerY = 0;
    if (childP) *childP = NULL;
    if (childInsert) *childInsert = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_rotate(FT_Face face, void* paintP, int paintInsert,
                                     int* angle, int* centerX, int* centerY,
                                     void** childP, int* childInsert) {
    if (angle) *angle = 0;
    if (centerX) *centerX = 0;
    if (centerY) *centerY = 0;
    if (childP) *childP = NULL;
    if (childInsert) *childInsert = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_skew(FT_Face face, void* paintP, int paintInsert,
                                   int* xSkew, int* ySkew, int* centerX, int* centerY,
                                   void** childP, int* childInsert) {
    if (xSkew) *xSkew = 0;
    if (ySkew) *ySkew = 0;
    if (centerX) *centerX = 0;
    if (centerY) *centerY = 0;
    if (childP) *childP = NULL;
    if (childInsert) *childInsert = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_transform(FT_Face face, void* paintP, int paintInsert,
                                        int* xx, int* xy, int* dx, int* yx, int* yy, int* dy,
                                        void** childP, int* childInsert) {
    if (xx) *xx = 0;
    if (xy) *xy = 0;
    if (dx) *dx = 0;
    if (yx) *yx = 0;
    if (yy) *yy = 0;
    if (dy) *dy = 0;
    if (childP) *childP = NULL;
    if (childInsert) *childInsert = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_composite(FT_Face face, void* paintP, int paintInsert,
                                        int* mode, void** backdropP, int* backdropInsert,
                                        void** sourceP, int* sourceInsert) {
    if (mode) *mode = 0;
    if (backdropP) *backdropP = NULL;
    if (backdropInsert) *backdropInsert = 0;
    if (sourceP) *sourceP = NULL;
    if (sourceInsert) *sourceInsert = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_linear_gradient(FT_Face face, void* paintP, int paintInsert,
                                              int* p0x, int* p0y, int* p1x, int* p1y, int* p2x, int* p2y,
                                              int* extend, unsigned int* numStops, unsigned int* currentStop,
                                              void** stopIterP, int* readVar) {
    if (p0x) *p0x = 0;
    if (p0y) *p0y = 0;
    if (p1x) *p1x = 0;
    if (p1y) *p1y = 0;
    if (p2x) *p2x = 0;
    if (p2y) *p2y = 0;
    if (extend) *extend = 0;
    if (numStops) *numStops = 0;
    if (currentStop) *currentStop = 0;
    if (stopIterP) *stopIterP = NULL;
    if (readVar) *readVar = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_radial_gradient(FT_Face face, void* paintP, int paintInsert,
                                              int* c0x, int* c0y, int* r0,
                                              int* c1x, int* c1y, int* r1,
                                              int* extend, unsigned int* numStops, unsigned int* currentStop,
                                              void** stopIterP, int* readVar) {
    if (c0x) *c0x = 0;
    if (c0y) *c0y = 0;
    if (r0) *r0 = 0;
    if (c1x) *c1x = 0;
    if (c1y) *c1y = 0;
    if (r1) *r1 = 0;
    if (extend) *extend = 0;
    if (numStops) *numStops = 0;
    if (currentStop) *currentStop = 0;
    if (stopIterP) *stopIterP = NULL;
    if (readVar) *readVar = 0;
    return 0;
}

EXPORT int ut_colr_get_paint_sweep_gradient(FT_Face face, void* paintP, int paintInsert,
                                             int* cx, int* cy, int* startAngle, int* endAngle,
                                             int* extend, unsigned int* numStops, unsigned int* currentStop,
                                             void** stopIterP, int* readVar) {
    if (cx) *cx = 0;
    if (cy) *cy = 0;
    if (startAngle) *startAngle = 0;
    if (endAngle) *endAngle = 0;
    if (extend) *extend = 0;
    if (numStops) *numStops = 0;
    if (currentStop) *currentStop = 0;
    if (stopIterP) *stopIterP = NULL;
    if (readVar) *readVar = 0;
    return 0;
}

EXPORT int ut_colr_get_colorstop(FT_Face face, unsigned int* numStops, unsigned int* currentStop,
                                  void** iterP, int* readVar, int* stopOffset,
                                  unsigned short* colorIndex, int* alpha) {
    if (stopOffset) *stopOffset = 0;
    if (colorIndex) *colorIndex = 0;
    if (alpha) *alpha = 0;
    return 0;
}

EXPORT int ut_colr_get_clipbox(FT_Face face, unsigned int baseGlyph,
                                int* blX, int* blY, int* tlX, int* tlY,
                                int* trX, int* trY, int* brX, int* brY) {
    if (blX) *blX = 0;
    if (blY) *blY = 0;
    if (tlX) *tlX = 0;
    if (tlY) *tlY = 0;
    if (trX) *trX = 0;
    if (trY) *trY = 0;
    if (brX) *brX = 0;
    if (brY) *brY = 0;
    return 0;
}

// =============================================================================
// Outline/Diagnostics Stubs (not supported on WebGL)
// =============================================================================

EXPORT int ut_ft_outline_to_blpath(FT_Face face, void* blPath) {
    return 0;
}

EXPORT int ut_ft_get_outline_info(FT_Face face, int* numContours, int* numPoints) {
    if (numContours) *numContours = 0;
    if (numPoints) *numPoints = 0;
    return 0;
}

EXPORT int ut_debug_sbix_graphic_type(FT_Face face, unsigned char* outGraphicType, int* outNumStrikes) {
    if (outGraphicType) outGraphicType[0] = 0;
    if (outNumStrikes) *outNumStrikes = 0;
    return 0;
}

// Blend2D not supported on WebGL - stubs are in C# (BL.cs)
