using Microsoft.PowerPlatform.Formulas.Tools;

namespace PowerNote.Services;

public record PowerApp(string Name, PowerAppComponent[] Components, string FilePath)
{
    public override string ToString() => Name;
}

public record PowerAppComponent(string Name, Dictionary<string, string> Types, PowerAppComponentObject[] Objects)
{
    public PowerAppComponent(string name, Dictionary<string, string> types, Dictionary<string, Dictionary<string, string>> objects)
        : this(name, types, objects.Select(o => new PowerAppComponentObject(o.Key, o.Value)).ToArray()) {}

    public override string ToString() => Name;
}

public record PowerAppComponentObject(string Name, Dictionary<string, string> Data)
{
    public override string ToString() => Name;
}

public class AppReader
{
    private readonly YamlReader _yamlReader;

    public AppReader(YamlReader yamlReader)
    {
        _yamlReader = yamlReader;
    }

    public PowerApp ReadApp(string msappFilePath)
    {
        var name = Path.GetFileNameWithoutExtension(msappFilePath);
        var components = ReadAppComponents(name, File.OpenRead(msappFilePath)).ToArray();
        return new(name, components, msappFilePath);
    }

    public IEnumerable<PowerAppComponent> ReadAppComponents(string msappFilePath)
    {
        var name = Path.GetFileNameWithoutExtension(msappFilePath);
        return ReadAppComponents(name, File.OpenRead(msappFilePath));
    }

    private IEnumerable<PowerAppComponent> ReadAppComponents(string name, Stream msappFileStream)
    {
        (CanvasDocument msApp, ErrorContainer errors) = CanvasDocument.LoadFromMsapp(msappFileStream);
        errors.Write(Console.Error);

        if (msApp == null || errors.HasErrors)
        {
            errors.Write(Console.Error);
            Console.WriteLine("*** ReadAppComponents: CanvasDocument {0} could not be read.", name);
            yield break;
        }

        var info = Directory.CreateDirectory(name);
        errors = msApp.SaveToSources(info.FullName, verifyOriginalPath: null);
        if (errors.HasErrors)
        {
            errors.Write(Console.Error);
            yield break;
        }

        var files = Directory.EnumerateFiles(info.FullName, "*.yaml", SearchOption.AllDirectories).ToArray();
        Console.WriteLine("*** CanvasDocument {0} contains {1} files", name, files.Length);
        foreach (var file in files)
        {
            if (!file.Contains("/tests/", StringComparison.InvariantCultureIgnoreCase))
            {
                Console.WriteLine(file);
                var component = Path.GetFileNameWithoutExtension(file);
                var data = _yamlReader.ReadYaml(file);
                yield return new(component, data.Types, data.Objects);
                Console.WriteLine();
            }
        }
    }
}
