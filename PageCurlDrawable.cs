namespace OneRugbyNavi2;

/// <summary>Paints the moving fold and cast shadow of a turning schedule page.</summary>
internal sealed class PageCurlDrawable : IDrawable
{
    private readonly bool _turnForward;
    private readonly Microsoft.Maui.Graphics.IImage? _departingPage;
    private float _progress;

    public PageCurlDrawable(bool turnForward, Microsoft.Maui.Graphics.IImage? departingPage)
    {
        _turnForward = turnForward;
        _departingPage = departingPage;
    }

    public void SetProgress(double progress)
    {
        _progress = (float)Math.Clamp(progress, 0, 1);
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var width = dirtyRect.Width;
        var height = dirtyRect.Height;
        if (width <= 0 || height <= 0)
            return;

        if (_departingPage is not null)
        {
            // Keep the outgoing DIV as a sheet and progressively clip it away,
            // exposing the newly selected DIV below the curling edge.
            var reveal = new PathF();
            if (_turnForward)
            {
                var edge = width * (1 - _progress);
                reveal.MoveTo(0, 0);
                reveal.LineTo(edge, 0);
                reveal.QuadTo(edge - width * 0.025f, height * 0.5f, edge, height);
                reveal.LineTo(0, height);
            }
            else
            {
                var edge = width * _progress;
                reveal.MoveTo(width, 0);
                reveal.LineTo(edge, 0);
                reveal.QuadTo(edge + width * 0.025f, height * 0.5f, edge, height);
                reveal.LineTo(width, height);
            }
            reveal.Close();
            canvas.SaveState();
            canvas.ClipPath(reveal);
            canvas.DrawImage(_departingPage, 0, 0, width, height);
            canvas.RestoreState();
        }

        if (_progress <= 0 || _progress >= 1)
            return;

        // The curled page edge travels from the dragged side to the far side.
        var edgeX = _turnForward
            ? width * (1 - _progress)
            : width * _progress;
        var curlWidth = Math.Max(34, width * 0.16f);
        var direction = _turnForward ? 1 : -1;
        var outerX = Math.Clamp(edgeX + direction * curlWidth, 0, width);

        // A soft shadow follows the crease and darkens the page being exposed.
        var shadowWidth = Math.Max(10, curlWidth * 0.34f);
        var shadowLeft = Math.Clamp(edgeX - shadowWidth / 2, 0, width);
        var shadowRight = Math.Clamp(edgeX + shadowWidth / 2, 0, width);
        var shadow = new LinearGradientPaint(
            new[]
            {
                new PaintGradientStop(0, Color.FromRgba(12, 27, 45, 0.02f)),
                new PaintGradientStop(0.5f, Color.FromRgba(12, 27, 45, 0.2f)),
                new PaintGradientStop(1, Color.FromRgba(12, 27, 45, 0.02f))
            }, new Point(shadowLeft / width, 0), new Point(shadowRight / width, 0));
        canvas.SetFillPaint(shadow, dirtyRect);
        canvas.FillRectangle(shadowLeft, 0, Math.Max(1, shadowRight - shadowLeft), height);

        // The rolled strip is a curved sliver, brighter on its bent surface.
        var foldPath = new PathF();
        foldPath.MoveTo(edgeX, 0);
        foldPath.CurveTo(edgeX + direction * curlWidth * 0.25f, height * 0.3f,
            outerX - direction * curlWidth * 0.15f, height * 0.7f, outerX, height);
        foldPath.LineTo(edgeX, height);
        foldPath.Close();

        var foldLeft = Math.Clamp(Math.Min(edgeX, outerX), 0, width);
        var foldRight = Math.Clamp(Math.Max(edgeX, outerX), 0, width);
        if (foldRight > foldLeft)
        {
            var paper = new LinearGradientPaint(
                new[]
                {
                    new PaintGradientStop(0, Color.FromRgba(235, 241, 247, 0.12f)),
                    new PaintGradientStop(0.5f, Color.FromRgba(255, 255, 255, 0.94f)),
                    new PaintGradientStop(1, Color.FromRgba(203, 214, 225, 0.74f))
                }, new Point(foldLeft / width, 0), new Point(foldRight / width, 0));
            canvas.SetFillPaint(paper, dirtyRect);
            canvas.FillPath(foldPath);
            canvas.StrokeColor = Color.FromRgba(110, 130, 150, 0.32f);
            canvas.StrokeSize = 1;
            canvas.DrawPath(foldPath);
        }
    }
}
