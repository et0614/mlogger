using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.BACnet;
using System.IO.BACnet.Storage;
using System.Text;
using MLLib;

namespace MLServer.BACnet
{
  internal class MLServerDevice
  {
    // 子機 1 台あたりの BACnet オブジェクト Instance ID 体系:
    //   AI       1000+i : DBT (°C)
    //   AI       2000+i : GLB (°C)
    //   AI       3000+i : VEL (m/s)
    //   AI       4000+i : ILL (lux)
    //   AI       5000+i : RHM (%)
    //   AI       6000+i : MRT (°C)
    //   AI       7000+i : PMV (-)
    //   AI       8000+i : SET (-)
    //   AI       9000+i : WBGT_Indoor (°C)
    //   AI      10000+i : WBGT_Outdoor (°C)
    //   AI      11000+i : CO2 (ppm)
    //   AI      12000+i : PPD (%)
    //   DateTime 1000+i : DBRH 最終計測日時
    //   DateTime 2000+i : GLB  最終計測日時
    //   DateTime 3000+i : VEL  最終計測日時
    //   DateTime 4000+i : ILL  最終計測日時
    //   DateTime 5000+i : CO2  最終計測日時
    //
    // i = 子機発見順 (myLoggers 内 index)。i は再起動で変わり得るので 2次側
    // アプリ (BAS フロントエンド) は OBJECT_NAME に埋め込まれた LowAddress
    // でマッピングする方針 (Instance ID では追跡しない)。
    private const int MAX_LOGGERS = 1000;
    private const int BACNET_UNIT_DEGREES_C  = 62;
    private const int BACNET_UNIT_PERCENT    = 29;
    private const int BACNET_UNIT_METERS_PER_S = 161;
    private const int BACNET_UNIT_LUX        = 37;
    private const int BACNET_UNIT_PPM        = 96;
    private const int BACNET_UNIT_NO_UNITS   = 95;

    /// <summary>
    /// DATETIME を文字列で持つときの書式。ライブラリ (System.IO.BACnet.Storage.Property) が
    /// ParseExact("yyyy/MM/dd-HH:mm:ss.ff") で読むため、これに一致させる必要がある
    /// (bacnet-stack 互換。小数部はミリ秒ではなく 1/100 秒の 2 桁)。
    /// 一致しないと解析に失敗し、PRESENT_VALUE が null になる。
    /// 小数部は <see cref="FormatDateTime"/> で付加する。
    /// </summary>
    private const string DATE_TIME_FORMAT = "yyyy/MM/dd-HH:mm:ss.";

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    public BACnetCommunicator Communicator { get; set; }

    // 子機発見順リスト。addLogger / UpdateLogger は _lock で全部直列化する
    // (XBee 受信 callback が複数子機ぶん並行で発火し得るため)。
    private readonly List<ImmutableMLogger> myLoggers = new();
    private readonly object _lock = new();
    private bool _overflowReported = false;

    #endregion

    #region コンストラクタ

    public MLServerDevice(uint deviceId, int exclusivePort, string localEndPointAddress)
    {
      Communicator = new BACnetCommunicator(makeDeviceStorage(deviceId), exclusivePort, localEndPointAddress);
    }

