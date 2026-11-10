namespace Core.StateMachines
{
    /// <summary>
    /// A single state run by a <see cref="StateMachine{TState}"/>.
    /// </summary>
    /// <remarks>
    /// States are plain C# objects created once by their owner (e.g. in <c>Awake</c>) and reused for the
    /// owner's whole lifetime, so switching states never allocates.
    /// </remarks>
    public interface IState
    {
        /// <summary>Called once when the machine switches into this state.</summary>
        void Enter();

        /// <summary>Called every tick while this state is current.</summary>
        void Update();

        /// <summary>Called once when the machine switches out of this state, or is stopped.</summary>
        void Exit();
    }
}
