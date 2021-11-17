declare var require

declare namespace monaco {
    export var KeyMod
    export var KeyCode
    export var Uri
    export var languages
    export namespace editor {
        export interface ICommandHandler {
            (...args: any[]): void;
        }
        export interface IDisposable {
            dispose(): void;
        }
        export interface IEvent<T> {
            (listener: (e: T) => any, thisArg?: any): IDisposable
        }
        export interface Command {
            id: string
            title: string
            tooltip?: string
            arguments?: any[]
        }
        export interface CodeLens {
            range: IRange
            id?: string
            command?: Command
        }
        export interface CodeLensList {
            lenses: CodeLens[]
            dispose(): void
        }
        export interface CodeLensProvider {
            onDidChange?: IEvent<this>
            provideCodeLenses(model: editor.ITextModel): Promise<CodeLensList> | CodeLensList
            resolveCodeLens?(model: editor.ITextModel, codeLens: CodeLens): Promise<CodeLens> | CodeLens
        }
        export interface IViewZone {
            heightInLines: number
            heightInPx?: number
            afterLineNumber: number
            domNode: HTMLElement
            marginDomNode: HTMLElement
            suppressMouseDown?: boolean
        }
        export interface IViewZoneChangeAccessor {
            layoutZone(zone: string)
            addZone(view: IViewZone)
            removeZone(arg0: string)
        }
        export interface ITextModel {
            getLinesContent()
        }
        export interface IModelDeltaDecoration {
            id: string
            range: IRange
            options: {
                hoverMessage?: { value: string },
                isWholeLine?: boolean
                className?: string
                linesDecorationsClassName?: string
                inlineClassName?: string
            }
        }
        export interface IStandaloneCodeEditor {
            onDidChangeModelContent(arg0: (e: any) => Promise<void>)
            getLineDecorations(lineNumber: number): IModelDeltaDecoration[] | null
            deltaDecorations(oldDecorations: string[], newDecorations: IModelDeltaDecoration[]): string[]
            addCommand(keybinding: number, handler: ICommandHandler, context?: string): string | null
        }
        export interface IStandaloneEditorConstructionOptions { }
        export interface IModelContentChange {
            range: any
        }
        export function createModel(language: string, text: string, uri: any)
        export function create(container: HTMLElement, options: IStandaloneEditorConstructionOptions)
    }
    export interface IRange {
        startLineNumber: number
        startColumn: number
        endLineNumber: number
        endColumn: number
    }
    export class Range implements IRange {
        public startLineNumber: number
        public startColumn: number
        public endLineNumber: number
        public endColumn: number
        constructor(
            startLine: number,
            startColumn: number,
            endLine: number,
            endColumn: number
        )
    }
}

interface CodeExecutionResult {
    code: string
    name: string
    text: string
    type: 'BlankType' | 'BooleanType' | 'NumberType' | 'StringType' | 'TimeType' | 'DateType' | 'DateTimeType' | 'DateTimeNoTimeZoneType' | 'OptionSetValueType' | 'Error'
    errors: {
        text: string
        description: string
        start: number
        end: number
    }[]
}

interface CodeSuggestionResult {
    text: string
    description: string
    startColumn?: number
    endColumn?: number
}

function createHandler<T, A = string>(text: string): (value: A) => T {
    return new Function("value", `return ${text}`) as (value: A) => T
}

var _updateMonacoEditor: (code: string) => void
export function updateMonacoEditor(code: string) {
    if (typeof (_updateMonacoEditor) === 'function') {
        _updateMonacoEditor(code)
    }
}

export function loadMonacoEditor(id: string, code: string, onChangeHandler: string, onHoverHandler: string, onSuggestHandler: string, onSaveHandler: string, onExecuteHandler: string, onPreviewHandler: string) {
    const onChange = createHandler<CodeExecutionResult>(onChangeHandler)
    const onHover = createHandler<CodeSuggestionResult>(onHoverHandler)
    const onSuggest = createHandler<CodeSuggestionResult[]>(onSuggestHandler)
    const onSave = createHandler<void>(onSaveHandler)
    const onExecute = createHandler<CodeExecutionResult>(onExecuteHandler)
    const onPreview = createHandler<void>(onPreviewHandler)
    const container = document.getElementById(id)
    const language = "PowerFx"
    const languageExt = "pfx"
    const theme = "vs-dark"
    const loaderScript = document.createElement("script")
    loaderScript.src = "https://www.typescriptlang.org/js/vs.loader.js"
    loaderScript.async = true
    loaderScript.onload = () => {
        require.config({
            paths: {
                vs: "https://typescript.azureedge.net/cdn/4.0.5/monaco/min/vs",
                sandbox: "https://www.typescriptlang.org/js/sandbox"
            },
            ignoreDuplicateModules: ["vs/editor/editor.main"]
        })
        require(["vs/editor/editor.main", "sandbox/index"], async (editorMain, sandboxFactory) => {
            monaco.languages.register({
                id: language,
                aliases: [languageExt],
                extensions: [languageExt]
            })

            const model = monaco.editor.createModel(code, language, monaco.Uri.parse(`file:///index.pfx`))
            model.setValue(code)

            addMonacoEditorSuggestions(monaco, language, (text, column) => {
                const items = []
                const suggested = onSuggest(text)
                for (let suggest of suggested) {
                    items.push({
                        startColumn: column,
                        text: suggest.text,
                        description: suggest.description
                    })
                }
                return items
            })

            addMonacoEditorHover(monaco, language, (text, column) => onHover(text))

            const editor = monaco.editor.create(container, {
                model,
                language,
                theme,
                ...defaultMonacoEditorOptions(),
                lineNumbers: (lineNumber) => {
                    const lines = model.getLinesContent()
                    var line = 0;
                    for (var i = 0; i < lines.length && i + 1 < lineNumber; i++) {
                        if (lines[i] && lines[i].trim()) {
                            line += 1;
                        }
                    }
                    const content = model.getLineContent(lineNumber)
                    if (content && content.trim()) {
                        return (line + 1).toString()
                    } else {
                        return ""
                    }
                }
            })

            _updateMonacoEditor = (code) => editor.setValue(code)

            monaco.languages.registerCodeLensProvider(language, new MonacoCodeLensProvider(editor, onChange, onExecute, onPreview));

            editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KEY_S, () => onSave(editor.getValue()))

            window.addEventListener("resize", () => editor.layout())
        })
    }
    document.body.appendChild(loaderScript)
}

