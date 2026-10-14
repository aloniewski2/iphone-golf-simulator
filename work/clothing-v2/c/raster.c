// CLOTHING V2: UV-space triangle rasteriser. For every texel centre covered by a triangle: triangle id + barycentrics.
// uv: ntri*6 floats (u0 v0 u1 v1 u2 v2) in [0,1]; row 0 = v 0. Later triangles overwrite earlier ones.
#include <math.h>
void raster(int ntri, const float* uv, int W, int H, int* tid, float* bary) {
    for (int t = 0; t < ntri; t++) {
        const float* q = uv + 6 * t;
        float x0 = q[0] * W, y0 = q[1] * H, x1 = q[2] * W, y1 = q[3] * H, x2 = q[4] * W, y2 = q[5] * H;
        float den = (y1 - y2) * (x0 - x2) + (x2 - x1) * (y0 - y2);
        if (fabsf(den) < 1e-12f) continue;
        int xa = (int)floorf(fminf(x0, fminf(x1, x2))), xb = (int)ceilf(fmaxf(x0, fmaxf(x1, x2)));
        int ya = (int)floorf(fminf(y0, fminf(y1, y2))), yb = (int)ceilf(fmaxf(y0, fmaxf(y1, y2)));
        if (xa < 0) xa = 0; if (ya < 0) ya = 0; if (xb > W - 1) xb = W - 1; if (yb > H - 1) yb = H - 1;
        for (int y = ya; y <= yb; y++) for (int x = xa; x <= xb; x++) {
            float px = x + .5f, py = y + .5f;
            float a = ((y1 - y2) * (px - x2) + (x2 - x1) * (py - y2)) / den;
            float b = ((y2 - y0) * (px - x2) + (x0 - x2) * (py - y2)) / den;
            float c = 1 - a - b;
            const float e = -1e-4f;
            if (a < e || b < e || c < e) continue;
            int i = y * W + x; tid[i] = t; bary[3 * i] = a; bary[3 * i + 1] = b; bary[3 * i + 2] = c;
        }
    }
}
// grow covered texels into empty ones (nearest-ish, iterative 8-neighbour), so mips never pull in empty space
void dilate(int W, int H, int channels, float* img, unsigned char* mask, int iters) {
    for (int it = 0; it < iters; it++) {
        int changed = 0;
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) {
            int i = y * W + x; if (mask[i] != 1) continue;
            for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) {
                int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                int j = yy * W + xx; if (mask[j]) continue;
                for (int c = 0; c < channels; c++) img[j * channels + c] = img[i * channels + c];
                mask[j] = 2; changed++;
            }
        }
        for (int i = 0; i < W * H; i++) if (mask[i] == 2) mask[i] = 1;
        if (!changed) break;
    }
}
