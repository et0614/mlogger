function drawRegion(mloggerID){
  rectMode(CORNERS);
  switch(mloggerID){
    case "42114F57":
      rect(80, 320, 320, 430);
      break;
    case "420BCD82":
      rect(320, 320, 520, 430);
      break;
    case "42114EB8":
      rect(0, 430, 220, 560);
      break;
    case "42114EF0":
      rect(220, 430, 425, 560);
      break;
    case "420BCCD1":
      rect(0, 560, 220, 760);
      break;
    case "42114F8F":      
      rect(220, 560, 425, 760);
      break;
    case "420D3FC1":
      rect(0, 760, 220, 900);      
      break;
    case "420BCD79":      
      rect(220, 760, 425, 900);
      break;
    case "420BCCDA":
      rect(580, 40, 660, 250);
      break;
    case "420BCD0B":
      rect(660, 40, 900, 250);
      break;
    case "42114F00":
      beginShape();
      vertex(425,250);
      vertex(660,250);
      vertex(660,350);
      vertex(520,350);
      vertex(520,320);
      vertex(425,320);
      endShape(CLOSE);
      break;
    case "42114E92":
      rect(660, 250, 900, 350);
      break;
    case "420BCD7E":
      beginShape();
      vertex(520,350);
      vertex(660,350);
      vertex(660,510);
      vertex(425,510);
      vertex(425,430);
      vertex(520,430);
      endShape(CLOSE);
      break;
    case "42114E95":
      rect(660, 350, 900, 510);
      break;
    case "420BCDC3":
      rect(425, 510, 660, 650);
      break;
    case "42114E40":
      rect(660, 510, 900, 650);
      break;
    case "42114F7E":
      rect(425, 650, 660, 760);
      break;
    case "42114EFA":
      rect(660, 650, 900, 760);
      break;
    case "420BCDD7":
      rect(425, 760, 660, 900);
      break;
    case "42114F78":
      rect(660, 760, 900, 900);
      break;
    default:
      break;
  }
}

function drawText(mloggerID, txt){
  switch(mloggerID){
    case "42114F57":
      text(txt, 90, 420);
      break;
    case "420BCD82":
      text(txt, 330, 420);
      break;
    case "42114EB8":
      text(txt, 10, 550);
      break;
    case "42114EF0":
      text(txt, 230, 550);
      break;
    case "420BCCD1":
      text(txt, 10, 750);
      break;
    case "42114F8F":
      text(txt, 230, 750);
      break;
    case "420D3FC1":
      text(txt, 10, 890);
      break;
    case "420BCD79":
      text(txt, 230, 890);
      break;
    case "420BCCDA":
      text(txt, 590, 240);
      break;
    case "420BCD0B":
      text(txt, 670, 240);
      break;
    case "42114F00":
      text(txt, 530, 340);
      break;
    case "42114E92":
      text(txt, 670, 340);
      break;
    case "420BCD7E":
      text(txt, 435, 500);
      break;
    case "42114E95":
      text(txt, 670, 500);
      break;
    case "420BCDC3":
      text(txt, 435, 640);
      break;
    case "42114E40":
      text(txt, 670, 640);
      break;
    case "42114F7E":
      text(txt, 435, 750);
      break;
    case "42114EFA":
      text(txt, 670, 750);
      break;
    case "420BCDD7":
      text(txt, 435, 890);
      break;
    case "42114F78":
      text(txt, 670, 890);
      break;
    default:
      break;
  }
}
