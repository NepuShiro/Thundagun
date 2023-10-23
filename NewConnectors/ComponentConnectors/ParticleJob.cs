namespace Thundagun.NewConnectors.ComponentConnectors;

public readonly struct ParticleJob
{
    public readonly ParticleSystemBehavior behavior;
    public readonly int startIndex;
    public readonly int endIndex;

    public ParticleJob(ParticleSystemBehavior behavior, int startIndex, int endIndex)
    {
        this.behavior = behavior;
        this.startIndex = startIndex;
        this.endIndex = endIndex;
    }
}