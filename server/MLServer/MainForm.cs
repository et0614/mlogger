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

using MLLib;

namespace MLServer
{
  public partial class MainForm : Form
  {

    #region 定数宣言

    /// <summary>XBEE端末の共通上部アドレス</summary>
    private const string HIGH_ADD = "0013A200";

    /// <summary>ログの最大表示文字数</summary>
    private const int MAX_LOG_LENGTH = 5000;

    /// <summary>HTMLデータを更新する時間間隔[msec]</summary>
    private const int REFRESH_HTML_TSPAN = 10 * 1000;

    /// <summary>ログ表示を更新する時間間隔[msec]</summary>
    private const int REFRESH_LOG_TSPAN = 1 * 1000;

    /// <summary>コーディネータ探索時間間隔[msec]</summary>
    private const int SCAN_COORDINATOR_TSPAN = 5 * 1000;

    /// <summary>エンドデバイス探索時間間隔[msec]</summary>
    private const int SCAN_ENDDEVICE_TSPAN = 5 * 1000;

    /// <summary>日時の型</summary>
    private const string DT_FORMAT = "yyyy/MM/dd HH:mm:ss";

    #endregion

    #region 定数宣言

    /// <summary>UART通信のボーレート</summary>
    private const int BAUD_RATE = 9600;

    /// <summary>設定および計測開始命令の送信をずらす秒数[msec]</summary>
    private const int CMD_TSPAN = 200;

    #endregion

    #region readonly 初期化パラメータ

    /// <summary>代謝量[met]</summary>
    private readonly double metValue = 1.1;

    /// <summary>着衣量[clo]</summary>
    private readonly double cloValue = 1.0;

    /// <summary>乾球温度[C]</summary>
    private readonly double dbtValue = 25.0;

    /// <summary>相対湿度[%]</summary>
    private readonly double rhdValue = 50.0;

    /// <summary>相対気流速度[m/s]</summary>
    private readonly double velValue = 0.2;

    /// <summary>平均放射温度[C]</summary>
    private readonly double mrtValue = 25.0;

    /// <summary>補正係数設定ボタンを表示するか否か</summary>
    private readonly bool showCFButton = false;

    /// <summary>SDカード書き出しを有効化するか否か</summary>
    private readonly bool enableSDOutput = false;

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
    private readonly List<string> connectedPorts = new List<string>();

    /// <summary>接続候補のポートリスト</summary>
    private readonly List<string> excludedPorts = new List<string>();

    /// <summary>発見されたMLogger端末のリスト</summary>
    private readonly Dictionary<string, MLogger> mLoggers = new Dictionary<string, MLogger>();

    /// <summary>MLoggerに関連付けられたリストビューのリスト（計測設定）</summary>
    private readonly Dictionary<MLogger, ListViewItem> lviSets = new Dictionary<MLogger, ListViewItem>();

    /// <summary>MLoggerに関連付けられたリストビューのリスト（計測値）</summary>
    private readonly Dictionary<MLogger, ListViewItem> lviVals = new Dictionary<MLogger, ListViewItem>();

    /// <summary>並び替えが昇順か否か（計測設定）</summary>
    private bool[] isAscendingSets = new bool[20];

    /// <summary>並び替えが昇順か否か（計測値）</summary>
    private bool[] isAscendingVals = new bool[11];

    /// <summary>通信用XBee端末リスト</summary>
    private readonly Dictionary<ZigBeeDevice, xbeeInfo> coordinators = new Dictionary<ZigBeeDevice, xbeeInfo>();

    /// <summary>ログの一時保存</summary>
    private StringBuilder logString = new StringBuilder();

    /// <summary>補正係数設定用フォーム</summary>
    private CFForm cfForm;

    /// <summary>MLoggerのアドレス-名称対応リスト</summary>
    private static readonly Dictionary<string, string> mlNames = new Dictionary<string, string>();

    #endregion

    #region コンストラクタ

