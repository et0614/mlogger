using Android.Views;
using System.Collections.Generic;

namespace MauiXAMLBluetoothLE;

public class BluetoothLEPermissions : Permissions.BasePlatformPermission
{
  public override (string androidPermission, bool isRuntime)[] RequiredPermissions
  {
    get
    {
      return getPermissions();
    }
  }

  private (string, bool)[] getPermissions()
  {
    List<(string, bool)> pmt = new List<(string androidPermission, bool isRuntime)>
    {
      (Android.Manifest.Permission.Bluetooth, true),
      (Android.Manifest.Permission.BluetoothAdmin, true),
      (Android.Manifest.Permission.AccessFineLocation, true),
      (Android.Manifest.Permission.AccessCoarseLocation, true),
    };

    if (OperatingSystem.IsAndroidVersionAtLeast(29))
      pmt.Add((Android.Manifest.Permission.AccessBackgroundLocation, true));

    if (OperatingSystem.IsAndroidVersionAtLeast(31))
    {
      pmt.Add((Android.Manifest.Permission.BluetoothScan, true));
      pmt.Add((Android.Manifest.Permission.BluetoothConnect, true));
    }

    return pmt.ToArray();
  }

}