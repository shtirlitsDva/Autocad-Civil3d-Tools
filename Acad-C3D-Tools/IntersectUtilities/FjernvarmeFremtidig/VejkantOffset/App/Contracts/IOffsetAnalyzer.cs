namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts
{
	// Generic analyzer contract
	public interface IAnalyzer<TInput, TResult>
	{
		TResult Analyze(TInput input);
		void Commit(TResult result);
	}

	public interface ISceneComposer<TAnalysis>
	{
		Rendering.Scene Compose(TAnalysis analysis, Autodesk.AutoCAD.DatabaseServices.Line workingLine);
	}

	public interface IInspectorMapper<TAnalysis, TModel>
	{
		TModel Map(TAnalysis analysis, Autodesk.AutoCAD.DatabaseServices.Line workingLine);
	}
}



