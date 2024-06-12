// ==UserScript==
// @name         BoothDownloader URL Handler
// @namespace    http://tampermonkey.net/
// @version      1.1.2
// @updateURL    https://raw.githubusercontent.com/Myrkie/BoothDownloader/master/BoothDownloader/src/TamperMonkey/BoothDownloader%20URL%20Handler.user.js
// @downloadURL  https://raw.githubusercontent.com/Myrkie/BoothDownloader/master/BoothDownloader/src/TamperMonkey/BoothDownloader%20URL%20Handler.user.js
// @description  Adds url handler button to booth.pm
// @author       Myrkur
// @supportURL   https://github.com/Myrkie/BoothDownloader/
// @match        https://*.booth.pm/*
// @match        https://booth.pm/*
// @match        https://boothplorer.com/avatar/*
// @grant        GM_cookie
// ==/UserScript==

(function() {
    'use strict';
    
    function addButtonboothplorer(item, buttonText, targetUrl) {
        let button = document.createElement('button');
        button.innerText = buttonText;

        button.classList.add(
            'font-semibold', 'text-center',
            'shadow-inner', 'hover:shadow-black/75',
            'transition-all', 'bg-[#fc4d50]', 'text-white',
            'rounded-full', 'px-4', 'py-1', 'flex',
            'justify-center', 'gap-x-2', 'items-center'
        );

        button.addEventListener('click', function() {

            let uriPath = new URL("boothdownloader://open/?id=" + targetUrl);

            window.open(uriPath, '_self');
        });

        item.appendChild(button);
    }

    function addButtonbooth(item, buttonText, targetUrl) {
        let button = document.createElement('button');
        button.innerText = buttonText;

        button.classList.add(
            'flex', 'justify-between', 'box-border',
            'items-center', 'cursor-pointer',
            'rounded-r-[5px]', 'border-y',
            'border-r', 'h-[32px]', 'px-12',
            'bg-white', 'hover:opacity-80',
            'disabled:opacity-[0.34]',
            'disabled:hover:opacity-[0.34]'
        );

        button.addEventListener('click', function() {

            let uriPath = new URL("boothdownloader://open/?id=" + targetUrl);

            window.open(uriPath, '_self');
        });

        item.appendChild(button);
    }


    function addTokenButtonbooth(item, buttonText) {
        let button = document.createElement('button');
        button.innerText = buttonText;

        button.classList.add(
            'flex', 'justify-between', 'box-border',
            'items-center', 'cursor-pointer',
            'rounded-r-[5px]', 'border-y',
            'border-r', 'h-[32px]', 'px-12',
            'bg-white', 'hover:opacity-80',
            'disabled:opacity-[0.34]',
            'disabled:hover:opacity-[0.34]'
        );

        button.addEventListener('click', function() {
            // this is done on click as booth does a token exchange after load
            // and getting the cookie before load completion returns an invalid cooking
            GM_cookie.list(
                { url: "https://*.booth.pm/*", name: "_plaza_session_nktz7u" },
                (cookie, error) => {
                    if (!error)
                    {
                        let uriPath = new URL("boothdownloader://open/?token=" + cookie[0].value);

                        window.open(uriPath, '_self');
                    }
                    else console.error(error);
                }
            );
        });

        item.appendChild(button);
    }

    window.addEventListener('load', function() {

        if (window.location.href.includes('booth.pm')){
            if (window.location.href.includes('accounts.booth.pm') || window.location.href.includes('manage.booth.pm'))
            {
                console.log("[BoothDownloader:booth] constructing global navigation bar buttons");
                let globalNavbooth = document.querySelector('.global-nav.shrink');
                if(globalNavbooth)
                {
                    addTokenButtonbooth(globalNavbooth, 'Token')
                    addButtonbooth(globalNavbooth, 'Owned', 'owned');
                    addButtonbooth(globalNavbooth, 'Orders Only', 'orders');
                    addButtonbooth(globalNavbooth, 'Gifts Only', 'gifts');
                    console.log("[BoothDownloader:booth] added library buttons");
                }
            }
            if (window.location.href.includes('items') && !window.location.href.includes('account'))
            {
                console.log("[BoothDownloader:booth] Created Item button");
                let itembooth = document.querySelector('.item-search-box.flex.w-full.max-w-\\[600px\\].box-border');
                if(itembooth)
                {
                    var trimidbooth = window.location.href.lastIndexOf('/');

                    var parsedidbooth = window.location.href.substring(trimidbooth + 1);

                    addButtonbooth(itembooth, 'Download', parsedidbooth);
                    console.log("[BoothDownloader:booth] Parsed ID: " + parsedidbooth);
                }
            }
        }

        if (window.location.href.includes('boothplorer.com')){
            if (window.location.href.includes('/avatar/'))
            {
                console.log("[BoothDownloader:boothplorer] constructing global navigation bar buttons");
                let globalNavboothplorer = document.querySelector('div[class*="flex-col"][class*="flex-wrap"][class*="gap-y-4"][class*="2xl\\:gap-y-0"][class*="2xl\\:flex-row"][class*="2xl\\:gap-x-4"]');
                if(globalNavboothplorer)
                {
                    console.log("[BoothDownloader:boothplorer] added download button");

                    var trimidboothplorer = window.location.href.lastIndexOf('/');

                    var parsedidboothplorer = window.location.href.substring(trimidboothplorer + 1);

                    addButtonboothplorer(globalNavboothplorer, 'DOWNLOAD', parsedidboothplorer);
                    console.log("[BoothDownloader:boothplorer] Parsed ID: " + parsedidboothplorer);
                }
            }
        }
        console.log("[BoothDownloader:core] everything...  seems to be in order");
    });
})();