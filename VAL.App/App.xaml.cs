using System;
using System.Windows;
using System.Windows.Threading;

namespace VAL.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var splash = new SplashWindow();

        MainWindow = splash;
        splash.UpdateVersion("VAL LAUNCHER");
        splash.UpdateProgress("Запуск VAL Launcher...", 0);

        splash.Show();

        try
        {
            // Даём загрузочному экрану отобразиться
            await Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Render);

            // Создаём главное окно
            var mainWindow = new MainWindow();

            // Выполняем настоящую инициализацию
            await mainWindow.InitializeAsync(splash);

            // Показываем готовый интерфейс
            MainWindow = mainWindow;
            mainWindow.Show();

            splash.Close();
        }
        catch (Exception ex)
        {
            splash.Close();

            MessageBox.Show(
                $"Не удалось запустить VAL Launcher.\n\n{ex.Message}",
                "Ошибка запуска",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown();
        }
    }
}
