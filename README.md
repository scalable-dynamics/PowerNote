# PowerNote

![PowerNote](https://raw.githubusercontent.com/scalable-dynamics/PowerNote/main/.github/images/powernote.png)

Power Fx editor and file viewer

[Online Version (GitHub Pages)](https://scalable-dynamics.github.io/PowerNote/)

### Features
- Create Power Fx formulas and check/eval the result
- View and edit Power Fx formulas from various file types (.msapp, .json, .xlsx, .zip)
  * Canvas Apps (.msapp)
  * Cloud Flows (.json)
  * Excel Documents (.xlsx)
  * Dataverse Solutions (.zip)
- Save, share and collaborate by sending a PowerNote url
  * Press CTRL+S to save while in the editor, this will set the url so you can share with friends!
  * Selecting an app or file will change the url to that file
  * The url will contain a base64-encoded representation of the file or code - all data after the '#' in the url will not be sent to the server

### Powered by
* [Microsoft.AspNetCore.Components.WebAssembly](https://blazor.net)
* [Microsoft.Fast.Components.FluentUI](https://www.nuget.org/packages/Microsoft.Fast.Components.FluentUI)
* [Microsoft.PowerFx.Core](https://www.nuget.org/packages/Microsoft.PowerFx.Core)
* [Microsoft.PowerFx.Interpreter](https://www.nuget.org/packages/Microsoft.PowerFx.Interpreter)
* [Microsoft.PowerPlatform.Formulas.Tools](https://github.com/microsoft/PowerApps-Language-Tooling)
* [Microsoft Monaco Editor](https://microsoft.github.io/monaco-editor/index.html)
* [Formula JS](https://formulajs.info/)

## License

[MIT License](https://github.com/scalable-dynamics/PowerNote/blob/master/LICENSE)

*PowerNote* is licensed under the
[MIT](https://github.com/scalable-dynamics/PowerNote/blob/master/LICENSE) license
