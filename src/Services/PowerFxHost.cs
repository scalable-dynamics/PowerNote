using Microsoft.PowerFx;
using Microsoft.PowerFx.Core.Errors;
using Microsoft.PowerFx.Core.Public;
using Microsoft.PowerFx.Core.Public.Types;
using Microsoft.PowerFx.Core.Public.Values;
using PowerNote.Models;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace PowerNote.Services;

public record PowerFxError(int Begin, int End, ErrorKind Kind, DocumentErrorSeverity Severity, string Message, string Name, FormulaValue Value) : PowerFxResult(Name, Message, Value);

public record PowerFxResult(string Name, string FormattedText, FormulaValue Value);
public record PowerFxCheck(string Name, string Description, FormulaType Type, PowerFxSuggestion[] Errors);
public record PowerFxSuggestion(string Text, string Description, int Start, int End);

public class PowerFxHost
{
    private RecalcEngine _engine;
    private readonly FormulaJsHost _formulaJsHost;

    private readonly HashSet<string> _formulas = new();
    private readonly HashSet<string> _variables = new();

    public PowerFxHost(FormulaJsHost formulaJsHost)
    {
        _formulaJsHost = formulaJsHost;
        _engine = new();
        _formulaJsHost.AddFunctions(_engine);
    }

    public PowerFxSuggestion Show(string expr)
    {
        Console.WriteLine($"Show: {expr}");
        var functions = _engine.GetAllFunctionNames().ToArray();
        foreach (var function in functions)
        {
            var functionIndex = expr.IndexOf($"{function}(");
            if (functionIndex > -1 && PowerFxGrammar.FunctionDescriptions.ContainsKey(function))
            {
                return new(function, PowerFxGrammar.FunctionDescriptions[function], functionIndex, functionIndex + function.Length);
            }
        }
        return null;
    }

    public IEnumerable<PowerFxSuggestion> Suggest(string expr)
    {
        Console.WriteLine($"Suggest: {expr}");
        var result = _engine.Suggest(expr, null, expr.Length);
        foreach (var suggestion in result.Suggestions)
        {
            if (suggestion.Overloads.Any())
            {
                foreach(var overload in suggestion.Overloads)
                {
                    yield return new(overload.DisplayText.Text, overload.Definition, overload.DisplayText.HighlightStart, overload.DisplayText.HighlightEnd);
                }
            }
            else
            {
                yield return new(suggestion.DisplayText.Text, suggestion.Definition, suggestion.DisplayText.HighlightStart, suggestion.DisplayText.HighlightEnd);
            }
        }
        var functions = _engine.GetAllFunctionNames().ToArray();
        foreach (var function in functions)
        {
            if (string.IsNullOrEmpty(expr))
            {
                yield return new(function, PowerFxGrammar.FunctionDescriptions[function], 1, 1);
            }
            else
            {
                var functionIndex = expr.IndexOf($"{function}(");
                if (functionIndex > -1 && PowerFxGrammar.FunctionDescriptions.ContainsKey(function))
                {
                    yield return new(function, PowerFxGrammar.FunctionDescriptions[function], functionIndex, functionIndex + function.Length);
                }
            }
        }
    }

    public PowerFxCheck Check(string expr)
    {
        Debug.WriteLine($"Check: {expr}");
        try
        {
            var result = checkExpression(expr);
            if (result.check != null) {
                if (result.check.IsSuccess)
                {
                    if (result.isAssignment)
                    {
                        return new(result.name, "Assignment", result.check.ReturnType, null);
                    }
                    else if (result.isFormula)
                    {
                        if (!_formulas.Contains(result.name))
                        {
                            _engine.SetFormula(result.name, result.text,
                                (name, value) =>
                                {
                                    Console.WriteLine($"PowerFxCheck {name}: {value.ToObject()}");
                                }
                            );
                            _formulas.Add(result.name);
                        }
                        return new(result.name, "Formula", result.check.ReturnType, null);
                    }
                    else if (result.isExpression)
                    {
                        return new(result.name, "Expression", result.check.ReturnType, null);
                    }
                }
                else
                {
                    return new(result.check.ReturnType?.GetType().Name ?? "Error", "", result.check.ReturnType, result.check.Errors.Select(e => new PowerFxSuggestion(e.Kind.ToString(), e.Message, e.Span.Min, e.Span.Lim)).ToArray());
                }
            }
            return new(result.name, result.text, null, null);
        }
        catch (Exception ex)
        {
            return new("Error", "", null, new[] { new PowerFxSuggestion(ex.GetType().Name, ex.Message, 1, 1) });
        }
    }

