#Getting started with file management
This tutorial shows you how to manage (create, download, delete and list) files using the Azure Mobile Apps client SDK. At the end of this guide, you'll have simple updated version of the *To do list* (***addlink***)  quickstart app that supports file attachments. 

To complete this tutorial, you'll need the following:

* An active Azure account. If you don't have an account, you can sign up for an Azure trial and get up to 10 free mobile apps that you can keep using even after your trial ends. For details, see [Azure Free Trial](http://azure.microsoft.com/pricing/free-trial/).
* [Visual Studio Professional 2013](https://go.microsoft.com/fwLink/p/?LinkID=257546)
* A Xamarin Account

##Azure Mobile Services File Management
TODO: Describe the goals of the functionality exposed by the SDK, the patterns and other relevant implementation/behavior details.

Examples of what we need to document:

 - Communication pattern
	 - Client, storage, service, etc.
 - Data items/files relationships (file scoping)
 - SAS issuance, caching, etc.
 - Default "no server state" behavior
	 - Examples on how to extend 
 - ???

##Offline file management
The Azure Mobile Services Client SDK provides offline file management support, allowing you to synchronize file changes when network connectivity is available.

The updated *To do list app* takes advantage of this functionality to expose the following features:

 - Allow users to associate files with *to do* items (multiple files per item)
 - All changes are local, until a user taps the *synchronize* button
 - Items and files created by other users are automatically downloaded by the application, making them available offline
 - Items and files deleted by other users are removed from the local device


###Offline flow
When working in offline mode, file management operations are saved locally, until the application synchronizes those changes (typically when network availability is restored or when the user explicitly requests a synchronization via an application gesture).

The diagram below shows the sequence of operations for a file creation:

```sequence
Application code->Azure Mobile Services SDK: Create file X
Azure Mobile Services SDK->Azure Mobile Services SDK: Queue create file X operation
Application code->Azure Mobile Services SDK: Push file changes
Azure Mobile Services SDK->Application code: Get file X data
Application code->Azure Mobile Services SDK: File X data
Azure Mobile Services SDK->Storage: Upload file
```

It's important to understand that the Azure Mobile Services Client SDK will not store the file data. The client SDK will invoke your code when it needs File contents will be requested. The application (your code) decides how (and if) files are stored on the local device.

####IFileSyncHandler
The Azure Mobile Services SDK interacts with the application code as part of the file management and synchronization process. This communication takes place using the IFileSyncHandler implementation provided by the application (your code).

IFileSyncHandler is a simple interface with the following definition:

```c#
 public interface IFileSyncHandler
    {
        Task<IMobileServiceFileDataSource> GetDataSource(MobileServiceFileMetadata metadata);

        Task ProcessFileSynchronizationAction(MobileServiceFile file, FileSynchronizationAction action);
    }
```
```GetDataSource``` is called when the Azure Mobile Services Client SDK needs the file data (e.g.: as part of the upload process). This gives you the ability manage how (and if) files are stored on the local device and return that information when needed.

```ProcessFileSynchronizationAction``` is invoked as part of the file synchronization flow. A file reference and a FileSynchronizationAction enumeration value are provided so you can decide how your application should handle that event (e.g. automatically downloading a file when it is created or updated, deleting a file from the local device when that file is deleted on the server).

When initializing the file synchronization runtime, your application must supply a concrete implementation of the ```IFileSyncHandler```, as shown below:

```c#
MobileServiceClient client = new MobileServiceClient("app_url", "gateway_url", "application_key");

// . . . Other initialization code (local store, sync context, etc.)
client.InitializeFileSync(new MyFileSyncHandler(), store);
```

####Creating and uploading a file
The most common way of working with the file management API is through a set of extension methods on the ```IMobileServiceTable<T>``` interface, so in order to use the API, you must have a reference to the table you're working with:

```c#
MobileServiceFile file = await myTable.AddFileAsync(myItem, "file_name");
``` 

The following using statement is also required:
```c#
using Microsoft.WindowsAzure.MobileServices.Files;
```

In the offline scenario, the upload will occur when the application initiates a synchronization, when that happens, the runtime will begin processing the operations queue and, once it finds this operation, it will invoke the ```GetDataSource``` method on the ```IFileSynchHandler``` instance provided by the application in order to retrieve the file contents for the upload.

####Deleting a file
To delete a file, you can follow the same pattern described above and use the ```DeleteFileAsync``` method on the ```IMobileServiceTable<T>``` instance:

```c#
await myTable.DeleteFileAsync(file);
``` 

As with the create file example, the following using statement is also required:
```c#
using Microsoft.WindowsAzure.MobileServices.Files;
```

In the offline scenario, the file deletion will occur when the application initiates a synchronization.

####Retrieve an item's files
As mentioned in the *Azure Mobile Services File Management* section, files are managed through its associated record. In order to retrieve an item's files, you can call the ```GetFilesAsync``` method on the  ```IMobileServiceTable<T>``` instance. 

```c#
IEnumerable<MobileServiceFile> files = await myTable.GetFilesAsync(myItem);
``` 
This method returns a list of files associated with the data item provided. It's important to remember that this is a ***local*** operation and will return the files based on the state of the object when it was last synchronized.

To get an updated list of files from the server, you can initiate a sync operation as described in the *synchronizing file changes* section