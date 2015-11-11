ko.observableArray.fn.distinct = function (prop) {
    var target = this;
    target.index = {};
    target.index[prop] = ko.observable({});

    ko.computed(function () {
        //rebuild index
        var propIndex = {};

        ko.utils.arrayForEach(target(), function (item) {
            var key = ko.utils.unwrapObservable(item[prop]);
            if (key) {
                propIndex[key] = propIndex[key] || [];
                propIndex[key].push(item);
            }
        });

        target.index[prop](propIndex);
    });

    return target;
};

var DraftItem = (function () {
    function DraftItem(draftContent) {
        var self = this;
        self.uniqueID = (((1 + Math.random()) * 0x10000) | 0).toString(16).substring(1);

        self.siteId = draftContent.SiteId != undefined ? draftContent.SiteId : "";
        self.webId = draftContent.WebId;
        self.url = draftContent.IsInternal ? hostUrl + draftContent.Url : draftContent.Url;
        self.relativeUrl = draftContent.Url;
        self.list = draftContent.ListId != undefined ? draftContent.ListId : "";
        self.item = draftContent.ItemId != undefined ? draftContent.ItemId : "";
        self.title = (draftContent.Title || draftContent.Url);
        self.title = !draftContent.IsBroken ? (self.title.length > 60 ? self.title.substring(0, 60) + "..." : self.title) : self.title;
        self.isBroken = draftContent.IsBroken;
        self.isDraft = draftContent.IsDraft;
        self.type = ko.observable(draftContent.FileType);
        self.hasPublishedVersion = draftContent.PreviouslyPublished;
        self.publishingStatus = draftContent.PreviouslyPublished ? "Published Version" : (self.item == "" ? "Draft" : "Pending");
        self.version = draftContent.PreviouslyPublished ? draftContent.LastPublishedVersion : draftContent.DraftVersion;
        self.scheduledDate = draftContent.ScheduledToPublish != undefined && draftContent.ScheduledToPublish != "" ? "Scheduled Start " + draftContent.ScheduledToPublish : "Scheduled Immediately";
        self.publishInFuture = ko.computed(function () {
            var date = new Date(draftContent.ScheduledToPublish);
            return date > Date.now();
        });
        self.selected = ko.observable(false);
        self.statusIcon = draftContent.PreviouslyPublished ? "fa fa-exclamation-triangle" : "fa fa-exclamation-circle";
        self.associatedAssets = ko.observableArray([]).distinct('type');
        self.parent = draftContent.page;
        self.message = ko.observable("");
        self.error = ko.observable(draftContent.LastError);
        self.expirationDate = draftContent.ExpirationDate;
        self.audience = (draftContent.Audience || "").replace(',', ', ');
        self.serviceArea = (draftContent.ServiceArea || "").replace(',', ', ');
        self.checkedOutTo = draftContent.CheckedOutTo;
        self.expiring = ko.computed(function () {
            if (self.expirationDate) {
                var expDate = new Date(self.expirationDate);
                var futureDate = new Date();
                futureDate.setDate(futureDate.getDate() + 30);
                return expDate <= futureDate;
            }
            return false;
        });

        if (draftContent.Assets && draftContent.Assets.length > 0) {
            $.each(draftContent.Assets, function (i, asset) {
                asset.page = self.parent;
                var draftItem = new DraftItem(asset);
                self.associatedAssets.push(draftItem); //each dynamic draft item can have multiple assets.

                if (self.parent.typesOfContent.indexOf(draftItem.type()) < 0) {
                    self.parent.typesOfContent.push(draftItem.type());
                }
            });
        }
        else {
            self.message = "No pending items detected";
        }

        self.select = function (file, event) {
            var page = file.parent;
            var selectAllId = 'chkSelectAll_' + page.id;
            var chkSelectAll = document.getElementById(selectAllId);
            chkSelectAll.indeterminate = true;
            return true;
        };

        self.publish = function (data, event) {
            if (data == undefined) {
                data = this;
            }
            var page = data.parent;
            var vm = page.parent;
            if (data.type() != "Broken Link") {
                vm.publishDraftContent(data).then(function (status) {
                    if (status) {
                        if (page) {
                            setTimeout(page.removeItem, 10, page, data.uniqueID);
                        }
                    }
                });
            }
        };
        self.open = function (data, event) {
            if (data == undefined) {
                data = this;
            }
            window.location.replace(hostUrl + " /" + data.parent.url);
            window.close();
        };
        self.runIntegrityCheck = function (data, event) {
            var url = '/home?site={' + data.siteId + '}&web={' + data.webId + '}&items=' + data.relativeUrl + '&hostUrl=' + hostUrl + '&user=' + userId + '&loc=' + loc;

            $('<div/>', { 'class': 'modal fade', 'id': 'dlgICheck_' + data.uniqueID, 'role': 'dialog' })
            .html($('<div/>', { 'class': 'modal-dialog' })
                .html($('<div/>', { 'class': 'modal-content' })
                    .html($('<div/>', { 'class': 'modal-header' })
                        .html($('<button/>', { 'type': 'button', 'class': 'close', 'data-dismiss': 'modal' }).html('&times;')
                        )
                        .append($('<h4/>').text("Running Intergity Check on " + data.title + " (" + data.relativeUrl + ")"))
                    )
                    .append($('<div/>', { 'class': 'modal-body' })
                        .html($('<iframe/>', { 'src': url, 'style': 'width:100%; height:100%;' })
                        )
                    )
                )
            )
            .appendTo('body');

            var id = '#dlgICheck_' + data.uniqueID;
            $(id).on('hidden.bs.modal', function () {
                // do something…
                var page = data.parent;
                page.parent.getDataInfo(data.siteId, data.webId, data.list, data.item || data.relativeUrl).then(function (item) {
                    if (item.isPublished()) { //if item is published than remove the item
                        setTimeout(page.removeItem, 100, page, data.uniqueID);
                    }
                });
                //data.parent.refresh();
            })
        };
    }
    return DraftItem;
})();

