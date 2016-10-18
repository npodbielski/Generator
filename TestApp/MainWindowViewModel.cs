using System.Windows.Input;
using Generator;

namespace TestApp
{
    public class MainWindowViewModel
    {
        private ICommand _increment;

        public MainWindowViewModel()
        {
            var test = GenerateProxy.ProxyOf<MainModel>();
            Model = test;
            test.IntValue = 2;
        }

        public ICommand Increment => _increment ?? (_increment = new Command(OnIncrement));

        public MainModel Model { get; set; }

        private void OnIncrement()
        {
            Model.Increment();
        }
    }
}