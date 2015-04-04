using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace ChatExchangeDotNet
{
    internal class ChatAction
    {
        public Delegate Action { get; private set; }
        public ActionType Type { get; private set; }



        public ChatAction(ActionType type, Delegate action)
        {
            Action = action;
            Type = type;
        }
    }
}
