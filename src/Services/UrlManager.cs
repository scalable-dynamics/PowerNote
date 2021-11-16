using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace PowerNote.Services;

public class UrlManager
{
    public static string CreateUrl(byte[] bytes, string type = "")
    {
        var data = Convert.ToBase64String(bytes);
        if (string.IsNullOrEmpty(type))
        {
            return $"#{data}";
        }
        else
        {
            return $"#{type}:{data}";
        }
    }

    private readonly NavigationManager _navigationManager;
    private readonly FileManager _fileManager;
    private readonly IJSInProcessRuntime _jsRuntime;

    public UrlManager(NavigationManager navigationManager, FileManager fileManager, IJSInProcessRuntime jsRuntime)
    {
        _navigationManager = navigationManager;
        _fileManager = fileManager;
        _jsRuntime = jsRuntime;
    }

    public void ReadUrl(Action onFilesAdded)
    {
        var fragment = new Uri(_navigationManager.Uri, UriKind.Absolute).Fragment;
        if (fragment.StartsWith('#'))
        {
            try
            {
                var data = fragment.TrimStart('#');
                var index = data.IndexOf(':');
                if (index > -1)
                {
                    var type = data.Substring(0, index);
                    var file = data.Substring(index + 1);
                    var bytes = Convert.FromBase64String(file);
                    if (type == "msapp")
                    {
                        Console.WriteLine("Reading App File...");
                        _fileManager.WriteApp("App", bytes);
                        onFilesAdded();
                    }
                    else if (type == "zip")
                    {
                        Console.WriteLine("Reading Zip File...");
                        _fileManager.SaveAppPackage(bytes);
                        onFilesAdded();
                    }
                    else if (type == "xlsx")
                    {
                        Console.WriteLine("Reading Excel File...");
                        _fileManager.WriteExcel("Doc", bytes);
                        onFilesAdded();
                    }
                    else if (type == "json")
                    {
                        Console.WriteLine("Reading Flow File...");
                        _fileManager.WriteFlow("Flow", bytes);
                        onFilesAdded();
                    }
                }
                else
                {
                    Console.WriteLine("Reading Raw Text...");
                    var bytes = Convert.FromBase64String(data);
                    _fileManager.WriteCode("Code", bytes);
                    onFilesAdded();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error Reading Url Fragment: " + ex.ToString());
                SetUrl(_navigationManager.BaseUri);
            }
        }
    }

    public void SetUrl(string url)
    {
        _jsRuntime.InvokeVoid("location.replace", url);
    }

}
