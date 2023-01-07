namespace MLS_Mobile;

using System.Text;
using System.Threading.Tasks;

using MLLib;
using MLS_Mobile.Resources.i18n;
using Microsoft.Maui.Controls;

public partial class CFSetting : ContentPage
{

  #region インスタンス変数・プロパティ

  private bool isEdited = false;

  #endregion

  #region コンストラクタ

  /// <summary>インスタンスを初期化する</summary>
  public CFSetting()
  {
    InitializeComponent();

    cV_dbt.Text = cV_glb.Text = cV_hmd.Text = cV_lux.Text = cV_vel.Text = MLSResource.CF_CorrectionValue;
    ms_dbt.Text = ms_glb.Text = ms_hmd.Text = ms_lux.Text = ms_vel.Text = MLSResource.CF_Measurement;

    lbl_dbt.Text = MLSResource.DrybulbTemperature;
    lbl_hmd.Text = MLSResource.RelativeHumidity;
    lbl_glb.Text = MLSResource.GlobeTemperature;
    lbl_vel.Text = MLSResource.Velocity;
    lbl_lux.Text = MLSResource.Illuminance;

    vel_voltage.Text = MLSResource.CF_VelocityVoltage;

    applyCorrectionFactors();
  }

  #endregion

  #region Entry操作時の処理

  private void dbt_TextChanged(object sender, TextChangedEventArgs e)
  {
    lbl_dbt.TextColor = Colors.Red;
    isEdited = true;
  }

  private void hmd_TextChanged(object sender, TextChangedEventArgs e)
  {
    lbl_hmd.TextColor = Colors.Red;
    isEdited = true;
  }

  private void glb_TextChanged(object sender, TextChangedEventArgs e)
  {
    lbl_glb.TextColor = Colors.Red;
    isEdited = true;
  }

  private void vel_TextChanged(object sender, TextChangedEventArgs e)
  {
    lbl_vel.TextColor = Colors.Red;
    isEdited = true;
  }

  private void lux_TextChanged(object sender, TextChangedEventArgs e)
  {
    lbl_lux.TextColor = Colors.Red;
    isEdited = true;
  }

  #endregion

  #region ボタンクリックイベント発生時の処理

  private void Load_Clicked(object sender, EventArgs e)
  {
    loadCorrectionFactors();
  }

