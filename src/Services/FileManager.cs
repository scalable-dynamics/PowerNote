using Microsoft.AspNetCore.Components.Forms;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace PowerNote.Services;

public class FileManager
{
    private const string Letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string AppsFolderName = "Apps";
    private DirectoryInfo AppsFolder => Directory.CreateDirectory(AppsFolderName);
    private string AppFilePath(string name) => Path.Combine(AppsFolder.FullName, name);
    private string ColumnLetter(int column) => 
        column < 1
        ? "A"
        :
        column < Letters.Length
        ? Letters[column - 1].ToString()
        : string.Concat(Enumerable.Repeat(Letters[column - 1].ToString(), column / Letters.Length));

    private Dictionary<string, PowerApp> _powerApps = new();
    private string _codeFromFile;
    private readonly AppReader _appReader;
    private readonly ExcelReader _excelReader;

    public FileManager(AppReader appReader, ExcelReader excelReader)
    {
        _appReader = appReader;
        _excelReader = excelReader;
    }

    public async ValueTask<byte[]> GetPowerAppFileAsync(string name) => await File.ReadAllBytesAsync(_powerApps[name].FilePath);

    public PowerAppComponent[] GetPowerAppComponents(string name) => _powerApps[name].Components;

    public PowerApp[] GetPowerApps() => _powerApps.Values.ToArray();
    public string GetLoadedCode() => _codeFromFile;

    public void WriteCode(string name, byte[] bytes) => File.WriteAllBytes(AppFilePath(name + ".pfx"), bytes);

    public void WriteApp(string name, byte[] bytes) => File.WriteAllBytes(AppFilePath(name + ".msapp"), bytes);

    public void WriteExcel(string name, byte[] bytes) => File.WriteAllBytes(AppFilePath(name + ".xlsx"), bytes);

    public void WriteFlow(string name, byte[] bytes) => File.WriteAllBytes(AppFilePath(name + ".json"), bytes);

    public string GetAppUrl(PowerApp app)
    {
        var ext = Path.GetExtension(app.FilePath).TrimStart('.').ToLower();
        return UrlManager.CreateUrl(File.ReadAllBytes(app.FilePath), ext);
    }

    public void ReadAppsFolder()
    {
        var dir = AppsFolder;
        var files = Directory.EnumerateFiles(dir.FullName, "*.*", SearchOption.AllDirectories).ToArray();
        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var ext = Path.GetExtension(file).TrimStart('.');
            try
            {
                if (ext.Equals("msapp", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!_powerApps.TryGetValue(name, out var app))
                    {
                        _powerApps[name] = _appReader.ReadApp(file);
                    }
                    else
                    {
                        Console.WriteLine($"ReadApp ({name}): Already loaded");
                    }
                }
                else if (ext.Equals("zip", StringComparison.InvariantCultureIgnoreCase))
                {
                    SaveAppPackage(File.ReadAllBytes(file));
                }
                else if (ext.Equals("xlsx", StringComparison.InvariantCultureIgnoreCase))
                {
                    var components = ReadExcelFile(file);
                    _powerApps[name] = new(name, components, file);
                }
                else if (ext.Equals("json", StringComparison.InvariantCultureIgnoreCase))
                {
                    _powerApps[name] = ReadFlow(file);
                }
                else if (ext.Equals("pfx", StringComparison.InvariantCultureIgnoreCase))
                {
                    _codeFromFile = File.ReadAllText(file);
                }
                else
                {
                    Console.WriteLine($"Removing unnecessary file: {file}");
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReadApp Error ({name}): {ex.ToString()}");
            }
        }
    }

    public void SaveAppPackage(byte[] zipFile)
    {
        var apps = WriteAppPackage(zipFile);
        foreach(var app in apps)
        {
            _powerApps[app.Name] = app;
        }
    }

    private PowerAppComponent[] ReadExcelFile(string xlsxFilePath)
    {
        var components = new List<PowerAppComponent>();
        var cells = _excelReader.ReadExcel(xlsxFilePath);
        var sheets = cells.GroupBy(c => c.Sheet).ToArray();
        foreach (var sheet in sheets)
        {
            var rows = sheet.GroupBy(c => c.Row).ToArray();
            var types = new Dictionary<string, string>();
            var objects = new Dictionary<string, Dictionary<string, string>>();
            foreach (var row in rows)
            {
                foreach (var column in row)
                {
                    var cell = column.Formula ?? column.Text;
                    if (!string.IsNullOrWhiteSpace(cell))
                    {
                        objects[$"{ColumnLetter(column.Column)}{column.Row}"] = new()
                        {
                            { "Text", cell },
                            { "X", ((column.Column - 1) * 120).ToString() },
                            { "Y", ((column.Row - 1) * 20).ToString() },
                            { "Width", "120" },
                            { "Height", "120" }
                        };
                    }
                }
            }
            components.Add(new(sheet.Key, types, objects));
        }
        return components.ToArray();
    }

    public PowerApp ReadFlow(string jsonFilePath)
    {
        var name = Path.GetFileNameWithoutExtension(jsonFilePath);
        var json = File.ReadAllText(jsonFilePath);
        var formulas = Regex.Matches(json, @"""@\{?.*\}?""").Select(m => m.Value).ToArray();
        var component = new PowerAppComponent("Formulas", new(), formulas.Distinct().ToDictionary(f => f, formula => new Dictionary<string, string>()
                {
                    { "Text", formula }
                }));
        return new(name, new[] { component }, jsonFilePath);
    }

    public async ValueTask UploadFilesAsync(IEnumerable<IBrowserFile> files)
    {
        foreach (var item in files)
        {
            var path = AppFilePath(item.Name);
            using var stream = item.OpenReadStream();
            using var file = File.Create(path);
            await stream.CopyToAsync(file);
        }
    }

    private IEnumerable<PowerApp> WriteAppPackage(byte[] zipFile)
    {
        using var stream = new MemoryStream(zipFile);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (entry.Name.EndsWith(".msapp", StringComparison.InvariantCultureIgnoreCase))
            {
                var path = AppFilePath(entry.Name);
                entry.ExtractToFile(path);
                yield return _appReader.ReadApp(path);
            }
            else if (entry.Name.EndsWith(".json", StringComparison.InvariantCultureIgnoreCase)
                    && entry.FullName.Contains("workflows", StringComparison.InvariantCultureIgnoreCase))
            {
                var path = AppFilePath(entry.Name);
                entry.ExtractToFile(path);
                yield return ReadFlow(path);
            }
        }
    }
}