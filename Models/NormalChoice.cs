using DragonTools.Interfaces;

namespace DragonTools.Models;

public class NormalChoice(string name) : IChoice
{
    public string Name { get; set; } = name;
    
    public override string ToString() => Name;
    
    public override bool Equals(object? obj)
    {
        if (obj is not NormalChoice other) return false;
        return Name == other.Name;
    }

    protected bool Equals(NormalChoice other)
    {
        return Name == other.Name;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name);
    }
}