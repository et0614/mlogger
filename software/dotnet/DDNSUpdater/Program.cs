using System;

using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace DDNSUpdater
{
  class Program
  {

    #region クラス変数

    private static string service = "dynamicDO";

    private static string userID = "";

    private static string password = "";

    private static string hostName = "";

    private static int update = 300;

    private static string gip = "https://api.ipify.org";

    private static string lastIP = "";

    private static DateTime lastUpdate = new DateTime(1999, 1, 1, 0, 0, 0);

    #endregion

    static void Main(string[] args)
    {
      //初期設定ファイル読み込み
      string sFile = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "ddns.ini";
      if (File.Exists(sFile))
      {
        using (StreamReader sReader = new StreamReader(sFile, Encoding.UTF8))
        {
          string line;
          while ((line = sReader.ReadLine()) != null)
          {
            if (!line.StartsWith("#") && line.Contains(";") && line.Contains("="))
            {
              line = line.Remove(line.IndexOf(';'));
              string[] st = line.Split('=');
              switch (st[0])
              {
                case "service":
                  service = st[1];
                  break;
                case "usr":
                  userID = st[1];
                  break;
                case "pwd":
                  password = st[1];
                  break;
                case "host":
                  hostName = st[1];
                  break;
                case "update":
                  update = Math.Max(1, int.Parse(st[1]));
                  break;
                case "gip":
                  gip = st[1];
                  break;
              }
            }
          }
        }
      }

      Task task = Task.Run(() =>
      {
        while (true)
        {
          //ネットワークにつながっていれば更新
          if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            checkUpdating();

          Thread.Sleep(5000); //5secごとにIPアドレスを更新すべきかを確認
        }
      });

      while (true) ;
    }

    private static async void checkUpdating()
    {
      //Global IPを再取得
      string ipAdd;
      try
      {
        ipAdd = await GetGlobalIP();
      }
      catch
      {
        Console.WriteLine("No network connection.");
        return;
      }

      //IPアドレスが変わったか、前回の更新から一定時間が経過した場合に更新する
      if (ipAdd != lastIP || update <= (DateTime.Now - lastUpdate).TotalSeconds)
      {
        using (HttpClient client = new HttpClient())
        {
          client.Timeout = TimeSpan.FromSeconds(10.0);
          var rsp = "";

          try
          {
            switch (service)
            {
              case "dynamicDO":
                rsp = "dynamicDO: " + await client.GetAsync("http://free.ddo.jp/dnsupdate.php?dn=" + hostName + "&pw=" + password);
                break;
              case "NoIP":
                rsp = "NoIP: " + await client.GetStringAsync("http://" + userID + ":" + password +"@dynupdate.no-ip.com/nic/update?hostname=" + hostName);
                break;
              case "DDNSNow":
                rsp = "DDNSNow: " + await client.GetStringAsync("http://f5.si/update.php?domain=" + userID + "&password=" + password + "&ip=" + ipAdd);
                break;
            }

            lastIP = ipAdd;
            lastUpdate = DateTime.Now;

            Console.WriteLine(rsp.Trim());
            Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + " DDNS updated.");
            Console.WriteLine("IP Address : " + ipAdd);
            Console.WriteLine();
          }
          catch (Exception ex)
          {
            Console.WriteLine(ex.Message);
            Console.WriteLine();
          }
        }
      }
    }

    #region Global IPアドレスの取得処理()

    private static readonly WeakReference<HttpClient> weakreference = new WeakReference<HttpClient>(null);

    private static Task<string> GetGlobalIP ()
    {
      var client = default(HttpClient);
      if (!weakreference.TryGetTarget(out client))
        weakreference.SetTarget(client = new HttpClient());

      return client.GetStringAsync(gip);
    }

    #endregion

  }
}
