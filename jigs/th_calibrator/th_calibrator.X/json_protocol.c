#include "json_protocol.h"
#include "jsmn.h"
#include "usb_comm.h"   // USB_Comm_SendString
#include "version.h"    // FW_VERSION / PROTOCOL_VERSION
#include "main.h"       // Clock_Set/Get / setLoggingActive / getLoggingActive
#include "eeprom_manager.h" // EM_mSettings / EM_saveMeasurementSetting
#include "w25q64.h"         // W25_Count_Record / W25_ReadRecord / SensorData_t
#include "tca9548a.h"       // Tca_Select / Tca_Deselect
#include "i2c_master.h"     // I2C_IsConnected
#include "th_probe.h"       // ThProbe_Trigger / ThProbe_ReadSample

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

// 全スロット一斉トリガ (th_trigger): 64 スロットを巡回して single-shot 計測を
// 開始させる。子機不在スロットは NACK で即スキップされる。子機の計測は ~500ms
// なので、ホストは ~1sec 待ってから th_read_all を呼ぶこと (pre-trigger 方式)。
static void ph_th_trigger(int32_t id, const char *json,
                          const jsmntok_t *t, int ntok, int params)
{
    (void)json; (void)t; (void)ntok; (void)params;
    for (uint8_t m = 0; m < TCA_MUX_COUNT; m++)
    {
        for (uint8_t ch = 0; ch < TCA_CH_PER_MUX; ch++)
        {
            if (!Tca_Select(m, ch)) break; // mux 自体が無応答なら残り ch も無駄
            ThProbe_Trigger();
        }
        (void)Tca_Deselect(m); // 次の mux に移る前に必ず切る (サブバス衝突防止)
    }
    reply_ok(id);
}

// 全スロット一括読み出し (th_read_all): 64 スロットの POLL ブロックを読んで
// 1 行の JSON で返す。事前に th_trigger → ~1sec 待ちが必要。
// 応答 (1 行、~1.8KB):
//   {"id":N,"ok":true,"vals":[[2531,4820,612,0,1,0],null,...]}
//   vals[m*8+c] = [温度℃*100, 湿度%*100, CO2ppm, status1, status2, stcc4_state]。
//   子機不在スロット (I2C NACK) は null。stale の値は要素単位で null。
//   末尾 3 要素はホスト側の状態表示用の生値 (stcc4_state は読み出し失敗時 -1):
//     status2=0        → トリガ未処理/計測中
//     stcc4_state=6    → conditioning 実行中 (boot 後 ~22sec、stale が正常)
//     status1=0xFF     → 子機内で STCC4 通信全滅
// 応答が JP_REPLY_CAP を大きく超えるため、チャンクを USB へ逐次送信する。
static void ph_th_read_all(int32_t id, const char *json,
                           const jsmntok_t *t, int ntok, int params)
{
    (void)json; (void)t; (void)ntok; (void)params;

    char b[96];
    snprintf(b, sizeof(b), "{\"id\":%ld,\"ok\":true,\"vals\":[", (long)id);
    USB_Comm_SendString(b);

    for (uint8_t m = 0; m < TCA_MUX_COUNT; m++)
    {
        for (uint8_t ch = 0; ch < TCA_CH_PER_MUX; ch++)
        {
            const char *sep = (m == 0 && ch == 0) ? "" : ",";
            ThSample_t s;
            if (Tca_Select(m, ch)) ThProbe_ReadSample(&s);
            else                   s.i2c_ok = false;

            if (!s.i2c_ok)
            {
                snprintf(b, sizeof(b), "%snull", sep);
            }
            else
            {
                // stcc4_state も同スロットから読む (状態表示用。失敗は -1)
                uint8_t st4 = 0;
                int st4_out = ThProbe_ReadStcc4State(&st4) ? (int)st4 : -1;

                // 値ごとに stale なら null (例: [2531,null,612,4,1,0])
                char tv[8], hv[8], cv[8];
                if (s.t_valid)   snprintf(tv, sizeof(tv), "%d", (int)s.t_c100);
                else             snprintf(tv, sizeof(tv), "null");
                if (s.rh_valid)  snprintf(hv, sizeof(hv), "%u", (unsigned)s.rh_100);
                else             snprintf(hv, sizeof(hv), "null");
                if (s.co2_valid) snprintf(cv, sizeof(cv), "%u", (unsigned)s.co2_ppm);
                else             snprintf(cv, sizeof(cv), "null");
                snprintf(b, sizeof(b), "%s[%s,%s,%s,%u,%u,%d]", sep, tv, hv, cv,
                         (unsigned)s.status1, (unsigned)s.status2, st4_out);
            }
            USB_Comm_SendString(b);
        }
        (void)Tca_Deselect(m);
    }

    USB_Comm_SendString("]}\n");
    USB_Comm_Flush();
}

