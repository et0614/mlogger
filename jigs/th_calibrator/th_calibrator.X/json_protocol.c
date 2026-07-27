#include "json_protocol.h"
#include "jsmn.h"
#include "usb_comm.h"   // USB_Comm_SendString
#include "version.h"    // FW_VERSION / PROTOCOL_VERSION
#include "main.h"       // Clock_Set/Get / setLoggingActive / getLoggingActive
#include "eeprom_manager.h" // EM_mSettings / EM_saveMeasurementSetting
#include "w25q64.h"         // W25_Count_Record / W25_ReadRecord / SensorData_t
#include "tca9548a.h"       // Tca_Select / Tca_Deselect
#include "i2c_master.h"     // I2C_IsConnected

#include <string.h>
#include <stdlib.h>
#include <stdio.h>
#include <stdint.h>
#include <stdbool.h>

// ============================================================
// 設定
// ============================================================
#define JP_MAX_CMD     256   // 1コマンドの最大文字数 (ラインバッファ)
#define JP_MAX_TOKENS  32    // jsmn トークン上限
#define JP_REPLY_CAP   128   // 1応答の最大文字数

// 子機の I2C アドレス既定値 (mlogger_th_sensor.X/i2c_shared_data.h と同期)。
// mux_scan の "addr" 省略時に使う。
#define DEFAULT_SLAVE_ADDR   0x11

// ============================================================
// 受信ラインバッファ
// ============================================================
static char     s_line[JP_MAX_CMD];
static uint16_t s_pos = 0;

// ============================================================
// 応答送信
// ============================================================
static void send_line(const char *s)
{
    USB_Comm_SendString(s);
}

// 成功(追加フィールド無し): {"id":N,"ok":true}\n
static void reply_ok(int32_t id)
{
    char b[48];
    snprintf(b, sizeof(b), "{\"id\":%ld,\"ok\":true}\n", (long)id);
    send_line(b);
}

// 失敗: {"id":N,"ok":false,"error":"<code>"}\n
static void reply_error(int32_t id, const char *code)
{
    char b[JP_REPLY_CAP];
    snprintf(b, sizeof(b), "{\"id\":%ld,\"ok\":false,\"error\":\"%s\"}\n",
             (long)id, code);
    send_line(b);
}

// ============================================================
// jsmn トークン操作ヘルパ
// ============================================================

// 文字列トークン t が C 文字列 s と一致するか
static bool tok_eq(const char *json, const jsmntok_t *t, const char *s)
{
    int len = t->end - t->start;
    return t->type == JSMN_STRING
        && (int)strlen(s) == len
        && strncmp(json + t->start, s, (size_t)len) == 0;
}

// プリミティブ/文字列トークンを整数として解釈する
static int32_t tok_int(const char *json, const jsmntok_t *t)
{
    char tmp[12];
    int len = t->end - t->start;
    if (len <= 0 || len >= (int)sizeof(tmp)) return 0;
    memcpy(tmp, json + t->start, (size_t)len);
    tmp[len] = '\0';
    return (int32_t)atol(tmp);
}

// オブジェクト obj_idx の直下から key に対応する「値トークン」の index を返す。
// 見つからなければ -1。
static int obj_get(const char *json, const jsmntok_t *t, int ntok,
                   int obj_idx, const char *key)
{
    if (obj_idx < 0 || t[obj_idx].type != JSMN_OBJECT) return -1;

    int end = t[obj_idx].end;
    int i = obj_idx + 1;
    while (i < ntok && t[i].start < end)
    {
        if (tok_eq(json, &t[i], key)) return i + 1;

        i++;                       // 値トークンへ
        if (i >= ntok) break;
        int vend = t[i].end;
        i++;                       // 値の最初の子へ
        while (i < ntok && t[i].start < vend) i++;
    }
    return -1;
}

// ============================================================
// コマンドハンドラ
// ============================================================
typedef void (*jp_handler_fn)(int32_t id, const char *json,
                              const jsmntok_t *t, int ntok, int params);

// 疎通確認: 追加フィールド無しで ok を返すだけ
static void ph_ping(int32_t id, const char *json,
                    const jsmntok_t *t, int ntok, int params)
{
    (void)json; (void)t; (void)ntok; (void)params;
    reply_ok(id);
}

