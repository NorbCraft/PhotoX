using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using PhotoX.App.Services;

namespace PhotoX.App.Tests;

/// <summary>
/// prompt_07 §6: visual mockups of text, icon + text and icon-only action buttons, for the owner to choose from. They
/// are drawn with PhotoX's own styles but are not implemented in the app. Generated on request only:
/// <c>PHOTOX_MOCKUPS=1 dotnet test --filter ButtonMockups</c> writes the images to <c>docs/mockups/buttons/</c>.
/// </summary>
public class ButtonMockups
{
    // Proposed icons on the 24-unit grid of Services/Icons.cs (only for these mockups).
    private static readonly Geometry Close = Geometry.Parse("M6.5 6.5l11 11 M17.5 6.5l-11 11");
    private static readonly Geometry Save = Geometry.Parse("M5.5 4h10.5l3.5 3.5v11a1.5 1.5 0 0 1-1.5 1.5h-12A1.5 1.5 0 0 1 4.5 18.5v-13A1.5 1.5 0 0 1 5.5 4z M8 4v4.5h7V4 M7.5 20v-6h9v6");
    private static readonly Geometry SaveNext = Geometry.Parse("M4 12h12 M11.5 7.5 16 12l-4.5 4.5 M20 5.5v13");
    private static readonly Geometry Reset = Geometry.Parse("M5 12a7 7 0 1 0 2.1-5 M4.5 4.5v4h4");
    private static readonly Geometry Undo = Geometry.Parse("M9 6.5 4.5 11 9 15.5 M5 11h9.5a4.5 4.5 0 0 1 0 9H12");
    private static readonly Geometry Redo = Geometry.Parse("M15 6.5 19.5 11 15 15.5 M19 11H9.5a4.5 4.5 0 0 0 0 9H12");
    private static readonly Geometry Back = Icons.FolderBack;
    private static readonly Geometry Print = Icons.Printer;

    private enum Style
    {
        Text,
        IconText,
        Icons,
        Toolbar,
    }

