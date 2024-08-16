//テーブルのソート方向を決める配列
const dirOrder = [true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true];

function loadMLoggerInfo(){
    fetch('./latest.json')
    .then(response => response.json())
    .then(data => {
        //台数を反映
        const elmNum = document.getElementById('mlNum');
        elmNum.textContent = Object.keys(data).length;

        //熱的快適性情報を反映
        const elmCmft = document.getElementById('clomet');
        elm = data[Object.keys(data)[0]];
        elmCmft.textContent = "Clo value = " + elm["cloValue"] + " clo; Metabolic rate = " + elm["metValue"] + " met"; 
    })
    .catch(error => {
        console.error('Error:', error);
    });
    return null;
}

function loadMLoggerTable(){
    fetch('./latest.json')
    .then(response => response.json())
    .then(data => {
        // テーブルの参照を取得
        const table = document.getElementById('mlTable').getElementsByTagName('tbody')[0];

        for (let key in data) {
            // 新しい行を作成
            const newRow = table.insertRow(table.rows.length);
            row = 0;

            //最終接続時刻
            dTime = new Date(data[key]["lastCommunicated"]);
            cell = newRow.insertCell(row++);
            cell.classList.add('general');
            cell.innerHTML = (dTime.getMonth() + 1) + "/" + dTime.getDate() + " " + dTime.toLocaleTimeString().split('.')[0];

            //名前
            cell = newRow.insertCell(row++);
            cell.classList.add('general');
            cell.innerHTML = data[key]["localName"];

            //アドレス
            cell = newRow.insertCell(row++);
            cell.classList.add('general');
            cell.innerHTML = data[key]["lowAddress"];

            //温湿度
            dTime = new Date(data[key]["drybulbTemperature"]["lastMeasureTime"]);
            hasDisconnected = dTime.getFullYear() < 1900 || 2100 < dTime.getFullYear();
            cell = newRow.insertCell(row++);
            cell.classList.add('thlog');
            cell.innerHTML = hasDisconnected ? "***" : (dTime.getMonth() + 1) + "/" + dTime.getDate() + " " + dTime.toLocaleTimeString().split('.')[0];
            cell = newRow.insertCell(row++);
            cell.classList.add('thlog');
            cell.innerHTML = data[key]["drybulbTemperature"]["lastValue"].toFixed(1);
            cell = newRow.insertCell(row++);
            cell.classList.add('thlog');
            cell.innerHTML = data[key]["relativeHumdity"]["lastValue"].toFixed(1);

            //グローブ温度
            dTime = new Date(data[key]["globeTemperature"]["lastMeasureTime"]);
            hasDisconnected = dTime.getFullYear() < 1900 || 2100 < dTime.getFullYear();
            cell = newRow.insertCell(row++);
            cell.classList.add('glblog');
            cell.innerHTML = hasDisconnected ? "***" : (dTime.getMonth() + 1) + "/" + dTime.getDate() + " " + dTime.toLocaleTimeString().split('.')[0];
            cell = newRow.insertCell(row++);
            cell.classList.add('glblog');
            cell.innerHTML = data[key]["globeTemperature"]["lastValue"].toFixed(1);

            //風速
            dTime = new Date(data[key]["velocity"]["lastMeasureTime"]);
            hasDisconnected = dTime.getFullYear() < 1900 || 2100 < dTime.getFullYear();
            cell = newRow.insertCell(row++);
            cell.classList.add('vellog');
            cell.innerHTML = hasDisconnected ? "***" : (dTime.getMonth() + 1) + "/" + dTime.getDate() + " " + dTime.toLocaleTimeString().split('.')[0];
            cell = newRow.insertCell(row++);
            cell.classList.add('vellog');
            cell.innerHTML = (100 * data[key]["velocity"]["lastValue"]).toFixed(1);

            //照度
            dTime = new Date(data[key]["illuminance"]["lastMeasureTime"]);
            hasDisconnected = dTime.getFullYear() < 1900 || 2100 < dTime.getFullYear();
            cell = newRow.insertCell(row++);
            cell.classList.add('illlog');
            cell.innerHTML = hasDisconnected ? "***" : (dTime.getMonth() + 1) + "/" + dTime.getDate() + " " + dTime.toLocaleTimeString().split('.')[0];
            cell = newRow.insertCell(row++);
            cell.classList.add('illlog');
            cell.innerHTML = data[key]["illuminance"]["lastValue"].toFixed(2);

            //熱的快適性
            cell = newRow.insertCell(row++);
            cell.classList.add('cmftlog');
            cell.innerHTML = data[key]["meanRadiantTemperature"].toFixed(1);
            cell = newRow.insertCell(row++);
            cell.classList.add('cmftlog');
            cell.innerHTML = data[key]["setStar"].toFixed(1);
            cell = newRow.insertCell(row++);
            cell.classList.add('cmftlog');
            cell.innerHTML = data[key]["pmv"].toFixed(2);
            cell = newRow.insertCell(row++);
            cell.classList.add('cmftlog');
            cell.innerHTML = data[key]["ppd"].toFixed(1);
            
            //CSVデータ
            cell = newRow.insertCell(row++);
            cell.classList.add('general');
            cell.innerHTML = "<a href='" + data[key]["lowAddress"] + ".csv'>" + data[key]["lowAddress"] + ".csv</a>";
        }
    })
    .catch(error => {
        console.error('Error:', error);
    });
    return null;
}

function sortTable(columnIndex) {
    const table = document.getElementById('mlTable');
    const tbody = table.querySelector('tbody');
    const rows = Array.from(tbody.querySelectorAll('tr'));
  
    // ソート対象の列ごとにデータを取得
    const sortedRows = rows.sort((a, b) => {
      const aValue = a.cells[columnIndex].textContent.trim();
      const bValue = b.cells[columnIndex].textContent.trim();
  
      if(dirOrder[columnIndex])
        return isNaN(aValue) ? aValue.localeCompare(bValue) : aValue - bValue;
      else 
        return isNaN(aValue) ? bValue.localeCompare(aValue) : bValue - aValue;
    });
  
    // ソート結果をテーブルに反映
    tbody.innerHTML = '';
    sortedRows.forEach(row => {
      tbody.appendChild(row);
    });
    dirOrder[columnIndex] = !dirOrder[columnIndex];
  }