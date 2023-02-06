using MLLib;
using MLS_Mobile.Resources.i18n;
using System.Text;

namespace MLS_Mobile;

public partial class Calibrator : ContentPage
{

  #region 定数・インスタンス変数・プロパティ定義

  /// <summary>風速自動校正時間[分間]</summary>
  private readonly int[] VEL_CAL_TIMES = new int[] { 3, 5, 10, 30, 60, 180 };

  /// <summary>温度自動校正時間[時間]</summary>
	private readonly int[] TMP_CAL_TIMES = new int[] { 1, 3, 6, 12, 24 };

  /// <summary>電圧手動校正中か否か</summary>
  private bool calibratingVoltage = false;

  /// <summary>風速電圧の初回受信か否か</summary>
  private bool isFirstVoltageMessage = true;

  /// <summary>風速電圧校正用カウントダウン</summary>
  private int countDownTime = 30;

  #endregion

  #region コンストラクタ

  public Calibrator()
  {
    InitializeComponent();

    //風速自動校正時間リストの作成
    List<string> velCalItems = new List<string>();
    for (int i = 0; i < VEL_CAL_TIMES.Length; i++)
      velCalItems.Add(VEL_CAL_TIMES[i] + " " + MLSResource.Minute);
    velPicker.ItemsSource = velCalItems;
    velPicker.SelectedIndex = 0;

    //温度自動校正時間リストの作成
    List<string> tmpCalItems = new List<string>();
    for (int i = 0; i < TMP_CAL_TIMES.Length; i++)
      tmpCalItems.Add(TMP_CAL_TIMES[i] + " " + MLSResource.Hour);
    tmpPicker.ItemsSource = tmpCalItems;
    tmpPicker.SelectedIndex = 0;

    Task.Run(async () =>
    {
      while (true)
      {
        if (calibratingVoltage && 0 < countDownTime)
        {
          countDownTime--;
          Application.Current.Dispatcher.Dispatch(() =>
          {
            cdownLabel.Text = countDownTime.ToString();
            cdownLabel.TextColor = countDownTime == 0 ? Colors.ForestGreen : Colors.Red;
          });          
        }        
        await Task.Delay(1000);
      }
    });
  }

  #endregion

  #region ロード・アンロードイベント

  protected override void OnAppearing()
  {
    base.OnAppearing();

    MLUtility.Logger.CalibratingVoltageReceivedEvent += Logger_CalibratingVoltageReceivedEvent;
    MLUtility.Logger.EndCalibratingVoltageMessageReceivedEvent += Logger_EndCalibratingVoltageMessageReceivedEvent;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    MLUtility.Logger.CalibratingVoltageReceivedEvent -= Logger_CalibratingVoltageReceivedEvent;
    MLUtility.Logger.EndCalibratingVoltageMessageReceivedEvent -= Logger_EndCalibratingVoltageMessageReceivedEvent;
  }

  private void Logger_CalibratingVoltageReceivedEvent(object sender, EventArgs e)
  {
    //初回受信時にコントロールを表示してカウントダウンを始める
    if (isFirstVoltageMessage)
    {
      calibratingVoltage = true;
      isFirstVoltageMessage = false;
      countDownTime = 30;

      Application.Current.Dispatcher.Dispatch(() =>
      {
        cdownLabel.TextColor = Colors.Red;
        velLabel.IsVisible = true;
        cdownLabel.IsVisible = true;
      });
    }

    //電圧表示を更新
    Application.Current.Dispatcher.Dispatch(() =>
    {
      velLabel.Text = MLUtility.Logger.VelocityVoltage.ToString("F3");
    });
  }

  private void Logger_EndCalibratingVoltageMessageReceivedEvent(object sender, EventArgs e)
  {
    calibratingVoltage = false;

    Application.Current.Dispatcher.Dispatch(() =>
    {
      velLabel.IsVisible = false;
      cdownLabel.IsVisible = false;
    });
  }

  #endregion

  #region コントローラ操作時の処理

