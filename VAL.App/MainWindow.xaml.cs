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

    private readonly ModScanner scanner = new();

    private readonly ObservableCollection<ModInfo> mods = new();

    public MainWindow()
    {
        InitializeComponent();

        modsView = CollectionViewSource.GetDefaultView(mods);

        ModsGrid.ItemsSource = modsView;

        ApplyFilters();
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

        string exe = Path.Combine(
            gamePath, "RimWorld.exe");

        if (!File.Exists(exe))
        {
            MessageBox.Show(
                "RimWorld.exe не найден!\n\n" +
                "Проверьте выбранную папку:\n" + gamePath);

            gamePath = null;
            return;
        }

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

        mods.Clear();

        string localMods = Path.Combine(
            gamePath, "Mods");

        foreach (var mod in scanner.Scan(
            localMods, "Local"))
        {
            mods.Add(mod);
        }

        if (workshopPath != null)
        {
            foreach (var mod in scanner.Scan(
                workshopPath, "Workshop"))
            {
                mods.Add(mod);
            }
        }

        MessageBox.Show(
            $"Найдено модов: {mods.Count}");
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
}