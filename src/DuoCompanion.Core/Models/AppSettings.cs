namespace DuoCompanion.Core.Models;

public sealed class AppSettings
{
    public bool LaunchOnStartup { get; set; } = false;
    public string Theme { get; set; } = "System";          // "Light", "Dark", "System"
    public string DefaultModule { get; set; } = "Keyboard"; // matches nav tag
    public double KeyboardButtonSize { get; set; } = 56;
    public double WindowOpacity { get; set; } = 1.0;
}
