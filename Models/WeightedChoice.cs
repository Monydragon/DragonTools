public class Choices(string  name, int weight)
{
    public string Name { get; set; } = name;
    public int Weight { get; set; } = weight;
    
    public override string ToString() => $"{Name} ({Weight})";
    
    
}