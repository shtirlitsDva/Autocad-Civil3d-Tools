//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows.Input;

//namespace DimensioneringV2.UI
//{
//    internal class RelayCommand : ICommand
//    {
//        readonly Action<object> execute;
//        readonly Predicate<object> canExecute;

//        /// <summary>
//        /// Creates a new instance of RelayCommand.
//        /// </summary>
//        /// <param name="execute">Action to execute.</param>
//        /// <param name="canExecute">Predicate indicating if the action can be executed.</param>
//        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
//        {
//            this.execute = execute;
//            this.canExecute = canExecute;
//        }

//        /// <summary>
//        /// Executes the action passed as parameter to the constructor.
//        /// </summary>
//        /// <param name="parameter">Action parameter (may be null).</param>
//        public void Execute(object parameter) => execute(parameter);

//        /// <summary>
//        /// Executes the predicate passed as parameter to the constructor.
//        /// </summary>
//        /// <param name="parameter">Predicate parameter (may be null).</param>
//        /// <returns>Result of the predictae execution.</returns>
//        public bool CanExecute(object parameter) => canExecute(parameter);

//        /// <summary>
//        /// Event indicating that the returned value of the predicte changed.
//        /// </summary>
//        public event EventHandler CanExecuteChanged
//        {
//            add { CommandManager.RequerySuggested += value; }
//            remove { CommandManager.RequerySuggested -= value; }
//        }
//    }
//}
