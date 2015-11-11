using CNP.CWE.SharePoint.Common.Diagnostics;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Administration;
using Microsoft.SharePoint.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace CNP.CWE.IntegrityCheck
{
    public static class Utility
    {
        public static void LogError(Exception ex, string message=null)
        {
            Recorder.Logger.TraceException(ex);
        }

        public static void LogInformation(string message)
        {
            Recorder.Logger.TraceInformation(message);
        }

        public static SPUser GetUserById(SPWeb web, string user)
        {
            int userId = 0;
            if (!string.IsNullOrEmpty(user))
            {
                if (int.TryParse(user, out userId))
                {
                    SPUser spUser = web.SiteUsers.GetByID(userId);
                    return spUser;
                }
            }
            return null;
        }

        public static string GetPublishingUrl(SPSite site)
        {
            string publishingPortalUrl = "";

            try
            {
                publishingPortalUrl = (site.RootWeb.Properties[Constants.WebProperties.PUBLISHINGTOPURL] ?? site.Url).ToString();
                publishingPortalUrl = SPUtility.ConcatUrls("https" + "://", publishingPortalUrl);
            }
            catch (Exception ex)
            {
                Recorder.Logger.TraceException(ex);
            }
            return publishingPortalUrl.ToLower();
        }
    }
}