    /// <summary>
    /// BACnet デバイスの初期オブジェクトをすべてコードで構築する。
    /// 配布ファイル (旧 MLServerDeviceStorage.xml) からの動的生成はユーザーに
    /// 書き換えられ得るため廃止。Device ID のみ setting.ini (bacdevid) で変更可能。
    /// </summary>
    private static DeviceStorage makeDeviceStorage(uint deviceId)
    {
      DeviceStorage storage = new DeviceStorage
      {
        DeviceId = deviceId,
        Objects = System.Array.Empty<System.IO.BACnet.Storage.Object>(),
      };

      // Device オブジェクト
      storage.AddObject(new System.IO.BACnet.Storage.Object
      {
        Instance = deviceId,
        Type = BacnetObjectTypes.OBJECT_DEVICE,
        Properties = new[]
        {
          MakeProp(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER,            BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, $"OBJECT_DEVICE:{deviceId}"),
          MakeProp(BacnetPropertyIds.PROP_OBJECT_NAME,                  BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "MLServer"),
          MakeProp(BacnetPropertyIds.PROP_OBJECT_TYPE,                  BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "8"),
          MakeProp(BacnetPropertyIds.PROP_SYSTEM_STATUS,                BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),  // 0 = Operational
          MakeProp(BacnetPropertyIds.PROP_VENDOR_NAME,                  BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "E. Togashi"),
          MakeProp(BacnetPropertyIds.PROP_VENDOR_IDENTIFIER,            BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "0"),  // 0 = ASHRAE。不詳の場合は 0 で良い
          MakeProp(BacnetPropertyIds.PROP_MODEL_NAME,                   BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "GNU"),
          MakeProp(BacnetPropertyIds.PROP_FIRMWARE_REVISION,            BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, Program.VERSION),
          MakeProp(BacnetPropertyIds.PROP_APPLICATION_SOFTWARE_VERSION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, Program.VERSION),
          MakeProp(BacnetPropertyIds.PROP_DESCRIPTION,                  BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "A BACnet device that communicates with the MLServer."),
          MakeProp(BacnetPropertyIds.PROP_PROTOCOL_VERSION,             BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "1"),
          MakeProp(BacnetPropertyIds.PROP_PROTOCOL_REVISION,            BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "14"),
          // SERVICES/OBJECT_TYPES_SUPPORTED と SEGMENTATION は BACnetCommunicator の
          // ReadOverride が実際の対応状況を動的に返すため、ここの値は placeholder
          MakeProp(BacnetPropertyIds.PROP_PROTOCOL_SERVICES_SUPPORTED,     BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "010001110000101111001000001010001010101000"),
          MakeProp(BacnetPropertyIds.PROP_PROTOCOL_OBJECT_TYPES_SUPPORTED, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000000010101010000000000000000100000000111110111111111"),
          new Property { Id = BacnetPropertyIds.PROP_OBJECT_LIST, Tag = BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID },  // ReadOverride が Storage から動的生成
          MakeProp(BacnetPropertyIds.PROP_MAX_APDU_LENGTH_ACCEPTED,     BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "1476"),
          MakeProp(BacnetPropertyIds.PROP_SEGMENTATION_SUPPORTED,       BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "3"),
          MakeProp(BacnetPropertyIds.PROP_APDU_TIMEOUT,                 BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "6000"),
          MakeProp(BacnetPropertyIds.PROP_NUMBER_OF_APDU_RETRIES,       BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "3"),
          new Property { Id = BacnetPropertyIds.PROP_DEVICE_ADDRESS_BINDING, Tag = BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL },
          MakeProp(BacnetPropertyIds.PROP_DATABASE_REVISION,            BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, "0"),
        }
      });

      //MLogger一覧を示す文字列 (LowAddress を CSV で並べる)
      storage.AddObject(new System.IO.BACnet.Storage.Object
      {
        Instance = 1u,
        Type = BacnetObjectTypes.OBJECT_CHARACTERSTRING_VALUE,
        Properties = new[]
        {
          MakeProp(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_CHARACTERSTRING_VALUE:1"),
          MakeProp(BacnetPropertyIds.PROP_OBJECT_NAME,        BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "MLoggerList"),
          MakeProp(BacnetPropertyIds.PROP_OBJECT_TYPE,        BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "40"),
          MakeProp(BacnetPropertyIds.PROP_DESCRIPTION,        BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "CSV of LowAddress for each connected MLogger. 2次側アプリは本 CSV と各 Object NAME 中の LowAddress でマッピングする。"),
          MakeProp(BacnetPropertyIds.PROP_PRESENT_VALUE,      BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, ""),
          MakeProp(BacnetPropertyIds.PROP_STATUS_FLAGS,       BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
        }
      });

      return storage;
    }

    #endregion

    public void UpdateLogger(ImmutableMLogger logger)
    {
      lock (_lock)
      {
        int indx;
        if (!myLoggers.Contains(logger))
        {
          if (myLoggers.Count >= MAX_LOGGERS)
          {
            if (!_overflowReported)
            {
              Console.WriteLine($"[BACnet] WARNING: MAX_LOGGERS ({MAX_LOGGERS}) reached. Subsequent devices will NOT be exposed via BACnet (CSV/JSON 出力は継続)。");
              _overflowReported = true;
            }
            return;
          }
          addLogger(logger);
        }
        indx = myLoggers.IndexOf(logger);

        // 計測時刻
        WriteDateTime(1000 + indx, logger.DrybulbTemperature.LastMeasureTime);
        WriteDateTime(2000 + indx, logger.GlobeTemperature.LastMeasureTime);
        WriteDateTime(3000 + indx, logger.Velocity.LastMeasureTime);
        WriteDateTime(4000 + indx, logger.Illuminance.LastMeasureTime);
        WriteDateTime(5000 + indx, logger.CO2Level.LastMeasureTime);

        // 計測値 / 計算値
        WriteAnalog(1000  + indx, (float)logger.DrybulbTemperature.LastValue);
        WriteAnalog(2000  + indx, (float)logger.GlobeTemperature.LastValue);
        WriteAnalog(3000  + indx, (float)logger.Velocity.LastValue);
        WriteAnalog(4000  + indx, (float)logger.Illuminance.LastValue);
        WriteAnalog(5000  + indx, (float)logger.RelativeHumdity.LastValue);
        WriteAnalog(6000  + indx, (float)logger.MeanRadiantTemperature);
        WriteAnalog(7000  + indx, (float)logger.PMV);
        WriteAnalog(8000  + indx, (float)logger.SETStar);
        WriteAnalog(9000  + indx, (float)logger.WBGT_Indoor);
        WriteAnalog(10000 + indx, (float)logger.WBGT_Outdoor);
        WriteAnalog(11000 + indx, (float)logger.CO2Level.LastValue);
        WriteAnalog(12000 + indx, (float)logger.PPD);
      }
    }

    private void WriteAnalog(int instance, float value)
    {
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)instance),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, value));
    }

