using System.Numerics;
using DuoCompanion.App;
using DuoCompanion.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI.Input.Inking;

namespace DuoCompanion.App.Pages;

public sealed partial class HandwritingPage : Page
{
    private readonly IInputService _input;
    private readonly InkStrokeBuilder _strokeBuilder = new();
    private readonly InkStrokeContainer _strokeContainer = new();
    private readonly InkRecognizerContainer _recognizerContainer = new();
    private readonly List<Polyline> _strokes = new();
    private Polyline? _currentStroke;
    private bool _isDrawing;

    public HandwritingPage()
    {
        _input = App.Services.GetRequiredService<IInputService>();
        InitializeComponent();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDrawing = true;
        var pt = e.GetCurrentPoint(InkSurface).Position;
        _currentStroke = new Polyline
        {
            Stroke = new SolidColorBrush(Microsoft.UI.Colors.Black),
            StrokeThickness = 3,
            Points = { pt }
        };
        InkSurface.Children.Add(_currentStroke);
        InkSurface.CapturePointer(e.Pointer);
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDrawing || _currentStroke is null) return;
        _currentStroke.Points.Add(e.GetCurrentPoint(InkSurface).Position);
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDrawing || _currentStroke is null) return;
        _isDrawing = false;
        if (_currentStroke.Points.Count > 1)
            _strokes.Add(_currentStroke);
        _currentStroke = null;
    }

    private async void OnRecognize(object sender, RoutedEventArgs e)
    {
        if (_strokes.Count == 0)
        {
            RecognizedText.Text = "Draw something first";
            return;
        }

        var recognizers = _recognizerContainer.GetRecognizers();
        if (recognizers.Count == 0)
        {
            RecognizedText.Text = "No handwriting recognizer installed";
            return;
        }

        _strokeContainer.Clear();
        foreach (var polyline in _strokes)
        {
            var inkPoints = polyline.Points.Select(p => new InkPoint(p, 0.5f)).ToList();
            var stroke = _strokeBuilder.CreateStrokeFromInkPoints(inkPoints, Matrix3x2.Identity);
            _strokeContainer.AddStroke(stroke);
        }

        var results = await _recognizerContainer.RecognizeAsync(_strokeContainer, InkRecognitionTarget.All);
        var text = string.Join(" ", results.Select(r => r.GetTextCandidates().FirstOrDefault() ?? ""));

        if (!string.IsNullOrWhiteSpace(text))
        {
            RecognizedText.Text = $"Recognized: {text}";
            _input.SendText(text);
        }
        else
        {
            RecognizedText.Text = "Could not recognize — try writing more clearly";
        }
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        InkSurface.Children.Clear();
        _strokes.Clear();
        _strokeContainer.Clear();
        RecognizedText.Text = string.Empty;
    }
}
