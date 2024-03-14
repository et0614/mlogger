using DigiIoT.Maui.Devices.XBee;
using MLLib;
using MLS_Mobile.Resources.i18n;
using System.Text;

namespace MLS_Mobile;

[QueryProperty(nameof(Logger), "mLogger")]
[QueryProperty(nameof(ConnectedXBee), "xbee")]
public partial class Calibrator : ContentPage
{

  #region 定数・インスタンス変数・プロパティ定義

  /// <summary>平均化する時間[sec]</summary>
  private const int AVE_TIME = 10;

  /// <summary>風速自動校正時間[分間]</summary>
  private readonly int[] VEL_CAL_TIMES = new int[] { 3, 5, 10, 30, 60, 180 };

  /// <summary>温度自動校正時間[時間]</summary>
	private readonly int[] TMP_CAL_TIMES = new int[] { 1, 3, 6, 12, 24 };

  /// <summary>電圧手動校正中か否か</summary>
  private bool calibratingVoltage = false;

  /// <summary>風速電圧の初回受信か否か</summary>
  private bool isFirstVoltageMessage = true;

  /// <summary>風速電圧校正用カウントダウン</summary>
  private int countDownTime = 0;

  /// <summary>風速電圧リスト[V]</summary>
  private double[] velVols = new double[AVE_TIME];

  /// <summary>コマンド送信用のXBeeを設定・取得する</summary>
  public XBeeBLEDevice ConnectedXBee { get; set; }

  //データを受信するMLogger
  private MLogger _mLogger;

  /// <summary>データを受信するMLoggerを設定・取得する</summary>
  public MLogger Logger
  {
    get
    {
      return _mLogger;
    }
    set
    {
      _mLogger = value;
    }
  }

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
        if (calibratingVoltage)
        {
          countDownTime++;
          Application.Current.Dispatcher.Dispatch(() =>
          {
            cdownLabel.Text = countDownTime.ToString();
            cdownLabel.TextColor = countDownTime < 30 ? Colors.Red : Colors.ForestGreen;
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

    Logger.CalibratingVoltageReceivedEvent += Logger_CalibratingVoltageReceivedEvent;
    Logger.EndCalibratingVoltageMessageReceivedEvent += Logger_EndCalibratingVoltageMessageReceivedEvent;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //無風電圧校正中ならば終了コマンドを送信
    if (calibratingVoltage)
      Task.Run(() =>
      {
        ConnectedXBee.SendSerialData
        (Encoding.ASCII.GetBytes(MLogger.MakeEndCalibratingVoltageCommand()));
      });

    Logger.CalibratingVoltageReceivedEvent -= Logger_CalibratingVoltageReceivedEvent;
    Logger.EndCalibratingVoltageMessageReceivedEvent -= Logger_EndCalibratingVoltageMessageReceivedEvent;
  }

  private void Logger_CalibratingVoltageReceivedEvent(object sender, EventArgs e)
  {
    //初回受信時にコントロールを表示してカウントダウンを始める
    if (isFirstVoltageMessage)
    {
      calibratingVoltage = true;
      isFirstVoltageMessage = false;
      countDownTime = 0;

      //風速リスト初期化
      for(int i=0;i<AVE_TIME;i++)
        velVols[i] = Logger.VelocityVoltage;

      Application.Current.Dispatcher.Dispatch(() =>
      {
        cdownLabel.TextColor = Colors.Red;
        velVLabel.IsVisible = true;
        velLabel.IsVisible = true;
        aveVelVLabel.IsVisible = true;
        aveVelLabel.IsVisible = true;
        cdownLabel.IsVisible = true;
      });
    }

    //平均風速の計算[
    double aveVol = 0;
    for (int i = 0; i < AVE_TIME - 1; i++)
    {
      aveVol += velVols[i];
      velVols[i] = velVols[i + 1];
    }
    aveVol += Logger.VelocityVoltage;
    velVols[AVE_TIME - 1] = Logger.VelocityVoltage;
    aveVol /= AVE_TIME;

    //電圧表示を更新
    Application.Current.Dispatcher.Dispatch(() =>
    {
      double velV = Logger.VelocityVoltage;
      velLabel.Text = Logger.ConvertVelocityVoltage(velV).ToString("F2") + " m/s";
      velVLabel.Text = "(" + velV.ToString("F3") + " V)";
      aveVelLabel.Text = Logger.ConvertVelocityVoltage(aveVol).ToString("F2") + " m/s";
      aveVelVLabel.Text = "(" + aveVol.ToString("F3") + " V)";
    });
  }

  private void Logger_EndCalibratingVoltageMessageReceivedEvent(object sender, EventArgs e)
  {
    calibratingVoltage = false;

    Application.Current.Dispatcher.Dispatch(() =>
    {
      velVLabel.IsVisible = false;
      velLabel.IsVisible = false;
      aveVelVLabel.IsVisible = false;
      aveVelLabel.IsVisible = false;
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
        Logger.HasCorrectionFactorsReceived = false;
        while (!Logger.HasCorrectionFactorsReceived)
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
          ConnectedXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeLoadCorrectionFactorsCommand()));

          await Task.Delay(500);
        }

        //開始に成功したらページ移動
        Application.Current.Dispatcher.Dispatch(() =>
        {
          Shell.Current.GoToAsync(nameof(CFSetting),
            new Dictionary<string, object> { { "mLogger", Logger }, { "xbee", ConnectedXBee } }
            );
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
        Logger.HasTemperatureAutoCalibrationReceived = false;
        while (!Logger.HasTemperatureAutoCalibrationReceived)
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
          ConnectedXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeAutoTemperatureCalibrationCommand(sec)));

          await Task.Delay(500);
        }

        //開始に成功したらページ移動
        Application.Current.Dispatcher.Dispatch(async () =>
        {
          await DisplayAlert("Alert", MLSResource.CR_StartCalibration, "OK");
          await Shell.Current.GoToAsync("../..");
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
        Logger.HasVelocityAutoCalibrationReceived = false;
        while (!Logger.HasVelocityAutoCalibrationReceived)
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
          ConnectedXBee.SendSerialData
          (Encoding.ASCII.GetBytes(MLogger.MakeAutoVelocityCalibrationCommand(sec)));

          await Task.Delay(500);
        }

        //開始に成功したらページ移動
        Application.Current.Dispatcher.Dispatch(async () =>
        {
          await DisplayAlert("Alert", MLSResource.CR_StartCalibration, "OK");
          await Shell.Current.GoToAsync("../..");
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
          Logger.HasEndCalibratingVoltageMessageReceived = false;
          while (!Logger.HasEndCalibratingVoltageMessageReceived)
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

            //終了コマンドを送信
            ConnectedXBee.SendSerialData
            (Encoding.ASCII.GetBytes(MLogger.MakeEndCalibratingVoltageCommand()));

            await Task.Delay(500);
          }
        }
        //非校正中ならば開始
        else 
        {
          Logger.HasStartCalibratingVoltageMessageReceived = false;
          while (!Logger.HasStartCalibratingVoltageMessageReceived)
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
            ConnectedXBee.SendSerialData
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
          cdownLabel.Text = "0";
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