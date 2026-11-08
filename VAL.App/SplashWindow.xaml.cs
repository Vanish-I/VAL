
using System.Windows;

namespace VAL.App;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void UpdateProgress(string status, int progress)
    {
        StatusText.Text = status;

        int value = Math.Clamp(progress, 0, 100);

        LoadingProgress.Value = value;
        ProgressText.Text = $"{value}%";
    }

    public void UpdateVersion(string version)
    {
        VersionText.Text = version;
    }
}
