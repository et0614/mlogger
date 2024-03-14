namespace MLS_Mobile;

using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;
using MLLib;

[QueryProperty(nameof(Logger), "mLogger")]
[QueryProperty(nameof(CloValue), "CloValue")]
[QueryProperty(nameof(MetValue), "MetValue")]
public partial class DataReceive : ContentPage
{

  #region インスタンス変数・プロパティ

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
      //登録済みのイベントは解除
      if(_mLogger != null)
        Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;

      _mLogger = value;

      this.Title = _mLogger.LocalName;

      //Clo値,代謝量初期化
      CloValue = _mLogger.CloValue;
      MetValue = _mLogger.MetValue;

      //MLoggerイベント登録
      _mLogger.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;
    }
  }

  /// <summary>Clo値を設定・取得する</summary>
  public double CloValue
  { get; set; } = 1.2;

  /// <summary>Met値を設定・取得する</summary>
  public double MetValue
  { get; set; } = 1.1;

  #endregion

  #region コンストラクタ

  public DataReceive()
  {
    InitializeComponent();

    BindingContext = this;

    cloTitle.Text = MLSResource.ClothingUnit + " [clo]";
    metTitle.Text = MLSResource.MetabolicUnit + " [met]";  
  }

  /// <summary>でコンストラクタ</summary>
  ~DataReceive()
  {
    if (Logger != null)
      Logger.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
  }

  #endregion

  #region コントロール操作時の処理

  //着衣量設定ボタンクリック時の処理
  private void CloBtn_Clicked(object sender, EventArgs e)
  {
    Shell.Current.GoToAsync(nameof(ClothingCoordinator));
  }

  private void slider_ValueChanged(object sender, ValueChangedEventArgs e)
  {
    if (Logger != null)
    {
      Logger.MetValue = metSlider.Value;
      Logger.CloValue = cloSlider.Value;
    }    
  }

  //活動量設定ボタンクリック時の処理
  private void ActBtn_Clicked(object sender, EventArgs e)
  {
    Shell.Current.GoToAsync(nameof(ActivitySelector));
  }

  #endregion

  #region ロード・アンロードイベント
  protected override void OnAppearing()
  {
    base.OnAppearing();

    //スリープ禁止
    DeviceDisplay.Current.KeepScreenOn = true;

    //着衣量と代謝量を反映
    cloSlider.Value = CloValue;
    metSlider.Value = MetValue;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //スリープ解除
    DeviceDisplay.Current.KeepScreenOn = false;
  }

  #endregion

  #region 通信処理

  private void Logger_MeasuredValueReceivedEvent(object sender, EventArgs e)
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      val_tmp.Text = Logger.DrybulbTemperature.LastValue.ToString("F1");
      val_hmd.Text = Logger.RelativeHumdity.LastValue.ToString("F1");
      val_glb.Text = Logger.GlobeTemperature.LastValue.ToString("F1");
      val_vel.Text = Logger.Velocity.LastValue.ToString("F2");
      val_lux.Text = Logger.Illuminance.LastValue.ToString("F1");
      val_mrt.Text = Logger.MeanRadiantTemperature.ToString("F1");
      val_pmv.Text = Logger.PMV.ToString("F2");
      val_ppd.Text = Logger.PPD.ToString("F1");
      val_set.Text = Logger.SETStar.ToString("F1");

      dtm_tmp.Text = Logger.DrybulbTemperature.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_hmd.Text = Logger.RelativeHumdity.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_glb.Text = Logger.GlobeTemperature.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_vel.Text = Logger.Velocity.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
      dtm_lux.Text = Logger.Illuminance.LastMeasureTime.ToString("yyyy/M/d HH:mm:ss");
    });

    //データを保存
    string line =
      Logger.LastMeasured.ToString("yyyy/M/d,HH:mm:ss") + "," +
      Logger.DrybulbTemperature.LastValue.ToString("F1") + "," +
      Logger.RelativeHumdity.LastValue.ToString("F1") + "," +
      Logger.GlobeTemperature.LastValue.ToString("F2") + "," +
      Logger.Velocity.LastValue.ToString("F3") + "," +
      Logger.Illuminance.LastValue.ToString("F2") + "," +
      Logger.GlobeTemperatureVoltage.ToString("F3") + "," +
      Logger.VelocityVoltage.ToString("F3") + Environment.NewLine;

    string fileName = Logger.LocalName + "_" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
    MLUtility.AppendData(fileName, line);
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