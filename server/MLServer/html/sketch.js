const MODE = {
  DBT:0,
  HMD:1,
  GLB:2,
  VEL:3,
}

const MARGIN = 50;
const WIDTH = 1000;
const HIGH_ADD = "0013A200";

let MIN_TMP, MAX_TMP;
let MIN_VEL, MAX_VEL;
let MIN_HMD, MAX_HMD;
let MIN_TMP_COLOR, MAX_TMP_COLOR;
let MIN_VEL_COLOR, MAX_VEL_COLOR;
let MIN_HMD_COLOR, MAX_HMD_COLOR;

let bgimg;
let mode = MODE.DBT;

function preload() {
  // 画像を読み込む
  bgimg = loadImage('background.png');
}

function changeMode(md){
  mode=md;
  redraw();
}

function setup() {
  //frameRate(1); //1秒に一度の更新
  noLoop(); // 再描画を停止

  //横幅を基準に縦を調整
  height = bgimg.height * (WIDTH / bgimg.width);
  createCanvas(MARGIN + WIDTH, MARGIN + height);

  //配色の設定
  MAX_TMP_COLOR = color(204,102,0,50);
  MIN_TMP_COLOR = color(0,0,255,50);
  MAX_VEL_COLOR = color(255,0,0,50);
  MIN_VEL_COLOR = color(51,153,0,50);
  MAX_HMD_COLOR = color(204,102,0,50);
  MIN_HMD_COLOR = color(0,0,255,50);
  MAX_TMP = 30;
  MIN_TMP = 20;
  MAX_VEL = 0.4;
  MIN_VEL = 0;
  MAX_HMD = 60;
  MIN_HMD = 20;
}

function draw() {
  // 背景をクリア
  background(255);

  // 画像を表示
  height = bgimg.height * (WIDTH / bgimg.width);
  image(bgimg, MARGIN, MARGIN, WIDTH, height);

  //オフセット
  translate(MARGIN, MARGIN);

  //最新の計測データを取得
  fetch('latest.json')
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
    default:
      break;
  }
  if(lastVal <= minVal) amt = 0.0;
  else if(maxVal <= lastVal) amt = 1.0;
  else amt = (lastVal - minVal) / (maxVal - minVal);
  fill(lerpColor(minCol,maxCol,amt));
}

