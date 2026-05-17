namespace MLController
{
  partial class MainForm
  {
    /// <summary>
    /// 必要なデザイナー変数です。
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// 使用中のリソースをすべてクリーンアップします。
    /// </summary>
    /// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows フォーム デザイナーで生成されたコード

    /// <summary>
    /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
    /// コード エディターで変更しないでください。
    /// </summary>
    private void InitializeComponent()
    {
      System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
      toolStrip = new System.Windows.Forms.ToolStrip();
      tssb_connection = new System.Windows.Forms.ToolStripSplitButton();
      tsb_reload = new System.Windows.Forms.ToolStripButton();
      tsb_downloading = new System.Windows.Forms.ToolStripButton();
      splitContainer1 = new System.Windows.Forms.SplitContainer();
      splitContainer2 = new System.Windows.Forms.SplitContainer();
      lv_setting = new System.Windows.Forms.ListView();
      lvhd_xbeeID = new System.Windows.Forms.ColumnHeader();
      lvhd_xbeeName = new System.Windows.Forms.ColumnHeader();
      lvhd_step = new System.Windows.Forms.ColumnHeader();
      lvhd_thMeasure = new System.Windows.Forms.ColumnHeader();
      lvhd_thInterval = new System.Windows.Forms.ColumnHeader();
      lvhd_glvMeasure = new System.Windows.Forms.ColumnHeader();
      lvhd_glvInterval = new System.Windows.Forms.ColumnHeader();
      lvhd_velMeasure = new System.Windows.Forms.ColumnHeader();
      lvhd_velInterval = new System.Windows.Forms.ColumnHeader();
      lvhd_illMeasure = new System.Windows.Forms.ColumnHeader();
      lvhd_illInterval = new System.Windows.Forms.ColumnHeader();
      lvhd_startTime = new System.Windows.Forms.ColumnHeader();
      lvhd_gv1Measure = new System.Windows.Forms.ColumnHeader();
      lvhd_gv1Interval = new System.Windows.Forms.ColumnHeader();
      lvhd_prxMeasure = new System.Windows.Forms.ColumnHeader();
      lv_measure = new System.Windows.Forms.ListView();
      lvhd2_xbeeID = new System.Windows.Forms.ColumnHeader();
      lvhd2_name = new System.Windows.Forms.ColumnHeader();
      lvhd2_dbt = new System.Windows.Forms.ColumnHeader();
      lvhd2_hmd = new System.Windows.Forms.ColumnHeader();
      lvhd2_glb = new System.Windows.Forms.ColumnHeader();
      lvhd2_vel = new System.Windows.Forms.ColumnHeader();
      lvhd2_ill = new System.Windows.Forms.ColumnHeader();
      lvhd2_pmv = new System.Windows.Forms.ColumnHeader();
      lvhd2_ppd = new System.Windows.Forms.ColumnHeader();
      lvhd2_set = new System.Windows.Forms.ColumnHeader();
      lvhd2_dtime = new System.Windows.Forms.ColumnHeader();
      tbx_log = new System.Windows.Forms.TextBox();
      pnl_settingEdit = new System.Windows.Forms.Panel();
      cbx_saveToSDCard = new System.Windows.Forms.CheckBox();
      rbtn_ill = new System.Windows.Forms.RadioButton();
      rbtn_prox = new System.Windows.Forms.RadioButton();
      lbl_gpv3 = new System.Windows.Forms.Label();
      lbl_gpv2 = new System.Windows.Forms.Label();
      lbl_gpv1 = new System.Windows.Forms.Label();
      lbl_vel = new System.Windows.Forms.Label();
      label9 = new System.Windows.Forms.Label();
      lbl_glb = new System.Windows.Forms.Label();
      label7 = new System.Windows.Forms.Label();
      lbl_th = new System.Windows.Forms.Label();
      cbx_gpv3Measure = new System.Windows.Forms.CheckBox();
      label4 = new System.Windows.Forms.Label();
      cbx_gpv2Measure = new System.Windows.Forms.CheckBox();
      btn_startCollecting = new System.Windows.Forms.Button();
      tbx_gpv3Interval = new System.Windows.Forms.TextBox();
      cbx_gpv1Measure = new System.Windows.Forms.CheckBox();
      tbx_gpv2Interval = new System.Windows.Forms.TextBox();
      label5 = new System.Windows.Forms.Label();
      tbx_gpv1Interval = new System.Windows.Forms.TextBox();
      cbx_illMeasure = new System.Windows.Forms.CheckBox();
      tbx_illInterval = new System.Windows.Forms.TextBox();
      lbl_SDTime = new System.Windows.Forms.Label();
      dtp_timer = new System.Windows.Forms.DateTimePicker();
      btn_outputSD = new System.Windows.Forms.Button();
      btn_setCFactor = new System.Windows.Forms.Button();
      btn_applySetting = new System.Windows.Forms.Button();
      label3 = new System.Windows.Forms.Label();
      cbx_thMeasure = new System.Windows.Forms.CheckBox();
      label2 = new System.Windows.Forms.Label();
      cbx_glbMeasure = new System.Windows.Forms.CheckBox();
      tbx_glbInterval = new System.Windows.Forms.TextBox();
      label1 = new System.Windows.Forms.Label();
      tbx_thInterval = new System.Windows.Forms.TextBox();
      cbx_velMeasure = new System.Windows.Forms.CheckBox();
      tbx_velInterval = new System.Windows.Forms.TextBox();
      toolStrip.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
      splitContainer1.Panel1.SuspendLayout();
      splitContainer1.Panel2.SuspendLayout();
      splitContainer1.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)splitContainer2).BeginInit();
      splitContainer2.Panel1.SuspendLayout();
      splitContainer2.Panel2.SuspendLayout();
      splitContainer2.SuspendLayout();
      pnl_settingEdit.SuspendLayout();
      SuspendLayout();
      // 
      // toolStrip
      // 
      toolStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
      toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { tssb_connection, tsb_reload, tsb_downloading });
      toolStrip.Location = new System.Drawing.Point(0, 0);
      toolStrip.Name = "toolStrip";
      toolStrip.Padding = new System.Windows.Forms.Padding(0, 0, 4, 0);
      toolStrip.Size = new System.Drawing.Size(2374, 58);
      toolStrip.TabIndex = 9;
      toolStrip.Text = "toolStrip1";
      // 
      // tssb_connection
      // 
      tssb_connection.Image = (System.Drawing.Image)resources.GetObject("tssb_connection.Image");
      tssb_connection.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
      tssb_connection.ImageTransparentColor = System.Drawing.Color.Magenta;
      tssb_connection.Name = "tssb_connection";
      tssb_connection.Size = new System.Drawing.Size(178, 52);
      tssb_connection.Text = i18n.Resources.MF_Connect;
      tssb_connection.ButtonClick += tsb_connection_Click;
      tssb_connection.DropDownOpening += tssb_connection_DropDownOpening;
      // 
      // tsb_reload
      // 
      tsb_reload.Enabled = false;
      tsb_reload.Image = (System.Drawing.Image)resources.GetObject("tsb_reload.Image");
      tsb_reload.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
      tsb_reload.ImageTransparentColor = System.Drawing.Color.Magenta;
      tsb_reload.Name = "tsb_reload";
      tsb_reload.Size = new System.Drawing.Size(137, 52);
      tsb_reload.Text = "Search";
      tsb_reload.ToolTipText = "周囲のXBee端末を探索します";
      tsb_reload.Click += tsb_reload_Click;
      // 
      // tsb_downloading
      // 
      tsb_downloading.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
      tsb_downloading.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
      tsb_downloading.Image = (System.Drawing.Image)resources.GetObject("tsb_downloading.Image");
      tsb_downloading.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
      tsb_downloading.ImageTransparentColor = System.Drawing.Color.Magenta;
      tsb_downloading.Name = "tsb_downloading";
      tsb_downloading.Size = new System.Drawing.Size(52, 52);
      tsb_downloading.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
      // 
      // splitContainer1
      // 
      splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
      splitContainer1.Location = new System.Drawing.Point(508, 58);
      splitContainer1.Margin = new System.Windows.Forms.Padding(6);
      splitContainer1.Name = "splitContainer1";
      splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
      // 
      // splitContainer1.Panel1
      // 
      splitContainer1.Panel1.Controls.Add(splitContainer2);
      splitContainer1.Panel1MinSize = 300;
      // 
      // splitContainer1.Panel2
      // 
      splitContainer1.Panel2.Controls.Add(tbx_log);
      splitContainer1.Size = new System.Drawing.Size(1866, 1403);
      splitContainer1.SplitterDistance = 1018;
      splitContainer1.SplitterWidth = 9;
      splitContainer1.TabIndex = 17;
      // 
      // splitContainer2
      // 
      splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
      splitContainer2.Location = new System.Drawing.Point(0, 0);
      splitContainer2.Name = "splitContainer2";
      splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
      // 
      // splitContainer2.Panel1
      // 
      splitContainer2.Panel1.Controls.Add(lv_setting);
      // 
      // splitContainer2.Panel2
      // 
      splitContainer2.Panel2.Controls.Add(lv_measure);
      splitContainer2.Size = new System.Drawing.Size(1866, 1018);
      splitContainer2.SplitterDistance = 557;
      splitContainer2.TabIndex = 5;
      // 
      // lv_setting
      // 
      lv_setting.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { lvhd_xbeeID, lvhd_xbeeName, lvhd_step, lvhd_thMeasure, lvhd_thInterval, lvhd_glvMeasure, lvhd_glvInterval, lvhd_velMeasure, lvhd_velInterval, lvhd_illMeasure, lvhd_illInterval, lvhd_startTime, lvhd_gv1Measure, lvhd_gv1Interval, lvhd_prxMeasure });
      lv_setting.Dock = System.Windows.Forms.DockStyle.Fill;
      lv_setting.FullRowSelect = true;
      lv_setting.Location = new System.Drawing.Point(0, 0);
      lv_setting.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      lv_setting.Name = "lv_setting";
      lv_setting.Size = new System.Drawing.Size(1866, 557);
      lv_setting.TabIndex = 3;
      lv_setting.UseCompatibleStateImageBehavior = false;
      lv_setting.View = System.Windows.Forms.View.Details;
      lv_setting.ColumnClick += listView_ColumnClick;
      lv_setting.SelectedIndexChanged += lv_setting_SelectedIndexChanged;
      lv_setting.SizeChanged += listView_SizeChanged;
      // 
      // lvhd_xbeeID
      // 
      lvhd_xbeeID.Name = "lvhd_xbeeID";
      lvhd_xbeeID.Text = "ID";
      lvhd_xbeeID.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_xbeeID.Width = 100;
      // 
      // lvhd_xbeeName
      // 
      lvhd_xbeeName.Name = "lvhd_xbeeName";
      lvhd_xbeeName.Text = "名前";
      lvhd_xbeeName.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_xbeeName.Width = 100;
      // 
      // lvhd_step
      // 
      lvhd_step.Name = "lvhd_step";
      lvhd_step.Text = "状態";
      lvhd_step.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_step.Width = 100;
      // 
      // lvhd_thMeasure
      // 
      lvhd_thMeasure.Name = "lvhd_thMeasure";
      lvhd_thMeasure.Text = "温湿度";
      lvhd_thMeasure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_thMeasure.Width = 100;
      // 
      // lvhd_thInterval
      // 
      lvhd_thInterval.Name = "lvhd_thInterval";
      lvhd_thInterval.Text = "測定間隔";
      lvhd_thInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_thInterval.Width = 100;
      // 
      // lvhd_glvMeasure
      // 
      lvhd_glvMeasure.Name = "lvhd_glvMeasure";
      lvhd_glvMeasure.Text = "グローブ温度";
      lvhd_glvMeasure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_glvMeasure.Width = 100;
      // 
      // lvhd_glvInterval
      // 
      lvhd_glvInterval.Name = "lvhd_glvInterval";
      lvhd_glvInterval.Text = "測定間隔";
      lvhd_glvInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_glvInterval.Width = 100;
      // 
      // lvhd_velMeasure
      // 
      lvhd_velMeasure.Name = "lvhd_velMeasure";
      lvhd_velMeasure.Text = "微風速";
      lvhd_velMeasure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_velMeasure.Width = 100;
      // 
      // lvhd_velInterval
      // 
      lvhd_velInterval.Name = "lvhd_velInterval";
      lvhd_velInterval.Text = "測定間隔";
      lvhd_velInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_velInterval.Width = 100;
      // 
      // lvhd_illMeasure
      // 
      lvhd_illMeasure.Name = "lvhd_illMeasure";
      lvhd_illMeasure.Text = "照度";
      lvhd_illMeasure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_illMeasure.Width = 100;
      // 
      // lvhd_illInterval
      // 
      lvhd_illInterval.Name = "lvhd_illInterval";
      lvhd_illInterval.Text = "測定間隔";
      lvhd_illInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_illInterval.Width = 100;
      // 
      // lvhd_startTime
      // 
      lvhd_startTime.Name = "lvhd_startTime";
      lvhd_startTime.Text = "開始日時";
      lvhd_startTime.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_startTime.Width = 120;
      // 
      // lvhd_gv1Measure
      // 
      lvhd_gv1Measure.Text = "電圧1";
      lvhd_gv1Measure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_gv1Measure.Width = 100;
      // 
      // lvhd_gv1Interval
      // 
      lvhd_gv1Interval.Text = "測定間隔";
      lvhd_gv1Interval.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_gv1Interval.Width = 100;
      // 
      // lvhd_prxMeasure
      // 
      lvhd_prxMeasure.Text = "近接";
      lvhd_prxMeasure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd_prxMeasure.Width = 100;
      // 
      // lv_measure
      // 
      lv_measure.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] { lvhd2_xbeeID, lvhd2_name, lvhd2_dbt, lvhd2_hmd, lvhd2_glb, lvhd2_vel, lvhd2_ill, lvhd2_pmv, lvhd2_ppd, lvhd2_set, lvhd2_dtime });
      lv_measure.Dock = System.Windows.Forms.DockStyle.Fill;
      lv_measure.FullRowSelect = true;
      lv_measure.Location = new System.Drawing.Point(0, 0);
      lv_measure.Name = "lv_measure";
      lv_measure.Size = new System.Drawing.Size(1866, 457);
      lv_measure.TabIndex = 4;
      lv_measure.UseCompatibleStateImageBehavior = false;
      lv_measure.View = System.Windows.Forms.View.Details;
      lv_measure.ColumnClick += listView_ColumnClick;
      lv_measure.SizeChanged += listView_SizeChanged;
      // 
      // lvhd2_xbeeID
      // 
      lvhd2_xbeeID.Text = "ID";
      lvhd2_xbeeID.Width = 100;
      // 
      // lvhd2_name
      // 
      lvhd2_name.Text = "Name";
      lvhd2_name.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd2_name.Width = 100;
      // 
      // lvhd2_dbt
      // 
      lvhd2_dbt.Text = "dbt";
      lvhd2_dbt.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd2_dbt.Width = 100;
      // 
      // lvhd2_hmd
      // 
      lvhd2_hmd.Text = "hmd";
      lvhd2_hmd.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd2_hmd.Width = 100;
      // 
      // lvhd2_glb
      // 
      lvhd2_glb.Text = "glb";
      lvhd2_glb.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd2_glb.Width = 100;
      // 
      // lvhd2_vel
      // 
      lvhd2_vel.Text = "vel";
      lvhd2_vel.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd2_vel.Width = 100;
      // 
      // lvhd2_ill
      // 
      lvhd2_ill.Text = "illuminance";
      lvhd2_ill.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd2_ill.Width = 100;
      // 
      // lvhd2_pmv
      // 
      lvhd2_pmv.Text = "pmv";
      lvhd2_pmv.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd2_pmv.Width = 100;
      // 
      // lvhd2_ppd
      // 
      lvhd2_ppd.Text = "ppd";
      lvhd2_ppd.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd2_ppd.Width = 100;
      // 
      // lvhd2_set
      // 
      lvhd2_set.Text = "set";
      lvhd2_set.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd2_set.Width = 100;
      // 
      // lvhd2_dtime
      // 
      lvhd2_dtime.Text = "dtime";
      lvhd2_dtime.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      lvhd2_dtime.Width = 120;
      // 
      // tbx_log
      // 
      tbx_log.Dock = System.Windows.Forms.DockStyle.Fill;
      tbx_log.Location = new System.Drawing.Point(0, 0);
      tbx_log.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      tbx_log.Multiline = true;
      tbx_log.Name = "tbx_log";
      tbx_log.ReadOnly = true;
      tbx_log.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
      tbx_log.Size = new System.Drawing.Size(1866, 376);
      tbx_log.TabIndex = 5;
      // 
      // pnl_settingEdit
      // 
      pnl_settingEdit.Controls.Add(cbx_saveToSDCard);
      pnl_settingEdit.Controls.Add(rbtn_ill);
      pnl_settingEdit.Controls.Add(rbtn_prox);
      pnl_settingEdit.Controls.Add(lbl_gpv3);
      pnl_settingEdit.Controls.Add(lbl_gpv2);
      pnl_settingEdit.Controls.Add(lbl_gpv1);
      pnl_settingEdit.Controls.Add(lbl_vel);
      pnl_settingEdit.Controls.Add(label9);
      pnl_settingEdit.Controls.Add(lbl_glb);
      pnl_settingEdit.Controls.Add(label7);
      pnl_settingEdit.Controls.Add(lbl_th);
      pnl_settingEdit.Controls.Add(cbx_gpv3Measure);
      pnl_settingEdit.Controls.Add(label4);
      pnl_settingEdit.Controls.Add(cbx_gpv2Measure);
      pnl_settingEdit.Controls.Add(btn_startCollecting);
      pnl_settingEdit.Controls.Add(tbx_gpv3Interval);
      pnl_settingEdit.Controls.Add(cbx_gpv1Measure);
      pnl_settingEdit.Controls.Add(tbx_gpv2Interval);
      pnl_settingEdit.Controls.Add(label5);
      pnl_settingEdit.Controls.Add(tbx_gpv1Interval);
      pnl_settingEdit.Controls.Add(cbx_illMeasure);
      pnl_settingEdit.Controls.Add(tbx_illInterval);
      pnl_settingEdit.Controls.Add(lbl_SDTime);
      pnl_settingEdit.Controls.Add(dtp_timer);
      pnl_settingEdit.Controls.Add(btn_outputSD);
      pnl_settingEdit.Controls.Add(btn_setCFactor);
      pnl_settingEdit.Controls.Add(btn_applySetting);
      pnl_settingEdit.Controls.Add(label3);
      pnl_settingEdit.Controls.Add(cbx_thMeasure);
      pnl_settingEdit.Controls.Add(label2);
      pnl_settingEdit.Controls.Add(cbx_glbMeasure);
      pnl_settingEdit.Controls.Add(tbx_glbInterval);
      pnl_settingEdit.Controls.Add(label1);
      pnl_settingEdit.Controls.Add(tbx_thInterval);
      pnl_settingEdit.Controls.Add(cbx_velMeasure);
      pnl_settingEdit.Controls.Add(tbx_velInterval);
      pnl_settingEdit.Dock = System.Windows.Forms.DockStyle.Left;
      pnl_settingEdit.Enabled = false;
      pnl_settingEdit.Location = new System.Drawing.Point(0, 58);
      pnl_settingEdit.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      pnl_settingEdit.Name = "pnl_settingEdit";
      pnl_settingEdit.Size = new System.Drawing.Size(508, 1403);
      pnl_settingEdit.TabIndex = 2;
      // 
      // cbx_saveToSDCard
      // 
      cbx_saveToSDCard.AutoSize = true;
      cbx_saveToSDCard.Location = new System.Drawing.Point(20, 1206);
      cbx_saveToSDCard.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      cbx_saveToSDCard.Name = "cbx_saveToSDCard";
      cbx_saveToSDCard.Size = new System.Drawing.Size(214, 36);
      cbx_saveToSDCard.TabIndex = 16;
      cbx_saveToSDCard.TabStop = false;
      cbx_saveToSDCard.Text = "Save to SD card";
      cbx_saveToSDCard.UseVisualStyleBackColor = true;
      // 
      // rbtn_ill
      // 
      rbtn_ill.AutoSize = true;
      rbtn_ill.Checked = true;
      rbtn_ill.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      rbtn_ill.Location = new System.Drawing.Point(20, 350);
      rbtn_ill.Name = "rbtn_ill";
      rbtn_ill.Size = new System.Drawing.Size(170, 36);
      rbtn_ill.TabIndex = 15;
      rbtn_ill.TabStop = true;
      rbtn_ill.Text = "Illuminance";
      rbtn_ill.UseVisualStyleBackColor = true;
      // 
      // rbtn_prox
      // 
      rbtn_prox.AutoSize = true;
      rbtn_prox.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      rbtn_prox.Location = new System.Drawing.Point(200, 350);
      rbtn_prox.Name = "rbtn_prox";
      rbtn_prox.Size = new System.Drawing.Size(148, 36);
      rbtn_prox.TabIndex = 14;
      rbtn_prox.Text = "Proximity";
      rbtn_prox.UseVisualStyleBackColor = true;
      // 
      // lbl_gpv3
      // 
      lbl_gpv3.AutoSize = true;
      lbl_gpv3.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      lbl_gpv3.Location = new System.Drawing.Point(20, 680);
      lbl_gpv3.Name = "lbl_gpv3";
      lbl_gpv3.Size = new System.Drawing.Size(302, 32);
      lbl_gpv3.TabIndex = 13;
      lbl_gpv3.Text = "General purpose voltage 3";
      lbl_gpv3.Visible = false;
      // 
      // lbl_gpv2
      // 
      lbl_gpv2.AutoSize = true;
      lbl_gpv2.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      lbl_gpv2.Location = new System.Drawing.Point(20, 570);
      lbl_gpv2.Name = "lbl_gpv2";
      lbl_gpv2.Size = new System.Drawing.Size(302, 32);
      lbl_gpv2.TabIndex = 13;
      lbl_gpv2.Text = "General purpose voltage 2";
      lbl_gpv2.Visible = false;
      // 
      // lbl_gpv1
      // 
      lbl_gpv1.AutoSize = true;
      lbl_gpv1.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      lbl_gpv1.Location = new System.Drawing.Point(20, 460);
      lbl_gpv1.Name = "lbl_gpv1";
      lbl_gpv1.Size = new System.Drawing.Size(299, 32);
      lbl_gpv1.TabIndex = 13;
      lbl_gpv1.Text = "General purpose voltage 1";
      // 
      // lbl_vel
      // 
      lbl_vel.AutoSize = true;
      lbl_vel.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      lbl_vel.Location = new System.Drawing.Point(20, 240);
      lbl_vel.Name = "lbl_vel";
      lbl_vel.Size = new System.Drawing.Size(99, 32);
      lbl_vel.TabIndex = 13;
      lbl_vel.Text = "Velocity";
      // 
      // label9
      // 
      label9.AutoSize = true;
      label9.Location = new System.Drawing.Point(325, 734);
      label9.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      label9.Name = "label9";
      label9.Size = new System.Drawing.Size(48, 32);
      label9.TabIndex = 11;
      label9.Text = "sec";
      label9.Visible = false;
      // 
      // lbl_glb
      // 
      lbl_glb.AutoSize = true;
      lbl_glb.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      lbl_glb.Location = new System.Drawing.Point(20, 130);
      lbl_glb.Name = "lbl_glb";
      lbl_glb.Size = new System.Drawing.Size(222, 32);
      lbl_glb.TabIndex = 13;
      lbl_glb.Text = "Globe temperature";
      // 
      // label7
      // 
      label7.AutoSize = true;
      label7.Location = new System.Drawing.Point(325, 624);
      label7.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      label7.Name = "label7";
      label7.Size = new System.Drawing.Size(48, 32);
      label7.TabIndex = 11;
      label7.Text = "sec";
      label7.Visible = false;
      // 
      // lbl_th
      // 
      lbl_th.AutoSize = true;
      lbl_th.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      lbl_th.Location = new System.Drawing.Point(20, 20);
      lbl_th.Name = "lbl_th";
      lbl_th.Size = new System.Drawing.Size(468, 32);
      lbl_th.TabIndex = 12;
      lbl_th.Text = "Dry-bulb temperature / Relative humdiity";
      // 
      // cbx_gpv3Measure
      // 
      cbx_gpv3Measure.AutoSize = true;
      cbx_gpv3Measure.Location = new System.Drawing.Point(60, 730);
      cbx_gpv3Measure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      cbx_gpv3Measure.Name = "cbx_gpv3Measure";
      cbx_gpv3Measure.Size = new System.Drawing.Size(138, 36);
      cbx_gpv3Measure.TabIndex = 7;
      cbx_gpv3Measure.TabStop = false;
      cbx_gpv3Measure.Text = "Measure";
      cbx_gpv3Measure.UseVisualStyleBackColor = true;
      cbx_gpv3Measure.Visible = false;
      cbx_gpv3Measure.CheckedChanged += cbx_measure_CheckedChanged;
      // 
      // label4
      // 
      label4.AutoSize = true;
      label4.Location = new System.Drawing.Point(325, 514);
      label4.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      label4.Name = "label4";
      label4.Size = new System.Drawing.Size(48, 32);
      label4.TabIndex = 11;
      label4.Text = "sec";
      // 
      // cbx_gpv2Measure
      // 
      cbx_gpv2Measure.AutoSize = true;
      cbx_gpv2Measure.Location = new System.Drawing.Point(60, 620);
      cbx_gpv2Measure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      cbx_gpv2Measure.Name = "cbx_gpv2Measure";
      cbx_gpv2Measure.Size = new System.Drawing.Size(138, 36);
      cbx_gpv2Measure.TabIndex = 7;
      cbx_gpv2Measure.TabStop = false;
      cbx_gpv2Measure.Text = "Measure";
      cbx_gpv2Measure.UseVisualStyleBackColor = true;
      cbx_gpv2Measure.Visible = false;
      cbx_gpv2Measure.CheckedChanged += cbx_measure_CheckedChanged;
      // 
      // btn_startCollecting
      // 
      btn_startCollecting.Image = (System.Drawing.Image)resources.GetObject("btn_startCollecting.Image");
      btn_startCollecting.Location = new System.Drawing.Point(20, 987);
      btn_startCollecting.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      btn_startCollecting.Name = "btn_startCollecting";
      btn_startCollecting.Size = new System.Drawing.Size(468, 55);
      btn_startCollecting.TabIndex = 10;
      btn_startCollecting.Text = "Start data collecting";
      btn_startCollecting.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      btn_startCollecting.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
      btn_startCollecting.UseVisualStyleBackColor = true;
      btn_startCollecting.Click += btn_startMLogger_Click;
      // 
      // tbx_gpv3Interval
      // 
      tbx_gpv3Interval.Location = new System.Drawing.Point(227, 728);
      tbx_gpv3Interval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      tbx_gpv3Interval.Name = "tbx_gpv3Interval";
      tbx_gpv3Interval.Size = new System.Drawing.Size(80, 39);
      tbx_gpv3Interval.TabIndex = 6;
      tbx_gpv3Interval.Text = "60";
      tbx_gpv3Interval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      tbx_gpv3Interval.Visible = false;
      // 
      // cbx_gpv1Measure
      // 
      cbx_gpv1Measure.AutoSize = true;
      cbx_gpv1Measure.Location = new System.Drawing.Point(60, 510);
      cbx_gpv1Measure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      cbx_gpv1Measure.Name = "cbx_gpv1Measure";
      cbx_gpv1Measure.Size = new System.Drawing.Size(138, 36);
      cbx_gpv1Measure.TabIndex = 7;
      cbx_gpv1Measure.TabStop = false;
      cbx_gpv1Measure.Text = "Measure";
      cbx_gpv1Measure.UseVisualStyleBackColor = true;
      cbx_gpv1Measure.CheckedChanged += cbx_measure_CheckedChanged;
      // 
      // tbx_gpv2Interval
      // 
      tbx_gpv2Interval.Location = new System.Drawing.Point(227, 618);
      tbx_gpv2Interval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      tbx_gpv2Interval.Name = "tbx_gpv2Interval";
      tbx_gpv2Interval.Size = new System.Drawing.Size(80, 39);
      tbx_gpv2Interval.TabIndex = 5;
      tbx_gpv2Interval.Text = "60";
      tbx_gpv2Interval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      tbx_gpv2Interval.Visible = false;
      // 
      // label5
      // 
      label5.AutoSize = true;
      label5.Location = new System.Drawing.Point(325, 404);
      label5.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      label5.Name = "label5";
      label5.Size = new System.Drawing.Size(48, 32);
      label5.TabIndex = 11;
      label5.Text = "sec";
      // 
      // tbx_gpv1Interval
      // 
      tbx_gpv1Interval.Location = new System.Drawing.Point(227, 508);
      tbx_gpv1Interval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      tbx_gpv1Interval.Name = "tbx_gpv1Interval";
      tbx_gpv1Interval.Size = new System.Drawing.Size(80, 39);
      tbx_gpv1Interval.TabIndex = 4;
      tbx_gpv1Interval.Text = "60";
      tbx_gpv1Interval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      // 
      // cbx_illMeasure
      // 
      cbx_illMeasure.AutoSize = true;
      cbx_illMeasure.Location = new System.Drawing.Point(60, 400);
      cbx_illMeasure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      cbx_illMeasure.Name = "cbx_illMeasure";
      cbx_illMeasure.Size = new System.Drawing.Size(138, 36);
      cbx_illMeasure.TabIndex = 7;
      cbx_illMeasure.TabStop = false;
      cbx_illMeasure.Text = "Measure";
      cbx_illMeasure.UseVisualStyleBackColor = true;
      cbx_illMeasure.CheckedChanged += cbx_measure_CheckedChanged;
      // 
      // tbx_illInterval
      // 
      tbx_illInterval.Location = new System.Drawing.Point(227, 398);
      tbx_illInterval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      tbx_illInterval.Name = "tbx_illInterval";
      tbx_illInterval.Size = new System.Drawing.Size(80, 39);
      tbx_illInterval.TabIndex = 3;
      tbx_illInterval.Text = "60";
      tbx_illInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      // 
      // lbl_SDTime
      // 
      lbl_SDTime.AutoSize = true;
      lbl_SDTime.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      lbl_SDTime.Location = new System.Drawing.Point(20, 807);
      lbl_SDTime.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      lbl_SDTime.Name = "lbl_SDTime";
      lbl_SDTime.Size = new System.Drawing.Size(368, 32);
      lbl_SDTime.TabIndex = 8;
      lbl_SDTime.Text = "Mesaurement starting date time";
      // 
      // dtp_timer
      // 
      dtp_timer.CustomFormat = "yyyy/MM/dd HH:mm";
      dtp_timer.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
      dtp_timer.Location = new System.Drawing.Point(60, 857);
      dtp_timer.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      dtp_timer.Name = "dtp_timer";
      dtp_timer.Size = new System.Drawing.Size(428, 39);
      dtp_timer.TabIndex = 8;
      dtp_timer.TabStop = false;
      dtp_timer.Value = new System.DateTime(2000, 1, 1, 0, 0, 0, 0);
      // 
      // btn_outputSD
      // 
      btn_outputSD.Image = Properties.Resources.sd;
      btn_outputSD.Location = new System.Drawing.Point(20, 1133);
      btn_outputSD.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      btn_outputSD.Name = "btn_outputSD";
      btn_outputSD.Size = new System.Drawing.Size(468, 55);
      btn_outputSD.TabIndex = 9;
      btn_outputSD.Text = "Start logging (SD card)";
      btn_outputSD.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      btn_outputSD.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
      btn_outputSD.UseVisualStyleBackColor = true;
      btn_outputSD.Click += btn_outputSD_Click;
      // 
      // btn_setCFactor
      // 
      btn_setCFactor.Image = Properties.Resources.ram;
      btn_setCFactor.Location = new System.Drawing.Point(20, 1060);
      btn_setCFactor.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      btn_setCFactor.Name = "btn_setCFactor";
      btn_setCFactor.Size = new System.Drawing.Size(468, 55);
      btn_setCFactor.TabIndex = 9;
      btn_setCFactor.Text = "Set correction factors";
      btn_setCFactor.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      btn_setCFactor.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
      btn_setCFactor.UseVisualStyleBackColor = true;
      btn_setCFactor.Click += btn_setCFactor_Click;
      // 
      // btn_applySetting
      // 
      btn_applySetting.Image = (System.Drawing.Image)resources.GetObject("btn_applySetting.Image");
      btn_applySetting.Location = new System.Drawing.Point(20, 914);
      btn_applySetting.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      btn_applySetting.Name = "btn_applySetting";
      btn_applySetting.Size = new System.Drawing.Size(468, 55);
      btn_applySetting.TabIndex = 9;
      btn_applySetting.Text = "Apply setting";
      btn_applySetting.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      btn_applySetting.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
      btn_applySetting.UseVisualStyleBackColor = true;
      btn_applySetting.Click += btn_updateMSetting_Click;
      // 
      // label3
      // 
      label3.AutoSize = true;
      label3.Location = new System.Drawing.Point(325, 294);
      label3.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      label3.Name = "label3";
      label3.Size = new System.Drawing.Size(48, 32);
      label3.TabIndex = 5;
      label3.Text = "sec";
      // 
      // cbx_thMeasure
      // 
      cbx_thMeasure.AutoSize = true;
      cbx_thMeasure.Location = new System.Drawing.Point(60, 70);
      cbx_thMeasure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      cbx_thMeasure.Name = "cbx_thMeasure";
      cbx_thMeasure.Size = new System.Drawing.Size(138, 36);
      cbx_thMeasure.TabIndex = 4;
      cbx_thMeasure.TabStop = false;
      cbx_thMeasure.Text = "Measure";
      cbx_thMeasure.UseVisualStyleBackColor = true;
      cbx_thMeasure.CheckedChanged += cbx_measure_CheckedChanged;
      // 
      // label2
      // 
      label2.AutoSize = true;
      label2.Location = new System.Drawing.Point(325, 185);
      label2.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      label2.Name = "label2";
      label2.Size = new System.Drawing.Size(48, 32);
      label2.TabIndex = 5;
      label2.Text = "sec";
      // 
      // cbx_glbMeasure
      // 
      cbx_glbMeasure.AutoSize = true;
      cbx_glbMeasure.Location = new System.Drawing.Point(60, 180);
      cbx_glbMeasure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      cbx_glbMeasure.Name = "cbx_glbMeasure";
      cbx_glbMeasure.Size = new System.Drawing.Size(138, 36);
      cbx_glbMeasure.TabIndex = 5;
      cbx_glbMeasure.TabStop = false;
      cbx_glbMeasure.Text = "Measure";
      cbx_glbMeasure.UseVisualStyleBackColor = true;
      cbx_glbMeasure.CheckedChanged += cbx_measure_CheckedChanged;
      // 
      // tbx_glbInterval
      // 
      tbx_glbInterval.Location = new System.Drawing.Point(227, 178);
      tbx_glbInterval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      tbx_glbInterval.Name = "tbx_glbInterval";
      tbx_glbInterval.Size = new System.Drawing.Size(80, 39);
      tbx_glbInterval.TabIndex = 1;
      tbx_glbInterval.Text = "60";
      tbx_glbInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      // 
      // label1
      // 
      label1.AutoSize = true;
      label1.Location = new System.Drawing.Point(321, 71);
      label1.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      label1.Name = "label1";
      label1.Size = new System.Drawing.Size(48, 32);
      label1.TabIndex = 5;
      label1.Text = "sec";
      // 
      // tbx_thInterval
      // 
      tbx_thInterval.Location = new System.Drawing.Point(227, 68);
      tbx_thInterval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      tbx_thInterval.Name = "tbx_thInterval";
      tbx_thInterval.Size = new System.Drawing.Size(80, 39);
      tbx_thInterval.TabIndex = 0;
      tbx_thInterval.Text = "60";
      tbx_thInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      // 
      // cbx_velMeasure
      // 
      cbx_velMeasure.AutoSize = true;
      cbx_velMeasure.Location = new System.Drawing.Point(60, 290);
      cbx_velMeasure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      cbx_velMeasure.Name = "cbx_velMeasure";
      cbx_velMeasure.Size = new System.Drawing.Size(138, 36);
      cbx_velMeasure.TabIndex = 6;
      cbx_velMeasure.TabStop = false;
      cbx_velMeasure.Text = "Measure";
      cbx_velMeasure.UseVisualStyleBackColor = true;
      cbx_velMeasure.CheckedChanged += cbx_measure_CheckedChanged;
      // 
      // tbx_velInterval
      // 
      tbx_velInterval.Location = new System.Drawing.Point(227, 288);
      tbx_velInterval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      tbx_velInterval.Name = "tbx_velInterval";
      tbx_velInterval.Size = new System.Drawing.Size(80, 39);
      tbx_velInterval.TabIndex = 2;
      tbx_velInterval.Text = "60";
      tbx_velInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      // 
      // MainForm
      // 
      AutoScaleDimensions = new System.Drawing.SizeF(13F, 32F);
      AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      ClientSize = new System.Drawing.Size(2374, 1461);
      Controls.Add(splitContainer1);
      Controls.Add(pnl_settingEdit);
      Controls.Add(toolStrip);
      Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      MinimumSize = new System.Drawing.Size(2400, 1350);
      Name = "MainForm";
      ShowIcon = false;
      Text = "MLController version 1.1.0";
      WindowState = System.Windows.Forms.FormWindowState.Maximized;
      toolStrip.ResumeLayout(false);
      toolStrip.PerformLayout();
      splitContainer1.Panel1.ResumeLayout(false);
      splitContainer1.Panel2.ResumeLayout(false);
      splitContainer1.Panel2.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
      splitContainer1.ResumeLayout(false);
      splitContainer2.Panel1.ResumeLayout(false);
      splitContainer2.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)splitContainer2).EndInit();
      splitContainer2.ResumeLayout(false);
      pnl_settingEdit.ResumeLayout(false);
      pnl_settingEdit.PerformLayout();
      ResumeLayout(false);
      PerformLayout();
    }

    #endregion
    private System.Windows.Forms.ToolStrip toolStrip;
    private System.Windows.Forms.ToolStripButton tsb_reload;
    private System.Windows.Forms.ToolStripButton tsb_downloading;
    private System.Windows.Forms.SplitContainer splitContainer1;
    private System.Windows.Forms.ListView lv_setting;
    private System.Windows.Forms.ColumnHeader lvhd_xbeeID;
    private System.Windows.Forms.ColumnHeader lvhd_step;
    private System.Windows.Forms.ColumnHeader lvhd_thMeasure;
    private System.Windows.Forms.ColumnHeader lvhd_thInterval;
    private System.Windows.Forms.ColumnHeader lvhd_glvMeasure;
    private System.Windows.Forms.ColumnHeader lvhd_glvInterval;
    private System.Windows.Forms.ColumnHeader lvhd_velMeasure;
    private System.Windows.Forms.ColumnHeader lvhd_velInterval;
    private System.Windows.Forms.ColumnHeader lvhd_illMeasure;
    private System.Windows.Forms.ColumnHeader lvhd_illInterval;
    private System.Windows.Forms.ColumnHeader lvhd_startTime;
    private System.Windows.Forms.Panel pnl_settingEdit;
    private System.Windows.Forms.Label label5;
    private System.Windows.Forms.CheckBox cbx_illMeasure;
    private System.Windows.Forms.TextBox tbx_illInterval;
    private System.Windows.Forms.Label lbl_SDTime;
    private System.Windows.Forms.DateTimePicker dtp_timer;
    private System.Windows.Forms.Button btn_applySetting;
    private System.Windows.Forms.Label label3;
    private System.Windows.Forms.CheckBox cbx_thMeasure;
    private System.Windows.Forms.Label label2;
    private System.Windows.Forms.CheckBox cbx_glbMeasure;
    private System.Windows.Forms.TextBox tbx_glbInterval;
    private System.Windows.Forms.Label label1;
    private System.Windows.Forms.TextBox tbx_thInterval;
    private System.Windows.Forms.CheckBox cbx_velMeasure;
    private System.Windows.Forms.TextBox tbx_velInterval;
    private System.Windows.Forms.TextBox tbx_log;
    private System.Windows.Forms.ToolStripSplitButton tssb_connection;
    private System.Windows.Forms.Button btn_startCollecting;
    private System.Windows.Forms.ColumnHeader lvhd_xbeeName;
    private System.Windows.Forms.Button btn_setCFactor;
    private System.Windows.Forms.Button btn_outputSD;
    private System.Windows.Forms.Label lbl_vel;
    private System.Windows.Forms.Label lbl_glb;
    private System.Windows.Forms.Label lbl_th;
    private System.Windows.Forms.Label lbl_gpv3;
    private System.Windows.Forms.Label lbl_gpv2;
    private System.Windows.Forms.Label lbl_gpv1;
    private System.Windows.Forms.Label label9;
    private System.Windows.Forms.Label label7;
    private System.Windows.Forms.CheckBox cbx_gpv3Measure;
    private System.Windows.Forms.Label label4;
    private System.Windows.Forms.CheckBox cbx_gpv2Measure;
    private System.Windows.Forms.TextBox tbx_gpv3Interval;
    private System.Windows.Forms.CheckBox cbx_gpv1Measure;
    private System.Windows.Forms.TextBox tbx_gpv2Interval;
    private System.Windows.Forms.TextBox tbx_gpv1Interval;
    private System.Windows.Forms.ColumnHeader lvhd_gv1Measure;
    private System.Windows.Forms.ColumnHeader lvhd_gv1Interval;
    private System.Windows.Forms.RadioButton rbtn_ill;
    private System.Windows.Forms.RadioButton rbtn_prox;
    private System.Windows.Forms.ColumnHeader lvhd_prxMeasure;
    private System.Windows.Forms.ListView lv_measure;
    private System.Windows.Forms.ColumnHeader lvhd2_xbeeID;
    private System.Windows.Forms.ColumnHeader lvhd2_name;
    private System.Windows.Forms.ColumnHeader lvhd2_dbt;
    private System.Windows.Forms.ColumnHeader lvhd2_hmd;
    private System.Windows.Forms.ColumnHeader lvhd2_glb;
    private System.Windows.Forms.ColumnHeader lvhd2_vel;
    private System.Windows.Forms.ColumnHeader lvhd2_pmv;
    private System.Windows.Forms.ColumnHeader lvhd2_ppd;
    private System.Windows.Forms.ColumnHeader lvhd2_set;
    private System.Windows.Forms.ColumnHeader lvhd2_dtime;
    private System.Windows.Forms.SplitContainer splitContainer2;
    private System.Windows.Forms.ColumnHeader lvhd2_ill;
    private System.Windows.Forms.CheckBox cbx_saveToSDCard;
  }
}

