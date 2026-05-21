using System;
using System.Collections.Generic;
using System.IO.BACnet;
using System.Text;
using MLLib;

using MLServer.BACnet.Storage;

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

    public MLServerDevice(int exclusivePort, string localEndPointAddress)
    {
      Communicator = new BACnetCommunicator(makeDeviceStorage(), exclusivePort, localEndPointAddress);
    }

    private DeviceStorage makeDeviceStorage()
    {
      DeviceStorage storage = DeviceStorage.Load("MLServerDeviceStorage.xml");

      //MLogger一覧を示す文字列 (LowAddress を CSV で並べる)
      storage.AddObject(new BACnetObject()
      {
        Instance = 1u,
        Type = BacnetObjectTypes.OBJECT_CHARACTERSTRING_VALUE,
        Properties = new BACnetProperty[]
        {
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_CHARACTERSTRING_VALUE:1"),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "MLoggerList"),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "40"),
            new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "CSV of LowAddress for each connected MLogger. 2次側アプリは本 CSV と各 Object NAME 中の LowAddress でマッピングする。"),
            new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, ""),
            new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
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

    /// <summary>Analog Input object をひとつ Storage に追加するヘルパ。</summary>
    private void AddAnalogInputObject(int instance, string nameTag, string descBody, string lowAddress, string localName, float initialValue, int unitEnum)
    {
      string tag = $"{lowAddress}({localName})";
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)instance,
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, $"OBJECT_ANALOG_INPUT:{instance}"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME,       BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, $"{nameTag}_{tag}"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE,       BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION,       BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, $"This object represents the {descBody} measured/calculated by {tag}"),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE,     BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, initialValue.ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS,      BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          new BACnetProperty(BacnetPropertyIds.PROP_OUT_OF_SERVICE,    BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          new BACnetProperty(BacnetPropertyIds.PROP_RELIABILITY,       BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_UNITS,             BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, unitEnum.ToString()),
        }
      });
    }

    /// <summary>DateTime Value object をひとつ Storage に追加するヘルパ。</summary>
    private void AddDateTimeObject(int instance, string nameTag, string descBody, string lowAddress, string localName, DateTime initialValue)
    {
      string tag = $"{lowAddress}({localName})";
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)instance,
        Type = BacnetObjectTypes.OBJECT_DATETIME_VALUE,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, $"OBJECT_DATETIME_VALUE:{instance}"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME,       BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, $"{nameTag}_LastMeasurementDate_{tag}"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE,       BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "44"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION,       BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, $"This object represents the date and time when {tag} last measured {descBody}."),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE,     BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, initialValue.ToString("yyyy/MM/dd HH:mm:ss")),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS,      BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
        }
      });
    }

  }
}
