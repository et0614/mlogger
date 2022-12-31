using MLS_Mobile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Android.App;
using Android.Content;
using Android.OS;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

using Android.Views;
using Android.Runtime;

[assembly: Dependency(typeof(DeviceService))]
namespace MLS_Mobile
{
  public class DeviceService : IDeviceService
  {
    private static PowerManager.WakeLock _wakeLock = null;

    /// <summary>
    /// スリープを無効にする
    /// </summary>
    public void DisableSleep()
    {
      Context context = Android.App.Application.Context;
      PowerManager pm = (PowerManager)context.GetSystemService(Context.PowerService);
      var packageName = context.PackageManager.GetPackageInfo(context.PackageName, 0).PackageName;
      _wakeLock = pm.NewWakeLock(WakeLockFlags.Full, packageName);
      _wakeLock.Acquire();
    }

    /// <summary>
    /// スリープを有効にする
    /// </summary>
    public void EnableSleep()
    {
      if (_wakeLock != null)
      {
        _wakeLock.Release();
        _wakeLock = null;
      }
    }
  }
}