function addMonacoEditorHover(monaco, language, onHover) {
    monaco.languages.registerHoverProvider(language, {
        provideHover: async (model, position) => {
            const line = model.getLineContent(position.lineNumber)
            const hover = await onHover(line, position.column)
            if (hover) {
                return {
                    contents: [{ value: `## ${hover.text}\n${hover.description}` }],
                    range: {
                        startLineNumber: position.lineNumber,
                        endLineNumber: position.lineNumber,
                        startColumn: hover.startColumn || 1,
                        endColumn: hover.endColumn || line.length
                    }
                }
            }
        }
    })
}

function addMonacoEditorSuggestions(monaco, language, onSuggest) {
    monaco.languages.registerCompletionItemProvider(language, {
        provideCompletionItems: async (model, position) => {
            const textUntilPosition = model.getValueInRange({
                startLineNumber: position.lineNumber,
                startColumn: 1,
                endLineNumber: position.lineNumber,
                endColumn: position.column
            })
            const lineSuggestions = await onSuggest(textUntilPosition, position.column)
            if (lineSuggestions && lineSuggestions.length > 0) {
                const word = model.getWordUntilPosition(position)
                const suggestions = []
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
                    })
                }
                return { suggestions }
            }
        }
    })
}

function defaultMonacoEditorOptions(): monaco.editor.IStandaloneEditorConstructionOptions {
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
    }
}

class MonacoCodeLensProvider implements monaco.editor.CodeLensProvider {
    private _decorations: string[]
    private _errorCommand: string
    private _previewCommand: string

    constructor(
        private editor: monaco.editor.IStandaloneCodeEditor,
        private check: (text: string) => CodeExecutionResult,
        private execute: (text: string) => CodeExecutionResult,
        preview: (output: string) => void
    ) {
        this._errorCommand = editor.addCommand(0, function (_,result: CodeExecutionResult) {
            preview("Error: " + result.text)
        }, '');
        this._previewCommand = editor.addCommand(1, function (_,result: CodeExecutionResult) {
            console.log('_previewCommand',arguments)
            preview(result.text?.trim())
        }, '');
    }
    onDidChange?: monaco.editor.IEvent<this>
    resolveCodeLens?(model: monaco.editor.ITextModel, codeLens: monaco.editor.CodeLens) {
        return codeLens
    }
    async provideCodeLenses(model: monaco.editor.ITextModel) {
        const lines = model.getLinesContent()
        const decorations: monaco.editor.IModelDeltaDecoration[] = []
        const lenses: monaco.editor.CodeLens[] = []
        for (let lineIndex = 0; lineIndex < lines.length; lineIndex++) {
            const lineNum = lineIndex + 1
            const expr = lines[lineIndex]
            if (expr) {
                //TODO: Only process lines that have changed... (memoization)
                const result = await this.check(expr)
                console.log("check", { ...result })
                if (result.errors) {
                    const errors: string[] = []
                    for (var err of result.errors) {
                        console.log("error", err.description)

                        decorations.push({
                            id: `error_${lineNum}_${err.start}_${err.end}`,
                            range: new monaco.Range(lineNum, err.start + 1, lineNum, err.end + 1),
                            options: {
                                inlineClassName: "powerfx-error",
                                hoverMessage: { value: err.description }
                            }
                        })
                        errors.push(err.description)
                    }
                    result.text = errors.join('\n')
                    lenses.push({
                        range: new monaco.Range(lineNum, err.start + 1, lineNum, err.end + 1),
                        id: `preview_${lineNum}_${err.start}_${err.end}`,
                        command: {
                            id: this._errorCommand,
                            title: "Error(s)",
                            tooltip: result.text,
                            arguments: [result]
                        }
                    })
                } else if (result.type !== "BlankType") {
                    const output = await this.execute(expr)
                    console.log("execute", { ...output })
                    decorations.push({
                        id: `type_${lineNum}`,
                        range: new monaco.Range(lineNum, 1, lineNum, 1),
                        options: {
                            inlineClassName: "powerfx-type",
                            hoverMessage: { value: result.type }
                        }
                    })
                    lenses.push({
                        range: new monaco.Range(lineNum, 1, lineNum, 1),
                        id: `preview_${lineNum}`,
                        command: {
                            id: this._previewCommand,
                            title: "View Result",
                            tooltip: result.text,
                            arguments: [output]
                        }
                    })
                }
            }
        }
        this._decorations = this.editor.deltaDecorations(this._decorations || [], decorations);
        return {
            lenses,
            dispose: () => { }
        }
    }
}