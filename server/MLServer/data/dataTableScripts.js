//テーブルのソート方向を決める配列
const dirOrder = [true, true, true, true, true, true, true, true, true, true, true, true, true, true, true, true];

//MLoggerの一般情報（台数、着衣量、代謝量）を読み込む
function loadMLoggerInfo(path) {
    fetch(path)
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

//MLoggerのデータを読み込んでテーブルを作成する
function loadMLoggerTable(path) {
    const isRowVisible = getRowsVisibility();

    fetch(path)
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
                cell.style.display = isRowVisible[row] ? '' : 'none';
                cell = newRow.insertCell(row++);
                cell.classList.add('thlog');
                cell.innerHTML = data[key]["drybulbTemperature"]["lastValue"].toFixed(1);
                cell.style.display = isRowVisible[row] ? '' : 'none';
                cell = newRow.insertCell(row++);
                cell.classList.add('thlog');
                cell.innerHTML = data[key]["relativeHumdity"]["lastValue"].toFixed(1);
                cell.style.display = isRowVisible[row] ? '' : 'none';

                //グローブ温度
                dTime = new Date(data[key]["globeTemperature"]["lastMeasureTime"]);
                hasDisconnected = dTime.getFullYear() < 1900 || 2100 < dTime.getFullYear();
                cell = newRow.insertCell(row++);
                cell.classList.add('glblog');
                cell.innerHTML = hasDisconnected ? "***" : (dTime.getMonth() + 1) + "/" + dTime.getDate() + " " + dTime.toLocaleTimeString().split('.')[0];
                cell.style.display = isRowVisible[row] ? '' : 'none';
                cell = newRow.insertCell(row++);
                cell.classList.add('glblog');
                cell.innerHTML = data[key]["globeTemperature"]["lastValue"].toFixed(1);
                cell.style.display = isRowVisible[row] ? '' : 'none';

                //風速
                dTime = new Date(data[key]["velocity"]["lastMeasureTime"]);
                hasDisconnected = dTime.getFullYear() < 1900 || 2100 < dTime.getFullYear();
                cell = newRow.insertCell(row++);
                cell.classList.add('vellog');
                cell.innerHTML = hasDisconnected ? "***" : (dTime.getMonth() + 1) + "/" + dTime.getDate() + " " + dTime.toLocaleTimeString().split('.')[0];
                cell.style.display = isRowVisible[row] ? '' : 'none';
                cell = newRow.insertCell(row++);
                cell.classList.add('vellog');
                cell.innerHTML = (100 * data[key]["velocity"]["lastValue"]).toFixed(1);
                cell.style.display = isRowVisible[row] ? '' : 'none';

                //照度
                dTime = new Date(data[key]["illuminance"]["lastMeasureTime"]);
                hasDisconnected = dTime.getFullYear() < 1900 || 2100 < dTime.getFullYear();
                cell = newRow.insertCell(row++);
                cell.classList.add('illlog');
                cell.innerHTML = hasDisconnected ? "***" : (dTime.getMonth() + 1) + "/" + dTime.getDate() + " " + dTime.toLocaleTimeString().split('.')[0];
                cell.style.display = isRowVisible[row] ? '' : 'none';
                cell = newRow.insertCell(row++);
                cell.classList.add('illlog');
                cell.innerHTML = data[key]["illuminance"]["lastValue"].toFixed(2);
                cell.style.display = isRowVisible[row] ? '' : 'none';

                //熱的快適性
                cell = newRow.insertCell(row++);
                cell.classList.add('cmftlog');
                cell.innerHTML = data[key]["meanRadiantTemperature"].toFixed(1);
                cell.style.display = isRowVisible[row] ? '' : 'none';
                cell = newRow.insertCell(row++);
                cell.classList.add('cmftlog');
                cell.innerHTML = data[key]["setStar"].toFixed(1);
                cell.style.display = isRowVisible[row] ? '' : 'none';
                cell = newRow.insertCell(row++);
                cell.classList.add('cmftlog');
                cell.innerHTML = data[key]["pmv"].toFixed(2);
                cell.style.display = isRowVisible[row] ? '' : 'none';
                cell = newRow.insertCell(row++);
                cell.classList.add('cmftlog');
                cell.innerHTML = data[key]["ppd"].toFixed(1);
                cell.style.display = isRowVisible[row] ? '' : 'none';
                cell = newRow.insertCell(row++);
                cell.classList.add('cmftlog');
                cell.innerHTML = data[key]["wbgt_indoor"].toFixed(1);
                cell.style.display = isRowVisible[row] ? '' : 'none';
                cell = newRow.insertCell(row++);
                cell.classList.add('cmftlog');
                cell.innerHTML = data[key]["wbgt_outdoor"].toFixed(1);
                cell.style.display = isRowVisible[row] ? '' : 'none';

                //CSVデータ
                cell = newRow.insertCell(row++);
                cell.classList.add('general');
                cell.innerHTML = "<a href='../" + data[key]["lowAddress"] + ".csv'>" + data[key]["lowAddress"] + ".csv</a>";
            }
        })
        .catch(error => {
            console.error('Error:', error);
        });
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

//選択した列のみを表示する
function toggleColumns(checkbox) {
    //選択をcookieに保存
    const checkboxes = document.querySelectorAll('input[name="toggle-columns"]');
    const states = Array.from(checkboxes).map(cb => `${cb.value}:${cb.checked}`).join(',');
    setCookie("checked-toggle-columns", states, 7); // 7日間有効なCookieを保存

    //表示を更新
    switchColumnDisplay();
}

function switchColumnDisplay() {
    const table = document.getElementById('mlTable');

    const isVisible = getRowsVisibility();
    for (const key in isVisible) {
        if (isVisible.hasOwnProperty(key)) {
            const cells = table.querySelectorAll(`td:nth-child(${key}), th:nth-child(${key})`);
            cells.forEach(function (cell) {
                cell.style.display = isVisible[key] ? '' : 'none';
            });
        }
    }
}

function getRowsVisibility() {
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

// 保存されたチェックボックスの状態を復元
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