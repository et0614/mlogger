namespace MLS_Mobile;

using System.Collections.ObjectModel;
using Popolo.ThermophysicalProperty;
using MLS_Mobile.Resources.i18n;

public partial class MoistAirCalculator : ContentPage
{

  #region インスタンス変数・プロパティ・定数宣言

  private readonly ObservableCollection<string> pairs = new ObservableCollection<string>();

  #endregion

  public MoistAirCalculator()
	{
		InitializeComponent();

    pairs.Add(MLSResource.MA_DbtAndRhmd); //0
    pairs.Add(MLSResource.MA_DbtAndAhmd);
    pairs.Add(MLSResource.MA_DbtAndWbt);
    pairs.Add(MLSResource.MA_DbtAndEnth);
    pairs.Add(MLSResource.MA_DbtAndDens);

    pairs.Add(MLSResource.MA_RhmdAndAhmd); //5
    pairs.Add(MLSResource.MA_RhmdAndWbt);
    pairs.Add(MLSResource.MA_RhmdAndEnth);
    pairs.Add(MLSResource.MA_RhmdAndDens);

    pairs.Add(MLSResource.MA_AhmdAndWbt); //9
    pairs.Add(MLSResource.MA_AhmdAndEnth);
    pairs.Add(MLSResource.MA_AhmdAndDens);

    pairs.Add(MLSResource.MA_WbtAndDens); //12

    pairList.ItemsSource = pairs;

    //選択を初期化
    if (pairList.SelectedIndex == -1)
      pairList.SelectedIndex = 0;
    updateValue();
  }

  private void slider_ValueChanged(object sender, ValueChangedEventArgs e)
  {
    updateValue();
  }

  private void pairList_SelectedIndexChanged(object sender, EventArgs e)
  {
    //非選択の場合は無視
    if (pairList.SelectedIndex == -1) return;

    //スライダの有効・無効設定
    int slc = pairList.SelectedIndex;
    dbtSlider.IsEnabled = slc <= 4;
    rhmdSlider.IsEnabled = (slc == 0) | (5 <= slc && slc <= 8);
    ahmdSlider.IsEnabled = (slc == 1) | (slc == 5) | (9 <= slc && slc <= 11);
    wbtSlider.IsEnabled = (slc == 2) | (slc == 6) | (slc == 9) | (slc == 12);
    entSlider.IsEnabled = (slc == 3) | (slc == 7) | (slc == 10);
    dnsSlider.IsEnabled = (slc == 4) | (slc == 8) | (slc == 11) | (slc == 12);

    //入力変数は黒、出力変数は緑
    FontAttributes non = FontAttributes.None;
    FontAttributes bld = FontAttributes.Bold;
    dbtLabel.FontAttributes = dbtSlider.IsEnabled ? bld : non;
    rhmdLabel.FontAttributes = rhmdSlider.IsEnabled ? bld : non;
    ahmdLabel.FontAttributes = ahmdSlider.IsEnabled ? bld : non;
    wbtLabel.FontAttributes = wbtSlider.IsEnabled ? bld : non;
    entLabel.FontAttributes = entSlider.IsEnabled ? bld : non;
    dnsLabel.FontAttributes = dnsSlider.IsEnabled ? bld : non;
  }

  private void updateValue()
  {
    double dbt, ahmd, svol;
    if (pairList == null) return;
    switch (pairList.SelectedIndex)
    {
      case 0: //乾球温度と相対湿度
        dbt = dbtSlider.Value;
        ahmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndRelativeHumidity(dbt, rhmdSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = rhmdSlider.Value.ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        svol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value);
        dnsLabel.Text = (1.0 / svol).ToString("F2");
        break;

      case 1: //乾球温度と絶対湿度
        dbt = dbtSlider.Value;
        ahmd = 0.001 * ahmdSlider.Value;
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = Math.Max(0, Math.Min(100, MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value))).ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        svol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value);
        dnsLabel.Text = (1.0 / svol).ToString("F2");
        break;

