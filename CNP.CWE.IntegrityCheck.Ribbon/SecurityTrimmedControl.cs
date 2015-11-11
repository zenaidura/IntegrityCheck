using Microsoft.SharePoint;
using Microsoft.SharePoint.WebControls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using Microsoft.Web.CommandUI;

namespace CNP.CWE.IntegrityCheck.Ribbon.Controls
{
    [ToolboxData("<{0}:SecurityTrimmedControl runat=server></{0}:SecurityTrimmedControl>")]
    public class SecurityTrimmedControl : WebControl
    {
        protected override void CreateChildControls()
        {
            base.CreateChildControls();
        }

        protected override void OnLoad(EventArgs e)
        {
            SPRibbon ribbon = SPRibbon.GetCurrent(this.Page);
            if (ribbon != null)
            {
                //remove Publish button for anyone who does not have Full Control rights to the site.
                if (!SPContext.Current.Web.DoesUserHavePermissions(SPBasePermissions.FullMask))
                {
                    //ribbon.TrimById("Ribbon.PublishTab.Publishing.Schedule");
                    ribbon.TrimById("Ribbon.PublishTab.Publishing.Publish");
                    //ribbon.TrimById("Ribbon.PublishTab");

                    ribbon.TrimById("Ribbon.Documents.Workflow.Moderate");
                    ribbon.TrimById("Ribbon.Documents.Workflow.Publish");
                    //ribbon.TrimById("Ribbon.Documents.Workflow.Unpublish");
                    //ribbon.TrimById("Ribbon.Documents.Workflow.CancelApproval");
                    //ribbon.TrimById("Ribbon.Documents.Workflow");
                    
                    ribbon.TrimById("Ribbon.ListItem.Workflow.Moderate");
                    ribbon.TrimById("Ribbon.ListItem.Workflow");
                }

                if (!SPContext.Current.Web.DoesUserHavePermissions(SPBasePermissions.EditListItems))
                {
                    ribbon.TrimById("Ribbon.Documents.Manage.IntegrityCheckButton");
                    ribbon.TrimById("Ribbon.WikiPageTab.PubPageActions.PageIntegrityCheckButton");
                    ribbon.TrimById("Ribbon.ListItem.Manage.IntegrityCheckButton");
                }
            }
            base.OnLoad(e);
        }
    }
}
