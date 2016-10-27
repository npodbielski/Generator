using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Generator;
using Newtonsoft.Json;
using Service;

namespace TestApp
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private ICommand _callEcho;
        private ICommand _callService;
        private string _echo;
        private ICommand _increment;
        private int _sum;

        public MainWindowViewModel()
        {
            var test = ProxyGenerator.PropertyChangedProxy<MainModel>();
            Model = test;
            test.IntValue = 2;
        }

        public ICommand CallEcho => _callEcho ?? (_callEcho = new Command(OnCallEcho));
        public ICommand CallService => _callService ?? (_callService = new Command(OnCallService2));

        public string Echo
        {
            get { return _echo; }
            set
            {
                _echo = value;
                OnPropertyChanged();
            }
        }

        public string EchoParam { get; set; }

        public int First { get; set; }

        public ICommand Increment => _increment ?? (_increment = new Command(OnIncrement));

        public MainModel Model { get; set; }

        public int Second { get; set; }

        public int Sum
        {
            get { return _sum; }
            set
            {
                _sum = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnCallEcho()
        {
            var serviceProxy = ProxyGenerator.ServiceProxy<IService>("http://localhost:8081");
            Echo = serviceProxy.Test(EchoParam);
        }

        private void OnCallService()
        {
            using (var client = new HttpClient(new HttpClientHandler()))
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var addRequest = new AddRequest
                {
                    FirstNumber = First,
                    SecondNumber = Second
                };
                var serializeObject = JsonConvert.SerializeObject(addRequest);
                var call = client.PostAsync("http://localhost:8081/add",
                    new StringContent(serializeObject, Encoding.UTF8, "application/json"));

                try
                {
                    call.Wait();
                    var result = call.Result.Content;
                    Sum = (int)JsonConvert.DeserializeObject(result.ReadAsStringAsync().Result, typeof(int));
                }
                catch (Exception)
                {
                }
            }
        }

        private void OnCallService2()
        {
            var serviceProxy = ProxyGenerator.ServiceProxy<IService>("http://localhost:8081");
            Sum = serviceProxy.Add(new AddRequest
            {
                FirstNumber = First,
                SecondNumber = Second
            });
        }

        private void OnIncrement()
        {
            Model.Increment();
        }
    }
}