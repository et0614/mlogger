using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using System.IO;
using System.Threading;

using XBeeLibrary.Core;
using XBeeLibrary.Core.Models;

namespace MLServer
{
  public partial class MainForm : Form
  {

    #region 定数宣言

    /// <summary>XBEE端末の共通上部アドレス</summary>
    private const string UP_ADD = "0013A200";

    /// <summary>ログの最大表示文字数</summary>
    private const int MAX_LOG_LENGTH = 5000;

    /// <summary>HTMLデータを更新する時間間隔[msec]</summary>
    private const int HTML_REFRESH_SPAN = 10 * 1000;

    /// <summary>ログ表示を更新する時間間隔[msec]</summary>
    private const int LOG_REFRESH_SPAN = 1 * 1000;

    #endregion

    #region readonly 初期化パラメータ

    /// <summary>代謝量[met]</summary>
    private static readonly double metValue = 1.1;

    /// <summary>着衣量[clo]</summary>
    private static readonly double cloValue = 1.0;

    /// <summary>乾球温度[C]</summary>
    private static readonly double dbtValue = 25.0;

    /// <summary>相対湿度[%]</summary>
    private static readonly double rhdValue = 50.0;

    /// <summary>相対気流速度[m/s]</summary>
    private static readonly double velValue = 0.2;

    /// <summary>平均放射温度[C]</summary>
    private static readonly double mrtValue = 25.0;

    /// <summary>UART通信のボーレート</summary>
    private static readonly int baudRate = 9600;

    /// <summary>設定および計測開始命令の送信をずらす秒数[msec]</summary>
    private static readonly int cmdSSpan = 500;

    /// <summary>補正係数設定ボタンを表示するか否か</summary>
    private static readonly bool showCFButton = false;

    /// <summary>SDカード書き出しを有効化するか否か</summary>
    private static readonly bool enableSDOutput = false;

    /// <summary>予め登録された計器からの計測信号を自動で記録するか否か</summary>
    private static readonly bool autoLogging = true;

    #endregion

    #region クラス変数

    /// <summary>受信パケット総量[bytes]</summary>
    private static int pBytes = 0;

    /// <summary>新しいデータ収集があったか否か</summary>
    private volatile bool hasNewData = true;

    /// <summary>新しいログがあったか否か</summary>
    private volatile bool hasNewLog = true;

    /// <summary>データディレクトリのパス</summary>
    private string dataDirectory;

    /// <summary>XBee端末と接続されたポート名のリスト</summary>
    private List<string> connectedPorts = new List<string>();

    /// <summary>発見されたMLogger端末のリスト</summary>
    private Dictionary<string, MLogger> mLoggers = new Dictionary<string, MLogger>();

    /// <summary>MLoggerに関連付けられたリストビューのリスト</summary>
    private Dictionary<MLogger, ListViewItem> lvItems = new Dictionary<MLogger, ListViewItem>();

    /// <summary>通信用XBee端末リスト</summary>
    private Dictionary<ZigBeeDevice, xbeeInfo> myXBees = new Dictionary<ZigBeeDevice, xbeeInfo>();

    /// <summary>補正係数リスト</summary>
    private Dictionary<string, string> cFactors = new Dictionary<string, string>();

    /// <summary>ログの一時保存</summary>
    private StringBuilder logString = new StringBuilder();

    /// <summary>並び替えが昇順か否か</summary>
    private bool[] isAscending = new bool[20];

    /// <summary>補正係数設定用フォーム</summary>
    private CFForm cfForm;

    #endregion

    #region コンストラクタ