var Page = (function () {
    function Page() {
        self = this;
        self.init = function (val) {
            this.pageTitle = val.Title;
            this.id = val.Id;
            this.siteId = val.SiteId;
            this.webId = val.WebId;
            this.url = hostUrl + val.Url;
            this.relativeUrl = val.Url;
            this.isPublished(!val.IsDraft);
            this.hasPublishedVersion(val.PreviouslyPublished);
            this.publishingStatus(val.PreviouslyPublished ? "Published Version" : (val.FileType == "Static" ? "Draft" : "Pending"));
            this.version(val.PreviouslyPublished ? val.LastPublishedVersion : val.DraftVersion);    
            this.scheduledToPublish(val.ScheduledToPublish);
            this.scheduledDate(val.ScheduledToPublish != undefined && val.ScheduledToPublish != "" ? "Scheduled Start " + val.ScheduledToPublish : "Scheduled Immediately");
            this.expirationDate(val.ExpirationDate);
            this.listId = val.ListId || null;
            this.error(val.LastError);
        };
        self.sortDraftFiles = function () {
            this.draftFiles.sort(function (left, right) { return left.publishingStatus == right.publishingStatus ? 0 : (left.publishingStatus < right.publishingStatus ? -1 : 1) })
        };
        self.parent = null;
        self.isPublished = ko.observable();
        self.id = ko.observable();
        self.url = "";
        self.draftFilesLoaded = ko.observable(false);
        self.pageTitle = ko.observable();
        self.draftFiles = ko.observableArray([]).distinct('type');
        self.selected = ko.observable(false);
        self.hasPublishedVersion = ko.observable();
        self.scheduledToPublish = ko.observable();
        self.approvedOrPublishInFuture = ko.computed(function () {
            if (self.scheduledToPublish()) {
                var date = new Date(self.scheduledToPublish());
                return self.isPublished() || date > Date.now();
            }
            else
                return self.isPublished();
        });
        self.publishingStatus = ko.observable("");
        self.version = ko.observable("");
        self.scheduledDate = ko.observable("");
        self.expirationDate = ko.observable("");
        self.error = ko.observable();
        self.filesInDraftMode = function () {
            return this.draftFiles().filter(function (draftFile) {
                if (draftFile.error() == null && (draftFile.isDraft || draftFile.isBroken || draftFile.expiring)) {
                    return true;
                }
                return false;
            });
        };

        self.selectAll = function (page, event) {
            $.each(page.filesInDraftMode(), function (i, asset) {
                asset.selected(event.currentTarget.checked);
                $.each(asset.associatedAssets(), function (index, associatedAsset) {
                    associatedAsset.selected(event.currentTarget.checked);
                });
            });
            return true;
        };
        self.publishAll = function (data, event) {
            $.each(data.filesInDraftMode(), function (i, val) {
                val.publish();
            });
        };
        self.publishSelectedAssets = function (page, event) {
            var selectedItems = page.filesInDraftMode().filter(function (val) {
                return val.selected() == true;
            });
            $.each(page.filesInDraftMode(), function (i, draftFile) {
                var selectedAssosAssets = draftFile.associatedAssets().filter(function (val) {
                    return val.selected() == true;
                });
                $.each(selectedAssosAssets, function (i, asset) {
                    selectedItems.push(asset);
                });
            });
            $.each(selectedItems, function (i, draftFile) {
                draftFile.publish();
            });
        };
        self.isPublished = ko.observable(false);
        self.publishPage = function (page, event) {
            var vm = page.parent;
            vm.publishDraftContent(page).then(function (status) {
                if (status) {
                    //page.resultsMessage("Successfully published!");
                    page.isPublished(true);
                    page.hasPublishedVersion(true);
                    //Refresh page
                    setTimeout(page.refresh, 30, page);
                }
                else {
                    page.resultsMessage("Unable to publish this content. Please contact support.");
                }
            });
        };
        self.hasDraftFiles = ko.observable(false);
        self.refresh = function (page, event) {
            if (page == undefined) page = self;
            if (page != null) {

                var vm = page.parent;

                //uncheck the selection
                page.selected(false);
                //remove files for this page
                page.draftFiles.removeAll();
                page.draftFilesLoaded(false);

                vm.statusMessage("We are scanning your selected content for draft content... ");
                vm.resetAccordion = false;
                vm.numberOfPages(vm.numberOfPages() - 1);

                vm.refreshPage(page, vm);
            }
        };
        self.removeItem = function (page, id) {
            page.draftFiles.remove(function (item) {
                return item.uniqueID == id;
            });
            $.each(page.draftFiles(), function (i, draftFile) {
                draftFile.associatedAssets.remove(function (item) {
                    return item.uniqueID == id; 
                });
            });

            if (page.filesInDraftMode().length == 0) {
                page.hasDraftFiles(false);
                page.resultsMessage('<i class="ionicons ion-android-done"></i>&nbsp;All items have been published successsfuly. ');
            }
        }
        self.fadeIn = function (element, index, data) {
            if (element.nodeType == document.ELEMENT_NODE) {
                element.style.textDecoration = "line-through";
                element.style.backgroundColor = "#fffedf";
                $(element).fadeOut("slow");
            }
        };
        self.cssClass = function (index) {
            return self.parent.cssClass[index()+1];
        }
        self.typesOfContent = ko.observableArray(["Document", "Image", "Broken Link"]);
        self.getIcon = function (fileType) {
            var icon = "";
            switch (fileType) {
                case "Document":
                    icon = "fa fa-file-text-o";
                    break;
                case "Image":
                    icon = "fa fa-file-image-o";
                    break;
                case "Broken Link":
                    icon = "fa fa-chain-broken";
                    break;
                default:
                    icon = "fa fa-list-alt";
                    break;
            }
            return icon;
        }
        self.resultsMessage = ko.observable("Working...");
        self.disabled = ko.observable("");
    }
    return Page;
})();

