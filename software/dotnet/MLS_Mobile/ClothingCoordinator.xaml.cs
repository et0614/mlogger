namespace MLS_Mobile;

using System.Collections.ObjectModel;

public partial class ClothingCoordinator : ContentPage
{

  #region インスタンス変数・プロパティ

  /// <summary>CLO値を取得する</summary>
  public double CloValue { get; private set; }

  /// <summary>着衣一覧</summary>
  public ObservableCollection<ClothGroup> Clothes { get; private set; } = new ObservableCollection<ClothGroup>();

  #endregion

  #region コンストラクタ

  /// <summary>インスタンスを初期化する</summary>
  public ClothingCoordinator()
  {
    InitializeComponent();

    CreateClothCollection();
    BindingContext = this;

    updateCloValue();
  }

  /// <summary>着衣リストを作成する</summary>
  private void CreateClothCollection()
  {

    List<Cloth> clt_Title_Underwear = new List<Cloth>
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Bra, 0.01,  "clo_bra.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Panties, 0.03,  "clo_panties.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_MensBriefs, 0.04, "clo_mens_briefs.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_TShirt, 0.08,  "clo_tshirt.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_HalfSlip, 0.14,  "clo_half_slip.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongUnderwearBottoms, 0.15,  "clo_long_underwear_bottoms.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_FullSlip, 0.16,  "clo_full_slip.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongUnderwearTop, 0.20,  "clo_long_underwear_top.png")
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_Underwear, clt_Title_Underwear));

    List<Cloth> clt_Clo_Title_Footwear = new List<Cloth>
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_AnkleLengthAthleticSocks, 0.02, "clo_ankle_length_athletic_socks.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Pantyhose_Stockings, 0.02, "clo_pantyhose_stockings.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Sandals_Thongs, 0.02, "clo_sandals_thongs.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Shoes, 0.02, "clo_shoes.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Slippers, 0.03, "clo_slippers.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_CalfLengthSocks, 0.03, "clo_calf_length_socks.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_KneeSocks_Thick, 0.06, "clo_knee_socks_thick.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Boots, 0.10, "clo_boots.png")
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_Footwear, clt_Clo_Title_Footwear));

    List<Cloth> clt_Title_ShirtsAndBlouses = new List<Cloth>
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Sleeveless, 0.13,  "clo_sleeveless.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleeveKnit, 0.17, "clo_short_sleeve_knit.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleeveDressShirt, 0.19, "clo_short_sleeve_dress_shirt.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveDressShirt, 0.25, "clo_long_sleeve_dress_shirt.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveFlannel, 0.34, "clo_long_sleeve_flannel.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveSweatShirt, 0.34, "clo_long_sleeve_sweat_shirt.png")
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_ShirtsAndBlouses, clt_Title_ShirtsAndBlouses));

    List<Cloth> clt_Clo_Title_TrousersAndCoveralls = new List<Cloth>
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortShorts, 0.06,  "clo_short_shorts.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_WalkingShorts, 0.08,  "clo_walking_shorts.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_StraightTrousersThin, 0.15,  "clo_straight_trousers_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_StraightTrousersThick, 0.24,  "clo_straight_trousers_thick.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Sweatpants, 0.28,  "clo_sweatpants.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Overalls, 0.30,  "clo_overalls.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Coveralls, 0.49,  "clo_coveralls.png")
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_TrousersAndCoveralls, clt_Clo_Title_TrousersAndCoveralls));

    List<Cloth> clt_Title_DressAndSkirts = new List<Cloth>()
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SkirtThin, 0.14,  "clo_skirt_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SkirtThick, 0.23,  "clo_skirt_thick.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessThin, 0.23,  "clo_sleeveless_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessThick, 0.27, "clo_sleeveless_thick.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleeveShirtDressThin, 0.29, "clo_short_sleeve_shirt_dress_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveShirtDressThin, 0.33,  "clo_long_sleeve_shirt_dress_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveShirtDressThick, 0.47,  "clo_long_sleeve_shirt_dress_thick.png")
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_DressAndSkirts, clt_Title_DressAndSkirts));

    List<Cloth> clt_Title_Sweaters = new List<Cloth>()
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessSweatVestThin, 0.13,  "clo_sleeveless_sweat_vest_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessSweatVestThick, 0.22, "clo_sleeveless_sweat_vest_thick.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveThin, 0.25,  "clo_long_sleeve_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveThick, 0.35,  "clo_long_sleeve_thick.png")
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_Sweaters, clt_Title_Sweaters));

    List<Cloth> clt_Title_SuitJacketsAndVests = new List<Cloth>()
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessVestThin, 0.10,  "clo_sleeveless_vest_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessVestThick, 0.17,  "clo_sleeveless_vest_thick.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SingleBreastedThin, 0.36,  "clo_single_breasted_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SingleBreastedThick, 0.42,  "clo_single_breasted_thick.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_DoubleBreastedThin, 0.44,  "clo_double_breasted_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_DoubleBreastedThick, 0.48,  "clo_double_breasted_thick.png")
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_SuitJacketsAndVests, clt_Title_SuitJacketsAndVests));

    List<Cloth> clt_Title_SleepwearAndRobes = new List<Cloth>()
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessShortGownThin, 0.18,  "clo_sleeveless_short_gown_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessLongGownThin, 0.20,  "clo_sleeveless_long_gown_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleeveHospitalGown, 0.31, "clo_short_sleeve_hospital_gown.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleeveShortRobeThin, 0.34,  "clo_short_sleeve_short_robe_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleevePajamasThin, 0.42, "clo_short_sleeve_pajamas_thin.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveLongGownThick, 0.46,  "clo_long_sleeve_long_gown_thick.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveShortWrapRobeThick, 0.48, "clo_long_sleeve_short_wrap_robe_thick.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleevePajamasThick, 0.57,  "clo_long_sleeve_pajamas_thick.png"),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveLongWrapRobeThick, 0.69, "clo_long_sleeve_long_wrap_robe_thick.png")
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_SleepwearAndRobes, clt_Title_SleepwearAndRobes));
  }

  #endregion

  #region コントロール操作時の処理

  /// <summary>選択項目変更時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void OnCollectionViewSelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    updateCloValue();
  }

  /// <summary>Clo値を更新する</summary>
  private void updateCloValue()
  {
    double sClo = 0;    
    foreach (object obj in cloView.SelectedItems)
      sClo += ((Cloth)obj).CloValue;
    CloValue = sClo;
    CloValueLabel.Text = "Clo value = " + CloValue.ToString("F2");
  }

  /// <summary>設定ボタンクリック時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void Button_Clicked(object sender, EventArgs e)
  {
    var navigationParameter = new Dictionary<string, object>
    {
        { "CloValue", CloValue }
    };
    Shell.Current.GoToAsync($"..", navigationParameter);
  }

  #endregion

}

#region CollectionView用のクラス定義

public class Cloth
{
  public Cloth(string name, double cloValue, ImageSource appearance)
  {
    Name = name;
    CloValue = cloValue;
    Appearance = appearance;
  }

  public ImageSource Appearance { get; set; }

  public string Name { get; set; }

  public double CloValue { get; set; }
}

public class ClothGroup : List<Cloth>
{
  public string Name { get; private set; }

  public ClothGroup(string name, List<Cloth> clothes) : base(clothes)
  {
    Name = name;
  }
}


#endregion