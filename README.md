# billpg.POP3Listener

A POP3 Listener for dot-net.

## What is it?

A component of a POP3 service. Like ```TcpListener``` and ```HttpListener```, this component's job is to listen for incomming connections and to talk the protocol with clients. When the time comes to authenticate users or download messages, the library will call through to event handlers that you write. This way, the listener code deals with the blah-blah-blah of commands and responses while your code is left with the important tasks.

## But why?

Because I thought you'd find it impressive.

![](https://media.giphy.com/media/eKNrUbDJuFuaQ1A37p/giphy.gif)         
(That's you.)

But as well as that, I wrote some extensions to POP3 and needed working code in the form of a prototype. I had already started a larger project to build a mail server library, so I decided to pull out the POP3 section and publish that as my prototype. The overall project is still in its early stage and using the one-thread-per-connection model, but this is good enough to move forward with writing these extensions into RFCs. It is with some irony that the point of one of these extensions is allow connections to be kept open in the long term. Having a thread open for each open connecton is not what you want.

So yes, next on my list of things to do is to use proper async reads. So don't bother pointing it out.

If you'd like to be even more impressed, here's the description of the extensions I wrote:
- [Mailbox Refresh Mechanism][1]
- [Goodbye to Numeric Message IDs][2]
- [Delete Immediately][3]
- [RFC drafting project][4]

[1]: https://billpg.com/pop3-refr/
[2]: https://billpg.com/pop3-message-ids/
[3]: https://billpg.com/pop3-deli/
[4]: https://github.com/billpg/Pop3ExtRfc/

## How do I use it?

````
var listen = new billpg.pop3.POP3Listener();
listen.SecureCertificate = /* Your PFK. */ ;
listen.ListenOn(IPAddress.Any, 110, false);
listen.ListenOn(IPAddress.Any, 995, true);
/* Service is now listening for incomming connections. */
````

This code snippet above will start a service that accepts secure (and insecure) POP3 connections. As it stands, it won't do anything useful as all of the default event handers are in place, but I've written a short introduction that walk through the process of writing a very simple POP3 service that reads email files from a folder on your computer. [Build Your Own POP3 Service.][5]

[5]: https://github.com/billpg/POP3Listener/blob/main/BuildYourOwnPOP3Server.md

## Does it work on Linux?

Yes. I've tested it using Microsoft's .NET 5.0 for Linux with Ubuntu in a virtual machine, with help from Visual Studio Code.

## I have requests or issues.

Please open an issue on this github project. I may end up closing it as won't-fix here but actually developing the requested change on my larger project under development. If I do that I'll let you know what's going on. 

<div><a href="https://billpg.com/"><img src="https://billpg.com/wp-content/uploads/2021/03/BillAndRobotAtFargo-e1616435505905-150x150.jpg" alt="billpg.com" align="right" border="0" style="border-radius: 25px; box-shadow: 5px 5px 5px grey;" /></a></div>
