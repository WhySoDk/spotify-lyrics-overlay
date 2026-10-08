using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;

namespace spotify_lyrics_overlay
{
    //whole number box with - and + buttons, arrow keys and the mouse wheel step by one
    public partial class NumberBox : UserControl
    {
        private int value;
        private bool updatingText;

        public int Minimum { get; set; }
        public int Maximum { get; set; } = 100;

        public event EventHandler? ValueChanged;

        public NumberBox()
        {
            InitializeComponent();
            showValue();
        }

        public int Value
        {
            get => value;
            set
            {
                int clamped = Math.Clamp(value, Minimum, Maximum);
                if (clamped != this.value)
                {
                    this.value = clamped;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
                showValue();
            }
        }

        public void SetRange(int minimum, int maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
            Value = value;
        }

        private void showValue()
        {
            updatingText = true;
            valueBox.Text = value.ToString();
            updatingText = false;
        }

        private void decreaseButton_Click(object sender, System.Windows.RoutedEventArgs e) => Value = value - 1;

        private void increaseButton_Click(object sender, System.Windows.RoutedEventArgs e) => Value = value + 1;

        private void valueBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(c => char.IsDigit(c) || (c == '-' && Minimum < 0));
        }

        //typed values apply right away when they are in range, the box is cleaned up on leave
        private void valueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (updatingText) return;
            if (int.TryParse(valueBox.Text, out int typed) && typed >= Minimum && typed <= Maximum && typed != value)
            {
                value = typed;
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void valueBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Value = int.TryParse(valueBox.Text, out int typed) ? typed : value;
        }

        private void valueBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Up: Value = value + 1; e.Handled = true; break;
                case Key.Down: Value = value - 1; e.Handled = true; break;
                case Key.Enter: Value = int.TryParse(valueBox.Text, out int typed) ? typed : value; e.Handled = true; break;
            }
        }

        private void valueBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!valueBox.IsKeyboardFocusWithin) return;
            Value = value + Math.Sign(e.Delta);
            e.Handled = true;
        }
    }
}
