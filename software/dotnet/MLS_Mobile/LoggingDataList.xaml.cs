namespace MLS_Mobile;

using System.Collections.ObjectModel;

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

  /// <summary>ログデータ閲覧ボタンクリック時の処理</summary>
  /// <param name="sender"></param>
  /// <param name="e"></param>
  private void log_Clicked(object sender, EventArgs e)
  {
    Shell.Current.GoToAsync(nameof(LogView));
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

      // 期待される命名:
      //   - 計測 (DataReceive)   : MLogger_{name}_{yyyymmdd}.txt
      //   - dump (DeviceSetting) : MLogger_{name}_{yyyymmdd}_{hhmmss}_M.txt
      // どちらも _ で split したときの bf[1] が機器名、bf[2] の先頭 8 文字が日付。
      string[] bf = fName.Split('_');
      MLoggerName = "MLogger_" + bf[1];
      DTime = new DateTime(int.Parse(bf[2].Substring(0, 4)), int.Parse(bf[2].Substring(4, 2)), int.Parse(bf[2].Substring(6, 2)));
      IsDump = fName.EndsWith("_M.txt");

      FileSize = MLUtility.GetFileSize(FileName);

      DeleteCommand = new Command<LogFile>(OnDeleteCommand);
      ShareCommand = new Command<LogFile>(OnShareCommand);
      NavigateCommand = new Command<LogFile>(OnNavigateCommand);
    }

    #region プロパティ

    public LoggingDataList Parent { get; private set; }

    /// <summary>ファイル名称を取得する</summary>
    public string FileName { get; private set; }

    /// <summary>ファイルサイズ[byte]を取得する</summary>
    public long FileSize { get; private set; }

    public Command<LogFile> DeleteCommand { get; private set; }

    public Command<LogFile> ShareCommand { get; private set; }

    public Command<LogFile> NavigateCommand { get; private set; }

    /// <summary>計測機器名称を取得する</summary>
    public string MLoggerName { get; private set; }

    /// <summary>計測日を取得する</summary>
    public DateTime DTime { get; private set; }

    /// <summary>このファイルが内蔵フラッシュからの dump (= 機器側で記録された) かどうか。
    /// 単純計測 (= スマホで直接記録) と区別するための markup と背景色に使う。</summary>
    public bool IsDump { get; private set; }

    /// <summary>一覧に表示する日付テキスト (dump は "-M" suffix 付き)。</summary>
    public string DTimeDisplay => DTime.ToString("yyyy/MM/dd") + (IsDump ? "-M" : "");

    /// <summary>行の背景色 (dump は淡い blue で視覚的に区別)。</summary>
    public Color RowBackgroundColor => IsDump ? Color.FromArgb("#E3F2FD") : Colors.White;

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
        Text = FileName + Environment.NewLine + LoggingData.MakeClipData(FileName),
        Title = "M-Logger Data",
        Subject = logFile.FileName
      });
    }

    private async void OnNavigateCommand(LogFile logFile)
    {
      var navigationParameter = new Dictionary<string, object>
        {
            { "FileName", logFile.FileName }
        };
      await Shell.Current.GoToAsync($"LoggingData", navigationParameter);
    }

  }

  #endregion

}