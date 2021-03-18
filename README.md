# pop3svc
A POP3 Service for .net with pluggable mailbox providers.

## What is it?

This is a POP3 service, written using C#. The service is implemented using the "Listener" pattern where your code launches the service with a "provider" object. The service listens for incoming requests on the configured ports and reads POP3 commands sent from the client. When it needs to, requests are passed along to your provider object so your code is in control of the important details.

This code was take from a snapshot of a larger project in order to demonstrate extensions to the POP3 protocol. To complete the demo, this project comes with two wrapper projects. One implements a Windows Form app that allows the user to populate mailboxes with messages that a POP3 client might come along into and download. The other is a command-line application that automatically and randomly populates a mailbox with messages.

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
  - Not yet implemented but aded for future expansion. Provider objects should implement this with an empty function. 

### IPOP3Mailbox

The service will call through to this provider object to handle requsts to access the contents of messages.

- ```UserID(info)```
  - Return the user ID for this user. This ID is the single ID that uniquely identifies a user. A user might login as "bob", "BOB" or "bob@here", but this function allows the provider to normalize all those names into just "bob".
- ```ListMessageUniqueIDs(info)```
  - Called when the service needs a "directory listing". The provider should return a collection of strings that identify the messages available to the user. If the user's mailbox is empty, this function should return an empty collection.
- ```MessageExists(info, uniqueID)```
  - Called to request if a message exists or not. The sevice only calls this when the client requests a message that hasn't been identified before and allows the service to handle it.
- ```MessageSize(info, uniqueID)```
  - Called to request the size of the identified message in bytes.
- ```MessageContent(info, uniqueID)```
  - Called to request the contents of the identified message. The response is in the form of an object that implements the ```IMessageContent``` interface.
- ```MessageDelete(info, uniqueIDCollection)```
  - Called to request that the listed messages are all to be deleted. The provider should delete all of them (or at least put them beyond future retrieval) in an atomic operation.

The provider code may throw an exception of type PopResponseException to cause the service to respond with an error to the client. This exception might have a "critical" flag that causes the connection to be shut down.

### IMessageContent

This interface allows the service to read a message's contents line-by-line, with an option to stop retrieval if the client used a TOP command.

- ```NextLine()```
  - Return the next line in the message.
- ```Close()```
  - Indicates the service has retrieved enough lines.   

## What extensions does it demonstrate?

These posts describe the POP3 extensions that this service implement:
- (Links redacted until they're published.)

## How does it work?

The service uses .net's ```TcpListener``` library to listen for incoming TCP connections. When one arrives, the handler launches a new thread to handle the incomming connection which listens for commands sent from the client and returns responses.

This does mean that there's going to be a new thread opened for every incomming connection. That's because this comes from an early stage of development for a larger project. When that project is released, it'll use the preferred model of waiting asynchronously for commands coming from clients. For now, this project should only be considered a prototype that demonstrates the concept. For this reason, the demo services only listen for incomming connections on the localhost network interface. (You are free to edit the code so it listens on the main netwok interface.)

The very purpose of one of these extensions is to allow a client to usefully keep a POP3 connection open in the long term. I acknowledge the irony that this prototype goes against that.

## Does it work on Linux?

Yes. I've tested it using Microsoft's .NET 5.0 for Linux with Ubuntu in a virtual machine, with help from Visual Studio Code.

## I have requests or issues.

Please open an issue on this github project. I may end up closing it as won't-fix here but actually developing the requested change on my larger project under development. If I do that I'll let you know what's going on.
