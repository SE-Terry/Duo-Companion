using DuoCompanion.Contracts.Services;
using DuoCompanion.Core.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace DuoCompanion.App;

public sealed partial class SpanOverlayWindow : Window
{
    private readonly IWindowManagerService _windowManager;
    private readonly IntPtr _hwnd;
    private DispatcherQueueTimer? _animationTimer;
    private DateTimeOffset _animationStartedAt;
    private TimeSpan _animationDuration;
    private double _animationStartOpacity;
    private double _animationTargetOpacity;
    private double _currentOpacity;
    private bool _hideWhenAnimationCompletes;
    private bool _isVisible;

    public SpanOverlayWindow(IWindowManagerService windowManager)
    {
        _windowManager = windowManager;
        InitializeComponent();

        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        AppWindow.IsShownInSwitchers = false;

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
        }

        _hwnd = WindowNative.GetWindowHandle(this);
        _windowManager.MakeWindowClickThrough(_hwnd);
    }

    public void ShowAt(SpanTarget target, double opacity, int fadeDurationMilliseconds)
    {
        _windowManager.SetWindowBounds(_hwnd, target.Left, target.Top, target.Width, target.Height);
        var targetOpacity = Math.Clamp(opacity, 0, 1);

        if (!_isVisible)
        {
            _isVisible = true;
            _currentOpacity = 0;
            _windowManager.SetWindowOpacity(_hwnd, 0);
            AppWindow.Show();
        }

        StartAnimation(targetOpacity, fadeDurationMilliseconds, hideWhenComplete: false);
    }

    public void HideOverlay(int fadeDurationMilliseconds)
    {
        if (!_isVisible)
        {
            CancelAnimation();
            return;
        }

        StartAnimation(0, fadeDurationMilliseconds, hideWhenComplete: true);
    }

    public void ApplySettings(double opacity, int fadeDurationMilliseconds)
    {
        if (_isVisible)
            StartAnimation(Math.Clamp(opacity, 0, 1), fadeDurationMilliseconds, hideWhenComplete: false);
    }

    private void StartAnimation(double targetOpacity, int fadeDurationMilliseconds, bool hideWhenComplete)
    {
        CancelAnimation();
        _animationStartOpacity = _currentOpacity;
        _animationTargetOpacity = targetOpacity;
        _hideWhenAnimationCompletes = hideWhenComplete;
        _animationDuration = TimeSpan.FromMilliseconds(Math.Clamp(fadeDurationMilliseconds, 0, 1000));

        if (_animationDuration == TimeSpan.Zero)
        {
            ApplyAnimationProgress(1);
            return;
        }

        _animationStartedAt = DateTimeOffset.UtcNow;
        _animationTimer = DispatcherQueue.CreateTimer();
        _animationTimer.Interval = TimeSpan.FromMilliseconds(16);
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void OnAnimationTick(DispatcherQueueTimer sender, object args)
    {
        var elapsed = DateTimeOffset.UtcNow - _animationStartedAt;
        ApplyAnimationProgress(Math.Clamp(elapsed.TotalMilliseconds / _animationDuration.TotalMilliseconds, 0, 1));
    }

    private void ApplyAnimationProgress(double progress)
    {
        _currentOpacity = _animationStartOpacity + ((_animationTargetOpacity - _animationStartOpacity) * progress);
        _windowManager.SetWindowOpacity(_hwnd, _currentOpacity);

        if (progress < 1) return;

        var hideWhenComplete = _hideWhenAnimationCompletes;
        CancelAnimation();
        if (!hideWhenComplete) return;

        AppWindow.Hide();
        _isVisible = false;
    }

    private void CancelAnimation()
    {
        if (_animationTimer is null) return;

        _animationTimer.Stop();
        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer = null;
    }
}
