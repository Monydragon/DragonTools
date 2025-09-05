using DragonTools.Interfaces;

namespace DragonTools.Models;

public class WeightedChoice(string  name, int weight) : IWeightedChoice
{
    public string Name { get; set; } = name;
    public int Weight { get; set; } = weight;
    
    public override string ToString() => $"{Name} ({Weight})";
    
    public override bool Equals(object? obj)
    {
        if (obj is not WeightedChoice other) return false;
        return Name == other.Name && Weight == other.Weight;
    }

    protected bool Equals(WeightedChoice other)
    {
        return Name == other.Name && Weight == other.Weight;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Weight);
    }
}