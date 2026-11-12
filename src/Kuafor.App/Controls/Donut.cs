using Kuafor.App.Theme;

namespace Kuafor.App.Controls;

/// <summary>Halka grafik: doluluk ya da ödeme dağılımı. Ortada büyük değer.</summary>
public sealed class Donut : GraphicsView, IDrawable
{
    (double Value, Color Color)[] _parts = [];
    string _center = "";
    string _caption = "";

    public Donut(double size = 120)
    {
        Drawable = this;
        WidthRequest = HeightRequest = size;
        BackgroundColor = Colors.Transparent;
    }

    public void Set(IEnumerable<(double Value, Color Color)> parts, string center, string caption = "")
    {
        _parts = [.. parts];
        _center = center; _caption = caption;
        Invalidate();
    }

    public void Draw(ICanvas canvas, RectF r)
    {
        var size = Math.Min(r.Width, r.Height);
        var stroke = size * 0.12f;
        var rect = new RectF(r.Center.X - size / 2 + stroke / 2, r.Center.Y - size / 2 + stroke / 2, size - stroke, size - stroke);
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeSize = stroke;
        canvas.StrokeColor = Pal.Now(Pal.SurfaceAlt);
        canvas.DrawEllipse(rect);
        var total = _parts.Sum(p => p.Value);
        if (total > 0)
        {
            float start = 90;
            foreach (var (v, c) in _parts)
            {
                var sweep = (float)(v / total * 360);
                if (sweep < 1) continue;
                canvas.StrokeColor = c;
                canvas.DrawArc(rect, start, start - sweep + (sweep > 8 ? 4 : 0), true, false);
                start -= sweep;
            }
        }
        canvas.FontColor = Pal.Now(Pal.Text);
        canvas.Font = new Microsoft.Maui.Graphics.Font("InterBold");
        canvas.FontSize = size * Math.Min(0.2f, 1.2f / Math.Max(1, _center.Length));
        canvas.DrawString(_center, r.Left, r.Center.Y - size * 0.16f, r.Width, size * 0.26f, HorizontalAlignment.Center, VerticalAlignment.Center);
        canvas.Font = new Microsoft.Maui.Graphics.Font("Inter");
        canvas.FontColor = Pal.Now(Pal.Muted);
        canvas.FontSize = size * 0.095f;
        canvas.DrawString(_caption, r.Left, r.Center.Y + size * 0.1f, r.Width, size * 0.14f, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
