using System.ServiceModel;

namespace Service
{
    [ServiceBehavior(AddressFilterMode = AddressFilterMode.Any)]
    public class Service : IService
    {
        public int Add(AddRequest req)
        {
            return req.FirstNumber + req.SecondNumber;
        }

        public string Test(string param)
        {
            return param;
        }
    }
}
