var channelChatInfo = {
    loaded: false
};

var userLogsData = {

};

var userLogsDataIndexes = {

};

var channelStatsLoaded = false;
var lastUserLogUrl = '?subtab=userLogs';
var lastChannelLogUrl = '?subtab=channelLogs';

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
    const tbody = target.parentElement.parentElement;
    const date = tbody.dataset.date;

    const currentTarget = document.querySelector(`div[data-table-log-date="${date}"] div.pinned`);
    if (currentTarget) {
        currentTarget.classList.remove('pinned');
    }

    target.parentElement.classList.add('pinned');

    tbody.dataset.pinnedMsgId = target.dataset.msgId;

    if (e.isScriptSwitch) {
        target.parentElement.parentElement.parentElement.scrollTop = e.scrollToOffset;
    } else {

        var url = new URL(window.location);
        url.searchParams.set('msg', target.dataset.msgId);
        window.history.replaceState(null, null, url);

        if (date == 'channel') {
            lastChannelLogUrl = url.toString();
        } else {
            lastUserLogUrl = url.toString();
        }
    }
}

function loadMessagesForContainer(container, cursor) {
    const messages = userLogsData[container.dataset.userId + container.dataset.date];
    if (!messages)
        return;

    if (cursor == 0) {
        container.parentElement.scrollTop = 0;
    }

    const indexes = userLogsDataIndexes[container.dataset.userId + container.dataset.date];
    const messageLength = indexes ? indexes.length : messages.length;

    const messagePixels = 2.5 * parseFloat(getComputedStyle(container).fontSize);
    const messagePerContainer = (container.parentElement.offsetHeight / messagePixels) + 2;
    const currentLoadedChildren = container.children.length;

    for (let i = 0; i < messagePerContainer; i++) {
        const messageIndex = i + cursor;
        if (messageIndex >= messageLength) {
            for (let y = i; y < container.children.length; y++) {
                container.children[y].remove();
            }
            return;
        }

        const command = messages[indexes ? indexes[messageIndex] : messageIndex];

        const mainMessageDiv = document.createElement('div');
        mainMessageDiv.style.position = 'absolute';
        mainMessageDiv.style.left = '0px';
        mainMessageDiv.style.top = (2.5 * messageIndex) + "rem";
        mainMessageDiv.style.height = '2.5rem';
        mainMessageDiv.style.width = "100%";
        mainMessageDiv.style.padding = '0.2rem';

        const datetimeSpan = document.createElement('span');
        datetimeSpan.classList.add('prevent-select');
        datetimeSpan.style.marginRight = '0.5rem';
        datetimeSpan.innerText = command.dateStr;
        datetimeSpan.classList.add('cursor-pointer');
        datetimeSpan.dataset.msgId = command.messageId;
        datetimeSpan.addEventListener('click', onMessageLogPin);
        mainMessageDiv.appendChild(datetimeSpan);

        if (command.type == 'MESSAGE') {
            const mainUserSpan = document.createElement('span');
            const mainMessageSpan = document.createElement('span');

            mainUserSpan.classList.add('prevent-select');
            mainUserSpan.style.marginRight = '0.5rem';

            if (channelChatInfo.badges) {
                for (let y = 0; y < command.params.badges.length; y++) {
                    const badgePath = command.params.badges[y].split('/');
                    if (badgePath.length == 2) {
                        const badgeInfo = channelChatInfo.badges[badgePath[0]]?.[badgePath[1]] ?? null;
                        if (badgeInfo) {
                            const img = document.createElement('img');
                            img.setAttribute('src', badgeInfo.image);
                            img.setAttribute('alt', command.params.badges[y]);
                            img.setAttribute('title', badgeInfo.title);
                            img.style.marginRight = '5px';
                            mainUserSpan.appendChild(img);
                        }
                    }
                }
            }

            const span = document.createElement('span');
            span.style.color = command.params.color;
            span.innerText = command.params.displayName;

            const spanMessage = document.createElement('span');
            spanMessage.innerText = command.params.message;
            if (command.params.isMeCommand) {
                spanMessage.style.color = command.params.color;
            }

            mainUserSpan.appendChild(span);
            mainMessageSpan.appendChild(spanMessage);

            mainMessageDiv.appendChild(mainUserSpan);
            mainMessageDiv.appendChild(mainMessageSpan);
        } else if (command.type == 'BAN') {
            const mainMessageSpan = document.createElement('span');
            mainMessageSpan.classList.add('prevent-select');
            mainMessageSpan.style.marginRight = '0.5rem';
            mainMessageSpan.innerText = `${command.params.user} has been ${command.params.duration ? `timed out for ${command.params.duration} seconds.` : 'permanently banned!'}`;

            mainMessageDiv.appendChild(mainMessageSpan);
        }

        if (container.dataset.pinnedMsgId && container.dataset.pinnedMsgId == command.messageId) {
            mainMessageDiv.classList.add('pinned');
        }

        if (i < currentLoadedChildren) {
            container.children[i].replaceWith(mainMessageDiv);
        } else {
            container.appendChild(mainMessageDiv);
        }
    }
}

