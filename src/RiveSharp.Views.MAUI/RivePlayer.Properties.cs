namespace RiveSharp.Views
{
    // XAML properies for RivePlayer.
    [ContentProperty(nameof(StateMachineInputs))]
    public partial class RivePlayer
    {
        // Filename of the .riv file to open. Can be a file path or a URL.
        public static readonly BindableProperty SourceProperty = BindableProperty.Create(
            nameof(Source),
            typeof(string),
            typeof(RivePlayer),
            null,
            propertyChanged: OnSourceNameChanged);

        public string Source
        {
            get => (string)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        // Name of the artbord to load from the .riv file. If null or empty, the default artboard
        // will be loaded.
        public static readonly BindableProperty ArtboardProperty = BindableProperty.Create(
            nameof(Artboard),
            typeof(string),
            typeof(RivePlayer),
            null,
            propertyChanged: OnArtboardNameChanged);

        public string Artboard
        {
            get => (string)GetValue(ArtboardProperty);
            set => SetValue(ArtboardProperty, value);
        }

        // Name of the state machine to load from the .riv file.
        public static readonly BindableProperty StateMachineProperty = BindableProperty.Create(
            nameof(StateMachine),
            typeof(string),
            typeof(RivePlayer),
            null,
            propertyChanged: OnStateMachineNameChanged);

        public string StateMachine
        {
            get => (string)GetValue(StateMachineProperty);
            set => SetValue(StateMachineProperty, value);
        }

        // Name of the fallback animation to load from the .riv if StateMachine is null or empty.
        public static readonly BindableProperty AnimationProperty = BindableProperty.Create(
            nameof(Animation),
            typeof(string),
            typeof(RivePlayer),
            null,
            propertyChanged: OnAnimationNameChanged);

        public string Animation
        {
            get => (string)GetValue(AnimationProperty);
            set => SetValue(AnimationProperty, value);
        }

        public static readonly BindableProperty StateMachineInputsProperty = BindableProperty.Create(
            nameof(StateMachineInputs),
            typeof(StateMachineInputCollection),
            typeof(RivePlayer),
            null
        );

        public StateMachineInputCollection StateMachineInputs
        {
            get => (StateMachineInputCollection)GetValue(StateMachineInputsProperty);
            set => SetValue(StateMachineInputsProperty, value);
        }

        static void OnSourceNameChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var player = (RivePlayer)bindable;
            var newSourceName = (string)newValue;
            // Clear the current Scene while we wait for the new one to load.
            player.sceneActionsQueue.Enqueue(() => player._scene = new Scene());
            player._activeSourceFileLoader?.Cancel();

            player._activeSourceFileLoader = new CancellationTokenSource();
            // Defer state machine inputs here until the new file is loaded.
            player._deferredSMInputsDuringFileLoad = new List<Action>();
            player.LoadSourceFileDataAsync(newSourceName, player._activeSourceFileLoader.Token);
        }

        static void OnArtboardNameChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var player = (RivePlayer)bindable;
            var newArtboardName = (string)newValue;
            player.sceneActionsQueue.Enqueue(() => player._artboardName = newArtboardName);
            if (player._activeSourceFileLoader != null)
            {
                // If a file is currently loading async, it will apply the new artboard once
                // it completes. Loading a new artboard also invalidates any state machine
                // inputs that were waiting for the file load.
                player._deferredSMInputsDuringFileLoad.Clear();
            }
            else
            {
                player.sceneActionsQueue.Enqueue(() => player.UpdateScene(SceneUpdates.Artboard));
            }
        }

        static void OnStateMachineNameChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var player = (RivePlayer)bindable;
            var newStateMachineName = (string)newValue;
            player.sceneActionsQueue.Enqueue(() => player._stateMachineName = newStateMachineName);
            if (player._activeSourceFileLoader != null)
            {
                // If a file is currently loading async, it will apply the new state machine
                // once it completes. Loading a new state machine also invalidates any state
                // machine inputs that were waiting for the file load.
                player._deferredSMInputsDuringFileLoad.Clear();
            }
            else
            {
                player.sceneActionsQueue.Enqueue(() => player.UpdateScene(SceneUpdates.AnimationOrStateMachine));
            }
        }

        static void OnAnimationNameChanged(BindableObject bindable, object oldValue, object newValue)
        {
            var player = (RivePlayer)bindable;
            var newAnimationName = (string)newValue;
            player.sceneActionsQueue.Enqueue(() => player._animationName = newAnimationName);
            // If a file is currently loading async, it will apply the new animation once it completes.
            if (player._activeSourceFileLoader == null)
            {
                player.sceneActionsQueue.Enqueue(() => player.UpdateScene(SceneUpdates.AnimationOrStateMachine));
            }
        }
    }
}