function drawRegion(mloggerID){
  rectMode(CORNERS);
  switch(mloggerID){
    case "42114F57":
      rect(55, 50, 160, 530);
      break;
    case "420BCD82":
      rect(160, 50, 510, 180);
      break;
    case "42114EB8":
      rect(510, 50, 875, 180);
      break;
    case "42114EF0":
      rect(875, 50, 960, 315);
      break;
    case "420BCCD1":
      beginShape();
      vertex(160,180);
      vertex(510,180);
      vertex(510,315);
      vertex(270,315);
      vertex(270,530);
      vertex(160,530);
      endShape(CLOSE);
      break;
    case "42114F8F":      
      rect(510, 180, 875, 315);
      break;
    case "420D3FC1":
      rect(55, 530, 160, 800);      
      break;
    case "420BCD79":      
      rect(160, 530, 510, 660);
      break;
    case "420BCCDA":
      rect(160, 660, 510, 800);
      break;
    case "420BCD0B":
      rect(360, 315, 435, 470);
      break;
    case "42114F00":
      rect(510, 315, 585, 470);
      break;
    case "42114E92":
      rect(660, 315, 810, 470);
      break;
    case "420BCD7E":
      beginShape();
      vertex(270,470);
      vertex(810,470);
      vertex(810,315);
      vertex(875,315);
      vertex(875,470);
      vertex(960,470);
      vertex(960,530);
      vertex(270,530);
      endShape(CLOSE);
      break;
    case "42114E95":
      rect(510, 530, 630, 710);
      break;
    case "420BCDC3":
      rect(630, 530, 690, 710);
      break;
    case "42114E40":
      rect(690, 530, 780, 620);
      break;
    case "42114F7E":
      rect(690, 620, 780, 710);
      break;
    case "42114EFA":
      rect(630, 710, 780, 800);
      break;
    case "420BCDD7":
      rect(780, 530, 875, 800);
      break;
    case "42114F78":
      rect(875, 530, 960, 800);
      break;
    default:
      break;
  }
}

function drawText(mloggerID, txt){
  switch(mloggerID){
    case "42114F57":
      text(txt, 65, 520);
      break;
    case "420BCD82":
      text(txt, 170, 170);
      break;
    case "42114EB8":
      text(txt, 520, 170);
      break;
    case "42114EF0":
      text(txt, 885, 305);
      break;
    case "420BCCD1":
      text(txt, 170, 520);
      break;
    case "42114F8F":
      text(txt, 520, 305);
      break;
    case "420D3FC1":
      text(txt, 65, 790);
      break;
    case "420BCD79":
      text(txt, 170, 650);
      break;
    case "420BCCDA":
      text(txt, 170, 790);
      break;
    case "420BCD0B":
      text(txt, 370, 460);
      break;
    case "42114F00":
      text(txt, 520, 460);
      break;
    case "42114E92":
      text(txt, 670, 460);
      break;
    case "420BCD7E":
      text(txt, 520, 520);
      break;
    case "42114E95":
      text(txt, 530, 700);
      break;
    case "420BCDC3":
      text(txt, 640, 630);
      break;
    case "42114E40":
      text(txt, 700, 610);
      break;
    case "42114F7E":
      text(txt, 700, 700);
      break;
    case "42114EFA":
      text(txt, 690, 780);
      break;
    case "420BCDD7":
      text(txt, 790, 780);
      break;
    case "42114F78":
      text(txt, 895, 780);
      break;
    default:
      break;
  }
}