      case 2: //乾球温度と湿球温度
        dbt = dbtSlider.Value;
        ahmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndWetBulbTemperature(dbtSlider.Value, wbtSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = Math.Max(0, Math.Min(100, MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value))).ToString("F1");
        wbtLabel.Text = wbtSlider.Value.ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        svol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value);
        dnsLabel.Text = (1.0 / svol).ToString("F2");
        break;

      case 3: //乾球温度と比エンタルピー
        dbt = dbtSlider.Value;
        ahmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndEnthalpy(dbtSlider.Value, entSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = Math.Max(0, Math.Min(100, MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value))).ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = entSlider.Value.ToString("F1");
        svol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value);
        dnsLabel.Text = (1.0 / svol).ToString("F2");
        break;

      case 4: //乾球温度と比重量
        dbt = dbtSlider.Value;
        ahmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndSpecificVolume(dbtSlider.Value, 1.0 / dnsSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = Math.Max(0, Math.Min(100, MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value))).ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        dnsLabel.Text = dnsSlider.Value.ToString("F2");
        break;

      case 5: //相対湿度と絶対湿度
        ahmd = 0.001 * ahmdSlider.Value;
        dbt = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndRelativeHumidity(ahmd, rhmdSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = ahmdSlider.Value.ToString("F1");
        rhmdLabel.Text = rhmdSlider.Value.ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        svol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value);
        dnsLabel.Text = (1.0 / svol).ToString("F2");
        break;

      case 6: //相対湿度と湿球温度
        dbt = MoistAir.GetDryBulbTemperatureFromWetBulbTemperatureAndRelativeHumidity(wbtSlider.Value, rhmdSlider.Value, atmSlider.Value);
        ahmd = MoistAir.GetHumidityRatioFromWetBulbTemperatureAndRelativeHumidity(wbtSlider.Value, rhmdSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = rhmdSlider.Value.ToString("F1");
        wbtLabel.Text = wbtSlider.Value.ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        svol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value);
        dnsLabel.Text = (1.0 / svol).ToString("F2");
        break;

      case 7: //相対湿度と比エンタルピー
        dbt = MoistAir.GetDryBulbTemperatureFromEnthalpyAndRelativeHumidity(entSlider.Value, rhmdSlider.Value, atmSlider.Value);
        ahmd = MoistAir.GetHumidityRatioFromEnthalpyAndRelativeHumidity(entSlider.Value, rhmdSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = rhmdSlider.Value.ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = entSlider.Value.ToString("F1");
        svol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value);
        dnsLabel.Text = (1.0 / svol).ToString("F2");
        break;

      case 8: //相対湿度と比重量
        dbt = MoistAir.GetDryBulbTemperatureFromRelativeHumidityAndSpecificVolume(rhmdSlider.Value, 1.0 / dnsSlider.Value, atmSlider.Value);
        ahmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndSpecificVolume(dbt, 1.0 / dnsSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = rhmdSlider.Value.ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        dnsLabel.Text = dnsSlider.Value.ToString("F2");
        break;

      case 9: //絶対湿度と湿球温度
        ahmd = 0.001 * ahmdSlider.Value;
        dbt = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndWetBulbTemperature(ahmd, wbtSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = ahmdSlider.Value.ToString("F1");
        rhmdLabel.Text = Math.Max(0, Math.Min(100, MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value))).ToString("F1");
        wbtLabel.Text = wbtSlider.Value.ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        svol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value);
        dnsLabel.Text = (1.0 / svol).ToString("F2");
        break;

      case 10: //絶対湿度と比エンタルピー
        ahmd = 0.001 * ahmdSlider.Value;
        dbt = MoistAir.GetDryBulbTemperatureFromHumidityRatioAndEnthalpy(ahmd, entSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = ahmdSlider.Value.ToString("F1");
        rhmdLabel.Text = Math.Max(0, Math.Min(100, MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value))).ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = entSlider.Value.ToString("F1");
        svol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value);
        dnsLabel.Text = (1.0 / svol).ToString("F2");
        break;

      case 11: //絶対湿度と比重量
        ahmd = 0.001 * ahmdSlider.Value;
        dbt = MoistAir.GetDryBulbTemperatureFromSpecificVolumeAndHumidityRatio(1.0 / dnsSlider.Value, ahmd, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = ahmdSlider.Value.ToString("F1");
        rhmdLabel.Text = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        dnsLabel.Text = dnsSlider.Value.ToString("F2");
        break;

      /*case 12: //湿球温度と比エンタルピー
        dbt = MoistAir.GetDryBulbTemperatureFromWetBulbTemperatureAndEnthalpy(wbtSlider.Value, entSlider.Value, atmSlider.Value);
        ahmd = MoistAir.GetHumidityRatioFromWetBulbTemperatureAndEnthalpy(wbtSlider.Value, entSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = Math.Max(0, Math.Min(100, MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value))).ToString("F1");
        wbtLabel.Text = wbtSlider.Value.ToString("F1");
        entLabel.Text = entSlider.Value.ToString("F1");
        svol = MoistAir.GetSpecificVolumeFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value);
        dnsLabel.Text = (1.0 / svol).ToString("F2");
        break;*/

      case 12: //湿球温度と比重量
        dbt = MoistAir.GetDryBulbTemperatureFromWetBulbTemperatureAndSpecificVolume(wbtSlider.Value, 1.0 / dnsSlider.Value, atmSlider.Value);
        ahmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndSpecificVolume(dbt, 1.0 / dnsSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = Math.Max(0, Math.Min(100, MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value))).ToString("F1");
        wbtLabel.Text = wbtSlider.Value.ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        dnsLabel.Text = dnsSlider.Value.ToString("F2");
        break;

    }
  }

  private void Button_Clicked(object sender, EventArgs e)
  {
    Button target = (Button)sender;

    pairList.SelectedIndex = 0;
    dbtSlider.Value = 
      (target == btnSummerIndoor ? 26 : 
      (target == btnWinterIndoor ? 22 : 
      (target == btnSummerOutdoor ? 35 : 2)));
    rhmdSlider.Value =
      (target == btnSummerIndoor ? 50 :
      (target == btnWinterIndoor ? 40 :
      (target == btnSummerOutdoor ? 70 : 40)));
    atmSlider.Value = 101.3;
    updateValue();
  }

}