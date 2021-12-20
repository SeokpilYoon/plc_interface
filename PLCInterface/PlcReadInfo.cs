using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLCInterface
{
    public class PlcReadInfo
    {
        public int[] Models { get; set; }
        public int Trigger { get; set; } = 0;
        public int MbbTrigger { get; set; } = 0;

        public PlcReadInfo()
        {
            int modelQty = Convert.ToInt32(ConfigurationManager.AppSettings["ModelQty"] ?? "1");
            Models = new int[modelQty];
        }

    }
}
