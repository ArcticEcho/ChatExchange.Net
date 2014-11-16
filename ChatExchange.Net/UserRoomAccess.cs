using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ChatExchangeDotNet
{
	public enum UserRoomAccess
	{
		Normal,
		ExplicitReadOnly,
		ExplicitReadWrite,
		Owner
	}
}
