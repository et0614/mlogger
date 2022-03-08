using System;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Xamarin.Forms;
using MLS_Mobile.Droid.Services;
using MLS_Mobile.Services;

[assembly: Dependency(typeof(DeviceService))]
namespace MLS_Mobile.Droid.Services
{
  public class DeviceService : IDeviceService
  {
    private static PowerManager.WakeLock _wakeLock = null;

    /// <summary>
    /// スリープを無効にする
    /// </summary>
    public void DisableSleep()
    {
      PowerManager pm = (PowerManager)Forms.Context.GetSystemService(Context.PowerService);
      Context context = Forms.Context;    //Android.App.Application.Context;
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

    public string Paste()
    {
      ClipboardManager clipboard = (ClipboardManager)Forms.Context.GetSystemService(Context.ClipboardService);
      var item = clipboard.PrimaryClip.GetItemAt(0);
      return item.Text;
    }

    public void Copy(string title, string target)
    {
      ClipboardManager clipboard = (ClipboardManager)Forms.Context.GetSystemService(Context.ClipboardService);
      ClipData clip = ClipData.NewPlainText(target, target);
      clipboard.PrimaryClip = clip;
    }
  }
}