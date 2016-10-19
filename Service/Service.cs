using System.ServiceModel;
using System.ServiceModel.Web;

namespace Service
{
    [ServiceContract]
    public interface IService
    {
        [OperationContract]
        int Add(AddRequest req);
    }

    public class AddRequest
    {
        public int FirstNumber { get; set; }
        public int SecondNumber { get; set; }
    }

    public class Service : IService
    {
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json,
            ResponseFormat = WebMessageFormat.Json)]
        public int Add(AddRequest req)
        {
            return req.FirstNumber + req.SecondNumber;
        }
    }
}
