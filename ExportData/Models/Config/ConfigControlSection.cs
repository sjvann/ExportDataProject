using ExportData.Models.EnumType;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExportData.Models.Config
{
    public class ConfigDbControlSection
    {
        public string? ConnectionString { get; set; }
        public EnumDbType? DbType { get; set; }
        public EnumTableType? TableType { get; set; }
        public string[]? TableList { get; set; }
        public string? SqlAllTable { get; set; }
        public string? SqlOneTable { get; set; }
        public string? Owner { get; set; }
        public string? DbName { get; set; }
        public int Size { get; set; }
        public string? Prefix { get; set; }

    }
    public class ConfigExControlSection
    {
        public string? ExportPath { get; set; }
        public bool MakeToZip { get; set; }
        public string? ZipFileName { get; set; }
    }
    public class ConfigDeIdentification
    {
        public bool DeIdentification { get; set; }
        public string[]? Pii { get; set; }
        public bool Phi { get; set; }
    }
}
