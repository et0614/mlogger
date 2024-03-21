namespace MLS_Mobile;

using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;
using MLLib;

[QueryProperty(nameof(MLoggerLowAddress), "mlLowAddress")]
[QueryProperty(nameof(CloValue), "CloValue")]
[QueryProperty(nameof(MetValue), "MetValue")]
public partial class DataReceive : ContentPage
{

  #region インスタンス変数・プロパティ

  /// <summary>低位アドレス</summary>
  private string _mlLowAddress = "";

  /// <summary>低位アドレスを設定・取得する</summary>
  public string MLoggerLowAddress
  {
    get
    {
      return _mlLowAddress;
    }
    set
    {
      //登録済の場合にはイベントを解除
      MLogger ml = MLUtility.GetLogger(_mlLowAddress);
      if (ml != null)
      {
        MLVModel.Logger = null;
        ml.MeasuredValueReceivedEvent -= Logger_MeasuredValueReceivedEvent;
      }

      _mlLowAddress = value;
      ml = MLUtility.GetLogger(_mlLowAddress);

      this.Title = ml.LocalName;

      //Clo値,代謝量初期化
      CloValue = ml.CloValue;
      MetValue = ml.MetValue;

      //MLoggerイベント登録
      ml.MeasuredValueReceivedEvent += Logger_MeasuredValueReceivedEvent;

      //Bind
      MLVModel.Logger = ml;
    }
  }

  /// <summary>データを受信するMLoggerを設定・取得する</summary>
  public MLogger Logger
  {
    get
    {
      return MLUtility.GetLogger(_mlLowAddress);
    }
  }

  public MLoggerViewModel MLVModel { get; set; } = new MLoggerViewModel();

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

    BindingContext = MLVModel;

    cloTitle.Text = MLSResource.ClothingUnit + " [clo]";
    metTitle.Text = MLSResource.MetabolicUnit + " [met]";  
  }

  /// <summary>デコンストラクタ</summary>
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