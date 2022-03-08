using System;
using System.Collections.Generic;
using System.Text;

namespace MLS_Mobile.Services
{  
  public interface IDeviceService
  {
    //スリープを無効にする
    void DisableSleep();

    //スリープを有効にする
    void EnableSleep();

    string Paste();

    void Copy(string title, string target);
  }
}
