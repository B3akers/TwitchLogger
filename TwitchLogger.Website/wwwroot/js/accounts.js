function deleteAccountClick(e) {
    document.getElementById('confirmModalButtonYes').dataset.id = e.dataset.id;
    document.getElementById('confirmModalButtonYes').dataset.url = deleteAccountUrl;
    document.getElementById('confirmModalButtonYes').dataset.datatable = 'accountsTableDatatable';
    document.getElementById('confirmModalButtonYes').dataset.callback = 'confirmObjectDelete';

    $('#confirmModal').modal('show');
}

function addAccountResponse(json) {
    $('#accountModal').modal('hide');
    toastr.success(translateCode(json.success));
    if (accountsTableDatatable)
        accountsTableDatatable.ajax.reload();
}

var accountsTableDatatable = $('#accountsTable').DataTable({
    "pageLength": 25,
    "ajax": getAccountsUrl,
    "columns": [
        { "data": "login" },
        {
            "data": "creationTime", render: function (data, type, row) {
                if (type === "display") {
                    return new Date(parseInt(data) * 1000).toISOString().
                        replace(/T/, ' ').
                        replace(/\..+/, '');

                }
                return data;
            }
        },
        {
            "data": "isModerator", render: function (data, type, row) {
                if (type === "display") {
                    return data ? '<i class="fas fa-check" color="#0ff000"/>' : '<i class="fas fa-times" color="#ff0000"/>';
                }
                return data;
            }
        },
        {
            "data": "isAdmin", render: function (data, type, row) {
                if (type === "display") {
                    return data ? '<i class="fas fa-check" color="#0ff000"/>' : '<i class="fas fa-times" color="#ff0000"/>';
                }
                return data;
            }
        },
        {
            "data": "id", orderable: false, render: function (data, type, row) {
                if (type === "display") {
                    return `<i role="button" style="color: #ff0000;" data-id="${data}" onclick="deleteAccountClick(this)" class="fas fa-times m-1"></i>`;
                }
                return data;
            }
        },
    ]
});