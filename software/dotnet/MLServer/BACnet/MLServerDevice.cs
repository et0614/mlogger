using System.Collections.Generic;
using System.IO.BACnet;
using System.Text;
using MLLib;

using MLServer.BACnet.Storage;

namespace MLServer.BACnet
{

    internal class MLServerDevice
  {

    #region インスタンス変数・プロパティ

    /// <summary>BACnet通信用オブジェクト</summary>
    public BACnetCommunicator Communicator { get; set; }

    private List<ImmutableMLogger> myLoggers = new List<ImmutableMLogger>();

    #endregion

    #region コンストラクタ

    public MLServerDevice(int exclusivePort, string localEndPointAddress)
    {
      Communicator = new BACnetCommunicator(makeDeviceStorage(), exclusivePort, localEndPointAddress);
    }

    private DeviceStorage makeDeviceStorage()
    {
      DeviceStorage storage = DeviceStorage.Load("MLServerDeviceStorage.xml");

      //MLogger一覧を示す文字列
      storage.AddObject(new BACnetObject()
      {
        Instance = 1u,
        Type = BacnetObjectTypes.OBJECT_CHARACTERSTRING_VALUE,
        Properties = new BACnetProperty[]
        {
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_CHARACTERSTRING_VALUE:" + 1),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "MLoggerList"),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "40"),
            new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the name (XBee low address) of the connected MLogger in a CSV format string."),
            new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, ""),
            new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          }
      });

      return storage;
    }

    #endregion

