﻿using System;
using Microsoft.WindowsAzure.MobileServices;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Microsoft.WindowsAzure.MobileServices.Sync;
using Microsoft.WindowsAzure.MobileServices.SQLiteStore;
using System.Collections.Generic;
using Microsoft.WindowsAzure.MobileServices.Files;
using Microsoft.WindowsAzure.MobileServices.Files.Sync;
using MobileAppsFilesSample.Droid;
using System.IO;
using MobileAppsFilesSample.Droid.Helpers;
using Microsoft.WindowsAzure.MobileServices.Eventing;

namespace MobileAppsFilesSample
{
    /// <summary>
    /// Manager classes are an abstraction on the data access layers
    /// </summary>
    public class TodoItemManager
    {
        // Azure
        IMobileServiceSyncTable<TodoItem> todoTable;
        MobileServiceClient client;
        IDisposable eventSubscription;

        public TodoItemManager()
        {
            client = new MobileServiceClient(
                Constants.ApplicationURL,
                Constants.GatewayURL,
                //Constants.ApplicationKey, new LoggingHandler(false));
                Constants.ApplicationKey, null);

           // var store = new TodoItemSQLiteStore("localstore.db", false, false, false);
            var store = new MobileServiceSQLiteStore("localstore.db");
            store.DefineTable<TodoItem>();

            // FILES: Initialize file sync
            this.client.InitializeFileSyncContext(new TodoItemFileSyncHandler(this), store);

            //Initializes the SyncContext using the default IMobileServiceSyncHandler.
            this.client.SyncContext.InitializeAsync(store);

            this.todoTable = client.GetSyncTable<TodoItem>();

            eventSubscription = this.client.EventManager.Subscribe<IMobileServiceEvent>(GeneralEventHandler);
        }

        private void GeneralEventHandler(IMobileServiceEvent mobileServiceEvent)
        {
            Debug.WriteLine("Event handled: " + mobileServiceEvent.Name);
        }

        public async Task SyncAsync()
        {
            ReadOnlyCollection<MobileServiceTableOperationError> syncErrors = null;

            try
            {
                // FILES: Push file changes
                await this.todoTable.PushFileChangesAsync();

                // FILES: Automatic pull
                // A normal pull will automatically process new/modified/deleted files, engaging the file sync handler
                await this.todoTable.PullAsync("todoItems", this.todoTable.CreateQuery());
            }
            catch (MobileServicePushFailedException exc)
            {
                if (exc.PushResult != null)
                {
                    syncErrors = exc.PushResult.Errors;
                }
            }

            // Simple error/conflict handling. A real application would handle the various errors like network conditions,
            // server conflicts and others via the IMobileServiceSyncHandler.
            if (syncErrors != null)
            {
                foreach (var error in syncErrors)
                {
                    if (error.OperationKind == MobileServiceTableOperationKind.Update && error.Result != null)
                    {
                        //Update failed, reverting to server's copy.
                        await error.CancelAndUpdateItemAsync(error.Result);
                    }
                    else
                    {
                        // Discard local change.
                        await error.CancelAndDiscardItemAsync();
                    }
                }
            }
        }

        public async Task<IEnumerable<TodoItem>> GetTodoItemsAsync()
        {
            try
            {
                return await todoTable.ReadAsync();
            }
            catch (MobileServiceInvalidOperationException msioe)
            {
                Debug.WriteLine(@"INVALID {0}", msioe.Message);
            }
            catch (Exception e)
            {
                Debug.WriteLine(@"ERROR {0}", e.Message);
            }
            return null;
        }

        public async Task SaveTaskAsync(TodoItem item)
        {
            if (item.Id == null)
            {
                await todoTable.InsertAsync(item);
                //TodoViewModel.TodoItems.Add(item);
            }
            else
                await todoTable.UpdateAsync(item);
        }

        public async Task DeleteTaskAsync(TodoItem item)
        {
            try
            {
                //TodoViewModel.TodoItems.Remove(item);
                await todoTable.DeleteAsync(item);
            }
            catch (MobileServiceInvalidOperationException msioe)
            {
                Debug.WriteLine(@"INVALID {0}", msioe.Message);
            }
            catch (Exception e)
            {
                Debug.WriteLine(@"ERROR {0}", e.Message);
            }
        }

        internal async Task DownloadFileAsync(MobileServiceFile file)
        {
            await this.todoTable.DownloadFileAsync(file, FileHelper.GetLocalFilePath(file.ParentId, file.Name));
        }

        internal async Task<MobileServiceFile> AddImage(TodoItem todoItem, string imagePath)
        {
            string targetPath = FileHelper.CopyTodoItemFile(todoItem.Id, imagePath);

            // FILES: Creating/Adding file
            MobileServiceFile file = await this.todoTable.AddFileAsync(todoItem, Path.GetFileName(targetPath));            

            // "Touch" the record to mark it as updated
            await this.todoTable.UpdateAsync(todoItem);

            return file;
        }

        internal async Task DeleteImage(TodoItem todoItem, MobileServiceFile file)
        {
            // FILES: Deleting file
            await this.todoTable.DeleteFileAsync(file);

            // "Touch" the record to mark it as updated
            await this.todoTable.UpdateAsync(todoItem);
        }

        internal async Task<IEnumerable<MobileServiceFile>> GetImageFiles(TodoItem todoItem)
        {
            // FILES: Get files (local)
            return await this.todoTable.GetFilesAsync(todoItem);
        }
    }
}