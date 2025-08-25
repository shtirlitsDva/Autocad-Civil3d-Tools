using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Models;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.Core.Models;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts
{
	// Generic analyzer contract
	internal interface IAnalyzer<TInput, TResult>
	{
		TResult Analyze(TInput input);
		void Commit(TResult result);
	}

	internal interface ISceneComposer<TAnalysis>
	{
		Rendering.Scene Compose(TAnalysis analysis, Autodesk.AutoCAD.DatabaseServices.Line workingLine);
	}

	internal interface IInspectorMapper<TAnalysis, TModel>
	{
		TModel Map(TAnalysis analysis, Autodesk.AutoCAD.DatabaseServices.Line workingLine);
	}
	
	// Specific contract for Vejkant visualization
	internal interface IVejkantInspectorMapper : IInspectorMapper<VejkantAnalysis, IntersectionVisualizationModel>
	{
	}
}