    /// <summary>インスタンスを初期化する</summary>
    public MainForm()
    {
      InitializeComponent();

      //初期設定ファイル読み込み
      string sFile = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "setting.ini";
      if (File.Exists(sFile))
      {
        using (StreamReader sReader = new StreamReader
        (sFile))
        {
          string line;
          while ((line = sReader.ReadLine()) != null)
          {
            line = line.Remove(line.IndexOf(';'));
            string[] st = line.Split('=');
            switch (st[0])
            {
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

      //End Device名称リスト読み込み
      string nFile = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "mlnames.txt";
      if (File.Exists(nFile))
      {
        using (StreamReader sReader = new StreamReader(nFile))
        {
          string line;
          while ((line = sReader.ReadLine()) != null)
          {
            string[] bf = line.Split(':');
            mlNames.Add(HIGH_ADD + bf[0], bf[1]);
          }
        }
      }

      //コントローラの国際化対応
      initControlLang();

      //コントローラ表示の初期化
      btn_outputSD.Enabled = enableSDOutput;
      btn_setCFactor.Visible = showCFButton;

      //データ格納用のディレクトリを作成
      dataDirectory = AppDomain.CurrentDomain.BaseDirectory + "data";
      string s_cs = dataDirectory + Path.DirectorySeparatorChar + "style.css";
      string l_js = dataDirectory + Path.DirectorySeparatorChar + "list.js";
      if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);
      if (!File.Exists(s_cs)) File.WriteAllText(s_cs, Properties.Resources.style_css);
      if (!File.Exists(l_js)) File.WriteAllText(l_js, Properties.Resources.list_js);

      //XBee端末切断状態から開始
      disconnectXBee();

      //定期的にHTMLファイルを更新する
      htmlRefreshTask();

      //定期的にログ表示を更新する
      loopLogRefresh();

      //定期的にコーディネータを探索する
      loopCoordinatorScan();

      //定期的にエンドデバイスを探索・初期化する
      loopEndDeviceInitialize();

      //ListViewの列幅調整
      resizeLVColumns(lv_setting);
      resizeLVColumns(lv_measure);
    }

    /// <summary>コントロールの国際化対応処理</summary>
    private void initControlLang()
    {
      lbl_th.Text = i18n.Resources.DBTemp + "/" + i18n.Resources.RHumid;
      lbl_glb.Text = i18n.Resources.GlbTemp;
      lbl_vel.Text = i18n.Resources.Velocity;
      rbtn_ill.Text = i18n.Resources.Illuminance;
      rbtn_prox.Text = i18n.Resources.Proximity;
      lbl_gpv1.Text = i18n.Resources.GeneralPurposeVoltage;
      //lbl_gpv1.Text = i18n.Resources.GeneralPurposeVoltage + " 1";
      lbl_gpv2.Text = i18n.Resources.GeneralPurposeVoltage + " 2";
      lbl_gpv3.Text = i18n.Resources.GeneralPurposeVoltage + " 3";
      lbl_SDTime.Text = i18n.Resources.MF_MStartDTime;

      cbx_thMeasure.Text = cbx_glbMeasure.Text = cbx_velMeasure.Text = cbx_illMeasure.Text
        = cbx_gpv1Measure.Text = cbx_gpv2Measure.Text = cbx_gpv3Measure.Text = i18n.Resources.MF_MeasureCBX;

      btn_applySetting.Text = i18n.Resources.MF_ApplySetting;
      btn_startCollecting.Text = i18n.Resources.MF_StartDataCollecting;
      btn_outputSD.Text = i18n.Resources.MF_StartLoggingToSDCard;
      btn_setCFactor.Text = i18n.Resources.MF_SetCorrectionFactors;

      //計測設定ListViewの項目
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
      //lvhd_gv1Measure.Text = i18n.Resources.GeneralPurposeVoltage + "1";
      lvhd_gv1Measure.Text = i18n.Resources.GeneralPurposeVoltage;
      lvhd_gv1Interval.Text = i18n.Resources.Interval;
      //lvhd_gv2Measure.Text = i18n.Resources.GeneralPurposeVoltage + "2";
      //lvhd_gv2Interval.Text = i18n.Resources.Interval;
      //lvhd_gv3Measure.Text = i18n.Resources.GeneralPurposeVoltage + "3";
      //lvhd_gv3Interval.Text = i18n.Resources.Interval;
      lvhd_prxMeasure.Text = i18n.Resources.Proximity;

      //計測値ListViewの項目
      lvhd2_name.Text = "ID";
      lvhd2_name.Text = i18n.Resources.Name;
      lvhd2_dbt.Text = i18n.Resources.DBTemp;
      lvhd2_hmd.Text = i18n.Resources.RHumid;
      lvhd2_glb.Text = i18n.Resources.GlbTemp;
      lvhd2_vel.Text = i18n.Resources.Velocity;
      lvhd2_ill.Text = i18n.Resources.Illuminance;
      lvhd2_pmv.Text = "PMV";
      lvhd2_ppd.Text = "PPD";
      lvhd2_set.Text = "SET*";
      lvhd2_dtime.Text = i18n.Resources.DateTime;
    }

    /// <summary>定期的にHTMLを更新する</summary>
    private void htmlRefreshTask()
    {
      Task.Run(() =>
      {
        while (true)
        {
          if (hasNewData)
          {
            hasNewData = false;
            makeWebData();
          }
          Thread.Sleep(REFRESH_HTML_TSPAN);
        }
      });
    }

    /// <summary>定期的にログを更新する</summary>
    private void loopLogRefresh()
    {
      Task.Run(() =>
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
          Thread.Sleep(REFRESH_LOG_TSPAN);
        }
      });
    }

    /// <summary>定期的にCoordinatorを探索する</summary>
    private void loopCoordinatorScan()
    {
      Task.Run(() =>
      {
        while (true)
        {
          //各ポートへの接続を試行
          string[] portList = System.IO.Ports.SerialPort.GetPortNames();
          foreach (string pn in excludedPorts)
            if (Array.IndexOf(portList, pn) == -1)
              excludedPorts.Remove(pn);

          for (int i = 0; i < portList.Length; i++)
          {
            if (!connectedPorts.Contains(portList[i]) && !excludedPorts.Contains(portList[i]))
              scanCoordinator(portList[i], BAUD_RATE);
          }
          Thread.Sleep(SCAN_COORDINATOR_TSPAN);
        }
      });
    }

    /// <summary>定期的にEndDeviceを探索・初期化する</summary>
    private void loopEndDeviceInitialize()
    {
      Task.Run(() =>
      {
        while (true)
        {
          scanEndDevice();

          //初期化未了のEndDeviceを初期化
          foreach (string key in mLoggers.Keys)
          {
            if (!mLoggers[key].MeasuringSettingLoaded && mLoggers[key].CurrentStatus == MLogger.Status.Initializing)
              sndMsg(key, MLogger.MakeLoadMeasuringSettingCommand());
            if (!mLoggers[key].VersionLoaded && mLoggers[key].CurrentStatus == MLogger.Status.Initializing)
              sndMsg(key, MLogger.MakeGetVersionCommand());
          }

          Thread.Sleep(SCAN_ENDDEVICE_TSPAN);
        }
      });
    }

    #endregion

    #region コントロールの描画更新処理（設定用リストビュー）

    private delegate void updateLVSettingItemDelegate(MLogger ml);

    private void updateLVSettingItem(MLogger ml)
    {
      if (InvokeRequired)
      {
        Invoke(new updateLVSettingItemDelegate(updateLVSettingItem), ml);
        return;
      }

      //MLoggerを取得
      if (mLoggers.ContainsValue(ml))
      {
        //リストビューに無ければ追加
        if (!lviSets.ContainsKey(ml))
        {
          ListViewItem lvm = new ListViewItem(new string[]
          { 
            ml.LowAddress, ml.Name, i18n.Resources.MF_Initializing,
            ml.DrybulbTemperature.Measure ? "true" : "false", ml.DrybulbTemperature.Interval.ToString(),
            ml.GlobeTemperature.Measure ? "true" : "false", ml.GlobeTemperature.Interval.ToString(),
            ml.Velocity.Measure ? "true" : "false", ml.Velocity.Interval.ToString(),
            ml.Illuminance.Measure ? "true" : "false", ml.Illuminance.Interval.ToString(),
            ml.GeneralVoltage1.Measure ? "true" : "false", ml.GeneralVoltage1.Interval.ToString(),
            //ml.GeneralVoltage2.Measure ? "true" : "false", ml.GeneralVoltage2.Interval.ToString(),
            //ml.GeneralVoltage3.Measure ? "true" : "false", ml.GeneralVoltage3.Interval.ToString(),
            ml.MeasureProximity ? "true" : "false",
            ml.StartMeasuringDateTime.ToString(DT_FORMAT) });
          lviSets.Add(ml, lvm);
          lv_setting.Items.Add(lvm);
        }

        //設定を更新
        string status;
        switch (ml.CurrentStatus)
        {
          case MLogger.Status.Initializing:
            status = i18n.Resources.MF_Initializing;
            break;
          case MLogger.Status.WaitingForCommand:
            status = i18n.Resources.MF_Editable;
            break;
          case MLogger.Status.StartMeasuring:
            status = i18n.Resources.MF_Measuring;
            break;
          case MLogger.Status.Measuring:
            status = i18n.Resources.MF_Measuring;
            break;
          default:
            status = "Unknown status";
            break;
        }

        ListViewItem item = lviSets[ml];
        item.SubItems[1].Text = ml.Name;
        item.SubItems[2].Text = status;
        item.SubItems[3].Text = ml.DrybulbTemperature.Measure ? "true" : "false";
        item.SubItems[4].Text = ml.DrybulbTemperature.Interval.ToString();
        item.SubItems[5].Text = ml.GlobeTemperature.Measure ? "true" : "false";
        item.SubItems[6].Text = ml.GlobeTemperature.Interval.ToString();
        item.SubItems[7].Text = ml.Velocity.Measure ? "true" : "false";
        item.SubItems[8].Text = ml.Velocity.Interval.ToString();
        item.SubItems[9].Text = ml.Illuminance.Measure ? "true" : "false";
        item.SubItems[10].Text = ml.Illuminance.Interval.ToString();
        item.SubItems[11].Text = ml.StartMeasuringDateTime.ToString(DT_FORMAT);
        item.SubItems[12].Text = ml.GeneralVoltage1.Measure ? "true" : "false";
        item.SubItems[13].Text = ml.GeneralVoltage1.Interval.ToString();
        /*item.SubItems[14].Text = ml.GeneralVoltage2.Measure ? "true" : "false";
        item.SubItems[15].Text = ml.GeneralVoltage2.Interval.ToString();
        item.SubItems[16].Text = ml.GeneralVoltage3.Measure ? "true" : "false";
        item.SubItems[17].Text = ml.GeneralVoltage3.Interval.ToString();
        item.SubItems[18].Text = ml.MeasureProximity ? "true" : "false";*/
        item.SubItems[14].Text = ml.MeasureProximity ? "true" : "false";
      }
    }

    #endregion

    #region コントロールの描画更新処理（測定値リストビュー）

    private delegate void updateLVValueItemDelegate(MLogger ml);

    /// <summary>XBee端末をリストに追加する</summary>
    /// <param name="name"></param>
    private void updateLVValueItem(MLogger ml)
    {
      if (InvokeRequired)
      {
        Invoke(new updateLVValueItemDelegate(updateLVValueItem), ml);
        return;
      }

      //リストビューに無ければ追加
      if (mLoggers.ContainsValue(ml))
      {
        if (!lviVals.ContainsKey(ml))
        {
          ListViewItem lvm = new ListViewItem(new string[]
          { 
            ml.LowAddress, ml.Name,
            ml.DrybulbTemperature.LastValue.ToString("F1"),
            ml.RelativeHumdity.LastValue.ToString("F1"),
            ml.GlobeTemperature.LastValue.ToString("F1"),
            ml.Velocity.LastValue.ToString("F2"),
            ml.Illuminance.LastValue.ToString("F1"),
            ml.PMV.ToString("F2"),
            ml.PPD.ToString("F1"),
            ml.SETStar.ToString("F1"),
            ml.LastCommunicated.ToString(DT_FORMAT)
          }); 
          lviVals.Add(ml, lvm);
          lv_measure.Items.Add(lvm);
        }
      }

      //測定値を更新
      ListViewItem item = lviVals[ml];
      item.SubItems[1].Text = ml.Name;
      item.SubItems[2].Text = ml.DrybulbTemperature.LastValue.ToString("F1");
      item.SubItems[3].Text = ml.RelativeHumdity.LastValue.ToString("F1");
      item.SubItems[4].Text = ml.GlobeTemperature.LastValue.ToString("F1");
      item.SubItems[5].Text = ml.Velocity.LastValue.ToString("F2");
      item.SubItems[6].Text = ml.Illuminance.LastValue.ToString("F1");
      item.SubItems[7].Text = ml.PMV.ToString("F2");
      item.SubItems[8].Text = ml.PPD.ToString("F1");
      item.SubItems[9].Text = ml.SETStar.ToString("F1");
      item.SubItems[10].Text = ml.LastCommunicated.ToString(DT_FORMAT);
    }

    #endregion

    #region コントロールの描画更新処理（ログ関連）

    private delegate void refreshLogDelegate();

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

    #endregion

    #region コントロールの描画更新処理（Tool strip関連）

    private delegate void setTSBtnStateDelegate
      (ToolStripButton tsBtn, bool enabled, Image img, string text, string toolTipText);

    private delegate void setTSSBtnStateDelegate
      (ToolStripSplitButton tssBtn, bool enabled, Image img, string text, string toolTipText);

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

    #endregion

    #region 通信イベント処理

    public delegate void UpdateCFactorsDelegate();

    private void Net_DeviceDiscovered(object sender, XBeeLibrary.Core.Events.DeviceDiscoveredEventArgs e)
    {
      //LongAddressを取得
      RemoteXBeeDevice rdv = e.DiscoveredDevice;
      ZigBeeDevice dv = rdv.GetLocalXBeeDevice() as ZigBeeDevice;
      string add = rdv.GetAddressString();

      //新規デバイスでなければ終了
      if (mLoggers.ContainsKey(add)) return;

      //HTML更新フラグを立てる
      hasNewData = true;

      MLogger ml = new MLogger(add);

      //名前を設定
      if (mlNames.ContainsKey(add)) ml.Name = mlNames[add];

      //熱的快適性計算のための情報を設定
      ml.CloValue = cloValue;
      ml.MetValue = metValue;
      ml.DefaultTemperature = dbtValue;
      ml.DefaultRelativeHumidity = rhdValue;
      ml.DefaultGlobeTemperature = mrtValue;
      ml.DefaultVelocity = velValue;

      //イベント登録
      ml.MeasuredValueReceivedEvent += Ml_MeasuredValueReceivedEvent;
      ml.MeasurementSettingReceivedEvent += Ml_MeasurementSettingReceivedEvent;
      ml.VersionReceivedEvent += Ml_VersionReceivedEvent;
      ml.CorrectionFactorsReceivedEvent += Ml_CorrectionFactorsReceivedEvent;
      ml.WaitingForCommandMessageReceivedEvent += Ml_WaitingForCommandMessageReceivedEvent;
      ml.StartMeasuringMessageReceivedEvent += Ml_StartMeasuringMessageReceivedEvent;

      mLoggers.Add(add, ml);

      //子機のアドレスと通信用XBeeを対応付ける
      if (!coordinators[dv].longAddress.Contains(add))
        coordinators[dv].longAddress.Add(add);

      //リストビューに追加
      updateLVSettingItem(mLoggers[add]);

      //測定設定情報を得る
      Task.Run(() =>
      {
        try
        {
          sndMsg(add, MLogger.MakeLoadMeasuringSettingCommand());
          sndMsg(add, MLogger.MakeGetVersionCommand());
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

      MLogger mlg = mLoggers[add];

      //受信データを追加
      mlg.AddReceivedData(rcvStr);

      //コマンド処理
      while (mlg.HasCommand)
      {
        try
        {
          appendLog(mlg.Name + ": " + mlg.NextCommand);
          mlg.SolveCommand();
        }
        catch (Exception exc)
        {
          appendErrorLog(mlg.LowAddress + " : " + exc.Message);
          mlg.ClearReceivedData(); //異常終了時はコマンドを全消去する
        }
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

    #region コマンド受信イベント発生時の処理

    private void Ml_StartMeasuringMessageReceivedEvent(object sender, EventArgs e)
    {
      updateLVSettingItem((MLogger)sender);
    }

    private void Ml_WaitingForCommandMessageReceivedEvent(object sender, EventArgs e)
    {
      updateLVSettingItem((MLogger)sender);
    }

    private void Ml_VersionReceivedEvent(object sender, EventArgs e)
    { }

    private void Ml_MeasurementSettingReceivedEvent(object sender, EventArgs e)
    {
      updateLVSettingItem((MLogger)sender);
    }

    private void Ml_MeasuredValueReceivedEvent(object sender, EventArgs e)
    {
      //HTML更新フラグを立てる
      hasNewData = true;

      MLogger ml = (MLogger)sender;
      updateLVSettingItem(ml);
      updateLVValueItem(ml);

      //データ書き出し
      string fName = dataDirectory + Path.DirectorySeparatorChar + ml.LowAddress + ".csv";

      try
      {
        using (StreamWriter sWriter = new StreamWriter(fName, true, Encoding.UTF8))
        {
          sWriter.WriteLine(
            DateTime.Now.ToString(DT_FORMAT) + "," + //親機の現在日時
            ml.LastMeasured.ToString(DT_FORMAT) + "," + //子機の計測日時
            ml.DrybulbTemperature.LastValue.ToString("F2") + "," +
            ml.RelativeHumdity.LastValue.ToString("F2") + "," +
            ml.GlobeTemperatureVoltage.ToString("F3") + "," +
            ml.GlobeTemperature.LastValue.ToString("F2") + "," +
            ml.VelocityVoltage.ToString("F3") + "," + 
            ml.Velocity.LastValue.ToString("F4") + "," +
            ml.Illuminance.LastValue.ToString("F2") + "," +
            ml.GeneralVoltage1.LastValue.ToString("F3"));
            //ml.GeneralVoltage1.LastValue.ToString("F3") + "," +
            //ml.GeneralVoltage2.LastValue.ToString("F3") + "," +
            //ml.GeneralVoltage3.LastValue.ToString("F3"));
        }
      }
      catch
      {
        appendLog(String.Format(i18n.Resources.MF_FileIsUsing, fName));
        return;
      }

      //通信中のアイコン明滅
      Task.Run(async () =>
      {
        setTSBtnState(tsb_downloading, true, Properties.Resources.downloading, "", "");
        await Task.Delay(300);
        setTSBtnState(tsb_downloading, true, Properties.Resources.waiting, "", "");
      });
    }

    private void Ml_CorrectionFactorsReceivedEvent(object sender, EventArgs e)
    {
      //表示中の補正係数設定フォームがあれば反映
      if (cfForm != null)
        Invoke(new UpdateCFactorsDelegate(cfForm.UpdateCFactors));
    }

    #endregion

    #region Coordinator端末の接続切断処理

    /// <summary>すべてのXBee端末を切り離す</summary>
    private void disconnectXBee()
    {
      foreach (ZigBeeDevice key in coordinators.Keys)
        disconnectXBee(coordinators[key].portName);
      connectedPorts.Clear(); //ここから3行は最後のXBee切断処理時に呼び出されるはずだが、必要？
      lv_setting.Items.Clear();
      lv_measure.Items.Clear();

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
      //再接続しないポートに登録
      excludedPorts.Add(portName);

      foreach (ZigBeeDevice key in coordinators.Keys)
      {
        xbeeInfo xInfo = coordinators[key];
        if (xInfo.portName == portName)
        {
          //ListViewの更新処理
          for (int i = 0; i < lv_setting.Items.Count; i++)
            if (key == getXBee(HIGH_ADD + lv_setting.Items[i].SubItems[0].Text))
              lv_setting.Items.RemoveAt(i);

          for (int i = 0; i < lv_measure.Items.Count; i++)
            if (key == getXBee(HIGH_ADD + lv_measure.Items[i].SubItems[0].Text))
              lv_measure.Items.RemoveAt(i);

          //イベント解除
          key.GetNetwork().DeviceDiscovered -= Net_DeviceDiscovered;
          key.DataReceived -= Device_DataReceived;

          key.Close();
          coordinators.Remove(key);
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
          scanCoordinator(portList[i], BAUD_RATE);
      }
    }

    /// <summary>Portへの接続Taskを生成</summary>
    /// <param name="pName">Port名称</param>
    /// <returns>Portへの接続Task</returns>
    private void scanCoordinator(string pName, int bRate)
    {
      Task.Run(() =>
      {
        //通信用XBee端末をOpen
        ZigBeeDevice device = new ZigBeeDevice(new XBeeLibrary.Windows.Connection.Serial.WinSerialPort(pName, bRate));
        try
        {
          device.Open();
        }
        catch (Exception ex)
        {
          excludedPorts.Add(pName);
          appendLog(pName + ": " + ex.Message);
          return;
        }
        coordinators.Add(device, new xbeeInfo(pName));
        appendLog(pName + ": " + i18n.Resources.MF_ConnectionSucceeded + " S/N = " + device.XBee64BitAddr.ToString());

        //Coordinatorが見つかった場合には直ちに初回のEndDevice探索
        scanEndDevice(device);

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
      appendLog(i18n.Resources.MF_StartSearch);
      scanEndDevice();
    }

    /// <summary>EndDeviceを探索する</summary>
    /// <param name="coordinator">コーディネータ</param>
    private void scanEndDevice(ZigBeeDevice coordinator)
    {
      Task.Run(() =>
      {
        XBeeNetwork net = coordinator.GetNetwork();

        //既に探索中の場合は一旦停止
        if (net.IsDiscoveryRunning) net.StopNodeDiscoveryProcess();

        //探索開始
        net.SetDiscoveryTimeout((long)(SCAN_ENDDEVICE_TSPAN * 0.9));
        try
        {
          net.StartNodeDiscoveryProcess(); //DiscoveryProcessの二重起動で例外が発生する
        }
        catch (Exception e)
        {
          appendLog(e.Message);
        }
      });
    }

    /// <summary>EndDeviceを探索する</summary>
    private void scanEndDevice()
    {
      foreach (ZigBeeDevice key in coordinators.Keys)
        scanEndDevice(key);
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
          sndMsg(HIGH_ADD + adds[i], MLogger.MakeStartMeasuringCommand(false));
          Thread.Sleep(CMD_TSPAN);
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
          sndMsg(HIGH_ADD + adds[i], MLogger.MakeStartMeasuringCommand(true));
          Thread.Sleep(CMD_TSPAN);
        }
      });
    }

    /// <summary>並び替える</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void listView_ColumnClick(object sender, ColumnClickEventArgs e)
    {
      int colNum = e.Column;

      ListView lv = (ListView)sender;
      bool[] isAc = (lv == lv_setting) ? isAscendingSets : isAscendingVals;
      isAc[colNum] = !isAc[colNum];
      lv_setting.ListViewItemSorter = new MLoggerComparer(colNum, isAc[colNum]);
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
            scanCoordinator(portName, BAUD_RATE);
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
      //cbx_gpv2Measure.Checked = (item.SubItems[14].Text == "true");
      //tbx_gpv2Interval.Text = item.SubItems[15].Text;
      //cbx_gpv3Measure.Checked = (item.SubItems[16].Text == "true");
      //tbx_gpv3Interval.Text = item.SubItems[17].Text;
      //rbtn_ill.Checked = (item.SubItems[18].Text != "true");
      //rbtn_prox.Checked = (item.SubItems[18].Text == "true");
      rbtn_ill.Checked = (item.SubItems[14].Text != "true");
      rbtn_prox.Checked = (item.SubItems[14].Text == "true");
      dtp_timer.Value = DateTime.ParseExact(item.SubItems[11].Text, DT_FORMAT, null);

      reflectCheckBoxState();
    }

    /// <summary>補正係数設定ボタンクリック時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void btn_setCFactor_Click(object sender, EventArgs e)
    {
      //選択されていなければ無視
      if (lv_setting.SelectedIndices.Count == 0) return;
      string add = HIGH_ADD + lv_setting.SelectedItems[0].SubItems[0].Text;

      //受信用ウィンドウを用意
      if (cfForm != null) cfForm.Close();
      cfForm = new CFForm();
      cfForm.Logger = mLoggers[add];
      cfForm.SendMessageFnc = sndMsg;
      cfForm.Show();

      //補正係数読み込みコマンドを送信
      sndMsg(add, MLogger.MakeLoadCorrectionFactorsCommand());
    }

    /// <summary>計測設定更新ボタンクリック時の処理</summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void btn_updateMSetting_Click(object sender, EventArgs e)
    {
      if (lv_setting.SelectedItems.Count == 0) return;

      //設定内容をチェック1
      int itTH, itRD, itVL, itIL, itGV1;
      //int itGV2, itGV3;
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
        //MessageBox.Show(String.Format(i18n.Resources.MF_Alrt_InvalidInput, i18n.Resources.GeneralPurposeVoltage + " 1"));
        MessageBox.Show(String.Format(i18n.Resources.MF_Alrt_InvalidInput, i18n.Resources.GeneralPurposeVoltage));
        return;
      }
      /*if (!int.TryParse(tbx_gpv2Interval.Text, out itGV2))
      {
        MessageBox.Show(String.Format(i18n.Resources.MF_Alrt_InvalidInput, i18n.Resources.GeneralPurposeVoltage + " 2"));
        return;
      }
      if (!int.TryParse(tbx_gpv3Interval.Text, out itGV3))
      {
        MessageBox.Show(String.Format(i18n.Resources.MF_Alrt_InvalidInput, i18n.Resources.GeneralPurposeVoltage + " 3"));
        return;
      }*/

      //設定内容をチェック2
      if (itTH < 1 || itRD < 1 || itVL < 1 || itIL < 1 || itGV1 < 1 
        || 86400 < itTH || 86400 < itRD || 86400 < itVL || 86400 < itIL || 86400 < itGV1)
      //if (itTH < 1 || itRD < 1 || itVL < 1 || itIL < 1 || itGV1 < 1 || itGV2 < 1 || itGV3 < 1
      //  || 86400 < itTH || 86400 < itRD || 86400 < itVL || 86400 < itIL || 86400 < itGV1 || 86400 < itGV2 || 86400 < itGV3)
      {
        MessageBox.Show(i18n.Resources.MF_Alrt_Interval);
        return;
      }

      //設定コマンドを作成
      string sData = MLogger.MakeChangeMeasuringSettingCommand(
        dtp_timer.Value,
        cbx_thMeasure.Checked, itTH,
        cbx_glbMeasure.Checked, itRD,
        cbx_velMeasure.Checked, itVL,
        cbx_illMeasure.Checked, itIL,
        cbx_gpv1Measure.Checked, itGV1,
        false, 87600,
        false, 87600,
        //cbx_gpv2Measure.Checked, itGV2,
        //cbx_gpv3Measure.Checked, itGV3,
        rbtn_prox.Checked);

      //1件ずつコマンドを送信
      for (int i = 0; i < lv_setting.SelectedItems.Count; i++)
      {
        sndMsg(HIGH_ADD + lv_setting.SelectedItems[i].SubItems[0].Text,
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
      //else if (cbx.Equals(cbx_gpv2Measure))
      //  lbl_gpv2.Enabled = tbx_gpv2Interval.Enabled = cbx.Checked;
      //else if (cbx.Equals(cbx_gpv3Measure))
      //  lbl_gpv3.Enabled = tbx_gpv3Interval.Enabled = cbx.Checked;
    }

    private void reflectCheckBoxState()
    {
      lbl_th.Enabled = tbx_thInterval.Enabled = cbx_thMeasure.Checked;
      lbl_glb.Enabled = tbx_glbInterval.Enabled = cbx_glbMeasure.Checked;
      lbl_vel.Enabled = tbx_velInterval.Enabled = cbx_velMeasure.Checked;
      rbtn_ill.Enabled = rbtn_prox.Enabled = tbx_illInterval.Enabled = cbx_illMeasure.Checked;
      lbl_gpv1.Enabled = tbx_gpv1Interval.Enabled = cbx_gpv1Measure.Checked;
      //lbl_gpv2.Enabled = tbx_gpv2Interval.Enabled = cbx_gpv2Measure.Checked;
      //lbl_gpv3.Enabled = tbx_gpv3Interval.Enabled = cbx_gpv3Measure.Checked;
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

      string html = MLogger.MakeHTMLTable(i18n.Resources.topPage_html, loggers);
      using (StreamWriter sWriter = new StreamWriter
        (dataDirectory + Path.DirectorySeparatorChar + "index.htm", false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
      { sWriter.Write(html); }

      string latestData = MLogger.MakeLatestData(loggers);
      using (StreamWriter sWriter = new StreamWriter
        (dataDirectory + Path.DirectorySeparatorChar + "latest.txt", false, Encoding.UTF8))
      { sWriter.Write(latestData); }
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
        logString.AppendLine(DateTime.Now.ToString(DT_FORMAT + " : ") + log);

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
      foreach (ZigBeeDevice key in coordinators.Keys)
        if (coordinators[key].longAddress.Contains(address)) return key;
      return null;
    }

    private static void resizeLVColumns(ListView lv)
    {
      lv.Columns[0].TextAlign = HorizontalAlignment.Center;
      int ave = Math.Max(1, lv.Width / lv.Columns.Count);
      foreach (ColumnHeader ch in lv.Columns) ch.Width = ave;
    }

    private void listView_SizeChanged(object sender, EventArgs e)
    {
      resizeLVColumns((ListView)sender);
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
