using System;

using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using XBee;
using XBee.Devices;

namespace MLServer
{
  class Program
  {

    #region 定数宣言

    /// <summary>XBEEの上位アドレス</summary>
    private const string XBEE_HIGHADD = "0013A200";

    /// <summary>HTMLデータを更新する時間間隔[msec]</summary>
    private const int HTML_REFRESH_SPAN = 10 * 1000;

    #endregion

    #region クラス変数

    /// <summary>UART通信のボーレート</summary>
    private static int baudRate;

    /// <summary>温冷感計算のための基準の物理量</summary>
    private static double metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue;

    /// <summary>新しいデータ収集があったか否か</summary>
    private static bool hasNewData = true;

    /// <summary></summary>
    private static string dataDirectory = AppDomain.CurrentDomain.BaseDirectory + "data";

    /// <summary>発見されたMLogger端末のリスト</summary>
    private static Dictionary<ulong, MLogger> mLoggers = new Dictionary<ulong, MLogger>();

    /// <summary>補正係数リスト</summary>
    private static Dictionary<string, string> cFactors = new Dictionary<string, string>();

    #endregion

    #region メイン処理

    static void Main(string[] args)
    {
      //データ格納用のディレクトリを作成
      dataDirectory = AppDomain.CurrentDomain.BaseDirectory + "data";
      if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);

      //補正係数読み込み
      loadCFactors();

      //温冷感計算のための代謝量[met]と着衣量[clo]を読み込む
      loadInitFile(out baudRate, out metValue, out cloValue, out dbtValue, out rhdValue, out velValue, out mrtValue);

      //子機のアドレスを読み込む
      using (StreamReader sReader = new StreamReader(AppDomain.CurrentDomain.BaseDirectory + "resume.txt"))
      {
        string buff;
        while ((buff = sReader.ReadLine()) != null)
        {
          NodeAddress add = new NodeAddress(new LongAddress(Convert.ToUInt64(XBEE_HIGHADD + buff, 16)));
          MLogger ml = new MLogger(add.LongAddress.Value);
          if (cFactors.ContainsKey(ml.LowAddress))
            ml.InitCFactors(cFactors[ml.LowAddress]);
          mLoggers.Add(add.LongAddress.Value, ml);
        }
      }

      //ポート一覧の取得
      string[] portList = System.IO.Ports.SerialPort.GetPortNames();
      string pList = "以下のPortへの接続を試みます:";
      for (int i = 0; i < portList.Length; i++) pList += " " + portList[i];
      Console.WriteLine(pList);

      //各ポートへの接続を試行
      for (int i = 0; i < portList.Length; i++)
      {
        Task tsk = makeConnectTask(portList[i]); ;
        tsk.Start();
      }

      //親機に接続とイベント登録
      /*tryTask.Run(async () =>
      {
        
        {
          Console.WriteLine("XBeeに接続中・・・");
          while (true)
          {
            string[] portList = System.IO.Ports.SerialPort.GetPortNames();
            Console.Write("Port一覧:");
            for (int i = 0; i < portList.Length; i++) Console.Write(" " + portList[i]);
            Console.WriteLine();

            var cntrl = XBeeController.FindAndOpenAsync(portList, 9600);
            XBeeController myXBee = cntrl.Result;
            if (myXBee != null)
            {
              var s2 = new XBeeSeries2(myXBee);
              var serial = await s2.GetSerialNumberAsync();
              Console.WriteLine("接続成功。S/N = " + serial);

              //データ受信時の処理
              myXBee.DataReceived += Controller_DataReceived;
              break;
            }
            else
            {
              Console.WriteLine("再試行中");
              Thread.Sleep(1000);
            }
          }
        }
        catch (Exception e)
        {
          Console.WriteLine(e.Message);
        }
    });*/

      //定期的にHTMLファイルを更新する
      Task htmlRefreshTask = Task.Run(() =>
      {
        while (true)
        {
          if (hasNewData)
          {
            makeWebData();
            hasNewData = false;
          }
          Thread.Sleep(HTML_REFRESH_SPAN);
        }
      });

      while (true) ;
    }

    /// <summary>Portへの接続Taskを生成</summary>
    /// <param name="pName">Port名称</param>
    /// <returns>Portへの接続Task</returns>
    private static Task makeConnectTask(string pName)
    {
      return new Task(async () =>
      {
        XBeeController ctrl = new XBeeController();
        try
        {
          await ctrl.OpenAsync(pName, baudRate);
        }
        catch (Exception ex)
        {
          Console.WriteLine(pName + ": " + ex.Message);
          return;
        }
        if (ctrl == null)
        {
          Console.WriteLine(pName + ": 接続失敗");
          return;
        }

        var s2 = new XBeeSeries2(ctrl);
        var serial = await s2.GetSerialNumberAsync();
        Console.WriteLine(pName + ": " + " 接続成功。S/N = " + serial);

        //データ受信時の処理
        ctrl.DataReceived += Controller_DataReceived;

      });
    }

