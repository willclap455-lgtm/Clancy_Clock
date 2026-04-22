using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ClancyClock;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new ClockForm());
    }
}

internal sealed class ClockForm : Form
{
    private readonly System.Windows.Forms.Timer _clockTimer;
    private readonly System.Windows.Forms.Timer _animationTimer;
    private readonly ImageRepository _repository;
    private readonly TransitionDirector _transitionDirector;

    private Bitmap? _currentBitmap;
    private Bitmap? _nextBitmap;
    private string? _currentDisplayName;
    private string? _nextDisplayName;
    private DateTime _currentMinute;
    private DateTime _lastRenderedMinute;
    private string? _statusMessage;

    public ClockForm()
    {
        DoubleBuffered = true;
        Text = "Clancy Clock";
        WindowState = FormWindowState.Maximized;
        BackColor = Color.Black;
        KeyPreview = true;

        _repository = new ImageRepository(AppContext.BaseDirectory);
        _transitionDirector = new TransitionDirector();

        _clockTimer = new System.Windows.Forms.Timer { Interval = 1_000 };
        _clockTimer.Tick += (_, _) => CheckForMinuteChange();

        _animationTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _animationTimer.Tick += (_, _) =>
        {
            if (_transitionDirector.Advance())
            {
                Invalidate();
            }
            else
            {
                _animationTimer.Stop();
                DisposeBitmap(ref _currentBitmap);
                _currentBitmap = _nextBitmap;
                _currentDisplayName = _nextDisplayName ?? _currentDisplayName;
                _nextBitmap = null;
                _nextDisplayName = null;
                Invalidate();
            }
        };

        Load += (_, _) => InitializeClock();
        Resize += (_, _) => Invalidate();
        KeyDown += OnKeyDown;
    }

