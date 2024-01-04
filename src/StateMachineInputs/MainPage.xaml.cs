namespace StateMachineInputs
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        void OnFailureClicked(object sender, EventArgs args)
        {
            TrigFail.Fire();
        }

        void OnSuccessClicked(object sender, EventArgs args)
        {
            TrigSuccess.Fire();
        }
    }
}