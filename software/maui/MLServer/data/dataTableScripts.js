//テーブルのソート方向を決める配列
const dirOrder = [true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true];

//強調表示用タイマ：複数用意しないとタイマが重なったときにキャンセルされて表示が異常になる
const timers = {};

//MLogger一覧を更新する
function updateMLoggerList(path){
    fetch(path+'?timestamp=' + new Date().getTime())
        .then(response => response.json())
        .then(data => {
            updateTable(data); //テーブル表示更新
        })
        .catch(error => {
            console.error('Error:', error);
        });
}

//テーブルを更新する
function updateTable(data){
    // テーブルのすべての行を取得
    const rows = document.querySelectorAll('#mlTable tr');

    //それぞれのデータに対して
    cnctedLogger=0;
    for (let key in data) {
        //一定時間通信できていないデータか否か
        dTime = new Date(data[key]["lastCommunicated"]);
        disconnected =　Config.cutoff_threshold < Math.floor(0.001 * (Date.now() - dTime.getTime()));
        cnctedLogger += disconnected ? 0 : 1;

        // 行をループして、IDが一致する行を探す
        isNewRow = true;
        disconnectedRow = null;
        rows.forEach(row => {
            const cells = row.querySelectorAll('td');
            if (cells.length > 0 && cells[2].textContent === data[key]["lowAddress"]) {
                isNewRow = false;
                if(disconnected) disconnectedRow = row;
                else updateRow(data[key], cells);
            }
        });
        if(isNewRow && !disconnected) addNewRow(data[key]);
        if(disconnectedRow != null) disconnectedRow.parentNode.removeChild(disconnectedRow);
    }

    //接続台数を更新
    const elmNum = document.getElementById('mlNum');
    elmNum.textContent = cnctedLogger;

    //熱的快適性情報を反映
    const elmCmft = document.getElementById('clomet');
    elm = data[Object.keys(data)[0]];
    elmCmft.textContent = "Clo value = " + elm["cloValue"] + " clo; Metabolic rate = " + elm["metValue"] + " met";
}

//行を更新する
function updateRow(singleData, cells){
    //最終接続時刻
    dTimeStr = makeDateTimeString(new Date(singleData["lastCommunicated"]));
    if(cells[0].innerHTML == dTimeStr) return; //時刻一致の場合には更新不要
    cells[0].innerHTML = dTimeStr;
    //更新された場合には強調表示
    lad = singleData["lowAddress"];
    if (timers[lad]) clearTimeout(timers[lad]);
    cells[0].classList.add('updated');
    timers[lad] = setTimeout(() => {
        cells[0].classList.remove('updated');
        delete timers[lad];
    }, 500);

    clm=1; //列番号

    //名前
    cells[clm++].innerHTML = singleData["localName"];

    //アドレス
    cells[clm++].innerHTML = singleData["lowAddress"];

    //温湿度
    dTime = new Date(singleData["drybulbTemperature"]["lastMeasureTime"]);
    cells[clm++].innerHTML = makeDateTimeString(dTime);
    cells[clm++].innerHTML = singleData["drybulbTemperature"]["lastValue"].toFixed(1);
    cells[clm++].innerHTML = singleData["relativeHumdity"]["lastValue"].toFixed(0);

    //グローブ温度
    dTime = new Date(singleData["globeTemperature"]["lastMeasureTime"]);
    cells[clm++].innerHTML = makeDateTimeString(dTime);
    cells[clm++].innerHTML = singleData["globeTemperature"]["lastValue"].toFixed(1);

    //風速
    dTime = new Date(singleData["velocity"]["lastMeasureTime"]);
    cells[clm++].innerHTML = makeDateTimeString(dTime);
    cells[clm++].innerHTML = (100 * singleData["velocity"]["lastValue"]).toFixed(1);

    //照度
    dTime = new Date(singleData["illuminance"]["lastMeasureTime"]);
    cells[clm++].innerHTML = makeDateTimeString(dTime);
    cells[clm++].innerHTML = singleData["illuminance"]["lastValue"].toFixed(2);

    //熱的快適性
    cells[clm++].innerHTML = singleData["meanRadiantTemperature"].toFixed(1);
    cells[clm++].innerHTML = singleData["setStar"].toFixed(1);
    cells[clm++].innerHTML = singleData["pmv"].toFixed(2);
    cells[clm++].innerHTML = singleData["ppd"].toFixed(1);
    cells[clm++].innerHTML = singleData["wbgt_indoor"].toFixed(1);
    cells[clm++].innerHTML = singleData["wbgt_outdoor"].toFixed(1);

    //CSVデータリンクは不変
    //***
}

