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
[QueryProperty(nameof(MinVoltageAndCoefficients), "minVandCoefs")]
public partial class VelocityCalibrator2 : ContentPage
{

  #region �萔�錾

  /// <summary>���ω����鎞��[sec]</summary>
  private const int AVE_TIME = 10;

  /// <summary>�����Z���p�ŏ�����[m/s]</summary>
  private const float MIN_AFLOW = 0.3f;

  /// <summary>�����Z���p���ԕ���[m/s]</summary>
  private const float MID_AFLOW = 0.7f;

  /// <summary>�����Z���p�ő啗��[m/s]</summary>
  private const float MAX_AFLOW = 1.5f;

  #endregion

  #region �C���X�^���X�ϐ��E�v���p�e�B

  /// <summary>�����������ۂ�</summary>
  private bool initializing = false;

  /// <summary>�ʐM����MLogger���擾����</summary>
  public MLogger Logger { get { return MLUtility.GetLogger(_mlLowAddress); } }

  /// <summary>�ŏ��d��[V]�ƌW�����X�g��ݒ肷��</summary>
  public double[] MinVoltageAndCoefficients
  {
    set
    {
      estimatedLine.Values = makePointsFromCoefficients(value[0], value[1], value[2]);

      initializing = true;
      double[] yVal = {
        value[0],
        (Math.Pow(MIN_AFLOW / value[2], 1d / value[1]) + 1) * value[0],
        (Math.Pow(MID_AFLOW / value[2], 1d / value[1]) + 1) * value[0],
        (Math.Pow(MAX_AFLOW / value[2], 1d / value[1]) + 1) * value[0]
      };

      measuredPoints[0].Y = yVal[0];
      measuredPoints[1].Y = yVal[1];
      measuredPoints[2].Y = yVal[2];
      measuredPoints[3].Y = yVal[3];

      eVolRef.Text = yVal[0].ToString();      
      eVol1.Text = yVal[1].ToString("F3");
      eVol2.Text = yVal[2].ToString("F3");
      eVol3.Text = yVal[3].ToString("F3");
      initializing = false;

      stopUpdatingChart = true;
      coefA.Text = value[1].ToString("F3");
      coefB.Text = value[2].ToString("F3");
      stopUpdatingChart = false;

      coefA.TextColor = coefB.TextColor = Colors.Black;
    }
  }

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

  private LineSeries<ObservablePoint> estimatedLine;

  /// <summary>�v�����ꂽ�_</summary>
  private ObservablePoint[] measuredPoints = new ObservablePoint[4];

  /// <summary>�v�����̓d��</summary>
  private ObservablePoint[] voltagePoints = [new ObservablePoint(0.0, 1.0), new ObservablePoint(MAX_AFLOW + 0.2, 1.0)];

  #endregion

  #region �R���X�g���N�^

  /// <summary>�C���X�^���X������������</summary>
  public VelocityCalibrator2()
  {
    InitializeComponent();

    initChart();

    //�߂�{�^���őJ�ڂ���ꍇ�̏���
    Shell.Current.Navigated += Current_Navigated;
  }