function parseMessagesFromRaw(data, pinMessageId, isChannelLog) {
    const messages = data.split('\r\n');
    const parsedMessages = [];

    let pinMsg = null;

    for (let i = 0; i < messages.length; i++) {
        const command = isChannelLog ? messages[i] : messages[messages.length - i - 1];
        const commandArgs = command.split(' ');

        if (commandArgs.length < 2)
            continue;

        let logType = '';

        if (commandArgs[2] == "PRIVMSG") {
            logType = 'MESSAGE';
        } else if (commandArgs[2] == "CLEARCHAT" && commandArgs.length > 4) {
            logType = 'BAN';
        }
       
        if (!logType) {
            continue;
        }

        const senderInfos = {};
        const messageInfos = commandArgs[0].substring(1).split(';');
        for (let y = 0; y < messageInfos.length; y++) {
            const splitInfo = messageInfos[y].split('=');

            senderInfos[splitInfo[0]] = splitInfo[1];
        }

        let isMeCommand = false;
        let message = commandArgs.length > 4 ? commandArgs.slice(4).join(' ').substring(1) : "";
        if (message.length > 0 && message.charCodeAt(0) == 1) {
            const msgStartIndex = message.indexOf(' ');
            if (msgStartIndex != -1)
                message = message.substring(msgStartIndex + 1, message.length - 1);
            isMeCommand = true;
        }

        const dateStr = new Date(parseInt(senderInfos["tmi-sent-ts"])).toLocaleString();

        let messageId = senderInfos['id'];
        if (!messageId) {
            messageId = senderInfos['tmi-sent-ts'];
        }

        if (pinMessageId && pinMessageId == messageId) {
            pinMsg = {
                id: messageId,
                index: parsedMessages.length
            };
        }

        if (logType == 'MESSAGE') {
            const color = senderInfos["color"] ?? '#FF4500';
            const badges = senderInfos["badges"].split(',');
            const displayName = senderInfos["display-name"];

            parsedMessages.push({
                type: logType,
                messageId: messageId,
                dateStr: dateStr,
                channel: commandArgs[3],
                params: {
                    message: message,
                    displayName: displayName,
                    color: color,
                    badges: badges,
                    isMeCommand: isMeCommand
                }
            });
        } else if (logType == 'BAN') {
            const duration = senderInfos["ban-duration"];

            parsedMessages.push({
                type: logType,
                messageId: messageId,
                dateStr: dateStr,
                channel: commandArgs[3],
                params: {
                    user: message,
                    duration: duration
                }
            });
        }
    }

    return {
        parsedMessages: parsedMessages,
        pinMsg: pinMsg
    };
}
function onLogContainerScroll(e) {
    const target = e.target;
    const tbody = target.querySelector('div[data-table-log-date]');
    const messagePixels = 2.5 * parseFloat(getComputedStyle(tbody).fontSize);

    let scrollTop = e.target.scrollTop;
    if (scrollTop > 0)
        scrollTop = Math.max(scrollTop, messagePixels);

    const currentIndex = Math.floor(scrollTop / messagePixels);

    loadMessagesForContainer(tbody, currentIndex);
}

function applyIndexesForLogs(mainContainer, userId, logTime) {
    const tbody = mainContainer.querySelector('div[data-table-log-date]');
    const indexArray = userLogsDataIndexes[userId + logTime];
    const messages = userLogsData[userId + logTime];

    const messageLength = indexArray ? indexArray.length : messages.length;
    tbody.style.height = (2.5 * messageLength) + "rem";
    tbody.innerHTML = '';
    loadMessagesForContainer(tbody, 0);
}

