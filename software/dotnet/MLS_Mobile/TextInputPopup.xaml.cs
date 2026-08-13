using CommunityToolkit.Maui.Views;

namespace MLS_Mobile;

public partial class TextInputPopup : Popup<string>
{

  public TextInputPopup(string labelText, string entryValue, Keyboard key)
  {
    InitializeComponent();

    LabelText = labelText;
    EntryValue = entryValue;
    Key = key;
    BindingContext = this;
  }

  public string EntryValue { get; set; }

  public Keyboard Key { get; set; }

  public string LabelText { get; set; }

  /// <summary>OK で閉じたか。Cancel・枠外タップの場合は false のまま。
  /// iOS では OK 押下時にも CloseAsync の Result が null で返る事象があるため、
  /// 呼び出し側は Result ではなくこちらで確定/取消を判定すること。</summary>
  public bool WasAccepted { get; private set; }

  /// <summary>OK 押下時点の入力文字列 (WasAccepted == true のときのみ有効)。</summary>
  public string AcceptedText { get; private set; } = "";

  private async void btnOK_Clicked(object sender, EventArgs e)
  {
    WasAccepted = true;
    AcceptedText = entName.Text ?? "";
    await CloseAsync(entName.Text);
  }

  private async void btnCancel_Clicked(object sender, EventArgs e)
      => await CloseAsync(default(string)); // null を返す場合

}