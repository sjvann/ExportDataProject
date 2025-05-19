using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExportData.Models.Records
{
    public class RecordBaseModel
    {
        public int Id { get; set; }
        public IEnumerable<KeyValuePair<string, object>>? FieldValue { get; set; }
    }
}
