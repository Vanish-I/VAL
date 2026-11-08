
using System.Windows;

namespace VAL.App;

public partial class RenameProfileWindow : Window
{
    public string ProfileName { get; private set; } = "";

    public RenameProfileWindow(string currentName)
    {
        InitializeComponent();

        ProfileNameBox.Text = currentName;

        Loaded += (_, _) =>
        {
            ProfileNameBox.Focus();
            ProfileNameBox.SelectAll();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = ProfileNameBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(
                "Введите название профиля.",
                "VAL Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        ProfileName = name;
        DialogResult = true;
    }
}
