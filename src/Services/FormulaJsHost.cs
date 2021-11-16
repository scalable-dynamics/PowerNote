using Microsoft.JSInterop;
using Microsoft.PowerFx;
using Microsoft.PowerFx.Core.Public.Types;
using Microsoft.PowerFx.Core.Public.Values;

namespace PowerNote.Services;

public class FormulaJsHost
{
    private static string[] StringFunctions = new[] {
        "Find",
        "Match",
        "Proper",
        "Search",
        "Weekday"
    };

    private static string[] NumericFunctions = new[] {
        "Acos",
        "Acot",
        "Asin",
        "Atan",
        "Atan2",
        "Cos",
        "Cot",
        "Count",
        "CountA",
        "Degrees",
        "ISOWeekNum",
        "Pi",
        "Radians",
        "Rand",
        "Sin",
        "StdevP",
        "Tan",
        "VarP",
        "WeekNum"
    };

    private readonly IJSRuntime _jSRuntime;
    private IJSInProcessRuntime _jSInProcessRuntime;
    private IJSInProcessRuntime JSInProcessRuntime
    {
        get {
            if (_jSInProcessRuntime == null)
            {
                _jSInProcessRuntime = _jSRuntime as IJSInProcessRuntime;
                if (_jSInProcessRuntime == null)
                {
                    throw new InvalidOperationException("IJSInProcessRuntime not available");
                }
            }
            return _jSInProcessRuntime;
        }
    }

    public FormulaJsHost(IJSRuntime jSRuntime)
    {
        _jSRuntime = jSRuntime;
    }

    public void AddFunctions(RecalcEngine engine)
    {
        var functions = engine.GetAllFunctionNames().ToArray();
        foreach (var function in StringFunctions)
        {
            if (!functions.Contains(function))
            {
                engine.AddFunction(new FormulaJsFunction<string>(function, FormulaType.String, (args) =>
                    JSInProcessRuntime.Invoke<string>($"formulajs.{function.ToUpper()}", args)
                ));
            }
        }
        foreach (var function in NumericFunctions)
        {
            if (!functions.Contains(function))
            {
                engine.AddFunction(new FormulaJsFunction<double>(function, FormulaType.Number, (args) =>
                    JSInProcessRuntime.Invoke<double>($"formulajs.{function.ToUpper()}", args)
                ));
            }
        }
    }

    private class FormulaJsFunction<T> : ReflectionFunction
    {
        private readonly Func<object[], T> _func;

        public FormulaJsFunction(string name, FormulaType type, Func<object[], T> func) : base(name, type)
        {
            _func = func;
        }

        public FormulaValue Execute(FormulaValue[] args)
        {
            var value = _func(args.Select(a => a.ToObject()).ToArray());
            return FormulaValue.New(value, typeof(T));
        }
    }
}