    private void InitializeClock()
    {
        _repository.Refresh();
        _currentMinute = DateTime.Now.TrimToMinute();
        _lastRenderedMinute = _currentMinute;
        LoadCurrentImage(_repository.ResolveFor(_currentMinute));
        _statusMessage = _currentBitmap is null
            ? "No matching image found. Add images next to the executable or in Images."
            : null;

        _clockTimer.Start();
        Invalidate();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
        {
            _repository.Refresh();
            _currentMinute = DateTime.Now.TrimToMinute();
            _lastRenderedMinute = _currentMinute;
            LoadCurrentImage(_repository.ResolveFor(_currentMinute));
            DisposeBitmap(ref _nextBitmap);
            _nextDisplayName = null;
            _statusMessage = _currentBitmap is null
                ? "No matching image found after refresh."
                : null;
            _transitionDirector.Stop();
            _animationTimer.Stop();
            Invalidate();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    private void CheckForMinuteChange()
    {
        var nowMinute = DateTime.Now.TrimToMinute();
        if (nowMinute == _lastRenderedMinute)
        {
            return;
        }

        _repository.Refresh();
        _lastRenderedMinute = nowMinute;
        _currentMinute = nowMinute;

        var resolved = _repository.ResolveFor(nowMinute);
        if (resolved is null)
        {
            _statusMessage = $"No image found for {nowMinute:HH:mm}.";
            DisposeBitmap(ref _currentBitmap);
            DisposeBitmap(ref _nextBitmap);
            _currentDisplayName = null;
            _nextDisplayName = null;
            _transitionDirector.Stop();
            _animationTimer.Stop();
            Invalidate();
            return;
        }

        _statusMessage = null;

        if (_currentBitmap is null)
        {
            LoadCurrentImage(resolved);
            Invalidate();
            return;
        }

        LoadNextImage(resolved);
        if (_nextBitmap is null)
        {
            _statusMessage = $"Unable to load image for {nowMinute:HH:mm}.";
            Invalidate();
            return;
        }

        _transitionDirector.Start(nowMinute, _currentDisplayName ?? "current", _nextDisplayName ?? "next");
        _animationTimer.Start();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var bounds = ClientRectangle;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var background = new LinearGradientBrush(
            bounds,
            Color.FromArgb(10, 14, 28),
            Color.FromArgb(2, 2, 8),
            LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(background, bounds);

        if (_currentBitmap is not null)
        {
            DrawImageCover(e.Graphics, _currentBitmap, bounds, 1f);
        }

        if (_transitionDirector.IsRunning && _currentBitmap is not null && _nextBitmap is not null)
        {
            _transitionDirector.Render(e.Graphics, bounds, _currentBitmap, _nextBitmap);
        }

        DrawOverlay(e.Graphics, bounds);
    }

    private void DrawOverlay(Graphics graphics, Rectangle bounds)
    {
        var timeText = _currentMinute == default ? DateTime.Now.ToString("HH:mm") : _currentMinute.ToString("HH:mm");
        var titleText = "Clancy Clock";
        var infoText = _statusMessage ?? (_currentDisplayName ?? "Awaiting image");

        using var titleFont = new Font("Segoe UI", 15, FontStyle.Bold);
        using var timeFont = new Font("Segoe UI", 54, FontStyle.Bold);
        using var infoFont = new Font("Segoe UI", 11, FontStyle.Regular);

        var width = Math.Max(220, Math.Min(520, bounds.Width - 48));
        var panelRect = new RectangleF(24, 24, width, 140);
        using var panelBrush = new SolidBrush(Color.FromArgb(120, 8, 10, 18));
        using var borderPen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f);
        using var whiteBrush = new SolidBrush(Color.FromArgb(245, 250, 255));
        using var subtleBrush = new SolidBrush(Color.FromArgb(190, 222, 230, 244));

        using var panelPath = RoundedRect(panelRect, 16f);
        graphics.FillPath(panelBrush, panelPath);
        graphics.DrawPath(borderPen, panelPath);

        graphics.DrawString(titleText, titleFont, subtleBrush, panelRect.X + 20, panelRect.Y + 14);
        graphics.DrawString(timeText, timeFont, whiteBrush, panelRect.X + 16, panelRect.Y + 34);
        graphics.DrawString(infoText, infoFont, subtleBrush, panelRect.X + 20, panelRect.Bottom - 30);
    }

    internal static void DrawImageCover(Graphics graphics, Image image, Rectangle bounds, float opacity)
    {
        var sourceRect = CalculateCoverSource(image.Size, bounds.Size);
        using var attributes = new ImageAttributes();
        var matrix = new ColorMatrix
        {
            Matrix33 = Math.Clamp(opacity, 0f, 1f)
        };
        attributes.SetColorMatrix(matrix);
        graphics.DrawImage(
            image,
            bounds,
            sourceRect.X,
            sourceRect.Y,
            sourceRect.Width,
            sourceRect.Height,
            GraphicsUnit.Pixel,
            attributes);
    }

    private static Rectangle CalculateCoverSource(Size imageSize, Size targetSize)
    {
        var imageRatio = imageSize.Width / (float)Math.Max(imageSize.Height, 1);
        var targetRatio = targetSize.Width / (float)Math.Max(targetSize.Height, 1);

        if (imageRatio > targetRatio)
        {
            var width = (int)(imageSize.Height * targetRatio);
            var x = (imageSize.Width - width) / 2;
            return new Rectangle(x, 0, width, imageSize.Height);
        }

        var height = (int)(imageSize.Width / Math.Max(targetRatio, 0.001f));
        var y = (imageSize.Height - height) / 2;
        return new Rectangle(0, y, imageSize.Width, height);
    }

    private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void LoadCurrentImage(MinuteImage? resolved)
    {
        DisposeBitmap(ref _currentBitmap);
        _currentDisplayName = null;

        if (resolved is null)
        {
            return;
        }

        _currentBitmap = resolved.LoadBitmap();
        _currentDisplayName = resolved.DisplayName;
    }

    private void LoadNextImage(MinuteImage resolved)
    {
        DisposeBitmap(ref _nextBitmap);
        _nextDisplayName = resolved.DisplayName;
        _nextBitmap = resolved.LoadBitmap();
    }

    private static void DisposeBitmap(ref Bitmap? bitmap)
    {
        bitmap?.Dispose();
        bitmap = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeBitmap(ref _currentBitmap);
            DisposeBitmap(ref _nextBitmap);
            _clockTimer.Dispose();
            _animationTimer.Dispose();
        }

        base.Dispose(disposing);
    }
}

internal sealed class ImageRepository
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"];
    private readonly string _baseDirectory;
    private readonly Dictionary<string, MinuteImage> _hourMinuteImages = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MinuteImage> _minuteFallbackImages = new(StringComparer.OrdinalIgnoreCase);

