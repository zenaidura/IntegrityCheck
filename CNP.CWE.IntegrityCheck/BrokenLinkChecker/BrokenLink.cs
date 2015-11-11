using System;
using System.Collections.Generic;
using System.Text;

namespace SharePoint.Intranet.LinkChecker.Functions
{
    public class BrokenLink
    {
        public BrokenLink(Uri BrokenUrl, BrokenLinkType TypeOfLink)
        {
            Url = BrokenUrl;
            LinkType = TypeOfLink;
        }

        public Uri Url { get; set; }

        public BrokenLinkType LinkType { get; set; }
        
    }
}
