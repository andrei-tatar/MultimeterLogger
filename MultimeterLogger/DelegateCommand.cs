using System;
using System.Windows.Input;

namespace MultimeterLogger
{
    class DelegateCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;

        public DelegateCommand(Action<T> execute, Func<T,bool> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return  _canExecute == null || !(parameter is T) || _canExecute((T) parameter);
        }

        public void Execute(object parameter)
        {
            if ((parameter == null && !typeof(T).IsGenericType) || parameter is T)
                _execute((T) parameter);
        }

        public event EventHandler CanExecuteChanged;
    }
}