function createIndexesForLogs(userId, logTime, filters) {
    const messages = userLogsData[userId + logTime];
    if (!messages)
        return;

    let indexArray = [];

    if (filters.length > 0) {
        for (let i = 0; i < messages.length; i++) {
            const message = messages[i];

            let passed = 0;

            for (let y = 0; y < filters.length; y++) {
                const filter = filters[y];
                if (filter.type == 'userName') {
                    for (let y1 = 0; y1 < filter.value.length; y1++) {
                        if (message.displayName.toLowerCase().localeCompare(filter.value[y1]) == 0) {
                            passed++;
                            break;
                        }
                    }
                } else if (filter.type == 'content') {
                    if (message.message.toLowerCase().indexOf(filter.value) != -1) {
                        passed++;
                    }
                }

                if (passed == filters.length)
                    break;
            }

            if (passed == filters.length)
                indexArray.push(i);
        }

        if (indexArray.length == messages.length)
            indexArray = null;
    } else {
        indexArray = null;
    }

    userLogsDataIndexes[userId + logTime] = indexArray;
}

function getFiltersForChannelLogs() {
    const filtersContainer = document.getElementById('channelLogFilters');
    const users = filtersContainer.querySelector('textarea').value.split('\n').reduce(function (result, element) {
        let value = element.trim();
        if (value)
            result.push(value.toLowerCase());
        return result;
    }, []);


    const filters = [];

    if (users.length > 0) {
        filters.push({
            type: 'userName',
            value: users
        });
    }

    const content = filtersContainer.querySelector('input[type="text"]').value;
    if (content) {
        filters.push({
            type: 'content',
            value: content.toLowerCase()
        });
    }

    return filters;
}

function applyChannelLogsFilters() {
    const filters = getFiltersForChannelLogs();

    const mainContainer = document.getElementById('channelLogsContainer');
    const tbody = mainContainer.querySelector('div[data-table-log-date]');

    let filterParam = '';
    if (filters.length > 0) {
        for (let i = 0; i < filters.length; i++) {
            const filter = filters[i];
            if (filter.type == 'userName') {
                filterParam = '&filterUser=' + encodeURIComponent(filter.value.join(','));
            } else if (filter.type == 'content') {
                filterParam = '&content=' + encodeURIComponent(filter.value);
            }
        }
    }

    const newUrl = `?subtab=channelLogs&date=${encodeURIComponent(tbody.dataset.channelLogDate)}${filterParam}`;
    window.history.replaceState(null, null, newUrl);
    lastChannelLogUrl = newUrl;

    createIndexesForLogs(channelId, 'channel', filters);
    applyIndexesForLogs(mainContainer, channelId, 'channel');
}

function createLogsForData(logContainer, data, userId, userLogin, logTime, pinMessageId, filters) {
    const messagesInfo = parseMessagesFromRaw(data, pinMessageId, logTime == 'channel');

    const parsedMessages = messagesInfo.parsedMessages;
    const pinMsg = messagesInfo.pinMsg;

    const tbody = logContainer.querySelector('div[data-table-log-date]');

    userLogsData[userId + logTime] = parsedMessages;
    createIndexesForLogs(userId, logTime, filters ?? []);

    const indexes = userLogsDataIndexes[userId + logTime];
    const messageLength = indexes ? indexes.length : parsedMessages.length;

    tbody.dataset.date = logTime;
    tbody.dataset.userId = userId;
    tbody.dataset.userLogin = userLogin;

    tbody.style.position = 'relative';
    tbody.style.margin = '10px';
    tbody.style.height = (2.5 * messageLength) + "rem";

    //Just in case
    if ((2.5 * messageLength) > 1118000)
        tbody.style.height = "1118000rem";

    logContainer.addEventListener('scroll', onLogContainerScroll);

    if (pinMsg) {
        let pinIndex = pinMsg.index;
        let foundPinMsg = true;
        if (indexes) {
            foundPinMsg = false;
            for (let i = 0; i < indexes.length; i++) {
                if (indexes[i] == pinIndex) {
                    pinIndex = i;
                    foundPinMsg = true;
                    break;
                }
            }
        }

        if (foundPinMsg) {
            const messagePixels = 2.5 * parseFloat(getComputedStyle(logContainer).fontSize);
            const messagePerContainer = (logContainer.offsetHeight / messagePixels);
            const maxIndex = messagePerContainer > messageLength ? 0 : (messageLength - messagePerContainer);

            if (pinIndex > maxIndex)
                pinIndex = maxIndex;

            loadMessagesForContainer(tbody, pinIndex);
            onMessageLogPin({ target: tbody.querySelector(`span[data-msg-id="${pinMsg.id}"]`), isScriptSwitch: true, scrollToOffset: pinIndex * messagePixels });
        } else {
            loadMessagesForContainer(tbody, 0);
            tbody.dataset.pinnedMsgId = pinMsg.id;
        }
    } else {
        loadMessagesForContainer(tbody, 0);
    }
}
function navigate(href) {
    var a = document.createElement('a');
    a.href = href;
    a.setAttribute('target', '_blank');
    a.click();
}

