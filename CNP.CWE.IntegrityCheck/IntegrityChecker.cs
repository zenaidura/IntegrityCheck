using CNP.CWE.CDA.WebParts.ExtendedContentSearchWebPart;
using CNP.CWE.IntegrityCheck.Model;
using CNP.CWE.SharePoint.Common;
using CNP.CWE.SharePoint.Common.Diagnostics;
using Microsoft.Office.Server.Search.Administration;
using Microsoft.Office.Server.Search.Administration.Query;
using Microsoft.Office.Server.Search.Query;
using Microsoft.Office.Server.Search.WebControls;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Publishing;
using Microsoft.SharePoint.Publishing.Navigation;
using Microsoft.SharePoint.Taxonomy;
using Microsoft.SharePoint.Utilities;
using Microsoft.SharePoint.WebControls;
using Microsoft.SharePoint.WebPartPages;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace CNP.CWE.IntegrityCheck
{
    public static class DraftChecker
    {
        #region Private Members
        private static ILogging log = Recorder.Logger;
        private static string lastException = string.Empty;
        #endregion

        #region Public Members
        public static string Location { get; set; }
        #endregion

        #region Public Members
        internal static void SetDiagnosticsCategory(string categoryName)
        {
            C.Diagnostics.AreaName = categoryName; 
        }

        internal static string GetLastException()
        {
            return lastException;
        }

        internal static List<ItemInfo> GetItems(SPWeb web, string listId, string[] itemIds)
        {
            log.Enter();
            List<ItemInfo> pages = new List<ItemInfo>();
            int id = 0;
            ItemInfo itemInfo = null;

            try
            {
                log.TraceInformation("Getting list: " + listId);
                SPList list = web.Lists[new Guid(listId)];
                log.TraceInformation("Error getting list: " + listId);

                if (list != null)
                {
                    foreach (string itemId in itemIds)
                    {
                        if (int.TryParse(itemId, out id))
                        {
                            SPQuery query = new SPQuery();
                            query.Query = "<Where><Eq><FieldRef Name=\"ID\"></FieldRef><Value Type=\"Integer\">" + id.ToString(CultureInfo.InvariantCulture) + "</Value></Eq></Where>";
                            query.ViewFields = string.Concat(
                                           "<FieldRef Name='ID' />",
                                           "<FieldRef Name='Title' />",
                                           "<FieldRef Name='" + list.Fields.GetField(Constants.FieldNames.ApprovalStatus).InternalName + "'/>",
                                           "<FieldRef Name='" + list.Fields.GetField(Constants.FieldNames.ScheduledStartDate).InternalName + "'/>",
                                           "<FieldRef Name='" + list.Fields.GetField(Constants.FieldNames.ScheduledEndDate).InternalName + "'/>",
                                           "<FieldRef Name='" + list.Fields.GetField(Constants.FieldNames.ContentType).InternalName + "'/>",
                                           "<FieldRef Name='" + list.Fields.GetField(Constants.FieldNames.Expiration).InternalName + "'/>",
                                           "<FieldRef Name='" + list.Fields.GetField(Constants.FieldNames.Audience).InternalName + "'/>",
                                           "<FieldRef Name='" + list.Fields.GetField(Constants.FieldNames.ServiceArea).InternalName + "'/>",
                                           "<FieldRef Name='LinkTitle' />");
                            query.ViewAttributes = "Scope=\"RecursiveAll\"";

                            log.TraceInformation("Getting item with query: " + query.Query);

                            SPListItem pageItem = null;

                            SPListItemCollection items = list.GetItems(query);
                            if (items != null && items.Count > 0)
                            {
                                pageItem = items[0];
                                itemInfo = GetItemInfo(pageItem);
                            }
                            else
                            {
                                itemInfo = new ItemInfo()
                                {
                                    Id = -1,
                                    Title = "Content not found",
                                    Url = list.DefaultDisplayFormUrl + "?ID=" + id,
                                    LastError = "Possible reason: Content might have been rejected by the approver."
                                };
                            }
                        }
                        pages.Add(itemInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                log.TraceException(ex);
                RecordLastException(ex);
                itemInfo = new ItemInfo()
                {
                    Id = -1,
                    Title = "Content not found.",
                    Url = "List Id = " + listId + " ID = " + id,
                    LastError = ex.Message
                };
                pages.Add(itemInfo);
            }

            log.Leave();
            return pages;
        }

        internal static List<ItemInfo> GetPage(SPWeb web, string[] pageUrls)
        {
            List<ItemInfo> pages = null;
            pages = new List<ItemInfo>();
            try
            {
                foreach (string url in pageUrls)
                {
                    SPFile file = web.GetFile(url);
                    if (file != null && file.Exists)
                    {
                        SPListItem pageItem = file.Item;
                        pages.Add(GetItemInfo(pageItem));
                    }
                    else
                    {
                        pages.Add(new ItemInfo()
                        {
                            Id = -1,
                            Title = "Content not be found",
                            Url = url
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                log.TraceException(ex);
                RecordLastException(ex);

                pages.Add(new ItemInfo()
                {
                    Id = -1,
                    Title = "Content not found.",
                    LastError = ex.Message
                });
            }

            return pages;
        }

        internal static List<ItemInfo> GetAllPages(SPWeb web)
        {
            List<ItemInfo> pages = null;
            try
            {
                PublishingWeb pubWeb = PublishingWeb.GetPublishingWeb(web);
                var pPages = pubWeb.GetPublishingPages();
                pages = new List<ItemInfo>();

                foreach (var pPage in pPages)
                {
                    SPListItem pageItem = pPage.ListItem;
                    pages.Add(GetItemInfo(pageItem));
                }
            }
            catch (Exception ex)
            {
                log.TraceException(ex);
                RecordLastException(ex);
            
                pages.Add(new ItemInfo()
                {
                    Id = -1,
                    Title = "Content not found.",
                    LastError = ex.Message
                });
            }

            return pages;
        }

        internal static List<DraftAsset> GetDraftContent(SPSite site, SPListItem pageItem, Dictionary<string, string> itemProcessed = null)
        {
            List<DraftAsset> assets = new List<DraftAsset>();
            try
            {
                if (itemProcessed == null)
                    itemProcessed = new Dictionary<string, string>();

                //assets.AddRange(GetContentFromForwardedLinks(site, pageItem, itemProcessed));

                assets.AddRange(GetContentByScanningItemProperties(site, pageItem, itemProcessed));
            }
            catch (Exception ex)
            {
                log.TraceException(ex);
                RecordLastException(ex);
            }

            return assets;
        }

        internal static Dictionary<string, List<DynamicAsset>> GetUnapprovedItems(SPSite site, SPListItem pageItem)
        {
            Dictionary<string, List<DynamicAsset>> dynamicContent = null;

            try
            {
                if (pageItem.File != null)
                {
                    dynamicContent = ScanCSWPWebParts(site, pageItem.File);
                }
            }
            catch (Exception ex)
            {
                log.TraceException(ex);
                RecordLastException(ex);
            }

            return dynamicContent;
        }

        internal static bool PublishContent(SPWeb web, string fileUrl, string listId, string itemId, string user)
        {
            Status iStatus = Status.None;
            try
            {
                AuditLogger.User = user;

                if (!string.IsNullOrEmpty(listId) && !string.IsNullOrEmpty(itemId))
                {
                    SPList list = web.Lists[new Guid(listId)];
                    int itemID = -1;
                    if (int.TryParse(itemId, out itemID))
                    {
                        SPListItem item = list.GetItemById(itemID);
                        if (item != null)
                        {
                            iStatus = ApproveItem(web, list, item) ? Status.Success : Status.Failure;
                            log.TraceInformation(string.Format("Approved item ID: {0} in List: {1} with status: {2}", itemId, list.Title, iStatus.ToString()));
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(fileUrl))
                {
                    iStatus = PublishFile(web, fileUrl) ? Status.Success : Status.Failure;
                    log.TraceInformation(string.Format("Published file {0} with status: {1}", fileUrl, iStatus.ToString()));
                }
            }
            catch (Exception ex)
            {
                log.TraceException(ex);
                RecordLastException(ex);
            }
            return (iStatus == Status.Success);
        }
        #endregion

        #region Private Methods

        #region Static Content
        private static List<DraftAsset> GetContentByScanningItemProperties(SPSite site, SPListItem pageItem, Dictionary<string, string> itemProcessed)
        {
            List<DraftAsset> assets = new List<DraftAsset>();

            //Scan and return draft items referenced in HTML fields including publishing fields on the item (Publishing Image, Publishing Hyperlink)
            LinkChecker linkChecker = new LinkChecker();

            linkChecker.ScanLinksAndImages(site, pageItem);

            //if valid links were found in various fields of the item then parse them 
            if (linkChecker.ValidLinks != null && linkChecker.ValidLinks.ContainsValue(true))
            {
                foreach (var linkAsset in DraftChecker.GetDraftContent(site, linkChecker.ValidLinks))
                {
                    if (!itemProcessed.ContainsKey(linkAsset.Url.ToLower()))
                    {
                        log.TraceInformation(string.Format("Adding draft content to the list of assets: {0}", linkAsset.Url));
                        assets.Add(linkAsset);
                    }
                }
            }

            //Found borken links on the page
            assets.AddRange(linkChecker.BrokenLinks);
            return assets;
        }

        private static List<DraftAsset> GetContentFromForwardedLinks(SPSite site, SPListItem pageItem, Dictionary<string, string> itemProcessed)
        {
            List<DraftAsset> assets = new List<DraftAsset>();
            foreach (SPLink link in pageItem.ForwardLinks)
            {
                if (!(link.Url.Contains('~') || link.Url.Contains("/_catalogs")))
                {
                    var asset = new DraftAsset()
                    {
                        IsBroken = link.IsInternal && link.IsBroken,
                        Url = (link.IsInternal) ? link.ServerRelativeUrl: link.Url,
                        IsInternal = link.IsInternal
                    };
                    if (!link.IsToFolder)
                    {
                        if (link.WebId != Guid.Empty)
                        {
                            using (var targetWeb = site.OpenWeb(link.WebId))
                            {
                                if (!link.IsBroken)
                                {
                                    SPFile file = (link.IsInternal) ? targetWeb.GetFile(link.ServerRelativeUrl) : targetWeb.GetFile(link.Url);
                                    asset = GetDraftAssetFromFile(file, asset);
                                }
                            }
                        }
                    }
                    if (asset != null && asset.IsDraft && !itemProcessed.ContainsKey(link.ServerRelativeUrl.ToLower()))
                    {
                        assets.Add(asset);
                        itemProcessed.Add(link.ServerRelativeUrl.ToLower(), asset.Title);
                    }
                }
            }
            return assets;
        }
        
        private static List<DraftAsset> GetDraftContent(SPSite site, Hashtable links)
        {
            log.Enter();
            List<DraftAsset> assets = null;

            if (links.ContainsValue(true))
            {
                assets = new List<DraftAsset>();
                log.TraceInformation(string.Format("Looking for draft content"));

                foreach (DictionaryEntry entry in links)
                {
                    if ((bool)entry.Value == true)
                    {
                        string linkUrl = HttpUtility.HtmlDecode(entry.Key.ToString());
                        Uri originalUri = new Uri(linkUrl, UriKind.RelativeOrAbsolute);
                        DraftAsset asset = null;
                        if (!(originalUri.ToString().Contains('~') || originalUri.ToString().Contains("/_catalogs")))
                        {
                            if (originalUri.IsAbsoluteUri)
                            {
                                if (originalUri.PathAndQuery != "/")
                                {
                                    try
                                    {
                                        //Handle external URLs
                                        if (!originalUri.GetLeftPart(UriPartial.Authority).ToLower().Contains(Utility.GetPublishingUrl(site)))
                                        {
                                            log.TraceInformation(string.Format("Extenal valid link found (ignoring): {0}", originalUri));
                                        }
                                        else
                                        {
                                            if (originalUri.ToString().ToLower().Contains("fixupredirect.aspx"))
                                            {
                                                string qry = originalUri.Query.Substring(1);
                                                System.Collections.Specialized.NameValueCollection parameters = HttpUtility.ParseQueryString(qry);
                                                string termId = parameters["termid"];

                                                TaxonomySession session = new TaxonomySession(site, true);
                                                Term term = session.GetTerm(new Guid(termId));
                                                string url = "";
                                                if (term != null)
                                                {
                                                    if (term.LocalCustomProperties != null && term.LocalCustomProperties.Count > 0)
                                                    {
                                                        if (term.LocalCustomProperties.ContainsKey("_Sys_Nav_TargetUrl"))
                                                        {
                                                            term.LocalCustomProperties.TryGetValue("_Sys_Nav_TargetUrl", out url);
                                                            originalUri = new Uri(new Uri(Utility.GetPublishingUrl(site)), url);
                                                        }
                                                    }
                                                }
                                            }

                                            using (SPSite externalSite = new SPSite(originalUri.ToString()))
                                            {
                                                using (SPWeb web = externalSite.OpenWeb())
                                                {
                                                    string relativePathWithoutQueryParams = string.Format("/{0}",originalUri.GetComponents(UriComponents.Path, UriFormat.UriEscaped));
                                                    SPFile file = web.GetFile(relativePathWithoutQueryParams);
                                                    //If file does not exist at this url then check if file is a friendly URL of a publishing page
                                                    if (file == null || !file.Exists)
                                                    {
                                                        //Get physical publishing page URL from friendly url
                                                        file = GetFileFromFriendlyUrl(web, originalUri);
                                                    }

                                                    if (file != null && file.Exists)
                                                    {
                                                        asset = new DraftAsset();
                                                        asset.IsInternal = true;
                                                        asset.Url = file.ServerRelativeUrl;
                                                        asset = GetDraftAssetFromFile(file, asset);

                                                        if (asset != null && asset.FileType == "Linked Page")
                                                        {
                                                            //asset.Assets = GetDraftContent(externalSite, file.Item);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        asset = new DraftAsset();
                                                        asset.Url = originalUri.ToString();
                                                        asset.LastError = GetLastException();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        log.TraceException(ex, "SharePoint site does not exist");
                                        RecordLastException(ex);
                                        if (asset == null)
                                        {
                                            asset = new DraftAsset();
                                            asset.Url = originalUri.ToString();
                                        }
                                        asset.LastError = GetLastException();
                                    }

                                    if (asset != null)
                                    {
                                        assets.Add(asset);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            log.Leave();
            return assets;
        }

        private static DynamicAsset GetDynamicAssetFromDraftAsset(DraftAsset asset, SPListItem item)
        {
            return new DynamicAsset()
            {
                IsInternal = asset.IsInternal,
                SiteId = asset.SiteId,
                WebId = asset.WebId,
                ListId = item.ParentList.ID.ToString(),
                ItemId = item.ID,
                Title = asset.Title,
                Url = asset.Url,
                FileType = asset.FileType,
                IsDraft = asset.IsDraft,
                ScheduledToPublish = asset.ScheduledToPublish,
                ExpirationDate = asset.ExpirationDate
            };
        }

        private static SPFile GetFileFromFriendlyUrl(SPWeb web, Uri originalUri)
        {
            SPFile file = null;
            string fileUrl = GetFileUrlFromFriendlyURL(web, originalUri, DraftChecker.Location);

            if (!string.IsNullOrEmpty(fileUrl))
            {
                Uri absoluteUri = new Uri(originalUri, fileUrl);

                using (SPSite newSite = new SPSite(absoluteUri.ToString()))
                {
                    using (SPWeb newWeb = newSite.OpenWeb())
                    {
                        file = newWeb.GetFile(absoluteUri.PathAndQuery);
                    }
                }
            }

            if (file != null && file.Exists)
                log.TraceInformation(string.Format("Found physical page for friendly url: {0} -> {1}", originalUri.ToString(), file.ServerRelativeUrl));
            else
                log.TraceWarning(string.Format("Physical page for friendly url {0} cannot be found", originalUri.ToString()));

            return file;
        }

        private static string GetFileUrlFromFriendlyURL(SPWeb web, Uri uri, string location)
        {
            string fileUrl = string.Empty;

            try
            {
                uri = SanitizeUrl(uri, location);
                if (!string.IsNullOrEmpty(uri.PathAndQuery))
                {
                    fileUrl = TaxonomyNavigation.GetPageUrlForFriendlyUrl(web.Site, uri.PathAndQuery);
                    if (string.IsNullOrEmpty(fileUrl))
                    {
                        log.TraceWarning("Failed to get physical path from friendly url at: " + uri.PathAndQuery);
                    }
                }
            }
            catch (Exception ex)
            {
                log.TraceException(ex, "Error getting page from friendly url at: " + uri.ToString());
            }
            return fileUrl;
        }

        private static Uri SanitizeUrl(Uri urlToCheck, string locale)
        {
            string localeUrl = "";
            CNP.CWE.SharePoint.Common.Models.CNPUserCookie user;
            CNP.CWE.SharePoint.Common.Business_Engine.SelfIdCookieEngine cookieEngine = new CNP.CWE.SharePoint.Common.Business_Engine.SelfIdCookieEngine();
            user = cookieEngine.GetCNPUser(HttpContext.Current);
            if (user == null)
            {
                user = new SharePoint.Common.Models.CNPUserCookie();
            }
            //set language to current culture
            user.Language = locale;

            try { cookieEngine.SetCNPUser(HttpContext.Current, user); }
            catch (Exception) { }

            if ((urlToCheck.Scheme == "http") || (urlToCheck.Scheme == "https"))
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(urlToCheck);
                request.Credentials = CredentialCache.DefaultCredentials;

                HttpWebResponse response = null;
                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                    if (response != null)
                    {
                        localeUrl = response.ResponseUri.ToString();
                    }
                }
                catch (WebException ex)
                {
                    log.TraceException(ex);
                }
                finally
                {
                    if (response != null)
                    {
                        response.Close();
                    }
                }
            }
            return new Uri(localeUrl);
        }

        private static DraftAsset GetDraftAssetFromFile(SPFile file, DraftAsset asset)
        {
            if (ValidateFile(file))
            {
                if (file != null && file.Exists)
                {
                    asset.IsDraft = GetFilePublishingStatus(file);
                    asset.CheckedOutTo = GetFileCheckedOutBy(file);
                    //do not process draft files
                    if (!asset.IsDraft)
                        return null;

                    GetVersionInfo(file.Item, asset);
                    asset.ScheduledToPublish = GetScheduledDate(file.Item);
                    asset.ExpirationDate = GetExpirationDate(file.Item);
                    asset.Title = string.IsNullOrEmpty(file.Title) ? file.Name : file.Title;
                    switch (file.Item.ContentType.Name)
                    {
                        case "Page":
                        case "Article Page":
                        case "Welcome Page":
                        case "CNP Page":
                        case "CNP Article Page":
                        case "Catalog-Item Reuse":
                            asset.FileType = "Linked Page";
                            break;
                        default:
                            asset.FileType = file.Item.ContentType.Name;
                            break;
                    }
                    asset.SiteId = file.Web.Site.ID.ToString();
                    asset.WebId = file.Web.ID.ToString();

                    log.TraceInformation(string.Format("File ({0}) | Draft: {1} | Published: {2} | Content Type: {3}", asset.Url, asset.DraftVersion, asset.LastPublishedVersion, file.Item.ContentType.Name));
                }
            }
            else
                asset = null;

            return asset;
        }

        private static bool ValidateFile(SPFile file)
        {
            if (file.Url.Contains("/_layouts") || !file.InDocumentLibrary)
            {
                return false;
            }
            else if (file.Item == null)
                return false;

            return true;
        }
        #endregion
        
        #region Dynamic Content
        /// <summary>
        /// Returns list of dynamic content from all Content By Search Web Parts on the page
        /// </summary>
        /// <param name="site"></param>
        /// <param name="file"></param>
        private static Dictionary<string, List<DynamicAsset>> ScanCSWPWebParts(SPSite site, SPFile file)
        {
            log.Enter();

            SPLimitedWebPartManager wpm = file.GetLimitedWebPartManager(System.Web.UI.WebControls.WebParts.PersonalizationScope.Shared);
            int timetoExecute = 0;
            List<DynamicAsset> items = null;
            Dictionary<string, List<DynamicAsset>> dynamicContent = new Dictionary<string, List<DynamicAsset>>();
            Dictionary<string, string> itemsProcessed = new Dictionary<string, string>();
            string dataProviderJson = "";
            string wpTitle = "";
            int rowlimit = -1;

            foreach (var wp in wpm.WebParts.OfType<object>().Select((value, index) => new {value, index}))
            {
                log.TraceInformation(string.Format("Scanning web part: {0} of type {1}", ((System.Web.UI.WebControls.WebParts.WebPart)wp.value).Title, wp.value.GetType().FullName));

                switch (wp.value.GetType().FullName)
                {
                    case "Microsoft.Office.Server.Search.WebControls.ContentBySearchWebPart":
                    case "CNP.CWE.CDA.WebParts.ExtendedContentSearchWebPart.ExtendedContentSearchWebPart":
                        log.TraceInformation("--Processing");
                        ContentBySearchWebPart ContentBySearchWP = (ContentBySearchWebPart)wp.value;
                        wpTitle = !string.IsNullOrEmpty(ContentBySearchWP.Title) ? ContentBySearchWP.Title : "Untitled"+ wp.index;
                        rowlimit = ContentBySearchWP.NumberOfItems;
                        dataProviderJson = ContentBySearchWP.DataProviderJSON;
                        break;

                    case "CNP.CWE.CDA.WebParts.ExtendedSearchResultsWebPart.ExtendedSearchResultsWebPart":
                        log.TraceInformation("--Processing");
                        ResultScriptWebPart SearchResultWP = (ResultScriptWebPart)wp.value;
                        wpTitle = !string.IsNullOrEmpty(SearchResultWP.Title) ? SearchResultWP.Title :  "Untitled"+ wp.index;
                        dataProviderJson = SearchResultWP.DataProviderJSON;
                        //rowlimit = SearchResultWP.;
                        break;
                    default:
                        log.TraceInformation("--Ignoring");
                        break;
                }

                if (!string.IsNullOrEmpty(dataProviderJson))
                {
                    var queryData = JsonConvert.DeserializeObject<Dictionary<string, Object>>(dataProviderJson);
                    string queryTemplate = (queryData["QueryTemplate"] ?? "").ToString();
                    string sourceLevel = (queryData["SourceLevel"] ?? "").ToString();
                    string sourceId = (queryData["SourceID"] ?? "").ToString();
                    string sourceName = (queryData["SourceName"] ?? "").ToString();
                    //Execute query using Search Object Model
                    timetoExecute = ExecuteSearch(site, file.Item, "", queryTemplate, sourceLevel, sourceId, sourceName, rowlimit, out items, itemsProcessed);
                    dynamicContent.Add(wpTitle, items);
                }
                dataProviderJson = "";
            }
            wpm.Dispose();
            return dynamicContent;
        }

        private static int ExecuteSearch(SPSite site, SPListItem item, string query, string queryTemplate, string sourceLevel, string sourceId, string sourceName, int rowlimit, out List<DynamicAsset> listItems, Dictionary<string, string> itemsProcessed)
        {
            HttpRequest httpRequest = new HttpRequest(string.Empty, site.RootWeb.Url, string.Empty);
            HttpContext.Current = new HttpContext(httpRequest, new HttpResponse(new StringWriter()));
            SPControl.SetContextWeb(HttpContext.Current, site.RootWeb);

            Microsoft.SharePoint.SPServiceContext context = SPServiceContext.GetContext(site);
            // Get the search service application proxy
            IEnumerable<ResultTable> results = null;
            KeywordQuery keywordQuery = new KeywordQuery(site);

            if (!string.IsNullOrEmpty(sourceName))
            {
                SearchServiceApplicationProxy searchProxy = context.GetDefaultProxy(typeof(SearchServiceApplicationProxy)) as SearchServiceApplicationProxy;
                SourceRecord resultSource = null;
                SearchObjectOwner owner = null;

                switch (sourceLevel.ToLower())
                {
                    case "ssa":
                        owner = new SearchObjectOwner(SearchObjectLevel.Ssa);
                        break;

                    case "spsite":
                        owner = new SearchObjectOwner(SearchObjectLevel.SPSite, site.RootWeb);
                        break;

                    default:
                        break;
                }
                if (owner != null)
                {
                    resultSource = searchProxy.GetResultSourceByName(sourceName, owner);
                    keywordQuery.SourceId = resultSource.Id;
                    log.TraceInformation(string.Format("Found Result Source: {0}", resultSource.Name));
                }
            }
            else
            {
                keywordQuery.SourceId = new Guid(sourceId);
            }

            //filter out the path from query template
            queryTemplate = FilterPathQueryTemplate(queryTemplate);

            //replace token values e.g 
            queryTemplate = ReplaceTokens(site, item, queryTemplate);

            //Dont filter expirt if query is for event type
            if (queryTemplate.ToLower().Contains("ListItemID:{QueryString.eventId}".ToLower()))
            {
                queryTemplate = queryTemplate.ToLower().Replace("ListItemID:{QueryString.eventId}".ToLower(), "");
            }
            else
            {
                //filter expired items
                queryTemplate += AddExpiryFilter();
                queryTemplate += AddPendingOrExpiringFilter();
            }

            listItems = null;
            log.TraceInformation(string.Format("Preparing search execution: QueryTempalte: {0}, Query: {1}, SourceName: {2}, SourceLevel: {3}", queryTemplate, query, sourceName, sourceLevel));

            if (!queryTemplate.Contains("searchboxquery"))
            {
                keywordQuery.QueryTemplate = queryTemplate;
                keywordQuery.QueryText = query;
                keywordQuery.RowLimit = 500;

                SearchExecutor searchExecutor = new SearchExecutor();
                ResultTableCollection resultTableCollection = searchExecutor.ExecuteQuery(keywordQuery);
                results = resultTableCollection.Filter("TableType", KnownTableTypes.RelevantResults);

                if (results.Count() > 0)
                {
                    ResultTable resultTable = results.FirstOrDefault();
                    DataTable dataTable = resultTable.Table;
                    listItems = ProcessResults(site, dataTable, itemsProcessed);
                }
                log.TraceInformation(string.Format("Executed query with elapsed time: {0}", resultTableCollection != null ? resultTableCollection.ElapsedTime : -1));

                return resultTableCollection != null ? resultTableCollection.ElapsedTime : -1;
            }
            return -1;
        }

        private static string ReplaceTokens(SPSite site, SPListItem item, string queryTemplate)
        {
            return queryTemplate.Replace(@"owstaxIdMetadataAllTagsInfo:{Term.Id}", "");
            StringBuilder query = new StringBuilder();
            string template = "";

            Regex reg = new Regex(@"owstaxIdMetadataAllTagsInfo:{Term.Id}", RegexOptions.IgnoreCase);
            Match m = reg.Match(queryTemplate);
            if (m.Success)
            {
                template = m.Groups[0].ToString();

                //Get pages term id
                IList<NavigationTerm> navigationTerms = TaxonomyNavigation.GetFriendlyUrlsForListItem(item, true);

                foreach (var navTerm in navigationTerms)
                {
                    if (navTerm.Id != Guid.Empty)
                    {
                        query.AppendLine(template.Replace("{Term.Id}", navTerm.Id.ToString()));
                    }
                }
                log.TraceInformation(string.Format("Replacing token {0} with {1} in query template", template, query.ToString(), queryTemplate));
            }

            return (query.Length > 0) ? queryTemplate.Replace(template, query.ToString()) : queryTemplate;
        }

        private static string FilterPathQueryTemplate(string queryTemplate)
        {
            Regex reg = new Regex(@"path:[\s]*""(http|https)(:/{2}[\w]+)(?:[\\/|\\.]?)(?:[^\s]*)""", RegexOptions.IgnoreCase);
            Match m = reg.Match(queryTemplate);
            if (m.Success)
            {
                string path = m.Groups[0].ToString();

                log.TraceInformation(string.Format("Filterting out path {0} from Query Template {1}", path, queryTemplate));
                queryTemplate = queryTemplate.Replace(path, "");
            }
            return queryTemplate.Trim();
        }

        private static List<DynamicAsset> ProcessResults(SPSite site, DataTable dataTable, Dictionary<string, string> itemsProcessed)
        {
            List<DynamicAsset> listItems = new List<DynamicAsset>();

            log.TraceInformation(string.Format("Processing results from query with {0} rows...", dataTable.Rows.Count));
            foreach (DataRow row in dataTable.Rows)
            {
                string siteUrl = row.Field<string>("spSiteUrl");
                string siteId = row.Field<string>("SiteId");
                string webId = row.Field<string>("WebId");
                string listId = row.Field<string>("ListId");
                string path = row.Field<string>("OriginalPath");
                string title = row.Field<string>("Title");

                log.TraceInformation(string.Format("Retrieving dynamic item at: Path: {0}, Title: {1}, Site Url: {2}, Site Id: {3}, Web Id: {4}, List Id: {5}", 
                    path, title, siteUrl, siteId, webId, listId));

                if (!itemsProcessed.ContainsKey(path))
                {
                    string strID = (path.IndexOf(Constants.DispFormWithID) > 0)
                        ? path.Substring(path.IndexOf(Constants.DispFormWithID)).Replace(Constants.DispFormWithID, "")
                        : "";
                    int itemId = -1;
                    if (int.TryParse(strID, out itemId))
                    {
                        log.TraceInformation(string.Format("Item ID : {0}", itemId));

                        DynamicAsset dynamicItem = GetDynamicItem(site, siteId, webId, listId, itemId, itemsProcessed);
                        if (dynamicItem != null)
                        {
                            dynamicItem.Title = !string.IsNullOrEmpty(dynamicItem.Title) ? dynamicItem.Title     : title;
                            dynamicItem.Url = !string.IsNullOrEmpty(dynamicItem.Url) ? dynamicItem.Url : path;

                            log.TraceInformation(string.Format("Adding dynamic item {0} to the list", dynamicItem.Url));
                            listItems.Add(dynamicItem);
                        }
                    }

                    itemsProcessed.Add(path, path);
                }
            }
            return listItems;
        }

        private static DynamicAsset GetDynamicItem(SPSite orginialSite, string siteId, string webId, string listId, int itemId, Dictionary<string, string> itemProcessed)
        {
            DynamicAsset asset = null;

            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite iSite = new SPSite(new Guid(siteId)))
                    {
                        using (SPWeb web = iSite.OpenWeb(new Guid(webId)))
                        {
                            log.TraceInformation("Getting list with ID: " + listId); 

                            SPList list = web.Lists[new Guid(listId)];

                            SPListItem item = list.GetItemById(itemId);

                            log.TraceInformation("Getting list item with ID: " + itemId); 

                            if (IsItemPending(item))
                            {
                                asset = new DynamicAsset()
                                {
                                    //if this dynamic item has the same site id then the item is internal, otherwise external
                                    IsInternal = (orginialSite.ID == new Guid(siteId)),
                                    SiteId = iSite.ID.ToString(),
                                    WebId = web.ID.ToString(),
                                    ListId = list.ID.ToString(),
                                    ItemId = item.ID,
                                    Audience = item.Fields.ContainsField(Constants.FieldNames.Audience) ? String.Join(",", (item[Constants.FieldNames.Audience] as TaxonomyFieldValueCollection).ToList().Select(x => x.Label)) : string.Empty,
                                    ServiceArea = item.Fields.ContainsField(Constants.FieldNames.Audience) ? String.Join(",", (item[Constants.FieldNames.ServiceArea] as TaxonomyFieldValueCollection).ToList().Select(x => x.Label)) : string.Empty,
                                    Title = item.Title,
                                    Url = SPUtility.ConcatUrls((orginialSite.ID != new Guid(siteId)) ? item.Web.Site.Url : "", item.ParentList.DefaultDisplayFormUrl) + "?ID=" + item.ID,
                                    FileType = item.ParentList.Title.Replace("List", "").Trim().TrimEnd('s'),
                                    IsDraft = IsItemPending(item),
                                    ScheduledToPublish = GetScheduledDate(item),
                                    ExpirationDate = GetExpirationDate(item)
                                };
                                GetVersionInfo(item, asset);

                                log.TraceInformation(string.Format("Retrieved dynamic item information for {0} ({1})", asset.Title, asset.Url));

                                //Scan and return draft items referenced in the body and other fields on this item
                                asset.Assets = GetDraftContent(iSite, item, itemProcessed);
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                log.TraceException(ex);
                RecordLastException(ex);

                if (asset == null)
                {
                    asset = new DynamicAsset();
                    asset.SiteId = siteId;
                    asset.WebId = webId;
                    asset.ListId = listId;
                    asset.ItemId = itemId;
                }
                asset.LastError = GetLastException();
            }
            return asset;
        }

        #endregion

        #region General

        private static void RecordLastException(Exception ex)
        {
            lastException = ex.Message;
        }

        private static ItemInfo GetItemInfo(SPListItem pageItem)
        {
            log.Enter();
            log.TraceInformation(string.Format("Getting item information for {0}", pageItem.ParentList.DefaultDisplayFormUrl + "?ID=" + pageItem.ID));

            ItemInfo item = null;
            try
            {
                item = new ItemInfo()
                {
                    SiteId = pageItem.Web.Site.ID.ToString(),
                    WebId = pageItem.Web.ID.ToString(),
                    ListId = pageItem.ParentList.ID.ToString(),
                    Id = pageItem.ID,
                    Title = string.IsNullOrEmpty(pageItem.Title) ? pageItem.File.Name : pageItem.Title,
                    Url = GetItemUrl(pageItem),
                    IsDraft = IsFileInDraft(pageItem) || IsItemPending(pageItem),
                    ScheduledToPublish = GetScheduledDate(pageItem),
                    ExpirationDate = GetExpirationDate(pageItem),
                    CheckedOutTo = GetFileCheckedOutBy(pageItem.File),
                    FileType = pageItem.ContentType.Id.ToString().Contains("0x0101") ? "Static" : "Dynamic"
                };

                GetVersionInfo(pageItem, item);
            }
            catch (Exception ex)
            {
                log.TraceException(ex);
                RecordLastException(ex);

                if (item == null)
                {
                    item = new ItemInfo()
                    {
                        Id = -1,
                        Title = pageItem.Title,
                        Url = pageItem.ParentList.DefaultDisplayFormUrl + "?ID=" + pageItem.ID,
                        LastError = GetLastException()
                    };
                }
            }

            log.Leave();
            return item;
        }

        private static string GetItemUrl(SPListItem pageItem)
        {
            string url = "";

            if (pageItem.File != null && pageItem.File.Exists && pageItem.File.InDocumentLibrary)
            {
                try
                {
                    //Check if publishing page
                    PublishingPage page = PublishingPage.GetPublishingPage(pageItem);
                    url = pageItem.File.ServerRelativeUrl;
                }
                catch (Exception ex)
                {
                    RecordLastException(ex);
                    url = pageItem.ParentList.DefaultDisplayFormUrl + "?ID=" + pageItem.ID;
                }
            }
            else
            {
                url = pageItem.ParentList.DefaultDisplayFormUrl + "?ID=" + pageItem.ID;
            }
            return url;
        }

        private static bool GetFilePublishingStatus(SPFile file)
        {
            bool status = false;
            if (file.Item.ParentList.EnableVersioning)
            {
                status = file.Level != SPFileLevel.Published || file.CheckOutType != SPFile.SPCheckOutType.None;
            }
            return status;
        }

        private static string GetFileCheckedOutBy(SPFile file)
        {
            string checkedOutTo = "";
            if (file != null && file.Exists)
            {
                if (file.Item.ParentList.EnableVersioning)
                {
                    if (file.Level == SPFileLevel.Checkout)
                    {
                        checkedOutTo = file.CheckedOutByUser.Name;
                    }
                }
            }
            return checkedOutTo;
        }

        private static bool IsFileInDraft(SPListItem item)
        {
            bool status = false;
            if (item.ParentList.EnableVersioning)
            {
                status = item.Level != SPFileLevel.Published;
                if (item.File != null)
                    status = GetFilePublishingStatus(item.File);
            }
            return status;
        }

        private static bool IsItemPending(SPListItem item)
        {
            log.TraceInformation("Checking if item is pending or approved");

            bool isDraft = false;
            try
            {
                if (item.ParentList.EnableModeration)
                {
                    if (item.Fields.ContainsField(Constants.FieldNames.ApprovalStatus))
                    {
                        switch (item[Constants.FieldNames.ApprovalStatus].ToString())
                        {
                            case "2":
                                isDraft = true;
                                break;
                            default:
                                //check if its scheduled to publish at a later date.
                                if (!string.IsNullOrEmpty((item[Constants.FieldNames.ScheduledStartDate] ?? "").ToString()))
                                {
                                    var dtStartDate = DateTime.Parse(GetScheduledDate(item));
                                    if (dtStartDate > DateTime.Now)
                                        isDraft = true;
                                }
                                isDraft = false;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.TraceException(ex);
                RecordLastException(ex);
            }
            return isDraft;
        }

        private static void GetVersionInfo(SPListItem item, Item asset)
        {
            log.TraceInformation("Getting version info");

            if (item.ParentList.EnableVersioning)
            {
                asset.PreviouslyPublished = item.HasPublishedVersion;
                if (item.Versions != null && item.Versions.Count > 0)
                {
                    foreach (SPListItemVersion version in item.Versions)
                    {
                        if (version.Level == SPFileLevel.Published)
                        {
                            asset.PreviouslyPublished = true;
                            asset.LastPublishedVersion = version.VersionLabel;
                            break;
                        }
                        else
                        {
                            if (version.IsCurrentVersion)
                            {
                                asset.DraftVersion = version.VersionLabel;
                            }
                        }
                    }
                }
            }
        }

        private static string GetScheduledDate(SPListItem item)
        {
            string dateScheduled = "";
            if (item.Fields.ContainsField(Constants.FieldNames.ScheduledStartDate))
            {
                dateScheduled = (item[Constants.FieldNames.ScheduledStartDate] ?? "").ToString();
                if (!string.IsNullOrEmpty(dateScheduled))
                {
                    DateTime dt = DateTime.Parse(dateScheduled);
                    dateScheduled = dt.ToShortDateString();
                }
            }
            return dateScheduled;
        }

        private static string GetExpirationDate(SPListItem item)
        {
            string dateExpired = "";
            if (item.Fields.ContainsField(Constants.FieldNames.ScheduledEndDate))
            {
                dateExpired = (item[Constants.FieldNames.ScheduledEndDate] ?? "").ToString();
                if (!string.IsNullOrEmpty(dateExpired))
                {
                    DateTime dt = DateTime.Parse(dateExpired);
                    dateExpired = dt.ToShortDateString();
                }
            }
            return dateExpired;
        }

        private static string AddExpiryFilter()
        {
            string endDateFilter = " (SchedulingEndDateOWSDateTime>={Today} OR CalculatedEndDate:Never)";
            return endDateFilter;
        }

        private static string AddPendingOrExpiringFilter()
        {
            return "(ApprovalStatus:2 OR (CalculatedEndDate:Never OR (SchedulingEndDateOWSDateTime>={Today} AND SchedulingEndDateOWSDateTime<={Today+30})))";
        }

        #endregion

        #region Publishing
        private static bool ApproveItem(SPWeb web, SPList list, SPListItem item)
        {
            string url = SPUtility.ConcatUrls(web.Site.Url, item.ParentList.DefaultDisplayFormUrl + "?ID=" + item.ID);
            try
            {
                if (list.EnableVersioning && list.EnableModeration)
                {
                    web.AllowUnsafeUpdates = true;
                    if (item.ModerationInformation != null)
                    {
                        if (item.ModerationInformation.Status != SPModerationStatusType.Approved)
                        {
                            SPUser spUser = Utility.GetUserById(web, AuditLogger.User);
                            item[SPBuiltInFieldId.Editor] = spUser;
                            item.UpdateOverwriteVersion();
                            item.ModerationInformation.Status = SPModerationStatusType.Approved;
                            item.ModerationInformation.Comment = string.Format("Approved by IntegrityCheck on behalf of {0} on {1}", spUser.Name, DateTime.Now.ToString());
                            item.SystemUpdate();

                            string details = "Approved by IntegrityCheck";
                            AuditLogger.LogApprovalStatus(item.Title + "|" + url, details);
                            return !AuditLogger.IsError;
                        }
                        else
                        {
                            log.TraceWarning(string.Format("Item {0}?ID={1} is already approved", SPUtility.ConcatUrls(item.Web.Url, item.ParentList.DefaultDisplayFormUrl)));
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.TraceException(ex);
                RecordLastException(ex);

                string details = "Error occurred while approving content by Integrity Check. Details: " + ex.Message;
                AuditLogger.LogException(item.Title + "|" + url, details);
            }
            return false;
        }

        private static void CheckInFileByUser(SPFile file, string checkinComment, SPCheckinType checkinType, SPUser modifiedByUser)
        {
            MethodInfo mi = typeof(SPFile).GetMethod(
                "CheckIn",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(string), typeof(SPCheckinType), typeof(bool), typeof(SPUser) },
                null);

            try
            {
                mi.Invoke(
                    file,
                    new object[] { checkinComment, checkinType, false, modifiedByUser }
                );
            }
            catch (TargetInvocationException invokeEx)
            {
                throw invokeEx.InnerException;
            }
        }

        private static bool PublishFile(SPWeb web, string fileUrl)
        {
            SPFile file = web.GetFile(fileUrl);

            if (file != null)
            {
                try
                {
                    if (file.Item.ParentList.EnableVersioning)
                    {
                        if (file.Item.ParentList.EnableMinorVersions)
                        {
                            //Publish and approve this item
                            web.AllowUnsafeUpdates = true;

                            //Update the user who is publishing the file
                            SPListItem item = file.Item;
                            SPUser spUser = Utility.GetUserById(web, AuditLogger.User);

                            if (spUser != null)
                            {
                                if (file.Level == SPFileLevel.Checkout || file.CheckOutType != SPFile.SPCheckOutType.None)
                                {
                                    CheckInFileByUser(file, string.Format("Published by IntegrityCheck on behalf of {0} on {1}", spUser.Name, DateTime.Now.ToString()),
                                        SPCheckinType.MajorCheckIn, spUser);
                                }
                                else if (file.Level == SPFileLevel.Draft)
                                {
                                    file.Publish(string.Format("Published by IntegrityCheck on behalf of {0} on {1}", spUser.Name, DateTime.Now.ToString()));
                                }
                                if (file.Item.ParentList.EnableModeration)
                                {
                                    file.Approve(string.Format("Approved by IntegrityCheck on behalf of {0} on {1}", spUser.Name, DateTime.Now.ToString()));
                                }
                                string details = "Published by IntegrityCheck";
                                AuditLogger.LogPublishingStatus(file.Title + "|" + fileUrl, details);
                            }
                            else
                            {
                                string details = string.Format("Error occurred while publishing content by Integrity Check. Details: User {0} not found", AuditLogger.User);
                                AuditLogger.LogException(file.Title + "|" + fileUrl, details);
                            }
                        }
                        else
                        {
                            ApproveItem(web, file.Item.ParentList, file.Item);
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    log.TraceException(ex);
                    RecordLastException(ex);

                    string details = "Error occurred while publishing content by Integrity Check. Details: " + ex.Message;
                    AuditLogger.LogException(file.Title + "|" + fileUrl, details);
                }
            }
            return false;
        }
        #endregion

        #endregion
    }
}