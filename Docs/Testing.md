# Testing

## EditMode tests

When the project is already open in Unity Editor, run tests from the existing Editor:

`DeckBattle > Tests > Run EditMode Tests`

Do not start a second Unity process against the same project path. Unity rejects that with `Multiple Unity instances cannot open the same project`, and tools polling for test results can time out because no result XML is produced.

For command-line runs, close Unity Editor first and use:

```powershell
.\Tools\RunEditModeTests.ps1
```

The script exits early if it detects an open Unity Editor for this project. Use a separate clone or worktree if an editor session must stay open while CLI tests run.
