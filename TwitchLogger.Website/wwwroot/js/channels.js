var channelsTableDatatable = $('#channelsTable').DataTable({
    "pageLength": 25,
    "ajax": getChannelsUrl,
    "columns": [
        {
            "data": "logoUrl", render: function (data, type, row) {
                if (type === "display") {
                    return `<img src="${data}" height="30" width="30"/>`;

                }
                return data;
            }
        },
        { "data": "login" },
        { "data": "displayName" },
        { "data": "userId" },
        {
            "data": "id", orderable: false, render: function (data, type, row) {
                if (type === "display") {
                    return `<i role="button" style="color: #ff0000;" data-id="${data}" onclick="deleteChannelClick(this)" class="fas fa-times m-1"></i>`;
                }
                return data;
            }
        },
    ]
});

function deleteChannelClick(e) {
    document.getElementById('confirmModalButtonYes').dataset.id = e.dataset.id;
    document.getElementById('confirmModalButtonYes').dataset.url = deleteChannelUrl;
    document.getElementById('confirmModalButtonYes').dataset.callback = 'confirmObjectDelete';

    $('#confirmModal').modal('show');
}

function addChannelResponse(json) {
    $('#channelModal').modal('hide');
    toastr.success(translateCode(json.success));
    if (channelsTableDatatable)
        channelsTableDatatable.ajax.reload();
}