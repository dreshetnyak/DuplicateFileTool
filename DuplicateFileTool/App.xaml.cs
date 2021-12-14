using System.Globalization;
using System.Threading;
using System.Windows;
using DuplicateFileTool.Configuration;

namespace DuplicateFileTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var culture = new CultureInfo(FileAppConfig.Get($"{nameof(ProgramConfiguration)}.{nameof(ProgramConfiguration.SelectedCulture)}", "en"));
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}
