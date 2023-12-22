using System;
using System.Collections.Generic;
using System.IO;
using NPOI.SS.Formula.Functions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace DataIntegrator
{
  internal class Program
  {
    static void Main(string[] args)
    {
      //DEBUG
      //args = new string[] { "3600" };

      //引数確認
      int tStep;
      if (args.Length == 0)
      {
        Console.WriteLine("Give the data integrating time step [sec] as the first argument.");
        return;
      }
      else tStep = Math.Max(1, int.Parse(args[0]));

      //統合するデータリスト
      List<string> files = new List<string>();
      string[] fs = Directory.GetFiles("data");
      for (int i = 0; i < fs.Length; i++)
        if (fs[i].EndsWith(".csv")) files.Add(fs[i]);

      //データ名称リスト
      string[] dNames = new string[files.Count];
      for (int i = 0; i < dNames.Length; i++) dNames[i] = files[i].Substring(5, files[i].Length - 9);

      //統合の開始日時を取得
      DateTime startDT = new DateTime(2200, 1, 1, 0, 0, 0);
      foreach (string file in files)
      {
        using (StreamReader sr = new StreamReader(file))
        {
          string[] bf = sr.ReadLine().Split(',');
          //DateTime dt = DateTime.ParseExact(bf[0], "yyyy/MM/dd HH:mm:ss", null);
          DateTime dt = DateTime.ParseExact(bf[0] + "/" + bf[1] + " " + bf[2], "yyyy/MM/dd HH:mm:ss", null);
          if (dt < startDT)
          {
            startDT = dt;
            if (startDT < new DateTime(2020, 1, 1, 0, 0, 0))
            {
              Console.WriteLine("Alert: Data of the \"" + file + "\" start at " + startDT.ToString("yyyy/MM/dd HH:mm:ss"));
              Console.WriteLine("Continue calculation ? (yes / no)");
              string arg = Console.ReadLine();
              if (arg != "yes" || arg != "y") return;
            }
          }
        }
      }
      //秒を切り捨て
      startDT = new DateTime(startDT.Year, startDT.Month, startDT.Day, startDT.Hour, startDT.Minute, 0);

      //統合後のデータ書き出し用Excelファイルを生成
      IWorkbook book = new XSSFWorkbook();
      ISheet dbtSht = book.CreateSheet("DrybulbTemperature");
      ISheet hmdSht = book.CreateSheet("RelativeHumidity");
      ISheet glbSht = book.CreateSheet("GlobeTemperature");
      ISheet velSht = book.CreateSheet("Velocity");
      ISheet illSht = book.CreateSheet("Illuminance");

      //計測値を整理
      Console.WriteLine("Loading csv files.");
      DateTime endDT = new DateTime(1500, 1, 1, 0, 0, 0);
      for (int i = 0; i < files.Count; i++)
      {
        Console.WriteLine(dNames[i]);

        //ヘッダ書き込み
        writeCellValue(dbtSht, i + 1, 0, dNames[i]);
        writeCellValue(hmdSht, i + 1, 0, dNames[i]);
        writeCellValue(glbSht, i + 1, 0, dNames[i]);
        writeCellValue(velSht, i + 1, 0, dNames[i]);
        writeCellValue(illSht, i + 1, 0, dNames[i]);

        DateTime now = startDT;
        DateTime dt = new DateTime(2500, 1, 1, 0, 0, 0);
        using (StreamReader sr = new StreamReader(files[i]))
        {
          int lineNum = 0;
          int rowNum = 1;
          string line;
          double dbt, hmd, glb, vel, ill;
          int dbtNum, hmdNum, glbNum, velNum, illNum;
          dbt = hmd = glb = vel = ill = double.NaN;
          dbtNum = hmdNum = glbNum = velNum = illNum = 0;
          while ((line = sr.ReadLine()) != null)
          {
            lineNum++;
            try
            {
              line = line.TrimStart('\0');
              string[] buff = line.Split(',');
              DateTime lastDT = dt;
              //dt = DateTime.ParseExact(buff[0], "yyyy/MM/dd HH:mm:ss", null);
              dt = DateTime.ParseExact(buff[0] + "/" + buff[1] + " " + buff[2], "yyyy/MM/dd HH:mm:ss", null);
              //データが遡るエラーを回避,異常な計測年となるエラーを回避
              if (lastDT < dt && dt.Year < now.Year + 2)
              {
                //データが現在日時を超えていない場合、または次のタイムステップを超えている場合にはデータなし（NA）として行を進ませる
                /*while (dt < now)
                {
                  writeCellValue(dbtSht, i + 1, rowNum, (byte)FormulaErrorEnum.NA);
                  writeCellValue(hmdSht, i + 1, rowNum, (byte)FormulaErrorEnum.NA);
                  writeCellValue(glbSht, i + 1, rowNum, (byte)FormulaErrorEnum.NA);
                  writeCellValue(velSht, i + 1, rowNum, (byte)FormulaErrorEnum.NA);
                  writeCellValue(illSht, i + 1, rowNum, (byte)FormulaErrorEnum.NA);

                  now = now.AddSeconds(tStep);
                  rowNum++;
                }*/

                //現在のデータの日時が次の書き出し日時を超えた場合、一時保存データがあれば書き出す。なければデータなし（NA）として行を進ませる
                while (now.AddSeconds(tStep) <= dt)
                {
                  if (double.IsNaN(dbt)) writeCellValue(dbtSht, i + 1, rowNum, (byte)FormulaErrorEnum.NA);
                  else writeCellValue(dbtSht, i + 1, rowNum, dbt / dbtNum);

                  if (double.IsNaN(hmd)) writeCellValue(hmdSht, i + 1, rowNum, (byte)FormulaErrorEnum.NA);
                  else writeCellValue(hmdSht, i + 1, rowNum, hmd / hmdNum);

                  if (double.IsNaN(glb)) writeCellValue(glbSht, i + 1, rowNum, (byte)FormulaErrorEnum.NA);
                  else writeCellValue(glbSht, i + 1, rowNum, glb / glbNum);

                  if (double.IsNaN(vel)) writeCellValue(velSht, i + 1, rowNum, (byte)FormulaErrorEnum.NA);
                  else writeCellValue(velSht, i + 1, rowNum, vel / velNum);

                  if (double.IsNaN(ill)) writeCellValue(illSht, i + 1, rowNum, (byte)FormulaErrorEnum.NA);
                  else writeCellValue(illSht, i + 1, rowNum, ill / illNum);

                  dbt = hmd = glb = vel = ill = double.NaN;
                  dbtNum = hmdNum = glbNum = velNum = illNum = 0;
                  now = now.AddSeconds(tStep);
                  rowNum++;
                }

                //データが現在日時から次のタイムステップまでの間にある場合は書き出し候補とする
                //ただし、計測間隔が短い場合には、別のデータも該当する可能性があるため、行は進めない
                if (now <= dt && dt < now.AddSeconds(tStep))
                {
                  double bf;
                  /*if (buff[2] != "NaN" && double.TryParse(buff[2], out bf)) dbt = bf;
                  if (buff[3] != "NaN" && double.TryParse(buff[3], out bf)) hmd = bf;
                  if (buff[5] != "NaN" && double.TryParse(buff[5], out bf)) glb = bf;
                  if (buff[7] != "NaN" && double.TryParse(buff[7], out bf)) vel = bf;
                  if (buff[8] != "NaN" && double.TryParse(buff[8], out bf)) ill = bf;*/
                  if (buff[3] != "NaN" && double.TryParse(buff[3], out bf)) { dbt = double.IsNaN(dbt) ? bf : dbt + bf; dbtNum++; }
                  if (buff[4] != "NaN" && double.TryParse(buff[4], out bf)) { hmd = double.IsNaN(hmd) ? bf : hmd + bf; hmdNum++; }
                  if (buff[5] != "NaN" && double.TryParse(buff[5], out bf)) { glb = double.IsNaN(glb) ? bf : glb + bf; glbNum++; }
                  if (buff[6] != "NaN" && double.TryParse(buff[6], out bf)) { vel = double.IsNaN(vel) ? bf : vel + bf; velNum++; }
                  if (buff[7] != "NaN" && double.TryParse(buff[7], out bf)) { ill = double.IsNaN(ill) ? bf : ill + bf; illNum++; }
                }
              }
            }
            catch
            {
              Console.WriteLine("Invalid data: File " + files[i] + ", line " + lineNum + ".");
            }
          }
        }
        if(endDT < now) endDT = now;
      }

      //日時を書き込み
      var style = book.CreateCellStyle();
      style.DataFormat = book.CreateDataFormat().GetFormat("yyyy/MM/dd HH:mm:ss");
      int rn = 1;
      while (startDT <= endDT)
      {
        startDT = startDT.AddSeconds(tStep);
        writeCellValue(dbtSht, 0, rn, startDT, style);
        writeCellValue(hmdSht, 0, rn, startDT, style);
        writeCellValue(glbSht, 0, rn, startDT, style);
        writeCellValue(velSht, 0, rn, startDT, style);
        writeCellValue(illSht, 0, rn, startDT, style);        
        rn++;
      }

      //結果を保存
      Console.Write("Saving data...");
      using (var xlsxFile = new FileStream("AllData_" + tStep.ToString() + "sec.xlsx", FileMode.Create))
      { book.Write(xlsxFile); }

      //終了通知
      Console.WriteLine("Done. Press any key.");
      Console.ReadLine();
    }

    #region セル書き出し汎用メソッド

    private static void writeCellValue
      (ISheet sheet, int idxColumn, int idxRow, double value)
    {
      var row = sheet.GetRow(idxRow) ?? sheet.CreateRow(idxRow);
      var cell = row.GetCell(idxColumn) ?? row.CreateCell(idxColumn);

      cell.SetCellValue(value);
    }

    static void writeCellValue(ISheet sheet, int idxColumn, int idxRow, string value)
    {
      var row = sheet.GetRow(idxRow) ?? sheet.CreateRow(idxRow);
      var cell = row.GetCell(idxColumn) ?? row.CreateCell(idxColumn);

      cell.SetCellValue(value);
    }

    static void writeCellValue(ISheet sheet, int idxColumn, int idxRow, DateTime value, ICellStyle style)
    {
      var row = sheet.GetRow(idxRow) ?? sheet.CreateRow(idxRow);
      var cell = row.GetCell(idxColumn) ?? row.CreateCell(idxColumn);

      cell.SetCellValue(value);
      cell.CellStyle = style;
    }

    static void writeCellValue(ISheet sheet, int idxColumn, int idxRow, byte err)
    {
      var row = sheet.GetRow(idxRow) ?? sheet.CreateRow(idxRow);
      var cell = row.GetCell(idxColumn) ?? row.CreateCell(idxColumn);

      cell.SetCellErrorValue(err);
    }

    #endregion

  }
}
