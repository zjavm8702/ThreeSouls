using ThreeSouls.Enumeration;

namespace ThreeSouls.Interface
{
    public interface IUIAnimation
    {
        IUIAnimation OnAnimationReady(System.Action onReady);

        IUIAnimation OnAnimationDone(System.Action onDone);

        void Play();

        void Release();
    }
}