// ファームウェア情報を返す
static void ph_hello(int32_t id, const char *json,
                     const jsmntok_t *t, int ntok, int params)
{
    (void)json; (void)t; (void)ntok; (void)params;
    char b[JP_REPLY_CAP];
    snprintf(b, sizeof(b),
             "{\"id\":%ld,\"ok\":true,\"fw\":\"%s\",\"proto\":%d}\n",
             (long)id, FW_VERSION, (int)PROTOCOL_VERSION);
    send_line(b);
}

// params.n をそのまま返すエコー (params 抽出パスの疎通確認用)
static void ph_echo(int32_t id, const char *json,
                    const jsmntok_t *t, int ntok, int params)
{
    int32_t n = 0;
    if (params >= 0)
    {
        int nt = obj_get(json, t, ntok, params, "n");
        if (nt >= 0) n = tok_int(json, &t[nt]);
    }
    char b[JP_REPLY_CAP];
    snprintf(b, sizeof(b),
             "{\"id\":%ld,\"ok\":true,\"n\":%ld}\n",
             (long)id, (long)n);
    send_line(b);
}

// ロギング(計測)開始。応答後、メインループがロギングモードへ移行し、
// STANDBYスリープ＋定期計測に入る。省電力のため以降USBは応答しなくなる。
static void ph_start_logging(int32_t id, const char *json,
                             const jsmntok_t *t, int ntok, int params)
{
    (void)json; (void)t; (void)ntok; (void)params;
    setLoggingActive(true);
    reply_ok(id);
    // ack を確実に送出してから USB を止める。USB 有効のままだと OSCHF(12MHz) が起き続け
    // STANDBY でも ~2mA 消費するため。以降 USB 送信不可、通信復帰にはリセット。
    USB_Comm_Flush();
    USB_Comm_Disable();
}

// ロギング(計測)停止。USB通信モードに留まる。
static void ph_stop_logging(int32_t id, const char *json,
                            const jsmntok_t *t, int ntok, int params)
{
    (void)json; (void)t; (void)ntok; (void)params;
    setLoggingActive(false);
    reply_ok(id);
}

// 日時設定: params.epoch (UNIX秒) をソフトウェア時計に設定する。
static void ph_set_time(int32_t id, const char *json,
                        const jsmntok_t *t, int ntok, int params)
{
    int et = (params >= 0) ? obj_get(json, t, ntok, params, "epoch") : -1;
    if (et < 0) { reply_error(id, "missing_epoch"); return; }
    Clock_Set((uint32_t)tok_int(json, &t[et]));
    reply_ok(id);
}

// 計測間隔設定: params.sec (秒) を interval_co2 スロットに設定し EEPROM 保存する。
// (骨格段階では旧 MeasurementSettings の interval_co2 フィールドを流用。Step 3 で
//  th_calibrator 専用の設定構造に置き換え予定。)
static void ph_set_interval(int32_t id, const char *json,
                            const jsmntok_t *t, int ntok, int params)
{
    int st = (params >= 0) ? obj_get(json, t, ntok, params, "sec") : -1;
    if (st < 0) { reply_error(id, "missing_sec"); return; }
    int32_t sec = tok_int(json, &t[st]);
    if (sec <= 0) { reply_error(id, "invalid_sec"); return; }
    EM_mSettings.interval_co2 = (unsigned int)sec;
    EM_saveMeasurementSetting();
    char b[JP_REPLY_CAP];
    snprintf(b, sizeof(b), "{\"id\":%ld,\"ok\":true,\"interval\":%ld}\n",
             (long)id, (long)sec);
    send_line(b);
}

// 状態取得 (検証用): logging / interval / 保存件数 / 現在時刻 / 計測開始日時 を返す。
static void ph_get_status(int32_t id, const char *json,
                          const jsmntok_t *t, int ntok, int params)
{
    (void)json; (void)t; (void)ntok; (void)params;
    char b[192];
    snprintf(b, sizeof(b),
        "{\"id\":%ld,\"ok\":true,\"logging\":%s,\"interval\":%u,"
        "\"count\":%lu,\"epoch\":%lu,\"start_dt\":%lu}\n",
        (long)id,
        getLoggingActive() ? "true" : "false",
        (unsigned)EM_mSettings.interval_co2,
        (unsigned long)W25_Count_Record(),
        (unsigned long)Clock_Get(),
        (unsigned long)EM_mSettings.start_dt);
    send_line(b);
}

// 1回の送出にまとめるレコード数 (骨格の 6B*40=240B、CDC送信バッファ 512B 以内)。
#define DUMP_BATCH_RECS  40

