using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;

namespace Service
{
    class Program
    {
        static void Main(string[] args)
        {
            var serviceHost = new ServiceHost(typeof(Service), new Uri("http://localhost:8081"));
            using (serviceHost)
            {
                var seb = new WebHttpBehavior
                {
                    DefaultOutgoingRequestFormat = WebMessageFormat.Json,
                    DefaultOutgoingResponseFormat = WebMessageFormat.Json,
                    FaultExceptionEnabled = true
                };
                serviceHost.Description.Behaviors.Add(new ServiceMetadataBehavior { HttpGetEnabled = true });
                var e = serviceHost.AddServiceEndpoint(typeof(IService), new WebHttpBinding(), "");
                e.Behaviors.Add(seb);
                serviceHost.Open();
                Console.WriteLine("Service ready...");
                Console.ReadLine();
                serviceHost.Close();
            }
        }
    }
}