// FRC 一括開始 (frc_start): params {ppm} = 基準 CO2 濃度。
// 64 スロットを巡回し、応答した子機全てに FRC コマンドを発行する。
// 子機側は 30 sec 連続測定 → STCC4_performForcedRecalibration (~35 sec 所要)。
// 進捗/結果は frc_status でポーリングする。
// 応答: {"id":N,"ok":true,"ppm":P,"started":M,"accepted":"...64bit..."}
//   accepted[m*8+c] = '1' なら該当スロットがコマンドを受理 (ACK した)。
static void ph_frc_start(int32_t id, const char *json,
                         const jsmntok_t *t, int ntok, int params)
{
    int pt = (params >= 0) ? obj_get(json, t, ntok, params, "ppm") : -1;
    if (pt < 0) { reply_error(id, "missing_ppm"); return; }
    int32_t ppm = tok_int(json, &t[pt]);
    // 校正基準値の妥当範囲 (外気 ~420ppm、室内校正でも高々数千 ppm)
    if (ppm < 300 || ppm > 5000) { reply_error(id, "invalid_ppm"); return; }

    char accepted[TCA_MUX_COUNT * TCA_CH_PER_MUX + 1];
    unsigned started = 0;
    for (uint8_t m = 0; m < TCA_MUX_COUNT; m++)
    {
        for (uint8_t ch = 0; ch < TCA_CH_PER_MUX; ch++)
        {
            bool ok = Tca_Select(m, ch) && ThProbe_StartFrc((uint16_t)ppm);
            accepted[m * TCA_CH_PER_MUX + ch] = ok ? '1' : '0';
            if (ok) started++;
        }
        (void)Tca_Deselect(m);
    }
    accepted[TCA_MUX_COUNT * TCA_CH_PER_MUX] = '\0';

    char b[JP_REPLY_CAP];
    snprintf(b, sizeof(b),
        "{\"id\":%ld,\"ok\":true,\"ppm\":%ld,\"started\":%u,\"accepted\":\"%s\"}\n",
        (long)id, (long)ppm, started, accepted);
    send_line(b);
}

// FRC 状態一括取得 (frc_status): 64 スロットの stcc4_state と FRC 補正値を返す。
// 応答 (1 行):
//   {"id":N,"ok":true,"stat":[[state,corr],null,...]}
//   stat[m*8+c] = [stcc4_state, frc_correction]。子機不在は null。
//   state: 1=FRC実行中, 2=FRC完了, 3=FRC失敗, 6=conditioning中 (FRC は完了後に開始)
//   corr : FRC 補正値 [ppm signed]。FRC 完了後のみ有効 (読み出し失敗は null)。
static void ph_frc_status(int32_t id, const char *json,
                          const jsmntok_t *t, int ntok, int params)
{
    (void)json; (void)t; (void)ntok; (void)params;

    char b[64];
    snprintf(b, sizeof(b), "{\"id\":%ld,\"ok\":true,\"stat\":[", (long)id);
    USB_Comm_SendString(b);

    for (uint8_t m = 0; m < TCA_MUX_COUNT; m++)
    {
        for (uint8_t ch = 0; ch < TCA_CH_PER_MUX; ch++)
        {
            const char *sep = (m == 0 && ch == 0) ? "" : ",";
            uint8_t state = 0;
            if (!Tca_Select(m, ch) || !ThProbe_ReadStcc4State(&state))
            {
                snprintf(b, sizeof(b), "%snull", sep);
            }
            else
            {
                int16_t corr = 0;
                if (ThProbe_ReadFrcCorrection(&corr))
                    snprintf(b, sizeof(b), "%s[%u,%d]", sep,
                             (unsigned)state, (int)corr);
                else
                    snprintf(b, sizeof(b), "%s[%u,null]", sep, (unsigned)state);
            }
            USB_Comm_SendString(b);
        }
        (void)Tca_Deselect(m);
    }

    USB_Comm_SendString("]}\n");
    USB_Comm_Flush();
}