// レコード [start, start+count) を生バイナリでバッチ送出する (dump / dump_range 共通)。
static void dump_records_binary(uint32_t start, uint32_t count)
{
    uint8_t buf[DUMP_BATCH_RECS * sizeof(SensorData_t)];
    uint16_t fill = 0;
    for (uint32_t i = 0; i < count; i++)
    {
        SensorData_t rec;
        if (!W25_ReadRecord(start + i, &rec)) break;
        memcpy(&buf[fill], &rec, sizeof(rec));
        fill += sizeof(rec);
        if (fill >= sizeof(buf))
        {
            USB_Comm_SendBytes(buf, fill);
            fill = 0;
        }
    }
    if (fill > 0) USB_Comm_SendBytes(buf, fill);
}

// データ読み出し (dump): JSONヘッダ → 生バイナリレコード列 → JSONフッタ。
// (骨格段階では旧 6B レコードのままで dump する。Step 3 で 64ch レコード形式に差し替え。)
static void ph_dump(int32_t id, const char *json,
                    const jsmntok_t *t, int ntok, int params)
{
    (void)json; (void)t; (void)ntok; (void)params;

    uint32_t count = W25_Count_Record();
    char b[JP_REPLY_CAP];

    snprintf(b, sizeof(b),
        "{\"id\":%ld,\"ok\":true,\"dump\":\"begin\",\"count\":%lu,\"rec_size\":%u,"
        "\"start_dt\":%lu,\"interval\":%u}\n",
        (long)id, (unsigned long)count, (unsigned)sizeof(SensorData_t),
        (unsigned long)EM_mSettings.start_dt, (unsigned)EM_mSettings.interval_co2);
    send_line(b);

    dump_records_binary(0, count);

    snprintf(b, sizeof(b), "{\"id\":%ld,\"dump\":\"end\"}\n", (long)id);
    send_line(b);
    USB_Comm_Flush();
}

// バス配線チェック: 8×8=64 スロットを巡回し、指定 I2C アドレスへの ACK 応答有無を返す。
// params:
//   addr (省略可): スキャン対象の 7bit I2C アドレス (既定は DEFAULT_SLAVE_ADDR = 0x11)。
// 応答:
//   {"id":N,"ok":true,"addr":17,"present":"...64文字のビット列..."}\n
//   present[i*8+j] は mux #i の ch #j に応答があれば '1'、無ければ '0'。
//   例) mux0-ch0 のみ応答 = "10000000""00000000""00000000""00000000""00000000""00000000""00000000""00000000"
// 用途:
//   ・8個の TCA9548A の RST 配線 (PD7,PD6,PD5,PD4,PD3,PD2,PD1,PC3) 検証
//   ・8個の TCA9548A のアドレス配線 (A0-A2 が mux 番号と一致するか) 検証
//   ・I2C プルアップと 64 台の子機物理配線検証
//   ・不良ソケットや半田不良の切り分け
static void ph_mux_scan(int32_t id, const char *json,
                        const jsmntok_t *t, int ntok, int params)
{
    // addr 抽出 (省略時は 0x11)
    int32_t addr = DEFAULT_SLAVE_ADDR;
    if (params >= 0)
    {
        int at = obj_get(json, t, ntok, params, "addr");
        if (at >= 0) addr = tok_int(json, &t[at]);
    }
    if (addr < 0x08 || addr > 0x77) { reply_error(id, "invalid_addr"); return; }

    // 64 slot を巡回。1 slot ごとに Tca_Select → I2C_IsConnected → Tca_Deselect。
    // ここでは正確性優先で毎回 Deselect する (バス衝突を避ける)。
    char present[TCA_MUX_COUNT * TCA_CH_PER_MUX + 1];
    for (uint8_t m = 0; m < TCA_MUX_COUNT; m++)
    {
        for (uint8_t ch = 0; ch < TCA_CH_PER_MUX; ch++)
        {
            bool ok = Tca_Select(m, ch);
            if (ok) ok = I2C_IsConnected((uint8_t)addr);
            (void)Tca_Deselect(m);
            present[m * TCA_CH_PER_MUX + ch] = ok ? '1' : '0';
        }
    }
    present[TCA_MUX_COUNT * TCA_CH_PER_MUX] = '\0';

    char b[JP_REPLY_CAP];
    snprintf(b, sizeof(b),
        "{\"id\":%ld,\"ok\":true,\"addr\":%ld,\"present\":\"%s\"}\n",
        (long)id, (long)addr, present);
    send_line(b);
}

