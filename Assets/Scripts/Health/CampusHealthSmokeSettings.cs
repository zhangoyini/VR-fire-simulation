namespace Jiao.HealthTime
{
    // Shared by component defaults and Editor scene writers. Tests keep independent expectations.
    public static class CampusHealthSmokeSettings
    {
        public const float MaxHealth = 100f;
        public const float SmokeBaseDrain = .1f;
        public const float IdleDrain = .02f;
        public const float SmokeDamage = 1f;
        public const float GraceSeconds = 3f;
        public const float CoveredMultiplier = .2f;
        public const float ProtectionSettleSeconds = .25f;
    }
}
