using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace Service
{
    class Program
    {
        static void Main(string[] args)
        {
            var uri = new Uri("http://localhost:8081/service");
            var serviceHost = new ServiceHost(typeof(Service), uri);
            using (serviceHost)
            {
                var smb = new ServiceMetadataBehavior
                {
                    HttpGetEnabled = true,
                    MetadataExporter = {PolicyVersion = PolicyVersion.Policy15}
                };
                serviceHost.Description.Behaviors.Add(smb);
                serviceHost.Open();
                Console.WriteLine("Service ready...");
                Console.ReadLine();
                serviceHost.Close();
            }
        }
    }
}
