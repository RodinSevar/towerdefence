public enum StatusEffectType
{
    Slow,
    Poison,
    Stun
}

public class StatusEffect
{
    public StatusEffectType type;
    public float duration;
    public float strength;
    
    public StatusEffect(StatusEffectType type, float duration, float strength)
    {
        this.type = type;
        this.duration = duration;
        this.strength = strength;
    }
}
