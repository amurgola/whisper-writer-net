using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using WhisperWriter.Core.Interfaces;
using WhisperWriter.UI.ViewModels;

namespace WhisperWriter.UI.Views;

public partial class StatusWindow : Window
{
    private readonly IConfigurationService? _configService;
    private bool _isDragging;
    private Point _dragStartPoint;

    public StatusWindow()
    {
        InitializeComponent();
    }

    public StatusWindow(StatusViewModel viewModel, IConfigurationService? configService = null) : this()
    {
        DataContext = viewModel;
        _configService = configService;
        PositionWindow();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isDragging = true;
            _dragStartPoint = e.GetPosition(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_isDragging)
        {
            var currentPoint = e.GetPosition(this);
            var delta = currentPoint - _dragStartPoint;

            Position = new PixelPoint(
                Position.X + (int)delta.X,
                Position.Y + (int)delta.Y);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isDragging)
        {
            _isDragging = false;
            SavePosition();
        }
    }

    private void PositionWindow()
    {
        var config = _configService?.Configuration.Misc;

        // Use saved position if available
        if (config != null && config.StatusWindowX >= 0 && config.StatusWindowY >= 0)
        {
            Position = new PixelPoint(config.StatusWindowX, config.StatusWindowY);
            return;
        }

        // Default: Position in bottom-right corner of primary screen
        var screen = Screens.Primary;
        if (screen != null)
        {
            var workingArea = screen.WorkingArea;
            Position = new PixelPoint(
                workingArea.Right - (int)Width - 20,
                workingArea.Bottom - (int)Height - 20);
        }
    }

    private void SavePosition()
    {
        if (_configService == null) return;

        _configService.Configuration.Misc.StatusWindowX = Position.X;
        _configService.Configuration.Misc.StatusWindowY = Position.Y;

        // Save async without awaiting
        _ = _configService.SaveAsync();
    }
}

