using System;
using Generator;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Newtonsoft.Json;
using Service;

namespace TestApp
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private ICommand _callService;
        private ICommand _increment;
        private int _sum;

        public MainWindowViewModel()
        {
            var test = GenerateProxy.ProxyOf<MainModel>();
            Model = test;
            test.IntValue = 2;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand CallService => _callService ?? (_callService = new Command(OnCallService));
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

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnCallService()
        {
            using (var client = new HttpClient(new HttpClientHandler()))
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //client
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
                }
                catch (System.Exception)
                {
                    throw;
                }
                var result = call.Result.Content;
                Sum = Convert.ToInt32(JsonConvert.DeserializeObject(result.ReadAsStringAsync().Result));
            }
        }

        private void OnIncrement()
        {
            Model.Increment();
        }
    }
}