    public void UpdateLogger(ImmutableMLogger logger) 
    {
      //新出の場合
      if (!myLoggers.Contains(logger))
        addLogger(logger);

      int indx = myLoggers.IndexOf(logger);
      if (1000 <= indx) return; //1000台まで

      //温湿度の最終計測日時
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)(1000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, logger.DrybulbTemperature.LastMeasureTime)
        );

      //グローブ温度の最終計測日時
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)(2000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, logger.GlobeTemperature.LastMeasureTime)
        );

      //風速の最終計測日時
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)(3000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, logger.Velocity.LastMeasureTime)
        );

      //照度の最終計測日時
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_DATETIME_VALUE, (uint)(4000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, logger.Illuminance.LastMeasureTime)
        );

      //乾球温度の現在値
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(1000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)logger.DrybulbTemperature.LastValue)
        );

      //相対湿度の現在値
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(5000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)logger.RelativeHumdity.LastValue)
        );

      //グローブ温度の現在値
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(2000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)logger.GlobeTemperature.LastValue)
        );

      //風速の現在値
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(3000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)logger.Velocity.LastValue)
        );

      //照度の現在値
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(4000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)logger.Illuminance.LastValue)
        );

      //MRTの現在値
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(6000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)logger.MeanRadiantTemperature)
        );

      //PMVの現在値
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(7000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)logger.PMV)
        );

      //SET*の現在値
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(8000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)logger.SETStar)
        );

      //WBGT(Indoor)の現在値
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(9000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)logger.WBGT_Indoor)
        );

      //WBGT(Outdoor)の現在値
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_INPUT, (uint)(10000 + indx)),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, (float)logger.WBGT_Outdoor)
        );
    }

    private void addLogger(ImmutableMLogger logger)
    {
      //リストに追加
      myLoggers.Add(logger);

      //機器一覧を更新
      StringBuilder sBuilder = new StringBuilder(myLoggers[0].LowAddress);
      for (int i = 1; i < myLoggers.Count; i++)
        sBuilder.Append("," + myLoggers[i].LowAddress);
      Communicator.Storage.WriteProperty(
        new BacnetObjectId(BacnetObjectTypes.OBJECT_CHARACTERSTRING_VALUE, 1u),
        BacnetPropertyIds.PROP_PRESENT_VALUE,
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, sBuilder.ToString())
        );

      //以下、オブジェクトの追加処理
      int indx = myLoggers.IndexOf(logger);
      if (1000 <= indx) return; //1000台まで

      //温湿度の最終計測日時
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(1000 + indx),
        Type = BacnetObjectTypes.OBJECT_DATETIME_VALUE,
        Properties = new BACnetProperty[]
        {
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_DATETIME_VALUE:" + (1000 + indx).ToString()),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "DBRH_LastMeasurementDate_" + logger.LowAddress + "(" + logger.LocalName + ")"),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "44"),
            new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the date and time when " + logger.LowAddress + "(" + logger.LocalName + ")" + " last measured dry-bulb temperature and relative humidity."),
            new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, logger.DrybulbTemperature.LastMeasureTime.ToString("yyyy/MM/dd HH:mm:ss")),
            new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          }
      });

      //グローブ温度の最終計測日時
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(2000 + indx),
        Type = BacnetObjectTypes.OBJECT_DATETIME_VALUE,
        Properties = new BACnetProperty[]
        {
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_DATETIME_VALUE:" + (2000 + indx).ToString()),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "GLB_LastMeasurementDate_" + logger.LowAddress + "(" + logger.LocalName + ")"),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "44"),
            new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the date and time when " + logger.LowAddress + "(" + logger.LocalName + ")" + " last measured globe temperature."),
            new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, logger.GlobeTemperature.LastMeasureTime.ToString("yyyy/MM/dd HH:mm:ss")),
            new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          }
      });

      //風速の最終計測日時
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(3000 + indx),
        Type = BacnetObjectTypes.OBJECT_DATETIME_VALUE,
        Properties = new BACnetProperty[]
        {
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_DATETIME_VALUE:" + (3000 + indx).ToString()),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "VEL_LastMeasurementDate_" + logger.LowAddress + "(" + logger.LocalName + ")"),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "44"),
            new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the date and time when " + logger.LowAddress + "(" + logger.LocalName + ")" + " last measured velocity."),
            new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, logger.Velocity.LastMeasureTime.ToString("yyyy/MM/dd HH:mm:ss")),
            new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          }
      });

      //照度の最終計測日時
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(4000 + indx),
        Type = BacnetObjectTypes.OBJECT_DATETIME_VALUE,
        Properties = new BACnetProperty[]
        {
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_DATETIME_VALUE:" + (4000 + indx).ToString()),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "ILL_LastMeasurementDate_" + logger.LowAddress + "(" + logger.LocalName + ")"),
            new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "44"),
            new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the date and time when " + logger.LowAddress + "(" + logger.LocalName + ")" + " last measured illuminance."),
            new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_DATETIME, logger.Illuminance.LastMeasureTime.ToString("yyyy/MM/dd HH:mm:ss")),
            new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          }
      });

      //乾球温度の現在値
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(1000 + indx),
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (1000 + indx).ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "DBT_" + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the current drybulb temperature measured by " + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, logger.DrybulbTemperature.LastValue.ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          new BACnetProperty(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          new BACnetProperty(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"), //C
          }
      });

      //相対湿度の現在値
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(5000 + indx),
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (5000 + indx).ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "RHM_" + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the current relative humidity measured by " + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, logger.RelativeHumdity.LastValue.ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          new BACnetProperty(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          new BACnetProperty(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "29"), //%
          }
      });

      //グローブ温度の現在値
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(2000 + indx),
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (2000 + indx).ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "GLB_" + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the current globe temperature measured by " + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, logger.GlobeTemperature.LastValue.ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          new BACnetProperty(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          new BACnetProperty(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"), //C
          }
      });

      //風速の現在値
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(3000 + indx),
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (3000 + indx).ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "VEL_" + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the current velocity measured by " + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, logger.Velocity.LastValue.ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          new BACnetProperty(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          new BACnetProperty(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "161"), //m/s
          }
      });

      //照度の現在値
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(4000 + indx),
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (4000 + indx).ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "ILL_" + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the current illuminance measured by " + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, logger.Illuminance.LastValue.ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          new BACnetProperty(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          new BACnetProperty(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "37"), //lux
          }
      });

      //MRTの現在値
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(6000 + indx),
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (6000 + indx).ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "MRT_" + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the current mean radiant temperature calculated by " + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, logger.MeanRadiantTemperature.ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          new BACnetProperty(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          new BACnetProperty(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"), //C
          }
      });

      //PMVの現在値
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(7000 + indx),
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (7000 + indx).ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "PMV_" + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the current PMV calculated by " + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, logger.MeanRadiantTemperature.ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          new BACnetProperty(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          new BACnetProperty(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "95"), //No units
          }
      });

      //SET*の現在値
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(8000 + indx),
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (8000 + indx).ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "SET_" + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the current SET* calculated by " + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, logger.MeanRadiantTemperature.ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          new BACnetProperty(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          new BACnetProperty(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "95"), //No units
          }
      });

      //WBGTの現在値
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(9000 + indx),
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (9000 + indx).ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "WBGT(IN)_" + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the current indoor WBGT calculated by " + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, logger.WBGT_Indoor.ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          new BACnetProperty(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          new BACnetProperty(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"), //C
          }
      });

      //MRTの現在値
      Communicator.Storage.AddObject(new BACnetObject()
      {
        Instance = (uint)(10000 + indx),
        Type = BacnetObjectTypes.OBJECT_ANALOG_INPUT,
        Properties = new BACnetProperty[]
        {
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, "OBJECT_ANALOG_INPUT:" + (10000 + indx).ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_NAME, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "WBGT(OUT)_" + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_OBJECT_TYPE, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_DESCRIPTION, BacnetApplicationTags.BACNET_APPLICATION_TAG_CHARACTER_STRING, "This object represents the current outdoor WBGT calculated by " + logger.LowAddress + "(" + logger.LocalName + ")"),
          new BACnetProperty(BacnetPropertyIds.PROP_PRESENT_VALUE, BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, logger.MeanRadiantTemperature.ToString()),
          new BACnetProperty(BacnetPropertyIds.PROP_STATUS_FLAGS, BacnetApplicationTags.BACNET_APPLICATION_TAG_BIT_STRING, "0000"),
          new BACnetProperty(BacnetPropertyIds.PROP_OUT_OF_SERVICE, BacnetApplicationTags.BACNET_APPLICATION_TAG_BOOLEAN, "False"),
          new BACnetProperty(BacnetPropertyIds.PROP_RELIABILITY, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "0"),
          new BACnetProperty(BacnetPropertyIds.PROP_UNITS, BacnetApplicationTags.BACNET_APPLICATION_TAG_ENUMERATED, "62"), //C
          }
      });
    }

  }
}
