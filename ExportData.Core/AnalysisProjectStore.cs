using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExportData.Core;

public sealed class AnalysisProjectAlreadyExistsException : Exception
{
    public AnalysisProjectAlreadyExistsException(string message)
        : base(message)
    {
    }
}

public sealed class AnalysisProjectNotFoundException : Exception
{
    public AnalysisProjectNotFoundException(string message)
        : base(message)
    {
    }
}

public sealed class AnalysisProjectStore
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly bool _commitRename;

    public AnalysisProjectStore()
        : this(commitRename: true)
    {
    }

    internal AnalysisProjectStore(bool commitRename)
    {
        _commitRename = commitRename;
    }

    public void Save(string folder, AnalysisProject project)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(project);
        DisplayNameRules.EnsureValid(project.DisplayName);
        if (project.TableSnapshots is null
            || project.DeclaredRelations is null
            || project.InferredRelations is null
            || project.Modules is null
            || project.StructureChanges is null)
        {
            throw new ArgumentException("解析專案的集合不可為 null。", nameof(project));
        }

        folder = Path.GetFullPath(folder);
        if (!Directory.Exists(folder))
            throw new DirectoryNotFoundException($"找不到資料夾「{folder}」。");

        var fileName = project.DisplayName + ".analysis.json";
        var targetPath = Path.Combine(folder, fileName);
        if (File.Exists(targetPath))
            throw new AnalysisProjectAlreadyExistsException($"資料夾裡已有「{fileName}」，拒絕覆寫。");

        var tempPath = Path.Combine(folder, fileName + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var committed = false;
        try
        {
            var bytes = Utf8.GetBytes(AnalysisProjectJson.Serialize(project));
            using (var stream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            if (!_commitRename)
                return;

            try
            {
                File.Move(tempPath, targetPath);
            }
            catch (IOException) when (File.Exists(targetPath))
            {
                throw new AnalysisProjectAlreadyExistsException($"資料夾裡已有「{fileName}」，拒絕覆寫。");
            }

            committed = true;
        }
        finally
        {
            if (!committed && _commitRename && File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public AnalysisProject Load(string folder, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        DisplayNameRules.EnsureValid(displayName);

        folder = Path.GetFullPath(folder);
        var fileName = displayName + ".analysis.json";
        var targetPath = Path.Combine(folder, fileName);
        if (!File.Exists(targetPath))
            throw new AnalysisProjectNotFoundException($"找不到「{fileName}」。不會把缺少的檔案當成空的盤點。");

        var json = File.ReadAllText(targetPath, Utf8);
        return AnalysisProjectJson.Deserialize(json);
    }
}

internal static class AnalysisProjectJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    internal static string Serialize(AnalysisProject project) =>
        JsonSerializer.Serialize(project, Options);

    internal static AnalysisProject Deserialize(string json)
    {
        var project = JsonSerializer.Deserialize<AnalysisProject>(json, Options)
            ?? throw new InvalidDataException("解析專案檔是空的。不會把它當成空盤點。");
        return project;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            RespectNullableAnnotations = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        return options;
    }
}
