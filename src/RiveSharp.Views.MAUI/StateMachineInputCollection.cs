#nullable disable
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace RiveSharp.Views
{
    // Manages a collection of StateMachineInput objects for RivePlayer. The [ContentProperty] tag
    // on RivePlayer instructs the XAML engine automatically route nested inputs through this
    // collection:
    //
    //   <rive:RivePlayer Source="...">
    //       <rive:BoolInput Target=... />
    //   </rive:RivePlayer>
    //
    public class StateMachineInputCollection : ObservableCollection<BindableObject>
    {
        readonly WeakReference<RivePlayer> rivePlayer;

        public StateMachineInputCollection(RivePlayer rivePlayer)
        {
            this.rivePlayer = new WeakReference<RivePlayer>(rivePlayer);
            CollectionChanged += InputsVectorChanged;
        }

        void InputsVectorChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Replace:
                    {
                        var input = ((ObservableCollection<BindableObject>)sender)[e.NewStartingIndex] as StateMachineInput;
                        input?.SetRivePlayer(rivePlayer);
                    }
                    break;
                case NotifyCollectionChangedAction.Remove:
                    {
                        var input = ((ObservableCollection<BindableObject>)sender)[e.NewStartingIndex] as StateMachineInput;
                        input?.SetRivePlayer(new WeakReference<RivePlayer>(null));
                        break;
                    }
                case NotifyCollectionChangedAction.Reset:
                    foreach (StateMachineInput input in sender as ObservableCollection<BindableObject>)
                    {
                        input.SetRivePlayer(new WeakReference<RivePlayer>(null));
                    }
                    break;
            }
        }
    }
}
