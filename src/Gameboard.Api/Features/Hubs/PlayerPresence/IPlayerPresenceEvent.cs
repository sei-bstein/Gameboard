using System.Threading.Tasks;

namespace Gameboard.Api.Hubs.PlayerPresence;

internal interface IPlayerPresenceEvent
{
    Task PlayerJoined(PlayerPresencePlayer player);
    Task PlayerLeft(PlayerPresencePlayer player);
}