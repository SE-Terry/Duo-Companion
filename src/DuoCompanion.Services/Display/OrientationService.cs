using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace DuoCompanion.Services.Display;

public sealed class OrientationService : IOrientationService
{
    private readonly IDisplayService _display;
    private readonly ILogger<OrientationService> _logger;

    public ScreenLayout Current { get; private set; }
    public event EventHandler<ScreenLayout>? LayoutChanged;

    public OrientationService(IDisplayService display, ILogger<OrientationService> logger)
    {
        _display = display;
        _logger = logger;
        Refresh();
    }

    public void Refresh()
    {
        var displays = _display.GetAllDisplays();
        var layout = displays.Count switch
        {
            1 => displays[0].Width >= displays[0].Height
                    ? ScreenLayout.SingleLandscape
                    : ScreenLayout.SinglePortrait,
            _ => displays[0].Width >= displays[0].Height
                    ? ScreenLayout.DualLandscape
                    : ScreenLayout.DualPortrait
        };

        if (layout == Current) return;
        Current = layout;
        _logger.LogInformation("Screen layout changed to {Layout}", layout);
        LayoutChanged?.Invoke(this, layout);
    }
}