    public ImageRepository(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
    }

    public void Refresh()
    {
        DisposeImages();
        _hourMinuteImages.Clear();
        _minuteFallbackImages.Clear();

        foreach (var directory in EnumerateImageRoots().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var extension = Path.GetExtension(file);
                if (!SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileName = Path.GetFileNameWithoutExtension(file);
                var image = new MinuteImage(file, Path.GetFileName(file));

                foreach (var key in BuildHourMinuteKeys(fileName))
                {
                    if (_hourMinuteImages.ContainsKey(key))
                    {
                        continue;
                    }

                    _hourMinuteImages[key] = image;
                }

                foreach (var key in BuildMinuteFallbackKeys(fileName))
                {
                    if (_minuteFallbackImages.ContainsKey(key))
                    {
                        continue;
                    }

                    _minuteFallbackImages[key] = image;
                }
            }
        }
    }

    public MinuteImage? ResolveFor(DateTime moment)
    {
        var hourMinuteCandidates = new[]
        {
            $"{moment:hhmm}",
            $"{moment:hh_mm}",
            $"{moment:hh-mm}",
            $"{moment:hh}:{moment:mm}",
            $"{moment:hh}.{moment:mm}",
            $"{moment:hh}h{moment:mm}",
            $"{moment:HHmm}",
            $"{moment:HH_mm}",
            $"{moment:HH-mm}",
            $"{moment:HH}:{moment:mm}",
            $"{moment:HH}.{moment:mm}",
            $"{moment:HH}h{moment:mm}"
        };

        foreach (var candidate in hourMinuteCandidates)
        {
            if (_hourMinuteImages.TryGetValue(Normalize(candidate), out var image))
            {
                return image;
            }
        }

        var minuteCandidates = new[]
        {
            $"{moment:mm}",
            $"{moment.Minute}",
            $"minute-{moment:mm}",
            $"minute-{moment.Minute}"
        };

        foreach (var candidate in minuteCandidates)
        {
            if (_minuteFallbackImages.TryGetValue(Normalize(candidate), out var image))
            {
                return image;
            }
        }

        return null;
    }

    private IEnumerable<string> EnumerateImageRoots()
    {
        yield return _baseDirectory;
        yield return Path.Combine(_baseDirectory, "Images");

        var parent = Directory.GetParent(_baseDirectory)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent))
        {
            yield return parent;
            yield return Path.Combine(parent, "Images");
        }
    }

    private static IEnumerable<string> BuildHourMinuteKeys(string fileNameWithoutExtension)
    {
        var normalized = Normalize(fileNameWithoutExtension);
        if (!TryGetHourMinuteKey(normalized, out var digits))
        {
            yield break;
        }

        yield return normalized;
        yield return digits;
    }

    private static IEnumerable<string> BuildMinuteFallbackKeys(string fileNameWithoutExtension)
    {
        var normalized = Normalize(fileNameWithoutExtension);
        if (!TryGetMinuteKey(normalized, out var minute))
        {
            yield break;
        }

        yield return minute;
    }

    private static bool TryGetHourMinuteKey(string normalized, out string digits)
    {
        digits = string.Empty;

        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith("minute", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var numeric = new string(normalized.Where(char.IsDigit).ToArray());
        if (numeric.Length is not 3 and not 4)
        {
            return false;
        }

        var padded = numeric.PadLeft(4, '0');
        if (!int.TryParse(padded[..2], out var hour) || !int.TryParse(padded[2..], out var minute))
        {
            return false;
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return false;
        }

        digits = padded;
        return true;
    }

    private static bool TryGetMinuteKey(string normalized, out string minuteKey)
    {
        minuteKey = string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var digits = new string(normalized.Where(char.IsDigit).ToArray());
        if (normalized.StartsWith("minute", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseMinute(digits, out minuteKey))
            {
                return false;
            }

            return true;
        }

        if (digits.Length is 0 or > 2 || digits.Length != normalized.Length)
        {
            return false;
        }

        return TryParseMinute(digits, out minuteKey);
    }

    private static bool TryParseMinute(string digits, out string minuteKey)
    {
        minuteKey = string.Empty;
        if (!int.TryParse(digits, out var minute) || minute is < 0 or > 59)
        {
            return false;
        }

        minuteKey = minute.ToString("00");
        return true;
    }

    private static string Normalize(string value)
    {
        return value.Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(".", string.Empty)
            .Replace(":", string.Empty)
            .Replace(" ", string.Empty)
            .ToLowerInvariant();
    }

    private void DisposeImages()
    {
    }
}

