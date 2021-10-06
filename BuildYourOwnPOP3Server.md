So, you want to write a POP3 service? That’s great. In this post, we’ll walk through building a simple POP3 service that uses a folder full of EML files 
as a mailbox and serves them to anyone logging in.

# Getting Started
I’m assuming you are already set-up to be writing and building C# code. If you have Windows, the free version of Visual Studio 2019 is great.
(Or use a more recent version if one exists.) Visual Studio Code is great on Linux too.

Download and build billpg industries POP3 Listener. Open up a new console app project and include the billpg,POP3Listener.dll file as a reference.
You’ll find the code for this project on the same github in its own folder.

```
using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using billpg.pop3;

namespace BuildYourOwnPop3Service
{
    class Program
    {
        static void Main()
        {
            /* Launch POP3. */
            var pop3 = new POP3Listener();
            pop3.ListenOn(IPAddress.Loopback, 110, false);

            /* Keep running until the process is killed. */
            while (true) System.Threading.Thread.Sleep(10000);
        }
    }
}
```
This is the bare minimum to run a POP3 service. It’ll only accept local connections. If you’re running on Linux, you may need to change the
port you’re listening on to 1100. Either way, try connecting to it. You can set up your mail reader or use telnet to connect in and type commands.

# Accepting log-in requests.
You’ll notice that any username and password combination fails. This is because you’ve not set up anything to handle authentication requests yet.
If you don’t set one up, the default event handler just rejects all attempts to log in. Let’s write one.

```
/* Add between listener constructor and call to ListenOn. */
pop3.Events.OnAuthenticate = MyAuthenticationHandler;
void MyAuthentictionHandler(POP3AuthenticationRequest request)
{
}
```

This still doesn't allow any log-in requests, but we can now put a breakpoint in this function and see what happens when the function is called.
While you are in debug mde, you can poke about the request object and see what's there.



This won’t compile because MyProvider doesn’t meet the requirements of the interface. Let’s add those.

/* Inside the MyProvider class. */
public string Name => "My Provider";

public IPOP3Mailbox Authenticate(
    IPOP3ConnectionInfo info, 
    string username, 
    string password)
{
    return null;
}
Now, the service is just as unyielding to attempts to log-in, but we can confirm our provider code is running by adding a breakpoint to the Authenticate function. Now, when we attempt to log-in, we can see that the service has collected a username and password and is asking us if these are correct credentials or not. Returning a NULL means they’re not.

This might be a good opportunity to take a look at the info parameter. All of the functions where the listener calls to the provider will include this object, providing you with the client’s IP address, IDs, user names, etc. You don’t have to make use of them but your code may find the information useful.

A basic mailbox with no messages.
We can change our Authenticate function to actually test credentials. For our play project we’ll just accept one combination of user-name and password.

if (username == "me" && password == "passw0rd")
    return new MyMailbox();
else
    return null;
This will fail compilation because we’ve not written MyMailbox yet. Let’s go ahead and do that.

class MyMailbox : IPOP3Mailbox
{
}
Again, we’ll need to write all the requirements of the interface before we can run. So we can move on quickly, let’s provide just the minimum.

The first thing we’ll need is a list of the available messages. We’ll return an empty collection for now.

public IList<string> ListMessageUniqueIDs(
    IPOP3ConnectionInfo info)
    => new List<string>();
The service needs to know if a mailbox is read-only or not. Let’s say it isn’t.

public bool MailboxIsReadOnly(
    IPOP3ConnectionInfo info)
    => false;
The service might sometimes need to know is a message exists or not. For now, it doesn’t.

public bool MessageExists(
    IPOP3ConnectionInfo info,
    string uniqueID)
    => false;
The client might request the size of a message before it downloads it and the service will pass the request along to the provider. I’ve often suspected that clients don’t really need this so let’s just return your favorite positive integer.

public long MessageSize(
   IPOP3ConnectionInfo info, 
   string uniqueID)
   => 58;
The client will, in due course, request the contents of a message, but won’t because both the list-messages and message-exists will deny the existence of any messages, so for now, we can just return null.

public IMessageContent MessageContents(
    IPOP3ConnectionInfo info, 
    string uniqueID)
    => null;
Finally, we need to handle message deletion. Again, we don’t need to do anything just yet.

public void MessageDelete(
    IPOP3ConnectionInfo info, 
    IList<string> uniqueIDs)
{}
And we’re done. Run the code and log-in. Your mailbox will be perpetually empty but you can add breakpoints and confirm everything is running.

List the messages.
Now, let’s actually start with something useful. Let’s change our ListMessageUniqueIDs to return a list of filenames from a folder. You’ll want to replace the value of FOLDER with something that works for you.

const string FOLDER = @"C:\MyMailbox\";

public IList<string> ListMessageUniqueIDs(
    IPOP3ConnectionInfo info)
    => Directory.GetFiles(FOLDER)
           .Select(Path.GetFileName)
           .ToList();

public bool MessageExists(
    IPOP3ConnectionInfo info, 
    string uniqueID)
    => ListMessageUniqueIDs(info)
           .Contains(uniqueID);
Let’s also place an EML file into our mailbox folder. If you don’t have an EML file to hand, you can write your own using notepad. (It doesn’t care if the file has a “.txt” extension.)

Subject: I'm a very simple EML file.
From: me@example.com
To: you@example.com

Message body goes after a blank line.
If we save that into our mailbox folder and run up the POP3 service, we’ll see there’s a message available. It won’t be able to download it though.

Download the message,
The MessageContents function expects an new object that implements the IMessageContent interface.

/* Replace the MessageContents function. */
public IMessageContent MessageContents(
    IPOP3ConnectionInfo info, 
    string uniqueID)
{
    if (MessageExists(info, uniqueID))
        return new MyMessageContents(
                       Path.Combine(FOLDER, uniqueID));
    else
        return null;
}

/* New class. */
class MyMessageContents : IMessageContent
{
    List<string> lines;
    int index;

    public MyMessageContents(string path)
    {
        lines = File.ReadAllLines(path).ToList();
        index = 0;
    }

    public string NextLine()
        => (index < lines.Count) ? lines[index++] : null;

    public void Close()
    {
    }
}
This shows the requirements of the object that regurgitates a single message’s contents. A function that returns the next line, one-by-one, and another that’s called to close down the stream. The Close function could close opened file streams or delete temporary files, but we don’t need it to do anything in our play project.

Note that the command handling code inside this library has an extension that allows the client to ask for a message by an arbitrary unique ID. Make sure your code doesn’t allow, for example, “../../../../my-secret-file.txt”. Observe the code above checks that the requested unique ID is in the list of acceptable message IDs by going through MessageExists.

Delete messages.
The interface to delete messages passes along a collection of string IDs. This is necessary because the protocol requires that a set of messages are deleted in an atomic manner. Either all of them are deleted or none of them are deleted. We can’t have a situation where some of messages are deleted but some are still there.

But since this is just a play project, we can play fast and loose with such things.

public void MessageDelete(
     IPOP3ConnectionInfo info, 
     IList<string> uniqueIDs)
{
    foreach (var toDelete in uniqueIDs)
        if (MessageExists(info, toDelete))
            File.Delete(Path.Combine(FOLDER, toDelete));
}
What now?
I hope you enjoyed building your very own POP3 service using the POP3 Listener component. The above was a simple project to get you going.

billpg.com
Maybe think about your service could handle multiple users and how you’d check their passwords. What would be a good way to achieve atomic transactions on delete? What happens if someone deletes the file in a mailbox folder just as they’re about to download it?

If you do encounter an issue or you have a question, please open an issue on the project’s github page.