//行を追加する
function addNewRow(singleData){
    // テーブルの参照を取得
    const table = document.getElementById('mlTable').getElementsByTagName('tbody')[0];
    const isColumnVisible = getColumnsVisibility();

    // 新しい行を作成
    const newRow = table.insertRow(table.rows.length);
    clm = 0;

    //最終接続時刻
    dTime = new Date(singleData["lastCommunicated"]);
    cell = newRow.insertCell(clm++);
    cell.classList.add('general');
    cell.innerHTML = makeDateTimeString(dTime);

    //名前
    cell = newRow.insertCell(clm++);
    cell.classList.add('general');
    cell.innerHTML = singleData["localName"];

    //アドレス
    cell = newRow.insertCell(clm++);
    cell.classList.add('general');
    cell.innerHTML = singleData["lowAddress"];

    //温湿度
    dTime = new Date(singleData["drybulbTemperature"]["lastMeasureTime"]);
    cell = newRow.insertCell(clm++);
    cell.classList.add('thlog');
    cell.innerHTML = makeDateTimeString(dTime);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';
    cell = newRow.insertCell(clm++);
    cell.classList.add('thlog');
    cell.innerHTML = singleData["drybulbTemperature"]["lastValue"].toFixed(1);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';
    cell = newRow.insertCell(clm++);
    cell.classList.add('thlog');
    cell.innerHTML = singleData["relativeHumdity"]["lastValue"].toFixed(0);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';

    //グローブ温度
    dTime = new Date(singleData["globeTemperature"]["lastMeasureTime"]);
    cell = newRow.insertCell(clm++);
    cell.classList.add('glblog');
    cell.innerHTML = makeDateTimeString(dTime);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';
    cell = newRow.insertCell(clm++);
    cell.classList.add('glblog');
    cell.innerHTML = singleData["globeTemperature"]["lastValue"].toFixed(1);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';

    //風速
    dTime = new Date(singleData["velocity"]["lastMeasureTime"]);
    cell = newRow.insertCell(clm++);
    cell.classList.add('vellog');
    cell.innerHTML = makeDateTimeString(dTime);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';
    cell = newRow.insertCell(clm++);
    cell.classList.add('vellog');
    cell.innerHTML = (100 * singleData["velocity"]["lastValue"]).toFixed(1);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';

    //照度
    dTime = new Date(singleData["illuminance"]["lastMeasureTime"]);
    cell = newRow.insertCell(clm++);
    cell.classList.add('illlog');
    cell.innerHTML = makeDateTimeString(dTime);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';
    cell = newRow.insertCell(clm++);
    cell.classList.add('illlog');
    cell.innerHTML = singleData["illuminance"]["lastValue"].toFixed(2);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';

    //熱的快適性
    cell = newRow.insertCell(clm++);
    cell.classList.add('cmftlog');
    cell.innerHTML = singleData["meanRadiantTemperature"].toFixed(1);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';
    cell = newRow.insertCell(clm++);
    cell.classList.add('cmftlog');
    cell.innerHTML = singleData["setStar"].toFixed(1);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';
    cell = newRow.insertCell(clm++);
    cell.classList.add('cmftlog');
    cell.innerHTML = singleData["pmv"].toFixed(2);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';
    cell = newRow.insertCell(clm++);
    cell.classList.add('cmftlog');
    cell.innerHTML = singleData["ppd"].toFixed(1);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';
    cell = newRow.insertCell(clm++);
    cell.classList.add('cmftlog');
    cell.innerHTML = singleData["wbgt_indoor"].toFixed(1);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';
    cell = newRow.insertCell(clm++);
    cell.classList.add('cmftlog');
    cell.innerHTML = singleData["wbgt_outdoor"].toFixed(1);
    cell.style.display = isColumnVisible[clm] ? '' : 'none';

    //CSVデータ
    cell = newRow.insertCell(clm++);
    cell.classList.add('general');
    cell.innerHTML = "<a href='../" + singleData["lowAddress"] + ".csv'>" + singleData["lowAddress"] + ".csv</a>";
}

