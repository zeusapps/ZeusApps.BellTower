namespace BellTower.Core;

public enum EventKind
{
    Register,
    Prompt,
    ToolStart,
    ToolEnd,
    Permission,
    Question,
    Stop,
    Failure,
    Interrupt,
    BackgroundStart,
    BackgroundEnd,
    End
}
