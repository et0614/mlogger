using System.Buffers.Binary;

namespace MLLib.Protocol;

/// <summary>
/// dump で得られる 22 byte/record の binary フォーマット <c>&lt;BIBIhhHHHH&gt;</c> を
/// 強い型のレコードに decode する。スケーリングを記録仕様 (firmware
/// <c>w25q256.h SensorData_t</c>) に従って戻す。
/// </summary>
public readonly record struct DumpRecord(
    byte           Generation,
    DateTimeOffset Timestamp,
    double?        DrybulbTemperature,  // °C
    double?        RelativeHumidity,    // %
    double?        GlobeTemperature,    // °C
    double?        Velocity,            // m/s
    double?        Illuminance,         // lx
    ushort?        VoltageMv,           // 風速推定用 ADC (mV 換算前の raw、参考値)
    int?           Co2Ppm);             // ppm

/// <summary>
/// dump バイナリストリーム → <see cref="DumpRecord"/> 列の decoder。
/// </summary>
public static class DumpDecoder
{
    // SensorData_t (22 bytes、packed、little-endian、format "<BIBIhhHHHH>")
    //   uint8  generation
    //   uint32 timestamp        (UNIX秒)
    //   uint8  valid_flags
    //   uint32 illuminance      (単位: lux * 10)
    //   int16  temp_dry         (単位: °C * 100)
    //   int16  temp_globe       (単位: °C * 100)
    //   uint16 humidity         (単位: % * 100)
    //   uint16 wind_speed       (単位: m/s * 10000)
    //   uint16 voltage          (単位: mV)
    //   uint16 co2_ppm          (単位: ppm)
    public const int RecordSize = 22;

    // valid_flags ビット (firmware w25q256.h と同期)
    private const byte FLAG_ILLUMINANCE = 1 << 0;
    private const byte FLAG_TEMP_DRY    = 1 << 1;
    private const byte FLAG_TEMP_GLOBE  = 1 << 2;
    private const byte FLAG_HUMIDITY    = 1 << 3;
    private const byte FLAG_WIND_SPEED  = 1 << 4;
    private const byte FLAG_VOLTAGE     = 1 << 5;
    private const byte FLAG_CO2_PPM     = 1 << 6;

    /// <summary>
    /// バイナリ列を decode して record を yield する。
    /// 端数 (recordSize 未満) は無視。
    /// </summary>
    public static IEnumerable<DumpRecord> Decode(ReadOnlyMemory<byte> data, int recordSize = RecordSize)
    {
        if (recordSize <= 0) yield break;
        int n = data.Length / recordSize;
        for (int i = 0; i < n; i++)
        {
            yield return DecodeOne(data.Slice(i * recordSize, recordSize).Span);
        }
    }

    /// <summary>1 record を decode。<paramref name="rec"/> は exactly <see cref="RecordSize"/> bytes。</summary>
    public static DumpRecord DecodeOne(ReadOnlySpan<byte> rec)
    {
        byte gen          = rec[0];
        uint ts           = BinaryPrimitives.ReadUInt32LittleEndian(rec.Slice(1, 4));
        byte flags        = rec[5];
        uint illRaw       = BinaryPrimitives.ReadUInt32LittleEndian(rec.Slice(6, 4));
        short tDryRaw     = BinaryPrimitives.ReadInt16LittleEndian (rec.Slice(10, 2));
        short tGlbRaw     = BinaryPrimitives.ReadInt16LittleEndian (rec.Slice(12, 2));
        ushort rhRaw      = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(14, 2));
        ushort velRaw     = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(16, 2));
        ushort voltRaw    = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(18, 2));
        ushort co2Raw     = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(20, 2));

        return new DumpRecord(
            Generation:         gen,
            Timestamp:          DateTimeOffset.FromUnixTimeSeconds(ts),
            DrybulbTemperature: (flags & FLAG_TEMP_DRY)    != 0 ? tDryRaw / 100.0    : null,
            RelativeHumidity:   (flags & FLAG_HUMIDITY)    != 0 ? rhRaw   / 100.0    : null,
            GlobeTemperature:   (flags & FLAG_TEMP_GLOBE)  != 0 ? tGlbRaw / 100.0    : null,
            Velocity:           (flags & FLAG_WIND_SPEED)  != 0 ? velRaw  / 10000.0  : null,
            Illuminance:        (flags & FLAG_ILLUMINANCE) != 0 ? illRaw  / 10.0     : null,
            VoltageMv:          (flags & FLAG_VOLTAGE)     != 0 ? voltRaw            : null,
            Co2Ppm:             (flags & FLAG_CO2_PPM)     != 0 ? co2Raw             : null);
    }
}
