namespace MLServer
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
      this.toolStrip = new System.Windows.Forms.ToolStrip();
      this.tssb_connection = new System.Windows.Forms.ToolStripSplitButton();
      this.tsb_reload = new System.Windows.Forms.ToolStripButton();
      this.tsb_downloading = new System.Windows.Forms.ToolStripButton();
      this.splitContainer1 = new System.Windows.Forms.SplitContainer();
      this.splitContainer2 = new System.Windows.Forms.SplitContainer();
      this.lv_setting = new System.Windows.Forms.ListView();
      this.lvhd_xbeeID = new System.Windows.Forms.ColumnHeader();
      this.lvhd_xbeeName = new System.Windows.Forms.ColumnHeader();
      this.lvhd_step = new System.Windows.Forms.ColumnHeader();
      this.lvhd_thMeasure = new System.Windows.Forms.ColumnHeader();
      this.lvhd_thInterval = new System.Windows.Forms.ColumnHeader();
      this.lvhd_glvMeasure = new System.Windows.Forms.ColumnHeader();
      this.lvhd_glvInterval = new System.Windows.Forms.ColumnHeader();
      this.lvhd_velMeasure = new System.Windows.Forms.ColumnHeader();
      this.lvhd_velInterval = new System.Windows.Forms.ColumnHeader();
      this.lvhd_illMeasure = new System.Windows.Forms.ColumnHeader();
      this.lvhd_illInterval = new System.Windows.Forms.ColumnHeader();
      this.lvhd_startTime = new System.Windows.Forms.ColumnHeader();
      this.lvhd_gv1Measure = new System.Windows.Forms.ColumnHeader();
      this.lvhd_gv1Interval = new System.Windows.Forms.ColumnHeader();
      this.lvhd_prxMeasure = new System.Windows.Forms.ColumnHeader();
      this.lv_measure = new System.Windows.Forms.ListView();
      this.lvhd2_xbeeID = new System.Windows.Forms.ColumnHeader();
      this.lvhd2_name = new System.Windows.Forms.ColumnHeader();
      this.lvhd2_dbt = new System.Windows.Forms.ColumnHeader();
      this.lvhd2_hmd = new System.Windows.Forms.ColumnHeader();
      this.lvhd2_glb = new System.Windows.Forms.ColumnHeader();
      this.lvhd2_vel = new System.Windows.Forms.ColumnHeader();
      this.lvhd2_ill = new System.Windows.Forms.ColumnHeader();
      this.lvhd2_pmv = new System.Windows.Forms.ColumnHeader();
      this.lvhd2_ppd = new System.Windows.Forms.ColumnHeader();
      this.lvhd2_set = new System.Windows.Forms.ColumnHeader();
      this.lvhd2_dtime = new System.Windows.Forms.ColumnHeader();
      this.tbx_log = new System.Windows.Forms.TextBox();
      this.pnl_settingEdit = new System.Windows.Forms.Panel();
      this.rbtn_ill = new System.Windows.Forms.RadioButton();
      this.rbtn_prox = new System.Windows.Forms.RadioButton();
      this.lbl_gpv3 = new System.Windows.Forms.Label();
      this.lbl_gpv2 = new System.Windows.Forms.Label();
      this.lbl_gpv1 = new System.Windows.Forms.Label();
      this.lbl_vel = new System.Windows.Forms.Label();
      this.label9 = new System.Windows.Forms.Label();
      this.lbl_glb = new System.Windows.Forms.Label();
      this.label7 = new System.Windows.Forms.Label();
      this.lbl_th = new System.Windows.Forms.Label();
      this.cbx_gpv3Measure = new System.Windows.Forms.CheckBox();
      this.label4 = new System.Windows.Forms.Label();
      this.cbx_gpv2Measure = new System.Windows.Forms.CheckBox();
      this.btn_startCollecting = new System.Windows.Forms.Button();
      this.tbx_gpv3Interval = new System.Windows.Forms.TextBox();
      this.cbx_gpv1Measure = new System.Windows.Forms.CheckBox();
      this.tbx_gpv2Interval = new System.Windows.Forms.TextBox();
      this.label5 = new System.Windows.Forms.Label();
      this.tbx_gpv1Interval = new System.Windows.Forms.TextBox();
      this.cbx_illMeasure = new System.Windows.Forms.CheckBox();
      this.tbx_illInterval = new System.Windows.Forms.TextBox();
      this.lbl_SDTime = new System.Windows.Forms.Label();
      this.dtp_timer = new System.Windows.Forms.DateTimePicker();
      this.btn_outputSD = new System.Windows.Forms.Button();
      this.btn_setCFactor = new System.Windows.Forms.Button();
      this.btn_applySetting = new System.Windows.Forms.Button();
      this.label3 = new System.Windows.Forms.Label();
      this.cbx_thMeasure = new System.Windows.Forms.CheckBox();
      this.label2 = new System.Windows.Forms.Label();
      this.cbx_glbMeasure = new System.Windows.Forms.CheckBox();
      this.tbx_glbInterval = new System.Windows.Forms.TextBox();
      this.label1 = new System.Windows.Forms.Label();
      this.tbx_thInterval = new System.Windows.Forms.TextBox();
      this.cbx_velMeasure = new System.Windows.Forms.CheckBox();
      this.tbx_velInterval = new System.Windows.Forms.TextBox();
      this.toolStrip.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
      this.splitContainer1.Panel1.SuspendLayout();
      this.splitContainer1.Panel2.SuspendLayout();
      this.splitContainer1.SuspendLayout();
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
      this.splitContainer2.Panel1.SuspendLayout();
      this.splitContainer2.Panel2.SuspendLayout();
      this.splitContainer2.SuspendLayout();
      this.pnl_settingEdit.SuspendLayout();
      this.SuspendLayout();
      // 
      // toolStrip
      // 
      this.toolStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
      this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tssb_connection,
            this.tsb_reload,
            this.tsb_downloading});
      this.toolStrip.Location = new System.Drawing.Point(0, 0);
      this.toolStrip.Name = "toolStrip";
      this.toolStrip.Padding = new System.Windows.Forms.Padding(0, 0, 4, 0);
      this.toolStrip.Size = new System.Drawing.Size(2374, 58);
      this.toolStrip.TabIndex = 9;
      this.toolStrip.Text = "toolStrip1";
      // 
      // tssb_connection
      // 
      this.tssb_connection.Image = ((System.Drawing.Image)(resources.GetObject("tssb_connection.Image")));
      this.tssb_connection.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
      this.tssb_connection.ImageTransparentColor = System.Drawing.Color.Magenta;
      this.tssb_connection.Name = "tssb_connection";
      this.tssb_connection.Size = new System.Drawing.Size(178, 52);
      this.tssb_connection.Text = global::MLServer.i18n.Resources.MF_Connect;
      this.tssb_connection.ButtonClick += new System.EventHandler(this.tsb_connection_Click);
      this.tssb_connection.DropDownOpening += new System.EventHandler(this.tssb_connection_DropDownOpening);
      // 
      // tsb_reload
      // 
      this.tsb_reload.Enabled = false;
      this.tsb_reload.Image = ((System.Drawing.Image)(resources.GetObject("tsb_reload.Image")));
      this.tsb_reload.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
      this.tsb_reload.ImageTransparentColor = System.Drawing.Color.Magenta;
      this.tsb_reload.Name = "tsb_reload";
      this.tsb_reload.Size = new System.Drawing.Size(137, 52);
      this.tsb_reload.Text = "Search";
      this.tsb_reload.ToolTipText = "周囲のXBee端末を探索します";
      this.tsb_reload.Click += new System.EventHandler(this.tsb_reload_Click);
      // 
      // tsb_downloading
      // 
      this.tsb_downloading.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
      this.tsb_downloading.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
      this.tsb_downloading.Image = ((System.Drawing.Image)(resources.GetObject("tsb_downloading.Image")));
      this.tsb_downloading.ImageScaling = System.Windows.Forms.ToolStripItemImageScaling.None;
      this.tsb_downloading.ImageTransparentColor = System.Drawing.Color.Magenta;
      this.tsb_downloading.Name = "tsb_downloading";
      this.tsb_downloading.Size = new System.Drawing.Size(52, 52);
      this.tsb_downloading.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageAboveText;
      // 
      // splitContainer1
      // 
      this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
      this.splitContainer1.Location = new System.Drawing.Point(508, 58);
      this.splitContainer1.Margin = new System.Windows.Forms.Padding(6);
      this.splitContainer1.Name = "splitContainer1";
      this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
      // 
      // splitContainer1.Panel1
      // 
      this.splitContainer1.Panel1.Controls.Add(this.splitContainer2);
      this.splitContainer1.Panel1MinSize = 300;
      // 
      // splitContainer1.Panel2
      // 
      this.splitContainer1.Panel2.Controls.Add(this.tbx_log);
      this.splitContainer1.Size = new System.Drawing.Size(1866, 1221);
      this.splitContainer1.SplitterDistance = 887;
      this.splitContainer1.SplitterWidth = 9;
      this.splitContainer1.TabIndex = 17;
      // 
      // splitContainer2
      // 
      this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
      this.splitContainer2.Location = new System.Drawing.Point(0, 0);
      this.splitContainer2.Name = "splitContainer2";
      this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
      // 
      // splitContainer2.Panel1
      // 
      this.splitContainer2.Panel1.Controls.Add(this.lv_setting);
      // 
      // splitContainer2.Panel2
      // 
      this.splitContainer2.Panel2.Controls.Add(this.lv_measure);
      this.splitContainer2.Size = new System.Drawing.Size(1866, 887);
      this.splitContainer2.SplitterDistance = 486;
      this.splitContainer2.TabIndex = 5;
      // 
      // lv_setting
      // 
      this.lv_setting.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.lvhd_xbeeID,
            this.lvhd_xbeeName,
            this.lvhd_step,
            this.lvhd_thMeasure,
            this.lvhd_thInterval,
            this.lvhd_glvMeasure,
            this.lvhd_glvInterval,
            this.lvhd_velMeasure,
            this.lvhd_velInterval,
            this.lvhd_illMeasure,
            this.lvhd_illInterval,
            this.lvhd_startTime,
            this.lvhd_gv1Measure,
            this.lvhd_gv1Interval,
            this.lvhd_prxMeasure});
      this.lv_setting.Dock = System.Windows.Forms.DockStyle.Fill;
      this.lv_setting.FullRowSelect = true;
      this.lv_setting.Location = new System.Drawing.Point(0, 0);
      this.lv_setting.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.lv_setting.Name = "lv_setting";
      this.lv_setting.Size = new System.Drawing.Size(1866, 486);
      this.lv_setting.TabIndex = 3;
      this.lv_setting.UseCompatibleStateImageBehavior = false;
      this.lv_setting.View = System.Windows.Forms.View.Details;
      this.lv_setting.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listView_ColumnClick);
      this.lv_setting.SelectedIndexChanged += new System.EventHandler(this.lv_setting_SelectedIndexChanged);
      this.lv_setting.SizeChanged += new System.EventHandler(this.listView_SizeChanged);
      // 
      // lvhd_xbeeID
      // 
      this.lvhd_xbeeID.Name = "lvhd_xbeeID";
      this.lvhd_xbeeID.Text = "ID";
      this.lvhd_xbeeID.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_xbeeID.Width = 100;
      // 
      // lvhd_xbeeName
      // 
      this.lvhd_xbeeName.Name = "lvhd_xbeeName";
      this.lvhd_xbeeName.Text = "名前";
      this.lvhd_xbeeName.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_xbeeName.Width = 100;
      // 
      // lvhd_step
      // 
      this.lvhd_step.Name = "lvhd_step";
      this.lvhd_step.Text = "状態";
      this.lvhd_step.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_step.Width = 100;
      // 
      // lvhd_thMeasure
      // 
      this.lvhd_thMeasure.Name = "lvhd_thMeasure";
      this.lvhd_thMeasure.Text = "温湿度";
      this.lvhd_thMeasure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_thMeasure.Width = 100;
      // 
      // lvhd_thInterval
      // 
      this.lvhd_thInterval.Name = "lvhd_thInterval";
      this.lvhd_thInterval.Text = "測定間隔";
      this.lvhd_thInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_thInterval.Width = 100;
      // 
      // lvhd_glvMeasure
      // 
      this.lvhd_glvMeasure.Name = "lvhd_glvMeasure";
      this.lvhd_glvMeasure.Text = "グローブ温度";
      this.lvhd_glvMeasure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_glvMeasure.Width = 100;
      // 
      // lvhd_glvInterval
      // 
      this.lvhd_glvInterval.Name = "lvhd_glvInterval";
      this.lvhd_glvInterval.Text = "測定間隔";
      this.lvhd_glvInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_glvInterval.Width = 100;
      // 
      // lvhd_velMeasure
      // 
      this.lvhd_velMeasure.Name = "lvhd_velMeasure";
      this.lvhd_velMeasure.Text = "微風速";
      this.lvhd_velMeasure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_velMeasure.Width = 100;
      // 
      // lvhd_velInterval
      // 
      this.lvhd_velInterval.Name = "lvhd_velInterval";
      this.lvhd_velInterval.Text = "測定間隔";
      this.lvhd_velInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_velInterval.Width = 100;
      // 
      // lvhd_illMeasure
      // 
      this.lvhd_illMeasure.Name = "lvhd_illMeasure";
      this.lvhd_illMeasure.Text = "照度";
      this.lvhd_illMeasure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_illMeasure.Width = 100;
      // 
      // lvhd_illInterval
      // 
      this.lvhd_illInterval.Name = "lvhd_illInterval";
      this.lvhd_illInterval.Text = "測定間隔";
      this.lvhd_illInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_illInterval.Width = 100;
      // 
      // lvhd_startTime
      // 
      this.lvhd_startTime.Name = "lvhd_startTime";
      this.lvhd_startTime.Text = "開始日時";
      this.lvhd_startTime.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_startTime.Width = 120;
      // 
      // lvhd_gv1Measure
      // 
      this.lvhd_gv1Measure.Text = "電圧1";
      this.lvhd_gv1Measure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_gv1Measure.Width = 100;
      // 
      // lvhd_gv1Interval
      // 
      this.lvhd_gv1Interval.Text = "測定間隔";
      this.lvhd_gv1Interval.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_gv1Interval.Width = 100;
      // 
      // lvhd_prxMeasure
      // 
      this.lvhd_prxMeasure.Text = "近接";
      this.lvhd_prxMeasure.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd_prxMeasure.Width = 100;
      // 
      // lv_measure
      // 
      this.lv_measure.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.lvhd2_xbeeID,
            this.lvhd2_name,
            this.lvhd2_dbt,
            this.lvhd2_hmd,
            this.lvhd2_glb,
            this.lvhd2_vel,
            this.lvhd2_ill,
            this.lvhd2_pmv,
            this.lvhd2_ppd,
            this.lvhd2_set,
            this.lvhd2_dtime});
      this.lv_measure.Dock = System.Windows.Forms.DockStyle.Fill;
      this.lv_measure.Location = new System.Drawing.Point(0, 0);
      this.lv_measure.Name = "lv_measure";
      this.lv_measure.Size = new System.Drawing.Size(1866, 397);
      this.lv_measure.TabIndex = 4;
      this.lv_measure.UseCompatibleStateImageBehavior = false;
      this.lv_measure.View = System.Windows.Forms.View.Details;
      this.lv_measure.ColumnClick += new System.Windows.Forms.ColumnClickEventHandler(this.listView_ColumnClick);
      this.lv_measure.SizeChanged += new System.EventHandler(this.listView_SizeChanged);
      // 
      // lvhd2_xbeeID
      // 
      this.lvhd2_xbeeID.Text = "ID";
      this.lvhd2_xbeeID.Width = 100;
      // 
      // lvhd2_name
      // 
      this.lvhd2_name.Text = "Name";
      this.lvhd2_name.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd2_name.Width = 100;
      // 
      // lvhd2_dbt
      // 
      this.lvhd2_dbt.Text = "dbt";
      this.lvhd2_dbt.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd2_dbt.Width = 100;
      // 
      // lvhd2_hmd
      // 
      this.lvhd2_hmd.Text = "hmd";
      this.lvhd2_hmd.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd2_hmd.Width = 100;
      // 
      // lvhd2_glb
      // 
      this.lvhd2_glb.Text = "glb";
      this.lvhd2_glb.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd2_glb.Width = 100;
      // 
      // lvhd2_vel
      // 
      this.lvhd2_vel.Text = "vel";
      this.lvhd2_vel.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd2_vel.Width = 100;
      // 
      // lvhd2_ill
      // 
      this.lvhd2_ill.Text = "illuminance";
      this.lvhd2_ill.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd2_ill.Width = 100;
      // 
      // lvhd2_pmv
      // 
      this.lvhd2_pmv.Text = "pmv";
      this.lvhd2_pmv.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd2_pmv.Width = 100;
      // 
      // lvhd2_ppd
      // 
      this.lvhd2_ppd.Text = "ppd";
      this.lvhd2_ppd.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd2_ppd.Width = 100;
      // 
      // lvhd2_set
      // 
      this.lvhd2_set.Text = "set";
      this.lvhd2_set.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd2_set.Width = 100;
      // 
      // lvhd2_dtime
      // 
      this.lvhd2_dtime.Text = "dtime";
      this.lvhd2_dtime.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.lvhd2_dtime.Width = 120;
      // 
      // tbx_log
      // 
      this.tbx_log.Dock = System.Windows.Forms.DockStyle.Fill;
      this.tbx_log.Location = new System.Drawing.Point(0, 0);
      this.tbx_log.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.tbx_log.Multiline = true;
      this.tbx_log.Name = "tbx_log";
      this.tbx_log.ReadOnly = true;
      this.tbx_log.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
      this.tbx_log.Size = new System.Drawing.Size(1866, 325);
      this.tbx_log.TabIndex = 5;
      // 
      // pnl_settingEdit
      // 
      this.pnl_settingEdit.Controls.Add(this.rbtn_ill);
      this.pnl_settingEdit.Controls.Add(this.rbtn_prox);
      this.pnl_settingEdit.Controls.Add(this.lbl_gpv3);
      this.pnl_settingEdit.Controls.Add(this.lbl_gpv2);
      this.pnl_settingEdit.Controls.Add(this.lbl_gpv1);
      this.pnl_settingEdit.Controls.Add(this.lbl_vel);
      this.pnl_settingEdit.Controls.Add(this.label9);
      this.pnl_settingEdit.Controls.Add(this.lbl_glb);
      this.pnl_settingEdit.Controls.Add(this.label7);
      this.pnl_settingEdit.Controls.Add(this.lbl_th);
      this.pnl_settingEdit.Controls.Add(this.cbx_gpv3Measure);
      this.pnl_settingEdit.Controls.Add(this.label4);
      this.pnl_settingEdit.Controls.Add(this.cbx_gpv2Measure);
      this.pnl_settingEdit.Controls.Add(this.btn_startCollecting);
      this.pnl_settingEdit.Controls.Add(this.tbx_gpv3Interval);
      this.pnl_settingEdit.Controls.Add(this.cbx_gpv1Measure);
      this.pnl_settingEdit.Controls.Add(this.tbx_gpv2Interval);
      this.pnl_settingEdit.Controls.Add(this.label5);
      this.pnl_settingEdit.Controls.Add(this.tbx_gpv1Interval);
      this.pnl_settingEdit.Controls.Add(this.cbx_illMeasure);
      this.pnl_settingEdit.Controls.Add(this.tbx_illInterval);
      this.pnl_settingEdit.Controls.Add(this.lbl_SDTime);
      this.pnl_settingEdit.Controls.Add(this.dtp_timer);
      this.pnl_settingEdit.Controls.Add(this.btn_outputSD);
      this.pnl_settingEdit.Controls.Add(this.btn_setCFactor);
      this.pnl_settingEdit.Controls.Add(this.btn_applySetting);
      this.pnl_settingEdit.Controls.Add(this.label3);
      this.pnl_settingEdit.Controls.Add(this.cbx_thMeasure);
      this.pnl_settingEdit.Controls.Add(this.label2);
      this.pnl_settingEdit.Controls.Add(this.cbx_glbMeasure);
      this.pnl_settingEdit.Controls.Add(this.tbx_glbInterval);
      this.pnl_settingEdit.Controls.Add(this.label1);
      this.pnl_settingEdit.Controls.Add(this.tbx_thInterval);
      this.pnl_settingEdit.Controls.Add(this.cbx_velMeasure);
      this.pnl_settingEdit.Controls.Add(this.tbx_velInterval);
      this.pnl_settingEdit.Dock = System.Windows.Forms.DockStyle.Left;
      this.pnl_settingEdit.Enabled = false;
      this.pnl_settingEdit.Location = new System.Drawing.Point(0, 58);
      this.pnl_settingEdit.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.pnl_settingEdit.Name = "pnl_settingEdit";
      this.pnl_settingEdit.Size = new System.Drawing.Size(508, 1221);
      this.pnl_settingEdit.TabIndex = 2;
      // 
      // rbtn_ill
      // 
      this.rbtn_ill.AutoSize = true;
      this.rbtn_ill.Checked = true;
      this.rbtn_ill.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.rbtn_ill.Location = new System.Drawing.Point(20, 350);
      this.rbtn_ill.Name = "rbtn_ill";
      this.rbtn_ill.Size = new System.Drawing.Size(170, 36);
      this.rbtn_ill.TabIndex = 15;
      this.rbtn_ill.TabStop = true;
      this.rbtn_ill.Text = "Illuminance";
      this.rbtn_ill.UseVisualStyleBackColor = true;
      // 
      // rbtn_prox
      // 
      this.rbtn_prox.AutoSize = true;
      this.rbtn_prox.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.rbtn_prox.Location = new System.Drawing.Point(200, 350);
      this.rbtn_prox.Name = "rbtn_prox";
      this.rbtn_prox.Size = new System.Drawing.Size(148, 36);
      this.rbtn_prox.TabIndex = 14;
      this.rbtn_prox.Text = "Proximity";
      this.rbtn_prox.UseVisualStyleBackColor = true;
      // 
      // lbl_gpv3
      // 
      this.lbl_gpv3.AutoSize = true;
      this.lbl_gpv3.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_gpv3.Location = new System.Drawing.Point(20, 680);
      this.lbl_gpv3.Name = "lbl_gpv3";
      this.lbl_gpv3.Size = new System.Drawing.Size(302, 32);
      this.lbl_gpv3.TabIndex = 13;
      this.lbl_gpv3.Text = "General purpose voltage 3";
      this.lbl_gpv3.Visible = false;
      // 
      // lbl_gpv2
      // 
      this.lbl_gpv2.AutoSize = true;
      this.lbl_gpv2.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_gpv2.Location = new System.Drawing.Point(20, 570);
      this.lbl_gpv2.Name = "lbl_gpv2";
      this.lbl_gpv2.Size = new System.Drawing.Size(302, 32);
      this.lbl_gpv2.TabIndex = 13;
      this.lbl_gpv2.Text = "General purpose voltage 2";
      this.lbl_gpv2.Visible = false;
      // 
      // lbl_gpv1
      // 
      this.lbl_gpv1.AutoSize = true;
      this.lbl_gpv1.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_gpv1.Location = new System.Drawing.Point(20, 460);
      this.lbl_gpv1.Name = "lbl_gpv1";
      this.lbl_gpv1.Size = new System.Drawing.Size(299, 32);
      this.lbl_gpv1.TabIndex = 13;
      this.lbl_gpv1.Text = "General purpose voltage 1";
      // 
      // lbl_vel
      // 
      this.lbl_vel.AutoSize = true;
      this.lbl_vel.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_vel.Location = new System.Drawing.Point(20, 240);
      this.lbl_vel.Name = "lbl_vel";
      this.lbl_vel.Size = new System.Drawing.Size(99, 32);
      this.lbl_vel.TabIndex = 13;
      this.lbl_vel.Text = "Velocity";
      // 
      // label9
      // 
      this.label9.AutoSize = true;
      this.label9.Location = new System.Drawing.Point(325, 734);
      this.label9.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      this.label9.Name = "label9";
      this.label9.Size = new System.Drawing.Size(48, 32);
      this.label9.TabIndex = 11;
      this.label9.Text = "sec";
      this.label9.Visible = false;
      // 
      // lbl_glb
      // 
      this.lbl_glb.AutoSize = true;
      this.lbl_glb.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_glb.Location = new System.Drawing.Point(20, 130);
      this.lbl_glb.Name = "lbl_glb";
      this.lbl_glb.Size = new System.Drawing.Size(222, 32);
      this.lbl_glb.TabIndex = 13;
      this.lbl_glb.Text = "Globe temperature";
      // 
      // label7
      // 
      this.label7.AutoSize = true;
      this.label7.Location = new System.Drawing.Point(325, 624);
      this.label7.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      this.label7.Name = "label7";
      this.label7.Size = new System.Drawing.Size(48, 32);
      this.label7.TabIndex = 11;
      this.label7.Text = "sec";
      this.label7.Visible = false;
      // 
      // lbl_th
      // 
      this.lbl_th.AutoSize = true;
      this.lbl_th.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_th.Location = new System.Drawing.Point(20, 20);
      this.lbl_th.Name = "lbl_th";
      this.lbl_th.Size = new System.Drawing.Size(468, 32);
      this.lbl_th.TabIndex = 12;
      this.lbl_th.Text = "Dry-bulb temperature / Relative humdiity";
      // 
      // cbx_gpv3Measure
      // 
      this.cbx_gpv3Measure.AutoSize = true;
      this.cbx_gpv3Measure.Location = new System.Drawing.Point(60, 730);
      this.cbx_gpv3Measure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.cbx_gpv3Measure.Name = "cbx_gpv3Measure";
      this.cbx_gpv3Measure.Size = new System.Drawing.Size(138, 36);
      this.cbx_gpv3Measure.TabIndex = 7;
      this.cbx_gpv3Measure.TabStop = false;
      this.cbx_gpv3Measure.Text = "Measure";
      this.cbx_gpv3Measure.UseVisualStyleBackColor = true;
      this.cbx_gpv3Measure.Visible = false;
      this.cbx_gpv3Measure.CheckedChanged += new System.EventHandler(this.cbx_measure_CheckedChanged);
      // 
      // label4
      // 
      this.label4.AutoSize = true;
      this.label4.Location = new System.Drawing.Point(325, 514);
      this.label4.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      this.label4.Name = "label4";
      this.label4.Size = new System.Drawing.Size(48, 32);
      this.label4.TabIndex = 11;
      this.label4.Text = "sec";
      // 
      // cbx_gpv2Measure
      // 
      this.cbx_gpv2Measure.AutoSize = true;
      this.cbx_gpv2Measure.Location = new System.Drawing.Point(60, 620);
      this.cbx_gpv2Measure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.cbx_gpv2Measure.Name = "cbx_gpv2Measure";
      this.cbx_gpv2Measure.Size = new System.Drawing.Size(138, 36);
      this.cbx_gpv2Measure.TabIndex = 7;
      this.cbx_gpv2Measure.TabStop = false;
      this.cbx_gpv2Measure.Text = "Measure";
      this.cbx_gpv2Measure.UseVisualStyleBackColor = true;
      this.cbx_gpv2Measure.Visible = false;
      this.cbx_gpv2Measure.CheckedChanged += new System.EventHandler(this.cbx_measure_CheckedChanged);
      // 
      // btn_startCollecting
      // 
      this.btn_startCollecting.Image = ((System.Drawing.Image)(resources.GetObject("btn_startCollecting.Image")));
      this.btn_startCollecting.Location = new System.Drawing.Point(20, 987);
      this.btn_startCollecting.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.btn_startCollecting.Name = "btn_startCollecting";
      this.btn_startCollecting.Size = new System.Drawing.Size(468, 55);
      this.btn_startCollecting.TabIndex = 10;
      this.btn_startCollecting.Text = "Start data collecting";
      this.btn_startCollecting.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      this.btn_startCollecting.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
      this.btn_startCollecting.UseVisualStyleBackColor = true;
      this.btn_startCollecting.Click += new System.EventHandler(this.btn_startMLogger_Click);
      // 
      // tbx_gpv3Interval
      // 
      this.tbx_gpv3Interval.Location = new System.Drawing.Point(227, 728);
      this.tbx_gpv3Interval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.tbx_gpv3Interval.Name = "tbx_gpv3Interval";
      this.tbx_gpv3Interval.Size = new System.Drawing.Size(80, 39);
      this.tbx_gpv3Interval.TabIndex = 6;
      this.tbx_gpv3Interval.Text = "60";
      this.tbx_gpv3Interval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      this.tbx_gpv3Interval.Visible = false;
      // 
      // cbx_gpv1Measure
      // 
      this.cbx_gpv1Measure.AutoSize = true;
      this.cbx_gpv1Measure.Location = new System.Drawing.Point(60, 510);
      this.cbx_gpv1Measure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.cbx_gpv1Measure.Name = "cbx_gpv1Measure";
      this.cbx_gpv1Measure.Size = new System.Drawing.Size(138, 36);
      this.cbx_gpv1Measure.TabIndex = 7;
      this.cbx_gpv1Measure.TabStop = false;
      this.cbx_gpv1Measure.Text = "Measure";
      this.cbx_gpv1Measure.UseVisualStyleBackColor = true;
      this.cbx_gpv1Measure.CheckedChanged += new System.EventHandler(this.cbx_measure_CheckedChanged);
      // 
      // tbx_gpv2Interval
      // 
      this.tbx_gpv2Interval.Location = new System.Drawing.Point(227, 618);
      this.tbx_gpv2Interval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.tbx_gpv2Interval.Name = "tbx_gpv2Interval";
      this.tbx_gpv2Interval.Size = new System.Drawing.Size(80, 39);
      this.tbx_gpv2Interval.TabIndex = 5;
      this.tbx_gpv2Interval.Text = "60";
      this.tbx_gpv2Interval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      this.tbx_gpv2Interval.Visible = false;
      // 
      // label5
      // 
      this.label5.AutoSize = true;
      this.label5.Location = new System.Drawing.Point(325, 404);
      this.label5.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      this.label5.Name = "label5";
      this.label5.Size = new System.Drawing.Size(48, 32);
      this.label5.TabIndex = 11;
      this.label5.Text = "sec";
      // 
      // tbx_gpv1Interval
      // 
      this.tbx_gpv1Interval.Location = new System.Drawing.Point(227, 508);
      this.tbx_gpv1Interval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.tbx_gpv1Interval.Name = "tbx_gpv1Interval";
      this.tbx_gpv1Interval.Size = new System.Drawing.Size(80, 39);
      this.tbx_gpv1Interval.TabIndex = 4;
      this.tbx_gpv1Interval.Text = "60";
      this.tbx_gpv1Interval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      // 
      // cbx_illMeasure
      // 
      this.cbx_illMeasure.AutoSize = true;
      this.cbx_illMeasure.Location = new System.Drawing.Point(60, 400);
      this.cbx_illMeasure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.cbx_illMeasure.Name = "cbx_illMeasure";
      this.cbx_illMeasure.Size = new System.Drawing.Size(138, 36);
      this.cbx_illMeasure.TabIndex = 7;
      this.cbx_illMeasure.TabStop = false;
      this.cbx_illMeasure.Text = "Measure";
      this.cbx_illMeasure.UseVisualStyleBackColor = true;
      this.cbx_illMeasure.CheckedChanged += new System.EventHandler(this.cbx_measure_CheckedChanged);
      // 
      // tbx_illInterval
      // 
      this.tbx_illInterval.Location = new System.Drawing.Point(227, 398);
      this.tbx_illInterval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.tbx_illInterval.Name = "tbx_illInterval";
      this.tbx_illInterval.Size = new System.Drawing.Size(80, 39);
      this.tbx_illInterval.TabIndex = 3;
      this.tbx_illInterval.Text = "60";
      this.tbx_illInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      // 
      // lbl_SDTime
      // 
      this.lbl_SDTime.AutoSize = true;
      this.lbl_SDTime.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_SDTime.Location = new System.Drawing.Point(20, 807);
      this.lbl_SDTime.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      this.lbl_SDTime.Name = "lbl_SDTime";
      this.lbl_SDTime.Size = new System.Drawing.Size(368, 32);
      this.lbl_SDTime.TabIndex = 8;
      this.lbl_SDTime.Text = "Mesaurement starting date time";
      // 
      // dtp_timer
      // 
      this.dtp_timer.CustomFormat = "yyyy/MM/dd HH:mm";
      this.dtp_timer.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
      this.dtp_timer.Location = new System.Drawing.Point(60, 857);
      this.dtp_timer.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.dtp_timer.Name = "dtp_timer";
      this.dtp_timer.Size = new System.Drawing.Size(428, 39);
      this.dtp_timer.TabIndex = 8;
      this.dtp_timer.TabStop = false;
      this.dtp_timer.Value = new System.DateTime(2000, 1, 1, 0, 0, 0, 0);
      // 
      // btn_outputSD
      // 
      this.btn_outputSD.Image = global::MLServer.Properties.Resources.sd;
      this.btn_outputSD.Location = new System.Drawing.Point(20, 1060);
      this.btn_outputSD.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.btn_outputSD.Name = "btn_outputSD";
      this.btn_outputSD.Size = new System.Drawing.Size(468, 55);
      this.btn_outputSD.TabIndex = 9;
      this.btn_outputSD.Text = "Start logging (SD card)";
      this.btn_outputSD.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      this.btn_outputSD.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
      this.btn_outputSD.UseVisualStyleBackColor = true;
      this.btn_outputSD.Click += new System.EventHandler(this.btn_outputSD_Click);
      // 
      // btn_setCFactor
      // 
      this.btn_setCFactor.Image = global::MLServer.Properties.Resources.ram;
      this.btn_setCFactor.Location = new System.Drawing.Point(20, 1133);
      this.btn_setCFactor.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.btn_setCFactor.Name = "btn_setCFactor";
      this.btn_setCFactor.Size = new System.Drawing.Size(468, 55);
      this.btn_setCFactor.TabIndex = 9;
      this.btn_setCFactor.Text = "Set correction factors";
      this.btn_setCFactor.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      this.btn_setCFactor.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
      this.btn_setCFactor.UseVisualStyleBackColor = true;
      this.btn_setCFactor.Click += new System.EventHandler(this.btn_setCFactor_Click);
      // 
      // btn_applySetting
      // 
      this.btn_applySetting.Image = ((System.Drawing.Image)(resources.GetObject("btn_applySetting.Image")));
      this.btn_applySetting.Location = new System.Drawing.Point(20, 914);
      this.btn_applySetting.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.btn_applySetting.Name = "btn_applySetting";
      this.btn_applySetting.Size = new System.Drawing.Size(468, 55);
      this.btn_applySetting.TabIndex = 9;
      this.btn_applySetting.Text = "Apply setting";
      this.btn_applySetting.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
      this.btn_applySetting.TextImageRelation = System.Windows.Forms.TextImageRelation.ImageBeforeText;
      this.btn_applySetting.UseVisualStyleBackColor = true;
      this.btn_applySetting.Click += new System.EventHandler(this.btn_updateMSetting_Click);
      // 
      // label3
      // 
      this.label3.AutoSize = true;
      this.label3.Location = new System.Drawing.Point(325, 294);
      this.label3.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      this.label3.Name = "label3";
      this.label3.Size = new System.Drawing.Size(48, 32);
      this.label3.TabIndex = 5;
      this.label3.Text = "sec";
      // 
      // cbx_thMeasure
      // 
      this.cbx_thMeasure.AutoSize = true;
      this.cbx_thMeasure.Location = new System.Drawing.Point(60, 70);
      this.cbx_thMeasure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.cbx_thMeasure.Name = "cbx_thMeasure";
      this.cbx_thMeasure.Size = new System.Drawing.Size(138, 36);
      this.cbx_thMeasure.TabIndex = 4;
      this.cbx_thMeasure.TabStop = false;
      this.cbx_thMeasure.Text = "Measure";
      this.cbx_thMeasure.UseVisualStyleBackColor = true;
      this.cbx_thMeasure.CheckedChanged += new System.EventHandler(this.cbx_measure_CheckedChanged);
      // 
      // label2
      // 
      this.label2.AutoSize = true;
      this.label2.Location = new System.Drawing.Point(325, 185);
      this.label2.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      this.label2.Name = "label2";
      this.label2.Size = new System.Drawing.Size(48, 32);
      this.label2.TabIndex = 5;
      this.label2.Text = "sec";
      // 
      // cbx_glbMeasure
      // 
      this.cbx_glbMeasure.AutoSize = true;
      this.cbx_glbMeasure.Location = new System.Drawing.Point(60, 180);
      this.cbx_glbMeasure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.cbx_glbMeasure.Name = "cbx_glbMeasure";
      this.cbx_glbMeasure.Size = new System.Drawing.Size(138, 36);
      this.cbx_glbMeasure.TabIndex = 5;
      this.cbx_glbMeasure.TabStop = false;
      this.cbx_glbMeasure.Text = "Measure";
      this.cbx_glbMeasure.UseVisualStyleBackColor = true;
      this.cbx_glbMeasure.CheckedChanged += new System.EventHandler(this.cbx_measure_CheckedChanged);
      // 
      // tbx_glbInterval
      // 
      this.tbx_glbInterval.Location = new System.Drawing.Point(227, 178);
      this.tbx_glbInterval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.tbx_glbInterval.Name = "tbx_glbInterval";
      this.tbx_glbInterval.Size = new System.Drawing.Size(80, 39);
      this.tbx_glbInterval.TabIndex = 1;
      this.tbx_glbInterval.Text = "60";
      this.tbx_glbInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      // 
      // label1
      // 
      this.label1.AutoSize = true;
      this.label1.Location = new System.Drawing.Point(321, 71);
      this.label1.Margin = new System.Windows.Forms.Padding(7, 0, 7, 0);
      this.label1.Name = "label1";
      this.label1.Size = new System.Drawing.Size(48, 32);
      this.label1.TabIndex = 5;
      this.label1.Text = "sec";
      // 
      // tbx_thInterval
      // 
      this.tbx_thInterval.Location = new System.Drawing.Point(227, 68);
      this.tbx_thInterval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.tbx_thInterval.Name = "tbx_thInterval";
      this.tbx_thInterval.Size = new System.Drawing.Size(80, 39);
      this.tbx_thInterval.TabIndex = 0;
      this.tbx_thInterval.Text = "60";
      this.tbx_thInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      // 
      // cbx_velMeasure
      // 
      this.cbx_velMeasure.AutoSize = true;
      this.cbx_velMeasure.Location = new System.Drawing.Point(60, 290);
      this.cbx_velMeasure.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.cbx_velMeasure.Name = "cbx_velMeasure";
      this.cbx_velMeasure.Size = new System.Drawing.Size(138, 36);
      this.cbx_velMeasure.TabIndex = 6;
      this.cbx_velMeasure.TabStop = false;
      this.cbx_velMeasure.Text = "Measure";
      this.cbx_velMeasure.UseVisualStyleBackColor = true;
      this.cbx_velMeasure.CheckedChanged += new System.EventHandler(this.cbx_measure_CheckedChanged);
      // 
      // tbx_velInterval
      // 
      this.tbx_velInterval.Location = new System.Drawing.Point(227, 288);
      this.tbx_velInterval.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.tbx_velInterval.Name = "tbx_velInterval";
      this.tbx_velInterval.Size = new System.Drawing.Size(80, 39);
      this.tbx_velInterval.TabIndex = 2;
      this.tbx_velInterval.Text = "60";
      this.tbx_velInterval.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
      // 
      // MainForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(13F, 32F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(2374, 1279);
      this.Controls.Add(this.splitContainer1);
      this.Controls.Add(this.pnl_settingEdit);
      this.Controls.Add(this.toolStrip);
      this.Margin = new System.Windows.Forms.Padding(7, 9, 7, 9);
      this.MinimumSize = new System.Drawing.Size(2400, 1350);
      this.Name = "MainForm";
      this.ShowIcon = false;
      this.Text = "MLServer version 1.0.8";
      this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
      this.toolStrip.ResumeLayout(false);
      this.toolStrip.PerformLayout();
      this.splitContainer1.Panel1.ResumeLayout(false);
      this.splitContainer1.Panel2.ResumeLayout(false);
      this.splitContainer1.Panel2.PerformLayout();
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
      this.splitContainer1.ResumeLayout(false);
      this.splitContainer2.Panel1.ResumeLayout(false);
      this.splitContainer2.Panel2.ResumeLayout(false);
      ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
      this.splitContainer2.ResumeLayout(false);
      this.pnl_settingEdit.ResumeLayout(false);
      this.pnl_settingEdit.PerformLayout();
      this.ResumeLayout(false);
      this.PerformLayout();

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
  }
}

