﻿using log4net;
using Logshark.PluginLib.Helpers;
using Logshark.PluginLib.Logging;
using Logshark.PluginLib.Persistence;
using Logshark.PluginLib.StatusWriter;
using MongoDB.Driver;
using ServiceStack.OrmLite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace Logshark.PluginLib.Model.Impl
{
    /// <summary>
    /// The base plugin from which all other plugins should inherit.
    /// </summary>
    public abstract class BasePlugin : IPlugin
    {
        public abstract ISet<string> CollectionDependencies { get; }
        protected IDictionary<Type, long> RecordsPersisted { get; set; }

        public IMongoDatabase MongoDatabase { get; set; }
        public IDbConnectionFactory OutputDatabaseConnectionFactory { get; set; }

        protected readonly ILog Log; 

        /// <summary>
        /// The method plugin creators will need to override to create a plugin.
        /// </summary>
        /// <param name="pluginRequest">The Plugin Request containing the MongoDatabase, the IDbConnection, and an Arguments Map.</param>
        /// <returns>PluginResponse indicating if this plugin executed successfully.</returns>
        public abstract IPluginResponse Execute(IPluginRequest pluginRequest);

        /// <summary>
        /// Helper method for plugin authors to be able to easily new up a PluginResponse for the derived plugin type.
        /// </summary>
        /// <returns>PluginResponse for the derived plugin type.</returns>
        protected PluginResponse CreatePluginResponse()
        {
            return new PluginResponse(GetType().Name);
        }

        /// <summary>
        /// Retrieves a new instance of IDbConnection using the connection factory.
        /// </summary>
        /// <returns>Initialized instance of IDbConnection.</returns>
        protected IDbConnection GetOutputDatabaseConnection()
        {
            return OutputDatabaseConnectionFactory.OpenDbConnection();
        }

        protected BasePlugin()
        {
            RecordsPersisted = new Dictionary<Type, long>();
            Log = PluginLogFactory.GetLogger(this.GetType());
        }

        protected ConcurrentBatchPersister<T> GetConcurrentBatchPersister<T>(IPluginRequest request = null) where T : new() 
        {
            if (request == null)
            {
                return new ConcurrentBatchPersister<T>(OutputDatabaseConnectionFactory, recordsPersisted:RecordsPersisted);
            }
            else
            {
                int poolSize = GlobalPluginArgumentHelper.GetPersisterPoolSize(request);
                int batchSize = GlobalPluginArgumentHelper.GetPersisterBatchSize(request);
                return new ConcurrentBatchPersister<T>(OutputDatabaseConnectionFactory, poolSize, batchSize, RecordsPersisted);
            }
        }

        protected ConcurrentCustomPersister<T> GetConcurrentCustomPersister<T>(ConcurrentCustomPersister<T>.InsertionMethod insertionMethod, IPluginRequest request = null) where T : new()
        {
            if (request == null)
            {
                return new ConcurrentCustomPersister<T>(OutputDatabaseConnectionFactory, insertionMethod, recordsPersisted:RecordsPersisted);
            }
            else
            {
                int poolSize = GlobalPluginArgumentHelper.GetPersisterPoolSize(request);
                return new ConcurrentCustomPersister<T>(OutputDatabaseConnectionFactory, insertionMethod, poolSize, RecordsPersisted);
            }
        }

        protected PersisterStatusWriter<T> GetPersisterStatusWriter<T>(IPersister<T> persister, long? expectedTotalPersistedItems = null) where T : new()
        {
            string persistedType = typeof(T).Name;
            string progressFormatMessage;

            if (expectedTotalPersistedItems.HasValue)
            {
                progressFormatMessage = PluginLibConstants.DEFAULT_PERSISTER_STATUS_WRITER_PROGRESS_MESSAGE_WITH_TOTAL.Replace("{PersistedType}", persistedType);   
            } 
            else 
            {
                progressFormatMessage = PluginLibConstants.DEFAULT_PERSISTER_STATUS_WRITER_PROGRESS_MESSAGE.Replace("{PersistedType}", persistedType);
            }

            return new PersisterStatusWriter<T>(persister, Log, progressFormatMessage, PluginLibConstants.DEFAULT_PROGRESS_MONITOR_POLLING_INTERVAL_SECONDS, expectedTotalPersistedItems);
        }

        protected TaskStatusWriter GetTaskStatusWriter(ICollection<Task> tasks, string taskType, long? expectedTotalTasks = null)
        {
            string progressFormatMessage;
            if (expectedTotalTasks.HasValue)
            {
                progressFormatMessage = PluginLibConstants.DEFAULT_TASK_STATUS_WRITER_PROGRESS_MESSAGE_WITH_TOTAL.Replace("{TaskType}", taskType);
            } 
            else
            {
                progressFormatMessage = PluginLibConstants.DEFAULT_PERSISTER_STATUS_WRITER_PROGRESS_MESSAGE.Replace("{TaskType}", taskType);
            }

            return new TaskStatusWriter(tasks, Log, progressFormatMessage, PluginLibConstants.DEFAULT_PROGRESS_MONITOR_POLLING_INTERVAL_SECONDS, expectedTotalTasks);
        }

        protected virtual bool PersistedData()
        {
            foreach (var persistableType in RecordsPersisted.Keys)
            {
                if (RecordsPersisted[persistableType] > 0)
                {
                    return true;
                }
            }

            return false;
        } 
    }
}
