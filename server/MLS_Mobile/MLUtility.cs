using System.Text;

namespace MLS_Mobile
{
  /// <summary>ユーティリティクラス</summary>
  public static class MLUtility
  {

    #region 定数宣言

    /// <summary>データフォルダの名称</summary>
    private const string DATA_DIR_NAME = "DATA";

    /// <summary>SDカード使用に関する初期設定ファイル</summary>
    private const string SD_F_NAME = "sd.ini";

    #endregion

    #region データ入出力処理

    /// <summary>データ保存ディレクトリを用意する</summary>
    public static void InitDirAndFiles()
    {
      string dFolder = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME;
      if (!Directory.Exists(dFolder))
        Directory.CreateDirectory(dFolder);

      string sdFPath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + SD_F_NAME;
      if (!File.Exists(sdFPath))
      {
        using (StreamWriter sWriter = new StreamWriter(sdFPath, false))
        {
          sWriter.Write("0");
        }
      }
    }

    /// <summary>データファイルリストを取得する</summary>
    /// <returns>データファイルリスト</returns>
    public static string[] GetDataFiles()
    {
      string folder = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME;
      return Directory.GetFiles(folder);
    }

    /// <summary>データファイルの内容を取得する</summary>
    /// <param name="fileName">データファイル名称</param>
    /// <param name="maxLine">読み込む最大行数</param>
    /// <returns>データファイルの内容</returns>
    public static string LoadDataFile(string fileName, int maxLine)
    {
      string filePath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME
        + Path.DirectorySeparatorChar + fileName;

      //先頭のmaxLine行を読み込む
      int lines = 0;
      StringBuilder sBuilder = new StringBuilder();
      using (StreamReader sReader = new StreamReader(filePath, Encoding.UTF8))
      {
        string buff;
        while ((buff = sReader.ReadLine()) != null && lines < maxLine)
        {
          sBuilder.AppendLine(buff);
          lines++;
        }
        return sBuilder.ToString();
      }
    }

    /// <summary>データファイルの内容を取得する</summary>
    /// <param name="fileName">データファイル名称</param>
    /// <returns>データファイルの内容</returns>
    public static string LoadDataFile(string fileName)
    {
      return LoadDataFile(fileName, int.MaxValue);
    }

    /// <summary>データファイルに追記する</summary>
    /// <param name="fileName">データファイル名称</param>
    /// <param name="data">追記するデータ</param>
    public static void AppendData(string fileName, string data)
    {
      string filePath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME
        + Path.DirectorySeparatorChar + fileName;
      using (StreamWriter sWriter = new StreamWriter(filePath, true))
      {
        sWriter.Write(data);
      }
    }

    public static void DeleteDataFile(string fileName)
    {
      string filePath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME
        + Path.DirectorySeparatorChar + fileName;
      if(File.Exists(filePath))
        File.Delete(filePath);
    }

    /// <summary>ファイルサイズ[byte]を取得する</summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    public static long GetFileSize(string fileName)
    {
      string filePath = FileSystem.Current.AppDataDirectory + Path.DirectorySeparatorChar + DATA_DIR_NAME
        + Path.DirectorySeparatorChar + fileName;
      if (File.Exists(filePath))
      {
        FileInfo file = new FileInfo(filePath);
        return file.Length;
      }
      return 0;
    }

    #endregion

  }
}
