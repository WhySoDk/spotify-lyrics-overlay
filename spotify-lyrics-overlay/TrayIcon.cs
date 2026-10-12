namespace spotify_lyrics_overlay
{
    //icon in the notification area, the settings window hides to it instead of closing the app
    internal sealed class TrayIcon : IDisposable
    {
        private const string AppName = "Spotify Lyrics Overlay";

        private readonly NotifyIcon icon;
        private readonly ToolStripMenuItem runItem;
        private bool tipShown;

        public event Action? OpenRequested;
        public event Action? RunToggled;
        public event Action? ExitRequested;

        public TrayIcon()
        {
            runItem = new ToolStripMenuItem("Start", null, (s, e) => RunToggled?.Invoke());
            var openItem = new ToolStripMenuItem("Settings", null, (s, e) => OpenRequested?.Invoke());
            openItem.Font = new Font(openItem.Font, FontStyle.Bold);

            var menu = new ContextMenuStrip();
            menu.Items.Add(openItem);
            menu.Items.Add(runItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => ExitRequested?.Invoke()));

            icon = new NotifyIcon
            {
                Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
                Text = AppName,
                ContextMenuStrip = menu,
                Visible = true
            };
            icon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) OpenRequested?.Invoke();
            };
        }

        public void ShowRunState(bool started)
        {
            runItem.Text = started ? "Stop" : "Start";
            icon.Text = started ? $"{AppName} — running" : $"{AppName} — stopped";
        }

        //once per run, the first time the window goes away
        public void ShowStillRunningTip()
        {
            if (tipShown) return;
            tipShown = true;
            icon.ShowBalloonTip(3000, AppName, "Still running here. Right-click the icon to exit.", ToolTipIcon.None);
        }

        public void Dispose()
        {
            icon.Visible = false;
            icon.ContextMenuStrip?.Dispose();
            icon.Dispose();
        }
    }
}
