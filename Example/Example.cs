/*
 * ChatExchange.Net. A .Net (4.0) API for interacting with Stack Exchange chat.
 * Copyright © 2015, ArcticEcho.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */





using System;
using System.Threading;
using ChatExchangeDotNet;

// This is a small example program to demonstrate 
// some of this library's basic functions. See our
// wiki for more detailed information:
// https://github.com/ArcticEcho/ChatExchange.Net/wiki

namespace Example
{
    public class Example
    {
        static void Main()
        {
            Console.WriteLine("This is a ChatExchange.Net demonstration. Press the 'Q' key to exit...\n\n");

            // Create a client to authenticate the user (which will then allow us to interact with chat).
            var client = new Client("some-email@address.com", "MySuperStr0ngPa55word");

            // Join a room by specifying its URL (returns a Room object).
            var sandbox = client.JoinRoom("https://chat.stackoverflow.com/rooms/68414/socvr-testing-facility", true);

            // Post a new message in the room (if successful, returns
            // a Message object, otherwise returns null).
            // (If you have no use of the returned Message object,
            // I'd recommend using .PostMessageLight() instead.)
            var myMessage = sandbox.PostMessage("Hello world!");

            // Listen to the InternalException event for any exceptions that may arise during execution.
            // See our wiki for examples of other events
            // (https://github.com/ArcticEcho/ChatExchange.Net/wiki/Hooking-up-chat-events).
            sandbox.EventManager.ConnectListener(EventType.InternalException, new Action<Exception>(ex => Console.WriteLine("[ERROR] " + ex)));

            // Listen to the MessagePosted event for new messages.
            sandbox.EventManager.ConnectListener(EventType.MessagePosted, new Action<Message>(message =>
            {
                // Print the new message (with the author's name).
                Console.WriteLine(message.Author.Name + ": " + message.Content);

                // If the message contains "3... 2... 1...", post "KA-BOOM!"
                // (this is simply an [awful] example).
                if (message.Content.Contains("3... 2... 1..."))
                {
                    // Create a new MessageBuilder to format our message.
                    var msgBuilder = new MessageBuilder();

                    // Append the text "KA-BOOM!" (formatted in bold).
                    msgBuilder.AppendText("KA-BOOM!", TextFormattingOptions.Bold);

                    // Finally post the formatted message.
                    // (PostMessage() internally calls the message's ToString() method.)
                    var success = sandbox.PostMessage(msgBuilder) != null;

                    Console.WriteLine("'KA-BOOM' message successfully posted: " + success);
                }
            }));

            // Listen to the UserEntered event and post a welcome message when the event is fired.
            sandbox.EventManager.ConnectListener(EventType.UserEntered, new Action<User>(user =>
            {
                var success = sandbox.PostMessage("Hello " + user + "!") != null;

                Console.WriteLine("'Welcome' message successfully posted: " + success);
            }));

            // Wait for the user to press the "Q" key before we exit (not
            // the best way to do this, but it'll suffice).
            while (char.ToLower(Console.ReadKey(true).KeyChar) != 'q')
            {
                Thread.Sleep(500);
            }

            // Not necessary for safe disposal, but still, it's nice to "physically"
            // leave the room rather than letting timeouts figure out that we've gone.
            // NOTE: Calling .Leave() also disposes the room for us.
            sandbox.Leave();

            // Safely dispose of the client object (which'll also clean up any
            // undisposed created room instances).
            client.Dispose();
        }
    }
}
