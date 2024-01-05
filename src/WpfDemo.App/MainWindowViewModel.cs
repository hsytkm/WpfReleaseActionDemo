using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace WpfDemo.App;

internal class MainWindowViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    public string Title
    {
        get
        {
            static string getExeFilePath()
            {
                string? text = Environment.ProcessPath;
                text ??= Environment.GetCommandLineArgs()[0];
                return text;
            }
            static string getAppVersion()
            {
                var exePath = getExeFilePath();
                var exeVer = FileVersionInfo.GetVersionInfo(exePath).FileVersion;
                return exeVer is null ? "?" : string.Join('.', exeVer.Split('.').Take(3));
            }
            return $"WpfReleaseActionDemo Ver{getAppVersion()}";
        }
    }

    public string? TextSource
    {
        get => _textSource;
        set
        {
            if (SetProperty(ref _textSource, value))
            {
                UpperText = Model.Helper.ToUpper(value);
            }
        }
    }
    private string? _textSource;

    public string? UpperText
    {
        get => _upperText;
        set => SetProperty(ref _upperText, value);
    }
    private string? _upperText;
}
