using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NorsynHydraulicCalc.Pipes;

namespace DimensioneringV2.UI.Dialogs
{
	internal partial class SelectPipeDimViewModel : ObservableObject
	{
		public ObservableCollection<string> PipeTypes { get; } = new();
		public ObservableCollection<Dim> PipeDims { get; } = new();

		[ObservableProperty]
		private string selectedPipeType;
		partial void OnSelectedPipeTypeChanged(string value)
		{
			LoadDimsForSelectedType();
		}

		[ObservableProperty]
		private Dim selectedDim;

		public SelectPipeDimViewModel()
		{
			PipeTypes.Add("Stål");
			PipeTypes.Add("AluPex");
			PipeTypes.Add("PertFlextra");
			PipeTypes.Add("Cu");
			SelectedPipeType = PipeTypes[0];
		}

		private void LoadDimsForSelectedType()
		{
			PipeDims.Clear();
			IEnumerable<Dim> dims = selectedPipeType switch
			{
				"Stål" => PipeTypesProvider().Stål.GetAllDimsSorted(),
				"AluPex" => PipeTypesProvider().AluPex.GetAllDimsSorted(),
				"PertFlextra" => PipeTypesProvider().PertFlextra.GetAllDimsSorted(),
				"Cu" => PipeTypesProvider().Cu.GetAllDimsSorted(),
				_ => System.Linq.Enumerable.Empty<Dim>()
			};
			foreach (var d in dims) PipeDims.Add(d);
			if (PipeDims.Count > 0) SelectedDim = PipeDims[0];
		}

		private static PipeTypes PipeTypesProvider()
		{
			// Default roughness via parameterless PipeTypes static holder
			return new PipeTypes();
		}

		public IRelayCommand OkCommand => new RelayCommand(() => Close(true));
		public IRelayCommand CancelCommand => new RelayCommand(() => Close(false));

		public event Action<bool> RequestClose;
		private void Close(bool ok)
		{
			RequestClose?.Invoke(ok);
		}
	}
}


