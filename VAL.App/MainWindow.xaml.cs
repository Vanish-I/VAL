using Microsoft.Win32;
using VAL.Core.Models;
using VAL.Infrastructure.Mods;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Media;
using VAL.Infrastructure.Storage;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Threading.Tasks;

namespace VAL.App;

public partial class MainWindow : Window
{
    private void CopyPackageId_Click(object sender, RoutedEventArgs e)
    {
        if (ModsGrid.SelectedItem is not ModInfo mod)
            return;

        if (!string.IsNullOrWhiteSpace(mod.PackageId))
        {
            Clipboard.SetText(mod.PackageId);
        }
    }

    private void OpenWorkshop_Click(object sender, RoutedEventArgs e)
    {
        if (ModsGrid.SelectedItem is not ModInfo mod)
            return;

        if (string.IsNullOrWhiteSpace(mod.PublishedFileId))
            return;

        string url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={mod.PublishedFileId}";

        System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
    }
    private string? gamePath;
    private string? workshopPath;
    private ICollectionView? modsView;
    private readonly LauncherDataService dataService = new();
    private LauncherSettings settings = new();

    private string currentGameVersion = "";

    private readonly ModScanner scanner = new();

    private bool isUpdatingProfileSelector;

    private readonly ObservableCollection<ModInfo> mods = new();


    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += MainWindow_SourceInitialized;
    }

    public async Task InitializeAsync(SplashWindow splash)
    {
        // Этап 1. Подготовка интерфейса
        splash.UpdateProgress("Подготовка интерфейса...", 10);
        await Task.Yield();

        modsView = CollectionViewSource.GetDefaultView(mods);
        ModsGrid.ItemsSource = modsView;

        // Этап 2. Чтение настроек
        splash.UpdateProgress("Чтение настроек лаунчера...", 30);
        await Task.Yield();

        settings = dataService.Load();

        UpdateProfileSelector();

        // Этап 3. Определение игры
        splash.UpdateProgress("Определение установки RimWorld...", 50);
        await Task.Yield();

        if (!string.IsNullOrWhiteSpace(settings.GamePath) &&
            File.Exists(Path.Combine(settings.GamePath, "RimWorld.exe")))
        {
            UpdateGamePaths(settings.GamePath);
        }
        else
        {
            settings.GamePath = "";
        }

        // Этап 4. Восстановление списка модов
        splash.UpdateProgress("Восстановление списка модификаций...", 70);
        await Task.Yield();

        if (settings.Mods != null)
        {
            foreach (var mod in settings.Mods)
            {
                mods.Add(mod);
            }
        }

        UpdateModsCompatibility();

        // Этап 5. Подготовка списка
        splash.UpdateProgress("Подготовка списка модов...", 90);
        await Task.Yield();

        ApplyFilters();

        if (ModsGrid.Items.Count > 0)
        {
            ModsGrid.SelectedIndex = 0;
        }

        splash.UpdateProgress("Загрузка завершена", 100);
        await Task.Yield();
    }


    private void UpdateGamePaths(string path)
    {
        gamePath = path;

        var steamApps = new DirectoryInfo(gamePath)
            .Parent?.Parent?.FullName;

        if (steamApps != null)
        {
            workshopPath = Path.Combine(
                steamApps,
                "workshop",
                "content",
                "294100");
        }
        else
        {
            workshopPath = null;
        }

        UpdateGameVersion(path);
    }

    private void UpdateGameVersion(string path)
    {
        string versionFile = Path.Combine(path, "Version.txt");

        currentGameVersion = "";

        if (!File.Exists(versionFile))
        {
            GameVersionText.Text = "НЕ ОПРЕДЕЛЕНА";
            UpdateModsCompatibility();
            return;
        }

        try
        {
            string version = File.ReadAllText(versionFile).Trim();

            if (!string.IsNullOrWhiteSpace(version))
            {
                string[] parts = version.Split('.');

                if (parts.Length >= 2)
                {
                    currentGameVersion = $"{parts[0]}.{parts[1]}";
                }
            }

            GameVersionText.Text = string.IsNullOrWhiteSpace(currentGameVersion)
                ? "НЕ ОПРЕДЕЛЕНА"
                : currentGameVersion;
        }
        catch
        {
            currentGameVersion = "";
            GameVersionText.Text = "НЕ ОПРЕДЕЛЕНА";
        }

        UpdateModsCompatibility();
    }

    private void UpdateModsCompatibility()
    {
        foreach (var mod in mods)
        {
            if (string.IsNullOrWhiteSpace(currentGameVersion) ||
                mod.SupportedVersions == null ||
                mod.SupportedVersions.Count == 0)
            {
                mod.IsCompatible = null;
                continue;
            }

            mod.IsCompatible = mod.SupportedVersions.Any(
                version => string.Equals(
                    version.Trim(),
                    currentGameVersion,
                    StringComparison.OrdinalIgnoreCase));
        }
    }

    private void SelectGame_Click(
        object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку RimWorld"
        };

        if (dialog.ShowDialog() != true)
            return;

        gamePath = dialog.FolderName;

        string selectedPath = dialog.FolderName;

        string exe = Path.Combine(
            selectedPath, "RimWorld.exe");

        if (!File.Exists(exe))
        {
            MessageBox.Show(
                "RimWorld.exe не найден!\n\n" +
                "Проверьте выбранную папку:\n" + selectedPath);

            return;
        }

        UpdateGamePaths(selectedPath);

        settings.GamePath = gamePath;
        dataService.Save(settings);

        MessageBox.Show(
            "RimWorld успешно обнаружена!\n\n" +
            "Путь: " + exe);
    }


    private void ScanMods_Click(
    object sender, RoutedEventArgs e)
    {
        if (gamePath == null)
        {
            MessageBox.Show("Сначала выберите игру.");
            return;
        }

        // Запоминаем состояния уже загруженных модов
        var enabledStates = new Dictionary<string, bool>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var mod in mods)
        {
            string key = GetModKey(mod);
            enabledStates[key] = mod.IsEnabled;
        }

        // Очищаем старый список
        mods.Clear();

        // Добавляем моды, сохраняя их прежние состояния
        void AddScannedMods(IEnumerable<ModInfo> scannedMods)
        {
            foreach (var mod in scannedMods)
            {
                string key = GetModKey(mod);

                if (enabledStates.TryGetValue(key, out bool isEnabled))
                {
                    mod.IsEnabled = isEnabled;
                }

                mods.Add(mod);
            }
        }

        string localMods = Path.Combine(gamePath, "Mods");

        AddScannedMods(scanner.Scan(localMods, "Local"));

        if (workshopPath != null)
        {
            AddScannedMods(scanner.Scan(workshopPath, "Workshop"));
        }

        // Сохраняем актуальную конфигурацию
        SaveCurrentConfiguration();

        settings.GamePath = gamePath;
        settings.Mods = mods.ToList();

        dataService.Save(settings);

        ApplyFilters();
        UpdateModsCompatibility();
        ModsGrid.Items.Refresh();

        MessageBox.Show($"Найдено модов: {mods.Count}");
    }



    private void Launch_Click(
        object sender, RoutedEventArgs e)
    {
        if (gamePath == null)
        {
            MessageBox.Show("Сначала выберите игру.");
            return;
        }

        string exe = Path.Combine(
            gamePath, "RimWorld.exe");

        if (!File.Exists(exe))
        {
            MessageBox.Show("Файл RimWorld.exe не найден!");
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = exe,
            WorkingDirectory = gamePath,
            UseShellExecute = true
        });
    }

    private void ApplyFilters()
    {
        if (modsView == null)
            return;

        string search = SearchBox.Text.Trim().ToLowerInvariant();

        int selectedSource = SourceFilter.SelectedIndex;

        modsView.Filter = item =>
        {
            if (item is not ModInfo mod)
                return false;

            bool matchesSearch =
                string.IsNullOrEmpty(search) ||
                mod.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                mod.PackageId.Contains(search, StringComparison.OrdinalIgnoreCase);

            bool matchesSource = selectedSource switch
            {
                1 => mod.Source == "Workshop",
                2 => mod.Source == "Local",
                _ => true
            };

            return matchesSearch && matchesSource;
        };

        modsView.Refresh();
    }

    private void SearchBox_TextChanged(
        object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void SourceFilter_SelectionChanged(
        object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

private void ModsGrid_SelectionChanged(
    object sender,
    System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ModsGrid.SelectedItem is not ModInfo mod)
            return;

        ModName.Text = mod.Name;
        ModPackageId.Text = string.IsNullOrWhiteSpace(mod.PackageId)
            ? "—"
            : mod.PackageId;

        ModVersion.Text = string.IsNullOrWhiteSpace(mod.Version)
            ? "—"
            : mod.Version;

        ModSource.Text = mod.Source;

        ModSupportedVersions.Text =
    mod.SupportedVersions != null &&
    mod.SupportedVersions.Count > 0
        ? string.Join(", ", mod.SupportedVersions)
        : "Версии не указаны";

        ModPreview.Source = null;

        if (!string.IsNullOrWhiteSpace(mod.PreviewPath) &&
            System.IO.File.Exists(mod.PreviewPath))
        {
            try
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();

                bitmap.BeginInit();
                bitmap.UriSource = new Uri(mod.PreviewPath);
                bitmap.CacheOption =
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                ModPreview.Source = bitmap;
            }
            catch
            {
                ModPreview.Source = null;
            }
        }
    }

    private void ModsGrid_PreviewMouseRightButtonDown(
        object sender,
        System.Windows.Input.MouseButtonEventArgs e)
    {
        var row = FindVisualParent<System.Windows.Controls.DataGridRow>(
            e.OriginalSource as System.Windows.DependencyObject);

        if (row != null)
        {
            row.IsSelected = true;
            row.Focus();
        }

        if (ModsGrid.ContextMenu?.Items.Count > 0 &&
            ModsGrid.ContextMenu.Items[0] is System.Windows.Controls.MenuItem workshopItem)
        {
            workshopItem.IsEnabled =
                ModsGrid.SelectedItem is ModInfo selected &&
                !string.IsNullOrWhiteSpace(selected.PublishedFileId);
        }
    }

    private static T? FindVisualParent<T>(
        System.Windows.DependencyObject? child)
        where T : System.Windows.DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
                return parent;

            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
        }

        return null;
    }
    private void OpenModFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ModsGrid.SelectedItem is not ModInfo mod)
            return;

        if (System.IO.Directory.Exists(mod.Path))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = mod.Path,
                UseShellExecute = true
            });
        }
    }
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int size);

    private void MainWindow_SourceInitialized(
        object? sender,
        EventArgs e)
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;

        int darkMode = 1;

        int result = DwmSetWindowAttribute(
            hwnd,
            20,
            ref darkMode,
            sizeof(int));

        if (result != 0)
        {
            DwmSetWindowAttribute(
                hwnd,
                19,
                ref darkMode,
                sizeof(int));
        }
    }

    private void SaveCurrentConfiguration()
    {
        var activeProfile = settings.Profiles.FirstOrDefault(
            p => p.Id == settings.ActiveProfileId);

        foreach (var mod in mods)
        {
            string key = GetModKey(mod);

            if (activeProfile != null)
            {
                activeProfile.EnabledMods[key] = mod.IsEnabled;
            }
            else
            {
                settings.CurrentConfiguration[key] = mod.IsEnabled;
            }
        }

        settings.Mods = mods.ToList();
        settings.GamePath = gamePath ?? "";
    }

    private void CreateProfile(string profileName)
    {
        string name = profileName.Trim();

        if (string.IsNullOrWhiteSpace(name))
            return;

        // Сохраняем конфигурацию, с которой работали
        SaveCurrentConfiguration();

        var profile = new ModProfile
        {
            Name = name
        };

        // Копируем состояния всех модов
        foreach (var mod in mods)
        {
            string key = GetModKey(mod);
            profile.EnabledMods[key] = mod.IsEnabled;
        }

        // Добавляем профиль и делаем его активным
        settings.Profiles.Add(profile);
        settings.ActiveProfileId = profile.Id;

        SaveCurrentConfiguration();
        dataService.Save(settings);
    }

    private void CreateProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CreateProfileWindow
        {
            Owner = this
        };

        if (dialog.ShowDialog() != true)
            return;

        string name = dialog.ProfileName;

        if (settings.Profiles.Any(
            p => string.Equals(
                p.Name,
                name,
                StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                "Профиль с таким названием уже существует.",
                "VAL Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        CreateProfile(name);



    }

    private void ManageProfiles_Click(object sender, RoutedEventArgs e)
    {
        var window = new ManageProfilesWindow(settings)
        {
            Owner = this
        };

        window.ShowDialog();

        UpdateProfileSelector();
    }

    private void ModEnabled_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentConfiguration();
        dataService.Save(settings);
    }

    private string GetModKey(ModInfo mod)
    {
        return !string.IsNullOrWhiteSpace(mod.PackageId)
            ? mod.PackageId
            : mod.Path;
    }

    private void UpdateProfileSelector()
    {
        isUpdatingProfileSelector = true;

        try
        {
            ProfileSelector.Items.Clear();

            ProfileSelector.Items.Add(new ComboBoxItem
            {
                Content = "Текущая конфигурация",
                Tag = ""
            });

            foreach (var profile in settings.Profiles)
            {
                ProfileSelector.Items.Add(new ComboBoxItem
                {
                    Content = profile.Name,
                    Tag = profile.Id
                });
            }

            var activeProfile = settings.Profiles.FirstOrDefault(
                p => p.Id == settings.ActiveProfileId);

            string activeId = activeProfile?.Id ?? "";

            foreach (ComboBoxItem item in ProfileSelector.Items)
            {
                if (string.Equals(
                    item.Tag?.ToString() ?? "",
                    activeId,
                    StringComparison.Ordinal))
                {
                    ProfileSelector.SelectedItem = item;
                    break;
                }
            }

            ActiveProfileText.Text = activeProfile != null
                ? $"Активная конфигурация: {activeProfile.Name}"
                : "Активная конфигурация: Текущая конфигурация";
        }
        finally
        {
            isUpdatingProfileSelector = false;
        }
    }

    private void ProfileSelector_SelectionChanged(
    object sender,
    SelectionChangedEventArgs e)
    {
        if (isUpdatingProfileSelector ||
            ProfileSelector.SelectedItem is not ComboBoxItem selectedItem)
        {
            return;
        }

        // Сохраняем состояние предыдущего профиля
        SaveCurrentConfiguration();

        // Получаем ID выбранного профиля
        string profileId = selectedItem.Tag?.ToString() ?? "";

        settings.ActiveProfileId = profileId;

        // Получаем настройки выбранного профиля
        var profile = settings.Profiles.FirstOrDefault(
            p => p.Id == profileId);

        var configuration = profile?.EnabledMods
            ?? settings.CurrentConfiguration;

        // Применяем настройки к модам
        foreach (var mod in mods)
        {
            string key = GetModKey(mod);

            if (configuration.TryGetValue(key, out bool isEnabled))
            {
                mod.IsEnabled = isEnabled;
            }
            else
            {
                mod.IsEnabled = true;
            }
        }

        // Сохраняем применённую конфигурацию
        SaveCurrentConfiguration();

        dataService.Save(settings);

        ModsGrid.Items.Refresh();

        UpdateProfileSelector();
    }

}