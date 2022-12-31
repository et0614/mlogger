namespace MLS_Mobile;

using System.Collections.ObjectModel;

public partial class ActivitySelector : ContentPage
{

  #region インスタンス変数・プロパティ

  /// <summary>設定を反映するか否か</summary>
  public bool ApplyChange { get; set; } = false;

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
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Sleeping, 0.7, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Sleeping.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Reclining, 0.8, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Reclining.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Seated_quiet, 1.0, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Seated_quiet.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Standing_relaxed, 1.2, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Standing_relaxed.png"))
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_Resting, act_Title_Resting));

    List<Activity> act_Title_Walking = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Walking_09, 2.0, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Walking_09.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Walking_12, 2.6, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Walking_12.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Walking_18, 3.8, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Walking_18.png"))
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_Walking, act_Title_Walking));

    List<Activity> act_Title_OfficeActivities = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Of_Seated, 1.0, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Of_Seated.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Typing, 1.1, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Typing.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_FilingSeated, 1.2, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_FilingSeated.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_FilingStanding, 1.4, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_FilingStanding.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_WalkingAbout, 1.7, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_WalkingAbout.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Lifting_Packing, 2.1, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Lifting_Packing.png"))
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_OfficeActivities, act_Title_OfficeActivities));

    List<Activity> act_Title_Driving = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Automobile, 1.5, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Automobile.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Aircraft_Routine, 1.2, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Aircraft_Routine.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Aircraft_InstrumentLanding, 1.8, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Aircraft_InstrumentLanding.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Aircraft_Combat, 2.4, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Aircraft_Combat.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_HeavyVehicle, 3.2, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_HeavyVehicle.png"))
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_Driving, act_Title_Driving));

    List<Activity> act_Title_OccupationalActivities = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Cooking, 1.8, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Cooking.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_HouseCleaning, 2.7, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_HouseCleaning.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_HeavyLimbMovement, 2.2, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_HeavyLimbMovement.png"))
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_OccupationalActivities, act_Title_OccupationalActivities));

    List<Activity> act_Title_MachineWork = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Sawing, 1.8, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Sawing.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Light, 2.2, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Light.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Heavy, 4.0, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Heavy.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_HandlingBags, 4.0, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_HandlingBags.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_PickAndShovelWork, 4.4, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_PickAndShovelWork.png"))
    };
    Activities.Add(new ActivityGroup(MLS_Mobile.Resources.i18n.TCResource.Act_Title_MachineWork, act_Title_MachineWork));

    List<Activity> act_Title_LeisureActivities = new List<Activity>
    {
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Dancing, 3.4, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Dancing.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Calisthenics, 3.5, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Calisthenics.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Tennis, 3.8, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Tennis.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Basketball, 6.3, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Basketball.png")),
      new Activity(MLS_Mobile.Resources.i18n.TCResource.Act_Wrestling, 7.9, ImageSource.FromResource("MLS_Mobile.Resources.Activities.Act_Wrestling.png"))
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
    ApplyChange = true;

    if (actView.SelectedItem != null)
      MetValue = ((Activity)actView.SelectedItem).MetValue;

    Navigation.PopAsync();
  }

  #endregion

  #region CollectionView用のインナークラス定義

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

}