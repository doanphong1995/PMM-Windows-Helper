using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PMM_Windows_Helper
{
    public class AppCatalog
    {
        public int version { get; set; }
        public List<AppItem> apps { get; set; } = new List<AppItem>();
    }

    public class AppItem
    {
        public string id { get; set; }
        public string name { get; set; }
        public string wingetId { get; set; }
        public string group { get; set; }
        public bool defaultSelected { get; set; }
    }
}
