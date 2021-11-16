namespace PowerNote.Services;

public record YamlObject(Dictionary<string, string> Types, Dictionary<string, Dictionary<string, string>> Objects);
public class YamlReader
{
    const string tabs = "    ";

    public YamlObject ReadYaml(string yamlFile)
    {
        var data = new Dictionary<string, Dictionary<string, string>>();
        var types = new Dictionary<string, string>();
        var yaml = File.ReadAllLines(yamlFile);
        var prev = "";
        var path = "";
        var parentProperty = "";
        var parentData = new Dictionary<string, string>();
        var value = false;
        var level = 0;
        foreach (var line in yaml)
        {
            var depth = 0;
            var indent = tabs;
            var text = line;
            var size = tabs.Length;
            while (text.StartsWith(tabs))
            {
                if (value && depth > level)
                {
                    break;
                }
                indent += tabs;
                depth++;
                text = text.Substring(size);
            }
            if (value && depth <= level)
            {
                parentData[parentProperty] = prev.TrimStart('\r', '\n');
                parentProperty = "";
                value = false;
            }
            else if (value)
            {
                prev = prev + Environment.NewLine + text;
            }
            else
            {
                text = text.TrimEnd(':', ' ', '|', '-', '+');
                if (depth < level)
                {
                    level = depth + 1;
                    path = string.Join('/', path.Split('/').Reverse().Skip(1).Reverse());
                }
                if (line.TrimEnd().EndsWith(':'))
                {
                    var asIndex = text.IndexOf(" As ");
                    var asType = "?";
                    if (asIndex > -1)
                    {
                        asType = text.Substring(asIndex + 4).Trim('"');
                        text = text.Substring(0, asIndex).Trim('"');
                    }
                    var suffix = "/" + text;
                    if (!path.EndsWith(suffix))
                    {
                        path += suffix;
                        types[path] = asType;
                        data[path] = parentData = new();
                    }
                    level = depth + 1;
                }
                else if (text.Contains(':') && text.Contains('='))
                {
                    var valueIndex = text.IndexOf(':');
                    var valueText = text.Substring(valueIndex + 1);
                    text = text.Substring(0, valueIndex);
                    parentData[text] = valueText.Trim().TrimStart('=');
                }
                if (line.EndsWith("|-") || line.EndsWith("|+") || line.EndsWith("|"))
                {
                    parentProperty = text;
                    prev = "";
                    value = true;
                }
                else
                {
                    prev = text;
                }
            }
        }
        return new(types, data);
    }
}
