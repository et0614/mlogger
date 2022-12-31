using System.Collections;
using System.Windows.Forms;

namespace MLServer
{
  public class MLoggerComparer : IComparer
  {

    private int colIndex;

    private bool isAscending;

    /// <summary>
    /// ListViewItemComparerクラスのコンストラクタ
    /// </summary>
    /// <param name="colIndex">並び替える列番号</param>
    /// <param name="isAscending">昇順か否か</param>
    public MLoggerComparer(int colIndex, bool isAscending)
    {
      this.colIndex = colIndex;
      this.isAscending = isAscending;
    }

    public int Compare(object x, object y)
    {
      //ListViewItemの取得
      ListViewItem itemx = (ListViewItem)x;
      ListViewItem itemy = (ListViewItem)y;

      //xとyを文字列として比較する
      return (isAscending ? 1 : -1) * string.Compare(itemx.SubItems[colIndex].Text,
          itemy.SubItems[colIndex].Text);
    }

  }
}
