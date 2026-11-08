using System.Windows;

namespace VAL.App;

public partial class CreateProfileWindow : Window
{
    public string ProfileName { get; private set; } = "";

    public CreateProfileWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ProfileNameBox.Focus();
    }

    private void Create_Click(object sender, RoutedEventArgs e)
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