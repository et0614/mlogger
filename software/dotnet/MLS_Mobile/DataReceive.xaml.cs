namespace MLS_Mobile;

using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using MLLib;
using MLLib.Protocol;
using MLS_Mobile.Resources.i18n;
using MLS_Mobile.ViewModels;

[QueryProperty(nameof(MLoggerLowAddress), "mlLowAddress")]
[QueryProperty(nameof(CloValue), "CloValue")]
[QueryProperty(nameof(MetValue), "MetValue")]
public partial class DataReceive : ContentPage
{
    #region Properties

    private string _mlLowAddress = "";
    private DataReceiveViewModel? _vm;

    /// <summary>QueryProperty: target device low address. Setter wires up the ViewModel.</summary>
    public string MLoggerLowAddress
    {
        get => _mlLowAddress;
        set
        {
            _mlLowAddress = value;
            BuildViewModel();
        }
    }

    /// <summary>QueryProperty: initial Clo.</summary>
    public double CloValue { get; set; } = 1.2;

    /// <summary>QueryProperty: initial Met.</summary>
    public double MetValue { get; set; } = 1.1;

    #endregion

    #region Constructor / lifecycle

    public DataReceive()
    {
        InitializeComponent();

        cloTitle.Text = MLSResource.ClothingUnit + " [clo]";
        metTitle.Text = MLSResource.MetabolicUnit + " [met]";
    }

    ~DataReceive()
    {
        _vm?.Dispose();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        DeviceDisplay.Current.KeepScreenOn = true;

        // Reflect Clo/Met from QueryProperty into sliders (which write back to the VM).
        cloSlider.Value = CloValue;
        metSlider.Value = MetValue;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        DeviceDisplay.Current.KeepScreenOn = false;

        _vm?.Dispose();
        _vm = null;
    }

    #endregion

    #region ViewModel wiring

    private void BuildViewModel()
    {
        _vm?.Dispose();

        MLogger? legacy = MLUtility.GetLogger(_mlLowAddress);
        if (legacy == null) return;

        Title = legacy.LocalName;

        // Seed Clo/Met into legacy logger so its internal thermal-index calculation
        // matches what the VM will compute.
        legacy.CloValue = CloValue;
        legacy.MetValue = MetValue;

        // v4 protocol takes priority if available; otherwise fall back to legacy MLogger events.
        IMLProtocol? proto = MLUtility.Protocol;
        bool useV4 = proto != null && proto.Device.ProtocolVersion >= 1;

        _vm = useV4
            ? new DataReceiveViewModel(proto!, legacy.LocalName, CloValue, MetValue, legacy.HasCO2LevelSensor)
            : new DataReceiveViewModel(legacy, legacy.LocalName, CloValue, MetValue);

        BindingContext = _vm;
    }

    #endregion

    #region UI events

    private void CloBtn_Clicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync(nameof(ClothingCoordinator));

    private void ActBtn_Clicked(object sender, EventArgs e)
        => Shell.Current.GoToAsync(nameof(ActivitySelector));

    private async void TapGestureRecognizer_ShortMemo_Tapped(object sender, TappedEventArgs e)
    {
        var popup = new DescriptionPopup(DescriptionText.ShortMemo);
        await this.ShowPopupAsync(popup);
    }

    #endregion

    #region Indicator

    private void showIndicator(string message)
    {
        Application.Current!.Dispatcher.Dispatch(() =>
        {
            indicatorLabel.Text = message;
            grayback.IsVisible = indicator.IsVisible = true;
        });
    }

    private void hideIndicator()
    {
        Application.Current!.Dispatcher.Dispatch(() =>
        {
            grayback.IsVisible = indicator.IsVisible = false;
        });
    }

    #endregion
}