function exportIconClick(e) {
    const target = e.target;
    let tableLogs = target.parentElement;
    while (tableLogs.tagName != "DIV" || !tableLogs.classList.contains('position-relative')) {
        tableLogs = tableLogs.parentElement;
    }

    const container = tableLogs.querySelector('div[data-table-log-date]');

    if (!tableLogs)
        return;

    const messages = userLogsData[container.dataset.userId + container.dataset.date];
    if (!messages)
        return;

    const indexes = userLogsDataIndexes[container.dataset.userId + container.dataset.date];
    const messageLength = indexes ? indexes.length : messages.length;

    const blobData = [];

    for (let i = 0; i < messageLength; i++) {
        const message = messages[indexes ? indexes[i] : i];

        if (message.type == 'MESSAGE') {
            blobData.push(`[${message.dateStr}] ${message.channel} ${message.params.displayName}: ${message.params.message}\n`);
        } else if (message.type == 'BAN') {
            blobData.push(`[${message.dateStr}] ${message.channel} :${message.params.user}: ${message.params.user} has been ${message.params.duration ? `timed out for ${message.params.duration} seconds.` : 'permanently banned!'}\n`);
        }
    }

    const blob = new Blob(blobData, { type: "text/plain;charset=utf8" });
    const url = URL.createObjectURL(blob);

    navigate(url);
}

function getExportIconElement() {
    const exportElement = document.createElement('span');
    exportElement.classList.add('txt');
    exportElement.innerHTML = '<svg class="txt" height="32" viewBox="0 0 32 32" width="32"><title></title><path d="M21 26v2.003A1.995 1.995 0 0119.003 30H3.997A2 2 0 012 27.993V5.007C2 3.898 2.9 3 4.009 3H14v6.002c0 1.111.898 1.998 2.006 1.998H21v2h-8.993A3.003 3.003 0 009 15.999V23A2.996 2.996 0 0012.007 26H21zM15 3v5.997c0 .554.451 1.003.99 1.003H21l-6-7zm-3.005 11C10.893 14 10 14.9 10 15.992v7.016A2 2 0 0011.995 25h17.01C30.107 25 31 24.1 31 23.008v-7.016A2 2 0 0029.005 14h-17.01zM14 17v6h1v-6h2v-1h-5v1h2zm6 2.5L18 16h1l1.5 2.625L22 16h1l-2 3.5 2 3.5h-1l-1.5-2.625L19 23h-1l2-3.5zm6-2.5v6h1v-6h2v-1h-5v1h2z" fill="#929292" fill-rule="evenodd"></path></svg>';
    exportElement.addEventListener('click', exportIconClick);

    return exportElement;
}

