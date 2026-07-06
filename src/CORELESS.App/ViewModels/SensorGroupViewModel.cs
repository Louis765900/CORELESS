using System.Collections.ObjectModel;

namespace Coreless.ViewModels;

/// <summary>A titled bucket of sensors (e.g. all "Températures") for the detail view.</summary>
public sealed class SensorGroupViewModel
{
    public SensorGroupViewModel(string title, IEnumerable<SensorViewModel> sensors)
    {
        Title = title;
        Sensors = new ObservableCollection<SensorViewModel>(sensors);
    }

    public string Title { get; }
    public ObservableCollection<SensorViewModel> Sensors { get; }
}
