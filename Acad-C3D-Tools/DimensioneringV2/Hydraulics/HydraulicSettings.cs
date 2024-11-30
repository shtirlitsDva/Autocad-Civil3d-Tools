using System.ComponentModel;
using System.Runtime.CompilerServices;

public class HydraulicSettings : INotifyPropertyChanged
{
    // Miscellaneous
    public int HotWaterReturnTemp { get; set; } = 75;
    public double FactorTillægForOpvarmningUdenBrugsvandsprioritering { get; set; } = 0.6;
    public double MinDifferentialPressureOverHovedHaner { get; set; } = 0.5;
    public string CalculationType { get; set; } = "CW"; // "CW" or "TM"
    public bool ReportToConsole { get; set; } = true;

    // Supply Lines (FL)
    public int TempFremFL { get; set; } = 110;
    public int TempReturFL { get; set; } = 75;
    public double FactorVarmtVandsTillægFL { get; set; } = 1.0;
    public int NyttetimerOneUserFL { get; set; } = 2000;
    public int Nyttetimer50PlusUsersFL { get; set; } = 2800;
    public double AcceptVelocity20_150FL { get; set; } = 1.5;
    public double AcceptVelocity200_300FL { get; set; } = 2.5;
    public double AcceptVelocity300PlusFL { get; set; } = 3.0;
    public int AcceptPressureGradient20_150FL { get; set; } = 100;
    public int AcceptPressureGradient200_300FL { get; set; } = 100;
    public int AcceptPressureGradient300PlusFL { get; set; } = 120;
    public bool UsePertFlextraFL { get; set; } = true;
    public int PertFlextraMaxDnFL { get; set; } = 75; // Dropdown: 75, 63, 50, 40, 32, 25

    // Service Lines (SL)
    public int TempFremSL { get; set; } = 110;
    public int TempReturSL { get; set; } = 75;
    public double FactorVarmtVandsTillægSL { get; set; } = 1.0;
    public int NyttetimerOneUserSL { get; set; } = 2000;
    public string PipeTypeSL { get; set; } = "AluPEX"; // Dropdown: AluPEX, Kobber, Stål, PertFlextra
    public double AcceptVelocityFlexibleSL { get; set; } = 1.0;
    public double AcceptVelocity20_150SL { get; set; } = 1.5;
    public int AcceptPressureGradientFlexibleSL { get; set; } = 600;
    public int AcceptPressureGradient20_150SL { get; set; } = 600;
    public double MaxPressureLossStikSL { get; set; } = 0.3;

    // Implement INotifyPropertyChanged
    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // Implement other properties similarly...

    // Serialization Methods
    public void Save(string path)
    {
        var json = JsonConvert.SerializeObject(this, Formatting.Indented);
        System.IO.File.WriteAllText(path, json);
    }

    public static HydraulicSettings Load(string path)
    {
        if (!System.IO.File.Exists(path))
            return new HydraulicSettings(); // Return default settings

        var json = System.IO.File.ReadAllText(path);
        return JsonConvert.DeserializeObject<HydraulicSettings>(json);
    }
}
