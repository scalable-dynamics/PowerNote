function createHandler(text) {
    return new Function("value", `return ${text}`);
}
var _updateMonacoEditor;
export function updateMonacoEditor(code) {
    if (typeof (_updateMonacoEditor) === 'function') {
        _updateMonacoEditor(code);
    }
}
export function loadMonacoEditor(id, code, onChangeHandler, onHoverHandler, onSuggestHandler, onSaveHandler, onExecuteHandler, onPreviewHandler) {
    const onChange = createHandler(onChangeHandler);
    const onHover = createHandler(onHoverHandler);
    const onSuggest = createHandler(onSuggestHandler);
    const onSave = createHandler(onSaveHandler);
    const onExecute = createHandler(onExecuteHandler);
    const onPreview = createHandler(onPreviewHandler);
    const container = document.getElementById(id);
    const language = "PowerFx";
    const languageExt = "pfx";
    const theme = "vs-dark";
    const loaderScript = document.createElement("script");
    loaderScript.src = "https://www.typescriptlang.org/js/vs.loader.js";
    loaderScript.async = true;
    loaderScript.onload = () => {
        require.config({
            paths: {
                vs: "https://typescript.azureedge.net/cdn/4.0.5/monaco/min/vs",
                sandbox: "https://www.typescriptlang.org/js/sandbox"
            },
            ignoreDuplicateModules: ["vs/editor/editor.main"]
        });
        require(["vs/editor/editor.main", "sandbox/index"], async (editorMain, sandboxFactory) => {
            monaco.languages.register({
                id: language,
                aliases: [languageExt],
                extensions: [languageExt]
            });
            const model = monaco.editor.createModel(code, language, monaco.Uri.parse(`file:///index.pfx`));
            model.setValue(code);
            addMonacoEditorSuggestions(monaco, language, (text, column) => {
                const items = [];
                const suggested = onSuggest(text);
                for (let suggest of suggested) {
                    items.push({
                        startColumn: column,
                        text: suggest.text,
                        description: suggest.description
                    });
                }
                return items;
            });
            addMonacoEditorHover(monaco, language, (text, column) => onHover(text));
            const editor = monaco.editor.create(container, {
                model,
                language,
                theme,
                ...defaultMonacoEditorOptions(),
                lineNumbers: (lineNumber) => {
                    const lines = model.getLinesContent();
                    var line = 0;
                    for (var i = 0; i < lines.length && i + 1 < lineNumber; i++) {
                        if (lines[i] && lines[i].trim()) {
                            line += 1;
                        }
                    }
                    const content = model.getLineContent(lineNumber);
                    if (line > 0 && content && content.trim()) {
                        return (line + 1).toString();
                    }
                    else {
                        return "";
                    }
                }
            });
            _updateMonacoEditor = (code) => editor.setValue(code);
            monaco.languages.registerCodeLensProvider(language, new MonacoCodeLensProvider(editor, onChange, onExecute, onPreview));
            editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KEY_S, () => onSave(editor.getValue()));
            window.addEventListener("resize", () => editor.layout());
        });
    };
    document.body.appendChild(loaderScript);
}
function addMonacoEditorHover(monaco, language, onHover) {
    monaco.languages.registerHoverProvider(language, {
        provideHover: async (model, position) => {
            const line = model.getLineContent(position.lineNumber);
            const hover = await onHover(line, position.column);
            if (hover) {
                return {
                    contents: [{ value: `## ${hover.text}\n${hover.description}` }],
                    range: {
                        startLineNumber: position.lineNumber,
                        endLineNumber: position.lineNumber,
                        startColumn: hover.startColumn || 1,
                        endColumn: hover.endColumn || line.length
                    }
                };
            }
        }
    });
}
function addMonacoEditorSuggestions(monaco, language, onSuggest) {
    monaco.languages.registerCompletionItemProvider(language, {
        provideCompletionItems: async (model, position) => {
            const textUntilPosition = model.getValueInRange({
                startLineNumber: position.lineNumber,
                startColumn: 1,
                endLineNumber: position.lineNumber,
                endColumn: position.column
            });
            const lineSuggestions = await onSuggest(textUntilPosition, position.column);
            if (lineSuggestions && lineSuggestions.length > 0) {
                const word = model.getWordUntilPosition(position);
                const suggestions = [];
                for (const suggestion of lineSuggestions) {
                    suggestions.push({
                        label: suggestion.text,
                        detail: suggestion.description,
                        kind: monaco.languages.CompletionItemKind.Value,
                        insertText: suggestion.text,
                        range: {
                            startLineNumber: position.lineNumber,
                            endLineNumber: position.lineNumber,
                            startColumn: suggestion.startColumn || word.startColumn,
                            endColumn: suggestion.endColumn || word.endColumn
                        }
                    });
                }
                return { suggestions };
            }
        }
    });
}
function defaultMonacoEditorOptions() {
    return {
        lineHeight: 30,
        fontSize: 22,
        renderLineHighlight: 'all',
        wordWrap: 'on',
        scrollBeyondLastLine: false,
        minimap: {
            enabled: false
        },
        renderValidationDecorations: 'off',
        lineDecorationsWidth: 0,
        glyphMargin: false,
        contextmenu: false,
        codeLens: true,
        mouseWheelZoom: true,
        quickSuggestions: false,
        suggest: {
            showIssues: false,
            shareSuggestSelections: false,
            showIcons: false,
            showMethods: false,
            showFunctions: false,
            showVariables: false,
            showKeywords: false,
            showWords: false,
            showClasses: false,
            showColors: false,
            showConstants: false,
            showConstructors: false,
            showEnumMembers: false,
            showEnums: false,
            showEvents: false,
            showFields: false,
            showFiles: false,
            showFolders: false,
            showInterfaces: false,
            showModules: false,
            showOperators: false,
            showProperties: false,
            showReferences: false,
            showSnippets: false,
            showStructs: false,
            showTypeParameters: false,
            showUnits: false,
            showValues: true,
            filterGraceful: false
        }
    };
}
class MonacoCodeLensProvider {
    constructor(editor, check, execute, preview) {
        this.editor = editor;
        this.check = check;
        this.execute = execute;
        this._errorCommand = editor.addCommand(0, function (result) {
            console.log("Error", result);
            preview(result);
        }, '');
        this._previewCommand = editor.addCommand(0, preview, '');
    }
    resolveCodeLens(model, codeLens) {
        return codeLens;
    }
    async provideCodeLenses(model) {
        const lines = model.getLinesContent();
        const decorations = [];
        const lenses = [];
        for (let lineIndex = 0; lineIndex < lines.length; lineIndex++) {
            const lineNum = lineIndex + 1;
            const expr = lines[lineIndex];
            if (expr) {
                const result = await this.check(expr);
                if (result.errors) {
                    const errors = [];
                    for (var err of result.errors) {
                        decorations.push({
                            id: `error_${lineNum}_${err.start}_${err.end}`,
                            range: new monaco.Range(lineNum, err.start + 1, lineNum, err.end + 1),
                            options: {
                                inlineClassName: "powerfx-error",
                                hoverMessage: { value: err.description }
                            }
                        });
                        errors.push(err.description);
                    }
                    result.text = errors.join('\n');
                    lenses.push({
                        range: new monaco.Range(lineNum, err.start + 1, lineNum, err.end + 1),
                        id: `preview_${lineNum}_${err.start}_${err.end}`,
                        command: {
                            id: this._errorCommand,
                            title: "Error(s)",
                            tooltip: result.text,
                            arguments: [result]
                        }
                    });
                }
                else {
                    const output = await this.execute(expr);
                    lenses.push({
                        range: new monaco.Range(lineNum, 1, lineNum, 1),
                        id: `preview_${lineNum}`,
                        command: {
                            id: this._previewCommand,
                            title: "Preview",
                            arguments: [output]
                        }
                    });
                }
            }
        }
        this._decorations = this.editor.deltaDecorations(this._decorations || [], decorations);
        return {
            lenses,
            dispose: () => { }
        };
    }
}
