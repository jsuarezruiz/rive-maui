#nullable disable
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;
using System.Collections.Concurrent;
using System.Net;

namespace RiveSharp.Views
{
    // Implements a simple view that plays content from a .riv file.
    public partial class RivePlayer : SKCanvasView
    {
        CancellationTokenSource _activeSourceFileLoader = null;

        public RivePlayer()
        {
            EnableTouchEvents = true;
            StateMachineInputs = new StateMachineInputCollection(this);

            Loaded += OnLoaded;
            PaintSurface += OnPaintSurface;
        }

        async void LoadSourceFileDataAsync(string name, CancellationToken cancellationToken)
        {
            byte[] data = null;

            if (Uri.TryCreate(name, UriKind.Absolute, out var uri))
            {
                // TODO: Replace with HttpClient
                var client = new WebClient();
                data = await client.DownloadDataTaskAsync(uri);
            }
            else
            {
                var fileStream = await FileSystem.OpenAppPackageFileAsync(name);
                data = new byte[fileStream.Length];
                fileStream.Read(data, 0, data.Length);
                fileStream.Dispose();  // Don't keep the file open.
            }

            if (data != null && !cancellationToken.IsCancellationRequested)
            {
                sceneActionsQueue.Enqueue(() => UpdateScene(SceneUpdates.File, data));
                // Apply deferred state machine inputs once the scene is fully loaded.
                foreach (Action stateMachineInput in _deferredSMInputsDuringFileLoad)
                {
                    sceneActionsQueue.Enqueue(stateMachineInput);
                }
            }

            _deferredSMInputsDuringFileLoad = null;
            _activeSourceFileLoader = null;
        }

        // State machine inputs to set once the current async file load finishes.
        List<Action> _deferredSMInputsDuringFileLoad = null;

        void EnqueueStateMachineInput(Action stateMachineInput)
        {
            if (_deferredSMInputsDuringFileLoad != null)
            {
                // A source file is currently loading async. Don't set this input until it completes.
                _deferredSMInputsDuringFileLoad.Add(stateMachineInput);
            }
            else
            {
                sceneActionsQueue.Enqueue(stateMachineInput);
            }
        }

        public void SetBool(string name, bool value)
        {
            EnqueueStateMachineInput(() => _scene.SetBool(name, value));
        }

        public void SetNumber(string name, float value)
        {
            EnqueueStateMachineInput(() => _scene.SetNumber(name, value));
        }

        public void FireTrigger(string name)
        {
            EnqueueStateMachineInput(() => _scene.FireTrigger(name));
        }

        delegate void PointerHandler(Vec2D pos);

        protected override void OnTouch(SKTouchEventArgs e)
        {
            base.OnTouch(e);

            if(e.ActionType == SKTouchAction.Pressed)
            {
                HandlePointerEvent(_scene.PointerDown, e);
            }
            else if(e.ActionType == SKTouchAction.Moved)
            {
                HandlePointerEvent(_scene.PointerMove, e);
            }
            else if (e.ActionType == SKTouchAction.Released)
            {
                HandlePointerEvent(_scene.PointerUp, e);
            }
        }

        void HandlePointerEvent(PointerHandler handler, SKTouchEventArgs e)
        {
            if (_activeSourceFileLoader != null)
            {
                // Ignore pointer events while a new scene is loading.
                return;
            }

            // Capture the viewSize and pointerPos at the time of the event.
            var viewSize = CanvasSize;
            var pointerPos = e.Location;

            // Forward the pointer event to the render thread.
            sceneActionsQueue.Enqueue(() =>
            {
                Mat2D mat = ComputeAlignment(viewSize.Width, viewSize.Height);
                if (mat.Invert(out var inverse))
                {
                    Vec2D artboardPos = inverse * new Vec2D((float)pointerPos.X, (float)pointerPos.Y);
                    handler(artboardPos);
                }
            });
        }

        // Incremented when the "InvalLoop" (responsible for scheduling PaintSurface events) should
        // terminate.
        int _invalLoopContinuationToken = 0;

        void OnLoaded(object sender, EventArgs e)
        {
            var view = (View)sender;
            InvalLoopAsync(view);

            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == IsVisibleProperty.PropertyName)
                {
                    InvalLoopAsync(view);
                }
            };
        }

        void InvalLoopAsync(View view)
        {
            ++_invalLoopContinuationToken;  // Terminate the existing inval loop (if any).
            if (view.IsVisible)
            {
                InvalLoopAsync(_invalLoopContinuationToken);
            }
        }

        // Schedules continual PaintSurface events until the window is no longer visible.
        // (Multiple calls to Invalidate() between PaintSurface events are coalesced.)
        async void InvalLoopAsync(int continuationToken)
        {
            while (continuationToken == _invalLoopContinuationToken)
            {
                InvalidateSurface();
                await Task.Delay(TimeSpan.FromMilliseconds(48));  // TODO: 120 fps
            }
        }

        // _scene is used on the render thread exclusively.
        Scene _scene = new Scene();

        // Source actions originating from other threads must be funneled through this queue.
        readonly ConcurrentQueue<Action> sceneActionsQueue = new ConcurrentQueue<Action>();

        // This is the render-thread copy of the animation parameters. They are set via
        // _sceneActionsQueue. _scene is then blah blah blah
        string _artboardName;
        string _animationName;
        string _stateMachineName;

        enum SceneUpdates
        {
            File = 3,
            Artboard = 2,
            AnimationOrStateMachine = 1,
        };

        DateTime? _lastPaintTime;

        void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
        {
            // Handle pending scene actions from the main thread.
            while (sceneActionsQueue.TryDequeue(out var action))
            {
                action();
            }

            if (!_scene.IsLoaded)
            {
                return;
            }

            // Run the animation.
            var now = DateTime.Now;
            if (_lastPaintTime is not null)
            {
                _scene.AdvanceAndApply((now - _lastPaintTime).Value.TotalSeconds);
            }
            _lastPaintTime = now;

            // Render.
            e.Surface.Canvas.Clear();
            var renderer = new Renderer(e.Surface.Canvas);
            renderer.Save();
            renderer.Transform(ComputeAlignment(e.Info.Width, e.Info.Height));
            _scene.Draw(renderer);
            renderer.Restore();
        }

        // Called from the render thread. Updates _scene according to updates.
        void UpdateScene(SceneUpdates updates, byte[] sourceFileData = null)
        {
            if (updates >= SceneUpdates.File)
            {
                _scene.LoadFile(sourceFileData);
            }
            if (updates >= SceneUpdates.Artboard)
            {
                _scene.LoadArtboard(_artboardName);
            }
            if (updates >= SceneUpdates.AnimationOrStateMachine)
            {
                if (!String.IsNullOrEmpty(_stateMachineName))
                {
                    _scene.LoadStateMachine(_stateMachineName);
                }
                else if (!String.IsNullOrEmpty(_animationName))
                {
                    _scene.LoadAnimation(_animationName);
                }
                else
                {
                    if (!_scene.LoadStateMachine(null))
                    {
                        _scene.LoadAnimation(null);
                    }
                }
            }
        }

        // Called from the render thread. Computes alignment based on the size of _scene.
        Mat2D ComputeAlignment(double width, double height)
        {
            return ComputeAlignment(new AABB(0, 0, (float)width, (float)height));
        }

        // Called from the render thread. Computes alignment based on the size of _scene.
        Mat2D ComputeAlignment(AABB frame)
        {
            return Renderer.ComputeAlignment(Fit.Contain, Alignment.Center, frame,              
                new AABB(0, 0, _scene.Width, _scene.Height));
        }
    }
}