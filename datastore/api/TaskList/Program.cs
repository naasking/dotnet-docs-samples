// Copyright 2016 Google Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Google.Cloud.Datastore.V1;
using System;
using Grpc.Core;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HigherLogics.Google.Datastore;

namespace GoogleCloudSamples
{
    /**
     * A simple Task List application demonstrating how to connect to Cloud
     * Datastore, create, modify, delete, and query entities.
     */

    class TaskList
    {
        private readonly DatastoreDb _db;
        const string emulatorHost = "localhost";
        const int emulatorPort = 8081;
        const string namespaceId = "";
        //private readonly KeyFactory _keyFactory;

        TaskList(string projectId)
        {
            // [START datastore_build_service]
            // Create an authorized Datastore service using Application Default Credentials.
            var client = DatastoreClient.Create(new Channel(emulatorHost, emulatorPort, ChannelCredentials.Insecure));
            _db = DatastoreDb.Create(projectId, namespaceId, client);
            //_db = DatastoreDb.Create(projectId);
            // Create a Key factory to construct keys associated with this project.
            //_keyFactory = _db.CreateKeyFactory("Task");
            // [END datastore_build_service]
        }

        public class Task
        {
            [Key]
            public long Id { get; set; }
            public string Description { get; set; }
            public DateTime Created { get; set; }
            public bool Done { get; set; }
        }

        // [START datastore_add_entity]
        /// <summary>
        ///  Adds a task entity to the Datastore
        /// </summary>
        /// <param name="description">The task description.</param>
        /// <returns>The key of the entity.</returns>
        Key AddTask(string description)
        {
            return _db.Insert<Task>(new Task
            {
                Description = description,
                Created = DateTime.UtcNow,
            });
        }
        // [END datastore_add_entity]

        // [START datastore_update_entity]
        /// <summary>
        /// Marks a task entity as done.
        /// </summary>
        /// <param name="id">The ID of the task entity as given by Key.</param>
        /// <returns>true if the task was found.</returns>
        bool MarkDone(long id)
        {
            using (var transaction = _db.BeginTransaction())
            {
                var task = transaction.Lookup(id.ToKey<Task>(), new Task());
                if (task != null)
                {
                    task.Done = true;
                    transaction.Update(task);
                }
                transaction.Commit();
                return task != null;
            }
        }
        // [END datastore_update_entity]

        // [START datastore_retrieve_entities]
        /// <summary>
        /// Returns a list of all task entities in ascending order of creation time.
        /// </summary>
        IEnumerable<Task> ListTasks()
        {
            var query = _db.CreateQuery<Task>();
                query.Order.Add(nameof(Task.Created), PropertyOrder.Types.Direction.Descending);
            return _db.RunQuery(query).Entities<Task>();
        }
        // [END datastore_retrieve_entities]

        // [START datastore_delete_entity]
        /// <summary>
        /// Deletes a task entity.
        /// </summary>
        /// <param name="id">The ID of the task entity as given by Key.</param>
        void DeleteTask(long id)
        {
            _db.Delete(id.ToKey<Task>());
        }
        // [END datastore_delete_entity]

        static IEnumerable<string> FormatTasks(IEnumerable<Task> tasks)
        {
            var results = new List<string>();
            foreach (var task in tasks)
            {
                var note = task.Done ? "done" : $"created {(DateTime)task.Created}";
                results.Add($"{task.Id} : {(string)task.Description} ({note})");
            }
            return results;
        }

        void HandleCommandLine(string commandLine)
        {
            string[] args = commandLine.Split(null, 2);
            if (args.Length < 1)
                throw new ArgumentException("not enough args");
            string command = args[0];
            switch (command)
            {
                case "new":
                    if (args.Length != 2)
                        throw new ArgumentException("missing description");
                    AddTask(args[1]);
                    Console.WriteLine("task added");
                    break;
                case "done":
                    if (args.Length != 2)
                        throw new ArgumentException("missing task id");
                    long id = long.Parse(args[1]);
                    if (MarkDone(id))
                        Console.WriteLine("task marked done");
                    else
                        Console.WriteLine($"did not find a Task entity with ID {id}");
                    break;
                case "list":
                    var tasks = FormatTasks(ListTasks());
                    Console.WriteLine($"found {tasks.Count()} tasks");
                    Console.WriteLine("task ID : description");
                    Console.WriteLine("---------------------");
                    foreach (string task in tasks)
                        Console.WriteLine(task);
                    break;
                case "delete":
                    if (args.Length != 2)
                        throw new ArgumentException("missing task id");
                    DeleteTask(long.Parse(args[1]));
                    Console.WriteLine("task deleted (if it existed)");
                    break;
                default:
                    throw new ArgumentException($"unrecognized command: {command}");
            }
        }

        static void Main(string[] args)
        {
            string projectId = args.Length == 1 ? args[0] :
                Environment.GetEnvironmentVariable("GOOGLE_PROJECT_ID");
            Console.WriteLine($"Using project {projectId}");
            if (string.IsNullOrWhiteSpace(projectId))
            {
                Console.WriteLine("Set the environment variable " +
                    "GOOGLE_PROJECT_ID or pass the google project id on" +
                    "the command line.");
                return;
            }
            TaskList taskList = new TaskList(projectId);
            Console.WriteLine("Cloud Datastore Task List\n");
            PrintUsage();
            while (true)
            {
                Console.Write("> ");
                string commandLine = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(commandLine))
                    break;
                try
                {
                    taskList.HandleCommandLine(commandLine);
                }
                catch (ArgumentException e)
                {
                    Console.WriteLine(e.Message);
                    PrintUsage();
                }
            }
            Console.WriteLine("exiting");
        }

        private static void PrintUsage()
        {
            Console.WriteLine(@"Usage:
  new <description>  Adds a task with a description <description>
  done <task-id>     Marks a task as done
  list               Lists all tasks by creation time
  delete <task-id>   Deletes a task");
        }
    }
}
