function getHistorySuccess(json) {
    const tbody = document.getElementById('loginHistoryTable').querySelector('tbody');
    tbody.innerHTML = '';

    if (json.data.length <= 0) {
        return;
    }

    json.data.sort((a, b) => b.recordInsertTime - a.recordInsertTime);

    if (json.data.length == 1) {
        json.data.push(json.data[0]);
    }

    for (let i = 1; i < json.data.length; i++) {
        const from = json.data[i];
        const to = json.data[i - 1];

        const tr = document.createElement('tr');
        const dateTh = document.createElement('th');
        const fromTd = document.createElement('td');
        const toTd = document.createElement('td');

        dateTh.setAttribute('scope', 'row');
        dateTh.innerText = new Date(to.recordInsertTime * 1000).toLocaleString();

        fromTd.innerText = `${from.displayName} (${from.login})`;
        toTd.innerText = `${to.displayName} (${to.login})`;

        tr.appendChild(dateTh);
        tr.appendChild(fromTd);
        tr.appendChild(toTd);
        tbody.appendChild(tr);
    }
}