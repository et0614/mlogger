//定数宣言
const MARGIN = 0; //背景画像マージン
const HIGH_ADD = "0013A200"; //XBEE High address

//モード選択用列挙型定義
const MODE = {
  DBT: 0,
  HMD: 1,
  GLB: 2,
  VEL: 3,
  PMV: 4,
  ILL: 5,
  PPD: 6,
  CLEAR: 7
}

let bgimg;
let mode = MODE.DBT;
let noImage = false;

//初期設定
function setup() {
  noLoop(); // 再描画を停止

  //横幅を基準に縦を調整
  height = noImage ? Config.heatmap_width : bgimg.height * (Config.heatmap_width / bgimg.width);
  canvas = createCanvas(MARGIN + Config.heatmap_width, MARGIN + height);

  //描画位置
  canvas.parent('canvas-container');
}

function preload() {
  // 画像を読み込む
  loadImage('../background.png', success, failure);
}

function success(img) {
  bgimg = img;
}

function failure(event) {
  noImage = true;
}

function changeMode(md) {
  mode = md;
  redraw();
}

function draw() {
  //最新の計測データを取得
  fetch('../latest.json')
    .then(response => response.json())
    .then(data => {
      // 背景をクリア
      background(255);

      // 画像を表示
      if (!noImage) {
        height = bgimg.height * (Config.heatmap_width / bgimg.width);
        image(bgimg, MARGIN, MARGIN, Config.heatmap_width, height);
      }

      //Clearの場合には画像描画のみ
      if (mode == MODE.CLEAR) return;

      //オフセット
      translate(MARGIN, MARGIN);

      //着色のための最大・最小値を求める
      minMax = Config.auto_color_range ? getMaxMinValue(data) : null;

      //各領域を描画
      for (let key in data) {
        dTime = new Date(data[key]["lastCommunicated"]);
        disconnected = Config.cutoff_threshold < Math.floor(0.001 * (Date.now() - dTime.getTime())); //最終接続時刻次第で非描画
        if(!disconnected){
          //領域を着色
          stroke(0);
          strokeWeight(2);
          setColor(data[key], minMax);
          drawRegion(data[key]["lowAddress"]);

          //計測値を文字として表示
          stroke(255);
          strokeWeight(3);
          fill(0);
          textSize(20);
          drawText(data[key]["lowAddress"], makeLastValueString(data[key]));
        }
      }
    })
    .catch(error => {
      console.error('Error:', error);
    });
}

function getMaxMinValue(data){
  md = "drybulbTemperature";
  minDelta = 0;
  switch(mode){
    case MODE.DBT:
      md = "drybulbTemperature";
      minDelta = Config.min_dta_tmp;
      break;
    case MODE.GLB:
      md = "globeTemperature";
      minDelta = Config.min_dta_tmp;
      break;
    case MODE.VEL:
      md = "velocity";
      minDelta = Config.min_dta_vel;
      break;
    case MODE.HMD:
      md = "relativeHumdity";
      minDelta = Config.min_dta_hmd;
      break;
    case MODE.PMV:
      md = "pmv";
      minDelta = Config.min_dta_pmv;
      break;
    case MODE.ILL:
      md = "illuminance";
      minDelta = Config.min_dta_ILL;
      break;
    case MODE.PPD:
      md = "ppd";
      minDelta = Config.min_dta_ppd;
      break;
    default:
      break;
  }
  
  minVal = Infinity
  maxVal = -Infinity
  for (let key in data) {
    dTime = new Date(data[key]["lastCommunicated"]);
    disconnected = Config.cutoff_threshold < Math.floor(0.001 * (Date.now() - dTime.getTime()));
    if(!disconnected)
    {
      if(md=="ppd") val = data[key]["ppd"];
      else if(md=="pmv") val = data[key]["pmv"];
      else val = data[key][md]["lastValue"];
      if(val != 0.0)  //丁度0は異常値なので除外。あまり良くない処理
      {
        minVal = Math.min(minVal, val);
        maxVal = Math.max(maxVal, val);
      }    
    }
  }
  if(maxVal - minVal < minDelta){
    mid = 0.5 * (maxVal + minVal);
    minVal = mid - 0.5 * minDelta
    maxVal = minVal + minDelta
  }
  return [minVal, maxVal];
}

function makeLastValueString(mlogger) {
  switch (mode) {
    case MODE.DBT:
      return mlogger["drybulbTemperature"]["lastValue"].toFixed(1) + " C";
    case MODE.GLB:
      return mlogger["globeTemperature"]["lastValue"].toFixed(1) + " C";
    case MODE.VEL:
      return (100 * mlogger["velocity"]["lastValue"]).toFixed(0) + " cm/s";
    case MODE.HMD:
      return mlogger["relativeHumdity"]["lastValue"].toFixed(0) + " %";
    case MODE.PMV:
      return mlogger["pmv"].toFixed(2);
    case MODE.PPD:
      return mlogger["ppd"].toFixed(1);
    case MODE.ILL:
      return mlogger["illuminance"]["lastValue"].toFixed(0) + " lx";
    default:
      break;
  }
}

