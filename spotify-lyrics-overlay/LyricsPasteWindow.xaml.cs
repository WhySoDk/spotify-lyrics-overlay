using System.Windows;

namespace spotify_lyrics_overlay
{
    //asks for pasted LRC lyrics and uses them as the lyrics of a song
    public partial class LyricsPasteWindow : Window
    {
        // gets the lyrics, returns an error message or null when they were saved
        private readonly Func<string, string?> save;

        public LyricsPasteWindow(string song, Func<string, string?> save)
        {
            InitializeComponent();
            this.save = save;
            songText.Text = song;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) => lyricsBox.Focus();

        private void lyricsBox_TextChanged(object sender, EventArgs e) => errorText.Visibility = Visibility.Collapsed;

        // not the default button, Enter makes a new line in the lyrics box
        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            string? error = save(lyricsBox.Text);
            if (error == null)
            {
                DialogResult = true;
                return;
            }

            errorText.Text = error;
            errorText.Visibility = Visibility.Visible;
        }
    }
}
