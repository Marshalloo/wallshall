using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

/// Анимация значка в трее, пока идёт смена обоев.
///
/// Пока доля неизвестна — крутится дуга, как только пошло скачивание — кольцо
/// заполняется. Кадры рисуются один раз при запуске: каждый значок держит
/// неуправляемый дескриптор, и создавать их на ходу значило бы их копить.
sealed class TrayBusy : IProgress<double>, IDisposable
{
    const int FrameMs = 80;
    const int SpinSteps = 12;
    const int RingSteps = 21;
    const float SpinSweep = 120f;

    /// Короткая смена из кэша проходит мгновенно, и мигать значком незачем.
    const int DelayMs = 400;

    readonly NotifyIcon tray;
    readonly Icon idle;
    readonly Icon[] spin;
    readonly Icon[] ring;
    readonly List<IntPtr> handles = new();
    readonly System.Windows.Forms.Timer frames = new() { Interval = FrameMs };
    readonly System.Windows.Forms.Timer delay = new() { Interval = DelayMs };

    int depth;
    int frame;
    volatile int percent = -1;

    public TrayBusy(NotifyIcon tray, Icon idle)
    {
        this.tray = tray;
        this.idle = idle;

        int size = Math.Max(SystemInformation.SmallIconSize.Width, 16);
        spin = Enumerable.Range(0, SpinSteps).Select(i => Icon(size, null, i)).ToArray();
        ring = Enumerable.Range(0, RingSteps).Select(i => Icon(size, (double)i / (RingSteps - 1), 0)).ToArray();

        frames.Tick += (_, _) => Draw();
        delay.Tick += (_, _) => Show();
    }

    /// Пока область живёт, значок показывает работу.
    public IDisposable Work()
    {
        if (++depth == 1)
        {
            percent = -1;
            delay.Start();
        }
        return new Scope(this);
    }

    /// Вызывается из фонового потока: только записывает число, значок рисует таймер.
    public void Report(double value) => percent = (int)Math.Round(Math.Clamp(value, 0, 1) * 100);

    void Finish()
    {
        if (--depth > 0) return;

        delay.Stop();
        frames.Stop();
        percent = -1;
        tray.Icon = idle;
    }

    void Show()
    {
        delay.Stop();
        tray.Text = "Меняю обои…";
        Draw();
        frames.Start();
    }

    void Draw()
    {
        int done = percent;
        if (done < 0)
        {
            frame = (frame + 1) % SpinSteps;
            tray.Icon = spin[frame];
            return;
        }

        tray.Icon = ring[done * (RingSteps - 1) / 100];
        tray.Text = $"Скачиваю… {done}%";
    }

    Icon Icon(int size, double? progress, int step)
    {
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float width = Math.Max(2f, size / 6f);
            var box = new RectangleF(width / 2 + 0.5f, width / 2 + 0.5f,
                                     size - width - 1, size - width - 1);

            using var arc = new Pen(Theme.Accent, width)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };

            // У кольца прогресса подложка показывает остаток; у дуги она только мешала бы
            if (progress is double share)
            {
                using (var track = new Pen(Color.FromArgb(55, Theme.Accent), width))
                    g.DrawEllipse(track, box);

                g.DrawArc(arc, box, -90, (float)(360 * Math.Max(share, 0.03)));
            }
            else g.DrawArc(arc, box, step * (360f / SpinSteps) - 90, SpinSweep);
        }

        var handle = bitmap.GetHicon();
        handles.Add(handle);
        return System.Drawing.Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        frames.Dispose();
        delay.Dispose();
        foreach (var handle in handles) DestroyIcon(handle);
        handles.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool DestroyIcon(IntPtr handle);

    sealed class Scope : IDisposable
    {
        readonly TrayBusy owner;
        bool closed;

        public Scope(TrayBusy owner) => this.owner = owner;

        public void Dispose()
        {
            if (closed) return;
            closed = true;
            owner.Finish();
        }
    }
}
