namespace BellTower.Core;

public static class IdentityText
{
    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : Clean(value);
    public static string Clean(string value)
    {
        var clean = new string(value.Where(character => !char.IsControl(character) && character != '\u001b').Take(100).ToArray()).Trim();
        return string.IsNullOrEmpty(clean) ? "Session" : clean;
    }
}
