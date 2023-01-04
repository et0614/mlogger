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
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Bra, 0.01,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_Bra.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Panties, 0.03,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_Panties.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_MensBriefs, 0.04, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_MensBriefs.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_TShirt, 0.08,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_TShirt.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_HalfSlip, 0.14,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_HalfSlip.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongUnderwearBottoms, 0.15,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongUnderwearBottoms.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_FullSlip, 0.16,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_FullSlip.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongUnderwearTop, 0.20,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongUnderwearTop.png"))
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_Underwear, clt_Title_Underwear));

    List<Cloth> clt_Clo_Title_Footwear = new List<Cloth>
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_AnkleLengthAthleticSocks, 0.02, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_AnkleLengthAthleticSocks.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Pantyhose_Stockings, 0.02, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_Pantyhose_Stockings.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Sandals_Thongs, 0.02, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_Sandals_Thongs.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Shoes, 0.02, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_Shoes.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Slippers, 0.03, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_Slippers.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_CalfLengthSocks, 0.03, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_CalfLengthSocks.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_KneeSocks_Thick, 0.06, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_KneeSocks_Thick.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Boots, 0.10, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_Boots.png"))
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_Footwear, clt_Clo_Title_Footwear));

    List<Cloth> clt_Title_ShirtsAndBlouses = new List<Cloth>
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Sleeveless, 0.13,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_Sleeveless.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleeveKnit, 0.17, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_ShortSleeveKnit.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleeveDressShirt, 0.19, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_ShortSleeveDressShirt.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveDressShirt, 0.25, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongSleeveDressShirt.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveFlannel, 0.34, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongSleeveFlannel.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveSweatShirt, 0.34, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongSleeveSweatShirt.png"))
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_ShirtsAndBlouses, clt_Title_ShirtsAndBlouses));

    List<Cloth> clt_Clo_Title_TrousersAndCoveralls = new List<Cloth>
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortShorts, 0.06,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_ShortShorts.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_WalkingShorts, 0.08,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_WalkingShorts.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_StraightTrousersThin, 0.15,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_StraightTrousersThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_StraightTrousersThick, 0.24,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_StraightTrousersThick.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Sweatpants, 0.28,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_Sweatpants.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Overalls, 0.30,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_Overalls.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_Coveralls, 0.49,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_Coveralls.png"))
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_TrousersAndCoveralls, clt_Clo_Title_TrousersAndCoveralls));

    List<Cloth> clt_Title_DressAndSkirts = new List<Cloth>()
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SkirtThin, 0.14,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SkirtThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SkirtThick, 0.23,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SkirtThick.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessThin, 0.23,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SleevelessThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessThick, 0.27, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SleevelessThick.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleeveShirtDressThin, 0.29, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_ShortSleeveShirtDressThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveShirtDressThin, 0.33,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongSleeveShirtDressThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveShirtDressThick, 0.47,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongSleeveShirtDressThick.png"))
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_DressAndSkirts, clt_Title_DressAndSkirts));

    List<Cloth> clt_Title_Sweaters = new List<Cloth>()
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessSweatVestThin, 0.13,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SleevelessSweatVestThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessSweatVestThick, 0.22, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SleevelessSweatVestThick.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveThin, 0.25,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongSleeveThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveThick, 0.35,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongSleeveThick.png"))
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_Sweaters, clt_Title_Sweaters));

    List<Cloth> clt_Title_SuitJacketsAndVests = new List<Cloth>()
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessVestThin, 0.10,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SleevelessVestThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessVestThick, 0.17,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SleevelessVestThick.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SingleBreastedThin, 0.36,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SingleBreastedThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SingleBreastedThick, 0.42,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SingleBreastedThick.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_DoubleBreastedThin, 0.44,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_DoubleBreastedThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_DoubleBreastedThick, 0.48,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_DoubleBreastedThick.png"))
    };
    Clothes.Add(new ClothGroup(MLS_Mobile.Resources.i18n.TCResource.Clo_Title_SuitJacketsAndVests, clt_Title_SuitJacketsAndVests));

    List<Cloth> clt_Title_SleepwearAndRobes = new List<Cloth>()
    {
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessShortGownThin, 0.18,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SleevelessShortGownThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_SleevelessLongGownThin, 0.20,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_SleevelessLongGownThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleeveHospitalGown, 0.31, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_ShortSleeveHospitalGown.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleeveShortRobeThin, 0.34,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_ShortSleeveShortRobeThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_ShortSleevePajamasThin, 0.42, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_ShortSleevePajamasThin.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveLongGownThick, 0.46,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongSleeveLongGownThick.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveShortWrapRobeThick, 0.48, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongSleeveShortWrapRobeThick.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleevePajamasThick, 0.57,  ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongSleevePajamasThick.png")),
      new Cloth(MLS_Mobile.Resources.i18n.TCResource.Clo_LongSleeveLongWrapRobeThick, 0.69, ImageSource.FromResource("MLS_Mobile.Resources.Clothes.Clo_LongSleeveLongWrapRobeThick.png"))
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

  #region CollectionView用のインナークラス定義

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

}