namespace MLS_Mobile;

using System.Text;
using Microsoft.Maui.ApplicationModel.DataTransfer;

using CommunityToolkit.Maui.Storage;

using MLS_Mobile.Resources.i18n;

[QueryProperty(nameof(FileName), "FileName")]
public partial class LoggingData : ContentPage
{

  #region �C���X�^���X�ϐ��E�v���p�e�B

  private bool isInitialized = false;

  public string FileName { get; set; }

  #endregion

  #region �R���X�g���N�^

  public LoggingData()
	{
		InitializeComponent();

    BindingContext = this;
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    if (!isInitialized)
    {
      isInitialized = true;

      string[] bf = FileName.Split('_');
      this.Title = "MLogger_" + bf[1] + ": "
        + bf[2].Substring(0, 4) + "/" + bf[2].Substring(4, 2) + "/" + bf[2].Substring(6, 2);
      
      //�e�[�u���\��
      showIndicator(MLSResource.LD_Formatting);
      Task.Run(() =>
      {
        try
        {
          Application.Current.Dispatcher.Dispatch(() =>
          {
            makeGrid();
          });
        }
        catch { }
        finally
        {
          //�C���W�P�[�^���B��
          Application.Current.Dispatcher.Dispatch(() =>
          {
            hideIndicator();
          });
        }
      });
    }
  }

  #endregion

  #region public���\�b�h

  /// <summary>�t�@�C������N���b�v�{�[�h�p�f�[�^�����</summary>
  /// <param name="fileName">�t�@�C������</param>
  /// <param name="maxLines">�R�s�[����ő�s��</param>
  /// <returns>�N���b�v�{�[�h�p�f�[�^</returns>
  public static string MakeClipData(string fileName, int maxLines)
  {
    return MLSResource.Date + "," + 
      MLSResource.Time + "," +
      MLSResource.DrybulbTemperature + "," +
      MLSResource.RelativeHumidity + "," +
      MLSResource.GlobeTemperature + "," +
      MLSResource.Velocity + "," +
      MLSResource.Illuminance + "," +
      MLSResource.GlobeTemperatureVoltage + "," +
      MLSResource.VelocityVoltage + Environment.NewLine +
      MLUtility.LoadDataFile(fileName, maxLines);
  }

  /// <summary>�t�@�C������N���b�v�{�[�h�p�f�[�^�����</summary>
  /// <param name="fileName">�t�@�C������</param>
  /// <returns>�N���b�v�{�[�h�p�f�[�^</returns>
  public static string MakeClipData(string fileName)
  {
    return MakeClipData(fileName, int.MaxValue);
  }

  private void makeGrid()
  {
    //�S�f�[�^�����s�R�[�h�ŕ���//�e�[�u���\����500�s�܂Łi�d�����̂Łj
    string[] lines = MakeClipData(FileName, 500).Split(Environment.NewLine);

    //�^�C�g���s
    string[] bf = lines[0].Split(',');
    int col = 0;
    for (int j = 1; j < 9; j++)
    {
      Label lbl = new Label
      {
        Text = bf[j],
        BackgroundColor = Colors.White,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        Margin = new Thickness(1),
        Padding = new Thickness(2),
        LineBreakMode = LineBreakMode.CharacterWrap
      };
      if (j != 7) tableGrid.Add(lbl, col++, 0);
    }

    //�f�[�^�s
    StringBuilder[] sBuilds = new StringBuilder[7];
    for (int i = 1; i < lines.Length; i++)
    {
      if (lines[i] != "")
      {
        bf = lines[i].Split(',');
        col = 0;
        for (int j = 1; j < 9; j++)
        {
          if (j != 7)
          {
            if (i == 1) sBuilds[col] = new StringBuilder("");
            if (i == lines.Length - 1) sBuilds[col].Append(bf[j]);
            else sBuilds[col].AppendLine(bf[j]);
            col++;
          }
        }
      }
    }
    //�f�[�^�����Ȃ��ꍇ�ɂ͋�s�����Ă���
    if (lines.Length < 40)
      for (int i = 0; i < 40 - lines.Length; i++)
        for (int j = 0; j < sBuilds.Length; j++)
          sBuilds[j].AppendLine();

    for (int i = 0; i < sBuilds.Length; i++)
    {
      Label lbl = new Label
      {
        Text = sBuilds[i].ToString(),
        BackgroundColor = Colors.White,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        Margin = new Thickness(1)
      };
      tableGrid.Add(lbl, i, 1);
    }

    //�f�[�^��500�𒴂���ꍇ�ɂ͑��������邱�Ƃ�\��
    if (lines.Length - 1 == 500)
    {
      IView iv = new Label
      {
        Text = MLSResource.LD_Exceed,
        BackgroundColor = Colors.White,
        HorizontalTextAlignment = TextAlignment.Start,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalOptions = LayoutOptions.Fill,
        VerticalOptions = LayoutOptions.Fill,
        Margin = new Thickness(1)
      };
      tableGrid.Add(iv, 0, 2);
      tableGrid.SetColumnSpan(iv, 8);
    }
  }

  #endregion

  #region �R���g���[�����쎞�̏���

  private void copy_Clicked(object sender, EventArgs e)
  {
    Clipboard.Default.SetTextAsync(MakeClipData(FileName));
  }

  private async void delete_Clicked(object sender, EventArgs e)
  {
    bool remove = await DisplayAlert("Alert", MLSResource.LD_DeleteAlert, "OK", "Cancel");
    if (remove)
    {
      MLUtility.DeleteDataFile(FileName);
      await Shell.Current.GoToAsync("..");
    }
  }

  private async void share_Clicked(object sender, EventArgs e)
  {
    string cData = MakeClipData(FileName);
    ShareTextRequest st = new ShareTextRequest
    {
      Text = cData,
      Title = "M-Logger Data"
    };

    try
    {
      //Android�ł�1MB�����E��android.os.TransactionTooLargeException�Ƃ����G���[���o��͗l
      await Share.Default.RequestAsync(st);
    }
    catch
    {
      bool result = await DisplayAlert("Alert", MLSResource.LD_SaveFileAlert, MLSResource.OK, MLSResource.Cancel);
      if (result)
      {
        var data = Encoding.GetEncoding("UTF-8").GetBytes(cData);
        var stream = new MemoryStream(data);
        await FileSaver.Default.SaveAsync(FileName, stream);
      }
    }
  }

  #endregion

  #region �C���W�P�[�^�̑���

  /// <summary>�C���W�P�[�^��\������</summary>
  private void showIndicator(string message)
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      indicatorLabel.Text = message;
      grayback.IsVisible = indicator.IsVisible = true;
    });
  }

  /// <summary>�C���W�P�[�^���B��</summary>
  private void hideIndicator()
  {
    Application.Current.Dispatcher.Dispatch(() =>
    {
      grayback.IsVisible = indicator.IsVisible = false;
    });
  }

  #endregion

}