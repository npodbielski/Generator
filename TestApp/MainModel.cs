namespace TestApp
{
    public class MainModel
    {
        public virtual int IntValue { get; set; }

        public void Increment()
        {
            IntValue++;
        }
    }
}
