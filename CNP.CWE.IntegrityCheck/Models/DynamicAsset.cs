using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CNP.CWE.IntegrityCheck.Model
{
    public class DynamicAsset : DraftAsset
    {
        public int ItemId { get; set; }
        public string ServiceArea { get; set; }
        public string Audience { get; set; }
        public List<DraftAsset> Assets { get; set; }
    }
}