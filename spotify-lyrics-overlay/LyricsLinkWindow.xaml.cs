using System.Windows;

namespace spotify_lyrics_overlay
{
    //asks for a lrclib link and uses that record as the lyrics of a song
    public partial class LyricsLinkWindow : Window
    {
        // gets the link, returns an error message or null when the lyrics were saved
        private readonly Func<string, Task<string?>> save;

        public LyricsLinkWindow(string song, Func<string, Task<string?>> save)
        {
            InitializeComponent();
            this.save = save;
            songText.Text = song;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => linkBox.Focus();

        private void linkBox_TextChanged(object sender, EventArgs e) => errorText.Visibility = Visibility.Collapsed;

        private async void saveButton_Click(object sender, RoutedEventArgs e)
        {
            saveButton.IsEnabled = false;
            linkBox.IsEnabled = false;
            try
            {
                string? error = await save(linkBox.Text.Trim());
                if (error == null)
                {
                    DialogResult = true;
                    return;
                }

                errorText.Text = error;
                errorText.Visibility = Visibility.Visible;
            }
            finally
            {
                saveButton.IsEnabled = true;
                linkBox.IsEnabled = true;
            }
        }
    }
}
