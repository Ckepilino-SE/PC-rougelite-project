using Core.StateMachines;

namespace Gameplay.Enemies
{
    /// <summary>
    /// A state in an enemy's finite state machine. Inherits <c>Enter()</c>, <c>Update()</c> and <c>Exit()</c>
    /// from <see cref="IState"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="EnemyController"/> ticks its machine from <c>FixedUpdate</c>, so <c>Update()</c> runs at the
    /// physics rate and <c>Time.deltaTime</c> inside it equals <c>Time.fixedDeltaTime</c>. Drive movement
    /// through the enemy's Rigidbody2D, never the transform.
    /// </remarks>
    public interface IEnemyState : IState
    {
    }
}
