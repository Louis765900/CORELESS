using LibreHardwareMonitor.Hardware;

namespace Coreless.Services;

/// <summary>
/// Owns the LibreHardwareMonitor <see cref="Computer"/> instance. Opening loads the
/// Ring0 kernel driver (needs admin) which unlocks temperatures, voltages and clocks.
/// </summary>
public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private readonly UpdateVisitor _visitor = new();
    private bool _opened;

    public HardwareMonitorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = true,
            IsControllerEnabled = true,
            IsBatteryEnabled = true,
            IsPsuEnabled = true,
        };
    }

    public IEnumerable<IHardware> Hardware => _computer.Hardware;

    public void Open()
    {
        if (_opened) return;
        _computer.Open();
        _opened = true;
    }

    /// <summary>Refresh every hardware node and its sub-hardware sensors.</summary>
    public void Update()
    {
        if (!_opened) return;
        _computer.Accept(_visitor);
    }

    public void Dispose()
    {
        if (_opened) _computer.Close();
        _opened = false;
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer) => computer.Traverse(this);

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (IHardware sub in hardware.SubHardware)
                sub.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
