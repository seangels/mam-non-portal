using AdminPortal.Application.Common.Models;

namespace AdminPortal.Application.Common.Interfaces;

public interface ICurrentActor
{
    ActorContext GetRequired();
}
