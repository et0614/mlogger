using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using XBeeLibrary.Xamarin;
using XBeeLibrary.Core.Events.Relay;

using MLServer;

using Plugin.NetStandardStorage;
using Plugin.NetStandardStorage.Abstractions.Types;
using Plugin.NetStandardStorage.Abstractions.Interfaces;

using MLS_Mobile.Resources;

namespace MLS_Mobile
{
  [XamlCompilation(XamlCompilationOptions.Compile)]
  public partial class CFSetting : ContentPage
  {

    #region インスタンス変数・プロパティ

    /// <summary>初期化完了フラグ</summary>
    private bool isInitialized = false;

    public MLogger Logger { get; set; }

    private bool isEdited = false;

    /// <summary>XBeeを設定・取得する</summary>
    public ZigBeeBLEDevice MLXBee { get; set; }

    #endregion


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

      btnBack.Text = MLSResource.CF_Back;
      btnSet.Text = MLSResource.CF_Set;
    }

    protected override void OnAppearing()
    {
      base.OnAppearing();

      //イベント登録      
      MLXBee.SerialDataReceived += MLXBee_SerialDataReceived;

      //補正係数読み込み処理
      showIndicator("補正係数読込中");
      Task.Run(async () =>
      {
        try
        {
          //補正係数読み込みコマンド
          MLXBee.SendSerialData(Encoding.ASCII.GetBytes("\rLCF\r"));

          await Task.Delay(5000);
          if (!isInitialized)
          {
            Device.BeginInvokeOnMainThread(() =>
            {
              DisplayAlert("Alert", "補正係数の読込に失敗しました", "OK");
              Navigation.PopAsync();
            });
          }
        }
        catch (Exception ex)
        {
          Device.BeginInvokeOnMainThread(() =>
          {
            DisplayAlert("Alert", ex.Message, "OK");
            Navigation.PopAsync();
          });
        }
      });
    }

    protected override void OnDisappearing()
    {
      base.OnDisappearing();

      //イベント解除
      MLXBee.SerialDataReceived -= MLXBee_SerialDataReceived;
    }

    #region Entry操作時の処理

    private void dbt_TextChanged(object sender, TextChangedEventArgs e)
    {
      lbl_dbt.TextColor = Color.Red;
      isEdited = true;
    }

    private void hmd_TextChanged(object sender, TextChangedEventArgs e)
    {
      lbl_hmd.TextColor = Color.Red;
      isEdited = true;
    }

    private void glb_TextChanged(object sender, TextChangedEventArgs e)
    {
      lbl_glb.TextColor = Color.Red;
      isEdited = true;
    }

    private void vel_TextChanged(object sender, TextChangedEventArgs e)
    {
      lbl_vel.TextColor = Color.Red;
      isEdited = true;
    }

    private void lux_TextChanged(object sender, TextChangedEventArgs e)
    {
      lbl_lux.TextColor = Color.Red;
      isEdited = true;
    }

    #endregion

    #region ボタンクリックイベント発生時の処理

    private async void Cancel_Clicked(object sender, EventArgs e)
    {
      if (isEdited)
      {
        if (await DisplayAlert("Alert", "設定を破棄して良いですか？", "OK", "Cancel"))
          await Navigation.PopAsync();
      }
      else await Navigation.PopAsync();
    }

