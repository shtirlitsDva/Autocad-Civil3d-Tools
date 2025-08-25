namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts
{
	public interface IVisualizer<TModel>
	{
		void Show();
		void Update(TModel model);
		void Hide();
	}
}



