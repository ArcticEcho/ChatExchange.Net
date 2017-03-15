using System;
using Xunit;

namespace ChatExchange.Net.Tests
{
    public class ExtensionsTests
    {
        public void IsReply_FalseWithNullMessage() => IsReply(false, null);

        public void IsReply_FalseWithEmptyMessage() => IsReply(false, "");

        public void IsReply_FalseWithNoColon() => IsReply(false, "this is a message");

        public void IsReply_FalseColonStartNoNumbers() => IsReply(false, ":abc yes no");

        public void IsReply_FalseColonNumberInMiddleOfMessage() => IsReply(false, "this is :123 a message");

        public void IsReply_TrueColonNumberAtStart() => IsReply(true, ":123 a message");

        public void IsReply_FalseOnlyColonNumber() => IsReply(false, ":4343");

        public void IsReply_FalseOnlyColonNumberSpace() => IsReply(false, ":4343 ");

        private void IsReply(bool expected, string message)
        {
            var actual = ChatExchangeDotNet.Extensions.IsReply(message);
            Assert.Equal(expected, actual);
        }
    }
}
