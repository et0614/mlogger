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

  #region �萔�錾

  /// <summary>���ω����鎞��[sec]</summary>
  private const int AVE_TIME = 10;

  #endregion

  #region �C���X�^���X�ϐ��E�v���p�e�B

  /// <summary>�ʐM����MLogger���擾����</summary>
  public MLogger Logger { get { return MLUtility.GetLogger(_mlLowAddress); } }

  /// <summary>��ʃA�h���X</summary>
  private string _mlLowAddress = "";

  /// <summary>��ʃA�h���X��ݒ�E�擾����</summary>
  public string MLoggerLowAddress
  {
    get
    {
      return _mlLowAddress;
    }
    set
    {
      //�o�^�ς̏ꍇ�ɂ̓C�x���g������
      MLogger ml = MLUtility.GetLogger(_mlLowAddress);
      if (ml != null)
        ml.CalibratingVoltageReceivedEvent -= Logger_CalibratingVoltageReceivedEvent;

      _mlLowAddress = value;
      ml = MLUtility.GetLogger(_mlLowAddress);
      if (ml != null)
        ml.CalibratingVoltageReceivedEvent += Logger_CalibratingVoltageReceivedEvent;
    }
  }

  /// <summary>�`���[�g�ĕ`����ꎞ��~����</summary>
  private bool stopUpdatingChart = false;

  /// <summary>�����d���̏����M���ۂ�</summary>
  private bool isFirstVoltageMessage = true;

  /// <summary>�����d�����X�g[V]</summary>
  private double[] velVols = new double[AVE_TIME];

  /// <summary>�Z�����̓_�ԍ�</summary>
  private int calibratingIndex = 5;

  /// <summary>�Z�����̓d�����X�g[V]</summary>
  private double[] calbratingVoltages = { 1.450, 1.648, 1.734, 1.801 };

  private LineSeries<ObservablePoint> estimatedLine;

  /// <summary>�v�����ꂽ�_</summary>
  private ObservablePoint[] measuredPoints = new ObservablePoint[4];

  /// <summary>�v�����̓d��</summary>
  private ObservablePoint[] voltagePoints = [new ObservablePoint(0.0, 1.0), new ObservablePoint(1.2, 1.0)];

  #endregion

  #region �R���X�g���N�^

  /// <summary>�C���X�^���X������������</summary>
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

    //�I���R�}���h���M
    Task.Run(() =>
    {
      MLUtility.ConnectedXBee.SendSerialData
      (Encoding.ASCII.GetBytes(MLogger.MakeEndCalibratingVoltageCommand()));
    });

    Logger.CalibratingVoltageReceivedEvent -= Logger_CalibratingVoltageReceivedEvent;
  }

  private void Logger_CalibratingVoltageReceivedEvent(object sender, EventArgs e)
  {
    //�����M���̓f�[�^��������
    if (isFirstVoltageMessage)
    {
      isFirstVoltageMessage = false;
      for (int i = 0; i < AVE_TIME; i++) velVols[i] = 0;
    }

    //���ϓd���̌v�Z[
    double aveVol = 0;
    for (int i = 0; i < AVE_TIME - 1; i++)
    {
      aveVol += velVols[i];
      velVols[i] = velVols[i + 1];
    }
    aveVol += Logger.VelocityVoltage;
    velVols[AVE_TIME - 1] = Logger.VelocityVoltage;
    aveVol /= AVE_TIME;

    //���ϓd�������肵�����ۂ��̔���
    bool isStabled = true;
    for (int i = 0; i < AVE_TIME - 1; i++)
    {
      if (0.05 < Math.Abs(velVols[i] - aveVol))
      {
        isStabled = false;
        break;
      }
    }

    //�d���\�����X�V
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

  #region �`���[�g�̏���������

  /// <summary>�`���[�g������������</summary>
  private void initChart()
  {
    //�S�̂̐F�ݒ�
    myChart.DrawMarginFrame = new DrawMarginFrame()
    {
      Fill = new SolidColorPaint(SKColors.White), // �w�i�F��
      Stroke = new SolidColorPaint(SKColors.Black), // �g���F��
    };

    //X���̐ݒ�
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

    //Y���̐ݒ�
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

    //�����W��������
    MLUtility.EstimateCoefs(
      0.3, 0.6, 1.0,
      calbratingVoltages[0], calbratingVoltages[1], calbratingVoltages[2], calbratingVoltages[3],
      out double cfA, out double cfB, out double cfC);

    //�_
    measuredPoints = [
        new ObservablePoint(0.0, calbratingVoltages[0]),
        new ObservablePoint(0.3, calbratingVoltages[1]),
        new ObservablePoint(0.6, calbratingVoltages[2]),
        new ObservablePoint(1.0, calbratingVoltages[3])
      ];

    //�d����
    LineSeries<ObservablePoint> voltageLine = new LineSeries<ObservablePoint>
    {
      Values = voltagePoints,

      Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 1 }, //��
      Fill = null, //�����h��Ԃ�
      GeometryFill = null, //�v���b�g�h��Ԃ�
      GeometryStroke = null, //�v���b�g�g��
      GeometrySize = 0,

      AnimationsSpeed = TimeSpan.Zero
    };

    //���
    LineSeries<ObservablePoint> referenceLine = new LineSeries<ObservablePoint>()
    {
      Values = makePointsFromCoefficients(1.45, 79.744, -12.029, 2.3595),
      Stroke = new SolidColorPaint(SKColors.LightGray) { StrokeThickness = 3 },
      Fill = null, //�����h��Ԃ�
      GeometryFill = null, //�v���b�g�h��Ԃ�
      GeometryStroke = null, //�v���b�g�g��
      LineSmoothness = 0.5, //���Ȑ�
    };

    //�����W������Z�o������
    estimatedLine = new LineSeries<ObservablePoint>
    {
      Values = makePointsFromCoefficients(calbratingVoltages[0], cfA, cfB, cfC),

      Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 }, //��
      Fill = null, //�����h��Ԃ�
      GeometryFill = null, //�v���b�g�h��Ԃ�
      GeometryStroke = null, //�v���b�g�g��
      GeometrySize = 0,

      AnimationsSpeed = TimeSpan.Zero
    };

    //�Z���p�̌v���_
    LineSeries<ObservablePoint> measuredLine = new LineSeries<ObservablePoint>
    {
      Values = measuredPoints,

      Stroke = null, //��
      Fill = null, //�����h��Ԃ�
      GeometryFill = null, //�v���b�g�h��Ԃ�
      GeometryStroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 }, //�v���b�g�g��
      GeometrySize = 8,

      DataLabelsSize = 14,       // �f�[�^���x���̃t�H���g�T�C�Y
      DataLabelsPosition = LiveChartsCore.Measure.DataLabelsPosition.Right, // �f�[�^���x���̈ʒu
      DataLabelsPaint = new SolidColorPaint(SKColors.Black), // �f�[�^���x���̐F
      DataLabelsFormatter = (point) => point.Coordinate.PrimaryValue.ToString("F3"),

      AnimationsSpeed = TimeSpan.Zero
    };

    //�`��f�[�^��ǉ�
    myChart.Series = new ISeries[]
    {
        referenceLine,
        estimatedLine,
        measuredLine,
        voltageLine
    };
  }

  #endregion

  #region �`���[�g�̍X�V����

  /// <summary>�����W�������Ƃɕ����̐���_���쐬����</summary>
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

  #region �R���g���[�����쎞�̏���

  /// <summary>�d���̒l���ύX���ꂽ�ꍇ�̏���</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void VoltageEntry_TextChanged(object sender, TextChangedEventArgs e)
  {
    //�d�����擾
    if (!double.TryParse(eVolRef.Text, out double volRef)) return;
    if (!double.TryParse(eVol1.Text, out double vol1)) return;
    if (!double.TryParse(eVol2.Text, out double vol2)) return;
    if (!double.TryParse(eVol3.Text, out double vol3)) return;

    //�ُ�Ȓl�̏ꍇ�ɂ͖���
    if (vol1 < volRef | vol2 < vol1 | vol3 < vol2) return; //�P�������ł͂Ȃ�
    if (volRef < 1.0 || 2.0 < volRef) return;
    if (vol1 < 1.0 || 2.0 < vol1) return;
    if (vol2 < 1.0 || 2.0 < vol2) return;
    if (vol3 < 1.0 || 2.0 < vol3) return;

    //�`���[�g�\�����X�V
    if (sender == eVolRef) measuredPoints[0].Y = volRef;
    if (sender == eVol1) measuredPoints[1].Y = vol1;
    if (sender == eVol2) measuredPoints[2].Y = vol2;
    if (sender == eVol3) measuredPoints[3].Y = vol3;

    //�d������W���𐄒�
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

    //�ĕ`��
    estimatedLine.Values = makePointsFromCoefficients(volRef, cfA, cfB, cfC);
  }

  /// <summary>�������Ƃ̓d�����X�V���ꂽ�ꍇ�̏���</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void VoltageButton_Clicked(object sender, EventArgs e)
  {
    //���݂̕��ϓd�����擾
    if (!double.TryParse(aveVoltage.Text, out double volt)) return;
    string volText = volt.ToString("F3");

    if (sender == btnVolRef) eVolRef.Text = volText;
    else if (sender == btnVol1) eVol1.Text = volText;
    else if (sender == btnVol2) eVol2.Text = volText;
    else if (sender == btnVol3) eVol3.Text = volText;

    coefA.TextColor = coefB.TextColor = coefC.TextColor = Colors.Red;
  }

  /// <summary>�����W�����ύX���ꂽ�ꍇ�̏���</summary>
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

    //�C���W�P�[�^�\��
    showIndicator(MLSResource.CR_Connecting);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        Logger.HasVelocityCharacteristicsReceived = false;
        while (!Logger.HasVelocityCharacteristicsReceived)
        {
          //5�񎸔s������G���[�\��
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
              return;
            });
          }
          tryNum++;

          //�J�n�R�}���h�𑗐M
          MLUtility.ConnectedXBee.SendSerialData
          (Encoding.ASCII.GetBytes(
            MLogger.MakeVelocityCharateristicsSettingCommand(volRef, cfA, cfB, cfC)));

          await Task.Delay(500);
        }

        //�J�n�ɐ��������甽�f
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
        //�C���W�P�[�^���B��
        Application.Current.Dispatcher.Dispatch(() =>
        {
          hideIndicator();
        });
      }
    });

  }

  #region �C���W�P�[�^�̑���

  /// <summary>�C���W�P�[�^��\������</summary>
  private void showIndicator(string message)
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      indicatorLabel.Text = message;
      grayback.IsVisible = indicator.IsVisible = true;
    });
  }

  /// <summary>�C���W�P�[�^���B��</summary>
  private void hideIndicator()
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      grayback.IsVisible = indicator.IsVisible = false;
    });
  }

  #endregion

}
