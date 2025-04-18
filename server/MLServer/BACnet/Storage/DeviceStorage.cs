﻿using System.Collections.Generic;
using System.Linq;

using System.Xml.Serialization;
using System.Reflection;

using System.IO.BACnet;
using System.IO.BACnet.Serialize;
using System.IO;
using System;

namespace MLServer.BACnet.Storage
{

  /// <summary>
  /// This is a basic example of a BACNet storage. This one is XML based. It has no fancy optimizing or anything.
  /// </summary>
  [Serializable]
  public class DeviceStorage
  {
    [XmlIgnore]
    public uint DeviceId { get; set; }

    public delegate void ChangeOfValueHandler(DeviceStorage sender, BacnetObjectId objectId, BacnetPropertyIds propertyId, uint arrayIndex, IList<BacnetValue> value);
    public event ChangeOfValueHandler ChangeOfValue;
    public delegate void ReadOverrideHandler(BacnetObjectId objectId, BacnetPropertyIds propertyId, uint arrayIndex, out IList<BacnetValue> value, out ErrorCodes status, out bool handled);
    public event ReadOverrideHandler ReadOverride;
    public delegate void WriteOverrideHandler(BacnetObjectId objectId, BacnetPropertyIds propertyId, uint arrayIndex, IList<BacnetValue> value, out ErrorCodes status, out bool handled);
    public event WriteOverrideHandler WriteOverride;

    public BACnetObject[] Objects { get; set; }

    public DeviceStorage()
    {
      DeviceId = (uint)new Random().Next();
      Objects = new BACnetObject[0];
    }

    public BACnetProperty FindProperty(BacnetObjectId objectId, BacnetPropertyIds propertyId)
    {
      //liniear search
      var obj = FindObject(objectId);
      return FindProperty(obj, propertyId);
    }

    private static BACnetProperty FindProperty(BACnetObject obj, BacnetPropertyIds propertyId)
    {
      //liniear search
      return obj?.Properties.FirstOrDefault(p => p.Id == propertyId);
    }

    private BACnetObject FindObject(BacnetObjectTypes objectType)
    {
      //liniear search
      return Objects.FirstOrDefault(obj => obj.Type == objectType);
    }

    public BACnetObject FindObject(BacnetObjectId objectId)
    {
      //liniear search
      return Objects.FirstOrDefault(obj => obj.Type == objectId.type && obj.Instance == objectId.instance);
    }

    public enum ErrorCodes
    {
      Good = 0,
      GenericError = -1,
      NotExist = -2,
      NotForMe = -3,
      WriteAccessDenied = -4,
      UnknownObject = -5,
      UnknownProperty = -6
    }

    public int ReadPropertyValue(BacnetObjectId objectId, BacnetPropertyIds propertyId)
    {
      if (ReadProperty(objectId, propertyId, ASN1.BACNET_ARRAY_ALL, out IList<BacnetValue> value) != ErrorCodes.Good)
        return 0;

      if (value == null || value.Count < 1)
        return 0;

      return (int)Convert.ChangeType(value[0].Value, typeof(int));
    }

    #region 追加メソッド

    public object ReadPresentValue(BacnetObjectId objectId)
    {
      if (ReadProperty(objectId, BacnetPropertyIds.PROP_PRESENT_VALUE, ASN1.BACNET_ARRAY_ALL, out IList<BacnetValue> value) != ErrorCodes.Good)
        return 0;

      if (value == null || value.Count < 1)
        return null;

      return value[0].Value;
    }

    #endregion

    public ErrorCodes ReadProperty(BacnetObjectId objectId, BacnetPropertyIds propertyId, uint arrayIndex, out IList<BacnetValue> value)
    {
      value = new BacnetValue[0];

      //wildcard device_id
      if (objectId.type == BacnetObjectTypes.OBJECT_DEVICE && objectId.instance >= ASN1.BACNET_MAX_INSTANCE)
        objectId.instance = DeviceId;

      //overrides
      if (ReadOverride != null)
      {
        ReadOverride(objectId, propertyId, arrayIndex, out value, out ErrorCodes status, out bool handled);
        if (handled)
          return status;
      }

      //find in storage
      var obj = FindObject(objectId);
      if (obj == null)
        return ErrorCodes.UnknownObject;

      //object found now find property
      var p = FindProperty(objectId, propertyId);
      if (p == null)
        return ErrorCodes.NotExist;

      //get value ... check for array index
      if (arrayIndex == 0)
      {
        value = new[] { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_UNSIGNED_INT, (uint)p.BacnetValue.Count) };
      }
      else if (arrayIndex != ASN1.BACNET_ARRAY_ALL)
      {
        value = new[] { p.BacnetValue[(int)arrayIndex - 1] };
      }
      else
      {
        value = p.BacnetValue;
      }

