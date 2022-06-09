using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using MLLib;

namespace MLServer
{
  public partial class CFForm : Form
  {

    public delegate void SendMessageDelegate(string longAddress, string msg);

    /// <summary>MLoggerを設定・取得する</summary>
    public MLogger Logger { get; set; }

    /// <summary></summary>
    public SendMessageDelegate SendMessageFnc { get; set; }

    /// <summary>編集済か否か</summary>
    private bool isEdited = false;

    public CFForm()
    {
      InitializeComponent();

      //国際化対応処理
      initControlLang();
    }

    private void initControlLang()
    {
      this.Text = i18n.Resources.CFF_Title;

      lbl_dbt.Text = i18n.Resources.DBTemp;
      lbl_hmd.Text = i18n.Resources.RHumid;
      lbl_glb.Text = i18n.Resources.GlbTemp;
      lbl_vel.Text = i18n.Resources.Velocity;
      lbl_ill.Text = i18n.Resources.Illuminance;

      btnClose.Text = i18n.Resources.Close;
      btnSet.Text = i18n.Resources.Set;

      lbl_cv1.Text = lbl_cv2.Text = lbl_cv3.Text = lbl_cv4.Text = lbl_cv5.Text = i18n.Resources.CFF_CValue;
      lbl_mv1.Text = lbl_mv2.Text = lbl_mv3.Text = lbl_mv4.Text = lbl_mv5.Text = i18n.Resources.CFF_MValue;
      lbl_bVoltage.Text = i18n.Resources.CFF_Bvoltage;
    }

    public void UpdateCFactors()
    {
      cA_dbt.Text = Logger.DrybulbTemperature.CorrectionFactorA.ToString("F3");
      cB_dbt.Text = Logger.DrybulbTemperature.CorrectionFactorB.ToString("F2");

      cA_hmd.Text = Logger.RelativeHumdity.CorrectionFactorA.ToString("F3");
      cB_hmd.Text = Logger.RelativeHumdity.CorrectionFactorB.ToString("F2");

      cA_glb.Text = Logger.GlobeTemperature.CorrectionFactorA.ToString("F3");
      cB_glb.Text = Logger.GlobeTemperature.CorrectionFactorB.ToString("F2");

      cA_vel.Text = Logger.Velocity.CorrectionFactorA.ToString("F3");
      cB_vel.Text = Logger.Velocity.CorrectionFactorB.ToString("F3");
      vel_0V.Text = Logger.VelocityMinVoltage.ToString("F3");

      cA_lux.Text = Logger.Illuminance.CorrectionFactorA.ToString("F3");
      cB_lux.Text = Logger.Illuminance.CorrectionFactorB.ToString("F0");

      isEdited = false;
      resetTextBoxColor();
    }

    private void cf_TextChanged(object sender, EventArgs e)
    {
      isEdited = true;

      TextBox tbx = (TextBox)sender;
      tbx.ForeColor = Color.Red;
    }

    private void resetTextBoxColor()
    {
      cA_dbt.ForeColor = cA_hmd.ForeColor = cA_glb.ForeColor = cA_vel.ForeColor = cA_lux.ForeColor =
        cB_dbt.ForeColor = cB_hmd.ForeColor = cB_glb.ForeColor = cB_vel.ForeColor = cB_lux.ForeColor =
        vel_0V.ForeColor = Color.Black;
    }

    private void btnClose_Click(object sender, EventArgs e)
    {
      this.Close();
    }

    private void btnSet_Click(object sender, EventArgs e)
    {
      if (!isEdited) return;

      //値が適正か、確認する
      bool hasError = false;
      string errMsg = "";

      if (!double.TryParse(cA_dbt.Text, out double dbtA))
      {
        hasError = true;
        errMsg += "乾球温度補正係数Aが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(cB_dbt.Text, out double dbtB))
      {
        hasError = true;
        errMsg += "乾球温度補正係数Bが不正です" + Environment.NewLine;
      }

      if (!double.TryParse(cA_hmd.Text, out double hmdA))
      {
        hasError = true;
        errMsg += "相対湿度補正係数Aが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(cB_hmd.Text, out double hmdB))
      {
        hasError = true;
        errMsg += "相対湿度補正係数Bが不正です" + Environment.NewLine;
      }

      if (!double.TryParse(cA_glb.Text, out double glbA))
      {
        hasError = true;
        errMsg += "グローブ温度補正係数Aが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(cB_glb.Text, out double glbB))
      {
        hasError = true;
        errMsg += "グローブ温度補正係数Bが不正です" + Environment.NewLine;
      }

      if (!double.TryParse(cA_vel.Text, out double velA))
      {
        hasError = true;
        errMsg += "微風速補正係数Aが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(cB_vel.Text, out double velB))
      {
        hasError = true;
        errMsg += "微風速補正係数Bが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(vel_0V.Text, out double velV))
      {
        hasError = true;
        errMsg += "微風速計の無風電圧が不正です" + Environment.NewLine;
      }
      else if (velV <= 0)
      {
        hasError = true;
        errMsg += "微風速計の無風電圧は0以下になりません" + Environment.NewLine;
      }

      if (!double.TryParse(cA_lux.Text, out double luxA))
      {
        hasError = true;
        errMsg += "照度補正係数Aが不正です" + Environment.NewLine;
      }
      if (!double.TryParse(cB_lux.Text, out double luxB))
      {
        hasError = true;
        errMsg += "照度補正係数Bが不正です" + Environment.NewLine;
      }

      if (hasError) MessageBox.Show(errMsg);
      else
      {
        //補正係数設定コマンドを送信
        Task.Run(() =>
        {
          SendMessageFnc(Logger.LongAddress,
            MLogger.MakeCorrectionFactorsSettingCommand
            (dbtA, dbtB, hmdA, hmdB, glbA, glbB, luxA, luxB, velA, velB, velV)
            );
        });
      }
    }
  }
}