    /// <summary>staticコンストラクタ：初期化パラメータ読み込み</summary>
    static MainForm()
    {
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
              baudRate = int.Parse(st[1]);
              break;
            case "met":
              metValue = double.Parse(st[1]);
              break;
            case "clo":
              cloValue = double.Parse(st[1]);
              break;
            case "dbt":
              dbtValue = double.Parse(st[1]);
              break;
            case "rhd":
              rhdValue = double.Parse(st[1]);
              break;
            case "vel":
              velValue = double.Parse(st[1]);
              break;
            case "mrt":
              mrtValue = double.Parse(st[1]);
              break;
            case "sspan":
              cmdSSpan = int.Parse(st[1]);
              break;
            case "show_cfactor":
              showCFButton = bool.Parse(st[1]);
              break;
            case "enable_sd":
              enableSDOutput = bool.Parse(st[1]);
              break;
          }
        }
      }
    }

    /// <summary>インスタンスを初期化する</summary>
    public MainForm()
    {
      InitializeComponent();

      //コントローラの国際化対応
      initControlLang();

      //コントローラ表示の初期化
      btn_outputSD.Enabled = enableSDOutput;
      btn_setCFactor.Visible = showCFButton;

      //データ格納用のディレクトリを作成
      dataDirectory = AppDomain.CurrentDomain.BaseDirectory + "data";
      if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);

      //XBee端末切断状態から開始
      disconnectXBee();

      //定期的にHTMLファイルを更新する
      Task htmlRefreshTask = Task.Run(() =>
      {
        while (true)
        {
          if (hasNewData)
          {
            hasNewData = false;
            makeWebData();
          }
          Thread.Sleep(HTML_REFRESH_SPAN);
        }
      });

      //定期的にログ表示を更新する
      Task logRefreshTask = Task.Run(() =>
      {
        while (true)
        {
          if (hasNewLog)
          {
            hasNewLog = false;
            try
            {
              refreshLog();
            }
            catch (Exception ex)
            {
              appendErrorLog(ex.Message);
              appendErrorLog(logString.ToString());
            }
          }
          Thread.Sleep(LOG_REFRESH_SPAN);
        }
      });

      //子機のアドレスを読み込む
      string rsPath = AppDomain.CurrentDomain.BaseDirectory + "resume.txt";
      if (File.Exists(rsPath))
      {
        using (StreamReader sReader = new StreamReader(rsPath))
        {
          string buff;
          while ((buff = sReader.ReadLine()) != null)
          {
            MLogger ml = new MLogger(buff);
            if (cFactors.ContainsKey(ml.LowAddress))
              ml.InitCFactors(cFactors[ml.LowAddress]);
            mLoggers.Add(ml.LongAddress, ml);
          }
        }
      }

    }

    /// <summary>コントロールの国際化対応処理</summary>
    private void initControlLang()
    {
      lbl_th.Text = i18n.Resources.DBTemp + "/" + i18n.Resources.RHumid;
      lbl_glb.Text = i18n.Resources.GlbTemp;
      lbl_vel.Text = i18n.Resources.Velocity;
      rbtn_ill.Text = i18n.Resources.Illuminance;
      rbtn_prox.Text = i18n.Resources.Proximity;
      lbl_gpv1.Text = i18n.Resources.GeneralPurposeVoltage + " 1";
      lbl_gpv2.Text = i18n.Resources.GeneralPurposeVoltage + " 2";
      lbl_gpv3.Text = i18n.Resources.GeneralPurposeVoltage + " 3";
      lbl_SDTime.Text = i18n.Resources.MF_MStartDTime;

      cbx_thMeasure.Text = cbx_glbMeasure.Text = cbx_velMeasure.Text = cbx_illMeasure.Text
        = cbx_gpv1Measure.Text = cbx_gpv2Measure.Text = cbx_gpv3Measure.Text = i18n.Resources.MF_MeasureCBX;

      btn_applySetting.Text = i18n.Resources.MF_ApplySetting;
      btn_startCollecting.Text = i18n.Resources.MF_StartDataCollecting;
      btn_outputSD.Text = i18n.Resources.MF_StartLoggingToSDCard;
      btn_setCFactor.Text = i18n.Resources.MF_SetCorrectionFactors;

      lvhd_xbeeID.Text = "ID";
      lvhd_xbeeName.Text = i18n.Resources.Name;
      lvhd_step.Text = i18n.Resources.Status;
      lvhd_thMeasure.Text = i18n.Resources.DBTandRH;
      lvhd_thInterval.Text = i18n.Resources.Interval;
      lvhd_glvMeasure.Text = i18n.Resources.GlbTemp;
      lvhd_glvInterval.Text = i18n.Resources.Interval;
      lvhd_velMeasure.Text = i18n.Resources.Velocity;
      lvhd_velInterval.Text = i18n.Resources.Interval;
      lvhd_illMeasure.Text = i18n.Resources.Illuminance;
      lvhd_illInterval.Text = i18n.Resources.Interval;
      lvhd_startTime.Text = i18n.Resources.MF_MStartDTime;
      lvhd_gv1Measure.Text = i18n.Resources.GeneralPurposeVoltage + "1";
      lvhd_gv1Interval.Text = i18n.Resources.Interval;
      lvhd_gv2Measure.Text = i18n.Resources.GeneralPurposeVoltage + "2";
      lvhd_gv2Interval.Text = i18n.Resources.Interval;
      lvhd_gv3Measure.Text = i18n.Resources.GeneralPurposeVoltage + "3";
      lvhd_gv3Interval.Text = i18n.Resources.Interval;
      lvhd_prxMeasure.Text = i18n.Resources.Proximity;
    }

    #endregion

    #region コントロールの描画更新処理

    private delegate void refreshLogDelegate();

    private delegate void initListViewItemDelegate(string longAddress);

    private delegate void setTSBtnStateDelegate
      (ToolStripButton tsBtn, bool enabled, Image img, string text, string toolTipText);

    private delegate void setTSSBtnStateDelegate
      (ToolStripSplitButton tssBtn, bool enabled, Image img, string text, string toolTipText);

    private delegate void setCurrentStateDelegate
      (ListViewItem item, string state);

    private delegate void setListViewContentsDelegate
      (ListViewItem item, string name, string state, 
      string measureTH, string intervalTH, string measureGlb, string intervalGlb, string measureVel, string intervalVel, 
      string measureIlm, string intervalIlm,
      string measureGV1, string intervalGV1, string measureGV2, string intervalGV2, string measureGV3, string intervalGV3, 
      string measurePrx, string startTime);

    /// <summary>ログ表示を更新する</summary>
    /// <param name="log">ログの内容</param>
    private void refreshLog()
    {
      if (InvokeRequired)
      {
        Invoke(new refreshLogDelegate(refreshLog));
        return;
      }

      tbx_log.Text = logString.ToString();
      tbx_log.SelectionStart = tbx_log.Text.Length; //キャレットを末尾に
      tbx_log.ScrollToCaret();
    }

    /// <summary>XBee端末をリストに追加する</summary>
    /// <param name="name"></param>
    private void initListViewItem(string longAddress)
    {
      if (InvokeRequired)
      {
        Invoke(new initListViewItemDelegate(initListViewItem), longAddress);
        return;
      }

      //リストビューに追加
      if (mLoggers.ContainsKey(longAddress))
      {
        MLogger ml = mLoggers[longAddress];
        if (!lvItems.ContainsKey(ml))
        {
          ListViewItem lvm = new ListViewItem(new string[]
          { ml.LowAddress, ml.Name, i18n.Resources.MF_Initializing, "true", "60", "true", "60", "true", "600", "true", "60", "2000/01/01 00:00", "true", "60", "true", "60", "true", "60", "false" });
          lvItems.Add(ml, lvm);
          lv_setting.Items.Add(lvm);
        }
      }
    }

    /// <summary>ToolStripの設定</summary>
    /// <param name="tsBtn"></param>
    /// <param name="enabled"></param>
    /// <param name="img"></param>
    /// <param name="text"></param>
    /// <param name="toolTipText"></param>
    private void setTSBtnState
      (ToolStripButton tsBtn, bool enabled, Image img, string text, string toolTipText)
    {
      if (InvokeRequired)
      {
        Invoke(new setTSBtnStateDelegate(setTSBtnState),
          tsBtn, enabled, img, text, toolTipText);
        return;
      }
      tsBtn.Image = img;
      tsBtn.Text = text;
      tsBtn.ToolTipText = toolTipText;
      tsBtn.Enabled = enabled;
    }

    /// <summary>ToolStripSplitButtonの設定</summary>
    /// <param name="tsBtn"></param>
    /// <param name="enabled"></param>
    /// <param name="img"></param>
    /// <param name="text"></param>
    /// <param name="toolTipText"></param>
    private void setTSSBtnState
      (ToolStripSplitButton tsBtn, bool enabled, Image img, string text, string toolTipText)
    {
      if (InvokeRequired)
      {
        Invoke(new setTSSBtnStateDelegate(setTSSBtnState),
          tsBtn, enabled, img, text, toolTipText);
        return;
      }
      tsBtn.Image = img;
      tsBtn.Text = text;
      tsBtn.ToolTipText = toolTipText;
      tsBtn.Enabled = enabled;
    }

    /// <summary>現在の状態を更新する</summary>
    /// <param name="item"></param>
    /// <param name="state"></param>
    private void setCurrentState(ListViewItem item, string state)
    {
      if (InvokeRequired)
      {
        Invoke(new setCurrentStateDelegate(setCurrentState), item, state);
        return;
      }
      item.SubItems[2].Text = state;
    }

    /// <summary>リストビューに測定内容を設定する</summary>
    /// <param name="item"></param>
    /// <param name="measureTH"></param>
    /// <param name="intervalTH"></param>
    /// <param name="measureGlb"></param>
    /// <param name="intervalGlb"></param>
    /// <param name="measureVel"></param>
    /// <param name="intervalVel"></param>
    private void setListViewContents
      (ListViewItem item, string name, string state,
      string measureTH, string intervalTH,
      string measureGlb, string intervalGlb,
      string measureVel, string intervalVel,
      string measureIlm, string intervalIlm,
      string measureGV1, string intervalGV1,
      string measureGV2, string intervalGV2,
      string measureGV3, string intervalGV3,
      string measurePrx,
      string startTime)
    {
      if (InvokeRequired)
      {
        Invoke(new setListViewContentsDelegate(setListViewContents), 
          item, name, state, measureTH, intervalTH, measureGlb, intervalGlb, measureVel, intervalVel, measureIlm, intervalIlm,
          measureGV1, intervalGV1, measureGV2, intervalGV2, measureGV3, intervalGV3, measurePrx, startTime);
        return;
      }
      item.SubItems[1].Text = name;
      item.SubItems[2].Text = state;
      item.SubItems[3].Text = measureTH;
      item.SubItems[4].Text = intervalTH;
      item.SubItems[5].Text = measureGlb;
      item.SubItems[6].Text = intervalGlb;
      item.SubItems[7].Text = measureVel;
      item.SubItems[8].Text = intervalVel;
      item.SubItems[9].Text = measureIlm;
      item.SubItems[10].Text = intervalIlm;
      item.SubItems[11].Text = startTime;
      item.SubItems[12].Text = measureGV1;
      item.SubItems[13].Text = intervalGV1;
      item.SubItems[14].Text = measureGV2;
      item.SubItems[15].Text = intervalGV2;
      item.SubItems[16].Text = measureGV3;
      item.SubItems[17].Text = intervalGV3;
      item.SubItems[18].Text = measurePrx;
    }

    #endregion

    #region その他の処理

    /// <summary>ログに追加する</summary>
    /// <param name="log">追加するログ</param>
    private void appendLog(string log)
    {
      lock (logString)
      {
        log = log.Replace("\r", "").Replace("\n", ""); //改行コードは除く
        logString.AppendLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss : ") + log);

        if (MAX_LOG_LENGTH < logString.Length) logString.Remove(0, Math.Max(logString.Length - MAX_LOG_LENGTH, 0));
        hasNewLog = true;
      }
    }

    /// <summary>エラーログを保存</summary>
    /// <param name="log"></param>
    private void appendErrorLog(string log)
    {
      using (StreamWriter sWriter = new StreamWriter("errLog.txt", true))
      {
        sWriter.WriteLine(log);
      }
    }

    /// <summary>子機のLongAddressを管理する通信用XBee端末を取得する</summary>
    /// <param name="address">子機のLongAddress</param>
    /// <returns>通信用XBee端末</returns>
    private ZigBeeDevice getXBee(string address)
    {
      foreach (ZigBeeDevice key in myXBees.Keys)
        if (myXBees[key].longAddress.Contains(address)) return key;
      return null;
    }

    #endregion

    #region 通信イベント処理

    /// <summary>受信データを処理する</summary>
    /// <param name="add">送信元アドレス</param>
    /// <param name="data">受信データ</param>
    /// <returns>コマンドが処理できたか否か</returns>
    private bool solveCommand(string add, string command)
    {
      //DTTであれば書き出す*******************************
      if (command.StartsWith("DTT") && mLoggers.ContainsKey(add))
      {
        MLogger ml = mLoggers[add];
        if (lvItems.ContainsKey(ml))
          setCurrentState(lvItems[ml], i18n.Resources.MF_Measuring);

        //データ書き出し
        string fName = dataDirectory + Path.DirectorySeparatorChar + ml.LowAddress + ".csv";

        try
        {
          using (StreamWriter sWriter = new StreamWriter(fName, true, Encoding.UTF8))
          {
            DateTime mlNow;
            double tmp, hmd, glbV, glb, velV, vel, ill, gpV1, gpV2, gpV3;
            mLoggers[add].SolveDTT(command, out mlNow, out tmp, out hmd, out glbV, out glb, out velV, out vel, out ill, out gpV1, out gpV2, out gpV3);
            sWriter.WriteLine(
              DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + "," + //親機の現在時刻
              mlNow.ToString("yyyy/MM/dd HH:mm:ss") + "," + //子機の現在時刻
              tmp.ToString("F2") + "," + hmd.ToString("F2") + "," +
              glbV.ToString("F3") + "," + glb.ToString("F2") + "," +
              velV.ToString("F3") + "," + vel.ToString("F4") + "," +
              ill.ToString("F2") + "," + gpV1.ToString("F3") + "," + gpV2.ToString("F3") + "," + gpV3.ToString("F3"));
          }
        }
        catch
        {
          appendLog(String.Format(i18n.Resources.MF_FileIsUsing, fName));
          return false;
        }

        //通信中のアイコン明滅
        Task.Run(async () =>
        {
          setTSBtnState(tsb_downloading, true, Properties.Resources.downloading, "", "");
          await Task.Delay(300);
          setTSBtnState(tsb_downloading, true, Properties.Resources.waiting, "", "");
        });
      }

      //CMSかLMSであればListViewに計測設定を反映************************
      else if (command.StartsWith("CMS") || command.StartsWith("LMS"))
      {
        if (mLoggers.ContainsKey(add))
        {
          string[] buff = command.Substring(4, command.Length - 4).Split(',');
          MLogger ml = mLoggers[add];
          setListViewContents(lvItems[ml], ml.Name, i18n.Resources.MF_Editable,
            (buff[0] == "1") ? "true" : "false", buff[1],
            (buff[2] == "1") ? "true" : "false", buff[3],
            (buff[4] == "1") ? "true" : "false", buff[5],
            (buff[6] == "1") ? "true" : "false", buff[7],
            (buff[9] == "1") ? "true" : "false", buff[10],
            (buff[11] == "1") ? "true" : "false", buff[12],
            (buff[13] == "1") ? "true" : "false", buff[14],
            (buff[15] == "1") ? "true" : "false",
            MLogger.GetDateTimeFromUTime(long.Parse(buff[8])).ToString("yyyy/MM/dd HH:mm"));
        }
      }

      //DMYであれば計測中
      else if (command.StartsWith("DMY") && mLoggers.ContainsKey(add) && lvItems.ContainsKey(mLoggers[add]))
        setCurrentState(lvItems[mLoggers[add]], i18n.Resources.MF_Measuring);

      //WFCであればコマンド入力待ち
      else if (command.StartsWith("WFC") && mLoggers.ContainsKey(add) && lvItems.ContainsKey(mLoggers[add]))
        setCurrentState(lvItems[mLoggers[add]], i18n.Resources.MF_Editable);

      //STLであれば計測開始
      else if (command.StartsWith("STL") && mLoggers.ContainsKey(add) && lvItems.ContainsKey(mLoggers[add]))
        setCurrentState(lvItems[mLoggers[add]], i18n.Resources.MF_Measuring);

      //VERであればバージョン情報更新
      else if (command.StartsWith("VER") && mLoggers.ContainsKey(add))
        mLoggers[add].SetVersion(command.Remove(0, 4));

      //SCFかLCFの場合には補正係数表示を更新
      else if (command.StartsWith("SCF") || command.StartsWith("LCF"))
      {
        //補正係数を読み込む
        MLogger ml = mLoggers[add];
        ml.LoadCFactors(command);

        //表示中の補正係数設定フォームがあれば反映
        if (cfForm != null)
          Invoke(new UpdateCFactorsDelegate(cfForm.UpdateCFactors));
      }

      return true;
    }

    public delegate void UpdateCFactorsDelegate();

    private void Net_DeviceDiscovered(object sender, XBeeLibrary.Core.Events.DeviceDiscoveredEventArgs e)
    {
      //HTML更新フラグを立てる
      hasNewData = true;

      RemoteXBeeDevice rdv = e.DiscoveredDevice;
      ZigBeeDevice dv = rdv.GetLocalXBeeDevice() as ZigBeeDevice;

      //MLoggerリストに追加
      string add = rdv.GetAddressString();
      if (!mLoggers.ContainsKey(add))
      {
        MLogger ml = new MLogger(add);
        if (cFactors.ContainsKey(ml.LowAddress))
          ml.InitCFactors(cFactors[ml.LowAddress]);
        mLoggers.Add(add, ml);

        //プログラム異常停止に備えてResumeリストに追加
        updateResumeNodeList();
      }

      //子機のアドレスと通信用XBeeを対応付ける
      if (!myXBees[dv].longAddress.Contains(add))
        myXBees[dv].longAddress.Add(add);

      //リストビューに追加
      initListViewItem(add);

      //測定設定情報を得る
      Task.Run(() =>
      {
        try
        {
          dv.SendData(rdv, Encoding.ASCII.GetBytes("\rLMS\r")); //\rを送ってからコマンドを送ると安心
          dv.SendData(rdv, Encoding.ASCII.GetBytes("\rVER\r")); //\rを送ってからコマンドを送ると安心
        }
        catch (Exception e)
        {
          appendLog("Error :" + dv.XBee16BitAddr + ": " + e.Message);
        }
      });
    }

    private void Device_DataReceived(object sender, XBeeLibrary.Core.Events.DataReceivedEventArgs e)
    {
      RemoteXBeeDevice rdv = e.DataReceived.Device;
      string add = rdv.GetAddressString();
      string rcvStr = e.DataReceived.DataString;

      if (!mLoggers.ContainsKey(add)) return; //未登録のノードからの受信は無視

      //HTML更新フラグを立てる
      hasNewData = true;

      MLogger mlg = mLoggers[add];
      mlg.LastCommunication = DateTime.Now;

      //受信データを追加
      mlg.AddReceivedData(rcvStr);

      //3コマンドまでは受け付ける
      int comNum = 0;
      string command;
      while ((command = mlg.GetCommand()) != null && comNum < 3)
      {
        //ここは確実に落ちないようにしないと、ロギング全体が止まる
        try
        {
          appendLog(mlg.LowAddress + " : " + command);
          if (solveCommand(add, command)) mlg.RemoveCommand(); //処理に成功した場合はコマンドを削除
          else break; //処理に失敗した場合には次回に再挑戦
        }
        catch (Exception exc)
        {
          appendErrorLog(mlg.LowAddress + " : " + exc.Message);
          mlg.ClearCommand(); //異常終了時はコマンドを全消去する
        }
        comNum++;
      }

      //受信パケット総量が48500bytesを超えた場合に再接続
      //XBeeLibrary.Coreのバグなのか、48500byteあたりで落ちるため
      //かなりいい加減でデータの取りこぼしが発生しかねない処理。
      if (45000 < pBytes)
      {
        while (true)
        {
          try
          {
            XBeeDevice dvv = (XBeeDevice)e.DataReceived.Device.GetLocalXBeeDevice();

            dvv.DataReceived -= Device_DataReceived;
            dvv.PacketReceived -= Device_PacketReceived;
            dvv.Close();

            pBytes = 0;

            dvv.Open();
            dvv.DataReceived += Device_DataReceived;
            dvv.PacketReceived += Device_PacketReceived;
            return;
          }
          catch
          {
            appendLog("Re-connect Error");
          }
        }

      }
    }

    private void Device_PacketReceived(object sender, XBeeLibrary.Core.Events.PacketReceivedEventArgs e)
    {
      //受信パケット総量を加算
      pBytes += e.ReceivedPacket.PacketLength;
    }

    #endregion

    #region Coordinator端末の接続切断処理

    /// <summary>すべてのXBee端末を切り離す</summary>
    private void disconnectXBee()
    {
      foreach (ZigBeeDevice key in myXBees.Keys)
        disconnectXBee(myXBees[key].portName);
      connectedPorts.Clear();
      lv_setting.Items.Clear();
      mLoggers.Clear();

      //接続ボタンを設定
      setTSSBtnState
        (tssb_connection, true, Properties.Resources.cnct, i18n.Resources.MF_Connect, i18n.Resources.MF_Connect_Def);

      //XBee端末探索ボタンを無効化
      setTSBtnState
        (tsb_reload, false, Properties.Resources.reload, i18n.Resources.MF_Search, "");

      //データ受信ボタンを無効化
      setTSBtnState
        (tsb_downloading, false, Properties.Resources.waiting, "", "");
    }

    private void disconnectXBee(string portName)
    {
      foreach (ZigBeeDevice key in myXBees.Keys)
      {
        xbeeInfo xInfo = myXBees[key];
        if (xInfo.portName == portName)
        {
          //ListViewの更新処理
          for (int i = 0; i < lv_setting.Items.Count; i++)
            if (key == getXBee(UP_ADD + lv_setting.Items[i].SubItems[0].Text))
              lv_setting.Items.RemoveAt(i);

          //イベント解除
          key.GetNetwork().DeviceDiscovered -= Net_DeviceDiscovered;
          key.DataReceived -= Device_DataReceived;

          key.Close();
          myXBees.Remove(key);
        }
      }
      if (connectedPorts.Contains(portName))
        connectedPorts.Remove(portName);

      //接続端末がなくなった場合には
      if (connectedPorts.Count == 0)
      {
        lv_setting.Items.Clear();
        mLoggers.Clear();

        //接続ボタンを設定
        setTSSBtnState
          (tssb_connection, true, Properties.Resources.cnct, i18n.Resources.MF_Connect, i18n.Resources.MF_Connect_Def);

        //XBee端末探索ボタンを無効化
        setTSBtnState
          (tsb_reload, false, Properties.Resources.reload, i18n.Resources.MF_Search, "");

        //データ受信ボタンを無効化
        setTSBtnState
          (tsb_downloading, false, Properties.Resources.waiting, "", "");
      }
    }

    #endregion

    #region コントロール操作時の処理

    /// <summary>XBee端末接続・切断ボタンクリック時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void tsb_connection_Click(object sender, EventArgs e)
    {
      //接続されている場合には切断処理
      if (connectedPorts.Count != 0) disconnectXBee();
      //切断されている場合には接続処理
      else
      {
        //ポート一覧の取得
        string[] portList = System.IO.Ports.SerialPort.GetPortNames();
        string pList = i18n.Resources.MF_PortChg;
        for (int i = 0; i < portList.Length; i++) pList += " " + portList[i];
        appendLog(pList);

        //各ポートへの接続を試行
        for (int i = 0; i < portList.Length; i++)
        {
          Task tsk = makeConnectTask(portList[i], baudRate);
          tsk.Start();
        }
      }
    }

    /// <summary>Portへの接続Taskを生成</summary>
    /// <param name="pName">Port名称</param>
    /// <returns>Portへの接続Task</returns>
    private Task makeConnectTask(string pName, int bRate)
    {
      return new Task(() =>
      {
        //通信用XBee端末をOpen
        //ZigBeeDevice device = new ZigBeeDevice(new SerialPortConnection(pName, bRate));
        ZigBeeDevice device = new ZigBeeDevice(new XBeeLibrary.Windows.Connection.Serial.WinSerialPort(pName, bRate));
        try
        {
          device.Open();
        }
        catch (Exception ex)
        {
          appendLog(pName + ": " + ex.Message);
          return;
        }
        myXBees.Add(device, new xbeeInfo(pName));
        appendLog(pName + ": " + i18n.Resources.MF_ConnectionSucceeded + " S/N = " + device.XBee64BitAddr.ToString());

        //イベント登録
        XBeeNetwork net = device.GetNetwork();
        device.GetNetwork().DeviceDiscovered += Net_DeviceDiscovered; //xbeeノード発見イベント
                                                                      //
        device.DataReceived += Device_DataReceived; //データ受信イベント
        device.PacketReceived += Device_PacketReceived;  //パケット総量を捕捉

        //Xbee端末接続ボタンの設定
        setTSSBtnState
          (tssb_connection, true, Properties.Resources.discnct, i18n.Resources.MF_Disconnect, i18n.Resources.MF_Disconnect_Def);

        //XBee端末探索ボタンの設定
        setTSBtnState
          (tsb_reload, true, Properties.Resources.reload, i18n.Resources.MF_Search, i18n.Resources.MF_Search_Def);

        connectedPorts.Add(pName);
      });
    }

    /// <summary>XBee端末探索ボタンクリック時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void tsb_reload_Click(object sender, EventArgs e)
    {
      foreach (ZigBeeDevice key in myXBees.Keys)
      {
        Task.Run(() =>
        {
          XBeeNetwork net = key.GetNetwork();

          //既に探索中の場合は一旦停止
          if (net.IsDiscoveryRunning) net.StopNodeDiscoveryProcess();

          //探索開始
          net.SetDiscoveryTimeout(5000); //5秒
          try
          {
            net.StartNodeDiscoveryProcess(); //DiscoveryProcessの二重起動で例外が発生する
          }
          catch (Exception e)
          {
            appendLog(e.Message);
          }

          appendLog(String.Format(i18n.Resources.MF_StartSearch, myXBees[key].portName));
        });
      }
    }

    /// <summary>開始ボタンクリック時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void btn_startMLogger_Click(object sender, EventArgs e)
    {
      string[] adds = new string[lv_setting.SelectedItems.Count];
      for (int i = 0; i < adds.Length; i++) adds[i] = lv_setting.SelectedItems[i].SubItems[0].Text;

      //別スレッドで計測開始処理。同時通信を防ぐため、ずらして計測を開始させる
      Task.Run(() =>
      {
        //1件ずつコマンドを送信
        for (int i = 0; i < adds.Length; i++)
        {
          //tffはxbee-on,bluetooth-off,sdcard-off
          sndMsg(UP_ADD + adds[i],
            "\rSTL" + String.Format("{0:D10}", MLogger.GetUnixTime(DateTime.Now)) + "tff\r");
          Thread.Sleep(cmdSSpan);
        }
      });
    }

    /// <summary>SDカード書き出しボタンクリック時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void btn_outputSD_Click(object sender, EventArgs e)
    {
      string[] adds = new string[lv_setting.SelectedItems.Count];
      for (int i = 0; i < adds.Length; i++) adds[i] = lv_setting.SelectedItems[i].SubItems[0].Text;

      //別スレッドで計測開始処理。同時通信を防ぐため、ずらして計測を開始させる
      Task.Run(() =>
      {
        //1件ずつコマンドを送信
        for (int i = 0; i < adds.Length; i++)
        {
          //tffはxbee-on,bluetooth-off,sdcard-off
          sndMsg(UP_ADD + adds[i],
            "\rSTL" + String.Format("{0:D10}", MLogger.GetUnixTime(DateTime.Now)) + "fft\r");
          Thread.Sleep(cmdSSpan);
        }
      });
    }

    /// <summary>並び替える</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void lv_setting_ColumnClick(object sender, ColumnClickEventArgs e)
    {
      int colNum = e.Column;
      isAscending[colNum] = !isAscending[colNum];

      lv_setting.ListViewItemSorter = new MLoggerComparer(colNum, isAscending[colNum]);
    }

    /// <summary>接続先一覧表示時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void tssb_connection_DropDownOpening(object sender, EventArgs e)
    {
      tssb_connection.DropDownItems.Clear();

      //ポート一覧の取得
      string[] pNames = System.IO.Ports.SerialPort.GetPortNames();
      for (int i = 0; i < pNames.Length; i++)
      {
        string portName = pNames[i];
        tssb_connection.DropDownItems.Add(portName);
        ToolStripItem item = tssb_connection.DropDownItems[i];
        //接続済の場合
        if (connectedPorts.Contains(pNames[i]))
        {
          item.Text = String.Format(i18n.Resources.MF_DisconnectPort, portName);
          item.ForeColor = Color.Red;
          item.Click += delegate (object sender, EventArgs e)
          {
            disconnectXBee(portName);
          };
        }
        //未接続の場合
        else
        {
          item.Text = String.Format(i18n.Resources.MF_ConnectPort, portName);
          item.ForeColor = Color.Green;
          item.Click += delegate (object sender, EventArgs e)
          {
            appendLog(String.Format(i18n.Resources.MF_TryConnectPort, portName));
            Task tsk = makeConnectTask(portName, baudRate);
            tsk.Start();
          };
        }
      }
    }

    /// <summary>XBee端末設定リスト選択時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void lv_setting_SelectedIndexChanged(object sender, EventArgs e)
    {
      bool selected = lv_setting.SelectedItems.Count != 0;
      pnl_settingEdit.Enabled = selected;

      if (!selected) return;

      ListViewItem item = lv_setting.SelectedItems[0];
      cbx_thMeasure.Checked = (item.SubItems[3].Text == "true");
      tbx_thInterval.Text = item.SubItems[4].Text;
      cbx_glbMeasure.Checked = (item.SubItems[5].Text == "true");
      tbx_glbInterval.Text = item.SubItems[6].Text;
      cbx_velMeasure.Checked = (item.SubItems[7].Text == "true");
      tbx_velInterval.Text = item.SubItems[8].Text;
      cbx_illMeasure.Checked = (item.SubItems[9].Text == "true");
      tbx_illInterval.Text = item.SubItems[10].Text;
      cbx_gpv1Measure.Checked = (item.SubItems[12].Text == "true");
      tbx_gpv1Interval.Text = item.SubItems[13].Text;
      cbx_gpv2Measure.Checked = (item.SubItems[14].Text == "true");
      tbx_gpv2Interval.Text = item.SubItems[15].Text;
      cbx_gpv3Measure.Checked = (item.SubItems[16].Text == "true");
      tbx_gpv3Interval.Text = item.SubItems[17].Text;
      rbtn_ill.Checked = (item.SubItems[18].Text != "true");
      rbtn_prox.Checked = (item.SubItems[18].Text == "true");
      dtp_timer.Value = DateTime.ParseExact(item.SubItems[11].Text, "yyyy/MM/dd HH:mm", null);

      reflectCheckBoxState();
    }

    /// <summary>補正係数設定ボタンクリック時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void btn_setCFactor_Click(object sender, EventArgs e)
    {
      //選択されていなければ無視
      if (lv_setting.SelectedIndices.Count == 0) return;
      string add = UP_ADD + lv_setting.SelectedItems[0].SubItems[0].Text;

      //受信用ウィンドウを用意
      if (cfForm != null) cfForm.Close();
      cfForm = new CFForm();
      cfForm.Logger = mLoggers[add];
      cfForm.SendMessageFnc = sndMsg;
      cfForm.Show();

      //補正係数読み込みコマンドを送信
      sndMsg(add, "\rLCF\r");
    }

    /// <summary>計測設定更新ボタンクリック時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void btn_updateMSetting_Click(object sender, EventArgs e)
    {
      if (lv_setting.SelectedItems.Count == 0) return;

      //設定内容をチェック1
      int itTH, itRD, itVL, itIL, itGV1, itGV2, itGV3;
      if (!int.TryParse(tbx_thInterval.Text, out itTH))
      {
        MessageBox.Show(String.Format(i18n.Resources.MF_Alrt_InvalidInput, i18n.Resources.DBTemp));
        return;
      }
      if (!int.TryParse(tbx_glbInterval.Text, out itRD))
      {
        MessageBox.Show(String.Format(i18n.Resources.MF_Alrt_InvalidInput, i18n.Resources.GlbTemp));
        return;
      }
      if (!int.TryParse(tbx_velInterval.Text, out itVL))
      {
        MessageBox.Show(String.Format(i18n.Resources.MF_Alrt_InvalidInput, i18n.Resources.Velocity));
        return;
      }
      if (!int.TryParse(tbx_illInterval.Text, out itIL))
      {
        MessageBox.Show(String.Format(i18n.Resources.MF_Alrt_InvalidInput, i18n.Resources.Illuminance));
        return;
      }
      if (!int.TryParse(tbx_gpv1Interval.Text, out itGV1))
      {
        MessageBox.Show(String.Format(i18n.Resources.MF_Alrt_InvalidInput, i18n.Resources.GeneralPurposeVoltage + " 1"));
        return;
      }
      if (!int.TryParse(tbx_gpv2Interval.Text, out itGV2))
      {
        MessageBox.Show(String.Format(i18n.Resources.MF_Alrt_InvalidInput, i18n.Resources.GeneralPurposeVoltage + " 2"));
        return;
      }
      if (!int.TryParse(tbx_gpv3Interval.Text, out itGV3))
      {
        MessageBox.Show(String.Format(i18n.Resources.MF_Alrt_InvalidInput, i18n.Resources.GeneralPurposeVoltage + " 3"));
        return;
      }

      //設定内容をチェック2
      if (itTH < 1 || itRD < 1 || itVL < 1 || itIL < 1 || itGV1 < 1 || itGV2 < 1 || itGV3 < 1
        || 86400 < itTH || 86400 < itRD || 86400 < itVL || 86400 < itIL || 86400 < itGV1 || 86400 < itGV2 || 86400 < itGV3)
      {
        MessageBox.Show(i18n.Resources.MF_Alrt_Interval);
        return;
      }

      //設定コマンドを作成
      string sData = "CMS"
        + (cbx_thMeasure.Checked ? "t" : "f") + string.Format("{0,5}", itTH)
        + (cbx_glbMeasure.Checked ? "t" : "f") + string.Format("{0,5}", itRD)
        + (cbx_velMeasure.Checked ? "t" : "f") + string.Format("{0,5}", itVL)
        + (cbx_illMeasure.Checked ? "t" : "f") + string.Format("{0,5}", itIL)
        + String.Format("{0, 10}", MLogger.GetUnixTime(dtp_timer.Value).ToString("F0")) //UNIX時間を10桁（空白埋め）で送信
        + (cbx_gpv1Measure.Checked ? "t" : "f") + string.Format("{0,5}", itGV1)
        + (cbx_gpv2Measure.Checked ? "t" : "f") + string.Format("{0,5}", itGV2)
        + (cbx_gpv3Measure.Checked ? "t" : "f") + string.Format("{0,5}", itGV3)
        + (rbtn_prox.Checked ? "t" : "f");

      //1件ずつコマンドを送信
      for (int i = 0; i < lv_setting.SelectedItems.Count; i++)
      {
        sndMsg(UP_ADD + lv_setting.SelectedItems[i].SubItems[0].Text,
          "\r" + sData + "\r"); //\rを送ってからコマンドを送ると安心
      }
    }

    /// <summary>計測真偽設定チェックボックス操作時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void cbx_measure_CheckedChanged(object sender, EventArgs e)
    {
      CheckBox cbx = (CheckBox)sender;
      if (cbx.Equals(cbx_thMeasure))
        lbl_th.Enabled = tbx_thInterval.Enabled = cbx.Checked;
      else if (cbx.Equals(cbx_glbMeasure))
        lbl_glb.Enabled = tbx_glbInterval.Enabled = cbx.Checked;
      else if (cbx.Equals(cbx_velMeasure))
        lbl_vel.Enabled = tbx_velInterval.Enabled = cbx.Checked;
      else if (cbx.Equals(cbx_illMeasure))
        rbtn_ill.Enabled = rbtn_prox.Enabled = tbx_illInterval.Enabled = cbx.Checked;
      else if (cbx.Equals(cbx_gpv1Measure))
        lbl_gpv1.Enabled = tbx_gpv1Interval.Enabled = cbx.Checked;
      else if (cbx.Equals(cbx_gpv2Measure))
        lbl_gpv2.Enabled = tbx_gpv2Interval.Enabled = cbx.Checked;
      else if (cbx.Equals(cbx_gpv3Measure))
        lbl_gpv3.Enabled = tbx_gpv3Interval.Enabled = cbx.Checked;
    }

    private void reflectCheckBoxState()
    {
      lbl_th.Enabled = tbx_thInterval.Enabled = cbx_thMeasure.Checked;
      lbl_glb.Enabled = tbx_glbInterval.Enabled = cbx_glbMeasure.Checked;
      lbl_vel.Enabled = tbx_velInterval.Enabled = cbx_velMeasure.Checked;
      rbtn_ill.Enabled = rbtn_prox.Enabled = tbx_illInterval.Enabled = cbx_illMeasure.Checked;
      lbl_gpv1.Enabled = tbx_gpv1Interval.Enabled = cbx_gpv1Measure.Checked;
      lbl_gpv2.Enabled = tbx_gpv2Interval.Enabled = cbx_gpv2Measure.Checked;
      lbl_gpv3.Enabled = tbx_gpv3Interval.Enabled = cbx_gpv3Measure.Checked;
    }

    private void sndMsg(string longAddress, string msg)
    {
      ZigBeeDevice xbee = getXBee(longAddress);
      if (xbee == null) return;

      //メッセージ送信
      Task.Run(() =>
      {
        RemoteXBeeDevice rmdv = xbee.GetNetwork().GetDevice(new XBee64BitAddress(longAddress));
        try
        {
          xbee.SendData(rmdv, Encoding.ASCII.GetBytes(msg));
        }
        catch { }
      });
    }

    #endregion

    #region WEBサーバーデータの生成処理

    private void makeWebData()
    {
      MLogger[] loggers = new MLogger[mLoggers.Values.Count];
      mLoggers.Values.CopyTo(loggers, 0);

      string html = MLogger.MakeListHTML(i18n.Resources.topPage_html, loggers, metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue);
      using (StreamWriter sWriter = new StreamWriter
        (dataDirectory + Path.DirectorySeparatorChar + "index.htm", false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
      { sWriter.Write(html); }

      string latestData = MLogger.MakeLatestData(loggers, metValue, cloValue, dbtValue, rhdValue, velValue, mrtValue);
      using (StreamWriter sWriter = new StreamWriter
        (dataDirectory + Path.DirectorySeparatorChar + "latest.txt", false, Encoding.UTF8))
      { sWriter.Write(latestData); }
    }

    #endregion

    #region Resume用NodeAddressリストの作成

    private void updateResumeNodeList()
    {
      List<string> addLng = new List<string>();

      if (File.Exists("resume.txt"))
      {
        using (StreamReader sReader = new StreamReader("resume.txt"))
        {
          string buff;
          while ((buff = sReader.ReadLine()) != null)
            addLng.Add(buff);
        }
      }

      foreach (string lngAdd in mLoggers.Keys)
        if (!addLng.Contains(lngAdd))
          addLng.Add(lngAdd);

      using (StreamWriter sWriter = new StreamWriter("resume.txt"))
      {
        for (int i = 0; i < addLng.Count; i++)
          sWriter.WriteLine(addLng[i]);
      }
    }

    #endregion

    #region インナークラスの定義

    /// <summary>通信用XBee端末の情報</summary>
    private class xbeeInfo
    {
      public xbeeInfo(string portName)
      {
        this.portName = portName;
      }

      public List<string> longAddress = new List<string>();

      public string portName { get; private set; }
    }

    #endregion

  }
}
