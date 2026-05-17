using System;
using System.Xml.Serialization;
using System.IO.BACnet;

namespace MLServer.BACnet.Storage
{

  [Serializable]
  public class BACnetObject
  {
    [XmlAttribute]
    public BacnetObjectTypes Type { get; set; }

    [XmlAttribute]
    public uint Instance { get; set; }

    public BACnetProperty[] Properties { get; set; }

    public BACnetObject()
    {
      Properties = new BACnetProperty[0];
    }

  }
}