// 1 スロット診断 (th_debug): params {mux, ch} の子機から生レジスタを読んで返す。
//   status1     (0x28): per-value stale bitmask (0xFF = 全滅/boot 直後)
//   status2     (0x29): 0 = トリガ受理待ち or 計測中, 1 = サンプル READY
//   stcc4_state (0x6F): 0x06 = conditioning 実行中 (boot 後 ~22sec) 等
//   data_count  (0x06): 共通レジスタ仕様の有効値数 (th_sensor は 4)
// 「トリガしても値が stale のまま」の原因切り分け用:
//   status2 が 0 のまま → 子機がトリガを処理していない (main loop 停止等)
//   status2=1 で status1=0xFF → 計測はしたが子機内で STCC4 通信全滅
//   stcc4_state=6 → conditioning 中 (T/RH/CO2 は約 22 秒間 stale が正常)
static void ph_th_debug(int32_t id, const char *json,
                        const jsmntok_t *t, int ntok, int params)
{
    int32_t mux = -1, ch = -1;
    if (params >= 0)
    {
        int mt = obj_get(json, t, ntok, params, "mux");
        int ct = obj_get(json, t, ntok, params, "ch");
        if (mt >= 0) mux = tok_int(json, &t[mt]);
        if (ct >= 0) ch  = tok_int(json, &t[ct]);
    }
    if (mux < 0 || mux >= TCA_MUX_COUNT || ch < 0 || ch >= TCA_CH_PER_MUX)
    {
        reply_error(id, "invalid_mux_ch");
        return;
    }

    if (!Tca_Select((uint8_t)mux, (uint8_t)ch))
    {
        reply_error(id, "mux_select_failed");
        return;
    }

    uint8_t st12[2] = { 0, 0 };   // status1, status2
    uint8_t dc = 0, st4 = 0;
    uint8_t reg;
    bool ok1, ok2, ok3;
    reg = 0x28; ok1 = I2C_WriteRead(TH_PROBE_ADDRESS, &reg, 1, st12, 2);
    reg = 0x06; ok2 = I2C_WriteRead(TH_PROBE_ADDRESS, &reg, 1, &dc, 1);
    reg = 0x6F; ok3 = I2C_WriteRead(TH_PROBE_ADDRESS, &reg, 1, &st4, 1);
    (void)Tca_Deselect((uint8_t)mux);

    if (!ok1) { reply_error(id, "probe_no_response"); return; }

    char b[JP_REPLY_CAP];
    snprintf(b, sizeof(b),
        "{\"id\":%ld,\"ok\":true,\"status1\":%u,\"status2\":%u,"
        "\"data_count\":%d,\"stcc4_state\":%d}\n",
        (long)id, (unsigned)st12[0], (unsigned)st12[1],
        ok2 ? (int)dc : -1, ok3 ? (int)st4 : -1);
    send_line(b);
}

// 親バス直接スキャン (i2c_scan): mux select を介さず、親 I2C バス上の
// 0x08-0x77 全アドレスへの ACK 有無を調べる。TCA9548A 自体 (0x70-0x77 想定) や
// 想定外アドレスに座っているデバイスの発見に使う。
// 応答: {"id":N,"ok":true,"found":[112,113,...]}  (7bit アドレスの 10進配列)
static void ph_i2c_scan(int32_t id, const char *json,
                        const jsmntok_t *t, int ntok, int params)
{
    (void)json; (void)t; (void)ntok; (void)params;

    // 応答は最悪 112 アドレス × 4 文字 + 枠 ≈ 500B。バッチで組み立てる。
    char b[560];
    int pos = snprintf(b, sizeof(b), "{\"id\":%ld,\"ok\":true,\"found\":[", (long)id);
    bool first = true;
    for (uint8_t a = 0x08; a <= 0x77; a++)
    {
        if (!I2C_IsConnected(a)) continue;
        pos += snprintf(&b[pos], sizeof(b) - (size_t)pos, first ? "%u" : ",%u",
                        (unsigned)a);
        first = false;
        if (pos >= (int)sizeof(b) - 8) break; // 溢れそうなら打ち切り (異常事態)
    }
    snprintf(&b[pos], sizeof(b) - (size_t)pos, "]}\n");
    send_line(b);
}