internal sealed record MinuteImage(string Path, string DisplayName)
{
    public Bitmap LoadBitmap()
    {
        using var source = Image.FromFile(Path);
        return new Bitmap(source);
    }
}

internal sealed class TransitionDirector
{
    private readonly List<Shard> _shards = [];
    private readonly List<Ribbon> _ribbons = [];
    private readonly List<Orbital> _orbitals = [];
    private readonly List<Bloom> _blooms = [];
    private readonly List<MeshNode> _meshNodes = [];
    private readonly Stopwatch _stopwatch = new();

    private float _durationSeconds = 3.6f;
    private float _progress;
    private int _seed;

    public bool IsRunning { get; private set; }

    public void Start(DateTime minute, string currentPath, string nextPath)
    {
        _seed = HashCode.Combine(minute.Hour, minute.Minute, currentPath, nextPath);
        _durationSeconds = 2.8f + ((_seed & 0x7) * 0.24f);
        BuildScene();
        _progress = 0f;
        IsRunning = true;
        _stopwatch.Restart();
    }

    public bool Advance()
    {
        if (!IsRunning)
        {
            return false;
        }

        _progress = (float)(_stopwatch.Elapsed.TotalSeconds / _durationSeconds);
        if (_progress < 1f)
        {
            return true;
        }

        Stop();
        return false;
    }

    public void Stop()
    {
        _progress = 1f;
        IsRunning = false;
        _stopwatch.Reset();
    }

    public void Render(Graphics graphics, Rectangle bounds, Image current, Image next)
    {
        var progress = Math.Clamp(_progress, 0f, 1f);
        var reveal = EaseInOutCubic(progress);
        var burst = EaseOutExpo(MathF.Min(1f, progress * 1.18f));
        var settle = EaseInOutSine(MathF.Max(0f, (progress - 0.2f) / 0.8f));

        DrawShards(graphics, bounds, current, next, reveal, burst);
        DrawRibbons(graphics, bounds, reveal, settle);
        DrawMesh(graphics, bounds, reveal, settle);
        DrawOrbitals(graphics, bounds, reveal);

        var nextOpacity = Math.Clamp((progress - 0.1f) / 0.9f, 0f, 1f);
        ClockForm.DrawImageCover(graphics, next, bounds, nextOpacity);

        DrawBlooms(graphics, bounds, reveal);
        DrawAureole(graphics, bounds, reveal);
        DrawHighlights(graphics, bounds, progress);
    }

    private void BuildScene()
    {
        _shards.Clear();
        _ribbons.Clear();
        _orbitals.Clear();
        _blooms.Clear();
        _meshNodes.Clear();

        var seeded = new Random(_seed);

        var shardCount = 24 + seeded.Next(24);
        for (var i = 0; i < shardCount; i++)
        {
            _shards.Add(new Shard(
                seeded.NextSingle(),
                seeded.NextSingle(),
                seeded.NextSingle(),
                0.08f + seeded.NextSingle() * 0.2f,
                seeded.NextSingle() * 360f,
                seeded.NextSingle(),
                seeded.Next(3)));
        }

        var ribbonCount = 4 + seeded.Next(4);
        for (var i = 0; i < ribbonCount; i++)
        {
            _ribbons.Add(new Ribbon(
                seeded.NextSingle(),
                seeded.NextSingle(),
                0.1f + seeded.NextSingle() * 0.18f,
                0.5f + seeded.NextSingle() * 1.8f,
                seeded.NextSingle() * 360f,
                seeded.Next(0, 2) == 0));
        }

        var orbitalCount = 32 + seeded.Next(18);
        for (var i = 0; i < orbitalCount; i++)
        {
            _orbitals.Add(new Orbital(
                seeded.NextSingle() * MathF.Tau,
                0.1f + seeded.NextSingle() * 0.78f,
                0.006f + seeded.NextSingle() * 0.016f,
                0.6f + seeded.NextSingle() * 1.7f,
                seeded.NextSingle()));
        }

        var bloomCount = 5 + seeded.Next(5);
        for (var i = 0; i < bloomCount; i++)
        {
            _blooms.Add(new Bloom(
                seeded.NextSingle(),
                seeded.NextSingle(),
                0.1f + seeded.NextSingle() * 0.25f,
                seeded.NextSingle() * MathF.Tau,
                0.6f + seeded.NextSingle() * 1.2f));
        }

        var nodeCount = 10 + seeded.Next(7);
        for (var i = 0; i < nodeCount; i++)
        {
            _meshNodes.Add(new MeshNode(seeded.NextSingle(), seeded.NextSingle(), 0.16f + seeded.NextSingle() * 0.3f));
        }
    }

