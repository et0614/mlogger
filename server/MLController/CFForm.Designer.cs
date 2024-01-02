namespace MLController
{
  partial class CFForm
  {
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
      if (disposing && (components != null))
      {
        components.Dispose();
      }
      base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
      this.lbl_dbt = new System.Windows.Forms.Label();
      this.lbl_cv1 = new System.Windows.Forms.Label();
      this.cA_dbt = new System.Windows.Forms.TextBox();
      this.lbl_mv1 = new System.Windows.Forms.Label();
      this.cB_dbt = new System.Windows.Forms.TextBox();
      this.lbl_hmd = new System.Windows.Forms.Label();
      this.lbl_cv2 = new System.Windows.Forms.Label();
      this.lbl_mv2 = new System.Windows.Forms.Label();
      this.cA_hmd = new System.Windows.Forms.TextBox();
      this.cB_hmd = new System.Windows.Forms.TextBox();
      this.lbl_glb = new System.Windows.Forms.Label();
      this.lbl_vel = new System.Windows.Forms.Label();
      this.lbl_cv3 = new System.Windows.Forms.Label();
      this.lbl_cv4 = new System.Windows.Forms.Label();
      this.lbl_mv3 = new System.Windows.Forms.Label();
      this.lbl_mv4 = new System.Windows.Forms.Label();
      this.cA_glb = new System.Windows.Forms.TextBox();
      this.cA_vel = new System.Windows.Forms.TextBox();
      this.cB_glb = new System.Windows.Forms.TextBox();
      this.cB_vel = new System.Windows.Forms.TextBox();
      this.lbl_bVoltage = new System.Windows.Forms.Label();
      this.vel_0V = new System.Windows.Forms.TextBox();
      this.lbl_ill = new System.Windows.Forms.Label();
      this.lbl_cv5 = new System.Windows.Forms.Label();
      this.lbl_mv5 = new System.Windows.Forms.Label();
      this.cA_lux = new System.Windows.Forms.TextBox();
      this.cB_lux = new System.Windows.Forms.TextBox();
      this.btnSet = new System.Windows.Forms.Button();
      this.btnClose = new System.Windows.Forms.Button();
      this.SuspendLayout();
      // 
      // lbl_dbt
      // 
      this.lbl_dbt.AutoSize = true;
      this.lbl_dbt.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_dbt.Location = new System.Drawing.Point(12, 20);
      this.lbl_dbt.Name = "lbl_dbt";
      this.lbl_dbt.Size = new System.Drawing.Size(254, 32);
      this.lbl_dbt.TabIndex = 0;
      this.lbl_dbt.Text = "Dry-bulb temperature";
      // 
      // lbl_cv1
      // 
      this.lbl_cv1.AutoSize = true;
      this.lbl_cv1.Location = new System.Drawing.Point(33, 68);
      this.lbl_cv1.Name = "lbl_cv1";
      this.lbl_cv1.Size = new System.Drawing.Size(145, 32);
      this.lbl_cv1.TabIndex = 0;
      this.lbl_cv1.Text = "corr. value =";
      // 
      // cA_dbt
      // 
      this.cA_dbt.Location = new System.Drawing.Point(184, 65);
      this.cA_dbt.Name = "cA_dbt";
      this.cA_dbt.Size = new System.Drawing.Size(103, 39);
      this.cA_dbt.TabIndex = 1;
      this.cA_dbt.Text = "1.000";
      this.cA_dbt.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.cA_dbt.TextChanged += new System.EventHandler(this.cf_TextChanged);
      // 
      // lbl_mv1
      // 
      this.lbl_mv1.AutoSize = true;
      this.lbl_mv1.Location = new System.Drawing.Point(293, 68);
      this.lbl_mv1.Name = "lbl_mv1";
      this.lbl_mv1.Size = new System.Drawing.Size(179, 32);
      this.lbl_mv1.TabIndex = 0;
      this.lbl_mv1.Text = "* meas. value +";
      this.lbl_mv1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
      // 
      // cB_dbt
      // 
      this.cB_dbt.Location = new System.Drawing.Point(492, 65);
      this.cB_dbt.Name = "cB_dbt";
      this.cB_dbt.Size = new System.Drawing.Size(103, 39);
      this.cB_dbt.TabIndex = 2;
      this.cB_dbt.Text = "0.000";
      this.cB_dbt.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.cB_dbt.TextChanged += new System.EventHandler(this.cf_TextChanged);
      // 
      // lbl_hmd
      // 
      this.lbl_hmd.AutoSize = true;
      this.lbl_hmd.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_hmd.Location = new System.Drawing.Point(12, 136);
      this.lbl_hmd.Name = "lbl_hmd";
      this.lbl_hmd.Size = new System.Drawing.Size(110, 32);
      this.lbl_hmd.TabIndex = 0;
      this.lbl_hmd.Text = "相対湿度";
      // 
      // lbl_cv2
      // 
      this.lbl_cv2.AutoSize = true;
      this.lbl_cv2.Location = new System.Drawing.Point(33, 184);
      this.lbl_cv2.Name = "lbl_cv2";
      this.lbl_cv2.Size = new System.Drawing.Size(102, 32);
      this.lbl_cv2.TabIndex = 0;
      this.lbl_cv2.Text = "補正値=";
      // 
      // lbl_mv2
      // 
      this.lbl_mv2.AutoSize = true;
      this.lbl_mv2.Location = new System.Drawing.Point(293, 184);
      this.lbl_mv2.Name = "lbl_mv2";
      this.lbl_mv2.Size = new System.Drawing.Size(126, 32);
      this.lbl_mv2.TabIndex = 0;
      this.lbl_mv2.Text = "* 実測値 +";
      this.lbl_mv2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
      // 
      // cA_hmd
      // 
      this.cA_hmd.Location = new System.Drawing.Point(184, 181);
      this.cA_hmd.Name = "cA_hmd";
      this.cA_hmd.Size = new System.Drawing.Size(103, 39);
      this.cA_hmd.TabIndex = 3;
      this.cA_hmd.Text = "1.000";
      this.cA_hmd.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.cA_hmd.TextChanged += new System.EventHandler(this.cf_TextChanged);
      // 
      // cB_hmd
      // 
      this.cB_hmd.Location = new System.Drawing.Point(492, 181);
      this.cB_hmd.Name = "cB_hmd";
      this.cB_hmd.Size = new System.Drawing.Size(103, 39);
      this.cB_hmd.TabIndex = 4;
      this.cB_hmd.Text = "0.000";
      this.cB_hmd.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.cB_hmd.TextChanged += new System.EventHandler(this.cf_TextChanged);
      // 
      // lbl_glb
      // 
      this.lbl_glb.AutoSize = true;
      this.lbl_glb.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_glb.Location = new System.Drawing.Point(12, 248);
      this.lbl_glb.Name = "lbl_glb";
      this.lbl_glb.Size = new System.Drawing.Size(133, 32);
      this.lbl_glb.TabIndex = 0;
      this.lbl_glb.Text = "グローブ温度";
      // 
      // lbl_vel
      // 
      this.lbl_vel.AutoSize = true;
      this.lbl_vel.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_vel.Location = new System.Drawing.Point(12, 364);
      this.lbl_vel.Name = "lbl_vel";
      this.lbl_vel.Size = new System.Drawing.Size(62, 32);
      this.lbl_vel.TabIndex = 0;
      this.lbl_vel.Text = "風速";
      // 
      // lbl_cv3
      // 
      this.lbl_cv3.AutoSize = true;
      this.lbl_cv3.Location = new System.Drawing.Point(33, 296);
      this.lbl_cv3.Name = "lbl_cv3";
      this.lbl_cv3.Size = new System.Drawing.Size(102, 32);
      this.lbl_cv3.TabIndex = 0;
      this.lbl_cv3.Text = "補正値=";
      // 
      // lbl_cv4
      // 
      this.lbl_cv4.AutoSize = true;
      this.lbl_cv4.Location = new System.Drawing.Point(33, 412);
      this.lbl_cv4.Name = "lbl_cv4";
      this.lbl_cv4.Size = new System.Drawing.Size(102, 32);
      this.lbl_cv4.TabIndex = 0;
      this.lbl_cv4.Text = "補正値=";
      // 
      // lbl_mv3
      // 
      this.lbl_mv3.AutoSize = true;
      this.lbl_mv3.Location = new System.Drawing.Point(293, 296);
      this.lbl_mv3.Name = "lbl_mv3";
      this.lbl_mv3.Size = new System.Drawing.Size(141, 32);
      this.lbl_mv3.TabIndex = 0;
      this.lbl_mv3.Text = " x 実測値 + ";
      this.lbl_mv3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
      // 
      // lbl_mv4
      // 
      this.lbl_mv4.AutoSize = true;
      this.lbl_mv4.Location = new System.Drawing.Point(293, 412);
      this.lbl_mv4.Name = "lbl_mv4";
      this.lbl_mv4.Size = new System.Drawing.Size(141, 32);
      this.lbl_mv4.TabIndex = 0;
      this.lbl_mv4.Text = " x 実測値 + ";
      this.lbl_mv4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
      // 
      // cA_glb
      // 
      this.cA_glb.Location = new System.Drawing.Point(184, 293);
      this.cA_glb.Name = "cA_glb";
      this.cA_glb.Size = new System.Drawing.Size(103, 39);
      this.cA_glb.TabIndex = 5;
      this.cA_glb.Text = "1.000";
      this.cA_glb.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.cA_glb.TextChanged += new System.EventHandler(this.cf_TextChanged);
      // 
      // cA_vel
      // 
      this.cA_vel.Location = new System.Drawing.Point(184, 409);
      this.cA_vel.Name = "cA_vel";
      this.cA_vel.Size = new System.Drawing.Size(103, 39);
      this.cA_vel.TabIndex = 7;
      this.cA_vel.Text = "1.000";
      this.cA_vel.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.cA_vel.TextChanged += new System.EventHandler(this.cf_TextChanged);
      // 
      // cB_glb
      // 
      this.cB_glb.Location = new System.Drawing.Point(492, 293);
      this.cB_glb.Name = "cB_glb";
      this.cB_glb.Size = new System.Drawing.Size(103, 39);
      this.cB_glb.TabIndex = 6;
      this.cB_glb.Text = "0.000";
      this.cB_glb.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.cB_glb.TextChanged += new System.EventHandler(this.cf_TextChanged);
      // 
      // cB_vel
      // 
      this.cB_vel.Location = new System.Drawing.Point(492, 409);
      this.cB_vel.Name = "cB_vel";
      this.cB_vel.Size = new System.Drawing.Size(103, 39);
      this.cB_vel.TabIndex = 8;
      this.cB_vel.Text = "0.000";
      this.cB_vel.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.cB_vel.TextChanged += new System.EventHandler(this.cf_TextChanged);
      // 
      // lbl_bVoltage
      // 
      this.lbl_bVoltage.AutoSize = true;
      this.lbl_bVoltage.Location = new System.Drawing.Point(33, 463);
      this.lbl_bVoltage.Name = "lbl_bVoltage";
      this.lbl_bVoltage.Size = new System.Drawing.Size(120, 32);
      this.lbl_bVoltage.TabIndex = 0;
      this.lbl_bVoltage.Text = "Base volt.:";
      // 
      // vel_0V
      // 
      this.vel_0V.Location = new System.Drawing.Point(184, 460);
      this.vel_0V.Name = "vel_0V";
      this.vel_0V.Size = new System.Drawing.Size(103, 39);
      this.vel_0V.TabIndex = 9;
      this.vel_0V.Text = "1.450";
      this.vel_0V.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.vel_0V.TextChanged += new System.EventHandler(this.cf_TextChanged);
      // 
      // lbl_ill
      // 
      this.lbl_ill.AutoSize = true;
      this.lbl_ill.Font = new System.Drawing.Font("Yu Gothic UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
      this.lbl_ill.Location = new System.Drawing.Point(12, 532);
      this.lbl_ill.Name = "lbl_ill";
      this.lbl_ill.Size = new System.Drawing.Size(62, 32);
      this.lbl_ill.TabIndex = 0;
      this.lbl_ill.Text = "照度";
      // 
      // lbl_cv5
      // 
      this.lbl_cv5.AutoSize = true;
      this.lbl_cv5.Location = new System.Drawing.Point(33, 580);
      this.lbl_cv5.Name = "lbl_cv5";
      this.lbl_cv5.Size = new System.Drawing.Size(102, 32);
      this.lbl_cv5.TabIndex = 0;
      this.lbl_cv5.Text = "補正値=";
      // 
      // lbl_mv5
      // 
      this.lbl_mv5.AutoSize = true;
      this.lbl_mv5.Location = new System.Drawing.Point(293, 580);
      this.lbl_mv5.Name = "lbl_mv5";
      this.lbl_mv5.Size = new System.Drawing.Size(141, 32);
      this.lbl_mv5.TabIndex = 0;
      this.lbl_mv5.Text = " x 実測値 + ";
      this.lbl_mv5.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
      // 
      // cA_lux
      // 
      this.cA_lux.Location = new System.Drawing.Point(184, 577);
      this.cA_lux.Name = "cA_lux";
      this.cA_lux.Size = new System.Drawing.Size(103, 39);
      this.cA_lux.TabIndex = 10;
      this.cA_lux.Text = "1.000";
      this.cA_lux.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.cA_lux.TextChanged += new System.EventHandler(this.cf_TextChanged);
      // 
      // cB_lux
      // 
      this.cB_lux.Location = new System.Drawing.Point(492, 577);
      this.cB_lux.Name = "cB_lux";
      this.cB_lux.Size = new System.Drawing.Size(103, 39);
      this.cB_lux.TabIndex = 11;
      this.cB_lux.Text = "0.000";
      this.cB_lux.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
      this.cB_lux.TextChanged += new System.EventHandler(this.cf_TextChanged);
      // 
      // btnSet
      // 
      this.btnSet.Location = new System.Drawing.Point(299, 656);
      this.btnSet.Name = "btnSet";
      this.btnSet.Size = new System.Drawing.Size(145, 49);
      this.btnSet.TabIndex = 12;
      this.btnSet.Text = "設定";
      this.btnSet.UseVisualStyleBackColor = true;
      this.btnSet.Click += new System.EventHandler(this.btnSet_Click);
      // 
      // btnClose
      // 
      this.btnClose.Location = new System.Drawing.Point(450, 656);
      this.btnClose.Name = "btnClose";
      this.btnClose.Size = new System.Drawing.Size(145, 49);
      this.btnClose.TabIndex = 13;
      this.btnClose.Text = "閉じる";
      this.btnClose.UseVisualStyleBackColor = true;
      this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
      // 
      // CFForm
      // 
      this.AutoScaleDimensions = new System.Drawing.SizeF(13F, 32F);
      this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
      this.ClientSize = new System.Drawing.Size(637, 726);
      this.Controls.Add(this.btnClose);
      this.Controls.Add(this.btnSet);
      this.Controls.Add(this.cB_vel);
      this.Controls.Add(this.cB_lux);
      this.Controls.Add(this.cB_glb);
      this.Controls.Add(this.cB_hmd);
      this.Controls.Add(this.vel_0V);
      this.Controls.Add(this.cA_vel);
      this.Controls.Add(this.cB_dbt);
      this.Controls.Add(this.cA_lux);
      this.Controls.Add(this.cA_glb);
      this.Controls.Add(this.cA_hmd);
      this.Controls.Add(this.lbl_mv4);
      this.Controls.Add(this.cA_dbt);
      this.Controls.Add(this.lbl_mv5);
      this.Controls.Add(this.lbl_mv3);
      this.Controls.Add(this.lbl_bVoltage);
      this.Controls.Add(this.lbl_mv2);
      this.Controls.Add(this.lbl_cv4);
      this.Controls.Add(this.lbl_mv1);
      this.Controls.Add(this.lbl_cv5);
      this.Controls.Add(this.lbl_cv3);
      this.Controls.Add(this.lbl_cv2);
      this.Controls.Add(this.lbl_vel);
      this.Controls.Add(this.lbl_cv1);
      this.Controls.Add(this.lbl_ill);
      this.Controls.Add(this.lbl_glb);
      this.Controls.Add(this.lbl_hmd);
      this.Controls.Add(this.lbl_dbt);
      this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
      this.MaximizeBox = false;
      this.MinimizeBox = false;
      this.Name = "CFForm";
      this.ShowIcon = false;
      this.Text = "補正係数";
      this.ResumeLayout(false);
      this.PerformLayout();

    }

    #endregion

    private System.Windows.Forms.Label lbl_dbt;
    private System.Windows.Forms.Label lbl_cv1;
    private System.Windows.Forms.TextBox cA_dbt;
    private System.Windows.Forms.Label lbl_mv1;
    private System.Windows.Forms.TextBox cB_dbt;
    private System.Windows.Forms.Label lbl_hmd;
    private System.Windows.Forms.Label lbl_cv2;
    private System.Windows.Forms.Label lbl_mv2;
    private System.Windows.Forms.TextBox cA_hmd;
    private System.Windows.Forms.TextBox cB_hmd;
    private System.Windows.Forms.Label lbl_glb;
    private System.Windows.Forms.Label lbl_vel;
    private System.Windows.Forms.Label lbl_cv3;
    private System.Windows.Forms.Label lbl_cv4;
    private System.Windows.Forms.Label lbl_mv3;
    private System.Windows.Forms.Label lbl_mv4;
    private System.Windows.Forms.TextBox cA_glb;
    private System.Windows.Forms.TextBox cA_vel;
    private System.Windows.Forms.TextBox cB_glb;
    private System.Windows.Forms.TextBox cB_vel;
    private System.Windows.Forms.Label lbl_bVoltage;
    private System.Windows.Forms.TextBox vel_0V;
    private System.Windows.Forms.Label lbl_ill;
    private System.Windows.Forms.Label lbl_cv5;
    private System.Windows.Forms.Label lbl_mv5;
    private System.Windows.Forms.TextBox cA_lux;
    private System.Windows.Forms.TextBox cB_lux;
    private System.Windows.Forms.Button btnSet;
    private System.Windows.Forms.Button btnClose;
  }
}