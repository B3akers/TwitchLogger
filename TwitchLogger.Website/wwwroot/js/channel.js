function getTopUserWordsSuccess(json) {
    const form = document.querySelector('form[data-callback="getTopUserWordsSuccess"]');
    const year = form.querySelector('input[data-form-name="year"]').value;
    const user = form.querySelector('input[data-form-name="user"]').value;

    window.history.replaceState(null, null, `?subtab=userWords&year=${encodeURIComponent(year)}&user=${encodeURIComponent(user)}`);

    const tbody = document.getElementById('tableUserWords').querySelector('tbody');
    tbody.innerHTML = '';

    for (let i = 0; i < json.data.length; i++) {
        const item = json.data[i];
        const tr = document.createElement('tr');
        const th = document.createElement('th');
        th.setAttribute('scope', 'row');
        th.innerText = (i + 1);

        const tdName = document.createElement('td');
        const tdCount = document.createElement('td');

        tdName.innerText = item.word;
        tdCount.innerText = item.count;

        tr.appendChild(th);
        tr.appendChild(tdName);
        tr.appendChild(tdCount);
        tbody.appendChild(tr);
    }
}

function loadUserLogs(e) {
    const target = e.target;
    target.setAttribute('disabled', '');
    fetch(getUserLogsUrl, getPostRequestOptions({
        id: target.dataset.roomId,
        user: target.dataset.userId,
        date: target.dataset.time
    }))
        .then((resp) => {
            if (resp.status >= 200 && resp.status < 300) {
                const dataPromise = resp.text();
                const logContainer = document.createElement('div');
                logContainer.classList.add('container', 'container-logs', 'border');
                logContainer.innerHTML = `
                <table class="table table-hover table-striped">
                       <thead>
                           <tr>
                               <th scope="col">Date</th>
                               <th scope="col">User</th>
                               <th scope="col">Message</th>
                           </tr>
                       </thead>
                       <tbody>
                       </tbody>
                </table>`;
                target.replaceWith(logContainer);
                dataPromise.then((data) => {
                    const messages = data.split('\r\n');
                    const tbody = logContainer.querySelector('tbody');
                    for (let i = (messages.length - 1); i >= 0; i--) {
                        const command = messages[i];
                        const commandArgs = command.split(' ');

                        if (commandArgs.length < 2)
                            continue;

                        if (commandArgs[2] != "PRIVMSG")
                            continue;

                        const senderInfos = {};
                        const messageInfos = commandArgs[0].substring(1).split(';');
                        for (let y = 0; y < messageInfos.length; y++) {
                            const splitInfo = messageInfos[y].split('=');

                            senderInfos[splitInfo[0]] = splitInfo[1];
                        }

                        const message = commandArgs.length > 4 ? commandArgs.slice(4).join(' ').substring(1) : "";

                        const badges = senderInfos["badges"].split(',');
                        const dateStr = new Date(parseInt(senderInfos["tmi-sent-ts"])).toISOString().
                            replace(/T/, ' ').
                            replace(/\..+/, '');
                        const color = senderInfos["color"] ?? '#FF4500';
                        const badgesStr = '';

                        const tr = document.createElement('tr');
                        const th = document.createElement('th');
                        const tdUser = document.createElement('td');
                        const tdMessage = document.createElement('td');

                        th.innerText = dateStr;
                        tdUser.innerText = senderInfos["display-name"];
                        tdMessage.innerText = message;

                        tr.appendChild(th);
                        tr.appendChild(tdUser);
                        tr.appendChild(tdMessage);
                        tbody.appendChild(tr);
                    }
                });
            
            } else if (resp.status == 404) {
                toastr.error(translateCode('not_found'));
            } else {
                toastr.error(translateCode('server_error'));
                console.error(resp.statusText);
            }
        }).catch((err) => {
            toastr.error(translateCode('server_error'));
            console.log(err);
        }).finally(() => {
            target.removeAttribute('disabled');
        });
}

function getUserLogsTimesSuccess(json) {
    const container = document.getElementById('userLogsContainer');
    container.innerHTML = '';

    for (let i = (json.data.length - 1); i >= 0; i--) {
        const dateStr = json.data[i];
        const button = document.createElement('button');
        button.setAttribute('type', 'button');
        button.classList.add('btn', 'btn-primary', 'w-100', 'm-2');
        button.innerText = dateStr;
        button.dataset.time = dateStr;
        button.dataset.userId = json.userId;
        button.dataset.roomId = json.roomId;
        button.addEventListener('click', loadUserLogs);
        container.appendChild(button);
    }
}

function getTopUsersSuccess(json) {
    const form = document.querySelector('form[data-callback="getTopUsersSuccess"]');
    const year = form.querySelector('input[data-form-name="year"]').value;
    const word = form.querySelector('input[data-form-name="word"]').value;

    window.history.replaceState(null, null, `?subtab=wordUsers&year=${encodeURIComponent(year)}&word=${encodeURIComponent(word)}`);

    const tbody = document.getElementById('tableWordUsers').querySelector('tbody');
    tbody.innerHTML = '';

    const usersData = {};
    for (let i = 0; i < json.userData.length; i++) {
        const item = json.userData[i];
        const currentItem = usersData[item.userId];
        if (currentItem && currentItem.recordInsertTime > item.recordInsertTime) {
            continue;
        }

        usersData[item.userId] = item;
    }

    for (let i = 0; i < json.data.length; i++) {
        const item = json.data[i];
        const tr = document.createElement('tr');
        const th = document.createElement('th');
        th.setAttribute('scope', 'row');
        th.innerText = (i + 1);

        const tdName = document.createElement('td');
        const tdCount = document.createElement('td');

        tdName.innerText = usersData[item.userId]?.displayName ?? item.userId;
        tdCount.innerText = item.count;

        tr.appendChild(th);
        tr.appendChild(tdName);
        tr.appendChild(tdCount);
        tbody.appendChild(tr);
    }
}