  /// <summary>補正係数設定ボタンクリック時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void SetCF_Clicked(object sender, EventArgs e)
  {
    //インジケータ表示
    showIndicator(MLSResource.CR_Connecting);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        MLUtility.Logger.HasCorrectionFactorsReceived = false;
        while (!MLUtility.Logger.HasCorrectionFactorsReceived)
        {
          //5回失敗したらエラー表示
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
              return;
            });
          }
          tryNum++;

          //開始コマンドを送信
          MLUtility.LoggerSideXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeLoadCorrectionFactorsCommand()));

          await Task.Delay(500);
        }

        //開始に成功したらページ移動
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync(nameof(CFSetting));
        });
      }
      catch { }
      finally
      {
        //インジケータを隠す
        Application.Current.Dispatcher.Dispatch(() =>
        {
          hideIndicator();
        });
      }
    });
  }

  /// <summary>温度自動校正ボタンクリック時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void AutoCBT_Clicked(object sender, EventArgs e)
  {
    //インジケータ表示
    showIndicator(MLSResource.CR_Connecting);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        MLUtility.Logger.HasTemperatureAutoCalibrationReceived = false;
        while (!MLUtility.Logger.HasTemperatureAutoCalibrationReceived)
        {
          //5回失敗したらエラー表示
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
              return;
            });
          }
          tryNum++;

          //開始コマンドを送信
          int sec = TMP_CAL_TIMES[tmpPicker.SelectedIndex] * 3600;
          MLUtility.LoggerSideXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeAutoTemperatureCalibrationCommand(sec)));

          await Task.Delay(500);
        }

        //開始に成功したらページ移動
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync("../..");
        });
      }
      catch { }
      finally
      {
        //インジケータを隠す
        Application.Current.Dispatcher.Dispatch(() =>
        {
          hideIndicator();
        });
      }
    });
  }

  /// <summary>風速自動校正ボタンクリック時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void AutoCBV_Clicked(object sender, EventArgs e)
  {
    //インジケータ表示
    showIndicator(MLSResource.CR_Connecting);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        MLUtility.Logger.HasVelocityAutoCalibrationReceived = false;
        while (!MLUtility.Logger.HasVelocityAutoCalibrationReceived)
        {
          //5回失敗したらエラー表示
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
              return;
            });
          }
          tryNum++;

          //開始コマンドを送信
          int sec = VEL_CAL_TIMES[velPicker.SelectedIndex] * 60;
          MLUtility.LoggerSideXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeAutoVelocityCalibrationCommand(sec)));

          await Task.Delay(500);
        }

        //開始に成功したらページ移動
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync("../..");
        });
      }
      catch { }
      finally
      {
        //インジケータを隠す
        Application.Current.Dispatcher.Dispatch(() =>
        {
          hideIndicator();
        });
      }
    });
  }

  /// <summary>風速電圧手動校正ボタンクリック時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void CBV_Clicked(object sender, EventArgs e)
  {
    //インジケータ表示
    showIndicator(MLSResource.CR_Connecting);

    isFirstVoltageMessage = true;

    Task.Run(async () =>
    {
      try
      {
        //校正中ならば停止
        if (calibratingVoltage)
        {
          MLUtility.Logger.HasEndCalibratingVoltageMessageReceived = false;
          while (!MLUtility.Logger.HasEndCalibratingVoltageMessageReceived)
          {
            //5回失敗したらエラー表示
            int tryNum = 0;
            if (5 <= tryNum)
            {
              Application.Current.Dispatcher.Dispatch(() =>
              {
                DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
                return;
              });
            }
            tryNum++;

            //開始コマンドを送信
            MLUtility.LoggerSideXBee.SendSerialData
            (Encoding.ASCII.GetBytes(MLogger.MakeEndCalibratingVoltageCommand()));

            await Task.Delay(500);
          }
        }
        //非校正中ならば開始
        else 
        {
          MLUtility.Logger.HasStartCalibratingVoltageMessageReceived = false;
          while (!MLUtility.Logger.HasStartCalibratingVoltageMessageReceived)
          {
            //5回失敗したらエラー表示
            int tryNum = 0;
            if (5 <= tryNum)
            {
              Application.Current.Dispatcher.Dispatch(() =>
              {
                DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
                return;
              });
            }
            tryNum++;

            //開始コマンドを送信
            MLUtility.LoggerSideXBee.SendSerialData
            (Encoding.ASCII.GetBytes(MLogger.MakeStartCalibratingVoltageCommand()));

            await Task.Delay(500);
          }
        }
      }
      catch { }
      finally
      {
        Application.Current.Dispatcher.Dispatch(() =>
        {
          //インジケータを隠す
          hideIndicator();

          //コントロールの表示を更新
          voltageBtn.Text = calibratingVoltage ? MLSResource.CR_EndCalibration : MLSResource.CR_CalibrateVelocityVoltage;
          velPicker.IsEnabled = tmpPicker.IsEnabled = autoTmpBtn.IsEnabled = autoVelBtn.IsEnabled = corBtn.IsEnabled = !calibratingVoltage;
        });
      }
    });
  }

  #endregion

  #region インジケータの操作

  /// <summary>インジケータを表示する</summary>
  private void showIndicator(string message)
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      indicatorLabel.Text = message;
      grayback.IsVisible = indicator.IsVisible = true;
    });
  }

  /// <summary>インジケータを隠す</summary>
  private void hideIndicator()
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      grayback.IsVisible = indicator.IsVisible = false;
    });
  }

  #endregion

}