    [AvaloniaFact]
    public void Write_the_button_mockups()
    {
        Assert.SkipWhen(Environment.GetEnvironmentVariable("PHOTOX_MOCKUPS") != "1", "Set PHOTOX_MOCKUPS=1 to write the mockups");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "PhotoX.slnx"))) root = root.Parent!;
        var dir = Path.Combine(root.FullName, "docs", "mockups", "buttons");
        Directory.CreateDirectory(dir);
        App.ApplyTheme("light");
        Render(Editor(Style.Text, "A · Text (as in 0.10)", "Text buttons; shortcuts in tooltips and the Enter key chip."), 1280, 660, Path.Combine(dir, "A-text.png"));
        Render(Editor(Style.IconText, "B · Icon and text", "Each action keeps its word and gains an icon; quickest to learn, a little wider."), 1280, 660, Path.Combine(dir, "B-icon-text.png"));
        Render(Editor(Style.Icons, "C · Compact icons", "Close, Print, Save, Reset, Undo and Redo as icons with tooltips; only Save & Next keeps its words."), 1280, 660, Path.Combine(dir, "C-icons.png"));
        Render(Editor(Style.Toolbar, "D · Actions in the toolbar", "Close, Print and Save move to the top right of the toolbar; the side panel keeps only Save & Next."), 1280, 660, Path.Combine(dir, "D-toolbar.png"));
        Render(OtherWorkspaces(), 1280, 350, Path.Combine(dir, "E-other-workspaces.png"));
        App.ApplyTheme("dark");
        Render(Editor(Style.IconText, "B · Icon and text (dark mode)", "The same proposal in dark mode."), 1280, 660, Path.Combine(dir, "B-icon-text-dark.png"));
        App.ApplyTheme("light");
    }

    private static void Render(Control content, int width, int height, string path)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using (var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame"))
        using (var image = UiHelpers.ToMagick(frame))
        {
            image.Write(path, ImageMagick.MagickFormat.Png);
        }

        window.Close();
    }

    // ------------------------------------------------------------------ building blocks with the app's styles

    private static IBrush Res(string key) =>
        Application.Current!.TryGetResource(key, Application.Current.ActualThemeVariant, out var v) && v is IBrush b ? b : Brushes.Magenta;

    private static Avalonia.Controls.Shapes.Path Icon(Geometry data, IBrush? stroke = null, double scale = 1) => new()
    {
        Data = data,
        Stroke = stroke ?? Res("TextBrush"),
        StrokeThickness = 1.8,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        Width = 24,
        Height = 24,
        Stretch = Stretch.None,
        VerticalAlignment = VerticalAlignment.Center,
        RenderTransform = scale == 1 ? null : new ScaleTransform(scale, scale),
    };

    private static Button IconButton(Geometry icon, string? tip = null)
    {
        var b = new Button { Content = Icon(icon), Focusable = false };
        b.Classes.Add("icon");
        if (tip is not null) ToolTip.SetTip(b, tip);
        return b;
    }

    private static Button TextButton(string text, Geometry? icon, bool primary = false, double height = 34, double fontSize = 14, string? kbd = null)
    {
        var fg = primary ? Brushes.White : Res("TextBrush");
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        if (icon is not null) row.Children.Add(Icon(icon, fg, primary ? 1.1 : 0.9));
        row.Children.Add(new TextBlock { Text = text, Foreground = fg, FontSize = fontSize, FontWeight = primary ? FontWeight.SemiBold : FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center });
        if (kbd is not null)
        {
            var k = new Border { Child = new TextBlock { Text = kbd, Foreground = fg }, Margin = new Thickness(4, 0, 0, 0) };
            k.Classes.Add("kbd");
            row.Children.Add(k);
        }

        var b = new Button { Content = row, MinHeight = height, HorizontalAlignment = HorizontalAlignment.Stretch, Focusable = false };
        if (primary) b.Classes.Add("primary");
        return b;
    }

    private static Button Chip(string text, bool selected = false)
    {
        var b = new Button { Content = text, Focusable = false };
        b.Classes.Add("chip");
        b.Classes.Add("compact");
        if (selected) b.Classes.Add("selected");
        return b;
    }

    private static Border Sep()
    {
        var s = new Border();
        s.Classes.Add("sep");
        return s;
    }

    private static Grid Row(params Control[] cells)
    {
        var g = new Grid();
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0) g.ColumnDefinitions.Add(new ColumnDefinition(8, GridUnitType.Pixel));
            g.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            Grid.SetColumn(cells[i], i * 2);
            g.Children.Add(cells[i]);
        }

        return g;
    }

    private static TextBlock Caption(string text, double size = 13, bool bold = false) =>
        new() { Text = text, FontSize = size, FontWeight = bold ? FontWeight.Bold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap };

    /// <summary>A stand-in for the photo: a soft landscape gradient on the canvas.</summary>
    private static Control Photo() => new Border
    {
        Background = Res("CanvasBrush"),
        Child = new Border
        {
            Margin = new Thickness(60, 36),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#7FB2E5"), 0),
                    new GradientStop(Color.Parse("#E9D7B0"), 0.55),
                    new GradientStop(Color.Parse("#5E8F5A"), 1),
                },
            },
        },
    };

    // ------------------------------------------------------------------ the Photos editor in each style

    private static Control Editor(Style style, string title, string note)
    {
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        toolbar.Children.Add(style is Style.Text ? IconButton(Back) : IconButton(Back));
        toolbar.Children.Add(Sep());
        foreach (var (name, sel) in new[] { ("10 × 15", true), ("15 × 20", false), ("9 × 13", false), ("13 × 18", false), ("Custom…", false) }) toolbar.Children.Add(Chip(name, sel));
        toolbar.Children.Add(Sep());
        toolbar.Children.Add(IconButton(Icons.RotateLeft));
        toolbar.Children.Add(IconButton(Icons.RotateRight));
        toolbar.Children.Add(IconButton(Icons.ResetCrop));
        toolbar.Children.Add(IconButton(Icons.Crop));

        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        bar.Children.Add(toolbar);
        if (style == Style.Toolbar)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            actions.Children.Add(TextButton("Close", Close));
            actions.Children.Add(TextButton("Print", Print));
            actions.Children.Add(TextButton("Save", Save));
            Grid.SetColumn(actions, 1);
            bar.Children.Add(actions);
        }

        var top = new Border { Background = Res("PanelBrush"), BorderBrush = Res("BorderBrush"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(10, 7), Child = bar };

        // The side panel: Undo / Redo / Reset, the adjustments (summarised), then the actions.
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        if (style is Style.Icons or Style.Toolbar or Style.IconText)
        {
            head.Children.Add(IconButton(Undo));
            var redo = IconButton(Redo);
            Grid.SetColumn(redo, 1);
            head.Children.Add(redo);
        }
        else
        {
            head.Children.Add(new TextBlock { Text = "↶   ↷", FontSize = 18, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0) });
        }

        Control reset = style switch
        {
            Style.Icons => IconButton(Reset, "Reset"),
            Style.Text => TextButton("Reset", null),
            _ => TextButton("Reset", Reset),
        };
        reset.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(reset, 3);
        head.Children.Add(reset);

        var adjustments = new Border
        {
            Background = Res("Panel2Brush"),
            CornerRadius = new CornerRadius(8),
            Margin = new Thickness(0, 10),
            Child = new TextBlock { Text = "Histogram & levels · Auto Adjust · sliders", Classes = { "faint" }, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };

        var bottom = new StackPanel { Spacing = 8 };
        switch (style)
        {
            case Style.Text:
                bottom.Children.Add(Row(TextButton("Close", null), TextButton("Print", null), TextButton("Save", null)));
                break;
            case Style.IconText:
                bottom.Children.Add(Row(TextButton("Close", Close), TextButton("Print", Print), TextButton("Save", Save)));
                break;
            case Style.Icons:
                var icons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
                icons.Children.Add(IconButton(Close, "Close (Esc)"));
                icons.Children.Add(IconButton(Print, "Print"));
                icons.Children.Add(IconButton(Save, "Save (Ctrl+S)"));
                bottom.Children.Add(icons);
                break;
        }

        bottom.Children.Add(TextButton("Save & Next", style is Style.Text ? null : SaveNext, primary: true, height: 56, fontSize: 17, kbd: "Enter"));

        var side = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(16, 10, 18, 16) };
        side.Children.Add(head);
        Grid.SetRow(adjustments, 1);
        side.Children.Add(adjustments);
        Grid.SetRow(bottom, 2);
        side.Children.Add(bottom);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,340") };
        body.Children.Add(Photo());
        var panel = new Border { Background = Res("PanelBrush"), BorderBrush = Res("BorderBrush"), BorderThickness = new Thickness(1, 0, 0, 0), Child = side };
        Grid.SetColumn(panel, 1);
        body.Children.Add(panel);

        var heading = new StackPanel { Margin = new Thickness(16, 12), Spacing = 2 };
        heading.Children.Add(Caption(title, 18, bold: true));
        heading.Children.Add(Caption(note));

        var page = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Background = Res("BgBrush") };
        page.Children.Add(heading);
        Grid.SetRow(top, 1);
        page.Children.Add(top);
        Grid.SetRow(body, 2);
        page.Children.Add(body);
        return page;
    }

    /// <summary>The ID sheet step and the Printing workspace in styles A, B and C.</summary>
    private static Control OtherWorkspaces()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("140,*,*,*"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"), Margin = new Thickness(16) };
        void Put(Control c, int row, int col)
        {
            c.Margin = new Thickness(8);
            Grid.SetRow(c, row);
            Grid.SetColumn(c, col);
            grid.Children.Add(c);
        }

        Put(Caption("E · Other workspaces", 18, bold: true), 0, 0);
        Grid.SetColumnSpan(grid.Children[^1], 4);
        Put(Caption("A · Text", 14, true), 1, 1);
        Put(Caption("B · Icon and text", 14, true), 1, 2);
        Put(Caption("C · Compact icons", 14, true), 1, 3);
        Put(Caption("ID Photos, sheet step", 14, true), 2, 0);
        Put(Caption("Printing", 14, true), 3, 0);

        Control Panel(Control content) => new Border { Background = Res("PanelBrush"), BorderBrush = Res("BorderBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14), Child = content };

        Control IdButtons(Style s)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions(s == Style.Icons ? "Auto,8,Auto,8,*" : "Auto,8,*,8,*") };
            Control back = s switch { Style.Icons => IconButton(Back, "Back"), Style.IconText => TextButton("Back", Back, height: 52), _ => TextButton("Back", null, height: 52) };
            Control save = s switch { Style.Icons => IconButton(Save, "Save"), Style.IconText => TextButton("Save", Save, height: 52), _ => TextButton("Save", null, height: 52) };
            var print = TextButton("Print", s == Style.Text ? null : Print, primary: true, height: 52, fontSize: 15);
            foreach (var (c, col) in new[] { (back, 0), (save, 2), ((Control)print, 4) })
            {
                c.VerticalAlignment = VerticalAlignment.Stretch;
                Grid.SetColumn(c, col);
                g.Children.Add(c);
            }

            return Panel(g);
        }

        Control PrintButtons(Style s)
        {
            var stack = new StackPanel { Spacing = 8 };
            stack.Children.Add(s == Style.Icons
                ? new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { IconButton(Back, "Back (Esc)"), IconButton(Close, "Clear") } }
                : Row(TextButton("Back", s == Style.Text ? null : Back), TextButton("Clear", s == Style.Text ? null : Close)));
            stack.Children.Add(TextButton("Print on 10 × 15 cm", s == Style.Text ? null : Print, primary: true, height: 56, fontSize: 17));
            return Panel(stack);
        }

        for (var i = 0; i < 3; i++)
        {
            var s = (Style)i;
            Put(IdButtons(s), 2, i + 1);
            Put(PrintButtons(s), 3, i + 1);
        }

        return new Border { Background = Res("BgBrush"), Child = grid };
    }
}
