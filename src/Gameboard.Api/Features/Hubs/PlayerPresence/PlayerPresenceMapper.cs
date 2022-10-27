using AutoMapper;

namespace Gameboard.Api.Hubs.PlayerPresence;

internal class PlayerPresenceMapper : Profile
{
    public PlayerPresenceMapper()
    {
        CreateMap<Player, PlayerPresencePlayer>()
            .ForMember(p3 => p3.OrignalName, opt => opt.MapFrom(p => p.Name))
            .ForMember(p3 => p3.SponsorLogo, opt => opt.MapFrom(p => p.Sponsor));
    }
}