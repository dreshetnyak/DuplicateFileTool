using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Media;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Configuration;

internal sealed class SortOrderData(SortOrder order, string name)
{
    public SortOrder Order { get; } = order;
    public string Name { get; } = name;
}

internal sealed class InclusionTypeData(InclusionType type, string name)
{
    public InclusionType Type { get; } = type;
    public string Name { get; } = name;
}

internal sealed class LanguageData(string cultureName, string languageName, ImageSource languageCountryFlag)
{
    public string CultureName { get; } = cultureName;
    public string LanguageName { get; } = languageName;
    public ImageSource LanguageCountryFlag { get; } = languageCountryFlag;
}

internal sealed class Configuration : NotifyPropertyChanged, IDisposable
{
    private bool _hasChanged;
    private bool _hasUnsavedChanges;
    private LanguageData _selectedLanguageData;

    private static readonly InclusionTypeData[] PathComparisonTypesData =
    [
        new(InclusionType.Include, Resources.Ui_Search_Path_Include),
        new(InclusionType.Exclude, Resources.Ui_Search_Path_Exclude)
    ];
    private static readonly SortOrderData[] SortOrderTypesData =
    [
        new(SortOrder.Number, Resources.Ui_Results_Sorting_By_Number),
        new(SortOrder.Size, Resources.Ui_Results_Sorting_By_Size),
        new(SortOrder.Path, Resources.Ui_Results_Sorting_By_Path),
        new(SortOrder.Name, Resources.Ui_Results_Sorting_By_Name)
    ];
    private static readonly LanguageData[] SupportedLanguagesData =
    [
        new("en", Resources.Ui_Language_Name_English, Resources.FlagUsa.ToImageSource()),
        new("es", Resources.Ui_Language_Name_Spanish, Resources.FlagSpain.ToImageSource()),
        new("ru", Resources.Ui_Language_Name_Russian, Resources.FlagRussia.ToImageSource())
    ];

