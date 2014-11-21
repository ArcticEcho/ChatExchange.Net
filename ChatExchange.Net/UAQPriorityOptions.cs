using System.Collections.Generic;



namespace ChatExchangeDotNet
{
	public class UAQPriorityOptions
	{
		/// <summary>
		/// The priority to use when processing the UserActions.
		/// </summary>
		public UAQPriority Priority { get; set; }

		/// <summary>
		/// The sub-priority to use when processing UserActions based on their Type (i.e., if UserActionQueuePriority is set to ActionType). 
		/// </summary>
		public List<UserActionType> TypePriorityOrder { get; set; }



		public UAQPriorityOptions()
		{
			TypePriorityOrder = new List<UserActionType>();
		}
	}
}
