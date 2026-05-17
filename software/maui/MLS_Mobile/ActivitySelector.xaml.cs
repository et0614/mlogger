namespace MLS_Mobile;

using System.Collections.ObjectModel;

public partial class ActivitySelector : ContentPage
{

  #region インスタンス変数・プロパティ

  /// <summary>Met値を取得する</summary>
  public double MetValue { get; private set; }

  /// <summary>活動一覧</summary>
  public ObservableCollection<ActivityGroup> Activities { get; private set; } = new ObservableCollection<ActivityGroup>();

  #endregion

  #region コンストラクタ

  public ActivitySelector()
  {
    InitializeComponent();

    CreateActivityCollection();
    BindingContext = this;
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    actView.SelectedItem = null;
  }

  /// <summary>活動量リストを作成する</summary>
  private void CreateActivityCollection()
  {
    List<Activity> act_Title_Resting = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Sleeping, 0.7, "act_sleeping.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Reclining, 0.8, "act_reclining.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Seated_quiet, 1.0, "act_seated_quiet.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Standing_relaxed, 1.2, "act_standing_relaxed.png")
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_Resting, act_Title_Resting));

    List<Activity> act_Title_Walking = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Walking_09, 2.0, "act_walking_09.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Walking_12, 2.6, "act_walking_12.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Walking_18, 3.8, "act_walking_18.png")
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_Walking, act_Title_Walking));

    List<Activity> act_Title_OfficeActivities = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Of_Seated, 1.0, "act_of_seated.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Typing, 1.1, "act_typing.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_FilingSeated, 1.2, "act_filing_seated.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_FilingStanding, 1.4, "act_filing_standing.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_WalkingAbout, 1.7, "act_walking_about.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Lifting_Packing, 2.1, "act_lifting_packing.png")
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_OfficeActivities, act_Title_OfficeActivities));

    List<Activity> act_Title_Driving = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Automobile, 1.5, "act_automobile.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Aircraft_Routine, 1.2, "act_aircraft_routine.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Aircraft_InstrumentLanding, 1.8, "act_aircraft_instrument_landing.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Aircraft_Combat, 2.4, "act_aircraft_combat.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_HeavyVehicle, 3.2, "act_heavy_vehicle.png")
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_Driving, act_Title_Driving));

    List<Activity> act_Title_OccupationalActivities = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Cooking, 1.8, "act_cooking.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_HouseCleaning, 2.7, "act_house_cleaning.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_HeavyLimbMovement, 2.2, "act_heavy_limb_movement.png")
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_OccupationalActivities, act_Title_OccupationalActivities));

    List<Activity> act_Title_MachineWork = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Sawing, 1.8, "act_sawing.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Light, 2.2, "act_light.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Heavy, 4.0, "act_heavy.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_HandlingBags, 4.0, "act_handling_bags.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_PickAndShovelWork, 4.4, "act_pick_and_shovel_work.png")
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_MachineWork, act_Title_MachineWork));

    List<Activity> act_Title_LeisureActivities = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Dancing, 3.4, "act_dancing.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Calisthenics, 3.5, "act_calisthenics.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Tennis, 3.8, "act_tennis.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Basketball, 6.3, "act_basketball.png"),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Wrestling, 7.9, "act_wrestling.png")
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_LeisureActivities, act_Title_LeisureActivities));

  }

  #endregion

  #region コントロール操作時の処理

  /// <summary>選択項目変更時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void OnCollectionViewSelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (actView.SelectedItem != null)
      MetValue = ((Activity)actView.SelectedItem).MetValue;

    var navigationParameter = new Dictionary<string, object>
    {
        { "MetValue", MetValue }
    };
    Shell.Current.GoToAsync($"..", navigationParameter);
  }

  #endregion

}

#region CollectionView用のクラス定義

public class Activity
{
  public Activity(string name, double metValue, ImageSource appearance)
  {
    Name = name;
    MetValue = metValue;
    Appearance = appearance;
  }

  public ImageSource Appearance { get; set; }

  public string Name { get; set; }

  public double MetValue { get; set; }
}

public class ActivityGroup : List<Activity>
{
  public string Name { get; private set; }

  public ActivityGroup(string name, List<Activity> activities) : base(activities)
  {
    Name = name;
  }
}

#endregion