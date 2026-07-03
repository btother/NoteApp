using System.Windows;
using System.Windows.Input;

namespace NoteApp.Views
{
    public partial class RenameDialog : Window
    {
        public string NewName { get; private set; } = "";

          
        public RenameDialog(string currentName)
        {
            InitializeComponent();
            NameBox.Text = currentName;
            NameBox.SelectAll();
            NameBox.Focus();
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NewName = NameBox.Text.Trim();
                DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;

        private void NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) Ok_Click(sender, e);
            if (e.Key == Key.Escape) Cancel_Click(sender, e);
        }
    }

        

}