// 範囲指定データ読み出し (dump_range): params { "start": N, "count": M }
static void ph_dump_range(int32_t id, const char *json,
                          const jsmntok_t *t, int ntok, int params)
{
    int32_t start = 0, want = 0;
    if (params >= 0)
    {
        int st = obj_get(json, t, ntok, params, "start");
        int ct = obj_get(json, t, ntok, params, "count");
        if (st >= 0) start = tok_int(json, &t[st]);
        if (ct >= 0) want  = tok_int(json, &t[ct]);
    }
    if (start < 0) start = 0;
    if (want  < 0) want  = 0;

    uint32_t total = W25_Count_Record();

    uint32_t s = (uint32_t)start;
    uint32_t c;
    if (s >= total)                       c = 0;
    else if ((uint32_t)want > (total - s)) c = total - s;
    else                                   c = (uint32_t)want;

    char b[160];
    snprintf(b, sizeof(b),
        "{\"id\":%ld,\"ok\":true,\"dump\":\"begin\",\"start\":%lu,\"count\":%lu,"
        "\"total\":%lu,\"rec_size\":%u,\"start_dt\":%lu,\"interval\":%u}\n",
        (long)id, (unsigned long)s, (unsigned long)c, (unsigned long)total,
        (unsigned)sizeof(SensorData_t),
        (unsigned long)EM_mSettings.start_dt, (unsigned)EM_mSettings.interval_co2);
    send_line(b);

    dump_records_binary(s, c);

    snprintf(b, sizeof(b), "{\"id\":%ld,\"dump\":\"end\"}\n", (long)id);
    send_line(b);
    USB_Comm_Flush();
}

// コマンドテーブル (NULL 終端)
typedef struct {
    const char    *name;
    jp_handler_fn  handler;
} jp_command_t;

// Step 0 骨格のコマンド一覧。
// (Step 1 で mux_scan、Step 3 で clear_memory / measure / selftest 等を追加予定)
static const jp_command_t s_commands[] = {
    { "ping",          ph_ping          },
    { "hello",         ph_hello         },
    { "echo",          ph_echo          },
    { "start_logging", ph_start_logging },
    { "stop_logging",  ph_stop_logging  },
    { "set_time",      ph_set_time      },
    { "set_interval",  ph_set_interval  },
    { "get_status",    ph_get_status    },
    { "mux_scan",      ph_mux_scan      },
    { "dump",          ph_dump          },
    { "dump_range",    ph_dump_range    },
    { NULL,            NULL             }
};

// ============================================================
// ディスパッチ
// ============================================================
void JP_ProcessLine(const char *line)
{
    if (line[0] != '{') return;

    jsmn_parser parser;
    jsmn_init(&parser);
    jsmntok_t toks[JP_MAX_TOKENS];
    int n = jsmn_parse(&parser, line, strlen(line), toks, JP_MAX_TOKENS);

    if (n < 1 || toks[0].type != JSMN_OBJECT)
    {
        reply_error(0, "parse_error");
        return;
    }

    int32_t id = 0;
    int id_tok = obj_get(line, toks, n, 0, "id");
    if (id_tok >= 0) id = tok_int(line, &toks[id_tok]);

    int cmd_tok = obj_get(line, toks, n, 0, "command");
    if (cmd_tok < 0 || toks[cmd_tok].type != JSMN_STRING)
    {
        reply_error(id, "missing_command");
        return;
    }

    int params_tok = obj_get(line, toks, n, 0, "params");
    if (params_tok >= 0 && toks[params_tok].type != JSMN_OBJECT)
    {
        reply_error(id, "invalid_params");
        return;
    }

    for (const jp_command_t *c = s_commands; c->name != NULL; c++)
    {
        if (tok_eq(line, &toks[cmd_tok], c->name))
        {
            c->handler(id, line, toks, n, params_tok);
            return;
        }
    }

    reply_error(id, "unknown_command");
}

// ============================================================
// ラインバッファ
// ============================================================
void JP_AppendChar(char c)
{
    if (c == '\r' || c == '\n')
    {
        if (s_pos > 0)
        {
            s_line[s_pos] = '\0';
            JP_ProcessLine(s_line);
            s_pos = 0;
        }
    }
    else if (s_pos < JP_MAX_CMD - 1)
    {
        s_line[s_pos++] = c;
    }
    else
    {
        s_pos = 0;
    }
}
