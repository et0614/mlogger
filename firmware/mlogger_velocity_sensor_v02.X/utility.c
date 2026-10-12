#include "utility.h"

// CRC8計算関数（多項式 0x31）
uint8_t calc_crc8(const uint8_t *data, uint8_t len) {
    uint8_t crc = 0xFF;
    for (uint8_t i = 0; i < len; i++) {
        crc ^= data[i];
        for (uint8_t j = 0; j < 8; j++) {
            if (crc & 0x80) crc = (crc << 1) ^ 0x31;
            else crc <<= 1;
        }
    }
    return crc;
}

// エンディアン入れ替え関数
void swap_float(float* f) {
    uint8_t* p = (uint8_t*)f;
    uint8_t temp;
    // 0123 -> 3210
    temp = p[0]; p[0] = p[3]; p[3] = temp;
    temp = p[1]; p[1] = p[2]; p[2] = temp;
}

// FNV-1aでハッシュを計算する
uint32_t fnv1a_32(const uint8_t* data, size_t len) {
    uint32_t hash = 2166136261U; // Offset basis
    for (size_t i = 0; i < len; i++) {
        hash ^= data[i];
        hash *= 16777619U; // FNV prime
    }
    return hash;
}

// FNV-1a (32-bit) を XOR-fold で 22-bit に圧縮し、BACnet Object Instance に
// そのまま使える値を返す。0x3FFFFF は BACnet 予約 (no object instance) のため回避する。
uint32_t fnv1a_22(const uint8_t* data, size_t len) {
    uint32_t h = fnv1a_32(data, len);
    h = (h ^ (h >> 22)) & 0x3FFFFFUL;
    if (h == 0x3FFFFFUL) h = 0x3FFFFEUL;
    return h;
}