//テーブルをソートする
function sortTable(columnIndex) {
    const table = document.getElementById('mlTable');
    const tbody = table.querySelector('tbody');
    const rows = Array.from(tbody.querySelectorAll('tr'));

    // ソート対象の列ごとにデータを取得
    const sortedRows = rows.sort((a, b) => {
        const aValue = a.cells[columnIndex].textContent.trim();
        const bValue = b.cells[columnIndex].textContent.trim();

        if (dirOrder[columnIndex])
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

//日時を表す文字列を作成する
function makeDateTimeString(dateTime){
    return (dateTime.getMonth() + 1) + "/" + dateTime.getDate() + " " + dateTime.toLocaleTimeString().split('.')[0];
}

//列ごとの表示・非表示状態を切り替える
function toggleColumns(checkbox) {
    //選択をcookieに保存
    const checkboxes = document.querySelectorAll('input[name="toggle-columns"]');
    const states = Array.from(checkboxes).map(cb => `${cb.value}:${cb.checked}`).join(',');
    setCookie("checked-toggle-columns", states, 7); // 7日間有効なCookieを保存

    //表示を更新
    switchColumnDisplay();
}

//選択した列のみを表示する
function switchColumnDisplay() {
    const table = document.getElementById('mlTable');

    const isVisible = getColumnsVisibility();
    for (const key in isVisible) {
        if (isVisible.hasOwnProperty(key)) {
            const cells = table.querySelectorAll(`td:nth-child(${key}), th:nth-child(${key})`);
            cells.forEach(function (cell) {
                cell.style.display = isVisible[key] ? '' : 'none';
            });
        }
    }
}

//列ごとの表示・非常時状態を取得する
function getColumnsVisibility() {
    const visibility = {
        "1": true, "2": true, "3": true, //一般情報
        "4": true, "5": true, "6": true, //温湿度
        "7": true, "8": true, //グローブ温度
        "9": true, "10": true, //風速
        "11": true, "12": true, //照度
        "13": true, "14": true, "15": true, "16": true, "17": true, "18": true, //温熱指標
        "19":true
    };

    const checkboxes = document.querySelectorAll('input[name="toggle-columns"]');
    checkboxes.forEach(function (checkbox) {
        const columns = JSON.parse(checkbox.getAttribute('data-columns'));
        columns.forEach(function (column) {
            visibility[column] = visibility[column] && checkbox.checked;
        });
    });
    return visibility;
}

// Cookieを設定する
function setCookie(name, value, days) {
    const d = new Date();
    d.setTime(d.getTime() + (days * 24 * 60 * 60 * 1000));
    const expires = "expires=" + d.toUTCString();
    document.cookie = name + "=" + value + ";" + expires + ";path=/";
}

// Cookieを取得する
function getCookie(name) {
    const nameEQ = name + "=";
    const ca = document.cookie.split(';');
    for (let i = 0; i < ca.length; i++) {
        let c = ca[i];
        while (c.charAt(0) === ' ') c = c.substring(1, c.length);
        if (c.indexOf(nameEQ) === 0) return c.substring(nameEQ.length, c.length);
    }
    return null;
}

// 保存されたチェックボックスの状態を復元する
function load_checkBoxState() {
    const savedStates = getCookie("checked-toggle-columns");
    if (savedStates) {
        const statesArray = savedStates.split(',');
        statesArray.forEach(function (state) {
            const [value, checked] = state.split(':');
            const checkbox = document.querySelector(`input[name="toggle-columns"][value="${value}"]`);
            if (checkbox) {
                checkbox.checked = (checked === 'true');
            }
        });
    }

    //表示を更新
    switchColumnDisplay();
}