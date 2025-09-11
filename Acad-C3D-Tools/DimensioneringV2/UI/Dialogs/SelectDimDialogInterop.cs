using System;
using System.Windows;
using NorsynHydraulicCalc.Pipes;

namespace DimensioneringV2.UI
{
	internal static class SelectDimDialogInterop
	{
		public static Dim? ShowSelectDimDialog()
		{
			var dlg = new Dialogs.SelectPipeDimDialog();
			var vm = new Dialogs.SelectPipeDimViewModel();
			dlg.DataContext = vm;
			Dim? result = null;
			vm.RequestClose += ok =>
			{
				if (ok) result = vm.SelectedDim;
				dlg.DialogResult = ok;
				dlg.Close();
			};
			dlg.Owner = Application.Current?.MainWindow;
			dlg.ShowDialog();
			return result;
		}
	}
}


