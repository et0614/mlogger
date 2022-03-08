using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using Plugin.NetStandardStorage;
using Plugin.NetStandardStorage.Abstractions.Interfaces;

using System.Collections.ObjectModel;

using MLS_Mobile.Resources;

namespace MLS_Mobile
{
  [XamlCompilation(XamlCompilationOptions.Compile)]
  public partial class LoggingDataList : ContentPage
  {

    public LoggingDataList()
    {
      InitializeComponent();

      this.Title = MLSResource.LD_List;
    }

    #region ロード・アンロードイベント

    protected override void OnAppearing()
    {
      base.OnAppearing();

      var flSource = new ObservableCollection<Label>();
      IFolder folder = CrossStorage.FileSystem.LocalStorage.GetFolder(MainPage.DATA_FOLDER);
      IList<IFile> files = folder.GetFiles();
      if (files.Count == 0)
      {
        DisplayAlert("Alert", MLSResource.LD_NoData, "OK");
        Navigation.PopAsync();
      }
      foreach (IFile fl in files)
        flSource.Add(makeLabel(fl.Name));
      fileList.ItemsSource = flSource;
    }

    private Label makeLabel(string text)
    {
      Label lbl = new Label();
      lbl.Text = text;
      lbl.TextColor = Color.Black;
      return lbl;
    }

    #endregion

    private void fileList_ItemSelected(object sender, SelectedItemChangedEventArgs e)
    {
      if (fileList.SelectedItem == null) return;

      string fName = ((Label)e.SelectedItem).Text;
      fileList.SelectedItem = null;

      LoggingData ld = new LoggingData();
      ld.LoadData(fName);
      Navigation.PushAsync(ld);
    }
  }
}