function setColor(mlogger, minMax) {
  //上下限および中間値での色
  max_tmp_color= color(255, 0, 0, 50);  //赤
  mid_tmp_color= color(0, 255, 0, 50);  //緑
  min_tmp_color= color(0, 0, 255, 50);  //青
  max_vel_color= color(255, 0, 0, 50);
  mid_vel_color= color(125, 125, 0, 50);
  min_vel_color= color(0, 255, 0, 50);
  max_hmd_color= color(255, 0, 0, 50);
  mid_hmd_color= color(0, 255, 0, 50);
  min_hmd_color= color(0, 0, 255, 50);
  max_pmv_color= color(204, 0, 0, 50);
  mid_pmv_color= color(125, 125, 0, 50);
  min_pmv_color= color(0, 0, 255, 50);
  max_ppd_color= color(255, 0, 0, 50);
  mid_ppd_color= color(125, 125, 0, 50);
  min_ppd_color= color(0, 255, 0, 50);
  max_ill_color= color(255, 255, 0, 50);
  mid_ill_color= color(125, 125, 0, 50);
  min_ill_color= color(0, 0, 0, 50);

  switch (mode) {
    case MODE.DBT:
      minVal = Config.auto_color_range ? minMax[0] : Config.min_tmp;
      maxVal = Config.auto_color_range ? minMax[1] : Config.max_tmp;
      maxCol = max_tmp_color;
      midCol = mid_tmp_color;
      minCol = min_tmp_color;
      lastVal = mlogger["drybulbTemperature"]["lastValue"];
      break;
    case MODE.GLB:
      minVal = Config.auto_color_range ? minMax[0] : Config.min_tmp;
      maxVal = Config.auto_color_range ? minMax[1] : Config.max_tmp;
      maxCol = max_tmp_color;
      midCol = mid_tmp_color;
      minCol = min_tmp_color;
      lastVal = mlogger["globeTemperature"]["lastValue"];
      break;
    case MODE.VEL:
      minVal = Config.auto_color_range ? minMax[0] : Config.min_vel;
      maxVal = Config.auto_color_range ? minMax[1] : Config.max_vel;
      maxCol = max_vel_color;
      midCol = mid_vel_color;
      minCol = min_vel_color;
      lastVal = mlogger["velocity"]["lastValue"];
      break;
    case MODE.HMD:
      minVal = Config.auto_color_range ? minMax[0] : Config.min_hmd;
      maxVal = Config.auto_color_range ? minMax[1] : Config.max_hmd;
      maxCol = max_hmd_color;
      midCol = mid_hmd_color;
      minCol = min_hmd_color;
      lastVal = mlogger["relativeHumdity"]["lastValue"];
      break;
    case MODE.PMV:
      minVal = Config.auto_color_range ? minMax[0] : Config.min_pmv;
      maxVal = Config.auto_color_range ? minMax[1] : Config.max_pmv;
      maxCol = max_pmv_color;
      midCol = mid_pmv_color;
      minCol = min_pmv_color;
      lastVal = mlogger["pmv"];
      break;
    case MODE.ILL:
      minVal = Config.auto_color_range ? minMax[0] : Config.min_ill;
      maxVal = Config.auto_color_range ? minMax[1] : Config.max_ill;
      maxCol = max_ill_color;
      midCol = mid_ill_color;
      minCol = min_ill_color;
      lastVal = mlogger["illuminance"]["lastValue"];
      break;
    case MODE.PPD:
      minVal = Config.auto_color_range ? minMax[0] : Config.min_ppd;
      maxVal = Config.auto_color_range ? minMax[1] : Config.max_ppd;
      maxCol = max_ppd_color;
      midCol = mid_ppd_color;
      minCol = min_ppd_color;
      lastVal = mlogger["ppd"];
      break;
    default:
      break;
  }
  if (lastVal <= minVal) amt = 0.0;
  else if (maxVal <= lastVal) amt = 1.0;
  else amt = (lastVal - minVal) / (maxVal - minVal);
  if(amt < 0.5) cl = lerpColor(minCol, midCol, 2.0 * amt);
  else cl = lerpColor(midCol, maxCol, 2.0 * amt - 1.0);
  stroke(cl);
  fill(cl);
}

function handleModeChange() {
  // ラジオボタンが変更された時に呼び出される関数
  var selectedMode = document.querySelector('input[name="mode"]:checked').value;
  switch (selectedMode) {
    case "dbt":
      changeMode(MODE.DBT);
      break;
    case "hmd":
      changeMode(MODE.HMD);
      break;
    case "glb":
      changeMode(MODE.GLB);
      break;
    case "vel":
      changeMode(MODE.VEL);
      break;
    case "ill":
      changeMode(MODE.ILL);
      break;
    case "pmv":
      changeMode(MODE.PMV);
      break;
    case "ppd":
      changeMode(MODE.PPD);
      break;
    case "clear":
      changeMode(MODE.CLEAR);
      break;
    default:
      break;
  }
}

function toggleHeatmap(checkbox) {
    var element = document.getElementById("heatMapBlock");
    if (checkbox.checked) {
        element.style.display = 'none';
        sessionStorage.setItem('displayMap', 'none');
    }
    else {
        element.style.display = 'block';
        sessionStorage.setItem('displayMap', 'block');
    }
}

function load_HeatmapState() {
    const displayState = sessionStorage.getItem('displayMap');
    const map = document.getElementById('heatMapBlock');

    // displayStateがnullの場合、デフォルトの状態を設定
    if (displayState !== null) {
        map.style.display = displayState;
    } else {
        map.style.display = 'none';  // 初期値として非表示に設定
    }
}
