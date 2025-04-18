using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MLController
{
  static class Program
  {
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
      //国際化対応確認用:DEBUG
      //string lang = "ja-JP";
      //string lang = "en-US";
      /*System.Threading.Thread.CurrentThread.CurrentCulture 
        = System.Threading.Thread.CurrentThread.CurrentUICulture 
        = new System.Globalization.CultureInfo(lang);*/

      Application.SetHighDpiMode(HighDpiMode.SystemAware);
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new MainForm());
    }
  }
}