      return ErrorCodes.Good;
    }

    public void ReadPropertyMultiple(BacnetObjectId objectId, ICollection<BacnetPropertyReference> properties, out IList<BacnetPropertyValue> values)
    {
      var valuesRet = new List<BacnetPropertyValue>();

      foreach (var entry in properties)
      {
        var newEntry = new BacnetPropertyValue { property = entry };

        switch (ReadProperty(objectId, (BacnetPropertyIds)entry.propertyIdentifier, entry.propertyArrayIndex, out newEntry.value))
        {
          case ErrorCodes.UnknownObject:
            newEntry.value = new[]
            {
                            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ERROR,
                            new BacnetError(BacnetErrorClasses.ERROR_CLASS_OBJECT, BacnetErrorCodes.ERROR_CODE_UNKNOWN_OBJECT))
                        };
            break;
          case ErrorCodes.NotExist:
            newEntry.value = new[]
            {
                            new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ERROR,
                            new BacnetError(BacnetErrorClasses.ERROR_CLASS_PROPERTY, BacnetErrorCodes.ERROR_CODE_UNKNOWN_PROPERTY))
                        };
            break;
        }

        valuesRet.Add(newEntry);
      }

      values = valuesRet;
    }

    public bool ReadPropertyAll(BacnetObjectId objectId, out IList<BacnetPropertyValue> values)
    {
      //find
      var obj = FindObject(objectId);
      if (obj == null)
      {
        values = null;
        return false;
      }

      //build
      var propertyValues = new BacnetPropertyValue[obj.Properties.Length];
      for (var i = 0; i < obj.Properties.Length; i++)
      {
        var newEntry = new BacnetPropertyValue
        {
          property = new BacnetPropertyReference((uint)obj.Properties[i].Id, ASN1.BACNET_ARRAY_ALL)
        };

        if (ReadProperty(objectId, obj.Properties[i].Id, ASN1.BACNET_ARRAY_ALL, out newEntry.value) != ErrorCodes.Good)
        {
          var bacnetError = new BacnetError(BacnetErrorClasses.ERROR_CLASS_OBJECT, BacnetErrorCodes.ERROR_CODE_UNKNOWN_PROPERTY);
          newEntry.value = new[] { new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_ERROR, bacnetError) };
        }

        propertyValues[i] = newEntry;
      }

      values = propertyValues;
      return true;
    }

    public void WritePropertyValue(BacnetObjectId objectId, BacnetPropertyIds propertyId, int value)
    {
      //get existing type
      if (ReadProperty(objectId, propertyId, ASN1.BACNET_ARRAY_ALL, out IList<BacnetValue> readValues) != ErrorCodes.Good)
        return;

      if (readValues == null || readValues.Count == 0)
        return;

      //write
      WriteProperty(objectId, propertyId, ASN1.BACNET_ARRAY_ALL, new[]
      {
      new BacnetValue(readValues[0].Tag, Convert.ChangeType(value, readValues[0].Value.GetType()))
    });
    }


    public void WriteProperty(BacnetObjectId objectId, BacnetPropertyIds propertyId, BacnetValue value)
    {
      WriteProperty(objectId, propertyId, ASN1.BACNET_ARRAY_ALL, new[] { value });
    }

    public ErrorCodes WriteProperty(BacnetObjectId objectId, BacnetPropertyIds propertyId, uint arrayIndex, IList<BacnetValue> value, bool addIfNotExits = false)
    {
      //wildcard device_id
      if (objectId.type == BacnetObjectTypes.OBJECT_DEVICE && objectId.instance >= ASN1.BACNET_MAX_INSTANCE)
        objectId.instance = DeviceId;

      //overrides
      if (WriteOverride != null)
      {
        WriteOverride(objectId, propertyId, arrayIndex, value, out ErrorCodes status, out bool handled);
        if (handled)
          return status;
      }

      //find
      var p = FindProperty(objectId, propertyId);
      if (p == null)
      {
        if (!addIfNotExits) return ErrorCodes.NotExist;

        //add obj
        var obj = FindObject(objectId);
        if (obj == null)
        {
          obj = new BACnetObject
          {
            Type = objectId.type,
            Instance = objectId.instance
          };
          var arr = Objects;
          Array.Resize(ref arr, arr.Length + 1);
          arr[arr.Length - 1] = obj;
          Objects = arr;
        }

        //add property
        p = new BACnetProperty { Id = propertyId };
        var props = obj.Properties;
        Array.Resize(ref props, props.Length + 1);
        props[props.Length - 1] = p;
        obj.Properties = props;
      }

      //set type if needed
      if (p.Tag == BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL && value != null)
      {
        foreach (var v in value)
        {
          if (v.Tag == BacnetApplicationTags.BACNET_APPLICATION_TAG_NULL)
            continue;

          p.Tag = v.Tag;
          break;
        }
      }

      //値が変化した場合のみCOV通知が発生するように修正
      bool hasChanged = false;
      if (p.BacnetValue.Count != value.Count) hasChanged = true;
      else
      {
        for (int i = 0; i < value.Count; i++)
        {
          if (p.BacnetValue[i].Tag != value[i].Tag || !p.BacnetValue[i].Value.Equals(value[i].Value))
          {
            hasChanged = true;
            break;
          }
        }
      }

      if (hasChanged)
      {
        //write
        p.BacnetValue = value;

        //send event ... for subscriptions
        ChangeOfValue?.Invoke(this, objectId, propertyId, arrayIndex, value);
      }

      return ErrorCodes.Good;
    }

    // Write PROP_PRESENT_VALUE or PROP_RELINQUISH_DEFAULT in an object with a 16 level PROP_PRIORITY_ARRAY (BACNET_APPLICATION_TAG_NULL)
    public ErrorCodes WriteCommandableProperty(BacnetObjectId objectId, BacnetPropertyIds propertyId, BacnetValue value, uint priority)
    {

      if (propertyId != BacnetPropertyIds.PROP_PRESENT_VALUE)
        return ErrorCodes.NotForMe;

      var presentvalue = FindProperty(objectId, BacnetPropertyIds.PROP_PRESENT_VALUE);
      if (presentvalue == null)
        return ErrorCodes.NotForMe;

      var relinquish = FindProperty(objectId, BacnetPropertyIds.PROP_RELINQUISH_DEFAULT);
      if (relinquish == null)
        return ErrorCodes.NotForMe;

      var outOfService = FindProperty(objectId, BacnetPropertyIds.PROP_OUT_OF_SERVICE);
      if (outOfService == null)
        return ErrorCodes.NotForMe;

      var array = FindProperty(objectId, BacnetPropertyIds.PROP_PRIORITY_ARRAY);
      if (array == null)
        return ErrorCodes.NotForMe;

      var errorcode = ErrorCodes.GenericError;

      try
      {
        // If PROP_OUT_OF_SERVICE=True, value is accepted as is : http://www.bacnetwiki.com/wiki/index.php?title=Priority_Array                 
        if ((bool)outOfService.BacnetValue[0].Value && propertyId == BacnetPropertyIds.PROP_PRESENT_VALUE)
        {
          WriteProperty(objectId, BacnetPropertyIds.PROP_PRESENT_VALUE, value);
          return ErrorCodes.Good;
        }

        IList<BacnetValue> valueArray = null;

        // Thank's to Steve Karg
        // The 135-2016 text:
        // 19.2.2 Application Priority Assignments
        // All commandable objects within a device shall be configurable to accept writes to all priorities except priority 6
        if (priority == 6)
          return ErrorCodes.WriteAccessDenied;

        // http://www.chipkin.com/changing-the-bacnet-present-value-or-why-the-present-value-doesn%E2%80%99t-change/
        // Write Property PROP_PRESENT_VALUE : A value is placed in the PROP_PRIORITY_ARRAY
        if (propertyId == BacnetPropertyIds.PROP_PRESENT_VALUE)
        {
          errorcode = ErrorCodes.Good;

          valueArray = array.BacnetValue;
          if (value.Value == null)
            valueArray[(int)priority - 1] = new BacnetValue(null);
          else
            valueArray[(int)priority - 1] = value;
          array.BacnetValue = valueArray;
        }

        // Look on the priority Array to find the first value to be set in PROP_PRESENT_VALUE
        if (errorcode == ErrorCodes.Good)
        {

          var done = false;
          for (var i = 0; i < 16; i++)
          {
            if (valueArray[i].Value == null)
              continue;

            WriteProperty(objectId, BacnetPropertyIds.PROP_PRESENT_VALUE, valueArray[i]);
            done = true;
            break;
          }

          if (done == false)  // Nothing in the array : PROP_PRESENT_VALUE = PROP_RELINQUISH_DEFAULT
          {
            var defaultValue = relinquish.BacnetValue;
            WriteProperty(objectId, BacnetPropertyIds.PROP_PRESENT_VALUE, defaultValue[0]);
          }
        }
      }
      catch
      {
        errorcode = ErrorCodes.GenericError;
      }

      return errorcode;
    }

    public ErrorCodes[] WritePropertyMultiple(BacnetObjectId objectId, ICollection<BacnetPropertyValue> values)
    {
      return values
          .Select(v => WriteProperty(objectId, (BacnetPropertyIds)v.property.propertyIdentifier, v.property.propertyArrayIndex, v.value))
          .ToArray();
    }

    /// <summary>
    /// Store the class, as XML file
    /// </summary>
    /// <param name="path"></param>
    public void Save(string path)
    {
      var s = new XmlSerializer(typeof(DeviceStorage));
      using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
      s.Serialize(fs, this);
    }

    /// <summary>
    /// Load XML values into class
    /// </summary>
    /// <param name="path">Embedded or external file</param>
    /// <param name="deviceId">Optional deviceId other than the one in the Xml file</param>
    /// <returns></returns>
    public static DeviceStorage Load(string path, uint? deviceId = null)
    {
      StreamReader textStreamReader;

      var assembly = Assembly.GetCallingAssembly();

      try
      {
        // check if the xml file is an embedded resource
        textStreamReader = new StreamReader(assembly.GetManifestResourceStream(path));
      }
      catch
      {
        // if not check the external file
        if (!File.Exists(path))
          throw new Exception("No AppSettings found");

        textStreamReader = new StreamReader(path);
      }

      return Load(textStreamReader, deviceId);
    }

    #region 追加処理

    public static DeviceStorage Load(StreamReader textStreamReader, uint? deviceId = null)
    {
      var s = new XmlSerializer(typeof(DeviceStorage));

      using (textStreamReader)
      {
        var ret = (DeviceStorage)s.Deserialize(textStreamReader);

        //set device_id
        var obj = ret.FindObject(BacnetObjectTypes.OBJECT_DEVICE);
        if (obj != null)
          ret.DeviceId = obj.Instance;

        // use the deviceId in the Xml file or another one
        if (!deviceId.HasValue)
          return ret;

        ret.DeviceId = deviceId.Value;
        if (obj == null)
          return ret;

        // change the value
        obj.Instance = deviceId.Value;
        IList<BacnetValue> val = new[]
        {
        new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_OBJECT_ID, $"OBJECT_DEVICE:{deviceId.Value}")
      };

        ret.WriteProperty(new BacnetObjectId(BacnetObjectTypes.OBJECT_DEVICE,
          ASN1.BACNET_MAX_INSTANCE), BacnetPropertyIds.PROP_OBJECT_IDENTIFIER, 1, val, true);

        return ret;
      }
    }

    public void AddObject(BACnetObject obj)
    {
      //あまり美しくないが・・・
      BACnetObject[] newObjs = new BACnetObject[Objects.Length + 1];
      Array.Copy(Objects, newObjs, Objects.Length);
      newObjs[newObjs.Length - 1] = obj;
      Objects = newObjs;
    }

    #endregion

  }

}
