#include "smooth_filter.h"

void SF_Init(SmoothFilter *f, int32_t n, int32_t x) {
    if (n < 0) n = 0;
    if (n > 20) n = 20;
    f->denom = (1L << n) + 1;
    f->acc = (int32_t)x * f->denom;
    f->out_y = x;
}

void SF_Apply(SmoothFilter *f, int32_t x) {
    if (x < 0) x = 0;
    else if (x > 2000) x = 2000;
    
    // 初期化忘れ防止：強制的に n=0 で初期化する
    if (f->denom == 0) SF_Init(f, 0, x); 

    int32_t current_y = f->acc / f->denom;
    f->acc += (x - current_y) * 2;
    f->out_y = f->acc / f->denom;
}