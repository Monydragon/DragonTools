namespace DragonTools.Interfaces;

public interface IWeightedChoice : IChoice
{
    int Weight { get; set; }
}