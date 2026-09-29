namespace ThreeSouls.Interface
{
    public interface IPopupElement
    {
        bool IsShowing { get; }
        void OnEscapeEvent();
    }

}