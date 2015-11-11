using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.SharePoint;
using System.Collections;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.SharePoint.WebPartPages;
using Microsoft.SharePoint.Publishing.WebControls;
using Microsoft.SharePoint.Publishing.Fields;
using Microsoft.SharePoint.Publishing;
using System.IO;
using CNP.CWE.IntegrityCheck;
using CNP.CWE.IntegrityCheck.Model;
using Microsoft.SharePoint.Utilities;

namespace SharePoint.Intranet.LinkChecker.Functions
{
    public class SiteLinkChecker
    {
        public List<DraftAsset> BrokenLinks { get; set; }
        public Hashtable CheckedLinks { get; set; }

        public string WWWUrl { get; set; }

        public SiteLinkChecker()
        {
            CheckedLinks = new Hashtable();
            BrokenLinks = new List<DraftAsset>();
        }

        /// <summary>
        /// Scans an entire site collection for broken links
        /// </summary>
        /// <param name="SiteToCheck">The site collection to scan for broken links</param>
        public void CheckLinksInSite(SPSite SiteToCheck)
        {
            DateTime MaximumRunTime = DateTime.Now.AddMinutes(45);
            Uri BaseUri = new Uri(SiteToCheck.Url);
            foreach (SPWeb web in SiteToCheck.AllWebs)
            {
                if (DateTime.Compare(DateTime.Now, MaximumRunTime) < 0)
                {
                    CheckLinksInWeb(web, BaseUri);
                    web.Dispose();
                }
                else
                {
                    break;
                }                
            }
        }

        /// <summary>
        /// Checks through a specific site to find broken links
        /// </summary>
        /// <param name="WebToCheck">The site to scan for broken links</param>
        /// <param name="BaseUri">The base url of the site collection that the site is found in</param>
        public void CheckLinksInWeb(SPWeb WebToCheck, Uri BaseUri)
        {
            // Check lists
            CheckListLinks(WebToCheck.Lists, BaseUri);

            //TODO: Check navigation links

            //TODO: Check links in web part pages

        }

        public void CheckLinksOnPage(SPListItem page, Uri BaseUri)
        {
            Uri PageUri = new Uri(BaseUri, page.File != null ? page.File.ServerRelativeUrl : page.Url);

            ScanListFields(page, PageUri);
            if (page.File != null)
            {
                ScanWebParts(page.File, PageUri);
            }
        }