var DraftContentViewModel = (function () {
    function DraftContentViewModel(site, web, list, items, pageUrl) {
        var self = this;
        self.draftItemDisplay = ko.observable(false);
        self.isLoaded = ko.observable(false);
        self.pages = ko.observableArray([]);
        self.numberOfPages = ko.observable(0);
        self.allPagesProcessed = function () {
            //if already loaded then dont refresh the accordion
            if (!self.pages || self.pages().length == 0) {
                return true;
            }

            if (self.numberOfPages() === items.length) {
                if (self.resetAccordion) {
                    $("#pagesAccordion").accordion({
                        collapsible: false,
                        active: false,
                        autoHeight: false
                    });
                    $("#pagesAccordion div").css({ 'height': 'auto' });

                    $('#pagesAccordion a').click(function (e) {
                        e.stopPropagation();
                    });
                }
                $(".contentAccordion").accordion({
                    collapsible: false,
                    active: false,
                    autoHeight: false,
                    beforeActivate: function (event, ui) {
                    }
                });
                $(".contentAccordion div").css({ 'height': 'auto' });

                $('.contentAccordion input[type="checkbox"]').click(function (e) {
                    e.stopPropagation();
                });
                $('.contentAccordion a').click(function (e) {
                    e.stopPropagation();
                });

                self.statusMessage("Finished scanning content");

                return true;
            }
            return false;
        };

        self.cssClass = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine"];
        self.resetAccordion = true;
        self.statusMessage = ko.observable();
        self.getPageAssetsInDraftMode = getPageAssetsInDraftMode;
        self.getPageInfo = getPageInfo;
        self.getDataInfo = getDataInfo;
        self.refreshPage = refreshPage;
        self.initPage = initPage;
        self.publishDraftContent = publishDraftContent;
        self.refreshAll = function (data, event) {
            $.each(data.pages(), function (i, page) {
                page.refresh(page, event);
            });
        };

        function publishDraftContent(asset) {
            var deferred = $.Deferred();
            var api = "/api/integritycheck/publishasset";
            var req = {
                'site': asset.siteId,
                'web': asset.webId,
                'file': asset.relativeUrl,
                'list': asset.list,
                'item': asset.item,
                'logSite': site,
                'user': userId
            };

            $.ajax({
                xhrFields: {
                    'withCredentials': true
                },
                url: api,
                type: "POST",
                contentType: 'application/x-www-form-urlencoded; charset=utf-8',
                data: '=' + JSON.stringify(req),
                success: function (status) {
                    if (status) {
                        console.log("Successfully published the file: " + asset.url);
                        asset.error(null);
                        deferred.resolve(status);
                    }
                    else {
                        console.log("Error publishing file: " + asset.url);
                        asset.error("Error ocurred while publishing content");
                        deferred.reject(status);
                    }
                },
                error: function (status, errorCode, errorMessge) {
                    console.log("Error publishing the the file: " + asset.url);
                    deferred.reject(status);
                }
            });
            return deferred.promise();
        }

        function getPageAssetsInDraftMode(page) {
            var deferred = $.Deferred();
            var api = "/api/integritycheck/";
            var url = String.format(api + "{0}/site/{1}/web/{2}/list/{3}/items/{4}", loc, page.siteId, page.webId, page.listId, page.id, loc);
            console.log("Scanning " + location.protocol + "//" + location.host + url);
            $.ajax({
                xhrFields: {
                    'withCredentials': true
                },
                url: url,
                success: function (data) {
                    console.log("Successfully retrieved related content in draft mode");

                    var draftFiles = [];
                    var draftAssets = data.DraftAssets;
                    var dynamicAssets = data.DynamicAssets;

                    //process draft assets
                    $.each(draftAssets, function (i, item) {
                        item.page = page;
                        var relatedContent = new DraftItem(item);
                        if (!relatedContent.type()) {
                            relatedContent.type("Document");
                        }
                        //If filetype does not exist in typesOfContent then add it so that it can be grouped
                        if (page.typesOfContent.indexOf(relatedContent.type()) < 0) {
                            page.typesOfContent.push(relatedContent.type());
                        }

                        //draftFiles.push(relatedContent);
                        page.draftFiles.push(relatedContent); //add do ko observable array
                    });

                    //process dynamic assets
                    if (dynamicAssets) {
                        $.each(dynamicAssets, function (i, val) {
                            if (val != null) {
                                $.each(val, function (index, item) {
                                    item.page = page;
                                    var relatedContent = new DraftItem(item);
                                    if (!relatedContent.type()) {
                                        relatedContent.type(i.slice(0, -1));
                                    }
                                    //If filetype does not exist in typesOfContent then add it so that it can be grouped
                                    if (page.typesOfContent.indexOf(relatedContent.type()) < 0) {
                                        page.typesOfContent.push(relatedContent.type());
                                    }

                                    page.draftFiles.push(relatedContent);
                                });
                            }
                        });
                    }

                    self.numberOfPages(self.numberOfPages() + 1);
                    deferred.resolve(page);
                },
                failure: function (err) {
                    console.log(err);
                }
            });
            return deferred.promise();
        }

        function refreshPage(page, vm) {
            if (page.id > 0) {
                console.log("Refreshing content");
                vm.getPageInfo(page.siteId, page.webId, page.listId, [page.id], page).then(function (pages) {
                    console.log("Selected content successfully refreshed");
                });
            }
        }

        function initPage(page, vm) {
            if (page.id > 0) {
                //reload all draft content
                console.log("Scanning page for draft and pending items");
                vm.getPageAssetsInDraftMode(page).then(function (page) {
                    page.draftFilesLoaded(true);
                    if (page.filesInDraftMode().length > 0) {
                        page.hasDraftFiles(true);
                        page.sortDraftFiles();
                        page.resultsMessage('<i class="fa fa-exclamation"></i>&nbsp;Unpublished items detected on this content. View the report below and take necessary actions');
                    }
                    else {
                        page.hasDraftFiles(false);
                        page.resultsMessage('<i class="ionicons ion-android-done"></i>&nbsp;No unpublished items detected on this content.');
                    }
                });
            }
            else {
                vm.numberOfPages(vm.numberOfPages() + 1);
                page.disabled("disabled");
            }
        }
        //prepare a page object and call draf contents for each page,
        //render page object as soon as the draft data is available that page.
        function getPageInfo(site, web, list, items, page) {
            var deferred = $.Deferred();
            var api = "/api/integritycheck/getiteminfo";
            var req = {
                'site': site,
                'siteUrl': hostUrl,
                'web': web,
                'list': (list !=null && list != 'undefined') ? list : "",
                'items': (items != null && items.length > 0) ? items : []
            };
            $.ajax({
                xhrFields: {
                    'withCredentials': true
                },
                url: api,
                type: "POST",
                contentType: 'application/x-www-form-urlencoded; charset=utf-8',
                data: '=' + JSON.stringify(req),
                success: function (data) {
                    var pages = [];
                    if (data) {
                        $.each(data, function (i, val) {
                            //initialize a new page if no specific page has been asked to refresh
                            if (!page)
                                page = new Page();

                            page.init(val);
                            page.parent = self;
                            self.initPage(page, self);
                            if (self.pages().length == 0) { //already loaded 
                                pages.push(page);
                                page = null;
                            }
                        });
                        console.log("Collected page information");
                    }
                    else
                        console.log("Error getting page information");

                    deferred.resolve(pages);
                },
                error: function (data, errorCode, errorMessge) {
                    console.log("Error getting page information.");
                    deferred.reject(data);
                }
            });
            return deferred.promise();
        }

        function getDataInfo(site, web, list, item) {
            var deferred = $.Deferred();
            var api = "/API/IntegrityCheck/GetItemInfo";
            var req = {
                'site': site,
                'web': web,
                'list': (list != null && list != 'undefined') ? list : "",
                'items': (item != "") ? [item] : [],
            };
            $.ajax({
                xhrFields: {
                    'withCredentials': true
                },
                url: api,
                type: "POST",
                contentType: 'application/x-www-form-urlencoded; charset=utf-8',
                data: '=' + JSON.stringify(req),
                success: function (data) {
                    var page = new Page();
                    page.init(data[0]);
                    console.log("Collected item information");
                    deferred.resolve(page);
                },
                error: function (data, errorCode, errorMessge) {
                    console.log("Error getting item information.");
                    deferred.reject(data);
                }
            });
            return deferred.promise();
        }

        function processPage(pages) {
            self.statusMessage("We are scanning your selected page(s) for draft content... ");

            var draftPromise = getPageInfo(site, web, list, pages);

            $.when(draftPromise).then(function (data) {
                if (data) {
                    $.each(data, function (i, page) {
                        self.pages.push(page);
                        self.isLoaded(true);
                    });
                }
                else {
                    self.statusMessage("Unable to retrieve information about selected content");
                }
            });
        }

        if (site == "undefined")
        {
            self.statusMessage("Necessary parameters are missing. Aborting...");
            console.log("Necessary parameters are missing. Aborting...");
        }
        else
        {
            if (items == 'undefined') {
                if (pageUrl != "")
                    processPage(pageUrl);
            }
            else {
                if (items != null)
                    processPage(items);
            }
        }
    }

    DraftContentViewModel.init = function (site, web, list, items, pageUrl, element) {

        var vm = new DraftContentViewModel(site, web, list, items, pageUrl);

        ko.bindingHandlers.fadeVisible = {
            init: function (element, valueAccessor) {
                // Initially set the element to be instantly visible/hidden depending on the value
                var value = valueAccessor();
                $(element).toggle(ko.utils.unwrapObservable(value)); // Use "unwrapObservable" so we can handle values that may or may not be observable
            },
            update: function (element, valueAccessor) {
                // Whenever the value subsequently changes, slowly fade the element in or out
                var value = valueAccessor();
                ko.utils.unwrapObservable(value) ? $(element).fadeIn() : $(element).fadeOut();
            }
        };

        ko.applyBindings(vm, element);
    }
    return DraftContentViewModel;
})();