namespace ChatExchangeDotNet
{
	public enum UAQPriority
	{
		/// <summary>
		/// Queue order will be maintained.
		/// </summary>
		Order,

		/// <summary>
		/// Processing speed will be prioritised (queue oerder is not maintained).
		/// </summary>
		Throughput,

		/// <summary>
		/// Queue priorty is based on the UserActions' Type.
		/// </summary>
		ActionType
	}
}
