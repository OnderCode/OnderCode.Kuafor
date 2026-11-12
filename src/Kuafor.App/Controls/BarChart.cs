using Kuafor.App.Theme;

namespace Kuafor.App.Controls;

/// <summary>Yuvarlatılmış sütun grafiği (Microsoft.Maui.Graphics ile çizilir). Vurgulanan sütun ana renkte, diğerleri yumuşak.</summary>
public sealed class BarChart : GraphicsView, IDrawable
{
    IReadOnlyList<double> _values = [];
    IReadOnlyList<string> _labels = [];
    int _highlight = -1;
    Func<double, string> _format = v => v.ToString("#,0");

    public BarChart()
    {
        Drawable = this;
        HeightRequest = 180;
        BackgroundColor = Colors.Transparent;
    }

    public void Set(IReadOnlyList<double> values, IReadOnlyList<string> labels, int highlight = -1, Func<double, string>? format = null)
    {
        _values = values; _labels = labels; _highlight = highlight;
        if (format is not null) _format = format;
        Invalidate();
    }

    public void Draw(ICanvas canvas, RectF r)
    {
        if (_values.Count == 0) return;
        var primary = Pal.Now(Pal.Primary);
        var soft = Pal.Soft(primary, Pal.IsDark ? 0.35f : 0.22f);
        var muted = Pal.Now(Pal.Muted);
        var grid = Pal.Now(Pal.Border);
        const float labelH = 22, topPad = 22;
        var max = Math.Max(1, _values.Max());
        var n = _values.Count;
        var slot = r.Width / n;
        var barW = Math.Min(28, slot * 0.62f);
        var chartH = r.Height - labelH - topPad;

        canvas.StrokeColor = grid;
        canvas.StrokeSize = 1;
        canvas.StrokeDashPattern = [3, 4];
        for (var i = 0; i <= 2; i++)
        {
            var y = topPad + chartH * i / 2f;
            canvas.DrawLine(r.Left, y, r.Right, y);
        }
        canvas.StrokeDashPattern = null;

        canvas.FontSize = 11;
        canvas.Font = new Microsoft.Maui.Graphics.Font("Inter");
        var labelEvery = n > 16 ? (int)Math.Ceiling(n / 8.0) : 1;
        for (var i = 0; i < n; i++)
        {
            var h = (float)(_values[i] / max * chartH);
            var x = r.Left + slot * i + (slot - barW) / 2;
            var y = topPad + chartH - h;
            var on = i == _highlight || (_highlight < 0 && _values[i] == max);
            canvas.FillColor = on ? primary : soft;
            if (h > 0.5f) canvas.FillRoundedRectangle(x, y, barW, Math.Max(h, 4), Math.Min(8, barW / 2), Math.Min(8, barW / 2), 2, 2);
            if (on && _values[i] > 0)
            {
                canvas.FontColor = primary;
                canvas.DrawString(_format(_values[i]), x - 30, y - 18, barW + 60, 16, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
            if (i % labelEvery == 0 && i < _labels.Count)
            {
                canvas.FontColor = on ? Pal.Now(Pal.Text) : muted;
                canvas.DrawString(_labels[i], r.Left + slot * i - 10, r.Bottom - labelH + 4, slot + 20, labelH - 4, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
    }
}