        /// <summary>
        /// Checks through a collection of SharePoint lists to find broken links within the list content
        /// </summary>
        /// <param name="ListsToCheck">The collection of lists to check for broken links</param>
        /// <param name="BaseUri">The base url of the site collection that the lists are found in</param>
        public void CheckListLinks(SPListCollection ListsToCheck, Uri BaseUri)
        {
            foreach (SPList list in ListsToCheck)
            {

                SPListItemCollection items = list.Items;
                switch (list.BaseTemplate.ToString())
                {
                    case "850": // Pages library

                        foreach (SPListItem page in items)
                        {
                            
                            Uri PageUri = new Uri(BaseUri, page.File.ServerRelativeUrl);

                            ScanListFields(page, PageUri);
                            ScanWebParts(page.File, PageUri);
                        }
                        break;

                    default:
                        foreach (SPListItem item in items)
                        {
                            Uri PageUri = new Uri(BaseUri, item.Url);
                            ScanListFields(item, PageUri);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Scans through the fields in the list to check supported fields for broken links
        /// </summary>
        /// <param name="Item">The list item to check</param>
        /// <param name="PageUri">The full url of the page</param>
        private void ScanListFields(SPListItem Item, Uri PageUri)
        {
            foreach (SPField field in Item.Fields)
            {
                switch (field.TypeAsString)
                {
                    case "HTML":
                    case "Link":
                        try
                        {
                            if ((Item[field.Title] != null) && !string.IsNullOrEmpty(Item[field.Title].ToString()))
                            {
                                StringCollection AllLinks = LinkCheckerUtilities.GetLinksFromHTML(Item[field.Title].ToString());

                                // Make all the links relative then validate
                                foreach (string Url in AllLinks)
                                {
                                    try
                                    {
                                        ValidateLink(PageUri, new Uri(PageUri, Url), Url, BrokenLinkType.HtmlField);
                                    }
                                    catch (Exception ex)
                                    {
                                        Utility.LogError(ex, string.Format("LinkChecker: Error checking link '{0}' at '{1}'", Url, PageUri.ToString()));
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Utility.LogError(ex, string.Format("LinkChecker: Error: Unable to check links for field {0} at {1} - Error Message: {2}, Stack Trace: {3}", field.Title, Item.Url, ex.Message, ex.StackTrace));
                        }
                        break;
                    case "Note":
                        SPFieldMultiLineText fieldAsMultiLineText = (SPFieldMultiLineText)field;
                        if ((fieldAsMultiLineText.RichText) && (Item[field.Title] != null) && !string.IsNullOrEmpty(Item[field.Title].ToString()))
                        {
                            StringCollection AllLinks = LinkCheckerUtilities.GetLinksFromHTML(Item[field.Title].ToString());

                            foreach (string Url in AllLinks)
                            {
                                try
                                {
                                    ValidateLink(PageUri, new Uri(PageUri, Url), Url, BrokenLinkType.HtmlField);
                                }
                                catch (Exception ex)
                                {
                                    Utility.LogError(ex, string.Format("LinkChecker: Error checking link '{0}' at '{1}'", Url, PageUri.ToString()));
                                }
                            }
                        }
                        break;
                    case "URL":
                        try
                        {
                            if ((Item[field.Title] != null) && !string.IsNullOrEmpty(Item[field.Title].ToString()))
                            {
                                SPFieldUrlValue UrlValue = new SPFieldUrlValue(Item[field.Title].ToString());
                                try
                                {
                                    ValidateLink(PageUri, new Uri(UrlValue.Url), UrlValue.Url, BrokenLinkType.LinkField);
                                }
                                catch (Exception ex)
                                {
                                    Utility.LogError(ex, string.Format("LinkChecker: Error checking link '{0}' at '{1}'", UrlValue.Url, PageUri.ToString()));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Utility.LogError(ex, string.Format("LinkChecker: Error: Unable to check links for field {0} at {1} - Error Message: {2}, Stack Trace: {3}", field.Title, Item.Url, ex.Message, ex.StackTrace));
                        }
                        break;
                    case "SPLink":
                        try
                        {
                            if ((Item[field.Title] != null) && !string.IsNullOrEmpty(Item[field.Title].ToString()))
                            {
                                try
                                {
                                    string Url =  new LinkFieldValue(Item[field.Title].ToString()).NavigateUrl;
                                    Uri uri = new Uri(PageUri, Url);
                                    ValidateLink(PageUri, uri, Url, BrokenLinkType.LinkField);
                                }
                                catch (Exception ex)
                                {
                                    Utility.LogError(ex, string.Format("LinkChecker: Error checking link '{0}' at '{1}'", Item[field.Title].ToString(), PageUri.ToString()));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Utility.LogError(ex, string.Format("LinkChecker: Error: Unable to check links for field {0} at {1} - Error Message: {2}, Stack Trace: {3}", field.Title, Item.Url, ex.Message, ex.StackTrace));
                        }
                        break;
                    case "Image":
                        try
                        {
                            if ((Item[field.Title] != null) && !string.IsNullOrEmpty(Item[field.Title].ToString()))
                            {
                                ImageFieldValue ImgValue = new ImageFieldValue(Item[field.Title].ToString());
                                try
                                {
                                    Uri uri = new Uri(PageUri, ImgValue.ImageUrl);
                                    ValidateLink(PageUri, uri, ImgValue.ImageUrl, BrokenLinkType.LinkField);
                                }
                                catch (Exception ex)
                                {
                                    Utility.LogError(ex, string.Format("LinkChecker: Error checking image link '{0}' at '{1}'", ImgValue.ImageUrl, PageUri.ToString()));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Utility.LogError(ex, string.Format("LinkChecker: Error: Unable to check links for field {0} at {1} - Error Message: {2}, Stack Trace: {3}", field.Title, Item.Url, ex.Message, ex.StackTrace));
                        }
                        break;
                    case "SummaryLinks":
                        try
                        {
                            if ((Item[field.Title] != null) && !string.IsNullOrEmpty(Item[field.Title].ToString()))
                            {
                                SummaryLinkFieldValue LinksFieldValue = new SummaryLinkFieldValue(Item[field.Title].ToString());

                                foreach (SummaryLink Link in LinksFieldValue.SummaryLinks)
                                {
                                    try
                                    {
                                        ValidateLink(PageUri, new Uri(Link.LinkUrl), Link.LinkUrl, BrokenLinkType.SummaryLinkField);
                                    }
                                    catch (Exception ex)
                                    {
                                        Utility.LogError(ex, string.Format("LinkChecker: Error checking link '{0}' at '{1}'", Link.LinkUrl, PageUri.ToString()));
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Utility.LogError(ex, string.Format("LinkChecker: Error: Unable to check links for field {0} at {1} - Error Message: {2}, Stack Trace: {3}", field.Title, Item.Url, ex.Message, ex.StackTrace));
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Scans through supported web parts on the specific page to check to see if there are any broken links in them
        /// </summary>
        /// <param name="File">The file to parse the web parts from</param>
        /// <param name="PageUri">The full url of the page</param>
        private void ScanWebParts(SPFile File, Uri PageUri)
        {
            SPLimitedWebPartManager wpm = File.GetLimitedWebPartManager(System.Web.UI.WebControls.WebParts.PersonalizationScope.Shared);
            StringCollection AllLinks;

            foreach (var wp in wpm.WebParts)
            {
                switch (wp.GetType().FullName)
                {
                    case "Microsoft.SharePoint.WebPartPages.ContentEditorWebPart":
                        ContentEditorWebPart ContentEditorWP = (ContentEditorWebPart)wp;

                        AllLinks = LinkCheckerUtilities.GetLinksFromHTML(ContentEditorWP.Content.OuterXml);

                        foreach (string Url in AllLinks)
                        {
                            ValidateLink(PageUri, new Uri(PageUri, Url), Url, BrokenLinkType.ContentEditorWebPart);
                        }
                        break;
                    case "Microsoft.SharePoint.Publishing.WebControls.SummaryLinkWebPart":
                        SummaryLinkWebPart SummaryLinkWP = (SummaryLinkWebPart)wp;

                        AllLinks = LinkCheckerUtilities.GetLinksFromHTML(SummaryLinkWP.SummaryLinkStore);

                        foreach (string Url in AllLinks)
                        {
                            ValidateLink(PageUri, new Uri(PageUri, Url), Url, BrokenLinkType.SummaryLinkWebPart);
                        }
                        break;
                }
            }

            wpm.Dispose();

        }

        /// <summary>
        /// Adds a new broken link entry to the current objects collection
        /// </summary>
        /// <param name="Source">Where the broken link came from</param>
        /// <param name="Details">Information about the broken link</param>
        private void AddBrokenLink(Uri Source, BrokenLink Details)
        {
            if (!Details.Url.ToString().Contains("_catalogs"))
            {
                BrokenLinks.Add(new DraftAsset() { Url = Details.Url.ToString(), IsBroken = true, FileType = "Broken Link" });
            }
        }

        /// <summary>
        /// Validates a link by checking the cache to see if the response has already been checked and recording broken links
        /// </summary>
        /// <param name="PageUri">The Uri of the page the link is on</param>
        /// <param name="UriToValidate">The Uri of the link to check</param>
        /// <param name="LinkType">The type of the link being checked</param>
        private void ValidateLink(Uri PageUri, Uri UriToValidate, string originalUrl, BrokenLinkType LinkType)
        {
            //Validate if original url is absolute and its pointing to authoring or publishing site instead of relative link
            Uri originalUri = new Uri(originalUrl, UriKind.RelativeOrAbsolute);

            if (originalUri.IsAbsoluteUri)
            {
                string originalAuthority = originalUri.GetLeftPart(UriPartial.Authority).ToLower();
                string pageAuthority = PageUri.GetLeftPart(UriPartial.Authority).ToLower();

                if (originalAuthority.Equals(WWWUrl.ToLower()))
                { 
                    //Absolute URL for www site
                }
                else if (originalAuthority.Equals(pageAuthority))
                {
                    //Absolute URL for publishing site
                }
                else
                { 
                    //External Url
                    LinkType = BrokenLinkType.ExternalLink;
                }
            }

            if (!CheckedLinks.Contains(UriToValidate.ToString()))
            {
                if (UriToValidate.ToString().ToLower().Contains("fixupredirect.aspx"))
                {
                    CheckedLinks.Add(UriToValidate.ToString(), true);
                }
                else if (!LinkCheckerUtilities.LinkIsValid(UriToValidate))
                {
                    Utility.LogInformation("Found broken link: " + UriToValidate.ToString());
                    AddBrokenLink(PageUri, new BrokenLink(UriToValidate, LinkType));
                    CheckedLinks.Add(UriToValidate.ToString(), false);
                }
                else
                {
                    CheckedLinks.Add(UriToValidate.ToString(), LinkType != BrokenLinkType.ExternalLink);
                }
            }
        }
    }
}