  private void Save_Clicked(object sender, EventArgs e)
  {
    if (!isEdited) return;

    //値が適正か、確認する
    bool hasError = false;
    string errMsg = "";

    if (!double.TryParse(cA_dbt.Text, out double dbtA))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "A", MLSResource.DrybulbTemperature) + Environment.NewLine;
    }
    if (!double.TryParse(cB_dbt.Text, out double dbtB))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "B", MLSResource.DrybulbTemperature) + Environment.NewLine;
    }

    if (!double.TryParse(cA_hmd.Text, out double hmdA))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "A", MLSResource.RelativeHumidity) + Environment.NewLine;
    }
    if (!double.TryParse(cB_hmd.Text, out double hmdB))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "B", MLSResource.RelativeHumidity) + Environment.NewLine;
    }

    if (!double.TryParse(cA_glb.Text, out double glbA))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "A", MLSResource.GlobeTemperature) + Environment.NewLine;
    }
    if (!double.TryParse(cB_glb.Text, out double glbB))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "B", MLSResource.GlobeTemperature) + Environment.NewLine;
    }

    if (!double.TryParse(cA_vel.Text, out double velA))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "A", MLSResource.RelativeAirVelocity) + Environment.NewLine;
    }
    if (!double.TryParse(cB_vel.Text, out double velB))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "B", MLSResource.RelativeAirVelocity) + Environment.NewLine;
    }
    if (!double.TryParse(vel_0V.Text, out double velV))
    {
      hasError = true;
      errMsg += MLSResource.CF_InvalidVelocity1 + Environment.NewLine;
    }
    else if (velV <= 0)
    {
      hasError = true;
      errMsg += MLSResource.CF_InvalidVelocity2 + Environment.NewLine;
    }

    if (!double.TryParse(cA_lux.Text, out double luxA))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "A", MLSResource.Illuminance) + Environment.NewLine;
    }
    if (!double.TryParse(cB_lux.Text, out double luxB))
    {
      hasError = true;
      errMsg += String.Format(MLSResource.CF_Invalid, "B", MLSResource.Illuminance) + Environment.NewLine;
    }

    if (hasError) DisplayAlert("Alert", errMsg, "OK");
    else 
      saveCorrectionFactors(MLogger.MakeCorrectionFactorsSettingCommand
          (dbtA, dbtB, hmdA, hmdB, glbA, glbB, luxA, luxB, velA, velB, velV));
  }

  private void saveCorrectionFactors(string command)
  {
    MLUtility.Logger.HasCorrectionFactorsReceived = false;

    //インジケータ表示
    showIndicator(MLSResource.CF_Setting);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        while (!MLUtility.Logger.HasCorrectionFactorsReceived)
        {
          //5回失敗したらエラー表示
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.CF_FailSetting, "OK");
              return;
            });
          }
          tryNum++;

          //開始コマンドを送信
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(command));

          await Task.Delay(500);
        }

        //更新された情報を反映
        Application.Current.Dispatcher.Dispatch(() =>
        {
          applyCorrectionFactors();
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

  private void loadCorrectionFactors()
  {
    MLUtility.Logger.HasCorrectionFactorsReceived = false;

    //インジケータ表示
    showIndicator(MLSResource.CF_Setting);

    Task.Run(async () =>
    {
      try
      {
        int tryNum = 0;
        while (!MLUtility.Logger.HasCorrectionFactorsReceived)
        {
          //5回失敗したらエラー表示
          if (5 <= tryNum)
          {
            Application.Current.Dispatcher.Dispatch(() =>
            {
              DisplayAlert("Alert", MLSResource.CF_FailSetting, "OK");
              return;
            });
          }
          tryNum++;

          //開始コマンドを送信
          MLUtility.LoggerSideXBee.SendSerialData(Encoding.ASCII.GetBytes(MLogger.MakeLoadCorrectionFactorsCommand()));

          await Task.Delay(500);
        }

        //更新された情報を反映
        Application.Current.Dispatcher.Dispatch(() =>
        {
          applyCorrectionFactors();
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

  private void applyCorrectionFactors()
  {
    cA_dbt.Text = MLUtility.Logger.DrybulbTemperature.CorrectionFactorA.ToString("F3");
    cB_dbt.Text = MLUtility.Logger.DrybulbTemperature.CorrectionFactorB.ToString("F2");

    cA_hmd.Text = MLUtility.Logger.RelativeHumdity.CorrectionFactorA.ToString("F3");
    cB_hmd.Text = MLUtility.Logger.RelativeHumdity.CorrectionFactorB.ToString("F2");

    cA_glb.Text = MLUtility.Logger.GlobeTemperature.CorrectionFactorA.ToString("F3");
    cB_glb.Text = MLUtility.Logger.GlobeTemperature.CorrectionFactorB.ToString("F2");

    cA_vel.Text = MLUtility.Logger.Velocity.CorrectionFactorA.ToString("F3");
    cB_vel.Text = MLUtility.Logger.Velocity.CorrectionFactorB.ToString("F3");
    vel_0V.Text = MLUtility.Logger.VelocityMinVoltage.ToString("F3");

    cA_lux.Text = MLUtility.Logger.Illuminance.CorrectionFactorA.ToString("F3");
    cB_lux.Text = MLUtility.Logger.Illuminance.CorrectionFactorB.ToString("F0");

    resetLabelColor();

    isEdited = false;
    hideIndicator();
  }

  private void resetLabelColor()
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      lbl_dbt.TextColor = lbl_hmd.TextColor = lbl_glb.TextColor = lbl_vel.TextColor = lbl_lux.TextColor = Colors.Black;
    });
    isEdited = false;
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