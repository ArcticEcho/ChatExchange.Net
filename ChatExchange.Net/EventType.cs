using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



namespace ChatExchangeDotNet
{
	[Flags]
	enum EventType
	{
		None = 0,
		MessagePosted = 1,
		MessageEdited = 2,
		UserEntered = 3,
		UserLeft = 4,
		RoomNameChanged = 5,
		MessageStarred = 6,
		DebugMessage = 7,
		UserMentioned = 8,
		MessageFlagged = 9,
		MessageDeleted = 10,
		FileAdded = 11,
		ModeratorFlag = 12,
		UserSettingsChanged = 13,
		GlobalNotification = 14,
		AccessLevelChanged = 15,
		UserNotification = 16,
		Invitation = 17,
		MessageReply = 18,
		MessageMovedOut = 19,
		MessageMovedIn = 20,
		TimeBreak = 21,
		FeedTicker = 22,
		UserSuspended = 29,
		UserMerged = 30
	}
}
