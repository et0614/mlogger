using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using System.Collections.ObjectModel;

using Popolo.HumanBody;
using MLS_Mobile.Resources;

namespace MLS_Mobile
{
  [XamlCompilation(XamlCompilationOptions.Compile)]
  public partial class ThermalComfortCalculator : ContentPage
  {

    #region インスタンス変数・プロパティ・定数宣言

    private bool isInitializing = false;

    private readonly ObservableCollection<string> metItems = new ObservableCollection<string>();

    #endregion

    public ThermalComfortCalculator()
    {
      InitializeComponent();

      this.Title = MLSResource.ThermalComfortCalculator;

      dbtTitle.Text = MLSResource.DrybulbTemperature + " [CDB]";
      rhmdTitle.Text = MLSResource.RelativeHumidity + " [%]";
      mrtTitle.Text = MLSResource.MeanRadiantTemperature + " [C]";
      velTitle.Text = MLSResource.RelativeAirVelocity + " [m/s]";
      cloTitle.Text = MLSResource.ClothingUnit + " [clo]";
      metTitle.Text = MLSResource.MetabolicUnit + " [met]";

      //活動量リスト
      metItems.Add(ThermalComfortRes.Rs_Sleeping);
      metItems.Add(ThermalComfortRes.Rs_Reclining);
      metItems.Add(ThermalComfortRes.Rs_Seated);
      metItems.Add(ThermalComfortRes.Rs_Standing);
      metItems.Add(ThermalComfortRes.Walking_09);
      metItems.Add(ThermalComfortRes.Walking_12);
      metItems.Add(ThermalComfortRes.Walking_18);
      metItems.Add(ThermalComfortRes.Of_Seated);
      metItems.Add(ThermalComfortRes.Of_Typing);
      metItems.Add(ThermalComfortRes.Of_FilingSeated);
      metItems.Add(ThermalComfortRes.Of_FilingStanding);
      metItems.Add(ThermalComfortRes.Of_Walking);
      metItems.Add(ThermalComfortRes.Of_Lifting);
      metItems.Add(ThermalComfortRes.Driving_Automobile);
      metItems.Add(ThermalComfortRes.Driving_Aircraft1);
      metItems.Add(ThermalComfortRes.Driving_Aircraft2);
      metItems.Add(ThermalComfortRes.Driving_Aircraft3);
      metItems.Add(ThermalComfortRes.Driving_Heavy);
      metItems.Add(ThermalComfortRes.Cooling);
      metItems.Add(ThermalComfortRes.HouseCleaning);
      metItems.Add(ThermalComfortRes.HeavyLimbMovement);
      metItems.Add(ThermalComfortRes.Mw_Sawing);
      metItems.Add(ThermalComfortRes.Mw_Light);
      metItems.Add(ThermalComfortRes.Mw_Heavy);
      metItems.Add(ThermalComfortRes.Mw_HandlingBags);
      metItems.Add(ThermalComfortRes.Mw_ShovelWork);
      metItems.Add(ThermalComfortRes.Dancing);
      metItems.Add(ThermalComfortRes.Exercise);
      metItems.Add(ThermalComfortRes.Tennis);
      metItems.Add(ThermalComfortRes.Basketball);
      metItems.Add(ThermalComfortRes.Wrestling);
      metList.ItemsSource = metItems;
    }

    protected override void OnAppearing()
    {
      base.OnAppearing();

      updateIndices();
    }

    private void slider_ValueChanged(object sender, ValueChangedEventArgs e)
    {
      updateIndices();
    }

    private void updateIndices()
    {
      double dbt = dbtSlider.Value;
      double hmd = hmdSlider.Value;
      double mrt = mrtSlider.Value;
      double vel = velSlider.Value;
      double clo = cloSlider.Value;
      double met = metSlider.Value;

      double pmv = ThermalComfort.GetPMV(dbt, mrt, hmd, vel, clo, met, 0);
      double ppd = ThermalComfort.GetPPD(pmv);
      double setstar = TwoNodeModel.GetSETStarFromAmbientCondition(dbt, mrt, hmd, vel, clo, 58 * met, 0);

      lblPMV.Text = pmv.ToString("F2");
      lblPPD.Text = ppd.ToString("F1");
      lblSET.Text = setstar.ToString("F2");
    }

    private void metList_SelectedIndexChanged(object sender, EventArgs e)
    {
      if (isInitializing) return;

      string sItem = (string)metList.SelectedItem;

      if (sItem == ThermalComfortRes.Rs_Sleeping) metSlider.Value = 0.7;
      else if (sItem == ThermalComfortRes.Rs_Reclining) metSlider.Value = 0.8;
      else if (sItem == ThermalComfortRes.Rs_Seated) metSlider.Value = 1.0;
      else if (sItem == ThermalComfortRes.Rs_Standing) metSlider.Value = 1.2;
      else if (sItem == ThermalComfortRes.Walking_09) metSlider.Value = 2.0;
      else if (sItem == ThermalComfortRes.Walking_12) metSlider.Value = 2.6;
      else if (sItem == ThermalComfortRes.Walking_18) metSlider.Value = 3.8;
      else if (sItem == ThermalComfortRes.Of_Seated) metSlider.Value = 1.0;
      else if (sItem == ThermalComfortRes.Of_Typing) metSlider.Value = 1.1;
      else if (sItem == ThermalComfortRes.Of_FilingSeated) metSlider.Value = 1.2;
      else if (sItem == ThermalComfortRes.Of_FilingStanding) metSlider.Value = 1.4;
      else if (sItem == ThermalComfortRes.Of_Walking) metSlider.Value = 1.7;
      else if (sItem == ThermalComfortRes.Of_Lifting) metSlider.Value = 2.1;
      else if (sItem == ThermalComfortRes.Driving_Automobile) metSlider.Value = 1.5;
      else if (sItem == ThermalComfortRes.Driving_Aircraft1) metSlider.Value = 1.2;
      else if (sItem == ThermalComfortRes.Driving_Aircraft2) metSlider.Value = 1.8;
      else if (sItem == ThermalComfortRes.Driving_Aircraft3) metSlider.Value = 2.4;
      else if (sItem == ThermalComfortRes.Driving_Heavy) metSlider.Value = 3.2;
      else if (sItem == ThermalComfortRes.Cooling) metSlider.Value = 1.8;
      else if (sItem == ThermalComfortRes.HouseCleaning) metSlider.Value = 2.7;
      else if (sItem == ThermalComfortRes.HeavyLimbMovement) metSlider.Value = 2.2;
      else if (sItem == ThermalComfortRes.Mw_Sawing) metSlider.Value = 1.8;
      else if (sItem == ThermalComfortRes.Mw_Light) metSlider.Value = 2.2;
      else if (sItem == ThermalComfortRes.Mw_Heavy) metSlider.Value = 4.0;
      else if (sItem == ThermalComfortRes.Mw_HandlingBags) metSlider.Value = 4.0;
      else if (sItem == ThermalComfortRes.Mw_ShovelWork) metSlider.Value = 4.4;
      else if (sItem == ThermalComfortRes.Dancing) metSlider.Value = 3.4;
      else if (sItem == ThermalComfortRes.Exercise) metSlider.Value = 3.5;
      else if (sItem == ThermalComfortRes.Tennis) metSlider.Value = 3.8;
      else if (sItem == ThermalComfortRes.Basketball) metSlider.Value = 6.3;
      else if (sItem == ThermalComfortRes.Wrestling) metSlider.Value = 7.9;

    }
  }
}