    private void Set_Clicked(object sender, EventArgs e)
    {
      if (!isEdited) return;

      //値が適正か、確認する
      bool hasError = false;
      string errMsg = "";
      
      if (!double.TryParse(cA_dbt.Text, out double dbtA))
      {
        hasError = true;
        errMsg += "乾球温度補正係数Aが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(cB_dbt.Text, out double dbtB))
      {
        hasError = true;
        errMsg += "乾球温度補正係数Bが不正です" + Environment.NewLine;
      }

      if (!double.TryParse(cA_hmd.Text, out double hmdA))
      {
        hasError = true;
        errMsg += "相対湿度補正係数Aが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(cB_hmd.Text, out double hmdB))
      {
        hasError = true;
        errMsg += "相対湿度補正係数Bが不正です" + Environment.NewLine;
      }

      if (!double.TryParse(cA_glb.Text, out double glbA))
      {
        hasError = true;
        errMsg += "グローブ温度補正係数Aが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(cB_glb.Text, out double glbB))
      {
        hasError = true;
        errMsg += "グローブ温度補正係数Bが不正です" + Environment.NewLine;
      }

      if (!double.TryParse(cA_vel.Text, out double velA))
      {
        hasError = true;
        errMsg += "微風速補正係数Aが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(cB_vel.Text, out double velB))
      {
        hasError = true;
        errMsg += "微風速補正係数Bが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(vel_0V.Text, out double velV))
      {
        hasError = true;
        errMsg += "微風速計の無風電圧が不正です" + Environment.NewLine;
      }
      else if (velV <= 0)
      {
        hasError = true;
        errMsg += "微風速計の無風電圧は0以下になりません" + Environment.NewLine;
      }

      if (!double.TryParse(cA_lux.Text, out double luxA))
      {
        hasError = true;
        errMsg += "照度補正係数Aが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(cB_lux.Text, out double luxB))
      {
        hasError = true;
        errMsg += "照度補正係数Bが不正です" + Environment.NewLine;
      }

      if (hasError) DisplayAlert("Alert", errMsg, "OK");
      else
      {
        //補正係数設定コマンドを送信
        Task.Run(() =>
        {
          try
          {
            MLXBee.SendSerialData
            (Encoding.ASCII.GetBytes(MLogger.MakeSCFCommand
            (dbtA, dbtB, hmdA, hmdB, glbA, glbB, luxA, luxB, velA, velB, velV)));
          }
          catch (Exception ex)
          {
            Device.BeginInvokeOnMainThread(() =>
            {
              DisplayAlert("Alert", ex.Message, "OK");
              Navigation.PopAsync();
            });
          }
        });
      }
    }

    private void resetLabelColor()
    {
      Device.BeginInvokeOnMainThread(() =>
      {
        lbl_dbt.TextColor = lbl_hmd.TextColor = lbl_glb.TextColor = lbl_vel.TextColor = lbl_lux.TextColor = Color.Black;
      });
      isEdited = false;
    }


    #endregion

    #region 通信処理

    private void MLXBee_SerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
      //受信データを追加
      Logger.AddReceivedData(Encoding.ASCII.GetString(e.Data));

      //コマンドがある限り処理を続ける
      string command;
      while ((command = Logger.GetCommand()) != null)
      {
        if (SolveCommand(command)) Logger.RemoveCommand(); //処理に成功した場合はコマンドを削除
        else break; //処理に失敗した場合には次回に再挑戦
      }
    }

    private bool SolveCommand(string command)
    {
      //補正係数通知
      if (command.StartsWith("SCF") || command.StartsWith("LCF"))
      {
        //補正係数を読み込み
        Logger.LoadCFactors(command);

        //テキストボックスに設定
        Device.BeginInvokeOnMainThread(() =>
        {
          cA_dbt.Text = Logger.CFactorA_Temperature.ToString("F3");
          cB_dbt.Text = Logger.CFactorB_Temperature.ToString("F2");

          cA_hmd.Text = Logger.CFactorA_RHumidity.ToString("F3");
          cB_hmd.Text = Logger.CFactorB_RHumidity.ToString("F2");

          cA_glb.Text = Logger.CFactorA_Globe.ToString("F3");
          cB_glb.Text = Logger.CFactorB_Globe.ToString("F2");

          cA_vel.Text = Logger.CFactorA_Velocity.ToString("F3");
          cB_vel.Text = Logger.CFactorB_Velocity.ToString("F3");
          vel_0V.Text = Logger.MinVoltage_Velocity.ToString("F3");

          cA_lux.Text = Logger.CFactorA_Illuminance.ToString("F3");
          cB_lux.Text = Logger.CFactorB_Illuminance.ToString("F0");
        });

        resetLabelColor();

        isEdited = false;
        isInitialized = true;
        hideIndicator();
      }

      return true;
    }

    #endregion

    #region インジケータの操作

    /// <summary>インジケータを表示する</summary>
    private void showIndicator(string message)
    {
      Device.BeginInvokeOnMainThread(() =>
      {
        indicatorLabel.Text = message;
        grayback.IsVisible = indicator.IsVisible = true;
      });
    }

    /// <summary>インジケータを隠す</summary>
    private void hideIndicator()
    {
      Device.BeginInvokeOnMainThread(() =>
      {
        grayback.IsVisible = indicator.IsVisible = false;
      });
    }

    #endregion

  }
}