function getChannelLogsSuccess(data) {
    const mainContainer = document.getElementById('channelLogsContainer');
    mainContainer.innerHTML = '';

    const keys = Object.keys(userLogsData);
    for (let i = 0; i < keys.length; i++) {
        if (keys[i].endsWith('channel')) {
            delete userLogsData[keys[i]];
            break;
        }
    }

    let filters = [];

    const dateInput = document.getElementById('channelLogsTab2').querySelector('form input[type="date"]');
    if (!dateInput.dataset.isScriptSwitch) {
        const newUrl = `?subtab=channelLogs&date=${encodeURIComponent(dateInput.value)}`;
        window.history.replaceState(null, null, newUrl);
        lastChannelLogUrl = newUrl;
    } else {
        delete dateInput.dataset.isScriptSwitch;
        filters = getFiltersForChannelLogs();
    }

    const logContainer = document.createElement('div');
    logContainer.classList.add('container-logs', 'border');
    logContainer.innerHTML = `<div data-table-log-date="channel"></div>`;

    const exportElement = getExportIconElement();

    mainContainer.appendChild(exportElement);
    mainContainer.appendChild(logContainer);

    logContainer.querySelector('div[data-table-log-date]').dataset.channelLogDate = dateInput.value;

    createLogsForData(logContainer, data, channelId, channelLogin, 'channel', dateInput.dataset.msg, filters);
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

                const mainLogContainer = document.createElement('div');
                mainLogContainer.classList.add('position-relative');

                const exportElement = getExportIconElement();
                mainLogContainer.appendChild(exportElement);

                const logContainer = document.createElement('div');
                logContainer.classList.add('container-logs', 'border');
                logContainer.innerHTML = `<div data-table-log-date="${logTime}"></div>`;

                mainLogContainer.appendChild(logContainer);

                const replaceMe = target.tagName === 'DIV' ? target : target.parentElement;
                replaceMe.replaceWith(mainLogContainer);

                dataPromise.then((data) => {
                    createLogsForData(logContainer, data, userId, userLogin, logTime, e.pinMessageAfetrLog, null);
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

    const keys = Object.keys(userLogsData);
    for (let i = 0; i < keys.length; i++) {
        if (keys[i].endsWith('channel')) {
            continue;
        }

        delete userLogsData[keys[i]];
    }

    const userLogin = document.querySelector('form[data-callback="getUserLogsTimesSuccess"] input[type="text"]').value;

    json.data.sort((a, b) => a.localeCompare(b));

    for (let i = (json.data.length - 1); i >= 0; i--) {
        const dateStr = json.data[i];
        const div = document.createElement('div');
        div.classList.add('text-center');
        const button = document.createElement('button');
        button.setAttribute('type', 'button');
        button.classList.add('btn', 'btn-primary', 'm-2');
        button.innerText = dateStr;
        button.dataset.time = dateStr;
        button.dataset.userLogin = userLogin;
        button.dataset.userId = json.userId;
        button.dataset.roomId = json.roomId;
        button.addEventListener('click', loadUserLogs);
        div.appendChild(button);
        container.appendChild(div);
    }

    const newUrl = `?subtab=userLogs&userLogin=${encodeURIComponent(userLogin)}&userId=${encodeURIComponent(json.userId)}`;
    window.history.replaceState(null, null, newUrl);
    lastUserLogUrl = newUrl;
}

function convertUserDataToObject(userData) {
    const usersData = {};
    for (let i = 0; i < userData.length; i++) {
        const item = userData[i];
        const currentItem = usersData[item.userId];
        if (currentItem && currentItem.recordInsertTime > item.recordInsertTime) {
            continue;
        }

        usersData[item.userId] = item;
    }

    return usersData;
}

function updateUrlChannelStats() {
    const form = document.querySelector('form[data-callback="getTopWordsSuccess"]');
    const year = form.querySelector('input[data-form-name="year"]').value;

    const formWordCount = document.querySelector('form[data-callback="getWordCountSuccess"]');
    const wordCountWord = formWordCount.querySelector('input[data-form-name="word"]').value;
    const userCountWord = formWordCount.querySelector('input[data-form-name="user"]').value;

    const formUserStats = document.querySelector('form[data-callback="getUserStatsSuccess"]');
    const userStats = formUserStats.querySelector('input[data-form-name="user"]').value;

    let urlData = `&year=${encodeURIComponent(year)}`;

    if (wordCountWord)
        urlData += `&word=${encodeURIComponent(wordCountWord)}`;

    if (userCountWord)
        urlData += `&user=${encodeURIComponent(userCountWord)}`;

    if (userStats)
        urlData += `&suser=${encodeURIComponent(userStats)}`;

    window.history.replaceState(null, null, `?subtab=channelStats${urlData}`);
}

function getTopUsersSuccess(json) {
    const form = document.querySelector('form[data-callback="getTopUsersSuccess"]');
    const year = form.querySelector('input[data-form-name="year"]').value;
    const word = form.querySelector('input[data-form-name="word"]').value;

    window.history.replaceState(null, null, `?subtab=wordUsers&year=${encodeURIComponent(year)}&word=${encodeURIComponent(word)}`);

    const tbody = document.getElementById('tableWordUsers').querySelector('tbody');
    tbody.innerHTML = '';

    const usersData = convertUserDataToObject(json.userData);

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

function getTopChattersSuccess(json) {
    const usersData = convertUserDataToObject(json.userData);

    const tbody = document.getElementById('tableUsers').querySelector('tbody');
    tbody.innerHTML = '';

    for (let i = 0; i < json.data.length; i++) {
        const item = json.data[i];

        const tr = document.createElement('tr');
        const th = document.createElement('th');
        th.setAttribute('scope', 'row');
        th.innerText = (i + 1);

        const tdUser = document.createElement('td');
        const tdMessages = document.createElement('td');
        const tdWords = document.createElement('td');
        const tdChars = document.createElement('td');

        tdUser.innerText = usersData[item.userId]?.displayName ?? item.userId;
        tdMessages.innerText = item.messages.toLocaleString();
        tdWords.innerText = item.words.toLocaleString();
        tdChars.innerText = item.chars.toLocaleString();

        tr.appendChild(th);
        tr.appendChild(tdUser);
        tr.appendChild(tdMessages);
        tr.appendChild(tdWords);
        tr.appendChild(tdChars);
        tbody.appendChild(tr);
    }
}

function getTopWordsSuccess(json) {
    updateUrlChannelStats();

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

function getUserStatsSuccess(json) {
    if (!json.user)
        return;

    updateUrlChannelStats();

    const userStatsText = document.getElementById('userStatsText');
    userStatsText.innerText = `User "${json.user}" Messages: ${json.messages.toLocaleString()} Words: ${json.words.toLocaleString()} Chars: ${json.chars.toLocaleString()}`;
}

function getWordCountSuccess(json) {
    updateUrlChannelStats();

    const wordCountText = document.getElementById('wordCountText');
    wordCountText.innerText = `Word "${json.word}" was used${(json.user ? ` by "${json.user}"` : '')} ${json.count.toLocaleString()} times`;
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

            if (form.checkValidity()) {
                form.requestSubmit(form.querySelector('button[type="submit"]'));
            }
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
        } else if (subTabName == 'channelLogs') {
            newUrl = lastChannelLogUrl;
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

            {
                const word = url.searchParams.get('word');
                const user = url.searchParams.get('user');
                const form = document.querySelector('form[data-callback="getWordCountSuccess"]');

                if (word)
                    form.querySelector('input[data-form-name="word"]').value = word;

                if (user)
                    form.querySelector('input[data-form-name="user"]').value = user;
            }

            {
                const formUserStats = document.querySelector('form[data-callback="getUserStatsSuccess"]');
                const suser = url.searchParams.get('suser');

                if (suser)
                    formUserStats.querySelector('input[data-form-name="user"]').value = suser;
            }

            const currentYearSwitch = document.querySelector(`span[data-year-switch-for="channelStats"][data-year-switch="${year ?? '0'}"]`);
            onYearSwitch({ target: currentYearSwitch });

        }
    } else if (subTabName == 'userLogs' || subTabName == 'channelLogs') {
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

                    document.getElementById('channelLogsTab1').classList.add('d-none');
                    document.getElementById('channelLogsTab2').classList.remove('d-none');

                    if (e.isScriptSwitch && subTabName == 'channelLogs') {
                        const url = new URL(window.location.href);
                        lastChannelLogUrl = '?' + url.searchParams.toString();

                        const logTime = url.searchParams.get('date');
                        const msg = url.searchParams.get('msg');
                        if (logTime) {
                            const logTimeInput = document.querySelector('form[data-callback="getChannelLogsSuccess"] input[type="date"]');
                            if (logTimeInput) {
                                logTimeInput.dataset.isScriptSwitch = 'true';
                                logTimeInput.value = logTime;
                                if (msg) {
                                    logTimeInput.dataset.msg = msg;
                                }
                            }
                        }

                        const filtersContainer = document.getElementById('channelLogFilters');

                        const filterUser = url.searchParams.get('filterUser');
                        if (filterUser) {
                            filtersContainer.querySelector('textarea').value = filterUser.split(',').join('\n');
                        }

                        const form = document.querySelector('form[data-callback="getChannelLogsSuccess"]');
                        form.requestSubmit(form.querySelector('button[type="submit"]'));

                    } else if (e.isScriptSwitch && subTabName == 'userLogs') {
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