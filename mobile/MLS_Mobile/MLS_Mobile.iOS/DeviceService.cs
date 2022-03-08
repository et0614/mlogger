using System;
using UIKit;
using Xamarin.Forms;
using MLS_Mobile.iOS.Services;
using MLS_Mobile.Services;

[assembly: Dependency(typeof(DeviceService))]
namespace MLS_Mobile.iOS.Services
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

    public string Paste()
    {
      var pb = UIPasteboard.General.GetValue("public.utf8-plain-text");
      return pb.ToString();
    }

    public void Copy(string title, string target)
    {
      UIPasteboard clipboard = UIPasteboard.General;
      clipboard.String = target;
    }
  }
}