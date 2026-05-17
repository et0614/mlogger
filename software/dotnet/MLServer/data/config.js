const Config = {
  /***一般の設定**************************/
  cutoff_threshold: 3600, //何秒以上通信されていない場合に非表示にするか

  /***ヒートマップ関連の設定***************/
  //全体
  heatmap_width: 1000, //背景画像の幅
  auto_color_range: false, //着色を計測値に応じて自動調整するか否か

  //範囲を自動設定しない場合の上下限値
  max_tmp: 28,
  min_tmp: 22,
  max_vel: 0.4,
  min_vel: 0,
  max_hmd: 60,
  min_hmd: 30,
  max_pmv: 1.5,
  min_pmv: -1.5,
  max_ppd: 50,
  min_ppd: 0,
  max_ill: 1000,
  min_ill: 0,

  //範囲を自動設定する場合の最小の幅。小さい値にすれば無理やりグラデーションがかかる
  min_dta_tmp: 0.3,
  min_dta_vel: 0.05,
  min_dta_hmd: 5,
  min_dta_ill: 50,
  min_dta_pmv: 0.1,
  min_dta_ppd: 2,
  //min_dta_tmp: 3;
  //min_dta_vel: 0.3;
  //min_dta_hmd: 10;
  //min_dta_ill: 300;
  //min_dta_pmv: 0.5;
  //min_dta_ppd: 20;
};
