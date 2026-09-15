using CommunityToolkit.Mvvm.ComponentModel;
using KeyPulse.Core;

namespace KeyPulse.App;

public sealed class MainWindowViewModel : ObservableObject
{
    public string Title => ProductInfo.Name;

    public string Version => ProductInfo.Version;

    public string PrivacyNotice => ProductInfo.PrivacyNotice;
}
