using MLLib;
using MLS_Mobile.Resources.i18n;
using System.Text;

namespace MLS_Mobile;

public partial class AppShell : Shell
{

  public AppShell()
  {
    InitializeComponent();

    //必要に応じてデータディレクトリと設定ファイルを初期化する
    MLUtility.InitDirAndFiles();
    MLUtility.LoadMLNamesFile();

    //ルート登録
    Routing.RegisterRoute(nameof(ActivitySelector), typeof(ActivitySelector));
    Routing.RegisterRoute(nameof(ClothingCoordinator), typeof(ClothingCoordinator));
    Routing.RegisterRoute(nameof(LoggingData), typeof(LoggingData));
    Routing.RegisterRoute(nameof(DeviceSetting), typeof(DeviceSetting));
    Routing.RegisterRoute(nameof(CFSetting), typeof(CFSetting));
    Routing.RegisterRoute(nameof(DataReceive), typeof(DataReceive));
    Routing.RegisterRoute(nameof(Calibrator), typeof(Calibrator));
    Routing.RegisterRoute(nameof(VelocityCalibrator), typeof(VelocityCalibrator));
    Routing.RegisterRoute(nameof(RelayedDataViewer), typeof(RelayedDataViewer));
  }

  protected override async void OnNavigating(ShellNavigatingEventArgs args)
  {
    base.OnNavigating(args);

    //DataReceiveからpopするとき、計測を中止するか警告を発する
    if (args.Current != null && args.Current.Location.OriginalString.Contains(nameof(DataReceive)))
    {
      var currentpage = (App.Current.MainPage as AppShell).CurrentPage as DataReceive;
      if (currentpage is DataReceive && args.Source == ShellNavigationSource.Pop)
      {
        ShellNavigatingDeferral token = args.GetDeferral();
        var result = await DisplayActionSheet(MLSResource.DR_FinishAlert, MLSResource.Cancel, MLSResource.Yes);
        if (result == MLSResource.Yes) token.Complete();
        else args.Cancel();
      }
    }
    
    //RelayedDataViewerからpopするとき、Bluetoothを転送停止する
    if (args.Current != null && args.Current.Location.OriginalString.Contains(nameof(RelayedDataViewer)))
    {
      var currentpage = (App.Current.MainPage as AppShell).CurrentPage as RelayedDataViewer;
      if (currentpage is RelayedDataViewer && args.Source == ShellNavigationSource.PopToRoot)
      {
        ShellNavigatingDeferral token = args.GetDeferral();

        try
        {
          //Bluetooth転送停止コマンドを送信
          await Task.Run(() =>
          {
            MLUtility.Transceiver.HasStopRelayToBluetoothReceived = false;
            for (int i = 0; i < 10; i++)
            {
              if (MLUtility.Transceiver.HasStopRelayToBluetoothReceived) return;
              MLUtility.ConnectedXBee.SendSerialData(Encoding.ASCII.GetBytes(MLTransceiver.MakeStopRelayToBluetoothCommand()));
              Task.Delay(100);
            }
          });
        }
        catch (Exception ex)
        {
          await DisplayAlert("Alert", "Failed to stop bluetooth relay." + Environment.NewLine + ex.Message, "OK");
          args.Cancel();
          return;
        }
        if (MLUtility.Transceiver.HasStopRelayToBluetoothReceived) token.Complete();
        else
        {
          await DisplayAlert("Alert", "Failed to stop bluetooth relay.", "OK");
          args.Cancel();
          return;
        }
      }
    }
  }

}
