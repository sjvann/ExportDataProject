
using ExportData.Models.Config;
using MySqlX.XDevAPI.CRUD;
using System.IO.Compression;
using System.Text;

namespace ExportData
{
    public class ExportService
    {
        private readonly ConfigExControlSection exControlSection;
        private readonly ConfigDeIdentification deIdentification;

        public ExportService(ConfigExControlSection exControlSection, ConfigDeIdentification deIdentification)
        {
            this.exControlSection = exControlSection;
            this.deIdentification = deIdentification;
        }

        public void Export(string tableName, IEnumerable<Dictionary<string, object>>? dataSet)
        {
            if (dataSet == null)
            {
                return;
            }
            if(deIdentification.DeIdentification)
            {
              
            }
            string fileName = $"{tableName}_{DateTime.Now:yyyyMMdd}.csv";
            string fullPath = Path.Combine(exControlSection?.ExportPath ?? new string(@"c:\"), fileName);
            string allCsvLine = CreateCSVString(dataSet);
            File.WriteAllText(fullPath, allCsvLine, Encoding.UTF8);
        }

        public void ZipFiles()
        {   
            string exportPath = exControlSection.ExportPath ?? new string(@"c:\");
            string zipFileName = exControlSection.ZipFileName ?? new string("ExportZip.zip");
            string zipFullFileName = Path.Combine(exportPath, $"{zipFileName}_{DateTime.Now:yyyyMMdd}.zip");
            string[] files = Directory.GetFiles(exportPath, "*.csv");
            if(File.Exists(zipFullFileName))
            {
                File.Delete(zipFullFileName);
            }
            using ZipArchive zip = ZipFile.Open(zipFullFileName, ZipArchiveMode.Create);
            foreach (string file in files)
            {
               zip.CreateEntryFromFile(file, Path.GetFileName(file));
                File.Delete(file);
            }
        }
        private string CreateCSVString(IEnumerable<Dictionary<string, object>>? dataSet)
        {
            List<string> csvSource = new ();
            bool needHead = true;
            if (dataSet != null && dataSet.Any())
            {
                foreach (var item in dataSet)
                {
                    if (needHead)
                    {
                        csvSource.Add(string.Join(",", item.Keys));
                        needHead = false;
                    }
                    csvSource.Add(string.Join(",", item.Values));
                }
            }
            return string.Join("\r", csvSource);
        }

    }
}