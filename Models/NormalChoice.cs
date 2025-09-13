using DragonTools.Interfaces;

namespace DragonTools.Models;

public class NormalChoice : IChoice
{
    public string Entry { get; set; } = "";
    public int Weight { get; set; } = 1;
    
    public NormalChoice() { }
    
    public NormalChoice(string entry)
    {
        Entry = entry;
    }
    
    public NormalChoice(string entry, int weight)
    {
        Entry = entry;
        Weight = weight;
    }
    public override string ToString() => Entry;
}