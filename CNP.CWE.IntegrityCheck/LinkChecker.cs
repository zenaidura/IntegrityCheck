using CNP.CWE.IntegrityCheck.Model;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Utilities;
using Newtonsoft.Json;
using SharePoint.Intranet.LinkChecker.Functions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Web;
using System.Xml;

namespace CNP.CWE.IntegrityCheck
{
    public class LinkChecker
    {
        private SiteLinkChecker linkChecker;
        public LinkChecker()
        {
            linkChecker = new SiteLinkChecker();
        }

        public List<DraftAsset> BrokenLinks { get { return linkChecker.BrokenLinks; } }
        public Hashtable ValidLinks
        {
            get
            {
                return linkChecker.CheckedLinks;
            }
        }
        public void ScanLinksAndImages(SPSite site, SPListItem pageItem)
        {
            ServicePointManager.ServerCertificateValidationCallback = new        
            RemoteCertificateValidationCallback
            (
               delegate { return true; }
            );

            //Convert the www site url to publishing
            linkChecker.WWWUrl = site.Url;
            string publishingPortalUrl = Utility.GetPublishingUrl(site);
            linkChecker.CheckLinksOnPage(pageItem, new Uri(publishingPortalUrl));
        }
    }
}