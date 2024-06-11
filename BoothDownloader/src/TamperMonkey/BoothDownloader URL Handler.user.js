// ==UserScript==
// @name         BoothDownloader URL Handler
// @namespace    http://tampermonkey.net/
// @version      1.0.1
// @description  Adds url handler button to booth.pm
// @author       Myrkur
// @supportURL   https://github.com/Myrkie/BoothDownloader/
// @match        https://*.booth.pm/*
// @match        https://booth.pm/*
// @grant        GM_cookie
// ==/UserScript==

(function() {
    'use strict';

    function addButton(item, buttonText, targetUrl) {
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


    function addTokenButton(item, buttonText) {
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

        if (window.location.href.includes('accounts.booth.pm') || window.location.href.includes('manage.booth.pm'))
        {
            console.log("[BoothDownloader] constructing global navigation bar buttons");
            let globalNav = document.querySelector('.global-nav.shrink');
            if(globalNav)
            {
                addTokenButton(globalNav, 'Token')
                addButton(globalNav, 'Owned', 'owned');
                addButton(globalNav, 'Orders Only', 'orders');
                addButton(globalNav, 'Gifts Only', 'gifts');
                console.log("[BoothDownloader] added library buttons");
            }
        }
        if (window.location.href.includes('booth.pm') && window.location.href.includes('items') && !window.location.href.includes('account'))
        {
            console.log("[BoothDownloader] Created Item button");
            let item = document.querySelector('.item-search-box.flex.w-full.max-w-\\[600px\\].box-border');
            if(item)
            {
                var trimid = window.location.href.lastIndexOf('/');

                var parsedid = window.location.href.substring(trimid + 1);

                addButton(item, 'Download', parsedid);
                console.log("[BoothDownloader] Parsed ID: " + parsedid);
            }
        }

        console.log("[BoothDownloader] everything...  seems to be in order");
    });
})();