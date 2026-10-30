using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PhotoX.App.Services;
using PhotoX.App.ViewModels;

namespace PhotoX.App.Views;

public sealed partial class PickerGrid : UserControl
{
    private PointerPressedEventArgs? _press;
    private PickerPhoto? _pressPhoto;
    private Point _pressPoint;

    public PickerGrid()
    {
        InitializeComponent();
        Grid.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || DataContext is not PhotoPickerViewModel vm) return;
            vm.Pick(null);
            e.Handled = true;
        };
        // Dragging a thumbnail onto a collage frame (prompt_06 §6); a click still selects as before.
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => _press = null, RoutingStrategies.Tunnel);
    }

    private void OnPhotoDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: PickerPhoto photo } && DataContext is PhotoPickerViewModel vm) vm.Pick(photo);
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        _press = null;
        if (DataContext is not PhotoPickerViewModel { CanDrag: true } || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if ((e.Source as Control)?.DataContext is not PickerPhoto { IsOpenable: true } photo) return;
        (_press, _pressPhoto, _pressPoint) = (e, photo, e.GetPosition(this));
    }

    private async void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_press is not { } press || _pressPhoto is not { } photo || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var d = e.GetPosition(this) - _pressPoint;
        if (Math.Abs(d.X) + Math.Abs(d.Y) < 8) return;
        _press = null;
        await DragDrop.DoDragDropAsync(press, PhotoDrag.Create(photo.Path), DragDropEffects.Copy);
    }
}
