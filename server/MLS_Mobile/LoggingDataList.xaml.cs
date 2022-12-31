namespace MLS_Mobile;

using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Windows.Input;

public partial class LoggingDataList : ContentPage
{

  public ObservableCollection<logFile> LogFiles { get; set; } = new ObservableCollection<logFile>();

  public LoggingDataList()
	{
		InitializeComponent();

    BindingContext = this;
  }

  public void UpdateLogFiles()
  {
    LogFiles.Clear();

    string[] files = MLUtility.GetDataFiles();
    foreach (string file in files)
    {
      string fName = file.Substring(file.LastIndexOf(Path.DirectorySeparatorChar) + 1);
      if (fName.StartsWith("MLogger"))
        LogFiles.Add(new logFile(fName, this));
    }
  }

  protected override void OnAppearing()
  {
    base.OnAppearing();

    UpdateLogFiles();
  }

  private void fileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
  {
    if (e.CurrentSelection == null || e.CurrentSelection.Count == 0) return;

    LoggingData ld = new LoggingData();
    ld.LoadData(((logFile)e.CurrentSelection[0]).FileName);
    Navigation.PushAsync(ld);
  }

  #region インナークラス定義

  public class delCommand : ICommand
  {

    public event EventHandler CanExecuteChanged;

    public bool CanExecute(object parameter)
    { return true; }

    public void Execute(object parameter)
    {
      logFile lf = (logFile)parameter;
      MLUtility.DeleteDataFile(lf.FileName);
      lf.Parent.UpdateLogFiles();
    }

  }

  public class logFile
  {
    public logFile(string fName, LoggingDataList parent)
    {
      FileName = fName;
      Parent = parent;

      string[] bf = fName.Split('_');
      MLoggerName = "MLogger_" + bf[1];
      DTime = new DateTime(int.Parse(bf[2].Substring(0, 4)), int.Parse(bf[2].Substring(4, 2)), int.Parse(bf[2].Substring(6, 2)));

      DeleteCommand = new delCommand();
    }

    public LoggingDataList Parent { get; private set; }

    public string Name { get { return MLoggerName + ": " + DTime.ToString("yyyy/MM/dd"); } }

    public string FileName { get; private set; }

    public ICommand DeleteCommand { get; private set; }

    public string MLoggerName { get; private set; }

    public DateTime DTime { get; private set; }

  }

  #endregion

}