using MLS_Mobile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIKit;

[assembly: Dependency(typeof(DeviceService))]
namespace MLS_Mobile
{
  public class DeviceService : IDeviceService
  {
    /// <summary>
    /// スリープを無効にする
    /// </summary>
    public void DisableSleep()
    {
      UIApplication.SharedApplication.IdleTimerDisabled = true;
    }

    /// <summary>
    /// スリープを有効にする
    /// </summary>
    public void EnableSleep()
    {
      UIApplication.SharedApplication.IdleTimerDisabled = false;
    }
  }
}
