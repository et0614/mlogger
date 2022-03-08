using System;

using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Text;

namespace MLWebServer
{
  class Program
  {
    static void Main(string[] args)
    {
      //HTMLデータ格納ディレクトリのパス
      string dataDirectory = AppDomain.CurrentDomain.BaseDirectory + "data";
      if (!Directory.Exists(dataDirectory)) Directory.CreateDirectory(dataDirectory);

      //初期設定ファイルの読み込み
      loadInitFile(out string httpUser, out string httpPwd);

      //サーバーを起動
      startHttpServer(dataDirectory, httpUser, httpPwd);

      while (true) ;
    }

    private static void loadInitFile(out string httpUser, out string httpPwd)
    {
      httpUser = "user";
      httpPwd = "pwd";

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
            case "http_usr":
              httpUser = st[1];
              break;
            case "http_pwd":
              httpPwd = st[1];
              break;
          }
        }
      }
    }

    /// <summary>HTTPサーバーを起動する</summary>
    private static async void startHttpServer(string dataDirectory, string user, string pwd)
    {
      Console.WriteLine("Starting web server...");

      //Web表示に必要なファイルを準備
      string fName = dataDirectory + Path.DirectorySeparatorChar + "style.css";
      File.WriteAllText(fName, Resources.style_css, Encoding.UTF8);
      fName = dataDirectory + Path.DirectorySeparatorChar + "list.js";
      File.WriteAllText(fName, Resources.list_js, Encoding.UTF8);

      HttpListener listener = new HttpListener();
      listener.Prefixes.Add("http://*:80/");
      listener.AuthenticationSchemes = AuthenticationSchemes.Basic;
      try
      {
        listener.Start();
        await Task.Run(() =>
        {
          while (true)
          {
            try
            {
              HttpListenerContext context = listener.GetContext();
              HttpListenerRequest req = context.Request;
              HttpListenerResponse res = context.Response;
              HttpListenerBasicIdentity identity = (HttpListenerBasicIdentity)context.User.Identity;

              Console.WriteLine(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") + " Accessed from " + req.UserHostAddress);
              using (StreamWriter sw = new StreamWriter("log.txt", true))
              { sw.WriteLine(req.UserHostAddress); }

              if ((identity.Name == user) && (identity.Password == pwd))
              {
                string path = "data" + req.RawUrl.Replace('/', Path.DirectorySeparatorChar);
                if (path == "data" + Path.DirectorySeparatorChar) path += "index.htm";
                
                if (path.StartsWith("data" + Path.DirectorySeparatorChar) && File.Exists(path))
                {
                  //書き込み中のファイルも取得できるように「FileShare.ReadWrite」
                  using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                  using (BinaryReader br = new BinaryReader(fs))
                  {
                    long numBytes = new FileInfo(path).Length;
                    byte[] content = br.ReadBytes((int)numBytes);
                    res.OutputStream.Write(content, 0, content.Length);
                  }
                }
                else res.StatusCode = 404;
              }
              else
              {
                res.StatusCode = 401;
                byte[] message = new UTF8Encoding().GetBytes("Access denied");
                res.OutputStream.Write(message, 0, message.Length);
              }

              try { res.Close(); }
              catch { } //client closed connection before the content was sent
            }
            catch (Exception e) { Console.WriteLine(e.Message); }
          }
        });
      }
      catch (HttpListenerException e)
      {
        //例外が発生する場合には[netsh http add urlacl url=http://*:80/ user=xxxxx]
        Console.WriteLine(e.Message + "\r\n" + "管理者権限で以下のコマンドを実行してください。\r\nnetsh http add urlacl url=http://*:80/ user=ユーザー名", "Httpサーバー起動エラー");
      }
    }
    
  }
}
