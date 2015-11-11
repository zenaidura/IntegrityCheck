'use strict';
var site,
    web,
    hostUrl,
    list,
    items,
    pageUrl,
    userId,
    loc;

// This code runs when the DOM is ready and creates a context object which is needed to use the SharePoint object model
$(document).ready(function () {
    init();
    getDraftContent();
});

function init() {

    console.log("Initializing App Context..");
    site = $.getQueryStringValue("site");
    hostUrl = $.getQueryStringValue("hostUrl");
    web = $.getQueryStringValue("web");
    list = $.getQueryStringValue("list");
    items = $.getQueryStringValue("items");
    userId = $.getQueryStringValue("user");
    loc = $.getQueryStringValue("loc") || "en-us";
    if (list == 'undefined' || list == "null" || list == null || list == undefined) {
        //Get the publishing page url
        pageUrl = document.referrer.substring(document.referrer.lastIndexOf('/') + 1, document.referrer.indexOf('?') > 0 ? document.referrer.indexOf('?') : document.referrer.length);
        //Check if its a friendly url. Make sure it does not have extension
        if (pageUrl != "" && pageUrl.indexOf('.') == -1) {
            //its a friendly url
            pageUrl = pageUrl.concat('.aspx');
        }
    }

    if (items != 'undefined' && items != "") {
        items = items.replace(/,\s*$/, "").split(',');
    }
    else
    {
        var url = document.referrer;
        items = [$.getQueryStringValue("ID", url)];
    }
}

function getDraftContent() {
    DraftContentViewModel.init(site, web, list, items, pageUrl, document.getElementById("draftContentMain"));
}

function closeDlg() {
    try {
        window.parent.postMessage('CloseCustomActionDialogNoRefresh', '*');

        if (window.parent.frameElement && window.parent.frameElement.commonModalDialogClose) {
            window.parent.frameElement.commonModalDialogClose();
            return;
        }
    }
    catch (e) { }
    try {
        if (window.frameElement && window.frameElement.cancelPopUp) {
            window.frameElement.cancelPopUp();
        }
        else {
            window.parent.close();
        }
    }
    catch (e) {
        window.parent.close();
    }
}

if (!String.format) {
    String.format = function (format) {
        var args = Array.prototype.slice.call(arguments, 1);
        return format.replace(/{(\d+)}/g, function (match, number) {
            return typeof args[number] != 'undefined'
              ? args[number]
              : match
            ;
        });
    };
}