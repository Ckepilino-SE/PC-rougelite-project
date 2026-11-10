namespace Gameplay.Enemies
{
    /// <summary>
    /// Put a component implementing this on an enemy to replace its built-in melee/ranged attack with a custom
    /// state (e.g. a boss's pattern attacks). <see cref="EnemyController"/> asks for the state once in <c>Awake</c>.
    /// </summary>
    public interface IEnemyAttackProvider
    {
        /// <summary>Creates the attack state <see cref="States.ChaseState"/> hands off to when in range.</summary>
        IEnemyState CreateAttackState(EnemyController enemy);
    }

    /// <summary>What a custom attack state is doing, for visuals and contact damage.</summary>
    public interface IEnemyAttackStatus
    {
        /// <summary>True while winding up an attack.</summary>
        bool IsTelegraphing { get; }

        /// <summary>True if the wind-up leads to a melee/charge attack (red) rather than shots (yellow).</summary>
        bool IsMeleeTelegraph { get; }

        /// <summary>True while touching the player should deal lunge damage (e.g. during a charge).</summary>
        bool IsContactDamageActive { get; }
    }
}
