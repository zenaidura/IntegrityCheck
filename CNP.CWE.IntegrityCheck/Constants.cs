using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CNP.CWE.IntegrityCheck
{
    public static class Constants
    {
        public const string DispFormWithID = "DispForm.aspx?ID=";
        public const string AuditLogList = "Integrity Check Audit Log";

        public class WebProperties
        {
            public const string PUBLISHINGTOPURL = "publishingtopurl";
        }
        public static class AuditList 
        {
            public const string ActionDate = "Action Date";
            public const string Item = "Item";
            public const string ActionTakenBy = "Action Taken By";
            public const string Status = "Status";
            public const string StatusDetail = "Status Detail";
        }
        public static class FieldNames
        {
            public const string ApprovalStatus = "Approval Status";
            public const string Expiration = "Expiration";
            public const string ScheduledStartDate = "Scheduling Start Date";
            public const string ScheduledEndDate = "Scheduling End Date";
            public const string Audience = "Audience";
            public const string ServiceArea = "Service Area";
            public const string ContentType = "ContentTypeId";
        }
    }
}