    private void WriteDateTime(int instance, DateTime value)
    {
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)instance),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, value));
    }

    private void addLogger(ImmutableMLogger logger)
    {
      // 呼び出し側で _lock 取得済の前提。
      myLoggers.Add(logger);

      //機器一覧 (LowAddress の CSV) を更新
      StringBuilder sBuilder = new StringBuilder(myLoggers[0].LowAddress);
      for (int i = 1; i < myLoggers.Count; i++)
        sBuilder.Append("," + myLoggers[i].LowAddress);
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_CHARACTERSTRING_VALUE, 1u),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, sBuilder.ToString()));

      int indx = myLoggers.Count - 1;
      string lo = logger.LowAddress;
      string nm = logger.LocalName;

      // 計測時刻 (最終計測日時) 群
      AddDateTimeObject(1000 + indx, "DBRH", "dry-bulb temperature and relative humidity", lo, nm, logger.DrybulbTemperature.LastMeasureTime);
      AddDateTimeObject(2000 + indx, "GLB",  "globe temperature",                          lo, nm, logger.GlobeTemperature.LastMeasureTime);
      AddDateTimeObject(3000 + indx, "VEL",  "velocity",                                   lo, nm, logger.Velocity.LastMeasureTime);
      AddDateTimeObject(4000 + indx, "ILL",  "illuminance",                                lo, nm, logger.Illuminance.LastMeasureTime);
      AddDateTimeObject(5000 + indx, "CO2",  "CO2 concentration",                          lo, nm, logger.CO2Level.LastMeasureTime);

      // 計測値 (Analog Input) 群
      AddAnalogInputObject(1000  + indx, "DBT",        "current drybulb temperature",        lo, nm, (float)logger.DrybulbTemperature.LastValue, BACNET_UNIT_DEGREES_C);
      AddAnalogInputObject(2000  + indx, "GLB",        "current globe temperature",          lo, nm, (float)logger.GlobeTemperature.LastValue,  BACNET_UNIT_DEGREES_C);
      AddAnalogInputObject(3000  + indx, "VEL",        "current velocity",                   lo, nm, (float)logger.Velocity.LastValue,          BACNET_UNIT_METERS_PER_S);
      AddAnalogInputObject(4000  + indx, "ILL",        "current illuminance",                lo, nm, (float)logger.Illuminance.LastValue,       BACNET_UNIT_LUX);
      AddAnalogInputObject(5000  + indx, "RHM",        "current relative humidity",          lo, nm, (float)logger.RelativeHumdity.LastValue,   BACNET_UNIT_PERCENT);
      AddAnalogInputObject(6000  + indx, "MRT",        "current mean radiant temperature",   lo, nm, (float)logger.MeanRadiantTemperature,      BACNET_UNIT_DEGREES_C);
      AddAnalogInputObject(7000  + indx, "PMV",        "current PMV",                        lo, nm, (float)logger.PMV,                          BACNET_UNIT_NO_UNITS);
      AddAnalogInputObject(8000  + indx, "SET",        "current SET*",                       lo, nm, (float)logger.SETStar,                      BACNET_UNIT_NO_UNITS);
      AddAnalogInputObject(9000  + indx, "WBGT(IN)",   "current indoor WBGT",                lo, nm, (float)logger.WBGT_Indoor,                  BACNET_UNIT_DEGREES_C);
      AddAnalogInputObject(10000 + indx, "WBGT(OUT)",  "current outdoor WBGT",               lo, nm, (float)logger.WBGT_Outdoor,                 BACNET_UNIT_DEGREES_C);
      AddAnalogInputObject(11000 + indx, "CO2",        "current CO2 concentration",          lo, nm, (float)logger.CO2Level.LastValue,           BACNET_UNIT_PPM);
      AddAnalogInputObject(12000 + indx, "PPD",        "current PPD",                        lo, nm, (float)logger.PPD,                          BACNET_UNIT_PERCENT);
    }

    // NuGet 版 Property は (id, tag, value) コンストラクタが無いので
    // オブジェクト初期化子で同等の構築を行う小さなラッパ。
    private static Property MakeProp(BacnetPropertyIds id, BacnetApplicationTags tag, string value)
    {
      return new Property { Id = id, Tag = tag, Value = new[] { value } };
    }

    /// <summary>
    /// DATETIME を Property.SerializeValue と同一の表現にする。
    /// 小数部は 1/100 秒 2 桁なので、ミリ秒を 10 で割って付加する。
    /// </summary>
    private static string FormatDateTime(DateTime value)
    {
      return value.ToString(DATE_TIME_FORMAT, CultureInfo.InvariantCulture)
           + (value.Millisecond / 10).ToString("D2", CultureInfo.InvariantCulture);
    }

    /// <summary>Analog Input object をひとつ Storage に追加するヘルパ。</summary>
    private void AddAnalogInputObject(int instance, string nameTag, string descBody, string lowAddress, string localName, float initialValue, int unitEnum)
    {
      string tag = $"{lowAddress}({localName})";
      Communicator.Storage.AddObject(new System.IO.BACnet.Storage.Object
      {
        Instance = (uint)instance,
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new[]
        {
          MakeProp(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, $"OBJECT_ANALOG_INPUT:{instance}"),
          MakeProp(BacnetPropertyIds.PROP_OBJECT_NAME,       BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, $"{nameTag}_{tag}"),
          MakeProp(BacnetPropertyIds.PROP_OBJECT_TYPE,       BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          MakeProp(BacnetPropertyIds.PROP_DESCRIPTION,       BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, $"This object represents the {descBody} measured/calculated by {tag}"),
          MakeProp(BacnetPropertyIds.PROP_PRESENT_VALUE,     BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, initialValue.ToString("R", CultureInfo.InvariantCulture)),
          MakeProp(BacnetPropertyIds.PROP_STATUS_FLAGS,      BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          MakeProp(BacnetPropertyIds.PROP_OUT_OF_SERVICE,    BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          MakeProp(BacnetPropertyIds.PROP_RELIABILITY,       BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          MakeProp(BacnetPropertyIds.PROP_UNITS,             BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, unitEnum.ToString()),
        }
      });
    }

    /// <summary>DateTime Value object をひとつ Storage に追加するヘルパ。</summary>
    private void AddDateTimeObject(int instance, string nameTag, string descBody, string lowAddress, string localName, DateTime initialValue)
    {
      string tag = $"{lowAddress}({localName})";
      Communicator.Storage.AddObject(new System.IO.BACnet.Storage.Object
      {
        Instance = (uint)instance,
        Type = BacnetObjectTypes.OBJECT_DATETIME_VALUE,
        Properties = new[]
        {
          MakeProp(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, $"OBJECT_DATETIME_VALUE:{instance}"),
          MakeProp(BacnetPropertyIds.PROP_OBJECT_NAME,       BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, $"{nameTag}_LastMeasurementDate_{tag}"),
          MakeProp(BacnetPropertyIds.PROP_OBJECT_TYPE,       BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "44"),
          MakeProp(BacnetPropertyIds.PROP_DESCRIPTION,       BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, $"This object represents the date and time when {tag} last measured {descBody}."),
          MakeProp(BacnetPropertyIds.PROP_PRESENT_VALUE,     BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, FormatDateTime(initialValue)),
          MakeProp(BacnetPropertyIds.PROP_STATUS_FLAGS,      BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
        }
      });
    }

  }
}