    private void DrawShards(Graphics graphics, Rectangle bounds, Image current, Image next, float reveal, float burst)
    {
        foreach (var shard in _shards)
        {
            var local = Math.Clamp((reveal - shard.Delay) / Math.Max(shard.Duration, 0.001f), 0f, 1f);
            if (local <= 0f)
            {
                continue;
            }

            var t = EaseInOutCubic(local);
            var center = new PointF(bounds.Width * shard.X, bounds.Height * shard.Y);
            var maxSize = Math.Min(bounds.Width, bounds.Height) * (0.08f + shard.Energy * 0.16f);
            var size = maxSize * (0.4f + t * 1.2f);
            var angle = shard.Angle + t * (shard.Mode == 0 ? 90f : shard.Mode == 1 ? -130f : 220f);

            using var path = BuildShard(center, size, angle, shard.Mode);
            using var region = new Region(path);
            var state = graphics.Save();
            graphics.SetClip(region, CombineMode.Replace);

            var offsetX = (0.5f - shard.X) * bounds.Width * 0.08f * (1f - t);
            var offsetY = (0.5f - shard.Y) * bounds.Height * 0.08f * (1f - t);
            graphics.TranslateTransform(offsetX, offsetY);

            var opacity = Math.Clamp(0.14f + burst * 1.1f - local * 0.15f, 0f, 1f);
            var source = local < 0.52f ? current : next;
            ClockForm.DrawImageCover(graphics, source, bounds, opacity);
            graphics.Restore(state);

            using var outlinePen = new Pen(Color.FromArgb((int)(120 * (1f - local)), 255, 255, 255), 1.2f);
            graphics.DrawPath(outlinePen, path);
        }
    }

    private void DrawRibbons(Graphics graphics, Rectangle bounds, float reveal, float settle)
    {
        foreach (var ribbon in _ribbons)
        {
            var progress = Math.Clamp((reveal - ribbon.Start) / Math.Max(ribbon.Width, 0.01f), 0f, 1f);
            if (progress <= 0f)
            {
                continue;
            }

            var alpha = (int)(120 * (1f - progress) + 45 * settle);
            using var path = new GraphicsPath();
            var points = new List<PointF>();
            const int steps = 28;

            for (var i = 0; i <= steps; i++)
            {
                var x = i / (float)steps;
                var xPixel = x * bounds.Width;
                var wave = MathF.Sin((x * ribbon.Frequency + progress * 1.4f) * MathF.Tau + ribbon.Phase) * bounds.Height * 0.08f;
                var drift = MathF.Cos((x * ribbon.Frequency * 0.5f + progress) * MathF.Tau + ribbon.Phase) * bounds.Height * 0.04f;
                var yBase = ribbon.Vertical
                    ? bounds.Height * x
                    : bounds.Height * ribbon.Anchor;
                var yPixel = ribbon.Vertical
                    ? bounds.Height * ribbon.Anchor + wave + drift
                    : yBase + wave + drift;
                var point = ribbon.Vertical ? new PointF(yPixel, yBase) : new PointF(xPixel, yPixel);
                points.Add(point);
            }

            path.AddCurve(points.ToArray(), 0.55f);
            using var ribbonPen = new Pen(Color.FromArgb(alpha, 180, 220, 255), 2.8f + 8f * (1f - progress))
            {
                LineJoin = LineJoin.Round,
                StartCap = LineCap.Round,
                EndCap = LineCap.Round
            };
            graphics.DrawPath(ribbonPen, path);
        }
    }

