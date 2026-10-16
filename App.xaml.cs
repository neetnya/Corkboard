using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Corkboard;

public partial class App : Application
{
    private const string MutexName = @"Local\Corkboard.SingleInstance";
    private const string WakeName = @"Local\Corkboard.Wake";

    private Mutex? _mutex;
    private EventWaitHandle? _wake;
    private CancellationTokenSource? _wakeCts;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, MutexName, out bool created);
        _ownsMutex = created;
        _wake = new EventWaitHandle(false, EventResetMode.AutoReset, WakeName);
        if (!created)
        {
            try { _wake.Set(); } catch { }
            Shutdown();
            return;
        }

        base.OnStartup(e);
        DispatcherUnhandledException += (_, ex) => { Log(ex.Exception); ex.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log(ex.ExceptionObject as Exception);

        var win = new MainWindow();
        MainWindow = win;
        win.Show();

        _wakeCts = new CancellationTokenSource();
        var token = _wakeCts.Token;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                if (_wake.WaitOne(TimeSpan.FromMilliseconds(400)))
                    Dispatcher.Invoke(RestoreMainWindow);
            }
        }, token);
    }

    private void RestoreMainWindow()
    {
        if (MainWindow is not Window w) return;
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Show();
        w.Activate();
        w.Topmost = true;
        w.Topmost = false;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _wakeCts?.Cancel();
        if (_ownsMutex)
        {
            try { _mutex?.ReleaseMutex(); } catch { }
        }
        _mutex?.Dispose();
        _wake?.Dispose();
        base.OnExit(e);
    }

    private static void Log(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch { }
    }
}
