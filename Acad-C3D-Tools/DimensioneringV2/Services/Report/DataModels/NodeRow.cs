namespace DimensioneringV2.Services.Report.DataModels;

/// <summary>
/// Flat data row for a node in the report (§7.5).
/// </summary>
internal record NodeRow(
    int NodeId,
    double X,
    double Y,
    bool IsRoot,
    bool IsBuilding,
    int Degree,
    double EffektKw,
    double PressureLossToNodeBar,
    double AvailableDifferentialPressureBar);
