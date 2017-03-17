using System;
using Xunit;

namespace ChatExchange.Net.Tests
{
    public class ExtensionsTests
    {
        [Fact]
        public void IsReply_FalseWithNullMessage() => IsReply(false, null);

        [Fact]
        public void IsReply_FalseWithEmptyMessage() => IsReply(false, "");

        [Fact]
        public void IsReply_FalseWithNoColon() => IsReply(false, "this is a message");

        [Fact]
        public void IsReply_FalseColonStartNoNumbers() => IsReply(false, ":abc yes no");

        [Fact]
        public void IsReply_FalseColonNumberInMiddleOfMessage() => IsReply(false, "this is :123 a message");

        [Fact]
        public void IsReply_TrueColonNumberAtStart() => IsReply(true, ":123 a message");

        [Fact]
        public void IsReply_FalseOnlyColonNumber() => IsReply(false, ":4343");

        [Fact]
        public void IsReply_FalseOnlyColonNumberSpace() => IsReply(false, ":4343 ");

        private void IsReply(bool expected, string message)
        {
            var actual = ChatExchangeDotNet.Extensions.IsReply(message);
            Assert.Equal(expected, actual);
        }
    }
}
