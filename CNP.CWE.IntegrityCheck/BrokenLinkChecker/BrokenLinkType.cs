using System;
using System.Collections.Generic;
using System.Text;

namespace SharePoint.Intranet.LinkChecker.Functions
{
    public enum BrokenLinkType
    {
        HtmlField,
        LinkField,
        SummaryLinkField,
        SummaryLinkWebPart,
        ContentEditorWebPart,
        SiteNavigation,
        ExternalLink
    }
}
