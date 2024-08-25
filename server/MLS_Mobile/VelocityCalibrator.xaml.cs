using LiveChartsCore.SkiaSharpView;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using MLLib;
using System.Text;
using MLS_Mobile.Resources.i18n;

namespace MLS_Mobile;

[QueryProperty(nameof(MLoggerLowAddress), "mlLowAddress")]
public partial class VelocityCalibrator : ContentPage
{

  #region 定数宣言

  /// <summary>平均化する時間[sec]</summary>
  private const int AVE_TIME = 10;

  #endregion

  #region インスタンス変数・プロパティ

  /// <summary>通信するMLoggerを取得する</summary>
  public MLogger Logger { get { return MLUtility.GetLogger(_mlLowAddress); } }

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
        ml.CalibratingVoltageReceivedEvent -= Logger_CalibratingVoltageReceivedEvent;

      _mlLowAddress = value;
      ml = MLUtility.GetLogger(_mlLowAddress);
      if (ml != null)
        ml.CalibratingVoltageReceivedEvent += Logger_CalibratingVoltageReceivedEvent;
    }
  }

  /// <summary>チャート再描画を一時停止する</summary>
  private bool stopUpdatingChart = false;

  /// <summary>風速電圧の初回受信か否か</summary>
  private bool isFirstVoltageMessage = true;

  /// <summary>風速電圧リスト[V]</summary>
  private double[] velVols = new double[AVE_TIME];

  /// <summary>校正中の点番号</summary>
  private int calibratingIndex = 5;

  /// <summary>校正中の電圧リスト[V]</summary>
  private double[] calbratingVoltages = { 1.450, 1.648, 1.734, 1.801 };

  private LineSeries<ObservablePoint> estimatedLine;

  /// <summary>計測された点</summary>
  private ObservablePoint[] measuredPoints = new ObservablePoint[4];

  /// <summary>計測中の電圧</summary>
  private ObservablePoint[] voltagePoints = [new ObservablePoint(0.0, 1.0), new ObservablePoint(1.2, 1.0)];

  #endregion

  #region コンストラクタ

  /// <summary>インスタンスを初期化する</summary>
  public VelocityCalibrator()
  {
    InitializeComponent();

    initChart();
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    isFirstVoltageMessage = true;
  }

  protected override void OnDisappearing()
  {
    base.OnDisappearing();

    //終了コマンド送信
    Task.Run(() =>
    {
      MLUtility.ConnectedXBee.SendSerialData
      (Encoding.ASCII.GetBytes(MLogger.MakeEndCalibratingVoltageCommand()));
    });

    Logger.CalibratingVoltageReceivedEvent -= Logger_CalibratingVoltageReceivedEvent;
  }

  private void Logger_CalibratingVoltageReceivedEvent(object sender, EventArgs e)
  {
    //初回受信時はデータを初期化
    if (isFirstVoltageMessage)
    {
      isFirstVoltageMessage = false;
      for (int i = 0; i < AVE_TIME; i++) velVols[i] = 0;
    }

    //平均電圧の計算[
    double aveVol = 0;
    for (int i = 0; i < AVE_TIME - 1; i++)
    {
      aveVol += velVols[i];
      velVols[i] = velVols[i + 1];
    }
    aveVol += Logger.VelocityVoltage;
    velVols[AVE_TIME - 1] = Logger.VelocityVoltage;
    aveVol /= AVE_TIME;

    //平均電圧が安定したか否かの判定
    bool isStabled = true;
    for (int i = 0; i < AVE_TIME - 1; i++)
    {
      if (0.05 < Math.Abs(velVols[i] - aveVol))
      {
        isStabled = false;
        break;
      }
    }

    //電圧表示を更新
    Application.Current.Dispatcher.Dispatch(() =>
    {
      double velV = Logger.VelocityVoltage;
      instVoltage.Text = "(" + Logger.VelocityVoltage.ToString("F3") + ")";
      aveVoltage.Text = aveVol.ToString("F3");

      instVoltage.TextColor = aveVoltage.TextColor = voltUnit.TextColor 
        = isStabled ? Colors.Green : Colors.Red;

      voltagePoints[0].Y = voltagePoints[1].Y = aveVol;
    });
  }

  #endregion

  #region チャートの初期化処理

  /// <summary>チャートを初期化する</summary>
  private void initChart()
  {
    //全体の色設定
    myChart.DrawMarginFrame = new DrawMarginFrame()
    {
      Fill = new SolidColorPaint(SKColors.White), // 背景：白
      Stroke = new SolidColorPaint(SKColors.Black), // 枠線：黒
    };

    //X軸の設定
    myChart.XAxes = new List<Axis>
      {
        new Axis(){
          Name = "Velocity [m/s]",
          NameTextSize = 14,
          NamePaint = new SolidColorPaint(SKColors.Black),
          NamePadding = new LiveChartsCore.Drawing.Padding(0, 0, 0, 10),

          MinLimit = 0.0,
          MaxLimit = 1.2,
          MinStep = 0.1,
          ForceStepToMin = true,
          SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray)
          {
            StrokeThickness = 1,
            PathEffect = new DashEffect(new float[] { 2, 2 })
          },

          LabelsRotation = 90,
          LabelsAlignment = LiveChartsCore.Drawing.Align.End,
          Labeler = value => value.ToString("F1"),
          TextSize = 12,
          LabelsPaint = new SolidColorPaint(SKColors.Black),
        }
      };

    //Y軸の設定
    myChart.YAxes = new List<Axis>
      {
        new Axis(){
          Name = "Voltage [V]",
          NameTextSize = 14,
          NamePaint = new SolidColorPaint(SKColors.Black),
          NamePadding = new LiveChartsCore.Drawing.Padding(0, 10, 0, 0),

          MinLimit = 1.4,
          MaxLimit = 2.0,
          MinStep = 0.1,
          ForceStepToMin = true,
          SeparatorsPaint = new SolidColorPaint(SKColors.LightSlateGray)
          {
            StrokeThickness = 1,
            PathEffect = new DashEffect(new float[] { 2, 2 })
          },

          LabelsRotation = 0,
          LabelsAlignment = LiveChartsCore.Drawing.Align.Start,
          Labeler = value => value.ToString("F1"),
          LabelsPaint = new SolidColorPaint(SKColors.Black),
          TextSize = 12,
        }
      };

    //特性係数初期化
    MLUtility.EstimateCoefs(
      0.3, 0.6, 1.0,
      calbratingVoltages[0], calbratingVoltages[1], calbratingVoltages[2], calbratingVoltages[3],
      out double cfA, out double cfB, out double cfC);

    //点
    measuredPoints = [
        new ObservablePoint(0.0, calbratingVoltages[0]),
        new ObservablePoint(0.3, calbratingVoltages[1]),
        new ObservablePoint(0.6, calbratingVoltages[2]),
        new ObservablePoint(1.0, calbratingVoltages[3])
      ];

    //電圧線
    LineSeries<ObservablePoint> voltageLine = new LineSeries<ObservablePoint>
    {
      Values = voltagePoints,

      Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 1 }, //線
      Fill = null, //下部塗りつぶし
      GeometryFill = null, //プロット塗りつぶし
      GeometryStroke = null, //プロット枠線
      GeometrySize = 0,

      AnimationsSpeed = TimeSpan.Zero
    };

    //基準線
    LineSeries<ObservablePoint> referenceLine = new LineSeries<ObservablePoint>()
    {
      Values = makePointsFromCoefficients(1.45, 79.744, -12.029, 2.3595),
      Stroke = new SolidColorPaint(SKColors.LightGray) { StrokeThickness = 3 },
      Fill = null, //下部塗りつぶし
      GeometryFill = null, //プロット塗りつぶし
      GeometryStroke = null, //プロット枠線
      LineSmoothness = 0.5, //やや曲線
    };

    //特性係数から算出した線
    estimatedLine = new LineSeries<ObservablePoint>
    {
      Values = makePointsFromCoefficients(calbratingVoltages[0], cfA, cfB, cfC),

      Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 }, //線
      Fill = null, //下部塗りつぶし
      GeometryFill = null, //プロット塗りつぶし
      GeometryStroke = null, //プロット枠線
      GeometrySize = 0,

      AnimationsSpeed = TimeSpan.Zero
    };

    //校正用の計測点
    LineSeries<ObservablePoint> measuredLine = new LineSeries<ObservablePoint>
    {
      Values = measuredPoints,

      Stroke = null, //線
      Fill = null, //下部塗りつぶし
      GeometryFill = null, //プロット塗りつぶし
      GeometryStroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 }, //プロット枠線
      GeometrySize = 8,

      DataLabelsSize = 14,       // データラベルのフォントサイズ
      DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Right, // データラベルの位置
      DataLabelsPaint = new SolidColorPaint(SKColors.Black), // データラベルの色
      DataLabelsFormatter = (point) => point.Coordinate.PrimaryValue.ToString("F3"),

      AnimationsSpeed = TimeSpan.Zero
    };

    //描画データを追加
    myChart.Series = new ISeries[]
    {
        referenceLine,
        estimatedLine,
        measuredLine,
        voltageLine
    };
  }

  #endregion

  #region チャートの更新処理

  /// <summary>特性係数をもとに風速の推定点を作成する</summary>
  /// <param name="minV"></param>
  /// <param name="coefA"></param>
  /// <param name="coefB"></param>
  /// <param name="coefC"></param>
  /// <returns></returns>
  private static List<ObservablePoint> makePointsFromCoefficients(
    double minV, double coefA, double coefB, double coefC)
  {
    double maxV = 2.00;
    List<ObservablePoint> points = new List<ObservablePoint>();
    double cV = minV;
    while (cV < maxV)
    {
      double vN = (cV / minV) - 1.0;
      double vel = vN * (coefC + vN * (coefB + vN * coefA));
      points.Add(new ObservablePoint(vel, cV));
      if (1.2 < vel) break;
      cV += 0.05;
    }
    return points;
  }

  #endregion

  #region コントローラ操作時の処理

  /// <summary>電圧の値が変更された場合の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void VoltageEntry_TextChanged(object sender, TextChangedEventArgs e)
  {
    //電圧を取得
    if (!double.TryParse(eVolRef.Text, out double volRef)) return;
    if (!double.TryParse(eVol1.Text, out double vol1)) return;
    if (!double.TryParse(eVol2.Text, out double vol2)) return;
    if (!double.TryParse(eVol3.Text, out double vol3)) return;

    //異常な値の場合には無視
    if (vol1 < volRef | vol2 < vol1 | vol3 < vol2) return; //単純増加ではない
    if (volRef < 1.0 || 2.0 < volRef) return;
    if (vol1 < 1.0 || 2.0 < vol1) return;
    if (vol2 < 1.0 || 2.0 < vol2) return;
    if (vol3 < 1.0 || 2.0 < vol3) return;

    //チャート表示を更新
    if (sender == eVolRef) measuredPoints[0].Y = volRef;
    if (sender == eVol1) measuredPoints[1].Y = vol1;
    if (sender == eVol2) measuredPoints[2].Y = vol2;
    if (sender == eVol3) measuredPoints[3].Y = vol3;

    //電圧から係数を推定
    MLUtility.EstimateCoefs(
      0.3, 0.6, 1.0, 
      volRef, vol1, vol2, vol3, 
      out double cfA, out double cfB, out double cfC);

    stopUpdatingChart = true;
    coefA.Text = cfA.ToString("F3");
    coefB.Text = cfB.ToString("F3");
    coefC.Text = cfC.ToString("F3");
    stopUpdatingChart = false;

    coefA.TextColor = coefB.TextColor = coefC.TextColor = Colors.Red;

    //再描画
    estimatedLine.Values = makePointsFromCoefficients(volRef, cfA, cfB, cfC);
  }

  /// <summary>風速ごとの電圧が更新された場合の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void VoltageButton_Clicked(object sender, EventArgs e)
  {
    //現在の平均電圧を取得
    if (!double.TryParse(aveVoltage.Text, out double volt)) return;
    string volText = volt.ToString("F3");

    if (sender == btnVolRef) eVolRef.Text = volText;
    else if (sender == btnVol1) eVol1.Text = volText;
    else if (sender == btnVol2) eVol2.Text = volText;
    else if (sender == btnVol3) eVol3.Text = volText;

    coefA.TextColor = coefB.TextColor = coefC.TextColor = Colors.Red;
  }

  /// <summary>特性係数が変更された場合の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void CoefficientEntry_TextChanged(object sender, TextChangedEventArgs e)
  {
    if (stopUpdatingChart) return;

    if (!double.TryParse(eVolRef.Text, out double volRef)) return;
    if (!double.TryParse(coefA.Text, out double cfA)) return;
    if (!double.TryParse(coefB.Text, out double cfB)) return;
    if (!double.TryParse(coefC.Text, out double cfC)) return;

    ((Entry)sender).TextColor = Colors.Red;

    estimatedLine.Values = makePointsFromCoefficients(volRef, cfA, cfB, cfC);
  }

  #endregion

  private void UpdateCoefficientButton_Clicked(object sender, EventArgs e)
  {
    if (!double.TryParse(eVolRef.Text, out double volRef)) return;
    if (!double.TryParse(coefA.Text, out double cfA)) return;
    if (!double.TryParse(coefB.Text, out double cfB)) return;
    if (!double.TryParse(coefC.Text, out double cfC)) return;

    //インジケータ表示
    showIndicator(MLSResource.CR_Connecting);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        Logger.HasVelocityCharacteristicsReceived = false;
        while (!Logger.HasVelocityCharacteristicsReceived)
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
          MLUtility.ConnectedXBee.SendSerialData
          (Encoding.ASCII.GetBytes(
            MLogger.MakeVelocityCharateristicsSettingCommand(volRef, cfA, cfB, cfC)));

          await Task.Delay(500);
        }

        //開始に成功したら反映
        Application.Current.Dispatcher.Dispatch(() =>
        {
          stopUpdatingChart = true;
          coefA.Text = Logger.VelocityCharacteristicsCoefA.ToString("F3");
          coefB.Text = Logger.VelocityCharacteristicsCoefB.ToString("F3");
          coefC.Text = Logger.VelocityCharacteristicsCoefC.ToString("F3");
          coefA.TextColor = coefB.TextColor = coefC.TextColor = Colors.Black;
          stopUpdatingChart = false;
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
