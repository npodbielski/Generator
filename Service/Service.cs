namespace Service
{
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