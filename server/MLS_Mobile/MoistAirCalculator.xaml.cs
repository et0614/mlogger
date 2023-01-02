namespace MLS_Mobile;

using System.Collections.ObjectModel;
using Popolo.ThermophysicalProperty;
using MLS_Mobile.Resources.i18n;

public partial class MoistAirCalculator : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B�E�萔�錾

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

    //�I����������
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
    //��I���̏ꍇ�͖���
    if (pairList.SelectedIndex == -1) return;

    //�X���C�_�̗L���E�����ݒ�
    int slc = pairList.SelectedIndex;
    dbtSlider.IsEnabled = slc <= 4;
    rhmdSlider.IsEnabled = (slc == 0) | (5 <= slc && slc <= 8);
    ahmdSlider.IsEnabled = (slc == 1) | (slc == 5) | (9 <= slc && slc <= 11);
    wbtSlider.IsEnabled = (slc == 2) | (slc == 6) | (slc == 9) | (slc == 12);
    entSlider.IsEnabled = (slc == 3) | (slc == 7) | (slc == 10);
    dnsSlider.IsEnabled = (slc == 4) | (slc == 8) | (slc == 11) | (slc == 12);

    //���͕ϐ��͍��A�o�͕ϐ��͗�
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
      case 0: //�������x�Ƒ��Ύ��x
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

      case 1: //�������x�Ɛ�Ύ��x
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

      case 2: //�������x�Ǝ������x
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

      case 3: //�������x�Ɣ�G���^���s�[
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

      case 4: //�������x�Ɣ�d��
        dbt = dbtSlider.Value;
        ahmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndSpecificVolume(dbtSlider.Value, 1.0 / dnsSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = Math.Max(0, Math.Min(100, MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value))).ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        dnsLabel.Text = dnsSlider.Value.ToString("F2");
        break;

      case 5: //���Ύ��x�Ɛ�Ύ��x
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

      case 6: //���Ύ��x�Ǝ������x
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

      case 7: //���Ύ��x�Ɣ�G���^���s�[
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

      case 8: //���Ύ��x�Ɣ�d��
        dbt = MoistAir.GetDryBulbTemperatureFromRelativeHumidityAndSpecificVolume(rhmdSlider.Value, 1.0 / dnsSlider.Value, atmSlider.Value);
        ahmd = MoistAir.GetHumidityRatioFromDryBulbTemperatureAndSpecificVolume(dbt, 1.0 / dnsSlider.Value, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = (1000 * ahmd).ToString("F1");
        rhmdLabel.Text = rhmdSlider.Value.ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        dnsLabel.Text = dnsSlider.Value.ToString("F2");
        break;

      case 9: //��Ύ��x�Ǝ������x
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

      case 10: //��Ύ��x�Ɣ�G���^���s�[
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

      case 11: //��Ύ��x�Ɣ�d��
        ahmd = 0.001 * ahmdSlider.Value;
        dbt = MoistAir.GetDryBulbTemperatureFromSpecificVolumeAndHumidityRatio(1.0 / dnsSlider.Value, ahmd, atmSlider.Value);
        dbtLabel.Text = dbt.ToString("F1");
        ahmdLabel.Text = ahmdSlider.Value.ToString("F1");
        rhmdLabel.Text = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        wbtLabel.Text = MoistAir.GetWetBulbTemperatureFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd, atmSlider.Value).ToString("F1");
        entLabel.Text = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(dbt, ahmd).ToString("F1");
        dnsLabel.Text = dnsSlider.Value.ToString("F2");
        break;

      /*case 12: //�������x�Ɣ�G���^���s�[
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

      case 12: //�������x�Ɣ�d��
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