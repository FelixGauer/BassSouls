public enum BassSignalType
{
    Started,
    Continued,
    Muted
}

public struct BassSignal
{
    public float frequency;
    public BassSignalType signalType;

    public BassSignal(float frequency, BassSignalType signalType)
    {
        this.frequency = frequency;
        this.signalType = signalType;
    }
}