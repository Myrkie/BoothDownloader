// ==UserScript==
// @name         BoothDownloader URL Handler
// @namespace    http://tampermonkey.net/
// @version      1.1.6
// @updateURL    https://raw.githubusercontent.com/Myrkie/BoothDownloader/master/BoothDownloader/src/TamperMonkey/BoothDownloader%20URL%20Handler.user.js
// @downloadURL  https://raw.githubusercontent.com/Myrkie/BoothDownloader/master/BoothDownloader/src/TamperMonkey/BoothDownloader%20URL%20Handler.user.js
// @description  Adds url handler button to booth.pm
// @author       Myrkur
// @icon         https://www.google.com/s2/favicons?domain=booth.pm
// @supportURL   https://github.com/Myrkie/BoothDownloader/
// @match        https://*.booth.pm/*
// @match        https://booth.pm/*
// @match        https://boothplorer.com/avatar/*
// @grant        GM_cookie
// ==/UserScript==

(function() {
    'use strict';
    const boothdownloaderOpen = "boothdownloader://open/"
    
    window.addEventListener('load', function() {
        logMessage("core", "starting up...");
//#region boothpm
        if (window.location.href.includes('booth.pm')){
            if (window.location.href.includes('accounts.booth.pm') || window.location.href.includes('manage.booth.pm'))
            {
                logMessage("booth", "constructing global navigation bar buttons");
                let globalNavbooth = document.querySelector('div.flex.flex-terminal.items-center.shrink');
                if(globalNavbooth)
                {
                    addTokenButtonbooth(globalNavbooth, 'Token')
                    addButtonbooth(globalNavbooth, 'Owned', 'owned');
                    addButtonbooth(globalNavbooth, 'Orders Only', 'orders');
                    addButtonbooth(globalNavbooth, 'Gifts Only', 'gifts');
                    addPathButtonbooth(globalNavbooth, 'Open Download Path', false);
                    logMessage("booth", "added library buttons");
                }
            }
            
            if (window.location.href.includes('items') && !window.location.href.includes('account'))
            {
                logMessage("booth", "Created Item button");
                let itembooth = document.querySelector('.item-search-box.flex.w-full.max-w-\\[600px\\].box-border');
                if(itembooth)
                {
                    const trimidbooth = window.location.href.lastIndexOf('/');

                    const parsedidbooth = window.location.href.substring(trimidbooth + 1);

                    addButtonbooth(itembooth, 'Download', parsedidbooth);
                    addPathButtonbooth(itembooth, 'Open Download Path',true);
                    logMessage("booth", "Parsed ID: " + parsedidbooth);
                }
            }
        }
//#endregion

//#region boothplorer
        if (window.location.href.includes('boothplorer.com')){
            if (window.location.href.includes('/avatar/'))
            {
                logMessage("boothplorer", "constructing global navigation bar buttons");
                let globalNavboothplorer = document.querySelector('.flex.flex-col.flex-wrap.justify-center.gap-4');
                if(globalNavboothplorer)
                {
                    logMessage("boothplorer", "added download button");

                    const trimidboothplorer = window.location.href.lastIndexOf('/');

                    const parsedidboothplorer = window.location.href.substring(trimidboothplorer + 1);

                    addButtonboothplorer(globalNavboothplorer, 'DOWNLOAD', parsedidboothplorer);
                    logMessage("boothplorer", "Parsed ID: " + parsedidboothplorer);
                }
            }
        }
//#endregion
        logMessage("core", "everything...  seems to be in order");
    });

//#region boothpmbuttons
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

            let uriPath = new URL(`${boothdownloaderOpen}?id=${targetUrl}`);

            window.open(uriPath, '_self');
        });

        item.appendChild(button);
    }

    function addPathButtonbooth(item, buttonText, shouldSetWidth) {
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
        if (shouldSetWidth) {
            button.style.width = "350px";
        }

        button.addEventListener('click', function() {
            let uriPath = new URL(`${boothdownloaderOpen}?path=UwU`);
            console.log(uriPath);

            window.open(uriPath, '_self');
        });

        item.appendChild(button);
    }
//#endregion
    
//#region boothplorerbuttons
    function addButtonboothplorer(item, buttonText, targetUrl) {
        let button = document.createElement('button');
        button.innerText = buttonText;

        button.classList.add(
            'shadow-inner', 'hover:shadow-black/50',
            'group', 'bg-[#fc4d50]', 'text-white',
            'rounded-md', 'px-4', 'py-1', 'flex',
            'justify-center', 'gap-x-2', 'items-center'
        );

        button.addEventListener('click', function() {

            let uriPath = new URL(`${boothdownloaderOpen}?id=${targetUrl}`);

            window.open(uriPath, '_self');
        });

        item.appendChild(button);
    }
//#endregion
    
//#region misc
    function logMessage(path, message) {
        const timestamp = new Date().toISOString();
        console.log(`[${timestamp}] [BoothDownloader:${path}] ${message}`);
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
                        let uriPath = new URL(`${boothdownloaderOpen}?token=${cookie[0].value}`);

                        window.open(uriPath, '_self');
                    }
                    else console.error(error);
                }
            );
        });
        item.appendChild(button);
    }
//#endregion
})();