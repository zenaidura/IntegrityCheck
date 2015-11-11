using CNP.CWE.IntegrityCheck.Model;
using CNP.CWE.SharePoint.Common.Diagnostics;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Publishing;
using Microsoft.SharePoint.Utilities;
using Newtonsoft.Json;
using SharePoint.Intranet.LinkChecker.Functions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace CNP.CWE.IntegrityCheck.Controllers
{
    [RoutePrefix("api/integritycheck")]
    public class IntegrityCheckController : ApiController
    {
        [HttpGet]
        [Route("")]
        public bool Check()
        {
            return true;
        }

        [HttpPost()]
        [AcceptVerbs("POST", "GetItemInfo")]
        [Route("GetItemInfo")]
        public List<ItemInfo> GetItemInfo([FromBody]string data)
        {
            var JsonData = JsonConvert.DeserializeObject<Dictionary<string, Object>>(data);

            string siteId = JsonData.ContainsKey("site") ? (JsonData["site"]??"").ToString(): "";
            string siteUrl = JsonData.ContainsKey("siteUrl") ? (JsonData["siteUrl"] ?? "").ToString() : "";
            string webId = JsonData.ContainsKey("web") ? (JsonData["web"] ?? "").ToString() : "";
            string listId = JsonData.ContainsKey("list") ? (JsonData["list"] ?? "").ToString() : "";
            string[] itemIds = JsonData.ContainsKey("items") ? ((Newtonsoft.Json.Linq.JArray)JsonData["items"]).ToObject<string[]>() : null;

            List<ItemInfo> pages = null;

            try
            {
                DraftChecker.SetDiagnosticsCategory("CenterPoint Integrity Check");

                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    if (!string.IsNullOrEmpty(siteId) && !string.IsNullOrEmpty(webId))
                    {
                        Recorder.Logger.TraceInformation(string.Format("Opening site: {0} | {1}", siteId, siteUrl));
                        using (SPSite site = new SPSite(new Guid(siteId)))
                        {
                            Recorder.Logger.TraceInformation("Opening web: " + webId);
                            using (SPWeb web = site.OpenWeb(new Guid(webId)))
                            {
                                if (PublishingWeb.IsPublishingWeb(web))
                                {
                                    if (itemIds != null)
                                    {
                                        if (!string.IsNullOrEmpty(listId))
                                        {
                                            Recorder.Logger.TraceInformation("Getting items from list: " + listId);
                                            pages = DraftChecker.GetItems(web, listId, itemIds);
                                        }
                                        else
                                        {
                                            pages = DraftChecker.GetPage(web, itemIds);
                                        }
                                    }
                                    else
                                    {
                                        pages = DraftChecker.GetAllPages(web);
                                    }
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Recorder.Logger.TraceException(ex);
            }
            return pages;
        }

       
        [HttpGet]
        [Route("{loc}/site/{siteId}/web/{webId}/list/{listId}/items/{itemId?}")]
        public ItemInfo GetDraftAssets(string loc, string siteId, string webId, string listId, string itemId = "")
        {
            ItemInfo itemInfo = new ItemInfo();
            List<DraftAsset> assets = null;
            Dictionary<string, List<DynamicAsset>> dynamicAssets = null;

            DraftChecker.SetDiagnosticsCategory("CenterPoint Integrity Check");
            DraftChecker.Location = loc;

            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    if (!string.IsNullOrEmpty(siteId) && !string.IsNullOrEmpty(webId))
                    {
                        using (SPSite site = new SPSite(new Guid(siteId)))
                        {
                            using (SPWeb web = site.OpenWeb(new Guid(webId)))
                            {
                                if (PublishingWeb.IsPublishingWeb(web))
                                {
                                    PublishingWeb pubWeb = PublishingWeb.GetPublishingWeb(web);

                                    //Check if the pageId is integer ID or page name
                                    int id = 0;
                                    SPListItem pageItem = null;
                                    SPList list = web.Lists[new Guid(listId)];

                                    if (int.TryParse(itemId, out id))
                                    {
                                        pageItem = list.GetItemById(id);
                                    }
                                    else
                                    {
                                        SPFile file = web.GetFile(SPUtility.ConcatUrls(pubWeb.PagesListName, itemId));
                                        if (file != null)
                                        {
                                            pageItem = file.Item;
                                        }
                                    }
                                    //Scan and return draft items referenced on the page
                                    assets = DraftChecker.GetDraftContent(site, pageItem);

                                    //Scan and return unapproved dynamic items
                                    dynamicAssets = DraftChecker.GetUnapprovedItems(site, pageItem);

                                    itemInfo.Url = SPUtility.ConcatUrls(web.ServerRelativeUrl, pageItem.Url);
                                    itemInfo.Title = pageItem.Title;
                                    itemInfo.Id = pageItem.ID;
                                    itemInfo.DraftAssets = assets;
                                    itemInfo.DynamicAssets = dynamicAssets;
                                    itemInfo.LastError = DraftChecker.GetLastException();
                                }
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Recorder.Logger.TraceException(ex);
                itemInfo.LastError = ex.Message;
            }
            return itemInfo;
        }

        [HttpPost()]
        [AcceptVerbs("POST", "PublishAsset")]
        [Route("PublishAsset")]
        public bool PublishAsset([FromBody]string data)
        {
            var JsonData = JsonConvert.DeserializeObject<Dictionary<string, Object>>(data);

            string siteId = JsonData.ContainsKey("site") ? (JsonData["site"] ?? "").ToString() : "";
            string webId = JsonData.ContainsKey("web") ? (JsonData["web"] ?? "").ToString() : "";
            string fileUrl = JsonData.ContainsKey("file") ? (JsonData["file"] ?? "").ToString() : "";
            string listId = JsonData.ContainsKey("list") ? (JsonData["list"] ?? "").ToString() : "";
            string itemId = JsonData.ContainsKey("item") ? (JsonData["item"] ?? "").ToString() : "";
            string user = JsonData.ContainsKey("user") ? (JsonData["user"] ?? "").ToString() : "";
            string logSiteId = JsonData.ContainsKey("logSite") ? (JsonData["logSite"] ?? "").ToString() : "";

            bool bSuccess = false;

            DraftChecker.SetDiagnosticsCategory("CenterPoint Integrity Check");

            try
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    if (!string.IsNullOrEmpty(siteId) && !string.IsNullOrEmpty(webId) && !string.IsNullOrEmpty(logSiteId))
                    {
                        using (SPSite site = new SPSite(new Guid(siteId)))
                        {
                            using (SPWeb web = site.OpenWeb(new Guid(webId)))
                            {
                                AuditLogger.LogSiteId = logSiteId;
                                bSuccess = DraftChecker.PublishContent(web, fileUrl, listId, itemId, user);
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Recorder.Logger.TraceException(ex);
            }

            return bSuccess;
        }
    }
}
