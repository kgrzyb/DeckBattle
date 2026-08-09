namespace DeckBattle
{
    // Semantic presentation cues. Gameplay emits battle events; presenters map them to these cues.
    public enum BattleVfxCue
    {
        None = 0,
        AttackWindup = 1,
        AttackFired = 2,
        AttackImpact = 3,
        Damaged = 4,
        CriticalImpact = 5,
        SpecialWindup = 6,
        SpecialCast = 7,
        SpecialStrike = 8,
        ProjectileLaunch = 9,
        ProjectileImpact = 10,
        Death = 11
    }
}