    /// <summary>補正係数を読み込む</summary>
    private static void loadCFactors()
    {
      string fPath = dataDirectory + Path.DirectorySeparatorChar + "cf.ini";
      if (File.Exists(fPath))
      {
        try
        {
          using (StreamReader sReader = new StreamReader(fPath, Encoding.GetEncoding("UTF-8")))
          {
            string bf;
            while ((bf = sReader.ReadLine()) != null)
              cFactors.Add(bf.Substring(0, bf.IndexOf(',')), bf.Substring(bf.IndexOf(',') + 1));
          }
        }
        catch { Console.WriteLine("cr.iniの読み込みに失敗しました"); }
      }
    }

    private static void loadInitFile
      (out int brate, out double met, out double clo, out double dbt, out double rhd, out double vel, out double mrt)
    {
      brate = 9600;
      met = 1.1;
      clo = 1.0;
      dbt = 25.0;
      rhd = 50.0;
      vel = 0.2;
      mrt = 25.0;

      using (StreamReader sReader = new StreamReader
        (AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "setting.ini"))
      {
        string line;
        while ((line = sReader.ReadLine()) != null)
        {
          line = line.Remove(line.IndexOf(';'));
          string[] st = line.Split('=');
          switch (st[0])
          {
            case "baud_rate":
              brate = int.Parse(st[1]);
              break;
            case "met":
              met = double.Parse(st[1]);
              break;
            case "clo":
              clo = double.Parse(st[1]);
              break;
            case "dbt":
              dbt = double.Parse(st[1]);
              break;
            case "rhd":
              rhd = double.Parse(st[1]);
              break;
            case "vel":
              vel = double.Parse(st[1]);
              break;
            case "mrt":
              mrt = double.Parse(st[1]);
              break;
          }
        }
      }
    }

    #endregion

    #region XBee通信処理

    /// <summary>データ受信時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private static void Controller_DataReceived(object sender, SourcedDataReceivedEventArgs e)
    {
      if (!mLoggers.ContainsKey(e.Address.LongAddress.Value)) return; //未登録のノードからの受信は無視

      //HTML更新フラグを立てる
      hasNewData = true;

      MLogger mlg = mLoggers[e.Address.LongAddress.Value];
      mlg.LastCommunication = DateTime.Now;

      //受信データを追加
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      char[] chars = Encoding.GetEncoding("Shift_JIS").GetString(e.Data).ToCharArray();
      mlg.AddReceivedData(new string(chars));

      //コマンドがある限り処理を続ける
      while (mlg.HasCommand)
      {
        string command = mlg.GetCommand();
        Console.WriteLine(MLogger.GetLowAddress(e.Address.LongAddress.Value) + " : " + command);
        if (solveCommand(e.Address, command)) mlg.RemoveCommand(); //処理に成功した場合はコマンドを削除
        else break; //処理に失敗した場合には次回に再挑戦
      }

    }

    /// <summary>受信データを処理する</summary>
    /// <param name="add">送信元アドレス</param>
    /// <param name="data">受信データ</param>
    /// <returns>コマンドが処理できたか否か</returns>
    private static bool solveCommand(NodeAddress add, string command)
    {
      //DATであれば書き出す*******************************
      if (command.StartsWith("DTT"))
      {
        //データ書き出し
        string fName = dataDirectory + Path.DirectorySeparatorChar + MLogger.GetLowAddress(add.LongAddress.Value) + ".csv";

        try
        {
          using (StreamWriter sWriter = new StreamWriter(fName, true, Encoding.GetEncoding("Shift_JIS")))
          {
            DateTime now;
            double tmp, hmd, glbV, glb, velV, vel, ill;
            mLoggers[add.LongAddress.Value].SolveDTT(command, out now, out tmp, out hmd, out glbV, out glb, out velV, out vel, out ill);
            sWriter.WriteLine(
              now.ToString("yyyy/MM/dd HH:mm:ss") + "," +
              tmp.ToString("F2") + "," + hmd.ToString("F2") + "," +
              glbV.ToString("F3") + "," + glb.ToString("F2") + "," +
              velV.ToString("F3") + "," + vel.ToString("F4") + "," +
              ill.ToString("F2"));
          }
        }
        catch
        {
          Console.WriteLine(fName + "が使用中です");
          return false;
        }
      }

      return true;
    }

    #endregion

    #region WEBサーバーデータの生成処理

    private static void makeWebData()
    {
      MLogger[] loggers = new MLogger[mLoggers.Values.Count];
      mLoggers.Values.CopyTo(loggers, 0);

      string html = MLogger.MakeListHTML(Resources.topPage_html, loggers, metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue);
      using (StreamWriter sWriter = new StreamWriter
        (dataDirectory + Path.DirectorySeparatorChar + "index.htm", false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
      { sWriter.Write(html); }

      string latestData = MLogger.MakeLatestData(loggers, metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue);
      using (StreamWriter sWriter = new StreamWriter
        (dataDirectory + Path.DirectorySeparatorChar + "latest.txt", false, Encoding.UTF8))
      { sWriter.Write(latestData); }
    }

    #endregion

  }
}
