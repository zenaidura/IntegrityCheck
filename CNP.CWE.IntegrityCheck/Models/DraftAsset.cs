using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace CNP.CWE.IntegrityCheck.Model
{
    public enum Status { None, Failure, Success };

    public class Item
    {
        public string SiteId { get; set; }
        public string WebId { get; set; }
        public string ListId { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public bool PreviouslyPublished { get; set; }
        public string LastPublishedVersion { get; set; }
        public string DraftVersion { get; set; }
        public bool IsDraft { get; set; }
        public string ScheduledToPublish { get; set; }
        public string LastError { get; set; }
        public string ExpirationDate { get; set; }
        public string CheckedOutTo { get; set; }
        public string FileType { get; set; }

        public Item()
        {
            CheckedOutTo = "";
        }
    }

    public class DraftAsset : Item
    {
        public bool IsBroken { get; set; }
        public bool IsInternal { get; set; }
        public List<DraftAsset> Assets { get; set; }
    }

    public class ItemInfo : Item
    {
        public int Id { get; set; }
        public List<DraftAsset> DraftAssets { get; set; }
        public Dictionary<string, List<DynamicAsset>> DynamicAssets { get; set; }
    }
}
