namespace Core.StateMachines
{
    /// <summary>
    /// Minimal, allocation-free finite state machine runner.
    /// </summary>
    /// <remarks>
    /// The owner decides when to call <see cref="Tick"/> (e.g. from <c>FixedUpdate</c> for physics-driven
    /// states). States request transitions themselves by calling <see cref="ChangeState"/>.
    /// </remarks>
    /// <typeparam name="TState">State family this machine runs, e.g. <c>IEnemyState</c>.</typeparam>
    public sealed class StateMachine<TState> where TState : class, IState
    {
        /// <summary>The active state, or null when stopped.</summary>
        public TState CurrentState { get; private set; }

        /// <summary>The state that was active before the most recent transition.</summary>
        public TState PreviousState { get; private set; }

        /// <summary>True while a state is active.</summary>
        public bool IsRunning => CurrentState != null;

        /// <summary>
        /// Exits the current state (if any) and enters <paramref name="next"/>.
        /// Changing to the state that is already current does nothing.
        /// </summary>
        /// <param name="next">State to enter. Passing null is equivalent to <see cref="Stop"/>.</param>
        public void ChangeState(TState next)
        {
            if (ReferenceEquals(next, CurrentState))
            {
                return;
            }

            CurrentState?.Exit();
            PreviousState = CurrentState;
            CurrentState = next;
            CurrentState?.Enter();
        }

        /// <summary>Runs one update of the current state.</summary>
        public void Tick()
        {
            CurrentState?.Update();
        }

        /// <summary>Exits the current state and leaves the machine idle.</summary>
        public void Stop()
        {
            ChangeState(null);
        }
    }
}
