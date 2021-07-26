// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a MIT (SEI)-style license. See LICENSE.md in the project root for license information.

using System.Threading.Tasks;

namespace Gameboard.Api.Hubs
{
    public interface IAppHubEvent
    {
        Task GeneralEvent(string eventMessage);
        // Task DatumEvent(HubEvent<Datum> payload);
    }

    public interface IAppHubAction
    {

    }

}