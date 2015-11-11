using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Net;
using System.Web;

namespace SharePoint.Intranet.LinkChecker.Functions
{
    public static class LinkCheckerUtilities
    {

        public static StringCollection GetLinksFromHTML(string HtmlContent)
        {
            StringCollection links = new StringCollection();

            MatchCollection AnchorTags = Regex.Matches(HtmlContent.ToLower(), @"(<a.*?>.*?</a>)", RegexOptions.Singleline);
            MatchCollection ImageTags = Regex.Matches(HtmlContent.ToLower(), @"(<img.*?>)", RegexOptions.Singleline);

            foreach (Match AnchorTag in AnchorTags)
            {
                string value = AnchorTag.Groups[1].Value;

                Match HrefAttribute = Regex.Match(value, @"href=\""(.*?)\""",
                    RegexOptions.Singleline);
                if (HrefAttribute.Success)
                {
                    string HrefValue = HrefAttribute.Groups[1].Value;
                    HrefValue = HrefValue.Replace(@"\0026", "&");
                    var ascii = Regex.Match(HrefValue, @"&#1?\d\d;", RegexOptions.Singleline);
                    if (ascii.Success)
                    {
                        string chr = ascii.Groups[0].ToString().Remove(0, 2);
                        chr = chr.Remove(chr.Length-1);
                        int ichr = int.Parse(chr);
                        char decodedChar = (char)ichr;
                        HrefValue = HrefValue.Replace(ascii.Groups[0].ToString(), decodedChar.ToString());
                    }
                    HrefValue = HrefValue.Replace("&#58;", ":");
                    if (!links.Contains(HrefValue))
                    {
                        links.Add(HrefValue);
                    }
                }
            }

            foreach (Match ImageTag in ImageTags)
            {
                string value = ImageTag.Groups[1].Value;

                Match SrcAttribute = Regex.Match(value, @"src=\""(.*?)\""",
                    RegexOptions.Singleline);
                if (SrcAttribute .Success)
                {
                    string SrcValue = SrcAttribute.Groups[1].Value;
                    if (!links.Contains(SrcValue))
                    {
                        links.Add(SrcValue);
                    }
                }
            }

            return links;
        }


        //TODO: Handle other status codes properlly and return more info than just true/false
        public static bool LinkIsValid(Uri UrlToCheck)
        {
            Uri uriToCheck = new Uri(HttpUtility.HtmlDecode(UrlToCheck.ToString()));
            bool ReturnValue = true;
            if ((uriToCheck.Scheme == "http") || (uriToCheck.Scheme == "https"))
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uriToCheck);
                request.Credentials = CredentialCache.DefaultCredentials;

                HttpWebResponse response = null;
                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                    response.Close();
                }
                catch (WebException ex)
                {
                    ReturnValue = false;
                }
                finally
                {
                    if (response != null)
                    {
                        response.Close();
                    }
                }
            }

            return ReturnValue;
        }
    }
}
