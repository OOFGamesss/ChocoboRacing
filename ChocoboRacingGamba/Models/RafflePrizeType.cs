/// <summary>
/// The kind of prize awarded to the raffle winner: the gil pot, an in-game item, or a free-text custom prize.
/// </summary>
namespace ChocoboRacing.Models;

public enum RafflePrizeType
{
    Pot,
    Item,
    Custom
}
