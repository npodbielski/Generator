using System.ComponentModel;

namespace Generator
{
    public static class PropertyChangedInvoker
    {
        public static void Invoke(INotifyPropertyChanged sender,
            PropertyChangedEventHandler source, string propertyName)
        {
            source?.Invoke(sender, new PropertyChangedEventArgs(propertyName));
        }
    }
}