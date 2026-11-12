using Kuafor.App.Theme;
using Microsoft.Maui.Controls.Shapes;

namespace Kuafor.App.Controls;

/// <summary>Onda tarzı bölmeli seçici: "Bugün | Hafta | Ay".</summary>
public sealed class Segmented : Border
{
    readonly Grid _grid = new() { ColumnSpacing = 4 };
    readonly List<Border> _items = [];
    int _selected;

    public event Action<int>? Changed;

    public Segmented(params string[] items)
    {
        StrokeThickness = 0;
        Padding = 4;
        StrokeShape = new RoundRectangle { CornerRadius = 14 };
        this.Bg(Pal.SurfaceAlt);
        for (var i = 0; i < items.Length; i++)
        {
            var index = i;
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var label = new Label { Text = items[i], FontSize = 13.5, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
            var b = new Border { Content = label, HeightRequest = 38, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 11 } };
            b.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => Select(index, true)) });
            _items.Add(b);
            _grid.Add(b, i);
        }
        Content = _grid;
        Paint();
    }

    public int SelectedIndex { get => _selected; set => Select(value, false); }

    void Select(int index, bool raise)
    {
        if (index == _selected && raise) return;
        _selected = index;
        Paint();
        if (raise) Changed?.Invoke(index);
    }

    void Paint()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            var on = i == _selected;
            var b = _items[i];
            var label = (Label)b.Content!;
            label.FontFamily = on ? F.SemiBold : F.Medium;
            label.Fg(on ? Pal.Text : Pal.Muted);
            if (on)
            {
                b.Bg(Pal.Surface);
                b.Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.06f, Radius = 6, Offset = new Point(0, 2) };
            }
            else
            {
                b.BackgroundColor = Colors.Transparent;
                b.ClearValue(VisualElement.BackgroundColorProperty);
                b.BackgroundColor = Colors.Transparent;
                b.Shadow = null!;
            }
        }
    }
}
