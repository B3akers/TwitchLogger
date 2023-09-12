var channelChatInfo = {
    loaded: false
};

var channelStatsLoaded = false;
var lastUserLogUrl = '?subtab=userLogs';

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
        tdCount.innerText = item.count.toLocaleString();

        tr.appendChild(th);
        tr.appendChild(tdName);
        tr.appendChild(tdCount);
        tbody.appendChild(tr);
    }
}

function onMessageLogPin(e) {
    const target = e.target;
    const date = target.dataset.date;

    const currentTarget = document.querySelector(`table[data-table-log-date="${date}"] tbody tr.pinned th[data-date="${date}"]`);
    if (currentTarget) {
        currentTarget.parentElement.classList.remove('pinned');
    }

    target.parentElement.classList.add('pinned');

    if (e.isScriptSwitch) {
        target.scrollIntoView();
    } else {
        const newUrl = `?subtab=userLogs&userLogin=${encodeURIComponent(target.dataset.userLogin)}&userId=${encodeURIComponent(target.dataset.userId)}&date=${encodeURIComponent(date)}&msg=${target.dataset.msgId}`;
        window.history.replaceState(null, null, newUrl);
        lastUserLogUrl = newUrl;
    }
}

function loadUserLogs(e) {
    const target = e.target;
    target.setAttribute('disabled', '');

    if (!e.isScriptSwitch) {
        const newUrl = `?subtab=userLogs&userLogin=${encodeURIComponent(target.dataset.userLogin)}&userId=${encodeURIComponent(target.dataset.userId)}&date=${encodeURIComponent(target.dataset.time)}`;
        window.history.replaceState(null, null, newUrl);
        lastUserLogUrl = newUrl;
    }

    fetch(getUserLogsUrl, getPostRequestOptions({
        id: target.dataset.roomId,
        user: target.dataset.userId,
        date: target.dataset.time
    }))
        .then((resp) => {
            if (resp.status >= 200 && resp.status < 300) {
                const dataPromise = resp.text();
                const logTime = target.dataset.time;
                const userId = target.dataset.userId;
                const userLogin = target.dataset.userLogin;
                const logContainer = document.createElement('div');
                logContainer.classList.add('container', 'container-logs', 'border');
                logContainer.innerHTML = `
                <table data-table-log-date="${logTime}" class="table table-hover table-striped">
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

                    let pinMsg = null;

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

                        const tr = document.createElement('tr');
                        const th = document.createElement('th');
                        const tdUser = document.createElement('td');
                        const tdMessage = document.createElement('td');

                        if (channelChatInfo.badges) {
                            for (let y = 0; y < badges.length; y++) {
                                const badgePath = badges[y].split('/');
                                if (badgePath.length == 2) {
                                    const badgeInfo = channelChatInfo.badges[badgePath[0]]?.[badgePath[1]] ?? null;
                                    if (badgeInfo) {
                                        const img = document.createElement('img');
                                        img.setAttribute('src', badgeInfo.image);
                                        img.setAttribute('alt', badges[y]);
                                        img.setAttribute('title', badgeInfo.title);
                                        img.style.marginRight = '5px';
                                        tdUser.appendChild(img);
                                    }
                                }
                            }
                        }

                        const span = document.createElement('span');
                        span.style.color = color;
                        span.innerText = senderInfos["display-name"];

                        th.innerText = dateStr;
                        th.classList.add('cursor-pointer');
                        th.dataset.date = logTime;
                        th.dataset.userId = userId;
                        th.dataset.userLogin = userLogin;
                        th.dataset.msgId = senderInfos['id'];
                        th.addEventListener('click', onMessageLogPin);
                        tdUser.appendChild(span);
                        tdMessage.innerText = message;

                        tr.appendChild(th);
                        tr.appendChild(tdUser);
                        tr.appendChild(tdMessage);
                        tbody.appendChild(tr);

                        if (e.pinMessageAfetrLog && e.pinMessageAfetrLog == th.dataset.msgId) {
                            pinMsg = th;
                        }
                    }

                    if (pinMsg) {
                        onMessageLogPin({ target: pinMsg, isScriptSwitch: true });
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

    const userLogin = document.querySelector('form[data-callback="getUserLogsTimesSuccess"] input[type="text"]').value;

    for (let i = (json.data.length - 1); i >= 0; i--) {
        const dateStr = json.data[i];
        const button = document.createElement('button');
        button.setAttribute('type', 'button');
        button.classList.add('btn', 'btn-primary', 'w-100', 'm-2');
        button.innerText = dateStr;
        button.dataset.time = dateStr;
        button.dataset.userLogin = userLogin;
        button.dataset.userId = json.userId;
        button.dataset.roomId = json.roomId;
        button.addEventListener('click', loadUserLogs);
        container.appendChild(button);
    }

    const newUrl = `?subtab=userLogs&userLogin=${encodeURIComponent(userLogin)}&userId=${encodeURIComponent(json.userId)}`;
    window.history.replaceState(null, null, newUrl);
    lastUserLogUrl = newUrl;
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
        tdCount.innerText = item.count.toLocaleString();

        tr.appendChild(th);
        tr.appendChild(tdName);
        tr.appendChild(tdCount);
        tbody.appendChild(tr);
    }
}

function getTopWordsSuccess(json) {
    const form = document.querySelector('form[data-callback="getTopWordsSuccess"]');
    const year = form.querySelector('input[data-form-name="year"]').value;

    if (year != '0') {
        window.history.replaceState(null, null, `?subtab=channelStats&year=${encodeURIComponent(year)}`);
    }
    else {
        window.history.replaceState(null, null, window.location.pathname);
    }

    const tbody = document.getElementById('tableWords').querySelector('tbody');
    tbody.innerHTML = '';

    for (let i = 0; i < json.data.length; i++) {
        const item = json.data[i];

        const tr = document.createElement('tr');
        const th = document.createElement('th');
        th.setAttribute('scope', 'row');
        th.innerText = (i + 1);

        const tdWord = document.createElement('td');
        const tdCount = document.createElement('td');

        tdWord.innerText = item.word;
        tdCount.innerText = item.count.toLocaleString();

        tr.appendChild(th);
        tr.appendChild(tdWord);
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
        const forms = document.querySelectorAll(`div[data-subtab="${target.dataset.yearSwitchFor}"] form`);
        for (let i = 0; i < forms.length; i++) {
            const form = forms[i];
            const yearInput = form.querySelector('input[data-form-name="year"]');
            yearInput.value = target.dataset.yearSwitch;
            form.requestSubmit(form.querySelector('button[type="submit"]'));
        }
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

    const subTabName = target.dataset.subtab;

    if (e.isScriptSwitch) {
        const url = new URL(window.location.href);
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
        } else if (subTabName == 'userLogs') {
            newUrl = lastUserLogUrl;
        }
        window.history.replaceState(null, null, newUrl);
    }

    const nextDivActive = document.querySelector(`div[data-subtab=${target.dataset.subtab}]`);
    if (nextDivActive) {
        nextDivActive.classList.remove('d-none');
    }

    if (subTabName == 'channelStats') {
        if (!channelStatsLoaded) {
            channelStatsLoaded = true;
            const url = new URL(window.location.href);
            const year = url.searchParams.get('year');

            const currentYearSwitch = document.querySelector(`span[data-year-switch-for="channelStats"][data-year-switch="${year ?? '0'}"].active`);
            onYearSwitch({ target: currentYearSwitch });

        }
    } else if (subTabName == 'userLogs') {
        if (!channelChatInfo.loaded) {
            channelChatInfo.loaded = true;

            const roomId = document.querySelector('form[data-callback="getUserLogsTimesSuccess"] input[type="hidden"]').value;
            const promisesType = ['badges', 'betterttvChannell', 'frankerfacezChannell', '7tvChannell', 'betterttvGlobal', 'frankerfacezGlobal', '7tvGlobal'];

            Promise.allSettled([fetch(getChannelBadgesUrl),
                /*fetch(`https://api.betterttv.net/3/cached/users/twitch/${roomId}`),
                fetch(`https://api.frankerfacez.com/v1/room/id/${roomId}`),
                fetch(`https://7tv.io/v3/users/twitch/${roomId}`),
                fetch('https://api.betterttv.net/3/cached/emotes/global'),
                fetch('https://api.frankerfacez.com/v1/set/global'),
                fetch('https://7tv.io/v3/emote-sets/global')*/
            ]
            ).then((results) => {
                const resultsPromises = [];
                const resultsPromisesType = [];

                for (let i = 0; i < results.length; i++) {
                    const promise = results[i];
                    if (promise.status == 'fulfilled') {
                        resultsPromises.push(promise.value.json());
                        resultsPromisesType.push(promisesType[i]);
                    } else {
                        console.error(promise.reason);
                    }
                }

                Promise.allSettled(resultsPromises).then((resultsData) => {
                    for (let i = 0; i < resultsData.length; i++) {
                        const promise = resultsData[i];
                        if (promise.status == 'rejected') {
                            console.error(promise.reason);
                            continue;
                        }

                        const jsonValue = promise.value;
                        if (resultsPromisesType[i] == 'badges') {
                            const badges = {};
                            for (let y = 0; y < jsonValue.data.length; y++) {
                                const badge = jsonValue.data[y];
                                if (!badges[badge.setID])
                                    badges[badge.setID] = {};
                                badges[badge.setID][badge.version] = { image: badge.image1x, title: badge.title };
                            }
                            channelChatInfo.badges = badges;
                        }
                    }

                    document.getElementById('userLogsTab1').classList.add('d-none');
                    document.getElementById('userLogsTab2').classList.remove('d-none');

                    if (e.isScriptSwitch && subTabName == 'userLogs') {
                        const url = new URL(window.location.href);

                        lastUserLogUrl = '?' + url.searchParams.toString();

                        const userLogin = url.searchParams.get('userLogin');
                        if (userLogin) {
                            const userLoginInput = document.querySelector('form[data-callback="getUserLogsTimesSuccess"] input[type="text"]');
                            if (userLoginInput) {
                                userLoginInput.value = userLogin;
                            }
                        }

                        const userId = url.searchParams.get('userId');
                        const date = url.searchParams.get('date');
                        if (userId && date && roomId && userLogin) {
                            const msg = url.searchParams.get('msg');
                            const dummy = document.createElement('div');

                            dummy.dataset.userLogin = userLogin;
                            dummy.dataset.roomId = roomId;
                            dummy.dataset.userId = userId;
                            dummy.dataset.time = date;

                            document.getElementById('userLogsContainer').appendChild(dummy);

                            if (msg) {
                                loadUserLogs({ target: dummy, isScriptSwitch: true, pinMessageAfetrLog: msg });
                            } else {
                                loadUserLogs({ target: dummy, isScriptSwitch: true });
                            }
                        }
                    }
                });
            });
        }
    }
}

(function () {
    const printSpans = document.querySelectorAll('span[data-print-type]');
    for (let i = 0; i < printSpans.length; i++) {
        const span = printSpans[i];
        const value = span.dataset.value;
        if (span.dataset.printType == 'date') {
            span.innerText = new Date(parseInt(value) * 1000).toISOString().
                replace(/T/, ' ').
                replace(/\..+/, '');
        } else if (span.dataset.printType == 'number') {
            span.innerText = parseInt(value).toLocaleString();
        }
    }

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

    let subtab = url.searchParams.get('subtab');
    if (!subtab) {
        subtab = 'channelStats';
    }

    if (subtab) {
        const currentSubtab = document.querySelector(`span[data-subtab="${subtab}"]`);
        if (currentSubtab) {
            onTabSwitch({ target: currentSubtab, isScriptSwitch: true });
        }
    }
})();