namespace TestApp
{
    public class MainModel
    {
        private int _intValue;

        public virtual int IntValue
        {
            get { return _intValue; }
            set { _intValue = value; }
        }

        public void Increment()
        {
            IntValue++;
        }
    }
}
