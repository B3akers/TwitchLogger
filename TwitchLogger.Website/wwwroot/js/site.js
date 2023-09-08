const englishTranslation = {
    'invalid_model': 'Wrong data',
    'channel_not_found': 'Channel not found',
    'channel_already_added': 'Channel already added',
    'channel_added': "Channel has been added",
    'channel_deleted': 'Channel has been deleted',
    'server_error': 'Something went wrong, please try again'
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
            if (channelsTableDatatable)
                channelsTableDatatable.ajax.reload();
        }).catch((error) => {
            console.error(error);
        }).finally(() => {
            $('#confirmModal').modal('hide');
            e.removeAttribute('disabled');
        });
}

(function () {
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

                fetch(event.target.dataset.url, getPostRequestOptions(jsonData))
                    .then((response) => response.json()).then((json) => {
                        if (json.error) {
                            toastr.error(translateCode(json.error));
                            return;
                        }
                        if (event.target.dataset.callback)
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