# CI stand-ins for git-ignored files

`CalibrationSessionState.cs` and `CalibrationSessionStore.cs` are listed in
`.gitignore`, so they exist only on the development computer. GitHub's build
machine does not have them and cannot compile the project without them.

The build workflow copies these stand-ins into place **only on the build
machine, and only when the real file is missing**. They are stored as `.txt`
so they are never compiled into the application, and nothing here touches the
real files on the development or production computer.
