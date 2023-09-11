(function () {
    fetch(getChannelsUrl).then(x => x.json()).then(json => {
        json.data.sort((a, b) => a.displayName.localeCompare(b.displayName));

        const list = document.getElementById('channelList');
        for (let i = 0; i < json.data.length; i++) {
            const channel = json.data[i];

            const li = document.createElement('li');
            li.classList.add('list-item');

            const a = document.createElement('a');
            a.classList.add('d-block');
            a.setAttribute('href', `${channelUrl}/${channel.userId}`);

            const img = document.createElement('img');
            img.setAttribute('src', channel.logoUrl);
            img.classList.add('avatar-img');

            const h5 = document.createElement('h5');
            h5.classList.add('d-inline-block', 'channel-name', 'align-middle');
            h5.innerText = channel.displayName;

            a.appendChild(img);
            a.appendChild(h5);
            li.appendChild(a);
            list.appendChild(li);
        }
    }).catch((error) => {
        toastr.error(translateCode('server_error'));
        console.error(error);
    });
})();