    private void DrawMesh(Graphics graphics, Rectangle bounds, float reveal, float settle)
    {
        if (_meshNodes.Count < 2)
        {
            return;
        }

        var threshold = Math.Max(0.2f, 0.5f - reveal * 0.18f);
        for (var i = 0; i < _meshNodes.Count; i++)
        {
            for (var j = i + 1; j < _meshNodes.Count; j++)
            {
                var a = _meshNodes[i];
                var b = _meshNodes[j];
                var distance = Distance(a.X, a.Y, b.X, b.Y);
                if (distance > threshold)
                {
                    continue;
                }

                var alpha = (int)(110 * (1f - distance / threshold) * (1f - reveal * 0.7f) + 30 * settle);
                using var pen = new Pen(Color.FromArgb(alpha, 120, 255, 210), 1f + (1f - distance / threshold) * 1.5f);
                graphics.DrawLine(
                    pen,
                    a.X * bounds.Width,
                    a.Y * bounds.Height,
                    b.X * bounds.Width,
                    b.Y * bounds.Height);
            }
        }

        foreach (var node in _meshNodes)
        {
            var radius = Math.Max(3f, node.Weight * Math.Min(bounds.Width, bounds.Height) * (0.02f + 0.01f * settle));
            var alpha = (int)(130 * (1f - reveal * 0.55f));
            using var brush = new SolidBrush(Color.FromArgb(alpha, 180, 255, 235));
            graphics.FillEllipse(brush, node.X * bounds.Width - radius / 2f, node.Y * bounds.Height - radius / 2f, radius, radius);
        }
    }

    private void DrawOrbitals(Graphics graphics, Rectangle bounds, float reveal)
    {
        var center = new PointF(bounds.Width / 2f, bounds.Height / 2f);
        var minDimension = Math.Min(bounds.Width, bounds.Height);

        foreach (var orbital in _orbitals)
        {
            var t = reveal * orbital.Speed + orbital.Seed;
            var angle = orbital.Angle + t * MathF.Tau;
            var radius = minDimension * (0.08f + orbital.Radius);
            var x = center.X + MathF.Cos(angle) * radius * (0.7f + reveal * 0.3f);
            var y = center.Y + MathF.Sin(angle * 1.3f) * radius * 0.55f;
            var size = orbital.Size * minDimension * (1f + 0.6f * reveal);
            var alpha = (int)(120 * (1f - reveal) + 40);

            using var brush = new SolidBrush(Color.FromArgb(alpha, 255, 225, 180));
            graphics.FillEllipse(brush, x - size / 2f, y - size / 2f, size, size);
        }
    }

    private void DrawBlooms(Graphics graphics, Rectangle bounds, float reveal)
    {
        foreach (var bloom in _blooms)
        {
            var progress = Math.Clamp((reveal * bloom.Speed) - 0.08f, 0f, 1f);
            if (progress <= 0f)
            {
                continue;
            }

            var center = new PointF(bounds.Width * bloom.X, bounds.Height * bloom.Y);
            var baseRadius = bloom.Size * Math.Min(bounds.Width, bounds.Height);
            for (var ring = 0; ring < 3; ring++)
            {
                var ringProgress = Math.Clamp(progress - ring * 0.12f, 0f, 1f);
                if (ringProgress <= 0f)
                {
                    continue;
                }

                var radius = baseRadius * (0.4f + ring * 0.5f + ringProgress * 1.4f);
                using var star = BuildStar(center, radius, 10, bloom.Angle + ring * 0.25f);
                using var brush = new PathGradientBrush(star)
                {
                    CenterColor = Color.FromArgb((int)(70 * (1f - ringProgress)), 255, 240, 220),
                    SurroundColors = [Color.FromArgb(0, 255, 160, 120)]
                };
                graphics.FillEllipse(brush, center.X - radius, center.Y - radius, radius * 2, radius * 2);
            }
        }
    }

