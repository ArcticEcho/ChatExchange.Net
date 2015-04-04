using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace ChatExchangeDotNet
{
    public enum ActionType
    {
        PostMessage,
        EditMessage,
        ToggleMessageStar,
        DeleteMessage,
        ToggleMessagePin,
        KickMute,
        SetUserAccess,
        ClearMessageStars
    }
}
