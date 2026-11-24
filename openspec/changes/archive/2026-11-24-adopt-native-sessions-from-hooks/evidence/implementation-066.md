# DEV-066 evidence (ANALYSIS-5400)

Program.UseUtf8Console sets output and input encodings independently, catching only IOException each; non-IO exceptions propagate. Setters stay outside the try. Tests: tests/BellTower.Tests/ProgramConsoleTests.cs (4). Release build 0 warnings; ProgramConsoleTests+CliTests 34 passed; dotnet format clean.

