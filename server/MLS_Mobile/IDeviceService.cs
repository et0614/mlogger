using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MLS_Mobile
{
  public interface IDeviceService
  {
    //スリープを無効にする
    void DisableSleep();

    //スリープを有効にする
    void EnableSleep();
  }
}
