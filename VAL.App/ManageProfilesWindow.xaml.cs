using System.Linq;
using VAL.Infrastructure.Storage;
using System.Windows;
using VAL.Core.Models;

namespace VAL.App;

public partial class ManageProfilesWindow : Window
{
    private readonly LauncherSettings settings;

    private readonly LauncherDataService dataService = new();

    public ManageProfilesWindow(LauncherSettings launcherSettings)
    {
        InitializeComponent();

        settings = launcherSettings;

        LoadProfiles();
    }

    private void LoadProfiles()
    {
        ProfilesList.ItemsSource = null;

        ProfilesList.ItemsSource = settings.Profiles;
    }

    private void ManageProfiles_Click(object sender, RoutedEventArgs e)
    {
        var window = new ManageProfilesWindow(settings)
        {
            Owner = this
        };

        window.ShowDialog();

    }

    private void RenameProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is not ModProfile profile)
        {
            MessageBox.Show(
                "Сначала выберите профиль.",
                "VAL Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var dialog = new RenameProfileWindow(profile.Name)
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
            return;

        string newName = dialog.ProfileName.Trim();

        bool alreadyExists = settings.Profiles.Any(p =>
            p.Id != profile.Id &&
            string.Equals(
                p.Name,
                newName,
                StringComparison.OrdinalIgnoreCase));

        if (alreadyExists)
        {
            MessageBox.Show(
                "Профиль с таким названием уже существует.",
                "VAL Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        profile.Name = newName;

        dataService.Save(settings);

        LoadProfiles();

        ProfilesList.SelectedItem = profile;
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is not ModProfile profile)
        {
            MessageBox.Show(
                "Сначала выберите профиль.",
                "VAL Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        var result = MessageBox.Show(
            $"Вы действительно хотите удалить профиль «{profile.Name}»?",
            "Удаление профиля",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        // Если удаляем активный профиль, сохраняем его конфигурацию
        if (settings.ActiveProfileId == profile.Id)
        {
            settings.CurrentConfiguration =
                new Dictionary<string, bool>(profile.EnabledMods);

            settings.ActiveProfileId = "";
        }

        settings.Profiles.Remove(profile);

        dataService.Save(settings);

        LoadProfiles();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

}
