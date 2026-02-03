using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace NorsynHydraulicTester.Views.Controls;

public partial class FormulaDisplayControl : UserControl
{
    public FormulaDisplayControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is NorsynHydraulicTester.Models.CalculationStep step)
        {
            Debug.WriteLine($"[FormulaDisplay] Step: {step.Name}");
            Debug.WriteLine($"[FormulaDisplay] FormulaWithValues length: {step.FormulaWithValues?.Length ?? 0}");
            Debug.WriteLine($"[FormulaDisplay] FormulaWithValues: {step.FormulaWithValues}");

            if (BeregningFormula != null)
            {
                Debug.WriteLine($"[FormulaDisplay] HasError: {BeregningFormula.HasError}");
                if (BeregningFormula.HasError)
                {
                    Debug.WriteLine($"[FormulaDisplay] Errors: {string.Join("; ", BeregningFormula.Errors)}");
                }
            }
        }
    }
}
