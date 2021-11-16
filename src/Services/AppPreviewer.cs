using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PowerNote.Services;

public record PowerAppPreview(string Name, string Component, string Html, string Code);
public class AppPreviewer
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    private static readonly string[] _mappedProperties = new string[]
    {
        "X",
        "Y",
        "Height",
        "Width",
        "ZIndex",
        "Size",
        "Text",
        "Fill",
        "Color",
        "BorderColor",
        "BorderThickness"
    };

    public PowerAppPreview GetAppComponentPreview(PowerApp app, PowerAppComponent component)
    {
        var code = new StringBuilder();
        code.AppendLine();

        var html = new StringBuilder();
        html.AppendLine("<div class=\"powerapp-preview\">");

        foreach (var obj in component.Objects)
        {
            if (!obj.Name.StartsWith("Screen"))
            {
                code.AppendLine(GetObjectTable(component, obj));
                html.AppendLine(GetObjectPreview(component, obj));
            }
        }

        html.AppendLine("</div>");

        var preview = html.ToString();
        var powerfx = code.ToString();
        Console.WriteLine("SetOutput: " + preview);
        Console.WriteLine("SetCode: " + powerfx);
        return new(app.Name, component.Name, preview, powerfx);
    }

    public string GetObjectTable(PowerAppComponent component, PowerAppComponentObject obj)
    {
        var code = new StringBuilder();
        code.AppendLine();

        var type = component.Types.TryGetValue(obj.Name, out var t) ? t : "Cell";
        var properties = obj.Data
                            .Where(p => p.Key != "OnSelect" && p.Key != "Text" && !_mappedProperties.Contains(p.Key))
                            .Select(p => $"{p.Key}: \"{p.Value}\"")
                            .ToArray();
        if (properties.Any())
        {
            code.AppendLine($"{getName(obj.Name)} = Table({{ Name:\"{obj.Name}\", Type:\"{type}\", {string.Join(", ", properties)} }})");
        }
        else
        {
            code.AppendLine($"{getName(obj.Name)} = Table({{ Name:\"{obj.Name}\", Type:\"{type}\" }})");
        }
        if (obj.Data.TryGetValue("OnSelect", out var onSelect))
        {
            var val = onSelect.TrimStart('=');
            code.AppendLine($"OnSelect = {val}");
        }
        else if (obj.Data.TryGetValue("Text", out var text))
        {
            var val = text.TrimStart('=');
            code.AppendLine($"Text = {val}");
            code.AppendLine();
        }

        return code.ToString();
    }

    private string getName(string value)
    {
        return value.TrimStart('/').Replace("/", "_");
    }

    public string GetObjectPreview(PowerAppComponent component, PowerAppComponentObject obj)
    {
        var html = new StringBuilder();
        var type = component.Types.TryGetValue(obj.Name, out var t) ? t : "Cell";
        html.Append("<div class=\"powerapp-component\" style=\"border:solid 1px pink;");
        if (obj.Data.ContainsKey("X") && obj.Data.ContainsKey("Y"))
        {
            html.Append($"position:absolute;");
        }
        foreach (var property in obj.Data)
        {
            //if (!property.Value.StartsWith('='))
            {
                var value = property.Value.Trim().TrimStart('=');
                html.Append(property.Key switch
                {
                    "X" => $"top:{getNumber(value)}px;",
                    "Y" => $"left:{getNumber(value)}px;",
                    "Height" => $"height:{getNumber(value)}px;",
                    "Width" => $"width:{getNumber(value)}px;",
                    "ZIndex" => $"z-index:{getNumber(value)};",
                    "Size" => $"font-size:{getNumber(value)}px;",
                    "Text" => $"content:'{getText(value)}';",
                    "Fill" => $"background-color:{getColor(value)};",
                    "Color" => $"color:{getColor(value)};",
                    "BorderColor" => $"border-color:{getColor(value)};",
                    "BorderThickness" => $"border-width:{getNumber(value)}px;",
                    _ => ""
                });
            }
        }
        html.AppendLine("\">");
        html.AppendLine($"<h4>{getName(obj.Name)} ({type})</h4>");

        if (obj.Data.TryGetValue("Text", out var text))
        {
            var val = text.TrimStart('=');
            html.AppendLine(val);
        }

        html.AppendLine("</div>");
        return html.ToString();
    }

    private string getColor(string value)
    {
        if (value.StartsWith("RGBA("))
        {
            return value.Replace("RGBA", "rgba");
        }
        else
        {
            return value;
        }
    }

    private string getNumber(string value)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(value, "\\D"))
        {
            return "00";
        }
        else
        {
            return value;
        }
    }

    private string getText(string value)
    {
        if (value.Contains('('))
        {
            return "<expr>";
        }
        else
        {
            return value.Replace("\"", "");
        }
    }

}
