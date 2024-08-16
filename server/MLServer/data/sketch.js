const MODE = {
  DBT:0,
  HMD:1,
  GLB:2,
  VEL:3,
  PMV:4,
  ILL:5,
  PPD:6,
  CLEAR:7
}

const MARGIN = 0;
const WIDTH = 1000;
const HIGH_ADD = "0013A200";

let MIN_TMP, MAX_TMP;
let MIN_VEL, MAX_VEL;
let MIN_HMD, MAX_HMD;
let MIN_ILL, MAX_ILL;
let MIN_PMV, MAX_PMV;
let MIN_PPD, MAX_PPD;
let MIN_TMP_COLOR, MAX_TMP_COLOR;
let MIN_VEL_COLOR, MAX_VEL_COLOR;
let MIN_HMD_COLOR, MAX_HMD_COLOR;
let MIN_ILL_COLOR, MAX_ILL_COLOR;
let MIN_PMV_COLOR, MAX_PMV_COLOR;
let MIN_PPD_COLOR, MAX_PPD_COLOR;

let bgimg;
let mode = MODE.DBT;
let noImage = false;

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

function changeMode(md){
  mode=md;
  redraw();
}

function setup() {
  //frameRate(1); //1秒に一度の更新
  noLoop(); // 再描画を停止

  //横幅を基準に縦を調整
  height = noImage ? WIDTH : bgimg.height * (WIDTH / bgimg.width);
  canvas = createCanvas(MARGIN + WIDTH, MARGIN + height);

  //描画位置
  canvas.parent('canvas-container');

  //配色の設定
  MAX_TMP_COLOR = color(204,102,0,50);
  MIN_TMP_COLOR = color(0,0,255,50);
  MAX_VEL_COLOR = color(255,0,0,50);
  MIN_VEL_COLOR = color(51,153,0,50);
  MAX_HMD_COLOR = color(204,102,0,50);
  MIN_HMD_COLOR = color(0,0,255,50);
  MAX_PMV_COLOR = color(204,102,0,50);
  MIN_PMV_COLOR = color(0,0,255,50);
  MAX_PPD_COLOR = color(255,0,0,50);
  MIN_PPD_COLOR = color(51,153,0,50);
  MAX_ILL_COLOR = color(255,241,0,50);
  MIN_ILL_COLOR = color(0,0,0,50);
  MAX_TMP = 28;
  MIN_TMP = 22;
  MAX_VEL = 0.4;
  MIN_VEL = 0;
  MAX_HMD = 60;
  MIN_HMD = 30;
  MAX_PMV = 1.5;
  MIN_PMV = -1.5;
  MAX_PPD = 50;
  MIN_PPD = 0;
  MAX_ILL = 1000;
  MIN_ILL = 0;
}

function draw() {
  // 背景をクリア
  background(255);

  // 画像を表示
  if(!noImage){
    height = bgimg.height * (WIDTH / bgimg.width);
    image(bgimg, MARGIN, MARGIN, WIDTH, height);
  }

  //Clearの場合には画像描画のみ
  if(mode == MODE.CLEAR) return;

  //オフセット
  translate(MARGIN, MARGIN);

  //最新の計測データを取得
  fetch('../latest.json')
  .then(response => response.json())
  .then(data => {

    //領域を着色
    stroke(0);
    strokeWeight(2);
    for (let key in data) {
      setColor(data[key]);
      drawRegion(data[key]["lowAddress"]);
    }

    //計測値を文字として表示
    for (let key in data) {
      stroke(255);
      strokeWeight(3);
      fill(0);
      textSize(20);
      drawText(data[key]["lowAddress"], getLastValue(data[key]));
    }
  })
  .catch(error => {
    console.error('Error:', error);
  });
}

function getLastValue(mlogger){
  switch(mode){
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

function setColor(mlogger){
  switch(mode){
    case MODE.DBT:
      maxVal = MAX_TMP;
      minVal = MIN_TMP;
      maxCol = MAX_TMP_COLOR;
      minCol = MIN_TMP_COLOR;
      lastVal = mlogger["drybulbTemperature"]["lastValue"];
      break;
    case MODE.GLB:
      maxVal = MAX_TMP;
      minVal = MIN_TMP;
      maxCol = MAX_TMP_COLOR;
      minCol = MIN_TMP_COLOR;
      lastVal = mlogger["globeTemperature"]["lastValue"];
      break;
    case MODE.VEL:
      maxVal = MAX_VEL;
      minVal = MIN_VEL;
      maxCol = MAX_VEL_COLOR;
      minCol = MIN_VEL_COLOR;
      lastVal = mlogger["velocity"]["lastValue"];
      break;
    case MODE.HMD:
      maxVal = MAX_HMD;
      minVal = MIN_HMD;
      maxCol = MAX_HMD_COLOR;
      minCol = MIN_HMD_COLOR;
      lastVal = mlogger["relativeHumdity"]["lastValue"];
      break;
    case MODE.PMV:
      maxVal = MAX_PMV;
      minVal = MIN_PMV;
      maxCol = MAX_PMV_COLOR;
      minCol = MIN_PMV_COLOR;
      lastVal = mlogger["pmv"];
      break;
    case MODE.ILL:
      maxVal = MAX_ILL;
      minVal = MIN_ILL;
      maxCol = MAX_ILL_COLOR;
      minCol = MIN_ILL_COLOR;
      lastVal = mlogger["illuminance"]["lastValue"];
      break;
    case MODE.PPD:
      maxVal = MAX_PPD;
      minVal = MIN_PPD;
      maxCol = MAX_PPD_COLOR;
      minCol = MIN_PPD_COLOR;
      lastVal = mlogger["ppd"];
      break;
    default:
      break;
  }
  if(lastVal <= minVal) amt = 0.0;
  else if(maxVal <= lastVal) amt = 1.0;
  else amt = (lastVal - minVal) / (maxVal - minVal);
  cl = lerpColor(minCol,maxCol,amt);
  stroke(cl);
  fill(cl);
}