function onYearSwitch(e) {
    const target = e.target;

    const currentActive = document.querySelector(`span[data-year-switch-for="${target.dataset.yearSwitchFor}"].active`);
    if (currentActive) {
        currentActive.removeAttribute('aria-current');
        currentActive.classList.remove('active');
    }

    target.setAttribute('aria-current', 'page');
    target.classList.add('active');

    if (!e.isScriptSwitch) {
        const form = document.querySelector(`div[data-subtab="${target.dataset.yearSwitchFor}"] form`);
        const yearInput = form.querySelector('input[data-form-name="year"]');
        yearInput.value = target.dataset.yearSwitch;
        form.requestSubmit(form.querySelector('button[type="submit"]'));
    }
}

function onTabSwitch(e) {
    const currentActive = document.querySelector('span[data-subtab].active');
    if (currentActive) {
        currentActive.removeAttribute('aria-current');
        currentActive.classList.remove('active');

        const currentDivActive = document.querySelector(`div[data-subtab=${currentActive.dataset.subtab}]`);
        if (currentDivActive) {
            currentDivActive.classList.add('d-none');
        }
    }

    const target = e.target;

    target.setAttribute('aria-current', 'page');
    target.classList.add('active');

    if (e.isScriptSwitch) {
        const url = new URL(window.location.href);

        const subTabName = url.searchParams.get('subtab');

        if (subTabName == 'wordUsers') {
            const year = url.searchParams.get('year');
            const word = url.searchParams.get('word');

            if (year && word) {
                const form = document.querySelector('form[data-callback="getTopUsersSuccess"]');
                const yearInput = form.querySelector('input[data-form-name="year"]');
                const wordInput = form.querySelector('input[data-form-name="word"]');

                yearInput.value = year;
                wordInput.value = word;

                const activeYearSwitch = document.querySelector(`span[data-year-switch-for="wordUsers"][data-year-switch="${year}"]`);
                if (activeYearSwitch) {
                    onYearSwitch({ target: activeYearSwitch, isScriptSwitch: true });
                }

                form.requestSubmit(form.querySelector('button[type="submit"]'));
            }
        } else if (subTabName == 'userWords') {
            const year = url.searchParams.get('year');
            const user = url.searchParams.get('user');
            if (year && user) {
                const form = document.querySelector('form[data-callback="getTopUserWordsSuccess"]');
                const yearInput = form.querySelector('input[data-form-name="year"]');
                const userInput = form.querySelector('input[data-form-name="user"]');

                yearInput.value = year;
                userInput.value = user;

                const activeYearSwitch = document.querySelector(`span[data-year-switch-for="userWords"][data-year-switch="${year}"]`);
                if (activeYearSwitch) {
                    onYearSwitch({ target: activeYearSwitch, isScriptSwitch: true });
                }

                form.requestSubmit(form.querySelector('button[type="submit"]'));
            }
        }
    } else {
        const subTabName = target.dataset.subtab;

        let newUrl = `?subtab=${subTabName}`;
        if (subTabName == 'wordUsers') {
            const form = document.querySelector('form[data-callback="getTopUsersSuccess"]');
            const year = form.querySelector('input[data-form-name="year"]').value;
            const word = form.querySelector('input[data-form-name="word"]').value;
            if (year && word) {
                newUrl = `${newUrl}&year=${encodeURIComponent(year)}&word=${encodeURIComponent(word)}`
            }
        } else if (subTabName == 'userWords') {
            const form = document.querySelector('form[data-callback="getTopUserWordsSuccess"]');
            const year = form.querySelector('input[data-form-name="year"]').value;
            const user = form.querySelector('input[data-form-name="user"]').value;
            if (year && user) {
                newUrl = `${newUrl}&year=${encodeURIComponent(year)}&user=${encodeURIComponent(user)}`
            }
        }
        window.history.replaceState(null, null, newUrl);
    }

    const nextDivActive = document.querySelector(`div[data-subtab=${target.dataset.subtab}]`);
    if (nextDivActive) {
        nextDivActive.classList.remove('d-none');
    }
}

(function () {
    const yearSwitchs = document.querySelectorAll('span[data-year-switch]');
    for (let i = 0; i < yearSwitchs.length; i++) {
        const yearSwitch = yearSwitchs[i];
        yearSwitch.addEventListener('click', onYearSwitch);
    }

    const tabs = document.querySelectorAll('span[data-subtab]');
    for (let i = 0; i < tabs.length; i++) {
        const tab = tabs[i];
        tab.addEventListener('click', onTabSwitch);
    }

    const url = new URL(window.location.href);
    const subtab = url.searchParams.get('subtab');
    if (subtab) {
        const currentSubtab = document.querySelector(`span[data-subtab="${subtab}"]`);
        if (currentSubtab) {
            onTabSwitch({ target: currentSubtab, isScriptSwitch: true });
        }
    }
})();