    public string ApplicationName { get; } = ConfigManager.GetAppName();
    public Logger Log { get; }
    public bool HasChanged
    {
        get => _hasChanged;
        // ReSharper disable once UnusedMember.Local
#pragma warning disable S1144
        private set
        {
            _hasChanged = value;
            OnPropertyChanged();
        }
#pragma warning restore S1144
    }
    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        set
        {
            if (_hasUnsavedChanges == value)
                return;
            _hasUnsavedChanges = value;
            OnPropertyChanged();
        }
    }

    private string StartupSelectedCulture { get; }
    public bool SelectedCultureChanged => ProgramConfig.SelectedCulture.Value != StartupSelectedCulture;
    public LanguageData SelectedLanguageData
    {
        get => _selectedLanguageData;
        set
        {
            _selectedLanguageData = value; 
            OnPropertyChanged();
            if (ProgramConfig.SelectedCulture.Value != value.CultureName)
                ProgramConfig.SelectedCulture.Value = value.CultureName;
        }
    }

    public ProgramConfiguration ProgramConfig { get; }
    public ObservableCollection<object> ProgramConfigParams { get; }
    public SearchConfiguration SearchConfig { get; }
    public ObservableCollection<object> SearchConfigParams { get; }
    public ExtensionsConfiguration ExtensionsConfig { get; }
    public ObservableCollection<object> ExtensionsConfigParams { get; }
    public ResultsConfiguration ResultsConfig { get; }
    public ObservableCollection<object> ResultsConfigParams { get; }

    public IReadOnlyCollection<IFileComparer> FileComparers { get; }
    
    // ReSharper disable ConvertToAutoProperty
    public static InclusionTypeData[] PathComparisonTypes => PathComparisonTypesData;
    public static SortOrderData[] SortOrderTypes => SortOrderTypesData;
    public static LanguageData[] SupportedLanguages => SupportedLanguagesData;
    // ReSharper restore ConvertToAutoProperty

    public Configuration()
    {
        Log = new Logger(Logger.Target.Debug);

        ProgramConfig = new ProgramConfiguration();
        ProgramConfigParams = new ObservableCollection<object>(ProgramConfig
            .GetGenericPropertiesObjects(typeof(IConfigurationProperty<>))
            .Where(IsParameterIncluded));

        SearchConfig = new SearchConfiguration();
        SearchConfigParams = new ObservableCollection<object>(SearchConfig
            .GetGenericPropertiesObjects(typeof(IConfigurationProperty<>))
            .Where(IsParameterIncluded));
            
        ExtensionsConfig = new ExtensionsConfiguration();
        ExtensionsConfigParams = new ObservableCollection<object>(ExtensionsConfig
            .GetGenericPropertiesObjects(typeof(IConfigurationProperty<>))
            .Where(IsParameterIncluded));
          
        ResultsConfig = new ResultsConfiguration();
        ResultsConfigParams = new ObservableCollection<object>(ResultsConfig
            .GetGenericPropertiesObjects(typeof(IConfigurationProperty<>))
            .Where(IsParameterIncluded));

        try { ProgramConfig.LoadFromAppConfig(); }
        catch (Exception ex) { Log.Write("Error: Loading application configuration from app config failed with the exception: " + ex); throw; }
        ProgramConfig.HasChanged = false;
        ProgramConfig.PropertyChanged += OnConfigurationChanged;
        ProgramConfig.SelectedCulture.PropertyChanged += OnSelectedCultureChanged;
        var selectedCulture = ProgramConfig.SelectedCulture.Value ?? "en";
        StartupSelectedCulture = selectedCulture;
        _selectedLanguageData = Array.Find(SupportedLanguages, lang => lang.CultureName == selectedCulture) ?? SupportedLanguages[0];

        if (ProgramConfig.SelectedCulture.Value != _selectedLanguageData.CultureName)
            ProgramConfig.SelectedCulture.Value = _selectedLanguageData.CultureName;

        try { SearchConfig.LoadFromAppConfig(); }
        catch (Exception ex) { Log.Write("Error: Loading search configuration from app config failed with the exception: " + ex); throw; }
        SearchConfig.HasChanged = false;
        SearchConfig.PropertyChanged += OnConfigurationChanged;

        try { ResultsConfig.LoadFromAppConfig(); }
        catch (Exception ex) { Log.Write("Error: Loading results configuration from app config failed with the exception: " + ex); throw; }
        ResultsConfig.HasChanged = false;
        ResultsConfig.PropertyChanged += OnConfigurationChanged;

        try { ExtensionsConfig.LoadFromAppConfig(); }
        catch (Exception ex) { Log.Write("Error: Loading extensions configuration from app config failed with the exception: " + ex); throw; }
        ExtensionsConfig.HasChanged = false;
        ExtensionsConfig.PropertyChanged += OnConfigurationChanged;

        FileComparers = GetFileComparers().ToArray();
    }

    private static bool IsParameterIncluded(object? parameter)
    {
        if (parameter == null)
            return false;

        var itemType = parameter.GetType();
        if (!itemType.ImplementsInterfaceGeneric(typeof(IConfigurationProperty<>)))
            return false;

        var isHiddenProperty = itemType.GetProperty(nameof(IConfigurationProperty<int>.IsHidden));
        return isHiddenProperty != null && isHiddenProperty.GetValue(parameter) is false;
    }

    public void Dispose()
    {
        if (HasUnsavedChanges)
            SaveChanges();

        ProgramConfig.Dispose();
        SearchConfig.Dispose();
        ExtensionsConfig.Dispose();
        ResultsConfig.Dispose();
        foreach (var comparer in FileComparers) 
            (comparer as IDisposable)?.Dispose();
        Log.Dispose();
    }

    private void SaveChanges()
    {
        if (HasChanged)
            this.SaveToAppConfig();
        if (ProgramConfig.HasChanged)
            ProgramConfig.SaveToAppConfig();
        if (SearchConfig.HasChanged)
            SearchConfig.SaveToAppConfig();
        if (ResultsConfig.HasChanged)
            ResultsConfig.SaveToAppConfig();
        if (ExtensionsConfig.HasChanged)
            ExtensionsConfig.SaveToAppConfig();
    }

    private void OnConfigurationChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (!HasUnsavedChanges && sender is IChangeable { HasChanged: true })
            HasUnsavedChanges = true;
    }

    private void OnSelectedCultureChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ConfigurationProperty<string>.Value))
            OnPropertyChanged(nameof(SelectedCultureChanged));
    }

    private static IEnumerable<IFileComparer> GetFileComparers()
    {
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(type => type.Namespace?.EndsWith(nameof(Comparers)) ?? false))
        {
            if (type.GetTypeInfo().ImplementedInterfaces.All(implementedInterfaceType => implementedInterfaceType != typeof(IFileComparer)))
                continue; // If it doesn't implement IFileComparer
            if (type.GetConstructor(Type.EmptyTypes) == null) 
                continue; // If there is no parameterless constructor

            IFileComparer? fileComparer;
            try { fileComparer = Activator.CreateInstance(type) as IFileComparer; }
            catch (Exception ex) { Debug.Fail(ex.ToString()); continue; }

            if(fileComparer == null)
                continue; 

            yield return fileComparer;
        }
    }
}