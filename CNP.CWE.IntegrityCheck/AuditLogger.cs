using CNP.CWE.SharePoint.Common.Diagnostics;
using Microsoft.SharePoint;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CNP.CWE.IntegrityCheck
{
    public static class AuditLogger
    {

        internal static string LogSiteId = "";
        public static string User { get; set; }
        internal static bool IsError { get; set; }

        public static void LogException(string url, string error)
        {
            Log(url, error, "Error");
        }

        public static void LogPublishingStatus(string url, string statusDetails)
        {
            Log(url, statusDetails, "Published");
        }
        public static void LogApprovalStatus(string url, string statusDetails)
        {
            Log(url, statusDetails, "Approved");
        }

        private static void Log(string url, string message, string status)
        {
            IsError = false;
            Guid siteGuid = Guid.Parse(LogSiteId);

            if (siteGuid != Guid.Empty)
            {
                SPSecurity.RunWithElevatedPrivileges(delegate()
                {
                    using (SPSite site = new SPSite(siteGuid))
                    {
                        using (SPWeb LogWeb = site.RootWeb)
                        {
                            if (LogWeb != null && LogWeb.Exists)
                            {
                                Recorder.Logger.TraceInformation(string.Format("Publishing content at {0} on site {1}", url, LogWeb.Url));
                                try
                                {
                                    SPUser spUser = Utility.GetUserById(LogWeb, User);
                                    if (spUser != null)
                                    {
                                        SPList list = LogWeb.Lists[Constants.AuditLogList];

                                        if (list != null)
                                        {
                                            LogWeb.AllowUnsafeUpdates = true;
                                            SPListItem newItem = list.AddItem();
                                            newItem[Constants.AuditList.ActionDate] = DateTime.Now;
                                            SPFieldUrlValue itemUrl = new SPFieldUrlValue();
                                            string[] urlValue = url.Split('|');
                                            itemUrl.Description = urlValue[0];
                                            itemUrl.Url = urlValue[1];
                                            newItem[Constants.AuditList.Item] = itemUrl;
                                            newItem[Constants.AuditList.ActionTakenBy] = spUser;
                                            newItem[Constants.AuditList.Status] = status;
                                            newItem[Constants.AuditList.StatusDetail] = message;
                                            newItem.Update();
                                            Recorder.Logger.TraceInformation("Content is published successfully");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    IsError = true;
                                    Recorder.Logger.TraceException(ex);
                                }
                            }
                        }
                    }
                });
            }
        }
    }
}