    private void DrawAureole(Graphics graphics, Rectangle bounds, float reveal)
    {
        var rect = Rectangle.Inflate(bounds, (int)(bounds.Width * 0.05f), (int)(bounds.Height * 0.05f));
        using var path = new GraphicsPath();
        path.AddEllipse(rect);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = Color.FromArgb((int)(25 * (1f - reveal)), 255, 255, 255),
            SurroundColors = [Color.FromArgb((int)(160 * (1f - reveal)), 60, 120, 255)]
        };
        graphics.FillPath(brush, path);
    }

    private void DrawHighlights(Graphics graphics, Rectangle bounds, float progress)
    {
        var flareWidth = bounds.Width * (0.09f + 0.05f * MathF.Sin(progress * MathF.Tau));
        var x = bounds.Width * progress - flareWidth;
        var flareRect = new RectangleF(x, 0, flareWidth, bounds.Height);
        using var flareBrush = new LinearGradientBrush(
            flareRect,
            Color.FromArgb(0, 255, 255, 255),
            Color.FromArgb(110, 255, 250, 225),
            LinearGradientMode.Horizontal);
        var blend = new ColorBlend
        {
            Positions = [0f, 0.5f, 1f],
            Colors =
            [
                Color.FromArgb(0, 255, 255, 255),
                Color.FromArgb(110, 255, 248, 225),
                Color.FromArgb(0, 255, 255, 255)
            ]
        };
        flareBrush.InterpolationColors = blend;
        graphics.FillRectangle(flareBrush, flareRect);
    }

    private static GraphicsPath BuildShard(PointF center, float size, float angle, int mode)
    {
        var path = new GraphicsPath();
        var points = new List<PointF>();
        var count = mode switch
        {
            0 => 4,
            1 => 5,
            _ => 6
        };

        for (var i = 0; i < count; i++)
        {
            var radius = size * (0.38f + (i % 2 == 0 ? 0.62f : 0.28f));
            var theta = DegreesToRadians(angle + (360f / count) * i);
            points.Add(new PointF(
                center.X + MathF.Cos(theta) * radius,
                center.Y + MathF.Sin(theta) * radius));
        }

        path.AddPolygon(points.ToArray());
        return path;
    }

    private static GraphicsPath BuildStar(PointF center, float radius, int points, float rotation)
    {
        var path = new GraphicsPath();
        var vertices = new List<PointF>();
        for (var i = 0; i < points * 2; i++)
        {
            var r = i % 2 == 0 ? radius : radius * 0.42f;
            var theta = rotation + MathF.PI * i / points;
            vertices.Add(new PointF(center.X + MathF.Cos(theta) * r, center.Y + MathF.Sin(theta) * r));
        }

        path.AddPolygon(vertices.ToArray());
        return path;
    }

    private static float EaseInOutCubic(float value)
    {
        return value < 0.5f
            ? 4f * value * value * value
            : 1f - MathF.Pow(-2f * value + 2f, 3f) / 2f;
    }

    private static float EaseOutExpo(float value)
    {
        return value >= 1f ? 1f : 1f - MathF.Pow(2f, -10f * value);
    }

    private static float EaseInOutSine(float value)
    {
        return -(MathF.Cos(MathF.PI * value) - 1f) / 2f;
    }

    private static float DegreesToRadians(float value) => value * (MathF.PI / 180f);

    private static float Distance(float x1, float y1, float x2, float y2)
    {
        var dx = x2 - x1;
        var dy = y2 - y1;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private sealed record Shard(float X, float Y, float Delay, float Duration, float Angle, float Energy, int Mode);
    private sealed record Ribbon(float Anchor, float Start, float Width, float Frequency, float Phase, bool Vertical);
    private sealed record Orbital(float Angle, float Radius, float Size, float Speed, float Seed);
    private sealed record Bloom(float X, float Y, float Size, float Angle, float Speed);
    private sealed record MeshNode(float X, float Y, float Weight);
}

internal static class DateTimeExtensions
{
    public static DateTime TrimToMinute(this DateTime dateTime)
    {
        return new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, dateTime.Hour, dateTime.Minute, 0, dateTime.Kind);
    }
}
