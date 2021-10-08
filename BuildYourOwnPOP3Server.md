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
            
            /* Start listening. */
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
You’ll notice that any username and password combination fails. This is because the default authentication handler is in place, one which rejects all attempts to
log-in. That's not very useful so let’s write one and set it as the `Events.OnAuthenticate` property.

```
/* Add this code between listener constructor and call to ListenOn. */
pop3.Events.OnAuthenticate = MyAuthenticationHandler;
void MyAuthentictionHandler(POP3AuthenticationRequest request)
{
    /* Is this the only valid username and password? */
    if (request.SuppliedUsername == "me" && request.SuppliedPassword == "passw0rd")
    {
        /* It is. Pass back the user's authenticated mailbox ID back to the server. */
        request.AuthMailboxID = "My-Mailbox-ID";
    }
}
```

With the `OnAuthenticate` propery set, the server will now pass all login requests to your function. You could set a breakpoint and take a look at the other
properties of the `request` object, including the client's IP. 

If the supplied username and password is successful, the authentiction function sets the user's autenticated mailbox ID. Other calls from the service to your code will pass this mailbox ID along to identify which mailbox the user is interacting with. What a mailbox ID looks like is up to you. Any string value (within reason) is fine except NULL. It could be a copy of the supplied username, a stringified primary key value, anything as long as the value is consistent.

## Suggested exercise:
- Create a table of usernames and hashed passwords. (Database table, text file, up to you.)
- Write an authentication function that queries this table.

# Mailbox Contents.
With an authentication function, we can now log-in to our mailbox, but the mailbox is empty. This is because the default message-list handler only ever responds 
with an empty list of messages. We need to write a handler that returns a list of messages.

You can set the handler by setting the `Events.OnMessageList` property to your custom fuunction. For now, let's write a simple function that reads the contents of a folder. The service supplies the Mailbox ID which was set by the authentication function earlier.
```
/* Set the OnMessageList event. Insert this block just before the ListenOn call. */
const string MAILBOX_FOLDER = @"C:/Set/This/To/An/Actual/Folder";
pop3.Events.OnMessageList = MyMessageList;
IEnumerable<string> MyMessageList(string mailboxID)
{
    /* If this is the mailbox ID we knw about, return the filenames from the folder. */
    if (mailboxID == "My-Mailbox-ID")
        return Directory.EnumerateFiles(FOLDER).Select(Path.GetFileName);

    /* Not a known mailbox-ID, so return an empty collection. */
    else
        return Enumerable.Empty<string>();
}
```

Now let's save an EML file into your mailbox folder. EML files are plain ASCII text files you can create with Notepad. Try copy-and-paste the following text into a file and save it into your mailbox.
```
From: John Rutabaga <john.rutabaga@example.com>
To: Alice Rutabaga <alice.rutabaga@example.com>, Bob Rutabaga <bob.rutabaga@example.com>
Subject: We are the Rutabagas of New York!

Hello my lovely family!
```

Because we've not written a way to retrieve the message contents yet, POP3 commands `STAT` and `LIST` won't work but `UIDL` will. (We'll see why later.)

If you're interacting with the server by typing commands directtly, send a `UIDL` command and you'll see a brief "folder listing" come back. The server has
identified each file in your mailbox folder and is presenting the file-names as a list of messages. If you try to download one or query its size, it will fail,
for now.

## Suggested exercise:
- Add a folder field to username/password table.
- Return a list of files for the authenticated mailbox by returning that user's mailbox folder's contents.

# Message Retrieval
As discussed earlier, we can't do much with a mailbox listing without a way to retrieve that message. Again, this is a new handler you can set. The default
handler only sends error responses so it needs to be replaced to have a useful POP3 service.

```
/* Set the OnMessageRetrieval event handler. Insert this block just before the ListenOn call. */
pop3.Events.OnMessageRetrieval = MyMessageDownload;
void MyMessageDownload(POP3MessageRetrievalRequest request)
{
    /* Is this the mailbox? */
    if (request.AuthMailboxID == "My-Mailbox-ID")
    {
        /* Locate the EML file in the mailbox folder. */
        string emlPath = Path.Combine(FOLDER, request.MessageUniqueID);
        
        /* Pass the EML file to the server and don't delete it. *
        request.UseTextFile(emlPath, false);
    }
}
```

Note that `UseTextFile` is a helper function. What the server expects is for two events to be set on the retrieval-request object: `OnNextLine` and `OnClose`.
The server will call `OnNextLine` to get one more line of text until that function returns null. Afterwards, the server will call the `OnClose` function to
conclude the process. (If the client sent a `TOP` command, the server will stop early once enough lines have been fetched and then call `OnClose.)

As well as `UseTextFile`, there is a `UseEnumerableLines` if a stream of strings is more your thing.

## Suggested exercises:
- Experiment setting `OnNextLine` and `OnClose` directly instead of going via the helpers.
- 
- Why do you think the server is calling your retrieval handler when the client makes a `STAT` or `LIST` call?

# Delete Messages
POP3 is designed to work on a download-and-delete model. The client sends a `DELE` command for each message it wants to delete once it has downloaded it, but the command to commit the delete commands is `QUIT`. Because the protocol requires that many deletes are batched, the interface from the server to your code supplies many message's unique IDs to be deleted as an atomic operation. This is necessary because the protocol requires that a set of messages are deleted in an atomic manner. Either all of them are deleted or none of them are deleted. We can’t have a situation where some of messages are deleted but some are still there.

But since this is just a play project, we can play fast and loose with such things. Atomic operations? Gluons more like!

```

```
   
## Suggested exercises:
- How could you make the delete operation atomic? What if something goes wrong part way through?
- What happens if the server is asking you to delete a message that's already gone?
   
   
# Why does the server download all the messages in the mailbox everytime I log-in?
Because the client is calling the `STAT` command and the server needs to know how big the messages are to send back to the client.

I would argue a well written POP3 client should only call `UIDL` to get the state of a mailbox instead of `STAT` or `LIST`, but this is where we're at. Sometimes the server needs to know how big a message is and the default handler does it by reading the message ad counting bytes. As you almost certainly have a better way of finding the size of a message, you can provide your own handler.

```

```

# Why 

Note that the command handling code inside this library has an extension that allows the client to ask for a message by an arbitrary unique ID. Make sure your code doesn’t allow, for example, “../../../../my-secret-file.txt”. Observe the code above checks that the requested unique ID is in the list of acceptable message IDs by going through MessageExists.

    
    What now?
I hope you enjoyed building your very own POP3 service using the POP3 Listener component. The above was a simple project to get you going.

billpg.com
Maybe think about your service could handle multiple users and how you’d check their passwords. What would be a good way to achieve atomic transactions on delete? What happens if someone deletes the file in a mailbox folder just as they’re about to download it?

If you do encounter an issue or you have a question, please open an issue on the project’s github page.
