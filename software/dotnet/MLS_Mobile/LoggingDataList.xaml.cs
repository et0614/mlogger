namespace MLS_Mobile;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using MLS_Mobile.Resources.i18n;

public partial class LoggingDataList : ContentPage
{

  //public ObservableCollection<LogFileGroup> LogFiles { get; set; } = new ObservableCollection<LogFileGroup>();

  /// <summary>一覧に表示中のファイル (選択モードの一括操作用)</summary>
  private List<LogFile> _allFiles = new List<LogFile>();

  /// <summary>チェックボックスで複数のファイルを選ぶ選択モードか</summary>
  public bool IsSelecting { get; private set; }

  public LoggingDataList()
	{
		InitializeComponent();

    UpdateLogFiles();

    BindingContext = this;

    // 行の背景色はコードで決めているので、起動中にライト/ダークが切り替わったら作り直す
    // (このページはタブのルートでアプリ終了まで残るので、購読の解除は不要)
    if (Application.Current != null)
      Application.Current.RequestedThemeChanged += (_, __) => Dispatcher.Dispatch(UpdateLogFiles);
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
        LogFile lf;
        try { lf = new LogFile(fName, this); }
        catch
        {
          // 命名規約外のファイル (外部から持ち込まれた等) が
          // 1 つあるだけでページ生成ごと失敗しないよう、除外して続行する。
          MLUtility.WriteLog($"[datalist] unrecognized file name skipped: {fName}");
          continue;
        }
        if (!lfDict.ContainsKey(lf.MLoggerName)) lfDict.Add(lf.MLoggerName, new List<LogFile>());
        lfDict[lf.MLoggerName].Add(lf);
      }
    }

    //毎回新しいObservableCollectionを作っているが、本来は動的に変えるべき。ただ、2023.1.8現在、iOSにバグがあり、落ちる
    ObservableCollection<LogFileGroup> logFiles = new ObservableCollection<LogFileGroup>();
    foreach (string key in lfDict.Keys)
      logFiles.Add(new LogFileGroup(key, new List<LogFile>(lfDict[key].OrderBy(n => n.DTime))));
    _allFiles = logFiles.SelectMany(g => g).ToList();
    foreach (LogFile lf in _allFiles) lf.IsSelecting = IsSelecting;
    fileList.ItemsSource = logFiles;
    UpdateSelectionBar();
  }

  #region 選択モード (複数ファイルの一括削除)

  /// <summary>選択モードの開始・終了を切り替える</summary>
  private void SetSelecting(bool selecting)
  {
    IsSelecting = selecting;
    foreach (LogFile lf in _allFiles)
    {
      lf.IsSelecting = selecting;
      if (!selecting) lf.IsSelected = false;
    }
    selectToolbarItem.Text = selecting ? MLSResource.LD_SelectDone : MLSResource.LD_Select;
    selectionBar.IsVisible = selecting;
    logButton.IsVisible = !selecting;
    UpdateSelectionBar();
  }

  /// <summary>選択数に応じて削除ボタンの表示を更新する</summary>
  internal void UpdateSelectionBar()
  {
    int n = _allFiles.Count(f => f.IsSelected);
    deleteSelectedButton.Text = string.Format(MLSResource.LD_DeleteSelected, n);
    deleteSelectedButton.IsEnabled = 0 < n;
  }

  private void select_Clicked(object sender, EventArgs e)
  {
    SetSelecting(!IsSelecting);
  }

  private void selectAll_Clicked(object sender, EventArgs e)
  {
    // すべて選択済みなら全解除、そうでなければ全選択
    bool all = _allFiles.Count > 0 && _allFiles.All(f => f.IsSelected);
    foreach (LogFile lf in _allFiles) lf.IsSelected = !all;
    UpdateSelectionBar();
  }

  private async void deleteSelected_Clicked(object sender, EventArgs e)
  {
    List<LogFile> targets = _allFiles.Where(f => f.IsSelected).ToList();
    if (targets.Count == 0) return;

    bool ok = await DisplayAlert(MLSResource.LD_Delete,
      string.Format(MLSResource.LD_DeleteSelectedAlert, targets.Count),
      MLSResource.Yes, MLSResource.Cancel);
    if (!ok) return;

    foreach (LogFile lf in targets)
    {
      try { MLUtility.DeleteDataFile(lf.FileName); }
      catch (Exception ex) { MLUtility.WriteLog($"[datalist] delete failed: {lf.FileName} {ex.Message}"); }
    }
    SetSelecting(false);
    UpdateLogFiles();
  }

  #endregion

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

  public class LogFile : INotifyPropertyChanged
  {
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
      => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool _isSelecting;
    private bool _isSelected;

    /// <summary>選択モード中か (チェックボックスの表示に使う)</summary>
    public bool IsSelecting
    {
      get => _isSelecting;
      set { if (_isSelecting != value) { _isSelecting = value; OnPropertyChanged(); } }
    }

    /// <summary>一括削除の対象として選ばれているか</summary>
    public bool IsSelected
    {
      get => _isSelected;
      set
      {
        if (_isSelected == value) return;
        _isSelected = value;
        OnPropertyChanged();
        Parent?.UpdateSelectionBar();
      }
    }

    public LogFile(string fName, LoggingDataList parent)
    {
      FileName = fName;
      Parent = parent;

      // 命名規約の解析は MLUtility.TryParseDataFileName に集約 (機器名に '_' を含んでもよい)
      if (!MLUtility.TryParseDataFileName(fName, out string mlName, out DateTime date, out bool isDump))
        throw new FormatException($"unrecognized data file name: {fName}");
      MLoggerName = mlName;
      DTime = date;
      IsDump = isDump;

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

    /// <summary>行の背景色 (dump は淡い青で視覚的に区別。ダーク表示では暗い青)。</summary>
    public Color RowBackgroundColor => MLUtility.ThemeColor(IsDump ? "DumpRow" : "Surface");

    #endregion


    private async void OnDeleteCommand(LogFile logFile)
    {
      // 一括削除 (選択モード) ができたので、スワイプでの削除も誤操作防止のため確認する
      bool ok = await logFile.Parent.DisplayAlert(MLSResource.LD_Delete,
        $"{MLSResource.LD_DeleteAlert}{Environment.NewLine}{logFile.FileName}",
        MLSResource.Yes, MLSResource.Cancel);
      if (!ok) return;

      MLUtility.DeleteDataFile(logFile.FileName);
      logFile.Parent.UpdateLogFiles();
    }

    private async void OnShareCommand(LogFile logFile)
    {
      // ShareTextRequest に全文を積むと Android では 1MB 境界で
      // TransactionTooLargeException になるため、詳細ページ (LoggingData.share_Clicked)
      // と同じ一時ファイル + ShareFileRequest 方式で共有する。
      try
      {
        string filePath = Path.Combine(FileSystem.CacheDirectory, logFile.FileName);
        await Task.Run(() =>
          File.WriteAllText(filePath, LoggingData.MakeClipData(logFile.FileName), Encoding.UTF8));
        await Share.Default.RequestAsync(new ShareFileRequest
        {
          Title = "M-Logger Data",
          File = new ShareFile(filePath)
        });
      }
      catch (Exception ex)
      {
        await MLUtility.ShowErrorAsync(logFile.Parent, MLSResource.LD_ShareFailed, ex);
      }
    }

    private async void OnNavigateCommand(LogFile logFile)
    {
      // 選択モード中は、行のタップでチェックを切り替える
      if (logFile.IsSelecting)
      {
        logFile.IsSelected = !logFile.IsSelected;
        return;
      }
      var navigationParameter = new Dictionary<string, object>
        {
            { "FileName", logFile.FileName }
        };
      await Shell.Current.GoToAsync($"LoggingData", navigationParameter);
    }

  }

  #endregion

}