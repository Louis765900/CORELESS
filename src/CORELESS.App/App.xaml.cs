using System.Windows;
using QuestPDF.Infrastructure;

namespace Coreless;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // QuestPDF Community licence (free for individuals / small orgs)
        QuestPDF.Settings.License = LicenseType.Community;
    }
}
