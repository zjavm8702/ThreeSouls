namespace ThreeSouls.Interface
{
    public interface IEventValue { }

    public interface IEventListenerBase { }
    
    public interface IEventListener<in T> : IEventListenerBase
    {
        void OnEvent<T1>(T1 sender, T eventType) where T1 : class;
        int CallbackOrder { get; }
    }
}