// RST 配線テスト (rst_test): RST ピンを 1 本ずつ Low に落とし、その間に親バスの
// 0x70-0x77 への ACK を調べる。応答の matrix は 64 文字で、matrix[i*8+j] は
// 「RST(i+1) を Low にしている間の、アドレス 0x70+j の ACK 有無」。
// 正常配線なら RST(i+1) の行だけ 0x70+i が '0' になる (対角線が消える)。
//   ・どの行でも消えない列 → その RST がどのアドレスの mux にも繋がっていない
//   ・2 本の RST で同じ列が消える → その 2 個の mux がアドレス重複している
// 実行後は全 RST を High に戻す。
//
// params.mask (省略可): 指定時は 1 本ずつではなく、mask の bit i = RST(i+1) を
// 「全部同時に」Low へ落として 1 回だけスキャンする。アドレス重複した複数 chip を
// 同時にリセットして初めて消えるかを確認する用途 (重複の確定診断)。
// mask 指定時の応答: {"id":N,"ok":true,"mask":M,"present":"8文字"}
static void ph_rst_test(int32_t id, const char *json,
                        const jsmntok_t *t, int ntok, int params)
{
    // mask 指定モード
    int mt = (params >= 0) ? obj_get(json, t, ntok, params, "mask") : -1;
    if (mt >= 0)
    {
        int32_t mask = tok_int(json, &t[mt]);
        if (mask < 0 || mask > 0xFF) { reply_error(id, "invalid_mask"); return; }

        for (uint8_t m = 0; m < TCA_MUX_COUNT; m++)
            if (mask & (1 << m)) Tca_SetReset(m, true);

        char present[9];
        for (uint8_t j = 0; j < 8; j++)
            present[j] = I2C_IsConnected((uint8_t)(TCA_ADDR_BASE + j)) ? '1' : '0';
        present[8] = '\0';

        for (uint8_t m = 0; m < TCA_MUX_COUNT; m++)
            if (mask & (1 << m)) Tca_SetReset(m, false);

        char b[JP_REPLY_CAP];
        snprintf(b, sizeof(b),
            "{\"id\":%ld,\"ok\":true,\"mask\":%ld,\"present\":\"%s\"}\n",
            (long)id, (long)mask, present);
        send_line(b);
        return;
    }

    // 1 本ずつモード (既定)
    char matrix[TCA_MUX_COUNT * 8 + 1];
    for (uint8_t m = 0; m < TCA_MUX_COUNT; m++)
    {
        Tca_SetReset(m, true);   // RST(m+1) を Low
        for (uint8_t j = 0; j < 8; j++)
        {
            bool ok = I2C_IsConnected((uint8_t)(TCA_ADDR_BASE + j));
            matrix[m * 8 + j] = ok ? '1' : '0';
        }
        Tca_SetReset(m, false);  // High に戻す
    }
    matrix[TCA_MUX_COUNT * 8] = '\0';

    char b[160];
    snprintf(b, sizeof(b),
        "{\"id\":%ld,\"ok\":true,\"matrix\":\"%s\"}\n", (long)id, matrix);
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
    { "i2c_scan",      ph_i2c_scan      },
    { "rst_test",      ph_rst_test      },
    { "th_trigger",    ph_th_trigger    },
    { "th_read_all",   ph_th_read_all   },
    { "th_debug",      ph_th_debug      },
    { "frc_start",     ph_frc_start     },
    { "frc_status",    ph_frc_status    },
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