  private void Current_Navigated(object sender, ShellNavigatedEventArgs e)
  {
    if (e.Source == ShellNavigationSource.Pop)
    {
      Task.Run(async () =>
      {
        try
        {
          //�����Z���I���R�}���h���M
          int tryNum = 0;
          Logger.HasEndCalibratingVoltageMessageReceived = false;
          while (!Logger.HasEndCalibratingVoltageMessageReceived)
          {
            //5�񎸔s������G���[�\��
            if (5 <= tryNum)
            {
              Application.Current.Dispatcher.Dispatch(() =>
              {
                DisplayAlert("Alert", MLSResource.CR_ConnectionFailed, "OK");
              });
              return;
            }
            tryNum++;

            //�I���R�}���h�𑗐M
            MLUtility.ConnectedXBee.SendSerialData
            (Encoding.ASCII.GetBytes(MLogger.MakeEndCalibratingVoltageCommand()));

            await Task.Delay(500);
          }
        }
        catch { }
      });

      Logger.CalibratingVoltageReceivedEvent -= Logger_CalibratingVoltageReceivedEvent;
      Shell.Current.Navigated -= Current_Navigated; //�C�x���g����
    }
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    isFirstVoltageMessage = true;
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

    //���ϓd�������肵�����ۂ��̔���i���ݒl�ƕ��ς̌덷<0.005V ���� �ߋ�AVE_TIME�X�e�b�v�Ƃ̌덷<0.01V�j
    bool isStabled = true;
    if (Math.Abs(Logger.VelocityVoltage - aveVol) < 0.005)
    {
      for (int i = 0; i < AVE_TIME - 1; i++)
      {
        if (0.01 < Math.Abs(velVols[i] - aveVol))
        {
          isStabled = false;
          break;
        }
      }
    }
    else isStabled = false;

    //�d���\�����X�V
    Application.Current.Dispatcher.Dispatch(() =>
    {
      double velV = Logger.VelocityVoltage;
      instVoltage.Text = "(" + Logger.VelocityVoltage.ToString("F3") + ")";
      aveVoltage.Text = aveVol.ToString("F3");

      instVoltage.TextColor = aveVoltage.TextColor = voltUnit.TextColor 
        = isStabled ? Colors.Green : Colors.Red;

      voltagePoints[0].Y = voltagePoints[1].Y = Math.Max(1.41, Math.Min(1.99, aveVol)); //�\�����1.4 - 2.0V�ɂ܂�߂�
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
          MaxLimit = MAX_AFLOW + 0.1,
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
          MaxLimit = 1.9,
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

    //�_:�����ɏ��������̂ňӖ��͖������B
    measuredPoints = [
        new ObservablePoint(0.0, 1.450),
        new ObservablePoint(MIN_AFLOW, 1.522),
        new ObservablePoint(MID_AFLOW, 1.572),
        new ObservablePoint(MAX_AFLOW, 1.639)
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
      Values = makePointsFromCoefficients(1.51, 2.730, 128.0),  //100��Z���̕��ϒl
      Stroke = new SolidColorPaint(SKColors.LightGray) { StrokeThickness = 3 },
      Fill = null, //�����h��Ԃ�
      GeometryFill = null, //�v���b�g�h��Ԃ�
      GeometryStroke = null, //�v���b�g�g��
      LineSmoothness = 0.5, //���Ȑ�
    };

    //�����W������Z�o������
    estimatedLine = new LineSeries<ObservablePoint>
    {
      Values = makePointsFromCoefficients(1.51, 2.730, 128.0),  //100��Z���̕��ϒl

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
  /// <returns></returns>
  private static List<ObservablePoint> makePointsFromCoefficients(
    double minV, double coefA, double coefB)
  {
    double maxV = 1.9;
    List<ObservablePoint> points = new List<ObservablePoint>();
    double cV = minV;
    while (cV < maxV)
    {
      double vN = (cV / minV) - 1.0;
      double vel = Math.Pow(vN, coefA) * coefB;
      points.Add(new ObservablePoint(vel, cV));
      if (MAX_AFLOW + 0.1 < vel) break;
      cV += 0.01;
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
    //���������͖���
    if (initializing) return;

    //�d�����擾
    if (!double.TryParse(eVolRef.Text, out double volRef)) return;
    if (!double.TryParse(eVol1.Text, out double vol1)) return;
    if (!double.TryParse(eVol2.Text, out double vol2)) return;
    if (!double.TryParse(eVol3.Text, out double vol3)) return;

    //�ُ�Ȓl�̏ꍇ�ɂ͖���
    if (volRef < 1.0 || 2.0 < volRef) return;
    if (vol1 < 1.0 || vol1 < volRef) return;
    if (vol2 < 1.0 || vol2 < volRef) return;
    if (vol3 < 1.0 || vol3 < volRef) return;

    //�`���[�g�\�����X�V
    if (sender == eVolRef) measuredPoints[0].Y = volRef;
    if (sender == eVol1) measuredPoints[1].Y = vol1;
    if (sender == eVol2) measuredPoints[2].Y = vol2;
    if (sender == eVol3) measuredPoints[3].Y = vol3;

    //�d������W���𐄒�
    EstimateCoefs(
      MIN_AFLOW, MID_AFLOW, MAX_AFLOW, 
      volRef, vol1, vol2, vol3, 
      out double cfA, out double cfB);

    stopUpdatingChart = true;
    coefA.Text = cfA.ToString("F3");
    coefB.Text = cfB.ToString("F3");
    stopUpdatingChart = false;

    coefA.TextColor = coefB.TextColor = Colors.Red;

    //�ĕ`��
    estimatedLine.Values = makePointsFromCoefficients(volRef, cfA, cfB);
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

    coefA.TextColor = coefB.TextColor = Colors.Red;
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

    ((Entry)sender).TextColor = Colors.Red;

    estimatedLine.Values = makePointsFromCoefficients(volRef, cfA, cfB);
  }

  #endregion

  private void UpdateCoefficientButton_Clicked(object sender, EventArgs e)
  {
    if (!double.TryParse(eVolRef.Text, out double volRef)) return;
    if (!double.TryParse(coefA.Text, out double cfA)) return;
    if (!double.TryParse(coefB.Text, out double cfB)) return;

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
            MLogger.MakeVelocityCharateristicsSettingCommand(volRef, cfA, cfB, 0.0)));

          await Task.Delay(500);
        }

        //�J�n�ɐ��������甽�f
        Application.Current.Dispatcher.Dispatch(() =>
        {
          //���M���O
          MLUtility.WriteLog(Logger.XBeeName + "; Velocity coefficient changed; " +
            Logger.LowAddress + "; " +
            "min. voltage=" + Logger.VelocityMinVoltage.ToString("F3") + "; " +
            "coef. A=" + Logger.VelocityCharacteristicsCoefA.ToString("F3") + "; " +
            "coef. B=" + Logger.VelocityCharacteristicsCoefB.ToString("F3") + "; " +
            "coef. C=" + Logger.VelocityCharacteristicsCoefC.ToString("F3") + "; "
            );

          stopUpdatingChart = true;
          coefA.Text = Logger.VelocityCharacteristicsCoefA.ToString("F3");
          coefB.Text = Logger.VelocityCharacteristicsCoefB.ToString("F3");
          coefA.TextColor = coefB.TextColor = Colors.Black;
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

  /// <summary>�v���l3�_���畗�ʂƓd���̊֌W���̌W�����v�Z����</summary>
  /// <remarks>
  /// vel = B * vtg_n^2 + C * vtg_n
  /// vtg_n = vtg / refVtg - 1.0
  /// </remarks>
  /// <param name="vel1">����1[m/s]</param>
  /// <param name="vel2">����2[m/s]</param>
  /// <param name="vel3">����3[m/s]</param>
  /// <param name="refVtg">0m/s�̊�d��[V]</param>
  /// <param name="vtg1">����1�ɑ΂���d��[V]</param>
  /// <param name="vtg2">����2�ɑ΂���d��[V]</param>
  /// <param name="vtg3">����3�ɑ΂���d��[V]</param>
  /// <param name="cfB">�o��:�W��B</param>
  /// <param name="cfC">�o��:�W��C</param>
  /// <returns>�W�����肪�����������ۂ�</returns>
  public static bool EstimateCoefs(
    double vel1, double vel2, double vel3,
    double refVtg, double vtg1, double vtg2, double vtg3,
    out double cfA, out double cfB)
  {
    cfA = cfB = 0.0;
    if (vtg1 < refVtg || vtg2 < refVtg || vtg3 < refVtg) return false;

    double x1 = Math.Log(vtg1 / refVtg - 1);
    double x2 = Math.Log(vtg2 / refVtg - 1);
    double x3 = Math.Log(vtg3 / refVtg - 1);
    double y1 = Math.Log(vel1);
    double y2 = Math.Log(vel2);
    double y3 = Math.Log(vel3);

    double aveX = (x1 + x2 + x3) / 3d;
    double aveY = (y1 + y2 + y3) / 3d;

    cfA = ((x1 - aveX) * (y1 - aveY) + (x2 - aveX) * (y2 - aveY) + (x3 - aveX) * (y3 - aveY)) 
      / (Math.Pow(x1 - aveX, 2) + Math.Pow(x2 - aveX, 2) + Math.Pow(x3 - aveX, 2));
    cfB = Math.Exp(aveY - aveX * cfA);

    return true;
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