    public (PowerFxResult Result, string Output) Execute(string expr)
    {
        Debug.WriteLine($"Execute: {expr}");
        var output = new StringBuilder();
        var addLine = (string line) => { output.AppendLine(line); return line; };
        try
        {
            var check = checkExpression(expr);
            if (check.check.IsSuccess)
            {
                if (check.isAssignment)
                {
                    if (!_variables.Contains(check.name))
                    {
                        _variables.Add(check.name);
                    }
                    var value = _engine.Eval(check.text);
                    _engine.UpdateVariable(check.name, value);
                    var result = createResult(check.name, value);
                    addLine($"{check.name} -> {result.FormattedText}");
                    return (result, output.ToString());
                }
                else if (check.isFormula)
                {
                    if (!_formulas.Contains(check.name))
                    {
                        _engine.SetFormula(check.name, check.text,
                            (name, value) => addLine(createResult(name, value).FormattedText)
                        );
                        _formulas.Add(check.name);
                    }
                    var value = _engine.GetValue(check.name);
                    var result = createResult(check.name, value);
                    addLine($"{check.name} = {result.FormattedText}");
                    return (result, output.ToString());
                }
                else if (check.isExpression)
                {
                    var value = _engine.Eval(check.text);
                    var result = createResult(check.name, value);
                    addLine(result.FormattedText);
                    return (result, output.ToString());
                }
                else if (!string.IsNullOrWhiteSpace(expr))
                {
                    return (createError("Not Recognized"), output.ToString());
                }
                else
                {
                    return (createError("Empty"), output.ToString());
                }
            }
            else
            {
                var errors = check.check.Errors.Select(e => new PowerFxSuggestion(e.Kind.ToString(), e.Message, e.Span.Min, e.Span.Lim)).ToArray();
                return (createError(string.Join("\n", errors.Select(e => e.ToString()))), check.text);
            }
        }
        catch (Exception ex)
        {
            return (createError(ex.ToString()), output.ToString());
        }
    }

    private (bool isAssignment, bool isFormula, bool isExpression, string name, string text, CheckResult check) checkExpression(string expr)
    {
        Match match;
        // variable assignment: Set( <ident>, <expr> )
        if ((match = Regex.Match(expr, @"^\s*Set\(\s*(?<ident>\w+)\s*,\s*(?<expr>.*)\)\s*$")).Success)
        {
            var name = match.Groups["ident"].Value;
            var text = match.Groups["expr"].Value;
            return (true, false, false, name, text, _engine.Check(text));
        }
        // formula definition: <ident> = <formula>
        else if ((match = Regex.Match(expr, @"^\s*(?<ident>\w+)\s*=(?<formula>.*)$")).Success)
        {
            var name = match.Groups["ident"].Value;
            var text = match.Groups["formula"].Value;
            return (false, true, false, name, text, _engine.Check(text));
        }
        // everything except single line comments
        else if (!Regex.IsMatch(expr, @"^\s*//") && Regex.IsMatch(expr, @"\w"))
        {
            return (false, false, true, "", expr, _engine.Check(expr));
        }
        else
        {
            return (false, false, false, "", expr, null);
        }
    }

    private PowerFxError createError(string name, ErrorValue errorValue)
    {
        var error = errorValue.Errors[0];
        return new(error.Span?.Min ?? 1, error.Span?.Lim ?? 1, error.Kind, error.Severity, error.Message, name, errorValue);
    }

    private PowerFxResult createError(string message)
    {
        return createResult("Error", FormulaValue.NewError(new ExpressionError
        {
            Kind = ErrorKind.Unknown,
            Severity = DocumentErrorSeverity.Warning,
            Message = message
        }));
    }

    private PowerFxResult createResult(string name, FormulaValue result)
    {
        return result switch
        {
            ErrorValue errorValue => createError(name, errorValue),
            _ => new PowerFxResult(name, PrintResult(result), result)
        };
    }

    private string PrintResult(object value)
    {
        string resultString = "";

        if (value is RecordValue record)
        {
            var separator = "";
            resultString = "{";
            foreach (var field in record.Fields)
            {
                resultString += separator + $"{field.Name}:";
                resultString += PrintResult(field.Value);
                separator = ", ";
            }
            resultString += "}";
        }
        else if (value is TableValue table)
        {
            int valueSeen = 0, recordsSeen = 0;
            string separator = "";

            // check if the table can be represented in simpler [ ] notation,
            //   where each element is a record with a field named Value.
            foreach (var row in table.Rows)
            {
                recordsSeen++;
                if (row.Value is RecordValue scanRecord)
                {
                    foreach (var field in scanRecord.Fields)
                        if (field.Name == "Value")
                        {
                            valueSeen++;
                            resultString += separator + PrintResult(field.Value);
                            separator = ", ";
                        }
                        else
                            valueSeen = 0;
                }
                else
                    valueSeen = 0;
            }

            if (valueSeen == recordsSeen)
                return ("[" + resultString + "]");
            else
            {
                // no, table is more complex that a single column of Value fields,
                //   requires full treatment
                resultString = "Table(";
                separator = "";
                foreach (var row in table.Rows)
                {
                    resultString += separator + PrintResult(row.Value);
                    separator = ", ";
                }
                resultString += ")";
            }
        }
        else if (value is ErrorValue errorValue)
            resultString = "<Error: " + errorValue.Errors[0].Message + ">";
        else if (value is StringValue str)
            resultString = "\"" + str.ToObject().ToString().Replace("\"", "\"\"") + "\"";
        else if (value is FormulaValue fv)
            resultString = fv.ToObject().ToString();
        else
            throw new Exception("unexpected type in PrintResult");

        return (resultString);
    }
}