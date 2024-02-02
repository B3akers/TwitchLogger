const englishTranslation = {
    'invalid_model': 'Wrong data',
    'channel_not_found': 'Channel not found',
    'channel_already_added': 'Channel already added',
    'channel_added': "Channel has been added",
    'channel_deleted': 'Channel has been deleted',
    'user_not_found': 'User not found',
    'not_found': "Not found",
    'server_error': 'Something went wrong, please try again'
};

const friendlyHttpStatus = {
    '200': 'OK',
    '201': 'Created',
    '202': 'Accepted',
    '203': 'Non-Authoritative Information',
    '204': 'No Content',
    '205': 'Reset Content',
    '206': 'Partial Content',
    '300': 'Multiple Choices',
    '301': 'Moved Permanently',
    '302': 'Found',
    '303': 'See Other',
    '304': 'Not Modified',
    '305': 'Use Proxy',
    '306': 'Unused',
    '307': 'Temporary Redirect',
    '400': 'Bad Request',
    '401': 'Unauthorized',
    '402': 'Payment Required',
    '403': 'Forbidden',
    '404': 'Not Found',
    '405': 'Method Not Allowed',
    '406': 'Not Acceptable',
    '407': 'Proxy Authentication Required',
    '408': 'Request Timeout',
    '409': 'Conflict',
    '410': 'Gone',
    '411': 'Length Required',
    '412': 'Precondition Required',
    '413': 'Request Entry Too Large',
    '414': 'Request-URI Too Long',
    '415': 'Unsupported Media Type',
    '416': 'Requested Range Not Satisfiable',
    '417': 'Expectation Failed',
    '418': 'I\'m a teapot',
    '429': 'Too Many Requests',
    '500': 'Internal Server Error',
    '501': 'Not Implemented',
    '502': 'Bad Gateway',
    '503': 'Service Unavailable',
    '504': 'Gateway Timeout',
    '505': 'HTTP Version Not Supported',
};

function translateCode(code) {
    return englishTranslation[code] ?? code;
}

function getPostRequestOptions(jsonData) {
    return {
        method: 'POST',
        mode: 'cors',
        cache: 'no-cache',
        credentials: 'same-origin',
        headers: {
            'Content-Type': 'application/json',
            'X-Csrf-Token-Value': csrfToken
        },
        redirect: 'follow',
        referrerPolicy: 'no-referrer',
        body: JSON.stringify(jsonData)
    };
}

function formatBytes(bytes, decimals = 2) {
    if (!+bytes) return '0 Bytes'

    const k = 1024
    const dm = decimals < 0 ? 0 : decimals
    const sizes = ['Bytes', 'KiB', 'MiB', 'GiB', 'TiB', 'PiB', 'EiB', 'ZiB', 'YiB']

    const i = Math.floor(Math.log(bytes) / Math.log(k))

    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`
}

function confirmObjectDelete(e) {
    e.setAttribute('disabled', '');

    fetch(e.dataset.url, getPostRequestOptions({
        'id': e.dataset.id
    })).then((response) => response.json())
        .then((json) => {
            if (json.error) {
                toastr.error(translateCode(json.error));
                return;
            }
            toastr.success(translateCode(json.success));
            if (e.dataset.datatable) {
                window[e.dataset.datatable].ajax.reload();
            }
        }).catch((error) => {
            console.error(error);
        }).finally(() => {
            $('#confirmModal').modal('hide');
            e.removeAttribute('disabled');
        });
}

const subTierDescriptor = {
    '1000': 'Sub Tier 1',
    '2000': 'Sub Tier 2',
    '3000': 'Sub Tier 3'
};

(function () {
    const printSpans = document.querySelectorAll('span[data-print-type]');
    for (let i = 0; i < printSpans.length; i++) {
        const span = printSpans[i];
        const value = span.dataset.value;
        if (span.dataset.printType == 'date') {
            span.innerText = new Date(parseInt(value) * 1000).toLocaleString();
        } else if (span.dataset.printType == 'number') {
            span.innerText = parseInt(value).toLocaleString();
        } else if (span.dataset.printType == 'bytes') {
            span.innerText = formatBytes(value).toLocaleString();
        } else if (span.dataset.printType == 'subTier') {
            span.innerText = subTierDescriptor[value] ?? value;
        }
    }

    const forms = document.querySelectorAll('form');
    Array.prototype.slice.call(forms)
        .forEach(function (form) {
            if (!form.dataset.url || !form.dataset.callback)
                return;

            form.addEventListener('submit', function (event) {
                event.submitter.setAttribute('disabled', '');

                event.preventDefault();
                event.stopPropagation();

                let jsonData = {};
                const inputs = event.target.querySelectorAll('input');
                for (let i = 0, len = inputs.length; i < len; i++) {
                    const input = inputs[i];
                    const name = input.dataset.formName;
                    const type = input.dataset.formType;
                    const convertType = input.dataset.formConvertType;
                    if (!name)
                        continue;

                    if (type) {
                        if (type == 'bool')
                            jsonData[name] = input.checked;
                        else if (type == 'date') {
                            const dt = new Date(input.value || Date.now());
                            jsonData[name] = dt.getTime() / 1000;
                        }
                    } else {
                        jsonData[name] = input.value;
                    }

                    if (convertType) {
                        if (convertType == 'float')
                            jsonData[name] = parseFloat(jsonData[name]);
                    }
                }

                if (event.target.dataset.callbackBefore)
                    jsonData = window[event.target.dataset.callbackBefore](jsonData);

                const isRaw = event.target.dataset.raw == 'true';

                fetch(event.target.dataset.url, getPostRequestOptions(jsonData))
                    .then((response) => {
                        if (isRaw) {
                            if (response.status < 200 || response.status >= 300) {
                                toastr.error(friendlyHttpStatus[response.status] ?? 'Unknown');
                            }
                            return response.text();
                        }
                        return response.json();
                    }).then((json) => {
                        if (!isRaw && json.error) {
                            toastr.error(translateCode(json.error));
                            return;
                        }

                        if (event.target.dataset.callback && json)
                            window[event.target.dataset.callback](json);
                    }).catch((error) => {
                        toastr.error(translateCode('server_error'));
                        console.error(error);
                    })
                    .finally(() => {
                        event.submitter.removeAttribute('disabled');
                    });
            }, false)
        });
})();