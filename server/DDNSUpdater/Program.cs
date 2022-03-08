using System;

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DDNSUpdater
{
  class Program
  {
    static void Main(string[] args)
    {
      if (args.Length < 2)
      {
        Console.WriteLine("Set the argument. The first argument is the user ID and the second argument is the password.");
        return;
      }

      string id = args[0];
      string pass = args[1];

      Task task = Task.Run(() =>
      {
        while (true)
        {
          updateIPAddress(id, pass);
          Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd hh:mm:ss") + " DDNS updated.");
          Thread.Sleep(5 * 60 * 1000); //5minに一度、IPを更新
        }
      });

      while (true) ;
    }

    private static async void updateIPAddress(string ddnsID, string ddnsPwd)
    {
      using (HttpClient client = new HttpClient())
      {
        client.Timeout = TimeSpan.FromSeconds(10.0);

        try
        {
          string url = "http://free.ddo.jp/dnsupdate.php?dn=" + ddnsID + "&pw=" + ddnsPwd;
          await client.GetStringAsync(url);
        }
        catch (Exception ex)
        {
          //DDOでは文字コードがEUC-JPのためにエラーが生じるが、IP更新には支障がないので無視
          string dbg = ex.Message;
        }
      }
    }

  }
}
