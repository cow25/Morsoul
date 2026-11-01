namespace Morsoul.Data
{
    public enum ClueType { Physical, Testimony, RedHerring, Latent }

    public enum AcquisitionMethod
    {
        Raycast3D,
        GolemEyeScan,
        Dialogue2D,
        Inspection2D,
        EmotionEcho,
        AetherSight,
        AetherSightPlusGolemEye,
        Automatic
    }

    public enum Tool { None, GolemEye, EmotionEcho, AetherSight }

    public enum Verb { Press, Contradict, PresentEvidence }

    public enum Aura { None, Amber, Violet, Crimson }

    public enum Grade { Ungraded, Clean, Standard, Flawed }
}
