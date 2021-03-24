# pop3svc
A POP3 Service for .net with pluggable mailbox providers.

## What is it?

This is a POP3 service, written using C#. The service is implemented using the "Listener" pattern where your code launches the service with a "provider" object that you write. The service listens for incoming requests on the configured ports and reads POP3 commands sent from the client. When the server needs a list of messages or the contents of a message, the service will invoke the provider object. This way, the service code deals with the complexity of talking protocol while your code is left with making the important decisions.

## But why?

Because I thought you'd find it impressive.

![](https://media.giphy.com/media/NInhSPmxCgaxq/giphy.gif)         
(That's you.)

But as well as that, I wrote some extensions to POP3 and needed working code in the form of a prototype. I had already started a larger project to build a mail server library, so I decided to pull out the POP3 section and publish that as my prototype. The overall project is still in its early stage and using the one-thread-per-connection model, but this is good enough to move forward with writing these extensions into RFCs. It is with some irony that the point of one of these extensions is allow connections to be kept open in the long term. Having a thread open for each open connecton is not what you want.

So yes, next on my list of things to do is to use proper async reads. So don't bother pointing it out.

If you'd like to be even more impressed, here's the description of the extensions I wrote:
- [Mailbox Refresh Mechanism][1]
- [Goodbye to Numeric Message IDs][2]
- [RFC drafting project][3]

[1]: https://billpg.com/pop3-refr/
[2]: https://billpg.com/pop3-message-ids/
[3]: https://github.com/billpg/Pop3ExtRfc/

## How do I use it?

````
var listen = new billpg.pop3svc.POP3Listener();
listen.Provider = /* Your provider object. */ ;
listen.SecureCertificate = /* Your PFK. */ ;
listen.ListenOn(IPAddress.Any, 110, false);
listen.ListenOn(IPAddress.Any, 995, true);
/* Service is now listening for incomming connections. */
````

The provider object needs to implement the interface ```IPOP3MailboxProvider```. This object will handle login requests when they come in.

### IPOP3MailboxProvider

The service will call through to this provider object to handle login requests.

- ```Name```
  - Returns the name of this provider object. Will be shown to clients in the connection banner.
- ```Authenticate(info, username, password)```
  - Passes a uername and password. Provider object should test the supplied credentials for validity and either return an object that implements the ```IPOP3Mailbox``` interface, or NULL to reject the login request.
- ```RegisterNewMessageAction(action)```
  - Not yet implemented but aded for planned expansion. Provider objects should implement this with an empty function. 

### IPOP3Mailbox

The service will call through to this provider object to handle requsts to access the contents of messages.

- ```UserID(info)```
  - Return the user ID for this user. This ID is the single ID that uniquely identifies a user. 
  - A user might login as "bob", "BOB" or "bob@here", but this function allows the provider to normalize all those names into just "bob".
- ```ListMessageUniqueIDs(info)```
  - Called when the service needs a "directory listing". The provider should return a collection of strings that identify the messages available to the user. 
  - If the user's mailbox is empty, this function should return an empty collection.
  - These identifiers will be shown to the user in response to a ```UIDL``` command. The strings must only use ASCII characters, not including control characters and space.
- ```MessageExists(info, uniqueID)```
  - Called to request if a message exists or not. 
  - The sevice only calls this when the client requests a message that hasn't been identified before and allows the service to handle it.
- ```MessageSize(info, uniqueID)```
  - Called to request the size of the identified message in bytes.
- ```MessageContent(info, uniqueID)```
  - Called to request the contents of the identified message. 
  - The response is in the form of an object that implements the ```IMessageContent``` interface.
- ```MessageDelete(info, uniqueIDCollection)```
  - Called to request that the listed messages are all to be deleted. 
  - The provider should delete all of them (or otherwise put them beyond future retrieval) in an atomic operation.

The provider code may throw an exception of type ```PopResponseException``` to cause the service to respond with an error to the client. This exception might have a "critical" flag that causes the connection to be shut down.

If the provider code throws a ```NotImplementedException```, the service will pass an appropriate error to the client but keep the onnection open.

### IMessageContent

This interface allows the service to read a message's contents line-by-line, with an option to stop retrieval if the client used a TOP command.

- ```NextLine()```
  - Return the next line in the message.
- ```Close()```
  - Indicates the service has retrieved enough lines.   

### IPOP3ConnectionInfo

Many calls to the above interfaces from the service engine will supply an "info" object that implements this interface, providing some details about the crrent connection.

- ```ClientIP```
  - The source IP address of the current connection.
- ```ConnectionID```
  - The internal connection identifier used by the service.
- ```UserID```
  - The user ID of this mailbox, as normalized by the UserID function documented above.
- ```UserNameAtLogin```
  - The user name supplied by the user on login.
- ```IsSecure```
  - Returns a flag indicating if the underlying connection is secured by TLS.
- ```ProviderTag```
  - A settable object reference your code can use that will remain attached to this connection. The service will ignore it.  

## Does it work on Linux?

Yes. I've tested it using Microsoft's .NET 5.0 for Linux with Ubuntu in a virtual machine, with help from Visual Studio Code.

## I have requests or issues.

Please open an issue on this github project. I may end up closing it as won't-fix here but actually developing the requested change on my larger project under development. If I do that I'll let you know what's going on. 

<div><a href="https://billpg.com/"><img src="https://billpg.com/wp-content/uploads/2021/03/BillAndRobotAtFargo-e1616435505905-150x150.jpg" alt="billpg.com" align="right" border="0" style="border-radius: 25px; box-shadow: 5px 5px 5px grey;" /></a></div>
