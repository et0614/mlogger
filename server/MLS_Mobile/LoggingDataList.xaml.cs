namespace MLS_Mobile;

using System.Collections.ObjectModel;
using System.Reflection.Metadata;
using System.Windows.Input;

public partial class LoggingDataList : ContentPage
{

  //public ObservableCollection<LogFileGroup> LogFiles { get; set; } = new ObservableCollection<LogFileGroup>();

  public LoggingDataList()
	{
		InitializeComponent();

    UpdateLogFiles();

    BindingContext = this;
  }

  public void UpdateLogFiles()
  {
    string[] files = MLUtility.GetDataFiles();
    SortedDictionary<string, List<LogFile>> lfDict = new SortedDictionary<string, List<LogFile>>();
    foreach (string file in files)
    {
      string fName = file.Substring(file.LastIndexOf(Path.DirectorySeparatorChar) + 1);
      if (fName.StartsWith("MLogger_"))
      {
        LogFile lf = new LogFile(fName, this);
        if (!lfDict.ContainsKey(lf.MLoggerName)) lfDict.Add(lf.MLoggerName, new List<LogFile>());
        lfDict[lf.MLoggerName].Add(lf);
      }
    }

    //毎回新しいObservableCollectionを作っているが、本来は動的に変えるべき。ただ、2023.1.8現在、iOSにバグがあり、落ちる
    ObservableCollection<LogFileGroup> logFiles = new ObservableCollection<LogFileGroup>();
    foreach (string key in lfDict.Keys)
      logFiles.Add(new LogFileGroup(key, new List<LogFile>(lfDict[key].OrderBy(n => n.DTime))));
    fileList.ItemsSource = logFiles;
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    UpdateLogFiles();
  }

  private void fileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    var navigationParameter = new Dictionary<string, object>
    {
        { "FileName", ((LogFile)e.CurrentSelection[0]).FileName }
    };
    Shell.Current.GoToAsync($"LoggingData", navigationParameter);
  }

  #region インナークラス定義

  public class LogFileGroup : List<LogFile>
  {

    public string MLoggerName { get; private set; }

    public LogFileGroup(string mlName, List<LogFile> logFiles) : base(logFiles)
    {
      MLoggerName = mlName;
    }
  }

  public class LogFile
  {
    public LogFile(string fName, LoggingDataList parent)
    {
      FileName = fName;
      Parent = parent;

      string[] bf = fName.Split('_');
      MLoggerName = "MLogger_" + bf[1];
      DTime = new DateTime(int.Parse(bf[2].Substring(0, 4)), int.Parse(bf[2].Substring(4, 2)), int.Parse(bf[2].Substring(6, 2)));

      FileSize = MLUtility.GetFileSize(FileName);

      DeleteCommand = new Command<LogFile>(OnDeleteCommand);
      ShareCommand = new Command<LogFile>(OnShareCommand);
    }

    #region プロパティ

    public LoggingDataList Parent { get; private set; }

    /// <summary>ファイル名称を取得する</summary>
    public string FileName { get; private set; }

    /// <summary>ファイルサイズ[byte]を取得する</summary>
    public long FileSize { get; private set; }

    public Command<LogFile> DeleteCommand { get; private set; }

    public Command<LogFile> ShareCommand { get; private set; }

    /// <summary>計測機器名称を取得する</summary>
    public string MLoggerName { get; private set; }

    /// <summary>計測日を取得する</summary>
    public DateTime DTime { get; private set; }

    #endregion


    private void OnDeleteCommand(LogFile logFile)
    {
      MLUtility.DeleteDataFile(logFile.FileName);
      logFile.Parent.UpdateLogFiles();
    }

    private async void OnShareCommand(LogFile logFile)
    {
      await Share.Default.RequestAsync(new ShareTextRequest
      {
        Text = LoggingData.MakeClipData(FileName),
        Title = "M-Logger Data"
      });

      //Clipboard.Default.SetTextAsync
      //  (LoggingData.MakeClipData(logFile.FileName));
    }
  }

  #endregion

}