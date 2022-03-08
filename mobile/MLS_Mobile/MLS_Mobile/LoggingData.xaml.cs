using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

using Plugin.NetStandardStorage;
using Plugin.NetStandardStorage.Abstractions.Types;
using Plugin.NetStandardStorage.Abstractions.Interfaces;

using MLS_Mobile.Services;

using MLS_Mobile.Resources;

namespace MLS_Mobile
{
  [XamlCompilation(XamlCompilationOptions.Compile)]
  public partial class LoggingData : ContentPage
  {
    private string fileName;

    public LoggingData()
    {
      InitializeComponent();

      btnCopy.Text = MLSResource.LD_Copy;
      btnDelete.Text = MLSResource.LD_Delete;
    }

    public void LoadData(string fileName)
    {
      this.fileName = fileName;
      lblFname.Text = "File: " + this.fileName;

      IFolder localSt = CrossStorage.FileSystem.LocalStorage;
      IFolder folder = localSt.CreateFolder(MainPage.DATA_FOLDER, CreationCollisionOption.OpenIfExists);
      IFile file = folder.GetFile(fileName);
      Stream strm = file.Open(FileAccess.ReadWrite);
      byte[] buff = new byte[strm.Length];
      strm.Read(buff, 0, (int)strm.Length);
      lbl_data.Text = Encoding.UTF8.GetString(buff);
    }

    private void copy_Clicked(object sender, EventArgs e)
    {
      DependencyService.Get<IDeviceService>().Copy("MLogger data", lbl_data.Text);
    }

    private async void delete_Clicked(object sender, EventArgs e)
    {
      bool remove = await DisplayAlert("Alert", "データを削除して良いですか？", "OK", "Cancel");
      if (remove)
      {
        IFolder localSt = CrossStorage.FileSystem.LocalStorage;
        IFolder folder = localSt.CreateFolder(MainPage.DATA_FOLDER, CreationCollisionOption.OpenIfExists);
        IFile file = folder.GetFile(fileName);
        file.Delete();

        await Navigation.PopAsync();
      }      
    }
  }
}